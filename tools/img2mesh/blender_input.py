"""Render one existing prop (FBX/GLB/OBJ) to a clean white-background image.

Used to synthesise img2mesh inputs from the project's parametric props, so the
image->3D round trip can be compared against a known-good model.

  blender.exe -b -P tools\\img2mesh\\blender_input.py -- ^
      --mesh "F:\\Workspace\\ESCAPE FROM WORK\\game\\Assets\\_Project\\Art\\Props\\P_crate_stack.fbx" ^
      --out  "F:\\Workspace\\ESCAPE FROM WORK\\tools\\img2mesh\\inputs\\crate_stack.png"

Renders RGBA (transparent film) then composites over pure white with Pillow, so
the silhouette is exact and no shadow bleeds into the alpha edge.
"""

import argparse
import math
import os
import sys

import bpy
import mathutils


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def import_mesh(path):
    ext = os.path.splitext(path)[1].lower()
    if ext in (".glb", ".gltf"):
        bpy.ops.import_scene.gltf(filepath=path)
    elif ext == ".obj":
        bpy.ops.wm.obj_import(filepath=path)
    elif ext == ".fbx":
        bpy.ops.import_scene.fbx(filepath=path)
    else:
        raise ValueError(path)
    return [o for o in bpy.context.scene.objects if o.type == "MESH"]


def join_all(meshes):
    if len(meshes) == 1:
        return meshes
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.join()
    return [bpy.context.view_layer.objects.active]


def normalize(meshes, target=1.6):
    mins = mathutils.Vector((1e9,) * 3)
    maxs = mathutils.Vector((-1e9,) * 3)
    for o in meshes:
        for corner in o.bound_box:
            w = o.matrix_world @ mathutils.Vector(corner)
            for i in range(3):
                mins[i] = min(mins[i], w[i])
                maxs[i] = max(maxs[i], w[i])
    center = (mins + maxs) / 2
    size = max((maxs - mins).x, (maxs - mins).y, (maxs - mins).z)
    scale = target / size if size else 1.0
    for o in meshes:
        o.location = (o.location - center) * scale
        o.scale = o.scale * scale
    bpy.context.view_layer.update()


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    ap = argparse.ArgumentParser()
    ap.add_argument("--mesh", required=True)
    ap.add_argument("--out", required=True, help="absolute output png path")
    ap.add_argument("--res", type=int, default=512)
    ap.add_argument("--samples", type=int, default=48)
    ap.add_argument("--az", type=float, default=0.0)
    ap.add_argument("--el", type=float, default=78.0)
    args = ap.parse_args(argv)

    clear_scene()
    meshes = join_all(import_mesh(args.mesh))
    normalize(meshes)

    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = args.samples
    scene.cycles.use_denoising = True
    scene.render.resolution_x = args.res
    scene.render.resolution_y = args.res
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True
    scene.view_settings.view_transform = "Standard"
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"

    world = bpy.data.worlds.new("W")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs[0].default_value = (0.6, 0.6, 0.6, 1.0)
    bg.inputs[1].default_value = 1.0

    for loc, energy, size in (
        ((3.0, -3.5, 4.0), 130.0, 4.0),
        ((-3.5, -2.0, 1.5), 55.0, 5.0),
        ((0.0, 4.0, 3.0), 80.0, 4.0),
    ):
        ld = bpy.data.lights.new("l", type="AREA")
        ld.energy = energy
        ld.size = size
        lo = bpy.data.objects.new("l", ld)
        lo.location = loc
        lo.rotation_euler = (math.radians(55), 0, math.radians(25))
        scene.collection.objects.link(lo)

    cam_data = bpy.data.cameras.new("cam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = 2.05
    cam = bpy.data.objects.new("cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    az_r, el_r = math.radians(args.az), math.radians(args.el)
    dist = 6.0
    cam.location = (
        dist * math.sin(az_r) * math.sin(el_r),
        -dist * math.cos(az_r) * math.sin(el_r),
        dist * math.cos(el_r),
    )
    cam.rotation_euler = (mathutils.Vector((0.0, 0.0, 0.0)) - cam.location).to_track_quat("-Z", "Y").to_euler()

    tmp = args.out
    scene.render.filepath = tmp
    bpy.ops.render.render(write_still=True)
    print(f"WROTE {args.out} (RGBA, transparent background)", flush=True)


if __name__ == "__main__":
    main()
