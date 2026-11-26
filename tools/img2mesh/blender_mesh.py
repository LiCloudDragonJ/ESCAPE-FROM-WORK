"""Headless Blender helper for the img2mesh study.

Two jobs:
  1. render    : import a mesh (glb/obj/fbx), render N orthographic views on a
                 neutral gray background for visual QA against the input image.
  2. tofbx     : import a mesh, optionally decimate to a triangle budget, then
                 export FBX (and a decimated GLB/OBJ for reference) for Unity.

Run with:
  "F:\\Tools\\blender\\blender-4.2.9-windows-x64\\blender.exe" -b -P tools\\img2mesh\\blender_mesh.py -- render --mesh a.glb --out out\\chair
  "F:\\Tools\\blender\\blender-4.2.9-windows-x64\\blender.exe" -b -P tools\\img2mesh\\blender_mesh.py -- tofbx --mesh a.glb --out out\\chair --tris 1500
"""

import argparse
import math
import os
import sys

import bpy
import mathutils

ANGLE_PRESETS = {
    "front": (0.0, 78.0),
    "q34": (35.0, 72.0),
    "side": (90.0, 78.0),
    "back": (180.0, 78.0),
}


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def import_mesh(path):
    ext = os.path.splitext(path)[1].lower()
    if ext == ".glb" or ext == ".gltf":
        bpy.ops.import_scene.gltf(filepath=path)
    elif ext == ".obj":
        bpy.ops.wm.obj_import(filepath=path)
    elif ext == ".fbx":
        bpy.ops.import_scene.fbx(filepath=path)
    else:
        raise ValueError(f"unsupported mesh: {path}")
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    return meshes


def scene_stats(meshes):
    tris = 0
    verts = 0
    for o in meshes:
        me = o.data
        me.calc_loop_triangles()
        tris += len(me.loop_triangles)
        verts += len(me.vertices)
    return verts, tris


def normalize(meshes, target=1.6):
    # join-ish bounds in world space
    mins = mathutils.Vector((1e9, 1e9, 1e9))
    maxs = mathutils.Vector((-1e9, -1e9, -1e9))
    for o in meshes:
        for corner in o.bound_box:
            w = o.matrix_world @ mathutils.Vector(corner)
            mins = mathutils.Vector((min(mins[i], w[i]) for i in range(3)))
            maxs = mathutils.Vector((max(maxs[i], w[i]) for i in range(3)))
    center = (mins + maxs) / 2
    size = max((maxs - mins).x, (maxs - mins).y, (maxs - mins).z)
    scale = target / size if size > 0 else 1.0
    for o in meshes:
        o.location = (o.location - center) * scale
        o.scale = o.scale * scale
    bpy.context.view_layer.update()


def standup(meshes):
    """Rotate -90 deg about X (applied) so a Z-up GLB stands in Blender's Z-up.
    The glTF importer leaves objects in QUATERNION mode, so set the mode first."""
    for o in meshes:
        o.rotation_mode = "XYZ"
        o.rotation_euler = (math.radians(-90), 0.0, 0.0)
    for o in meshes:
        bpy.context.view_layer.objects.active = o
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    bpy.context.view_layer.update()


def report_materials(meshes):
    for o in meshes:
        me = o.data
        cols = [a.name for a in me.color_attributes]
        print(f"MESH {o.name}: verts={len(me.vertices)} polys={len(me.polygons)} color_attrs={cols} materials={[m.name if m else None for m in me.materials]}", flush=True)


def ensure_vertex_color_material(meshes):
    """glTF with COLOR_0 but no material imports as bare white. Build a Principled
    BSDF that reads the Color attribute so the QA render shows TripoSR's colors."""
    for o in meshes:
        if o.data.materials:
            continue
        if not o.data.color_attributes:
            continue
        me = o.data
        mat = bpy.data.materials.new(name=f"{o.name}_vcol")
        mat.use_nodes = True
        nt = mat.node_tree
        bsdf = nt.nodes.get("Principled BSDF")
        attr = nt.nodes.new("ShaderNodeVertexColor")
        attr.layer_name = me.color_attributes[0].name
        # glTF COLOR_0 is linear, the values in this GLB are sRGB-ish bytes, so
        # apply gamma 2.2 to keep the QA render from looking washed out.
        gamma = nt.nodes.new("ShaderNodeGamma")
        gamma.inputs["Gamma"].default_value = 2.2
        nt.links.new(attr.outputs["Color"], gamma.inputs["Color"])
        nt.links.new(gamma.outputs["Color"], bsdf.inputs["Base Color"])
        bsdf.inputs["Roughness"].default_value = 0.65
        me.materials.append(mat)
        print(f"MATERIAL {o.name}: vertex-color -> Principled", flush=True)


