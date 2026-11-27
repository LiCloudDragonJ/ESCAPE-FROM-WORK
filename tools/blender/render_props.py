"""道具抽查渲染：把导出的 FBX 重新导入 Blender，按统一 3/4 视角渲成 512×512 灰底 PNG。

这一步是"验收"而不是"出图"：渲染用的是 game/Assets/_Project/Art/Props 里真正导出的
FBX（不是内存里的生成结果），所以能同时验证 1) 导出没丢面 2) pivot 在底面中心
3) 尺度（1 unit = 1 m）4) 朝向（正面 -Y / Unity +Z 是否对着镜头右前方）。

用法（在仓库根执行）：
  & "F:\Tools\blender\blender-4.2.9-windows-x64\blender.exe" -b -P tools/blender/render_props.py -- ^
      --props conference_chair,forklift,washbasin ^
      --out "docs/3d-rebuild/screenshots/props" --size 512

参数：
  --props  逗号分隔的 id（与 P_<id>.fbx 一致）
  --out    PNG 输出目录（默认 docs/3d-rebuild/screenshots/props）
  --fbx    FBX/JSON 目录（默认 game/Assets/_Project/Art/Props）
  --size   边长像素（默认 512）
  --samples Cycles 采样数（默认 32）

每张图旁边打印一行 [RENDER] 校验信息：网格数、三角面、包围盒、最低点，
以及 FBX 是否存在、sidecar 里的 facing 字段——报告里直接引用这些数字。
"""

import bpy
import json
import math
import os
import sys


def parse_args():
    argv = sys.argv
    argv = argv[argv.index("--") + 1:] if "--" in argv else []
    props, out, fbx_dir, size, samples = None, None, None, 512, 32
    for i, a in enumerate(argv):
        if a == "--props" and i + 1 < len(argv):
            props = [x.strip() for x in argv[i + 1].split(",") if x.strip()]
        elif a == "--out" and i + 1 < len(argv):
            out = os.path.abspath(argv[i + 1])
        elif a == "--fbx" and i + 1 < len(argv):
            fbx_dir = os.path.abspath(argv[i + 1])
        elif a == "--size" and i + 1 < len(argv):
            size = int(argv[i + 1])
        elif a == "--samples" and i + 1 < len(argv):
            samples = int(argv[i + 1])
    if out is None:
        out = os.path.abspath("docs/3d-rebuild/screenshots/props")
    if fbx_dir is None:
        fbx_dir = os.path.abspath("game/Assets/_Project/Art/Props")
    return props, out, fbx_dir, size, samples


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.cameras,
                  bpy.data.lights, bpy.data.images):
        for b in list(block):
            if b.users == 0:
                block.remove(b)


def make_grey_world(scene):
    world = bpy.data.worlds.get("PropCheckWorld") or bpy.data.worlds.new("PropCheckWorld")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    bg.inputs[0].default_value = (0.36, 0.36, 0.36, 1.0)  # 纯灰背景
    bg.inputs[1].default_value = 1.0


def add_area(name, loc, target, energy, size):
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.size = size
    ob = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(ob)
    ob.location = loc
    direction = target - ob.location
    ob.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    return ob


def mesh_bounds(objs):
    pts = []
    for ob in objs:
        for c in ob.bound_box:
            pts.append(ob.matrix_world @ __import__("mathutils").Vector(c))
    lo = [min(p[i] for p in pts) for i in range(3)]
    hi = [max(p[i] for p in pts) for i in range(3)]
    return lo, hi


