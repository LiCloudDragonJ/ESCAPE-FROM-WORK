"""Minimal TripoSR runner for the ESCAPE FROM WORK img2mesh feasibility study.

Wraps the upstream TripoSR pipeline (src/TripoSR) with:
  - white-background / alpha input handling (no rembg model download needed)
  - per-stage timing and peak VRAM reporting
  - OBJ / GLB export, optional xatlas texture bake

Usage:
  F:\\ComfyUI\\venv\\Scripts\\python.exe tools\\img2mesh\\run_tsr.py ^
      --image tools\\img2mesh\\inputs\\chair.png --out tools\\img2mesh\\out\\chair ^
      --mc-resolution 256
"""

import argparse
import json
import math
import os
import sys
import time

import numpy as np
import torch
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "src", "TripoSR"))

from tsr.system import TSR  # noqa: E402
from tsr.utils import resize_foreground  # noqa: E402


def log(msg):
    print(f"[{time.strftime('%H:%M:%S')}] {msg}", flush=True)


def load_rgba(path):
    """Return RGBA image; for opaque images, treat near-white pixels as background."""
    im = Image.open(path)
    if im.mode == "RGBA":
        rgba = im
    else:
        rgba = im.convert("RGBA")
        arr = np.array(rgba)
        near_white = (arr[:, :, :3] >= 244).all(axis=2)
        arr[:, :, 3] = np.where(near_white, 0, 255).astype(np.uint8)
        rgba = Image.fromarray(arr)
    return rgba