def pivot_bottom(meshes):
    """Move the pivot to the bottom-center of the bbox so Unity can drop the prop
    on the floor (TripoSR output is centred on the bbox middle)."""
    mins = mathutils.Vector((1e9, 1e9, 1e9))
    maxs = mathutils.Vector((-1e9, -1e9, -1e9))
    for o in meshes:
        for corner in o.bound_box:
            w = o.matrix_world @ mathutils.Vector(corner)
            for i in range(3):
                mins[i] = min(mins[i], w[i])
                maxs[i] = max(maxs[i], w[i])
    delta = mathutils.Vector(((mins.x + maxs.x) / 2, (mins.y + maxs.y) / 2, mins.z))
    for o in meshes:
        o.location = o.location - delta
    bpy.context.view_layer.update()


def face_forward(meshes):
    """TripoSR reconstructs a front-view input facing +X; rotate -90 about Z so the
    asset faces -Y in Blender (which becomes +Z forward after FBX -> Unity).
    Pre-multiplies matrix_world: the OBJ importer already stores +90deg X as an
    object rotation, so overwriting rotation_euler would tip the asset over."""
    rot = mathutils.Euler((0.0, 0.0, math.radians(-90)), "XYZ").to_matrix().to_4x4()
    for o in meshes:
        o.matrix_world = rot @ o.matrix_world
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    bpy.context.view_layer.update()


def make_textured_material(o, texture_path):
    me = o.data
    mat = bpy.data.materials.new(name=f"{o.name}_baked")
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    img = bpy.data.images.load(texture_path)
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = img
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.65
    me.materials.append(mat)
    print(f"MATERIAL {o.name}: baked texture {os.path.basename(texture_path)}", flush=True)


def setup_render(res, samples=32):
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = samples
    scene.cycles.use_denoising = True
    scene.render.resolution_x = res
    scene.render.resolution_y = res
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = False
    scene.view_settings.view_transform = "Standard"

    world = bpy.data.worlds.new("W")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs[0].default_value = (0.5, 0.5, 0.5, 1.0)
    bg.inputs[1].default_value = 1.0

    # key + fill + rim
    for name, loc, energy, size in (
        ("key", (3.0, -3.5, 4.0), 120.0, 4.0),
        ("fill", (-3.5, -2.0, 1.5), 45.0, 5.0),
        ("rim", (0.0, 4.0, 3.0), 70.0, 4.0),
    ):
        light_data = bpy.data.lights.new(name, type="AREA")
        light_data.energy = energy
        light_data.size = size
        obj = bpy.data.objects.new(name, light_data)
        obj.location = loc
        obj.rotation_euler = (math.radians(55), 0, math.radians(25))
        scene.collection.objects.link(obj)