def render_one(prop_id, fbx_dir, out_dir, size, samples):
    fbx = os.path.join(fbx_dir, "P_%s.fbx" % prop_id)
    side = os.path.join(fbx_dir, "P_%s.json" % prop_id)
    if not os.path.exists(fbx):
        print("[RENDER] MISSING %s" % fbx)
        return None
    meta = {}
    if os.path.exists(side):
        with open(side, "r", encoding="utf-8") as f:
            meta = json.load(f)

    scene = bpy.context.scene
    clear_scene()
    make_grey_world(scene)

    bpy.ops.import_scene.fbx(filepath=fbx)
    objs = [o for o in bpy.context.selected_objects if o.type == "MESH"]
    if not objs:
        print("[RENDER] EMPTY %s（FBX 里没有 mesh）" % prop_id)
        return None
    tris = sum(len(p.vertices) - 2 for o in objs for p in o.data.polygons)
    lo, hi = mesh_bounds(objs)
    dims = [hi[i] - lo[i] for i in range(3)]
    center = [(lo[i] + hi[i]) * 0.5 for i in range(3)]
    span = max(dims)

    # 地面：接阴影，颜色比背景略深，避免"悬空"错觉
    bpy.ops.mesh.primitive_plane_add(size=max(span * 14.0, 4.0),
                                     location=(center[0], center[1], lo[2] - 0.002))
    floor = bpy.context.active_object
    floor.name = "CheckFloor"
    fmat = bpy.data.materials.new("M_CheckFloor")
    fmat.use_nodes = True
    fmat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (
        0.30, 0.30, 0.30, 1.0)
    fmat.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.9
    floor.data.materials.append(fmat)

    # 统一 3/4 视角：从正面（-Y）偏右（+X）看，等于 Unity 里站在道具正面右侧 45°
    direction = __import__("mathutils").Vector((0.92, -1.55, 0.80)).normalized()
    cam_data = bpy.data.cameras.new("CheckCam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = span * 1.22
    cam = bpy.data.objects.new("CheckCam", cam_data)
    scene.collection.objects.link(cam)
    cam.location = (center[0] + direction.x * span * 4.0,
                    center[1] + direction.y * span * 4.0,
                    center[2] + direction.z * span * 4.0)
    aim = __import__("mathutils").Vector((center[0], center[1], lo[2] + dims[2] * 0.45))
    cam.rotation_euler = (aim - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam

    target = __import__("mathutils").Vector((center[0], center[1], lo[2] + dims[2] * 0.5))
    add_area("Key", (center[0] - span * 2.2, center[1] - span * 2.6, lo[2] + span * 2.6),
             target, 260.0 * span, span * 2.0)
    add_area("Fill", (center[0] + span * 2.8, center[1] - span * 1.8, lo[2] + span * 1.8),
             target, 120.0 * span, span * 2.4)
    add_area("Rim", (center[0] + span * 0.6, center[1] + span * 3.0, lo[2] + span * 2.4),
             target, 180.0 * span, span * 2.0)
    sun_data = bpy.data.lights.new("CheckSun", "SUN")
    sun_data.energy = 2.0
    sun_data.angle = math.radians(10.0)
    sun = bpy.data.objects.new("CheckSun", sun_data)
    scene.collection.objects.link(sun)
    sun.rotation_euler = (math.radians(50.0), 0.0, math.radians(-40.0))

    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = samples
    scene.cycles.use_denoising = True
    scene.cycles.max_bounces = 4
    scene.render.resolution_x = size
    scene.render.resolution_y = size
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = False
    scene.render.image_settings.file_format = "PNG"
    png = os.path.join(out_dir, "P_%s.png" % prop_id)
    os.makedirs(out_dir, exist_ok=True)
    scene.render.filepath = png
    bpy.ops.render.render(write_still=True)

    print("[RENDER] %-18s meshes=%-2d tris=%-5d bbox=%.2f x %.2f x %.2f m min_z=%+.3f "
          "facing=%s -> %s"
          % (prop_id, len(objs), tris, dims[0], dims[1], dims[2], lo[2],
             meta.get("facing", "-"), png))
    return {"id": prop_id, "tris": tris, "dims": dims, "min_z": lo[2], "png": png}


def main():
    props, out_dir, fbx_dir, size, samples = parse_args()
    if not props:
        print("[RENDER] 需要 --props a,b,c")
        sys.exit(2)
    rows = []
    for pid in props:
        r = render_one(pid, fbx_dir, out_dir, size, samples)
        if r:
            rows.append(r)
    print("[RENDER] done %d/%d -> %s" % (len(rows), len(props), out_dir))


if __name__ == "__main__":
    main()