def prep_input(path, out_png, fg_ratio=0.85):
    rgba = load_rgba(path)
    rgba = resize_foreground(rgba, fg_ratio)
    arr = np.array(rgba).astype(np.float32) / 255.0
    rgb = arr[:, :, :3] * arr[:, :, 3:4] + (1 - arr[:, :, 3:4]) * 0.5
    img = Image.fromarray((rgb * 255.0).astype(np.uint8))
    os.makedirs(os.path.dirname(out_png), exist_ok=True)
    img.save(out_png)
    return img


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--image", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--weights", default=os.path.join(HERE, "weights", "TripoSR"))
    ap.add_argument("--device", default="cuda:0")
    ap.add_argument("--mc-resolution", type=int, default=256)
    ap.add_argument("--chunk-size", type=int, default=8192)
    ap.add_argument("--fg-ratio", type=float, default=0.85)
    ap.add_argument("--format", default="glb", choices=["glb", "obj"])
    ap.add_argument("--bake-texture", action="store_true")
    ap.add_argument("--texture-resolution", type=int, default=2048)
    ap.add_argument("--target-faces", type=int, default=0, help="quadric-decimate to N tris before baking")
    ap.add_argument("--tag", default="")
    args = ap.parse_args()

    os.makedirs(args.out, exist_ok=True)
    metrics = {"image": args.image, "mc_resolution": args.mc_resolution, "chunk_size": args.chunk_size}

    t0 = time.time()
    input_path = os.path.join(args.out, "input.png")
    img = prep_input(args.image, input_path, args.fg_ratio)
    metrics["prep_s"] = round(time.time() - t0, 3)
    log(f"input prepared -> {input_path}")

    t = time.time()
    model = TSR.from_pretrained(args.weights, config_name="config.yaml", weight_name="model.ckpt")
    model.renderer.set_chunk_size(args.chunk_size)
    model.to(args.device)
    metrics["model_load_s"] = round(time.time() - t, 2)
    metrics["params_m"] = round(sum(p.numel() for p in model.parameters()) / 1e6, 1)
    log(f"model loaded in {metrics['model_load_s']}s, {metrics['params_m']}M params")

    torch.cuda.reset_peak_memory_stats()
    t = time.time()
    with torch.no_grad():
        scene_codes = model([img], device=args.device)
    if args.device.startswith("cuda"):
        torch.cuda.synchronize()
    metrics["forward_s"] = round(time.time() - t, 2)
    metrics["vram_after_forward_gb"] = round(torch.cuda.max_memory_allocated() / 2**30, 2)
    log(f"forward {metrics['forward_s']}s, peak alloc {metrics['vram_after_forward_gb']} GB")

    t = time.time()
    meshes = model.extract_mesh(scene_codes, not args.bake_texture, resolution=args.mc_resolution)
    if args.device.startswith("cuda"):
        torch.cuda.synchronize()
    metrics["extract_s"] = round(time.time() - t, 2)
    metrics["vram_peak_gb"] = round(torch.cuda.max_memory_allocated() / 2**30, 2)
    mesh = meshes[0]
    metrics["verts"] = int(len(mesh.vertices))
    metrics["faces"] = int(len(mesh.faces))
    log(f"extract {metrics['extract_s']}s -> {metrics['verts']} verts / {metrics['faces']} tris, peak alloc {metrics['vram_peak_gb']} GB")

    # NOTE(frame): keep the mesh in TripoSR's native Z-up frame until AFTER the
    # texture bake - bake_texture queries the triplane with the mesh's vertex
    # positions, so rotating first samples the wrong 3D locations and yields
    # garbage colors. The Y-up rotation happens below, right before export.

    if args.target_faces > 0 and len(mesh.faces) > args.target_faces:
        t = time.time()
        src_faces = len(mesh.faces)
        mesh = mesh.simplify_quadric_decimation(face_count=args.target_faces)
        metrics["decimate_s"] = round(time.time() - t, 2)
        metrics["faces_src"] = src_faces
        metrics["verts"] = int(len(mesh.vertices))
        metrics["faces"] = int(len(mesh.faces))
        log(f"decimated {src_faces} -> {metrics['faces']} tris in {metrics['decimate_s']}s")

    if args.bake_texture:
        from tsr.bake_texture import bake_texture

        t = time.time()
        bake = bake_texture(mesh, model, scene_codes[0], args.texture_resolution)
        metrics["bake_s"] = round(time.time() - t, 2)
        import xatlas

        tex_path = os.path.join(args.out, "texture.png")
        mesh_path = os.path.join(args.out, f"mesh.{args.format}")
        # rotate to Y-up AFTER baking (both uv atlas and vertex mapping stay valid)
        import trimesh.transformations as tf

        mesh.apply_transform(tf.rotation_matrix(-math.pi / 2, [1, 0, 0]))
        xatlas.export(
            mesh_path,
            mesh.vertices[bake["vmapping"]],
            bake["indices"],
            bake["uvs"],
            mesh.vertex_normals[bake["vmapping"]],
        )
        Image.fromarray((bake["colors"] * 255.0).astype(np.uint8)).transpose(Image.FLIP_TOP_BOTTOM).save(tex_path)
        metrics["texture"] = tex_path
        log(f"baked texture {args.texture_resolution}px in {metrics['bake_s']}s")
    else:
        import trimesh.transformations as tf

        mesh.apply_transform(tf.rotation_matrix(-math.pi / 2, [1, 0, 0]))
        mesh_path = os.path.join(args.out, f"mesh.{args.format}")
        mesh.export(mesh_path)

    metrics["mesh"] = mesh_path
    metrics["mesh_mb"] = round(os.path.getsize(mesh_path) / 1e6, 2)
    if args.device.startswith("cuda"):
        metrics["vram_peak_final_gb"] = round(torch.cuda.max_memory_allocated() / 2**30, 2)
    metrics["total_s"] = round(sum(v for k, v in metrics.items() if k.endswith("_s") and isinstance(v, (int, float))), 2)
    with open(os.path.join(args.out, "metrics.json"), "w", encoding="utf-8") as f:
        json.dump(metrics, f, indent=2, ensure_ascii=False)
    log(f"wrote {mesh_path} ({metrics['mesh_mb']} MB)")
    print(json.dumps(metrics, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