def render_views(prefix, res, samples):
    scene = bpy.context.scene
    cam_data = bpy.data.cameras.new("cam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = 2.1
    cam = bpy.data.objects.new("cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam

    for name, (az, el) in ANGLE_PRESETS.items():
        az_r, el_r = math.radians(az), math.radians(el)
        dist = 6.0
        cam.location = (
            dist * math.sin(az_r) * math.sin(el_r),
            -dist * math.cos(az_r) * math.sin(el_r),
            dist * math.cos(el_r),
        )
        direction = mathutils.Vector((0, 0, 0)) - cam.location
        cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = f"{prefix}_{name}.png"
        bpy.ops.render.render(write_still=True)
        print(f"RENDERED {scene.render.filepath}", flush=True)


def bbox_dims(meshes):
    mins = mathutils.Vector((1e9, 1e9, 1e9))
    maxs = mathutils.Vector((-1e9, -1e9, -1e9))
    for o in meshes:
        for corner in o.bound_box:
            w = o.matrix_world @ mathutils.Vector(corner)
            mins = mathutils.Vector((min(mins[i], w[i]) for i in range(3)))
            maxs = mathutils.Vector((max(maxs[i], w[i]) for i in range(3)))
    d = maxs - mins
    return d.x, d.y, d.z


def decimate(meshes, target_tris):
    verts, tris = scene_stats(meshes)
    if tris <= target_tris:
        print(f"NO DECIMATION NEEDED: {tris} tris <= {target_tris}", flush=True)
        return verts, tris
    ratio = target_tris / tris
    for o in meshes:
        mod = o.modifiers.new(name="decimate", type="DECIMATE")
        mod.ratio = ratio
        mod.use_collapse_triangulate = True
    # apply
    for o in meshes:
        bpy.context.view_layer.objects.active = o
        for mod in list(o.modifiers):
            bpy.ops.object.modifier_apply(modifier=mod.name)
    return scene_stats(meshes)


def export_fbx(path):
    # join to a single object for a clean game asset
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.name = os.path.splitext(os.path.basename(path))[0]
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        apply_scale_options="FBX_SCALE_ALL",
        mesh_smooth_type="FACE",
        bake_space_transform=True,
        path_mode="COPY",
    )
    print(f"EXPORTED {path}", flush=True)


def export_glb(path):
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True)


def main():
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else sys.argv[1:]
    ap = argparse.ArgumentParser()
    ap.add_argument("cmd", choices=["render", "tofbx", "texture_fbx"])
    ap.add_argument("--mesh", required=True)
    ap.add_argument("--out", required=True, help="output prefix (no extension for render)")
    ap.add_argument("--texture", help="texture png for texture_fbx")
    ap.add_argument("--res", type=int, default=512)
    ap.add_argument("--samples", type=int, default=32)
    ap.add_argument("--tris", type=int, default=1500)
    ap.add_argument("--standup", action="store_true", help="rotate -90deg X (Z-up GLB -> Blender Z-up)")
    ap.add_argument("--face-forward", action="store_true", help="rotate -90deg Z so a +X-facing asset faces -Y")
    ap.add_argument("--pivot-bottom", action="store_true", help="move pivot to bottom-center")
    args = ap.parse_args(argv)

    clear_scene()
    meshes = import_mesh(args.mesh)
    print(f"IMPORTED {args.mesh}: {len(meshes)} mesh objects, stats={scene_stats(meshes)}", flush=True)
    report_materials(meshes)
    if args.standup:
        standup(meshes)
        print(f"AFTER STANDUP bbox XYZ: {bbox_dims(meshes)}", flush=True)

    if args.cmd == "render":
        ensure_vertex_color_material(meshes)
        normalize(meshes)
        setup_render(args.res, args.samples)
        render_views(args.out, args.res, args.samples)
    else:
        dims = bbox_dims(meshes)
        print(f"NATIVE BBOX XYZ: {dims[0]:.3f} x {dims[1]:.3f} x {dims[2]:.3f}", flush=True)
        if args.face_forward:
            face_forward(meshes)
            print(f"AFTER FACE-FORWARD bbox XYZ: {bbox_dims(meshes)}", flush=True)
        v, t = decimate(meshes, args.tris)
        print(f"AFTER DECIMATE: verts={v} tris={t}", flush=True)
        if args.pivot_bottom:
            pivot_bottom(meshes)
            print(f"AFTER PIVOT-BOTTOM bbox XYZ: {bbox_dims(meshes)}", flush=True)
        if args.cmd == "texture_fbx":
            if not args.texture:
                raise SystemExit("--texture required for texture_fbx")
            for o in meshes:
                make_textured_material(o, args.texture)
        export_fbx(args.out + ".fbx")
        if args.cmd == "tofbx":
            export_glb(args.out + "_decimated.glb")
        if args.cmd == "texture_fbx":
            normalize(meshes)
            setup_render(args.res, args.samples)
            render_views(args.out + "_render", args.res, args.samples)


if __name__ == "__main__":
    main()
