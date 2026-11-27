"""ESCAPE FROM WORK 道具模型生成器（Blender 4.2 headless）

用法（在仓库根执行）：
  & "F:\Tools\Blender\blender-4.2.9-windows-x64\blender.exe" -b -P tools/blender/gen_props.py -- ^
      --out "game/Assets/_Project/Art/Props" --only cabinet_group,server_rack

每个道具导出两个文件：
  P_<id>.fbx   —— pivot 在底面中心、正面朝 +Z、1 unit = 1 m，Unity 直接拖进场景
  P_<id>.json  —— 占地/高度/面数/材质槽，PropImporter 用它生成 prefab 与填 PropCatalog

只重导某一批（不覆盖已有 FBX/JSON，清单里其他条目原样保留）：
  & "F:\Tools\Blender\blender-4.2.9-windows-x64\blender.exe" -b -P tools/blender/gen_props.py -- ^
      --only conference_chair,file_cabinet,... --out "game/Assets/_Project/Art/Props"

抽查单件渲染（重新导入 FBX 再渲 512×512，灰底 3/4 视角）：
  & "F:\Tools\Blender\blender-4.2.9-windows-x64\blender.exe" -b -P tools/blender/render_props.py -- ^
      --props desk,conference_chair --out "docs/3d-rebuild/screenshots/props" --size 512

设计约束（对 04-3d-assets.md）：
  - 大件 ≤1500 三角面、中件 ≤900、小件 ≤300；
  - 只用 box / cylinder 两种基元 + bevel 倒角，保证低面数且风格统一（办公/设施混搭的白盒质感）；
  - 每件道具最多两个材质槽：body（主体）/ accent（强调面），槽名写进 json，
    Unity 侧按槽名换 19 套 PBR（未接时回退灰盒）。
"""

import bpy
import json
import math
import os
import sys

# ---------------------------------------------------------------------------
# 参数解析（Blender 会把 "--" 之后的参数留在 sys.argv）
# ---------------------------------------------------------------------------


def parse_args():
    argv = sys.argv
    if "--" not in argv:
        argv = []
    else:
        argv = argv[argv.index("--") + 1:]

    out = os.path.abspath("game/Assets/_Project/Art/Props")
    only = None
    render = None
    for i, a in enumerate(argv):
        if a == "--out" and i + 1 < len(argv):
            out = os.path.abspath(argv[i + 1])
        elif a == "--only" and i + 1 < len(argv):
            only = set(x.strip() for x in argv[i + 1].split(",") if x.strip())
        elif a == "--render" and i + 1 < len(argv):
            render = os.path.abspath(argv[i + 1])
    return out, only, render


# ---------------------------------------------------------------------------
# 基元
# ---------------------------------------------------------------------------

def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.objects):
        for item in list(block):
            if item.users == 0:
                block.remove(item)


def material(name, rgba):
    mat = bpy.data.materials.get(name)
    if mat:
        return mat
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = rgba
        bsdf.inputs["Roughness"].default_value = 0.55
        bsdf.inputs["Metallic"].default_value = 0.15
    return mat


def box(name, size, loc, mat, bevel=0.02, rot_z=0.0, segments=2):
    """建一个立方体：size=(x,y,z)，loc 是中心点。

    segments 是倒角段数：2 段 108 tris / 1 段 44 tris / 0 段 12 tris（每个盒子）。
    小件道具（≤300 tris）靠 0/1 段倒角控制面数，原 21 个道具全部用默认 2 段，行为不变。
    """
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc)
    ob = bpy.context.active_object
    ob.name = name
    ob.scale = (size[0], size[1], size[2])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if rot_z:
        ob.rotation_euler[2] = rot_z
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    if bevel > 0:
        mod = ob.modifiers.new("bevel", "BEVEL")
        mod.width = bevel
        mod.segments = segments
        mod.limit_method = "ANGLE"
        mod.angle_limit = math.radians(40)
        bpy.ops.object.modifier_apply(modifier=mod.name)
    ob.data.materials.append(mat)
    return ob


def cyl(name, radius, depth, loc, mat, sides=16, bevel=0.015, segments=1):
    bpy.ops.mesh.primitive_cylinder_add(vertices=sides, radius=radius, depth=depth, location=loc)
    ob = bpy.context.active_object
    ob.name = name
    if bevel > 0:
        mod = ob.modifiers.new("bevel", "BEVEL")
        mod.width = bevel
        mod.segments = segments
        bpy.ops.object.modifier_apply(modifier=mod.name)
    ob.data.materials.append(mat)
    return ob


def rot(ob, axis, deg):
    """把刚建好的零件绕某个轴旋转并 apply（0=X, 1=Y, 2=Z）。"""
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    ob.rotation_euler[axis] = math.radians(deg)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    return ob


def join(objs, name):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    ob = bpy.context.active_object
    ob.name = name
    return ob


def finish(ob, name):
    """把 mesh 数据平移到"底面中心在原点"，并统一法线。

    先把物体的 location/rotation/scale 全部 apply 进网格，再做包围盒平移；
    否则 join 后的活动物体 location 不为 0，旧写法会把 pivot 偏移 -location.z
    （道具会陷进地板一半）。
    """
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.ops.object.mode_set(mode="OBJECT")
    # 计算包围盒（此时矩阵为单位阵，local 即 world）
    bb = [ob.matrix_world @ mathutils.Vector(c) for c in ob.bound_box]
    min_z = min(v.z for v in bb)
    cx = (min(v.x for v in bb) + max(v.x for v in bb)) * 0.5
    cy = (min(v.y for v in bb) + max(v.y for v in bb)) * 0.5
    for v in ob.data.vertices:
        v.co.x -= cx
        v.co.y -= cy
        v.co.z -= min_z
    ob.location = (0.0, 0.0, 0.0)
    # 合并重复顶点 + 平滑无关（保持硬边）
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.remove_doubles(threshold=0.0005)
    bpy.ops.mesh.normals_make_consistent(inside=False)
    # 立方体投影 UV：1 个 UV 单位 = 1 米，Unity 侧材质 tiling 才能按真实尺度铺（否则每个面都是 0–1，
    # 木纹被放大成一整块，花纹还会在相邻面上跳）。
    if not ob.data.uv_layers:
        ob.data.uv_layers.new(name="UVMap")
    bpy.ops.uv.cube_project(cube_size=1.0, correct_aspect=True, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    ob.name = name
    return ob


# ---------------------------------------------------------------------------
# 道具生成器：每个返回 (objs, footprint, height, size_class, slots)
#   slots: {"body": 材质名后缀, "accent": ...}
# ---------------------------------------------------------------------------

_MATERIAL_DEFS = {
    "M_PropBody": (0.62, 0.62, 0.60, 1.0),
    "M_PropDark": (0.28, 0.30, 0.32, 1.0),
    "M_PropAccent": (0.72, 0.55, 0.22, 1.0),
    "M_PropSteel": (0.55, 0.58, 0.60, 1.0),
    "M_PropWhite": (0.86, 0.87, 0.86, 1.0),
}

BODY = DARK = ACCENT = STEEL = WHITE = None
GLASS_MAT = BOTTLE_MAT = LINEN_MAT = LEAF_MAT = None


def ensure_materials():
    """
    素材必须在 clear_scene() 之后再建：clear_scene 会把 users==0 的材质块删掉，
    模块导入时创建的那批 Material 会变成 "StructRNA of type Material has been removed"。
    """
    global BODY, DARK, ACCENT, STEEL, WHITE, GLASS_MAT, BOTTLE_MAT, LINEN_MAT, LEAF_MAT
    BODY = material("M_PropBody", _MATERIAL_DEFS["M_PropBody"])
    DARK = material("M_PropDark", _MATERIAL_DEFS["M_PropDark"])
    ACCENT = material("M_PropAccent", _MATERIAL_DEFS["M_PropAccent"])
    STEEL = material("M_PropSteel", _MATERIAL_DEFS["M_PropSteel"])
    WHITE = material("M_PropWhite", _MATERIAL_DEFS["M_PropWhite"])
    GLASS_MAT = material("M_PropGlass", (0.62, 0.78, 0.80, 1.0))
    BOTTLE_MAT = material("M_PropBottle", (0.55, 0.72, 0.85, 1.0))
    LINEN_MAT = material("M_PropLinen", (0.80, 0.82, 0.84, 1.0))
    LEAF_MAT = material("M_PropLeaf", (0.16, 0.30, 0.17, 1.0))


def gen_cabinet_group():
    """双柜组 1.8×0.9×2.0：两联文件柜，中间一道缝，底部踢脚。"""
    o = []
    o.append(box("body", (1.78, 0.88, 1.86), (0.0, 0.0, 1.0), BODY, 0.025))
    o.append(box("split", (0.035, 0.90, 1.80), (0.0, 0.0, 1.0), DARK, 0.0))
    for k in range(4):
        z = 0.34 + k * 0.42
        o.append(box("drawer_l", (0.82, 0.03, 0.30), (-0.45, -0.455, z), WHITE, 0.012))
        o.append(box("drawer_r", (0.82, 0.03, 0.30), (0.45, -0.455, z), WHITE, 0.012))
        o.append(box("handle_l", (0.20, 0.035, 0.03), (-0.45, -0.475, z), STEEL, 0.006))
        o.append(box("handle_r", (0.20, 0.035, 0.03), (0.45, -0.475, z), STEEL, 0.006))
    o.append(box("plinth", (1.72, 0.82, 0.10), (0.0, 0.0, 0.05), DARK, 0.01))
    return o, (1.8, 0.9), 2.0, "large", ("WoodPanel", "BrushedMetal")


def gen_desk():
    """办公桌 1.4×0.7×0.75：桌面 + 两侧柜体 + 桌腿空档。"""
    o = []
    o.append(box("top", (1.38, 0.70, 0.05), (0.0, 0.0, 0.725), BODY, 0.012))
    o.append(box("cab_l", (0.42, 0.66, 0.58), (-0.46, 0.0, 0.36), DARK, 0.015))
    o.append(box("leg_r", (0.05, 0.05, 0.70), (0.64, 0.30, 0.35), STEEL, 0.004))
    o.append(box("leg_r2", (0.05, 0.05, 0.70), (0.64, -0.30, 0.35), STEEL, 0.004))
    o.append(box("drawer", (0.38, 0.03, 0.36), (-0.46, -0.335, 0.44), WHITE, 0.008))
    return o, (1.4, 0.7), 0.75, "medium", ("WoodPanel", "BrushedMetal")


def gen_sofa():
    """沙发 1.8×0.8×0.85：坐垫 + 靠背 + 两侧扶手。"""
    o = []
    o.append(box("base", (1.78, 0.78, 0.32), (0.0, 0.0, 0.20), BODY, 0.03))
    o.append(box("seat", (1.62, 0.66, 0.14), (0.0, 0.02, 0.40), ACCENT, 0.02))
    o.append(box("back", (1.78, 0.16, 0.52), (0.0, -0.31, 0.62), BODY, 0.03))
    o.append(box("arm_l", (0.14, 0.74, 0.30), (-0.82, 0.0, 0.52), BODY, 0.03))
    o.append(box("arm_r", (0.14, 0.74, 0.30), (0.82, 0.0, 0.52), BODY, 0.03))
    for sx in (-0.6, 0.6):
        for sy in (-0.28, 0.28):
            o.append(cyl("foot", 0.03, 0.08, (sx, sy, 0.04), STEEL, 10, 0.004))
    return o, (1.8, 0.8), 0.85, "medium", ("VenueCarpet", "WoodPanel")


def gen_crate_stack():
    """纸箱堆 0.78×0.74×1.4：三层纸箱，逐层略收、层间不外挑。"""
    o = []
    layout = [
        (0.78, 0.74, 0.50, 0.00, 0.00),
        (0.72, 0.68, 0.48, 0.01, -0.01),
        (0.64, 0.60, 0.42, -0.01, 0.01),
    ]
    z = 0.0
    for i, (w, d, h, x, y) in enumerate(layout):
        o.append(box("crate_%d" % i, (w, d, h), (x, y, z + h * 0.5), BODY if i % 2 == 0 else WHITE, 0.012))
        o.append(box("tape_%d" % i, (w * 0.5, 0.02, 0.012), (x, y - d * 0.5 - 0.005, z + h - 0.02), ACCENT, 0.0))
        z += h
    return o, (0.80, 0.76), 1.40, "large", ("WoodPanel", "IndustrialPaint")


def gen_trash_bin():
    """垃圾桶 0.4×0.4×0.55。"""
    o = []
    o.append(cyl("body", 0.19, 0.50, (0.0, 0.0, 0.25), DARK, 16, 0.02))
    o.append(cyl("rim", 0.20, 0.04, (0.0, 0.0, 0.52), STEEL, 16, 0.008))
    o.append(box("pedal", (0.14, 0.07, 0.03), (0.0, 0.17, 0.03), STEEL, 0.005))
    return o, (0.4, 0.4), 0.55, "small", ("BrushedMetal", "DarkMetal")


def gen_water_cooler():
    """饮水机 0.5×0.45×1.15。"""
    o = []
    o.append(box("body", (0.44, 0.40, 1.00), (0.0, 0.0, 0.50), WHITE, 0.03))
    o.append(cyl("bottle", 0.16, 0.42, (0.0, 0.0, 1.20), material("M_PropBottle", (0.55, 0.72, 0.85, 1.0)), 14, 0.02))
    o.append(box("tray", (0.26, 0.10, 0.03), (0.0, -0.21, 0.72), DARK, 0.006))
    return o, (0.5, 0.45), 1.4, "small", ("CleanPanel", "Steel")


def gen_server_rack():
    """服务器机柜 0.6×0.9×2.0：柜体 + 网孔门 + 顶部走线槽。"""
    o = []
    o.append(box("body", (0.60, 0.90, 1.96), (0.0, 0.0, 1.0), DARK, 0.02))
    o.append(box("door", (0.54, 0.03, 1.70), (0.0, -0.465, 0.98), STEEL, 0.008))
    for k in range(6):
        z = 0.36 + k * 0.26
        o.append(box("unit_%d" % k, (0.50, 0.02, 0.18), (0.0, -0.485, z), WHITE, 0.004))
        o.append(box("led_%d" % k, (0.04, 0.02, 0.02), (0.22, -0.50, z + 0.06), ACCENT, 0.0))
    o.append(box("tray", (0.56, 0.30, 0.10), (0.0, 0.30, 2.02), STEEL, 0.008))
    return o, (0.6, 0.9), 2.05, "large", ("ServerPanel", "DarkMetal")


def gen_cold_shelf():
    """冷库货架 1.2×0.8×2.0：四立柱 + 三层横板。"""
    o = []
    for x in (-0.55, 0.55):
        for y in (-0.34, 0.34):
            o.append(box("post", (0.06, 0.06, 1.96), (x, y, 0.98), STEEL, 0.008))
    for k in range(4):
        z = 0.28 + k * 0.52
        o.append(box("shelf_%d" % k, (1.20, 0.74, 0.04), (0.0, 0.0, z), BODY, 0.008))
        if k < 3:
            o.append(box("goods_%d" % k, (0.90, 0.55, 0.34), (0.0, 0.0, z + 0.21), WHITE, 0.02))
    return o, (1.2, 0.8), 2.0, "large", ("ColdMetal", "CleanPanel")


def gen_workbench():
    """工作台 1.8×0.8×0.9：台面 + 下层板 + 背板工具挂架。"""
    o = []
    o.append(box("top", (1.78, 0.78, 0.08), (0.0, 0.0, 0.86), BODY, 0.012))
    o.append(box("under", (1.66, 0.68, 0.05), (0.0, 0.0, 0.30), STEEL, 0.008))
    for x in (-0.82, 0.82):
        for y in (-0.34, 0.34):
            o.append(box("leg", (0.06, 0.06, 0.82), (x, y, 0.41), STEEL, 0.006))
    o.append(box("pegboard", (1.60, 0.04, 0.60), (0.0, -0.36, 1.20), DARK, 0.008))
    for k in range(3):
        o.append(box("tool_%d" % k, (0.06, 0.06, 0.30), (-0.40 + k * 0.40, -0.40, 1.10), ACCENT, 0.006))
    return o, (1.8, 0.8), 1.5, "large", ("DiamondPlate", "IndustrialPaint")


def gen_control_console():
    """控制台 2.0×0.8×1.0：桌面 + 斜面操作台 + 上方显示器。"""
    o = []
    o.append(box("desk", (1.98, 0.78, 0.72), (0.0, 0.0, 0.38), DARK, 0.02))
    slanted = box("panel", (1.90, 0.42, 0.06), (0.0, -0.18, 0.80), STEEL, 0.01)
    slanted.rotation_euler[0] = math.radians(18)
    bpy.context.view_layer.objects.active = slanted
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    o.append(slanted)
    for k in range(3):
        o.append(box("monitor_%d" % k, (0.56, 0.05, 0.34), (-0.62 + k * 0.62, 0.30, 1.14), DARK, 0.01))
        o.append(box("screen_%d" % k, (0.50, 0.02, 0.28), (-0.62 + k * 0.62, 0.27, 1.14), material("M_PropScreen", (0.30, 0.45, 0.52, 1.0)), 0.004))
    return o, (2.0, 0.8), 1.3, "large", ("CleanPanel", "DarkMetal")


def gen_power_cabinet():
    """配电柜 1.2×0.6×2.2：柜体 + 双开门 + 指示灯带。"""
    o = []
    o.append(box("body", (1.18, 0.58, 2.16), (0.0, 0.0, 1.10), BODY, 0.02))
    o.append(box("door_l", (0.56, 0.03, 2.00), (-0.30, -0.30, 1.06), DARK, 0.008))
    o.append(box("door_r", (0.56, 0.03, 2.00), (0.30, -0.30, 1.06), DARK, 0.008))
    for k in range(5):
        o.append(box("lamp_%d" % k, (0.06, 0.02, 0.06), (-0.42 + k * 0.21, -0.325, 1.86), ACCENT, 0.004))
    o.append(box("plinth", (1.10, 0.52, 0.10), (0.0, 0.0, 0.05), STEEL, 0.01))
    return o, (1.2, 0.6), 2.2, "large", ("IndustrialPaint", "DarkMetal")


def gen_tank():
    """储罐 1.6×1.6×2.6：立式罐 + 顶盖 + 进出管。"""
    o = []
    o.append(cyl("shell", 0.74, 2.30, (0.0, 0.0, 1.20), STEEL, 20, 0.02))
    o.append(cyl("cap", 0.76, 0.10, (0.0, 0.0, 2.40), DARK, 20, 0.02))
    o.append(box("pipe_out", (0.16, 0.16, 0.80), (0.55, 0.55, 0.90), BODY, 0.02))
    o.append(cyl("valve", 0.10, 0.10, (0.55, 0.55, 1.36), ACCENT, 12, 0.008))
    for k in range(3):
        o.append(cyl("band_%d" % k, 0.755, 0.06, (0.0, 0.0, 0.50 + k * 0.70), DARK, 20, 0.008))
    return o, (1.6, 1.6), 2.6, "large", ("ColdMetal", "IndustrialPaint")


def gen_archive_shelf():
    """档案架 1.4×0.6×2.1：五层隔板 + 档案盒。"""
    o = []
    o.append(box("back", (1.38, 0.04, 2.06), (0.0, 0.26, 1.05), DARK, 0.008))
    for x in (-0.66, 0.66):
        o.append(box("side", (0.05, 0.56, 2.06), (x, 0.0, 1.05), DARK, 0.008))
    for k in range(5):
        z = 0.22 + k * 0.44
        o.append(box("shelf_%d" % k, (1.34, 0.54, 0.04), (0.0, 0.0, z), BODY, 0.006))
        for j in range(4):
            o.append(box("box_%d_%d" % (k, j), (0.28, 0.40, 0.30), (-0.48 + j * 0.32, -0.02, z + 0.19), WHITE, 0.01))
    return o, (1.4, 0.6), 2.1, "large", ("PaintedWall", "WoodPanel")


def gen_pallet():
    """装卸托盘 1.2×1.0×0.6：托盘 + 两层货箱。"""
    o = []
    for k in range(3):
        o.append(box("plank_%d" % k, (1.18, 0.16, 0.03), (0.0, -0.40 + k * 0.40, 0.10), BODY, 0.006))
    o.append(box("deck", (1.18, 0.96, 0.04), (0.0, 0.0, 0.16), BODY, 0.006))
    o.append(box("cargo_a", (0.96, 0.76, 0.38), (0.0, 0.0, 0.38), WHITE, 0.015))
    o.append(box("cargo_b", (0.70, 0.60, 0.26), (-0.08, 0.04, 0.70), BODY, 0.015))
    o.append(box("strap", (0.98, 0.04, 0.06), (0.0, -0.20, 0.60), ACCENT, 0.0))
    return o, (1.2, 1.0), 0.85, "large", ("WoodPanel", "IndustrialPaint")


def gen_bed():
    """床铺 1.0×2.0×0.55：床架 + 床垫 + 枕头。"""
    o = []
    o.append(box("frame", (1.00, 1.98, 0.22), (0.0, 0.0, 0.22), DARK, 0.02))
    o.append(box("mattress", (0.94, 1.90, 0.20), (0.0, 0.0, 0.43), WHITE, 0.03))
    o.append(box("pillow", (0.52, 0.32, 0.12), (0.0, -0.76, 0.58), material("M_PropLinen", (0.80, 0.82, 0.84, 1.0)), 0.03))
    o.append(box("head", (1.00, 0.08, 0.70), (0.0, 0.95, 0.35), DARK, 0.02))
    return o, (1.0, 2.0), 0.70, "large", ("VenueCarpet", "BrushedMetal")


def gen_locker():
    """储物柜 0.8×0.5×1.8：两联格口。"""
    o = []
    o.append(box("body", (0.78, 0.48, 1.76), (0.0, 0.0, 0.90), BODY, 0.02))
    o.append(box("split", (0.02, 0.50, 1.72), (0.0, 0.0, 0.90), DARK, 0.0))
    for x in (-0.19, 0.19):
        o.append(box("door", (0.36, 0.02, 1.66), (x, -0.245, 0.90), DARK, 0.006))
        for k in range(3):
            o.append(box("vent_%d" % k, (0.20, 0.01, 0.02), (x, -0.256, 1.40 + k * 0.06), STEEL, 0.0))
        o.append(box("handle", (0.04, 0.05, 0.18), (x + 0.13, -0.26, 0.95), STEEL, 0.006))
    return o, (0.8, 0.5), 1.8, "medium", ("PaintedWall", "BrushedMetal")


def gen_med_cabinet():
    """医疗柜 0.8×0.45×1.8：玻璃门 + 三层托盘。"""
    o = []
    o.append(box("body", (0.78, 0.44, 1.76), (0.0, 0.0, 0.90), WHITE, 0.02))
    o.append(box("glass", (0.62, 0.02, 1.20), (0.0, -0.225, 1.10), material("M_PropGlass", (0.62, 0.78, 0.80, 1.0)), 0.006))
    for k in range(3):
        z = 0.62 + k * 0.42
        o.append(box("tray_%d" % k, (0.66, 0.34, 0.03), (0.0, 0.02, z), STEEL, 0.006))
        o.append(box("kit_%d" % k, (0.30, 0.20, 0.14), (0.0, 0.02, z + 0.09), ACCENT if k % 2 else BODY, 0.01))
    return o, (0.8, 0.45), 1.8, "medium", ("CleanPanel", "GlassPanel")


def gen_partition():
    """隔断屏 1.6×0.12×1.7：屏风 + 铝框 + 布面。"""
    o = []
    o.append(box("panel", (1.58, 0.10, 1.56), (0.0, 0.0, 0.86), ACCENT, 0.02))
    o.append(box("frame_t", (1.60, 0.12, 0.05), (0.0, 0.0, 1.66), STEEL, 0.008))
    o.append(box("frame_b", (1.60, 0.12, 0.05), (0.0, 0.0, 0.04), STEEL, 0.008))
    for x in (-0.79, 0.79):
        o.append(box("frame_s", (0.05, 0.12, 1.70), (x, 0.0, 0.85), STEEL, 0.008))
    return o, (1.6, 0.12), 1.75, "medium", ("VenueCarpet", "BrushedMetal")


def gen_pipe_rack():
    """管道支架 1.0×0.4×1.2：门架 + 两根横管。"""
    o = []
    for x in (-0.46, 0.46):
        o.append(box("post", (0.07, 0.07, 1.16), (x, 0.0, 0.58), STEEL, 0.008))
    o.append(box("beam", (1.00, 0.07, 0.07), (0.0, 0.0, 1.14), STEEL, 0.008))
    for y, z in ((-0.10, 0.92), (-0.10, 0.66)):
        pipe = cyl("pipe", 0.09, 1.00, (0.0, y, z), BODY, 12, 0.01)
        pipe.rotation_euler[1] = math.radians(90)
        bpy.context.view_layer.objects.active = pipe
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
        o.append(pipe)
    return o, (1.0, 0.4), 1.25, "medium", ("IndustrialPaint", "ColdMetal")


def gen_meeting_table():
    """会议桌 2.2×1.0×0.75。"""
    o = []
    o.append(box("top", (2.18, 0.98, 0.06), (0.0, 0.0, 0.72), BODY, 0.015))
    o.append(box("rail", (1.80, 0.16, 0.10), (0.0, 0.0, 0.62), DARK, 0.008))
    for x in (-0.86, 0.86):
        o.append(box("ped", (0.14, 0.62, 0.66), (x, 0.0, 0.34), DARK, 0.01))
    return o, (2.2, 1.0), 0.75, "large", ("WoodPanel", "BrushedMetal")


def gen_printer():
    """打印台 1.2×0.6×1.1：台体 + 打印机 + 纸盒。"""
    o = []
    o.append(box("stand", (1.16, 0.56, 0.72), (0.0, 0.0, 0.38), DARK, 0.02))
    o.append(box("printer", (0.66, 0.44, 0.30), (-0.14, 0.0, 0.88), WHITE, 0.02))
    o.append(box("tray", (0.50, 0.30, 0.03), (-0.14, 0.22, 1.04), STEEL, 0.006))
    o.append(box("paper", (0.34, 0.24, 0.12), (0.38, -0.02, 0.82), BODY, 0.01))
    return o, (1.2, 0.6), 1.1, "medium", ("DarkMetal", "CleanPanel")


# ---------------------------------------------------------------------------
# 第二批（R2 追加，2026-11-27）：椅子 / 冷机 / 泵 / 屏幕 / 仓储 / 医疗 / 车间 / 隔断
#   面数预算：large ≤1500 / medium ≤900 / small ≤300（build_one 里逐件自检）
# ---------------------------------------------------------------------------

def gen_office_chair():
    """办公椅 0.62×0.62×1.05：五星脚 + 气压杆 + 坐垫 + 靠背 + 扶手。"""
    o = []
    for k in range(5):
        a = math.radians(72.0 * k)
        o.append(box("arm_%d" % k, (0.28, 0.055, 0.04),
                     (math.cos(a) * 0.14, math.sin(a) * 0.14, 0.05), DARK, 0.0, a))
        o.append(box("caster_%d" % k, (0.05, 0.05, 0.04),
                     (math.cos(a) * 0.27, math.sin(a) * 0.27, 0.03), DARK, 0.0))
    o.append(cyl("lift", 0.05, 0.36, (0.0, 0.0, 0.26), STEEL, 10, 0.0))
    o.append(box("seat", (0.50, 0.48, 0.09), (0.0, 0.0, 0.49), BODY, 0.012, 0.0, 1))
    o.append(box("back", (0.46, 0.07, 0.50), (0.0, 0.22, 0.78), BODY, 0.012, 0.0, 1))
    for x in (-0.27, 0.27):
        o.append(box("armrest", (0.05, 0.26, 0.04), (x, -0.02, 0.66), DARK, 0.0))
        o.append(box("arm_post", (0.04, 0.04, 0.14), (x, -0.02, 0.58), STEEL, 0.0))
    return o, (0.60, 0.57), 1.05, "small", ("VenueCarpet", "BrushedMetal")


def gen_folding_chair():
    """折叠椅 0.52×0.55×0.82：折叠钢管腿 + 座板 + 靠背（展开状态）。"""
    o = []
    o.append(box("seat", (0.44, 0.42, 0.05), (0.0, 0.0, 0.44), BODY, 0.01, 0.0, 1))
    o.append(box("back", (0.42, 0.04, 0.32), (0.0, 0.20, 0.66), BODY, 0.01, 0.0, 1))
    for x in (-0.19, 0.19):
        o.append(box("leg_f", (0.035, 0.035, 0.44), (x, -0.16, 0.22), STEEL, 0.0))
        o.append(box("leg_b", (0.035, 0.035, 0.80), (x, 0.20, 0.40), STEEL, 0.0))
    o.append(box("cross", (0.40, 0.035, 0.035), (0.0, -0.10, 0.20), STEEL, 0.0))
    o.append(box("brace", (0.40, 0.035, 0.035), (0.0, 0.05, 0.10), STEEL, 0.0))
    return o, (0.46, 0.45), 0.82, "small", ("WoodPanel", "BrushedMetal")


def gen_cold_fan():
    """冷风机 1.0×0.5×0.95：机箱 + 圆形出风口 + 百叶 + 顶部散热格栅。"""
    o = []
    o.append(box("body", (0.96, 0.44, 0.86), (0.0, 0.0, 0.47), BODY, 0.02, 0.0, 1))
    fan = cyl("fan", 0.31, 0.04, (0.0, -0.235, 0.50), DARK, 16, 0.0)
    o.append(rot(fan, 0, 90))
    for k in range(4):
        o.append(box("louvre_%d" % k, (0.54, 0.02, 0.03),
                     (0.0, -0.26, 0.34 + k * 0.11), STEEL, 0.0))
    for k in range(3):
        o.append(box("vent_%d" % k, (0.80, 0.30, 0.02), (0.0, 0.02, 0.91), STEEL, 0.0))
    for x in (-0.42, 0.42):
        for y in (-0.16, 0.16):
            o.append(box("foot", (0.06, 0.06, 0.08), (x, y, 0.04), DARK, 0.0))
    o.append(box("panel", (0.20, 0.02, 0.14), (0.36, -0.23, 0.72), ACCENT, 0.0))
    return o, (1.0, 0.5), 0.95, "medium", ("ColdMetal", "BrushedMetal")


def gen_pump():
    """泵组 1.0×0.6×0.78：底座 + 卧式电机 + 泵壳 + 进出口管 + 阀轮。"""
    o = []
    o.append(box("skid", (0.96, 0.56, 0.08), (0.0, 0.0, 0.04), STEEL, 0.0))
    motor = cyl("motor", 0.20, 0.52, (-0.16, 0.0, 0.32), ACCENT, 16, 0.0)
    o.append(rot(motor, 1, 90))
    casing = cyl("casing", 0.25, 0.18, (0.26, 0.0, 0.36), BODY, 16, 0.0)
    o.append(rot(casing, 1, 90))
    o.append(box("coupling", (0.10, 0.10, 0.10), (0.06, 0.0, 0.32), DARK, 0.0))
    for x in (-0.40, 0.40):
        o.append(box("mount", (0.10, 0.46, 0.10), (x, 0.0, 0.13), DARK, 0.0))
    o.append(cyl("inlet", 0.09, 0.26, (0.26, 0.0, 0.56), STEEL, 10, 0.0))
    outlet = cyl("outlet", 0.08, 0.34, (0.26, -0.22, 0.34), STEEL, 10, 0.0)
    o.append(rot(outlet, 0, 90))
    valve = cyl("valve", 0.11, 0.04, (0.26, -0.38, 0.34), ACCENT, 12, 0.0)
    o.append(rot(valve, 0, 90))
    return o, (0.96, 0.68), 0.78, "medium", ("IndustrialPaint", "BrushedMetal")


def gen_screen_wall():
    """屏幕墙 1.6×0.3×2.2：六联监控屏 + 背架 + 底柜（对齐生成器 1.6×0.3×2.2）。"""
    o = []
    o.append(box("back", (1.54, 0.10, 2.02), (0.0, 0.09, 1.10), DARK, 0.01, 0.0, 1))
    scr = material("M_PropScreen", (0.30, 0.45, 0.52, 1.0))
    for r in range(3):
        for c in range(2):
            x = -0.39 + c * 0.78
            z = 0.62 + r * 0.50
            o.append(box("bezel_%d_%d" % (r, c), (0.72, 0.07, 0.44), (x, -0.01, z), DARK, 0.0))
            o.append(box("screen_%d_%d" % (r, c), (0.64, 0.02, 0.36), (x, -0.055, z), scr, 0.0))
    o.append(box("plinth", (1.50, 0.26, 0.12), (0.0, 0.02, 0.06), STEEL, 0.0))
    for x in (-0.74, 0.74):
        o.append(box("trim", (0.06, 0.24, 2.10), (x, 0.02, 1.06), STEEL, 0.0))
    return o, (1.6, 0.3), 2.2, "large", ("CleanPanel", "DarkMetal")


def gen_vending_machine():
    """售货机 1.0×0.8×1.95：柜体 + 玻璃橱窗 + 三层货道 + 按钮面板。"""
    o = []
    o.append(box("body", (0.94, 0.72, 1.86), (0.0, 0.03, 0.97), BODY, 0.02, 0.0, 1))
    o.append(box("glass", (0.66, 0.02, 1.28), (-0.09, -0.345, 1.16), GLASS_MAT, 0.0))
    for k in range(3):
        z = 0.62 + k * 0.44
        o.append(box("shelf_%d" % k, (0.64, 0.26, 0.03), (-0.09, -0.19, z), STEEL, 0.0))
        for j in range(4):
            o.append(box("goods_%d_%d" % (k, j), (0.13, 0.16, 0.20),
                         (-0.32 + j * 0.155, -0.19, z + 0.115),
                         WHITE if (j + k) % 2 else ACCENT, 0.0))
    o.append(box("panel", (0.18, 0.03, 0.62), (0.33, -0.335, 1.12), DARK, 0.0))
    for k in range(6):
        o.append(box("btn_%d" % k, (0.08, 0.02, 0.04),
                     (0.33, -0.355, 0.92 + k * 0.09), STEEL, 0.0))
    o.append(box("slot", (0.22, 0.04, 0.06), (0.33, -0.35, 0.80), DARK, 0.0))
    o.append(box("dispenser", (0.30, 0.10, 0.14), (-0.09, -0.33, 0.28), DARK, 0.0))
    o.append(box("sign", (0.86, 0.06, 0.16), (0.0, -0.33, 1.90), ACCENT, 0.0))
    return o, (1.0, 0.8), 1.95, "medium", ("CleanPanel", "GlassPanel")


def gen_water_cooler_v2():
    """直饮水机 0.5×0.45×1.26：机身 + 水桶 + 双出水嘴 + 接水盘（旧饮水机的面数达标版）。"""
    o = []
    o.append(box("body", (0.46, 0.40, 0.92), (0.0, 0.0, 0.48), WHITE, 0.015, 0.0, 1))
    o.append(cyl("bottle", 0.145, 0.32, (0.0, 0.0, 1.10), BOTTLE_MAT, 12, 0.0))
    o.append(cyl("neck", 0.05, 0.06, (0.0, 0.0, 0.96), WHITE, 8, 0.0))
    o.append(box("tray", (0.28, 0.12, 0.03), (0.0, -0.235, 0.60), DARK, 0.0))
    o.append(box("grate", (0.24, 0.08, 0.01), (0.0, -0.24, 0.62), STEEL, 0.0))
    for x in (-0.07, 0.07):
        o.append(box("tap", (0.05, 0.07, 0.06), (x, -0.22, 0.72), ACCENT, 0.0))
    o.append(box("panel", (0.10, 0.02, 0.30), (0.18, -0.205, 0.80), DARK, 0.0))
    return o, (0.46, 0.50), 1.26, "small", ("CleanPanel", "Steel")


def gen_shelf_unit():
    """仓储货架 1.8×0.6×2.0：四立柱 + 五层横板 + 横梁 + 纸箱（区别于冷库货架）。"""
    o = []
    for x in (-0.86, 0.86):
        for y in (-0.26, 0.26):
            o.append(box("post", (0.06, 0.06, 1.96), (x, y, 0.98), STEEL, 0.006, 0.0, 1))
    for k in range(5):
        z = 0.19 + k * 0.44
        o.append(box("shelf_%d" % k, (1.74, 0.56, 0.04), (0.0, 0.0, z), BODY, 0.006, 0.0, 1))
        if k < 3:
            for j in range(3):
                o.append(box("carton_%d_%d" % (k, j), (0.40, 0.42, 0.26),
                             (-0.48 + j * 0.48, 0.0, z + 0.17),
                             WHITE if j % 2 else BODY, 0.008, 0.0, 1))
    for y in (-0.26, 0.26):
        for z in (0.42, 1.06, 1.70):
            o.append(box("rail", (1.72, 0.04, 0.04), (0.0, y, z), STEEL, 0.0))
    return o, (1.8, 0.6), 2.0, "large", ("IndustrialPaint", "WoodPanel")


def gen_pallet_stack():
    """托盘堆 1.2×1.0×0.72：四层空托盘交错叠放 + 捆扎带。"""
    o = []
    z = 0.0
    for k in range(4):
        rotz = math.radians(4 if k % 2 else -4)
        o.append(box("deck_%d" % k, (1.16, 0.96, 0.05), (0.0, 0.0, z + 0.13), BODY, 0.0, rotz))
        for j in range(3):
            o.append(box("runner_%d_%d" % (k, j), (1.16, 0.13, 0.09),
                         (0.0, -0.40 + j * 0.40, z + 0.045), ACCENT, 0.0, rotz))
        z += 0.17
    for x in (-0.35, 0.35):
        o.append(box("strap", (0.05, 0.98, 0.56), (x, 0.0, 0.30), DARK, 0.0))
    return o, (1.2, 1.0), 0.72, "large", ("WoodPanel", "IndustrialPaint")


def gen_barrel():
    """油桶 0.6×0.6×0.90：桶身 + 两道加强箍 + 顶盖 + 桶塞。"""
    o = []
    o.append(cyl("body", 0.29, 0.84, (0.0, 0.0, 0.43), BODY, 16, 0.0))
    for k in range(2):
        o.append(cyl("rib_%d" % k, 0.30, 0.05, (0.0, 0.0, 0.26 + k * 0.38), STEEL, 12, 0.0))
    o.append(cyl("lid", 0.285, 0.04, (0.0, 0.0, 0.86), DARK, 12, 0.0))
    o.append(box("bung", (0.08, 0.08, 0.03), (0.14, 0.0, 0.885), STEEL, 0.0))
    return o, (0.6, 0.6), 0.90, "small", ("IndustrialPaint", "BrushedMetal")


def gen_cable_spool():
    """电缆盘 1.0×0.8×1.08：双法兰 + 绕线轴 + 电缆 + 放线架。"""
    o = []
    for x in (-0.17, 0.17):
        o.append(rot(cyl("flange", 0.46, 0.06, (x, 0.0, 0.62), BODY, 20, 0.0), 1, 90))
    o.append(rot(cyl("core", 0.28, 0.40, (0.0, 0.0, 0.62), STEEL, 16, 0.0), 1, 90))
    o.append(rot(cyl("cable", 0.33, 0.34, (0.0, 0.0, 0.62), DARK, 16, 0.0), 1, 90))
    for x in (-0.52, 0.52):
        o.append(box("upright", (0.06, 0.06, 0.62), (x, 0.0, 0.31), STEEL, 0.0))
        o.append(box("foot", (0.10, 0.70, 0.06), (x, 0.0, 0.03), STEEL, 0.0))
    o.append(box("axle", (0.70, 0.05, 0.05), (0.0, 0.0, 0.62), STEEL, 0.0))
    return o, (1.14, 0.92), 1.08, "medium", ("WoodPanel", "BrushedMetal")


def gen_sign_stand():
    """立式指示牌 0.55×0.45×1.45：配重底座 + 双立柱 + 空白牌面（方便贴 signage）。"""
    o = []
    o.append(box("base", (0.50, 0.40, 0.06), (0.0, 0.0, 0.03), DARK, 0.01, 0.0, 1))
    for x in (-0.20, 0.20):
        o.append(box("post", (0.04, 0.04, 1.30), (x, 0.02, 0.68), STEEL, 0.0))
    o.append(box("panel", (0.46, 0.03, 0.62), (0.0, 0.0, 0.92), WHITE, 0.01, 0.0, 1))
    for z in (0.60, 1.24):
        o.append(box("rail", (0.50, 0.05, 0.04), (0.0, 0.0, z), DARK, 0.0))
    o.append(box("stripe", (0.40, 0.005, 0.06), (0.0, -0.019, 0.72), ACCENT, 0.0))
    return o, (0.50, 0.40), 1.33, "small", ("PaintedWall", "BrushedMetal")


def gen_plant():
    """绿植 0.65×0.65×1.36：花盆 + 主干 + 四团灌木。"""
    o = []
    o.append(cyl("pot", 0.22, 0.34, (0.0, 0.0, 0.17), ACCENT, 12, 0.0))
    o.append(cyl("rim", 0.235, 0.05, (0.0, 0.0, 0.34), DARK, 12, 0.0))
    o.append(cyl("soil", 0.20, 0.02, (0.0, 0.0, 0.355), DARK, 12, 0.0))
    o.append(cyl("trunk", 0.035, 0.55, (0.0, 0.0, 0.60), DARK, 8, 0.0))
    leaves = [
        (0.0, 0.0, 1.02, 0.52, 0.50, 0.36),
        (-0.12, 0.08, 0.86, 0.36, 0.34, 0.30),
        (0.14, -0.06, 1.18, 0.34, 0.32, 0.26),
        (0.05, 0.12, 0.92, 0.30, 0.28, 0.24),
    ]
    for i, (x, y, z, sx, sy, sz) in enumerate(leaves):
        o.append(box("leaf_%d" % i, (sx, sy, sz), (x, y, z), LEAF_MAT, 0.04, 0.0, 1))
    return o, (0.62, 0.52), 1.31, "medium", ("VenueCarpet", "WoodPanel")


def gen_fire_cabinet():
    """消防柜 0.72×0.28×1.10：红色柜体 + 玻璃门 + 隔板 + 把手。"""
    o = []
    o.append(box("body", (0.70, 0.26, 1.04), (0.0, 0.0, 0.54), ACCENT, 0.015, 0.0, 1))
    o.append(box("glass", (0.56, 0.02, 0.80), (0.0, -0.135, 0.60), GLASS_MAT, 0.0))
    o.append(box("divider", (0.56, 0.02, 0.03), (0.0, -0.13, 0.36), STEEL, 0.0))
    o.append(box("handle", (0.03, 0.04, 0.14), (0.28, -0.145, 0.60), STEEL, 0.0))
    for x in (-0.24, 0.24):
        o.append(box("hinge", (0.04, 0.03, 0.06), (x, -0.14, 1.00), DARK, 0.0))
    o.append(box("plinth", (0.64, 0.22, 0.06), (0.0, 0.0, 0.03), DARK, 0.0))
    return o, (0.72, 0.28), 1.10, "small", ("IndustrialPaint", "GlassPanel")


def gen_extinguisher():
    """灭火器 0.28×0.28×0.64：罐体 + 色带 + 阀头 + 把手 + 喷管。"""
    o = []
    o.append(cyl("body", 0.125, 0.46, (0.0, 0.0, 0.25), ACCENT, 12, 0.0))
    o.append(cyl("band", 0.128, 0.10, (0.0, 0.0, 0.38), WHITE, 12, 0.0))
    o.append(cyl("neck", 0.055, 0.08, (0.0, 0.0, 0.51), DARK, 8, 0.0))
    o.append(box("head", (0.14, 0.10, 0.08), (0.0, 0.0, 0.57), DARK, 0.008, 0.0, 1))
    o.append(box("lever", (0.16, 0.05, 0.03), (0.02, 0.0, 0.625), STEEL, 0.0))
    o.append(box("hose", (0.035, 0.035, 0.22), (0.13, -0.03, 0.42), DARK, 0.0))
    o.append(box("nozzle", (0.05, 0.08, 0.04), (0.13, -0.08, 0.30), STEEL, 0.0))
    return o, (0.28, 0.28), 0.64, "small", ("IndustrialPaint", "DarkMetal")


def gen_hospital_bed():
    """病床 1.0×2.05×1.52：床架 + 床垫 + 护栏 + 脚轮 + 床头尾板 + 输液架。"""
    o = []
    o.append(box("frame", (0.94, 1.96, 0.10), (0.0, 0.0, 0.50), STEEL, 0.0))
    o.append(box("mattress", (0.90, 1.88, 0.16), (0.0, 0.0, 0.63), WHITE, 0.03, 0.0, 1))
    o.append(box("pillow", (0.52, 0.30, 0.09), (0.0, -0.66, 0.76), LINEN_MAT, 0.02, 0.0, 1))
    o.append(box("headboard", (0.96, 0.06, 0.55), (0.0, 0.98, 0.775), STEEL, 0.0))
    o.append(box("footboard", (0.96, 0.06, 0.42), (0.0, -0.98, 0.71), STEEL, 0.0))
    for x in (-0.42, 0.42):
        for y in (-0.90, 0.90):
            o.append(box("leg", (0.05, 0.05, 0.42), (x, y, 0.23), STEEL, 0.0))
    for x in (-0.42, 0.42):
        for y in (-0.90, 0.90):
            w = cyl("wheel", 0.06, 0.05, (x, y, 0.06), DARK, 8, 0.0)
            o.append(rot(w, 0, 90))
    for s in (-1, 1):
        o.append(box("rail_%d" % s, (0.04, 0.80, 0.04), (s * 0.47, 0.0, 0.82), STEEL, 0.0))
        for y in (-0.34, 0.34):
            o.append(box("rail_post", (0.04, 0.04, 0.24), (s * 0.47, y, 0.70), STEEL, 0.0))
    o.append(cyl("iv_pole", 0.015, 1.02, (0.42, 0.80, 1.01), STEEL, 8, 0.0))
    o.append(box("iv_hook", (0.12, 0.03, 0.03), (0.36, 0.80, 1.50), STEEL, 0.0))
    return o, (1.0, 2.05), 1.52, "large", ("CleanPanel", "BrushedMetal")


def gen_medical_cart():
    """医疗推车 0.62×0.48×0.98：三层托盘 + 四立柱 + 脚轮 + 推手 + 污物桶。"""
    o = []
    for k in range(3):
        o.append(box("tray_%d" % k, (0.58, 0.44, 0.04), (0.0, 0.0, 0.28 + k * 0.30), WHITE, 0.0))
    for x in (-0.26, 0.26):
        for y in (-0.19, 0.19):
            o.append(box("post", (0.03, 0.03, 0.88), (x, y, 0.46), STEEL, 0.0))
    for x in (-0.24, 0.24):
        for y in (-0.16, 0.16):
            w = cyl("wheel", 0.05, 0.04, (x, y, 0.05), DARK, 8, 0.0)
            o.append(rot(w, 0, 90))
    o.append(box("handle", (0.44, 0.04, 0.04), (0.0, 0.22, 0.94), STEEL, 0.0))
    for x in (-0.20, 0.20):
        o.append(box("handle_post", (0.03, 0.03, 0.14), (x, 0.22, 0.87), STEEL, 0.0))
    o.append(box("bin", (0.20, 0.16, 0.14), (0.15, -0.10, 0.36), ACCENT, 0.0))
    return o, (0.62, 0.48), 0.98, "small", ("CleanPanel", "BrushedMetal")


def gen_tool_cart():
    """工具车 0.95×0.55×1.05：四抽屉柜 + 台面 + 推手 + 脚轮。"""
    o = []
    o.append(box("body", (0.88, 0.50, 0.78), (0.0, 0.0, 0.52), ACCENT, 0.02, 0.0, 1))
    for k in range(4):
        z = 0.22 + k * 0.18
        o.append(box("drawer_%d" % k, (0.80, 0.02, 0.14), (0.0, -0.26, z), DARK, 0.0))
        o.append(box("handle_%d" % k, (0.24, 0.03, 0.03), (0.0, -0.28, z + 0.04), STEEL, 0.0))
    o.append(box("top", (0.92, 0.54, 0.06), (0.0, 0.0, 0.94), DARK, 0.01, 0.0, 1))
    o.append(box("push", (0.60, 0.04, 0.04), (0.0, 0.30, 1.00), STEEL, 0.0))
    for x in (-0.24, 0.24):
        o.append(box("push_post", (0.04, 0.04, 0.18), (x, 0.30, 0.90), STEEL, 0.0))
    for x in (-0.32, 0.32):
        for y in (-0.18, 0.18):
            w = cyl("wheel", 0.06, 0.05, (x, y, 0.06), DARK, 8, 0.0)
            o.append(rot(w, 0, 90))
    return o, (0.92, 0.62), 1.05, "medium", ("IndustrialPaint", "BrushedMetal")


def gen_workbench_v2():
    """加固工作台 2.0×0.8×1.50：厚台面 + 方钢腿 + 下层板 + 背板 + 台钳。"""
    o = []
    o.append(box("top", (1.96, 0.76, 0.10), (0.0, 0.0, 0.85), BODY, 0.015, 0.0, 1))
    for x in (-0.86, 0.86):
        for y in (-0.32, 0.32):
            o.append(box("leg", (0.08, 0.08, 0.80), (x, y, 0.40), STEEL, 0.006, 0.0, 1))
    o.append(box("shelf", (1.76, 0.62, 0.05), (0.0, 0.0, 0.28), DARK, 0.0))
    o.append(box("rail", (1.88, 0.05, 0.06), (0.0, 0.36, 0.78), STEEL, 0.0))
    o.append(box("pegboard", (1.84, 0.04, 0.56), (0.0, 0.38, 1.16), DARK, 0.0))
    o.append(box("vise_base", (0.16, 0.14, 0.08), (-0.62, -0.22, 0.94), ACCENT, 0.0))
    o.append(box("vise_jaw", (0.14, 0.12, 0.10), (-0.62, -0.24, 1.02), ACCENT, 0.0))
    vh = cyl("vise_handle", 0.02, 0.34, (-0.62, -0.36, 1.02), STEEL, 8, 0.0)
    o.append(rot(vh, 0, 90))
    for k in range(3):
        o.append(box("tool_%d" % k, (0.05, 0.05, 0.26),
                     (-0.30 + k * 0.30, 0.35, 1.10), ACCENT, 0.0))
    return o, (1.96, 0.93), 1.50, "large", ("DiamondPlate", "IndustrialPaint")


def gen_glass_partition():
    """玻璃隔断 2.0×0.10×2.20：双玻 + 铝合金立柱/横梁 + 踢脚 + 拉手。"""
    o = []
    for x in (-0.49, 0.49):
        o.append(box("pane", (0.92, 0.02, 1.92), (x, 0.0, 1.10), GLASS_MAT, 0.0))
    for x in (-0.98, 0.0, 0.98):
        o.append(box("post", (0.06, 0.08, 2.16), (x, 0.0, 1.08), STEEL, 0.006, 0.0, 1))
    for z in (0.10, 2.14):
        o.append(box("rail", (1.98, 0.08, 0.06), (0.0, 0.0, z), STEEL, 0.0))
    o.append(box("kick", (1.94, 0.06, 0.14), (0.0, 0.0, 0.09), DARK, 0.0))
    for y in (-1.0, 1.0):
        o.append(box("handle", (0.03, 0.12, 0.24), (0.44, y * 0.05, 1.10), STEEL, 0.0))
    return o, (2.02, 0.22), 2.20, "large", ("GlassPanel", "BrushedMetal")


# ---------------------------------------------------------------------------
# 第四批（R4，2026-11-27）：设施道具族 A–F
#   办公/行政、机房/中控、冷链/仓储、制造/设备、医疗/洁净、生活舱。
#   约定与前三批完全一致：Blender 里正面朝 -Y，FBX 以 axis_forward=-Z / axis_up=Y 导出，
#   到 Unity 里正面就是 +Z（04-3d-assets.md §2）；sidecar 写 "facing": "+Z"，
#   PropSpawner 按墙朝向再转 0°/90°（PropSpec 的 W/D 决定长轴方向）。
#   面数：large ≤1500 / medium ≤900 / small ≤300（build_one 逐件自检）。
# ---------------------------------------------------------------------------

def _scr():
    """共享的"屏幕"材质（蓝灰自发光感），多件设备复用同一个材质块。"""
    return material("M_PropScreen", (0.30, 0.45, 0.52, 1.0))


# ---- A. 办公 / 行政 --------------------------------------------------------

def gen_conference_chair():
    """会议椅 0.54×0.50×0.98：四腿 + 座垫 + 靠背 + 扶手（区别于五星脚办公椅）。"""
    o = []
    o.append(box("seat", (0.50, 0.48, 0.09), (0.0, 0.0, 0.45), BODY, 0.012, 0.0, 1))
    o.append(box("back", (0.46, 0.06, 0.44), (0.0, 0.20, 0.72), BODY, 0.012, 0.0, 1))
    o.append(box("back_top", (0.48, 0.07, 0.05), (0.0, 0.20, 0.95), DARK, 0.0))
    for x in (-0.20, 0.20):
        for y in (-0.18, 0.18):
            o.append(box("leg", (0.04, 0.04, 0.43), (x, y, 0.215), STEEL, 0.0, 0.0, 1))
    for y in (-0.18, 0.18):
        o.append(box("brace", (0.44, 0.03, 0.03), (0.0, y, 0.12), STEEL, 0.0))
    for x in (-0.25, 0.25):
        o.append(box("arm", (0.05, 0.24, 0.04), (x, -0.02, 0.66), DARK, 0.0))
        o.append(box("arm_post", (0.04, 0.04, 0.16), (x, -0.02, 0.56), STEEL, 0.0))
    return o, (0.54, 0.50), 0.98, "medium", ("FabricPanel", "BrushedMetal")


def gen_file_cabinet():
    """文件柜 0.9×0.5×1.68：三抽屉 + 标签框 + 顶板（生成器点名「文件柜」）。"""
    o = []
    o.append(box("body", (0.86, 0.46, 1.60), (0.0, 0.0, 0.86), BODY, 0.015, 0.0, 1))
    o.append(box("plinth", (0.80, 0.40, 0.07), (0.0, 0.0, 0.035), DARK, 0.0))
    o.append(box("top", (0.88, 0.48, 0.03), (0.0, 0.0, 1.665), DARK, 0.0))
    for k in range(3):
        z = 0.34 + k * 0.46
        o.append(box("drawer_%d" % k, (0.78, 0.02, 0.40), (0.0, -0.235, z), WHITE, 0.0))
        o.append(box("handle_%d" % k, (0.28, 0.03, 0.03), (0.0, -0.255, z + 0.12), STEEL, 0.0))
        o.append(box("label_%d" % k, (0.18, 0.01, 0.07), (0.0, -0.251, z - 0.10), ACCENT, 0.0))
    return o, (0.9, 0.5), 1.68, "medium", ("WoodPanel", "BrushedMetal")


def gen_printer_unit():
    """打印机 0.76×0.60×1.14：落地复合机，双纸盘 + 出纸口 + 扫描台 + 面板。"""
    o = []
    o.append(box("pedestal", (0.70, 0.56, 0.56), (0.0, 0.0, 0.30), WHITE, 0.012, 0.0, 1))
    for k in range(2):
        o.append(box("tray_%d" % k, (0.58, 0.02, 0.18), (0.0, -0.29, 0.20 + k * 0.24), DARK, 0.0))
        o.append(box("tray_h_%d" % k, (0.16, 0.02, 0.03), (0.0, -0.305, 0.26 + k * 0.24), STEEL, 0.0))
    o.append(box("body", (0.74, 0.58, 0.42), (0.0, 0.0, 0.79), DARK, 0.015, 0.0, 1))
    o.append(box("output", (0.56, 0.30, 0.05), (0.0, -0.10, 1.02), STEEL, 0.0))
    o.append(box("scanner", (0.68, 0.44, 0.08), (0.0, 0.02, 1.06), WHITE, 0.0))
    panel = box("panel", (0.26, 0.06, 0.18), (0.20, -0.30, 1.00), WHITE, 0.008, 0.0, 1)
    o.append(rot(panel, 0, -25))
    o.append(box("screen", (0.18, 0.01, 0.10), (0.21, -0.328, 1.02), _scr(), 0.0))
    return o, (0.76, 0.60), 1.14, "medium", ("CleanPanel", "DarkMetal")


def gen_coffee_table():
    """茶几 1.1×0.55×0.42：台面 + 下层板 + 四腿 + 横撑。"""
    o = []
    o.append(box("top", (1.08, 0.54, 0.05), (0.0, 0.0, 0.395), BODY, 0.01, 0.0, 1))
    o.append(box("shelf", (0.92, 0.42, 0.03), (0.0, 0.0, 0.16), BODY, 0.0))
    for x in (-0.47, 0.47):
        for y in (-0.22, 0.22):
            o.append(box("leg", (0.05, 0.05, 0.37), (x, y, 0.185), DARK, 0.0, 0.0, 1))
    for x in (-0.47, 0.47):
        o.append(box("rail", (0.05, 0.40, 0.04), (x, 0.0, 0.10), DARK, 0.0))
    return o, (1.1, 0.55), 0.42, "medium", ("WoodPanel", "BrushedMetal")


def gen_whiteboard():
    """白板 1.5×0.14×1.28：板面 + 铝框 + 笔槽 + 支腿 + 马克笔。"""
    o = []
    o.append(box("board", (1.40, 0.03, 1.00), (0.0, 0.0, 0.72), WHITE, 0.008, 0.0, 1))
    o.append(box("frame_t", (1.50, 0.06, 0.06), (0.0, 0.0, 1.245), STEEL, 0.0))
    o.append(box("frame_b", (1.50, 0.06, 0.06), (0.0, 0.0, 0.195), STEEL, 0.0))
    for x in (-0.73, 0.73):
        o.append(box("frame_s", (0.06, 0.06, 1.08), (x, 0.0, 0.72), STEEL, 0.0))
    o.append(box("tray", (0.90, 0.10, 0.03), (0.0, -0.06, 0.155), STEEL, 0.0))
    for x in (-0.72, 0.72):
        o.append(box("leg", (0.05, 0.09, 0.16), (x, 0.0, 0.08), DARK, 0.0))
    for k, x in enumerate((-0.20, 0.02)):
        o.append(box("marker_%d" % k, (0.14, 0.025, 0.025), (x, -0.08, 0.185),
                     ACCENT if k else DARK, 0.0))
    return o, (1.5, 0.14), 1.28, "medium", ("PaintedWall", "BrushedMetal")


# ---- B. 机房 / 中控 --------------------------------------------------------

def gen_precision_ac():
    """精密空调 1.6×0.9×2.0：柜体 + 上部出风格栅 + 检修门 + 顶部双风机（09 §2.2 1.6×0.9×2.0）。"""
    o = []
    o.append(box("body", (1.56, 0.86, 1.84), (0.0, 0.0, 0.96), BODY, 0.02, 0.0, 1))
    o.append(box("plinth", (1.44, 0.78, 0.10), (0.0, 0.0, 0.05), DARK, 0.0))
    o.append(box("top", (1.62, 0.90, 0.07), (0.0, 0.0, 1.915), DARK, 0.0))
    o.append(box("grille_frame", (1.30, 0.04, 0.46), (0.0, -0.445, 1.60), DARK, 0.0))
    for k in range(3):
        o.append(box("louvre_%d" % k, (1.22, 0.03, 0.09), (0.0, -0.465, 1.45 + k * 0.13), STEEL, 0.0))
    o.append(box("door", (1.16, 0.03, 0.60), (0.0, -0.445, 0.56), DARK, 0.0))
    o.append(box("handle", (0.05, 0.05, 0.18), (0.46, -0.465, 0.56), STEEL, 0.0))
    o.append(box("panel", (0.28, 0.02, 0.26), (-0.38, -0.448, 1.34), DARK, 0.0))
    o.append(box("screen", (0.18, 0.01, 0.12), (-0.38, -0.462, 1.36), _scr(), 0.0))
    for k in range(3):
        o.append(box("btn_%d" % k, (0.04, 0.01, 0.03), (-0.38, -0.462, 1.26 + k * 0.05), ACCENT, 0.0))
    # 顶部双冷凝风机：并排沿 X，直径 0.54 < 柜深 0.9，不超出占地
    for k, x in enumerate((-0.38, 0.38)):
        o.append(cyl("fan_ring_%d" % k, 0.27, 0.07, (x, 0.0, 1.97), DARK, 16, 0.0))
        for j in range(4):
            a = math.radians(45 + 90 * j)
            o.append(box("blade_%d_%d" % (k, j), (0.22, 0.05, 0.02),
                         (x + math.cos(a) * 0.10, math.sin(a) * 0.10, 1.995), STEEL, 0.0, a))
    return o, (1.6, 0.9), 2.0, "large", ("ServerPanel", "BrushedMetal")


def gen_cable_tray():
    """桥架段 2.0×0.5×0.60：梯式桥架 + 落地支腿 + 斜撑 + 三根电缆。"""
    o = []
    for y in (-0.22, 0.22):
        o.append(box("rail", (1.96, 0.05, 0.10), (0.0, y, 0.50), STEEL, 0.0, 0.0, 1))
    for k in range(7):
        o.append(box("rung_%d" % k, (0.05, 0.44, 0.025), (-0.84 + k * 0.28, 0.0, 0.45), STEEL, 0.0))
    for x in (-0.86, 0.86):
        for y in (-0.22, 0.22):
            o.append(box("leg", (0.05, 0.05, 0.45), (x, y, 0.225), DARK, 0.0, 0.0, 1))
        o.append(box("brace", (0.05, 0.44, 0.04), (x, 0.0, 0.16), DARK, 0.0))
    for k in range(3):
        o.append(rot(cyl("cable_%d" % k, 0.05, 1.86, (0.0, -0.12 + k * 0.12, 0.57), DARK, 8, 0.0), 1, 90))
    return o, (2.0, 0.5), 0.60, "medium", ("DiamondPlate", "BrushedMetal")


def gen_main_console():
    """主控台 2.4×1.0×1.22：三席位台体 + 斜面操作区 + 三联屏 + 键盘托（09 §2.2 预算 900）。"""
    o = []
    o.append(box("desk", (2.36, 0.96, 0.70), (0.0, 0.0, 0.36), DARK, 0.02, 0.0, 1))
    o.append(box("plinth", (2.28, 0.88, 0.08), (0.0, 0.0, 0.04), STEEL, 0.0))
    o.append(rot(box("panel", (2.28, 0.44, 0.06), (0.0, -0.26, 0.80), STEEL, 0.008, 0.0, 1), 0, 16))
    o.append(box("riser", (2.30, 0.34, 0.06), (0.0, 0.28, 0.74), DARK, 0.0))
    for k in range(3):
        x = -0.74 + k * 0.74
        o.append(box("monitor_%d" % k, (0.68, 0.06, 0.40), (x, 0.26, 0.97), DARK, 0.0))
        o.append(box("screen_%d" % k, (0.60, 0.01, 0.32), (x, 0.225, 0.97), _scr(), 0.0))
        o.append(box("keyboard_%d" % k, (0.40, 0.14, 0.02), (x, -0.20, 0.75), WHITE, 0.0))
    for x in (-1.05, 1.05):
        o.append(box("end", (0.06, 0.92, 0.72), (x, 0.0, 0.36), DARK, 0.0, 0.0, 1))
    return o, (2.4, 1.0), 1.22, "large", ("BrushedMetal", "DarkMetal")


def gen_big_screen_wall():
    """大屏墙 3.0×0.22×1.78：3×3 拼接屏 + 背架 + 底座（09 §2.2 预算 600）。"""
    o = []
    o.append(box("back", (2.90, 0.10, 1.66), (0.0, 0.04, 0.92), DARK, 0.0, 0.0, 1))
    for r in range(3):
        for c in range(3):
            x = -0.97 + c * 0.97
            z = 0.32 + r * 0.56
            o.append(box("bezel_%d_%d" % (r, c), (0.94, 0.06, 0.54), (x, -0.02, z), DARK, 0.0))
            o.append(box("screen_%d_%d" % (r, c), (0.88, 0.01, 0.48), (x, -0.055, z), _scr(), 0.0))
    o.append(box("base", (2.94, 0.18, 0.06), (0.0, 0.02, 0.03), STEEL, 0.0))
    for x in (-1.42, 1.42):
        o.append(box("trim", (0.06, 0.16, 1.74), (x, 0.02, 0.87), STEEL, 0.0, 0.0, 1))
    return o, (3.0, 0.22), 1.78, "large", ("CleanPanel", "DarkMetal")


def gen_floor_vent():
    """地板送风口 0.6×0.6×0.07：方框 + 可调格栅叶片 + 内胆。"""
    o = []
    o.append(box("frame", (0.58, 0.58, 0.05), (0.0, 0.0, 0.035), STEEL, 0.006, 0.0, 1))
    o.append(box("pan", (0.50, 0.50, 0.015), (0.0, 0.0, 0.015), DARK, 0.0))
    for k in range(6):
        o.append(box("blade_%d" % k, (0.46, 0.055, 0.012), (0.0, -0.20 + k * 0.08, 0.055), STEEL, 0.0))
    return o, (0.6, 0.6), 0.07, "small", ("DiamondPlate", "DarkMetal")


# ---- C. 冷链 / 仓储 --------------------------------------------------------

def gen_chiller_unit():
    """制冷机组 1.8×0.9×1.8：底座 + 压缩机箱 + 顶部双冷凝风机 + 立管 + 控制箱（预算 1200）。"""
    o = []
    o.append(box("skid", (1.74, 0.86, 0.10), (0.0, 0.0, 0.05), DARK, 0.0))
    o.append(box("body", (1.66, 0.80, 1.56), (0.0, 0.0, 0.87), BODY, 0.02, 0.0, 1))
    for k in range(4):
        o.append(box("louvre_%d" % k, (1.40, 0.03, 0.10), (0.0, -0.415, 0.30 + k * 0.17), STEEL, 0.0))
    o.append(box("panel", (0.28, 0.02, 0.22), (0.60, -0.415, 1.30), DARK, 0.0))
    o.append(box("screen", (0.18, 0.01, 0.10), (0.60, -0.427, 1.31), _scr(), 0.0))
    for k in range(3):
        o.append(box("btn_%d" % k, (0.04, 0.01, 0.03), (0.60, -0.427, 1.18 + k * 0.05), ACCENT, 0.0))
    o.append(box("top", (1.70, 0.86, 0.06), (0.0, 0.0, 1.69), DARK, 0.0))
    # 顶部双风机沿 X 并排，直径 0.56 < 机深 0.9
    for k, x in enumerate((-0.42, 0.42)):
        o.append(cyl("fan_ring_%d" % k, 0.28, 0.07, (x, 0.0, 1.745), DARK, 16, 0.0))
        for j in range(4):
            a = math.radians(45 + 90 * j)
            o.append(box("blade_%d_%d" % (k, j), (0.22, 0.05, 0.02),
                         (x + math.cos(a) * 0.11, math.sin(a) * 0.11, 1.775), STEEL, 0.0, a))
    for k, y in enumerate((-0.30, 0.30)):
        o.append(cyl("pipe_%d" % k, 0.055, 1.30, (-0.72, y, 0.72), STEEL, 10, 0.0))
    return o, (1.8, 0.9), 1.80, "large", ("ColdMetal", "IndustrialPaint")


def gen_insulated_box():
    """保温箱 0.8×0.6×0.63：厚壁箱体 + 盖 + 双卡扣 + 侧提手 + 角垫。"""
    o = []
    o.append(box("body", (0.76, 0.56, 0.50), (0.0, 0.0, 0.27), BODY, 0.02, 0.0, 1))
    o.append(box("lid", (0.78, 0.58, 0.10), (0.0, 0.0, 0.57), WHITE, 0.015, 0.0, 1))
    o.append(box("rim", (0.70, 0.50, 0.02), (0.0, 0.0, 0.51), DARK, 0.0))
    for x in (-0.26, 0.26):
        o.append(box("latch", (0.06, 0.04, 0.16), (x, -0.295, 0.48), ACCENT, 0.0))
    for x in (-0.40, 0.40):
        o.append(box("handle", (0.04, 0.16, 0.05), (x, 0.0, 0.36), STEEL, 0.0))
    for x in (-0.34, 0.34):
        for y in (-0.24, 0.24):
            o.append(box("bumper", (0.06, 0.06, 0.06), (x, y, 0.03), DARK, 0.0))
    return o, (0.8, 0.6), 0.63, "small", ("CleanPanel", "ColdMetal")


def gen_wooden_crate():
    """木箱 0.9×0.9×0.78：三层板条箱体 + 角柱 + 顶盖板条 + 对角加强条。"""
    o = []
    for k in range(3):
        z = 0.14 + k * 0.21
        for y in (-0.44, 0.44):
            o.append(box("plank_%d" % k, (0.88, 0.03, 0.18), (0.0, y, z), BODY, 0.0))
        for x in (-0.44, 0.44):
            o.append(box("side_%d" % k, (0.03, 0.88, 0.18), (x, 0.0, z), BODY, 0.0))
    for x in (-0.40, 0.40):
        for y in (-0.40, 0.40):
            o.append(box("post", (0.07, 0.07, 0.76), (x, y, 0.38), DARK, 0.0, 0.0, 1))
    for k in range(3):
        o.append(box("lid_%d" % k, (0.86, 0.26, 0.03), (0.0, -0.29 + k * 0.29, 0.765), BODY, 0.0))
    o.append(rot(box("brace", (0.94, 0.04, 0.10), (0.0, -0.455, 0.40), ACCENT, 0.0), 1, 38))
    return o, (0.9, 0.9), 0.78, "small", ("WoodPanel", "IndustrialPaint")


def gen_plastic_crate():
    """塑料周转箱 0.6×0.4×0.32：箱底 + 四壁 + 加强筋 + 叠放口 + 标签。"""
    o = []
    o.append(box("base", (0.56, 0.36, 0.03), (0.0, 0.0, 0.015), DARK, 0.0))
    for y in (-0.17, 0.17):
        o.append(box("wall", (0.56, 0.02, 0.28), (0.0, y, 0.17), ACCENT, 0.0))
    for x in (-0.27, 0.27):
        o.append(box("wall_s", (0.02, 0.36, 0.28), (x, 0.0, 0.17), ACCENT, 0.0))
    for k in range(5):
        x = -0.20 + k * 0.10
        o.append(box("rib_%d" % k, (0.02, 0.015, 0.24), (x, -0.175, 0.18), DARK, 0.0))
    o.append(box("rim", (0.58, 0.38, 0.02), (0.0, 0.0, 0.31), ACCENT, 0.0))
    o.append(box("label", (0.16, 0.01, 0.08), (0.0, -0.181, 0.20), WHITE, 0.0))
    return o, (0.6, 0.4), 0.32, "small", ("IndustrialPaint", "DarkMetal")


def gen_compact_shelf():
    """密集档案架 1.2×0.6×2.18：地轨 + 架体 + 五层板 + 摇柄 + 档案盒（09 §2.2 预算 800）。"""
    o = []
    for y in (-0.22, 0.22):
        o.append(box("rail", (1.10, 0.06, 0.05), (0.0, y, 0.025), STEEL, 0.0))
    for x in (-0.56, 0.56):
        o.append(box("end", (0.06, 0.56, 2.06), (x, 0.0, 1.08), DARK, 0.0, 0.0, 1))
    for k in range(5):
        o.append(box("shelf_%d" % k, (1.08, 0.52, 0.035), (0.0, 0.0, 0.32 + k * 0.44), BODY, 0.0))
    o.append(box("top", (1.16, 0.58, 0.05), (0.0, 0.0, 2.16), DARK, 0.0))
    o.append(box("back", (1.16, 0.03, 1.96), (0.0, 0.27, 1.10), BODY, 0.0))
    for k in range(3):
        o.append(box("box_%d" % k, (0.26, 0.40, 0.30), (-0.34 + k * 0.34, -0.02, 1.02), WHITE, 0.0))
    o.append(rot(cyl("wheel", 0.13, 0.04, (0.60, 0.0, 1.10), ACCENT, 12, 0.0), 1, 90))
    o.append(box("crank", (0.04, 0.04, 0.20), (0.62, -0.10, 1.02), ACCENT, 0.0))
    return o, (1.2, 0.6), 2.18, "large", ("PaintedWall", "WoodPanel")


# ---- D. 制造 / 设备 --------------------------------------------------------

def gen_machine_tool():
    """机床 1.8×1.2×1.52：床身 + 立柱 + 主轴箱 + 工作台 + 防护拉门 + 数控面板（预算 1400）。"""
    o = []
    o.append(box("base", (1.76, 1.16, 0.52), (0.0, 0.0, 0.26), DARK, 0.02, 0.0, 1))
    o.append(box("table", (1.10, 0.68, 0.10), (0.0, -0.06, 0.57), STEEL, 0.0))
    o.append(box("column", (0.44, 0.34, 1.00), (0.0, 0.40, 1.02), BODY, 0.02, 0.0, 1))
    o.append(box("head", (0.38, 0.46, 0.40), (0.0, 0.18, 1.10), BODY, 0.015, 0.0, 1))
    o.append(box("spindle", (0.12, 0.12, 0.22), (0.0, -0.02, 0.94), STEEL, 0.0))
    for x in (-0.88, 0.88):
        o.append(box("side", (0.03, 0.90, 0.86), (x, -0.06, 0.86), BODY, 0.0, 0.0, 1))
    o.append(box("top_beam", (1.76, 0.06, 0.06), (0.0, -0.56, 1.30), DARK, 0.0))
    o.append(box("door", (0.62, 0.03, 0.78), (-0.36, -0.56, 0.90), WHITE, 0.008, 0.0, 1))
    o.append(box("door_rail", (1.64, 0.04, 0.04), (0.0, -0.56, 1.26), STEEL, 0.0))
    o.append(rot(box("panel", (0.34, 0.06, 0.30), (0.72, -0.52, 1.12), DARK, 0.008, 0.0, 1), 0, -20))
    o.append(box("screen", (0.24, 0.01, 0.18), (0.73, -0.552, 1.13), _scr(), 0.0))
    for k in range(3):
        o.append(box("btn_%d" % k, (0.05, 0.01, 0.03), (0.70, -0.552, 1.00 + k * 0.05), ACCENT, 0.0))
    o.append(box("chip_tray", (0.90, 0.16, 0.05), (0.0, -0.46, 0.42), STEEL, 0.0))
    return o, (1.8, 1.2), 1.52, "large", ("IndustrialPaint", "DiamondPlate")


def gen_material_rack():
    """料架 1.6×0.8×2.0：四立柱 + 四层板 + 斜撑 + 料筐（09 §2.2 预算 900）。"""
    o = []
    for x in (-0.76, 0.76):
        for y in (-0.36, 0.36):
            o.append(box("post", (0.07, 0.07, 1.96), (x, y, 0.98), STEEL, 0.0, 0.0, 1))
    for k in range(4):
        o.append(box("shelf_%d" % k, (1.54, 0.74, 0.05), (0.0, 0.0, 0.26 + k * 0.50), BODY, 0.0, 0.0, 1))
    for x in (-0.76, 0.76):
        for z in (0.51, 1.01, 1.51):
            o.append(box("brace", (0.05, 0.05, 0.46), (x, -0.36, z), DARK, 0.0))
        o.append(box("rail", (0.05, 0.74, 0.05), (x, 0.0, 1.94), DARK, 0.0))
    for k in range(3):
        o.append(box("bin_%d" % k, (0.36, 0.32, 0.20), (-0.48 + k * 0.48, 0.0, 0.39),
                     ACCENT if k % 2 else WHITE, 0.0))
    for k in range(2):
        o.append(box("bin_b_%d" % k, (0.36, 0.32, 0.18), (-0.24 + k * 0.48, 0.0, 0.89), BODY, 0.0))
    return o, (1.6, 0.8), 2.0, "large", ("IndustrialPaint", "WoodPanel")


def gen_valve_group():
    """阀门组 0.8×0.8×1.56：立管 + 三层支管 + 三只闸阀 + 法兰 + 压力表（预算 400）。"""
    o = []
    o.append(box("base", (0.70, 0.70, 0.10), (0.0, 0.0, 0.05), DARK, 0.0))
    o.append(cyl("riser", 0.085, 1.46, (0.0, 0.0, 0.83), STEEL, 12, 0.0))
    for k, (z, s) in enumerate(((0.52, -1), (0.90, 1), (1.28, -1))):
        y = 0.18 * s
        o.append(rot(cyl("branch_%d" % k, 0.06, 0.36, (0.0, y, z), STEEL, 10, 0.0), 0, 90))
        o.append(box("valve_%d" % k, (0.16, 0.16, 0.14), (0.0, y, z), ACCENT, 0.0))
        o.append(cyl("handle_%d" % k, 0.11, 0.03, (0.0, y, z + 0.10), ACCENT, 12, 0.0))
        o.append(rot(cyl("flange_%d" % k, 0.115, 0.03, (0.0, y + 0.18 * s, z), STEEL, 12, 0.0), 0, 90))
    for k in range(2):
        o.append(rot(cyl("gauge_%d" % k, 0.06, 0.05, (0.10, 0.0, 1.16 + k * 0.22), WHITE, 10, 0.0), 0, 90))
    return o, (0.8, 0.8), 1.56, "medium", ("IndustrialPaint", "BrushedMetal")


def gen_pipe_segment():
    """管道组 4.0×0.6×0.88：三根平行管 + 法兰 + 门式支架 + 弯头（09 §2.2 预算 600）。"""
    o = []
    for k, (y, z, r) in enumerate(((-0.18, 0.66, 0.10), (0.02, 0.50, 0.08), (0.20, 0.68, 0.07))):
        o.append(rot(cyl("pipe_%d" % k, r, 3.86, (0.0, y, z), ACCENT if k == 1 else BODY, 12, 0.0), 1, 90))
        for sx in (-1.20, 1.20):
            o.append(rot(cyl("flange_%d" % k, r + 0.045, 0.05, (sx, y, z), STEEL, 12, 0.0), 1, 90))
    for x in (-1.60, 1.60):
        for y in (-0.26, 0.26):
            o.append(box("post", (0.07, 0.07, 0.80), (x, y, 0.40), STEEL, 0.0))
        o.append(box("beam", (0.07, 0.62, 0.07), (x, 0.0, 0.80), STEEL, 0.0))
        o.append(box("foot", (0.16, 0.66, 0.05), (x, 0.0, 0.025), DARK, 0.0))
    o.append(cyl("bend", 0.10, 0.22, (1.86, -0.18, 0.78), BODY, 10, 0.0))
    return o, (4.0, 0.6), 0.88, "large", ("IndustrialPaint", "BrushedMetal")


def gen_transformer():
    """变压器 1.8×1.2×1.60：油箱 + 八片散热器 + 三只套管 + 底座（09 §2.2 预算 1400）。"""
    o = []
    o.append(box("base", (1.70, 1.10, 0.14), (0.0, 0.0, 0.07), DARK, 0.0))
    o.append(box("tank", (1.44, 0.92, 0.96), (0.0, 0.0, 0.62), BODY, 0.02, 0.0, 1))
    for k in range(4):
        y = -0.34 + k * 0.22
        for sx in (-1, 1):
            o.append(box("fin_%d" % k, (0.05, 0.16, 0.74), (sx * 0.745, y, 0.62), DARK, 0.0))
    o.append(box("cover", (1.52, 1.00, 0.10), (0.0, 0.0, 1.15), DARK, 0.0))
    for k in range(3):
        x = -0.44 + k * 0.44
        o.append(cyl("bushing_%d" % k, 0.075, 0.34, (x, 0.0, 1.37), WHITE, 10, 0.0))
        o.append(cyl("cap_%d" % k, 0.05, 0.06, (x, 0.0, 1.57), STEEL, 8, 0.0))
    o.append(box("panel", (0.26, 0.03, 0.34), (-0.52, -0.48, 0.62), STEEL, 0.0))
    o.append(box("gauge", (0.10, 0.01, 0.10), (-0.52, -0.50, 0.70), WHITE, 0.0))
    return o, (1.8, 1.2), 1.60, "large", ("ColdMetal", "IndustrialPaint")


def gen_hv_cabinet():
    """高压柜 1.0×0.6×2.10：柜体 + 单门 + 仪表窗 + 指示灯 + 观察窗 + 警示条（预算 700）。"""
    o = []
    o.append(box("body", (0.96, 0.56, 2.08), (0.0, 0.0, 1.06), BODY, 0.02, 0.0, 1))
    o.append(box("plinth", (0.90, 0.50, 0.10), (0.0, 0.0, 0.05), DARK, 0.0))
    o.append(box("door", (0.86, 0.03, 1.84), (0.0, -0.29, 1.04), DARK, 0.0))
    o.append(box("handle", (0.04, 0.05, 0.22), (0.32, -0.305, 1.04), STEEL, 0.0))
    o.append(box("meter", (0.40, 0.02, 0.30), (0.0, -0.305, 1.72), WHITE, 0.0))
    for x in (-0.10, 0.10):
        o.append(rot(cyl("gauge", 0.08, 0.03, (x, -0.318, 1.72), DARK, 10, 0.0), 0, 90))
    for k in range(4):
        o.append(box("lamp_%d" % k, (0.06, 0.02, 0.06), (-0.24 + k * 0.16, -0.305, 1.42), ACCENT, 0.0))
    o.append(box("window", (0.36, 0.02, 0.34), (0.0, -0.305, 0.90), GLASS_MAT, 0.0))
    o.append(box("hazard", (0.82, 0.01, 0.10), (0.0, -0.302, 0.22), ACCENT, 0.0))
    return o, (1.0, 0.6), 2.10, "large", ("IndustrialPaint", "MetalDoor")


def gen_forklift():
    """叉车 1.1×2.2×1.60：车身 + 配重 + 门架 + 货叉 + 护顶架 + 四轮。

    车头朝 -Y（Unity 里即 +Z），货叉向前伸出，方便沿装卸口摆放时"车头对外"。
    """
    o = []
    o.append(box("chassis", (0.96, 1.24, 0.42), (0.0, -0.02, 0.46), ACCENT, 0.02, 0.0, 1))
    o.append(box("counter", (0.94, 0.44, 0.58), (0.0, 0.72, 0.52), DARK, 0.02, 0.0, 1))
    o.append(box("seat", (0.36, 0.40, 0.12), (0.0, 0.34, 0.74), DARK, 0.0))
    o.append(box("back", (0.36, 0.10, 0.40), (0.0, 0.56, 0.96), DARK, 0.0))
    for x in (-0.28, 0.28):
        o.append(box("mast", (0.07, 0.07, 1.44), (x, -0.60, 0.72), STEEL, 0.0, 0.0, 1))
    for z in (0.30, 0.86, 1.42):
        o.append(box("cross", (0.60, 0.10, 0.06), (0.0, -0.60, z), STEEL, 0.0))
    o.append(box("carriage", (0.62, 0.06, 0.56), (0.0, -0.70, 0.30), STEEL, 0.0, 0.0, 1))
    for x in (-0.20, 0.20):
        o.append(box("fork", (0.10, 0.55, 0.04), (x, -0.99, 0.06), STEEL, 0.0))
        o.append(box("fork_bend", (0.10, 0.10, 0.26), (x, -0.74, 0.17), STEEL, 0.0))
    for k, (x, y) in enumerate(((0.44, -0.40), (-0.44, -0.40), (0.44, 0.50), (-0.44, 0.50))):
        o.append(rot(cyl("wheel_%d" % k, 0.22, 0.16, (x, y, 0.22), DARK, 12, 0.0), 1, 90))
    for x in (-0.40, 0.40):
        for y in (-0.26, 0.34):
            o.append(box("guard_post", (0.05, 0.05, 0.70), (x, y, 1.20), STEEL, 0.0))
    o.append(box("guard_roof", (0.94, 0.74, 0.05), (0.0, 0.04, 1.57), STEEL, 0.0))
    o.append(rot(cyl("steer", 0.11, 0.04, (0.0, 0.24, 1.00), DARK, 10, 0.0), 0, 70))
    return o, (1.1, 2.2), 1.60, "large", ("IndustrialPaint", "DiamondPlate")


def gen_container():
    """集装箱 2.4×1.2×2.40：波纹板 + 双开门 + 锁杆 + 角柱（预算 1500）。"""
    o = []
    o.append(box("shell", (2.32, 1.14, 2.28), (0.0, 0.0, 1.16), BODY, 0.02, 0.0, 1))
    for k in range(9):
        o.append(box("rib_%d" % k, (0.06, 0.02, 2.10), (-1.00 + k * 0.25, -0.585, 1.16), DARK, 0.0))
    for x in (-0.56, 0.56):
        o.append(box("door", (0.54, 0.03, 2.04), (x, -0.585, 1.14), DARK, 0.0))
    for k in range(4):
        o.append(box("lockbar_%d" % k, (0.05, 0.05, 1.90), (-0.86 + k * 0.573, -0.60, 1.14), STEEL, 0.0))
    for x in (-1.18, 1.18):
        for y in (-0.57, 0.57):
            o.append(box("corner", (0.12, 0.12, 2.36), (x, y, 1.18), DARK, 0.0, 0.0, 1))
    for z in (0.10, 2.28):
        o.append(box("rail", (2.40, 1.16, 0.06), (0.0, 0.0, z), DARK, 0.0))
    o.append(box("stencil", (0.70, 0.01, 0.22), (0.40, -0.596, 2.02), ACCENT, 0.0))
    return o, (2.4, 1.2), 2.40, "large", ("IndustrialPaint", "MetalDoor")


# ---- E. 医疗 / 洁净 --------------------------------------------------------

def gen_isolation_pod():
    """隔离舱 1.2×0.8×2.10：舱壳 + 观察玻璃 + 密封门 + 顶部过滤箱 + 舱内床垫（预算 700）。"""
    o = []
    o.append(box("base", (1.16, 0.76, 0.12), (0.0, 0.0, 0.06), DARK, 0.0))
    o.append(box("back", (1.12, 0.04, 1.72), (0.0, 0.36, 0.98), WHITE, 0.0, 0.0, 1))
    for x in (-0.55, 0.55):
        o.append(box("side", (0.04, 0.74, 1.72), (x, 0.0, 0.98), WHITE, 0.0, 0.0, 1))
    o.append(box("roof", (1.16, 0.78, 0.12), (0.0, 0.0, 1.90), WHITE, 0.0, 0.0, 1))
    for k in range(2):
        o.append(box("filter_%d" % k, (0.46, 0.46, 0.16), (-0.28 + k * 0.56, 0.0, 2.02), STEEL, 0.0))
    o.append(box("glass", (0.96, 0.02, 1.40), (0.0, -0.37, 1.06), GLASS_MAT, 0.0))
    for x in (-0.50, 0.50):
        o.append(box("frame", (0.05, 0.04, 1.72), (x, -0.37, 0.98), STEEL, 0.0))
    o.append(box("door_split", (0.03, 0.04, 1.44), (0.0, -0.375, 1.04), STEEL, 0.0))
    o.append(box("handle", (0.04, 0.05, 0.24), (0.10, -0.395, 1.02), STEEL, 0.0))
    o.append(box("mattress", (0.92, 0.62, 0.14), (0.0, 0.02, 0.56), LINEN_MAT, 0.0))
    for x in (-0.35, 0.35):
        o.append(box("rail", (0.04, 0.60, 0.04), (x, 0.02, 0.78), STEEL, 0.0))
    return o, (1.2, 0.8), 2.10, "large", ("CleanPanel", "GlassPanel")


def gen_washbasin():
    """洗手台 0.6×0.45×1.72：落地台盆柱 + 台面 + 盆体 + 龙头 + 镜子 + 存水弯（小件）。"""
    o = []
    o.append(box("pedestal", (0.34, 0.34, 0.78), (0.0, 0.04, 0.41), WHITE, 0.012, 0.0, 1))
    o.append(box("ped_foot", (0.44, 0.42, 0.04), (0.0, 0.04, 0.02), DARK, 0.0))
    o.append(box("counter", (0.58, 0.44, 0.06), (0.0, 0.0, 0.82), WHITE, 0.01, 0.0, 1))
    o.append(box("apron", (0.56, 0.06, 0.18), (0.0, -0.20, 0.72), WHITE, 0.0))
    o.append(cyl("bowl", 0.19, 0.12, (0.0, -0.02, 0.86), WHITE, 12, 0.0))
    o.append(cyl("drain", 0.03, 0.05, (0.0, -0.02, 0.80), DARK, 8, 0.0))
    o.append(cyl("faucet", 0.025, 0.20, (0.0, 0.16, 0.94), STEEL, 8, 0.0))
    o.append(box("spout", (0.03, 0.14, 0.03), (0.0, 0.10, 1.02), STEEL, 0.0))
    o.append(box("mirror_frame", (0.54, 0.02, 0.64), (0.0, 0.20, 1.40), STEEL, 0.0))
    o.append(box("mirror", (0.50, 0.03, 0.60), (0.0, 0.19, 1.40), GLASS_MAT, 0.0))
    for x in (-0.22, 0.22):
        o.append(box("bracket", (0.04, 0.12, 0.05), (x, 0.12, 0.76), STEEL, 0.0))
    o.append(cyl("p_trap", 0.045, 0.30, (0.0, 0.02, 0.62), STEEL, 8, 0.0))
    return o, (0.6, 0.45), 1.72, "small", ("CleanPanel", "GlassPanel")


def gen_iv_stand():
    """输液架 0.45×0.45×1.86：五爪底座 + 立杆 + 升降杆 + 双钩 + 输液袋。"""
    o = []
    for k in range(5):
        a = math.radians(72 * k)
        o.append(box("foot_%d" % k, (0.26, 0.05, 0.04),
                     (math.cos(a) * 0.10, math.sin(a) * 0.10, 0.06), DARK, 0.0, a))
        o.append(box("caster_%d" % k, (0.06, 0.06, 0.06),
                     (math.cos(a) * 0.21, math.sin(a) * 0.21, 0.03), DARK, 0.0))
    o.append(cyl("pole", 0.022, 1.52, (0.0, 0.0, 0.83), STEEL, 8, 0.0))
    o.append(cyl("sleeve", 0.035, 0.18, (0.0, 0.0, 1.18), STEEL, 10, 0.0))
    o.append(cyl("top_pole", 0.016, 0.30, (0.0, 0.0, 1.70), STEEL, 8, 0.0))
    o.append(box("cross", (0.34, 0.03, 0.03), (0.0, 0.0, 1.84), STEEL, 0.0))
    for x in (-0.17, 0.17):
        o.append(box("hook", (0.03, 0.03, 0.10), (x, 0.0, 1.78), STEEL, 0.0))
    o.append(box("bag", (0.14, 0.06, 0.20), (-0.14, 0.0, 1.64), WHITE, 0.0))
    o.append(box("bag_label", (0.10, 0.01, 0.06), (-0.14, -0.032, 1.66), ACCENT, 0.0))
    return o, (0.45, 0.45), 1.86, "small", ("CleanPanel", "BrushedMetal")


def gen_air_shower():
    """风淋室 1.2×1.2×2.24：双门舱体 + 侧壁喷嘴 + 顶部灯箱 + 底盘（09 §2.2 预算 600）。"""
    o = []
    o.append(box("floor", (1.16, 1.16, 0.06), (0.0, 0.0, 0.03), DARK, 0.0))
    o.append(box("roof", (1.18, 1.18, 0.10), (0.0, 0.0, 2.15), WHITE, 0.0, 0.0, 1))
    for x in (-0.58, 0.58):
        o.append(box("side", (0.04, 1.16, 2.06), (x, 0.0, 1.06), WHITE, 0.0, 0.0, 1))
        for k in range(4):
            o.append(box("nozzle", (0.03, 0.05, 0.05), (x, -0.36 + k * 0.24, 1.30), STEEL, 0.0))
    o.append(box("back", (1.12, 0.04, 2.06), (0.0, 0.56, 1.06), WHITE, 0.0))
    for y, dy in ((-0.58, -0.02), (0.58, 0.02)):
        o.append(box("door_frame", (0.94, 0.05, 1.90), (0.0, y, 1.02), STEEL, 0.0))
        o.append(box("door", (0.80, 0.03, 1.80), (0.0, y + dy, 1.02), GLASS_MAT, 0.0))
    o.append(box("lamp", (0.50, 0.10, 0.06), (0.30, 0.0, 2.10), ACCENT, 0.0))
    o.append(box("switch", (0.16, 0.02, 0.24), (0.42, -0.60, 1.30), DARK, 0.0))
    return o, (1.2, 1.2), 2.24, "large", ("CleanPanel", "GlassPanel")


def gen_clean_bench():
    """洁净工作台 1.5×0.8×1.66：台面 + 层流罩 + 玻璃侧挡 + 下架 + 风机箱（预算 700）。"""
    o = []
    o.append(box("top", (1.46, 0.76, 0.06), (0.0, 0.0, 0.86), WHITE, 0.01, 0.0, 1))
    for x in (-0.68, 0.68):
        for y in (-0.32, 0.32):
            o.append(box("leg", (0.05, 0.05, 0.82), (x, y, 0.41), STEEL, 0.0, 0.0, 1))
    o.append(box("shelf", (1.30, 0.60, 0.03), (0.0, 0.0, 0.24), STEEL, 0.0))
    o.append(box("back", (1.46, 0.05, 0.56), (0.0, 0.36, 1.18), WHITE, 0.0))
    o.append(box("hood_top", (1.46, 0.62, 0.08), (0.0, 0.02, 1.46), WHITE, 0.0))
    o.append(box("filter", (1.30, 0.50, 0.06), (0.0, 0.02, 1.40), STEEL, 0.0))
    o.append(box("fan_box", (0.90, 0.40, 0.16), (0.0, 0.30, 1.58), DARK, 0.0))
    for x in (-0.70, 0.70):
        o.append(box("glass", (0.02, 0.56, 0.50), (x, -0.08, 1.16), GLASS_MAT, 0.0))
        o.append(box("post", (0.05, 0.05, 0.56), (x, 0.34, 1.20), WHITE, 0.0))
    o.append(box("control", (0.24, 0.02, 0.14), (0.52, 0.37, 1.26), DARK, 0.0))
    for k in range(3):
        o.append(box("btn_%d" % k, (0.04, 0.01, 0.03), (0.52, 0.355, 1.18 + k * 0.05), ACCENT, 0.0))
    return o, (1.5, 0.8), 1.66, "large", ("CleanPanel", "GlassPanel")


def gen_changing_locker():
    """更衣柜 0.9×0.6×1.80：双门挂衣柜 + 底部鞋格 + 通风百叶（09 §2.2 预算 500）。"""
    o = []
    o.append(box("body", (0.86, 0.56, 1.56), (0.0, 0.0, 0.98), WHITE, 0.015, 0.0, 1))
    o.append(box("shoe", (0.88, 0.58, 0.18), (0.0, 0.0, 0.09), DARK, 0.0))
    o.append(box("top", (0.90, 0.58, 0.04), (0.0, 0.0, 1.78), DARK, 0.0))
    o.append(box("split", (0.02, 0.57, 1.50), (0.0, 0.0, 0.98), DARK, 0.0))
    for x in (-0.22, 0.22):
        o.append(box("door", (0.40, 0.02, 1.44), (x, -0.285, 0.96), WHITE, 0.0))
        for k in range(3):
            o.append(box("vent", (0.24, 0.01, 0.02), (x, -0.296, 1.50 + k * 0.06), DARK, 0.0))
        o.append(box("handle", (0.04, 0.04, 0.16), (x + 0.15, -0.30, 0.96), STEEL, 0.0))
    for k in range(2):
        o.append(box("shoe_door", (0.40, 0.02, 0.16), (-0.22 + k * 0.44, -0.295, 0.09), DARK, 0.0))
    return o, (0.9, 0.6), 1.80, "medium", ("PaintedWall", "BrushedMetal")


# ---- F. 生活舱 -------------------------------------------------------------

def gen_bunk_bed():
    """双层床 2.0×0.9×1.80：四立柱 + 上下床架 + 床垫 + 护栏 + 侧梯（09 §2.2 预算 900）。"""
    o = []
    for x in (-0.94, 0.94):
        for y in (-0.41, 0.41):
            o.append(box("post", (0.06, 0.06, 1.76), (x, y, 0.88), STEEL, 0.0, 0.0, 1))
    for z in (0.36, 1.30):
        o.append(box("frame", (1.94, 0.86, 0.08), (0.0, 0.0, z), DARK, 0.0, 0.0, 1))
        o.append(box("mattress", (1.86, 0.78, 0.14), (0.0, 0.0, z + 0.11), LINEN_MAT, 0.0, 0.0, 1))
        o.append(box("pillow", (0.50, 0.68, 0.10), (-0.62, 0.0, z + 0.22), WHITE, 0.0))
    for z in (1.56, 1.72):
        o.append(box("rail", (1.86, 0.04, 0.04), (0.0, -0.40, z), STEEL, 0.0))
    for x in (-0.60, 0.0, 0.60):
        o.append(box("rail_post", (0.04, 0.04, 0.30), (x, -0.40, 1.60), STEEL, 0.0))
    for x in (0.30, 0.42):
        o.append(box("ladder", (0.04, 0.05, 1.28), (x, -0.46, 0.66), STEEL, 0.0))
    for k in range(4):
        o.append(box("rung_%d" % k, (0.14, 0.04, 0.04), (0.36, -0.46, 0.30 + k * 0.30), STEEL, 0.0))
    return o, (2.0, 0.9), 1.80, "large", ("WoodPanel", "BrushedMetal")


def gen_mini_fridge():
    """小冰箱 0.5×0.55×0.81：柜体 + 门 + 密封条 + 把手 + 顶部散热格栅。"""
    o = []
    o.append(box("body", (0.48, 0.52, 0.74), (0.0, 0.0, 0.40), WHITE, 0.015, 0.0, 1))
    o.append(box("door", (0.44, 0.03, 0.66), (0.0, -0.275, 0.42), WHITE, 0.0, 0.0, 1))
    o.append(box("seal", (0.38, 0.01, 0.60), (0.0, -0.292, 0.42), DARK, 0.0))
    o.append(box("handle", (0.04, 0.05, 0.24), (0.16, -0.30, 0.46), STEEL, 0.0))
    o.append(box("top", (0.48, 0.52, 0.04), (0.0, 0.0, 0.79), DARK, 0.0))
    for k in range(3):
        o.append(box("vent_%d" % k, (0.40, 0.02, 0.02), (0.0, -0.29, 0.75 + k * 0.04), DARK, 0.0))
    for x in (-0.18, 0.18):
        for y in (-0.20, 0.20):
            o.append(box("foot", (0.04, 0.04, 0.03), (x, y, 0.015), DARK, 0.0))
    return o, (0.5, 0.55), 0.81, "small", ("CleanPanel", "BrushedMetal")


def gen_microwave():
    """微波炉 0.5×0.4×0.31：机身 + 门玻璃 + 控制面板 + 脚垫。"""
    o = []
    o.append(box("body", (0.48, 0.38, 0.28), (0.0, 0.0, 0.17), WHITE, 0.01, 0.0, 1))
    o.append(box("door", (0.32, 0.02, 0.24), (-0.06, -0.195, 0.17), DARK, 0.0))
    o.append(box("glass", (0.20, 0.01, 0.16), (-0.06, -0.208, 0.17), GLASS_MAT, 0.0))
    o.append(box("panel", (0.12, 0.02, 0.24), (0.16, -0.195, 0.17), DARK, 0.0))
    for k in range(3):
        o.append(box("btn_%d" % k, (0.05, 0.01, 0.02), (0.16, -0.208, 0.12 + k * 0.05), STEEL, 0.0))
    o.append(box("display", (0.06, 0.01, 0.02), (0.16, -0.208, 0.25), _scr(), 0.0))
    for x in (-0.16, 0.16):
        for y in (-0.12, 0.12):
            o.append(box("foot", (0.03, 0.03, 0.03), (x, y, 0.015), DARK, 0.0))
    return o, (0.5, 0.4), 0.31, "small", ("CleanPanel", "DarkMetal")


def gen_dining_table():
    """餐桌 1.2×0.8×0.74：台面 + 四腿 + 横撑（生活舱用餐）。"""
    o = []
    o.append(box("top", (1.18, 0.78, 0.05), (0.0, 0.0, 0.715), BODY, 0.01, 0.0, 1))
    for x in (-0.52, 0.52):
        for y in (-0.32, 0.32):
            o.append(box("leg", (0.05, 0.05, 0.69), (x, y, 0.345), STEEL, 0.0, 0.0, 1))
    for y in (-0.32, 0.32):
        o.append(box("rail", (1.02, 0.04, 0.05), (0.0, y, 0.16), STEEL, 0.0))
    return o, (1.2, 0.8), 0.74, "medium", ("WoodPanel", "BrushedMetal")


# ---- G. 生成器点名、但还没有独立模型的配套件（FacilityGrammar.PropsOf） ---------

def gen_row_header_cabinet():
    """列头柜 0.8×0.6×2.02：机柜 + 前门 + 断路器排 + 指示灯（生成器点名「列头柜」）。"""
    o = []
    o.append(box("body", (0.78, 0.56, 1.94), (0.0, 0.0, 0.99), DARK, 0.02, 0.0, 1))
    o.append(box("plinth", (0.72, 0.50, 0.08), (0.0, 0.0, 0.04), STEEL, 0.0))
    o.append(box("door", (0.68, 0.03, 1.80), (0.0, -0.29, 1.00), STEEL, 0.0))
    for k in range(4):
        o.append(box("breaker_%d" % k, (0.52, 0.02, 0.07), (0.0, -0.305, 0.56 + k * 0.28), WHITE, 0.0))
    for k in range(3):
        o.append(box("lamp_%d" % k, (0.05, 0.01, 0.05), (-0.20 + k * 0.20, -0.305, 1.72), ACCENT, 0.0))
    o.append(box("meter", (0.24, 0.02, 0.16), (0.0, -0.305, 1.46), WHITE, 0.0))
    o.append(box("cable_entry", (0.36, 0.20, 0.06), (0.0, 0.0, 1.99), STEEL, 0.0))
    o.append(box("handle", (0.03, 0.04, 0.18), (0.28, -0.31, 1.00), STEEL, 0.0))
    return o, (0.8, 0.6), 2.02, "large", ("ServerPanel", "DarkMetal")


def gen_patch_panel_rack():
    """配线架 0.6×0.45×1.92：开放架 + 八层跳线盘 + 理线环 + 底座（生成器点名「配线架」）。"""
    o = []
    for x in (-0.26, 0.26):
        o.append(box("post", (0.05, 0.05, 1.86), (x, 0.0, 0.93), STEEL, 0.0, 0.0, 1))
    o.append(box("base", (0.58, 0.42, 0.05), (0.0, 0.0, 0.025), DARK, 0.0))
    for k in range(8):
        z = 0.30 + k * 0.20
        o.append(box("panel_%d" % k, (0.46, 0.32, 0.025), (0.0, 0.0, z), DARK, 0.0))
        for j in range(4):
            o.append(box("port_%d_%d" % (k, j), (0.03, 0.02, 0.02),
                         (-0.15 + j * 0.10, -0.17, z), WHITE, 0.0))
        o.append(box("ring_%d" % k, (0.04, 0.10, 0.16), (0.24, 0.0, z), STEEL, 0.0))
    return o, (0.6, 0.45), 1.92, "medium", ("ServerPanel", "BrushedMetal")


def gen_tool_cabinet():
    """工具柜 1.0×0.5×1.62：双门柜 + 门板工具挂具 + 踢脚（生成器点名「工具柜」）。"""
    o = []
    o.append(box("body", (0.96, 0.46, 1.50), (0.0, 0.0, 0.83), BODY, 0.015, 0.0, 1))
    o.append(box("plinth", (0.90, 0.40, 0.08), (0.0, 0.0, 0.04), DARK, 0.0))
    o.append(box("top", (0.98, 0.48, 0.04), (0.0, 0.0, 1.60), DARK, 0.0))
    for x in (-0.24, 0.24):
        o.append(box("door", (0.44, 0.02, 1.38), (x, -0.24, 0.84), DARK, 0.0))
        o.append(box("handle", (0.03, 0.04, 0.18), (x + 0.17, -0.26, 0.84), STEEL, 0.0))
    for k in range(3):
        o.append(box("tool_%d" % k, (0.05, 0.04, 0.24), (-0.30 + k * 0.30, -0.255, 1.24), ACCENT, 0.0))
    return o, (1.0, 0.5), 1.62, "medium", ("IndustrialPaint", "DiamondPlate")


def gen_isolation_bed():
    """隔离床 1.2×2.0×1.94：加宽床体 + 头尾板 + 四柱隔离罩 + 透明帘 + 脚轮（生成器点名「隔离床」）。"""
    o = []
    o.append(box("frame", (1.16, 1.96, 0.10), (0.0, 0.0, 0.48), STEEL, 0.0, 0.0, 1))
    o.append(box("mattress", (1.10, 1.88, 0.16), (0.0, 0.0, 0.61), WHITE, 0.0, 0.0, 1))
    o.append(box("pillow", (0.54, 0.34, 0.10), (0.0, -0.66, 0.74), LINEN_MAT, 0.0))
    for y in (-0.98, 0.98):
        o.append(box("board", (1.16, 0.05, 0.46), (0.0, y, 0.70), STEEL, 0.0))
    for x in (-0.56, 0.56):
        for y in (-0.92, 0.92):
            o.append(box("canopy_post", (0.04, 0.04, 1.10), (x, y, 1.40), STEEL, 0.0, 0.0, 1))
    for x in (-0.56, 0.56):
        o.append(box("canopy_rail", (0.04, 1.84, 0.04), (x, 0.0, 1.92), STEEL, 0.0))
        o.append(box("curtain", (0.01, 1.84, 1.00), (x, 0.0, 1.40), GLASS_MAT, 0.0))
    for y in (-0.92, 0.92):
        o.append(box("canopy_cross", (1.12, 0.04, 0.04), (0.0, y, 1.92), STEEL, 0.0))
    for x in (-0.50, 0.50):
        for y in (-0.86, 0.86):
            o.append(rot(cyl("wheel", 0.06, 0.05, (x, y, 0.06), DARK, 8, 0.0), 0, 90))
    return o, (1.2, 2.0), 1.94, "large", ("CleanPanel", "GlassPanel")


def gen_pass_box():
    """传递柜 0.9×0.5×1.78：双面互锁传递窗 + 两扇门 + 紫外灯 + 控制面板（生成器点名「传递柜」）。"""
    o = []
    o.append(box("body", (0.86, 0.46, 1.72), (0.0, 0.0, 0.87), WHITE, 0.02, 0.0, 1))
    o.append(box("plinth", (0.80, 0.40, 0.08), (0.0, 0.0, 0.04), DARK, 0.0))
    for z in (0.52, 1.24):
        o.append(box("hatch_frame", (0.68, 0.02, 0.54), (0.0, -0.235, z), STEEL, 0.0))
        o.append(box("hatch", (0.60, 0.03, 0.46), (0.0, -0.24, z), GLASS_MAT, 0.0))
        o.append(box("handle", (0.16, 0.03, 0.04), (0.0, -0.255, z - 0.20), STEEL, 0.0))
    o.append(box("uv", (0.50, 0.02, 0.04), (0.0, -0.245, 1.66), ACCENT, 0.0))
    o.append(box("panel", (0.20, 0.02, 0.26), (0.28, -0.245, 0.90), DARK, 0.0))
    for k in range(3):
        o.append(box("btn_%d" % k, (0.04, 0.01, 0.03), (0.28, -0.257, 0.82 + k * 0.07), ACCENT, 0.0))
    o.append(box("interlock", (0.10, 0.06, 0.10), (-0.34, 0.0, 0.90), STEEL, 0.0))
    return o, (0.9, 0.5), 1.78, "medium", ("CleanPanel", "GlassPanel")


def gen_toilet_stall():
    """隔间 1.0×1.4×2.05：三面隔板 + 门 + 门锁 + 立柱（生成器点名「隔间」）。"""
    o = []
    o.append(box("back", (0.98, 0.03, 2.00), (0.0, 0.68, 1.05), BODY, 0.0, 0.0, 1))
    for x in (-0.48, 0.48):
        o.append(box("side", (0.03, 1.36, 2.00), (x, 0.0, 1.05), BODY, 0.0, 0.0, 1))
    o.append(box("door", (0.70, 0.03, 1.92), (-0.08, -0.66, 1.04), BODY, 0.0))
    for x in (-0.44, 0.44):
        o.append(box("pilaster", (0.05, 0.05, 2.06), (x, -0.66, 1.03), DARK, 0.0, 0.0, 1))
    o.append(box("top_rail", (0.92, 0.04, 0.05), (0.0, -0.66, 2.02), DARK, 0.0))
    o.append(box("latch", (0.06, 0.03, 0.10), (0.24, -0.68, 1.00), STEEL, 0.0))
    for z in (0.40, 1.72):
        o.append(box("hinge", (0.04, 0.03, 0.08), (-0.42, -0.68, z), STEEL, 0.0))
    o.append(box("foot", (0.92, 1.30, 0.04), (0.0, 0.02, 0.02), DARK, 0.0))
    return o, (1.0, 1.4), 2.05, "medium", ("PaintedWall", "BrushedMetal")


def gen_storage_rack():
    """货架 1.4×0.6×2.0：四层横板 + 立柱 + 斜撑 + 纸箱（生成器点名「货架」）。"""
    o = []
    for x in (-0.66, 0.66):
        for y in (-0.26, 0.26):
            o.append(box("post", (0.06, 0.06, 1.96), (x, y, 0.98), STEEL, 0.0, 0.0, 1))
    for k in range(4):
        o.append(box("shelf_%d" % k, (1.32, 0.54, 0.04), (0.0, 0.0, 0.24 + k * 0.50), BODY, 0.0, 0.0, 1))
    for y in (-0.26, 0.26):
        o.append(box("rail", (1.28, 0.04, 0.04), (0.0, y, 1.94), STEEL, 0.0))
    for k in range(2):
        o.append(box("brace", (0.90, 0.04, 0.04), (0.0, -0.28, 0.70 + k * 0.60), DARK, 0.0))
    for k in range(3):
        o.append(box("carton_%d" % k, (0.36, 0.40, 0.26), (-0.44 + k * 0.44, 0.0, 0.39),
                     WHITE if k % 2 else BODY, 0.0))
    for k in range(2):
        o.append(box("carton_b_%d" % k, (0.36, 0.40, 0.24), (-0.22 + k * 0.44, 0.0, 0.90), BODY, 0.0))
    return o, (1.4, 0.6), 2.0, "large", ("IndustrialPaint", "WoodPanel")


def gen_riser_rack():
    """管井架 0.6×0.6×1.96：立管束 + 三道卡箍 + 底盘 + 顶板 + 手轮（生成器点名「管井架」）。"""
    o = []
    o.append(box("base", (0.56, 0.56, 0.06), (0.0, 0.0, 0.03), DARK, 0.0))
    for x in (-0.18, 0.18):
        for y in (-0.18, 0.18):
            o.append(cyl("pipe", 0.06, 1.86, (x, y, 0.99), BODY, 10, 0.0))
    o.append(cyl("pipe_c", 0.09, 1.86, (0.0, 0.0, 0.99), ACCENT, 10, 0.0))
    for k in range(3):
        o.append(box("clamp_%d" % k, (0.50, 0.50, 0.05), (0.0, 0.0, 0.42 + k * 0.56), STEEL, 0.0))
    o.append(box("top", (0.56, 0.56, 0.05), (0.0, 0.0, 1.94), STEEL, 0.0))
    o.append(rot(cyl("wheel", 0.11, 0.03, (0.26, 0.0, 1.20), ACCENT, 10, 0.0), 1, 90))
    o.append(box("flange", (0.42, 0.05, 0.05), (0.0, 0.0, 1.62), STEEL, 0.0))
    return o, (0.6, 0.6), 1.96, "medium", ("ColdMetal", "BrushedMetal")


BUILDERS = {
    "cabinet_group": gen_cabinet_group,
    "desk": gen_desk,
    "sofa": gen_sofa,
    "crate_stack": gen_crate_stack,
    "trash_bin": gen_trash_bin,
    "water_cooler": gen_water_cooler,
    "server_rack": gen_server_rack,
    "cold_shelf": gen_cold_shelf,
    "workbench": gen_workbench,
    "control_console": gen_control_console,
    "power_cabinet": gen_power_cabinet,
    "tank": gen_tank,
    "archive_shelf": gen_archive_shelf,
    "pallet": gen_pallet,
    "bed": gen_bed,
    "locker": gen_locker,
    "med_cabinet": gen_med_cabinet,
    "partition": gen_partition,
    "pipe_rack": gen_pipe_rack,
    "meeting_table": gen_meeting_table,
    "printer": gen_printer,
    # R2 追加（2026-11-27）
    "office_chair": gen_office_chair,
    "folding_chair": gen_folding_chair,
    "cold_fan": gen_cold_fan,
    "pump": gen_pump,
    "screen_wall": gen_screen_wall,
    "vending_machine": gen_vending_machine,
    "water_cooler_v2": gen_water_cooler_v2,
    "shelf_unit": gen_shelf_unit,
    "pallet_stack": gen_pallet_stack,
    "barrel": gen_barrel,
    "cable_spool": gen_cable_spool,
    "sign_stand": gen_sign_stand,
    "plant": gen_plant,
    "fire_cabinet": gen_fire_cabinet,
    "extinguisher": gen_extinguisher,
    "hospital_bed": gen_hospital_bed,
    "medical_cart": gen_medical_cart,
    "tool_cart": gen_tool_cart,
    "workbench_v2": gen_workbench_v2,
    "glass_partition": gen_glass_partition,
    # R4 追加（2026-11-27）：设施道具族 A–F + 生成器点名的配套件
    #   A 办公/行政
    "conference_chair": gen_conference_chair,
    "file_cabinet": gen_file_cabinet,
    "printer_unit": gen_printer_unit,
    "coffee_table": gen_coffee_table,
    "whiteboard": gen_whiteboard,
    #   B 机房/中控
    "precision_ac": gen_precision_ac,
    "cable_tray": gen_cable_tray,
    "main_console": gen_main_console,
    "big_screen_wall": gen_big_screen_wall,
    "floor_vent": gen_floor_vent,
    #   C 冷链/仓储
    "chiller_unit": gen_chiller_unit,
    "insulated_box": gen_insulated_box,
    "wooden_crate": gen_wooden_crate,
    "plastic_crate": gen_plastic_crate,
    "compact_shelf": gen_compact_shelf,
    #   D 制造/设备
    "machine_tool": gen_machine_tool,
    "material_rack": gen_material_rack,
    "valve_group": gen_valve_group,
    "pipe_segment": gen_pipe_segment,
    "transformer": gen_transformer,
    "hv_cabinet": gen_hv_cabinet,
    "forklift": gen_forklift,
    "container": gen_container,
    #   E 医疗/洁净
    "isolation_pod": gen_isolation_pod,
    "washbasin": gen_washbasin,
    "iv_stand": gen_iv_stand,
    "air_shower": gen_air_shower,
    "clean_bench": gen_clean_bench,
    "changing_locker": gen_changing_locker,
    #   F 生活舱
    "bunk_bed": gen_bunk_bed,
    "mini_fridge": gen_mini_fridge,
    "microwave": gen_microwave,
    "dining_table": gen_dining_table,
    #   G 生成器点名但缺独立模型的配套件
    "row_header_cabinet": gen_row_header_cabinet,
    "patch_panel_rack": gen_patch_panel_rack,
    "tool_cabinet": gen_tool_cabinet,
    "isolation_bed": gen_isolation_bed,
    "pass_box": gen_pass_box,
    "toilet_stall": gen_toilet_stall,
    "storage_rack": gen_storage_rack,
    "riser_rack": gen_riser_rack,
}

# 导出名 -> PropCatalog 里的中文名（生成器 PropSpec.Name 必须与之一致）
DISPLAY_NAME = {
    "cabinet_group": "双柜组",
    "desk": "办公桌",
    "sofa": "沙发",
    "crate_stack": "纸箱堆",
    "trash_bin": "垃圾桶",
    "water_cooler": "饮水机",
    "server_rack": "服务器机柜",
    "cold_shelf": "冷库货架",
    "workbench": "工作台",
    "control_console": "控制台",
    "power_cabinet": "配电柜",
    "tank": "储罐",
    "archive_shelf": "档案架",
    "pallet": "装卸托盘",
    "bed": "床铺",
    "locker": "储物柜",
    "med_cabinet": "医疗柜",
    "partition": "隔断屏",
    "pipe_rack": "管道支架",
    "meeting_table": "会议桌",
    "printer": "打印台",
    # R2 追加（2026-11-27）
    "office_chair": "办公椅",
    "folding_chair": "折叠椅",
    "cold_fan": "冷风机",
    "pump": "泵组",
    "screen_wall": "屏幕墙",
    "vending_machine": "售货机",
    "water_cooler_v2": "直饮水机",
    "shelf_unit": "仓储货架",
    "pallet_stack": "托盘堆",
    "barrel": "油桶",
    "cable_spool": "电缆盘",
    "sign_stand": "立式指示牌",
    "plant": "绿植",
    "fire_cabinet": "消防柜",
    "extinguisher": "灭火器",
    "hospital_bed": "病床",
    "medical_cart": "医疗推车",
    "tool_cart": "工具车",
    "workbench_v2": "加固工作台",
    "glass_partition": "玻璃隔断",
    # R4 追加（2026-11-27）
    #   A 办公/行政
    "conference_chair": "会议椅",
    "file_cabinet": "文件柜",
    "printer_unit": "打印机",
    "coffee_table": "茶几",
    "whiteboard": "白板",
    #   B 机房/中控
    "precision_ac": "精密空调",
    "cable_tray": "桥架段",
    "main_console": "主控台",
    "big_screen_wall": "大屏墙",
    "floor_vent": "地板送风口",
    #   C 冷链/仓储
    "chiller_unit": "制冷机组",
    "insulated_box": "保温箱",
    "wooden_crate": "木箱",
    "plastic_crate": "塑料周转箱",
    "compact_shelf": "密集档案架",
    #   D 制造/设备
    "machine_tool": "机床",
    "material_rack": "料架",
    "valve_group": "阀门组",
    "pipe_segment": "管道组",
    "transformer": "变压器",
    "hv_cabinet": "高压柜",
    "forklift": "叉车",
    "container": "集装箱",
    #   E 医疗/洁净
    "isolation_pod": "隔离舱",
    "washbasin": "洗手台",
    "iv_stand": "输液架",
    "air_shower": "风淋室",
    "clean_bench": "洁净工作台",
    "changing_locker": "更衣柜",
    #   F 生活舱
    "bunk_bed": "双层床",
    "mini_fridge": "小冰箱",
    "microwave": "微波炉",
    "dining_table": "餐桌",
    #   G 生成器点名但缺独立模型的配套件
    "row_header_cabinet": "列头柜",
    "patch_panel_rack": "配线架",
    "tool_cabinet": "工具柜",
    "isolation_bed": "隔离床",
    "pass_box": "传递柜",
    "toilet_stall": "隔间",
    "storage_rack": "货架",
    "riser_rack": "管井架",
}


# 面数预算（04-3d-assets.md）：large ≤1500 / medium ≤900 / small ≤300
TRI_BUDGET = {"large": 1500, "medium": 900, "small": 300}
# 历史道具已超标（任务说明允许保留；重新生成它们时只告警，不阻塞新道具）
LEGACY_OVER_BUDGET = {"archive_shelf", "cabinet_group", "sofa", "trash_bin", "water_cooler"}


def build_one(key, out_dir):
    clear_scene()
    ensure_materials()
    objs, footprint, height, size_class, slots = BUILDERS[key]()
    ob = join(objs, "P_" + key)
    ob = finish(ob, "P_" + key)

    tris = sum(len(p.vertices) - 2 for p in ob.data.polygons)
    budget = TRI_BUDGET.get(size_class, 900)
    status = "OK" if tris <= budget else "OVER>%d" % budget
    # FBX 导出：只选当前物体，-Z 朝前 / Y 朝上（Unity 侧即 +Z 朝前、Y 朝上）
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    fbx = os.path.join(out_dir, "P_%s.fbx" % key)
    bpy.ops.export_scene.fbx(
        filepath=fbx,
        use_selection=True,
        object_types={"MESH"},
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=False,
        mesh_smooth_type="FACE",
        use_mesh_modifiers=True,
        path_mode="AUTO",
    )

    meta = {
        "id": key,
        "name": DISPLAY_NAME[key],
        "width": round(max(ob.dimensions.x, 0.01), 3),
        "depth": round(max(ob.dimensions.y, 0.01), 3),
        "height": round(max(ob.dimensions.z, 0.01), 3),
        "footprint": [footprint[0], footprint[1]],
        "size_class": size_class,
        "triangles": tris,
        "material_slots": list(slots),
        # 04-3d-assets.md §2 轴约定：Blender 里正面朝 -Y，以 axis_forward=-Z 导出，
        # 到 Unity 即正面朝 +Z。PropSpawner/PropPipeline 只用 x/z 尺寸，不读 facing，
        # 这里写进 sidecar 是给 Unity 端按墙朝向再旋转 0°/90° 的说明（不参与解析）。
        "facing": "+Z",
        "facing_note": "front faces +Z in Unity (modelled -Y in Blender); rotate by wall normal",
    }
    with open(os.path.join(out_dir, "P_%s.json" % key), "w", encoding="utf-8") as f:
        json.dump(meta, f, ensure_ascii=False, indent=2)
    print("[PROP] %-18s tris=%-5d %-9s %.2f x %.2f x %.2f m -> %s"
          % (key, tris, status, meta["width"], meta["depth"], meta["height"], fbx))
    return meta


def merge_manifest(out_dir, metas):
    """保留本批未重导出的道具条目：--only 只更新指定 id，清单仍包含全部道具。"""
    manifest_path = os.path.join(out_dir, "prop_manifest.json")
    merged = {}
    if os.path.exists(manifest_path):
        try:
            with open(manifest_path, "r", encoding="utf-8") as f:
                for m in json.load(f).get("props", []):
                    merged[m["id"]] = m
        except Exception as exc:  # 旧清单损坏时不要拖死整批生成
            print("[PROP] WARN 无法读取旧 manifest（将重写）: %s" % exc)
    for m in metas:
        merged[m["id"]] = m
    ordered = [merged[k] for k in BUILDERS if k in merged]
    ordered += [v for k, v in merged.items() if k not in BUILDERS]
    with open(manifest_path, "w", encoding="utf-8") as f:
        json.dump({"props": ordered}, f, ensure_ascii=False, indent=2)
    print("[PROP] manifest -> %s (%d props)" % (manifest_path, len(ordered)))


# ---------------------------------------------------------------------------
# 联系表渲染（可选 --render）：Blender 里直接摆位 + 三点光 + 灰底出 PNG
# ---------------------------------------------------------------------------

SHOWCASE_ORDER = [
    # 前排小件
    "office_chair", "folding_chair", "barrel", "sign_stand", "extinguisher",
    # 第二排
    "water_cooler_v2", "fire_cabinet", "medical_cart", "plant", "cold_fan",
    # 第三排
    "pump", "vending_machine", "cable_spool", "tool_cart", "shelf_unit",
    # 后排大件
    "screen_wall", "pallet_stack", "workbench_v2", "hospital_bed", "glass_partition",
]


def _look_at(ob, target):
    import mathutils
    direction = mathutils.Vector(target) - ob.location
    ob.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def render_showcase(out_png, keys):
    """把指定道具排成 5 列联系表：灰底、三点光、正交相机，1600×1000。"""
    scene = bpy.context.scene
    clear_scene()
    ensure_materials()

    cols = 5
    sx, sy = 2.90, 4.20
    rows = max(1, (len(keys) + cols - 1) // cols)
    label_mat = material("M_ShowcaseLabel", (0.92, 0.92, 0.90, 1.0))

    for i, key in enumerate(keys):
        objs = BUILDERS[key]()[0]
        ob = finish(join(objs, "SHOW_" + key), "SHOW_" + key)
        row, col = divmod(i, cols)
        x = (col - (cols - 1) * 0.5) * sx
        y = row * sy
        ob.location = (x, y, 0.0)

        bpy.ops.object.text_add(location=(x, y - 1.18, 0.012))
        t = bpy.context.active_object
        t.data.body = key
        t.data.size = 0.20
        t.data.align_x = "CENTER"
        t.data.align_y = "CENTER"
        t.data.materials.append(label_mat)

    # 地面：中性灰，接住阴影
    bpy.ops.mesh.primitive_plane_add(size=120.0, location=(0.0, 5.0, -0.003))
    floor = bpy.context.active_object
    floor.name = "ShowcaseFloor"
    floor_mat = material("M_ShowcaseFloor", (0.24, 0.25, 0.26, 1.0))
    floor_mat.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.9
    floor.data.materials.append(floor_mat)

    # 世界光
    world = bpy.data.worlds.get("ShowcaseWorld") or bpy.data.worlds.new("ShowcaseWorld")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    bg.inputs[0].default_value = (0.17, 0.18, 0.20, 1.0)
    bg.inputs[1].default_value = 0.8

    def add_light(name, kind, loc, energy, size=5.0, target=(0.0, 4.0, 0.6)):
        data = bpy.data.lights.new(name, kind)
        data.energy = energy
        if kind == "AREA":
            data.size = size
        ob = bpy.data.objects.new(name, data)
        scene.collection.objects.link(ob)
        ob.location = loc
        _look_at(ob, target)
        return ob

    add_light("KeyLight", "AREA", (-6.0, -7.0, 9.0), 1500.0, 8.0)
    add_light("FillLight", "AREA", (7.0, -4.0, 6.0), 450.0, 9.0)
    add_light("RimLight", "AREA", (2.0, 11.0, 8.0), 800.0, 7.0)

    sun_data = bpy.data.lights.new("Sun", "SUN")
    sun_data.energy = 1.6
    sun_data.angle = math.radians(12.0)
    sun = bpy.data.objects.new("Sun", sun_data)
    scene.collection.objects.link(sun)
    sun.rotation_euler = (math.radians(52.0), 0.0, math.radians(-38.0))

    # 正交相机：约 36° 俯角（比纯俯视更能看出侧立面轮廓），4 排加标签全在画幅内
    cam_data = bpy.data.cameras.new("ShowcaseCam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = 18.5
    cam = bpy.data.objects.new("ShowcaseCam", cam_data)
    scene.collection.objects.link(cam)
    cam.location = (0.0, -16.0, 18.0)
    _look_at(cam, (0.0, 5.4, 0.95))
    scene.camera = cam

    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 24
    scene.cycles.use_denoising = True
    scene.cycles.max_bounces = 4
    scene.view_settings.exposure = -0.35
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 1000
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = out_png
    os.makedirs(os.path.dirname(out_png), exist_ok=True)
    bpy.ops.render.render(write_still=True)
    print("[PROP] showcase -> %s (%d props)" % (out_png, len(keys)))


def main():
    global mathutils
    import mathutils  # noqa: F401  (finish / 渲染相机里用到)

    out_dir, only, render = parse_args()
    os.makedirs(out_dir, exist_ok=True)

    if render:
        keys = [k for k in SHOWCASE_ORDER if (only is None or k in only)]
        if not keys:
            keys = list(SHOWCASE_ORDER)
        render_showcase(render, keys)
        return

    keys = [k for k in BUILDERS.keys() if (only is None or k in only)]
    metas = []
    for k in keys:
        metas.append(build_one(k, out_dir))

    merge_manifest(out_dir, metas)

    bad = [m for m in metas
           if m["id"] not in LEGACY_OVER_BUDGET
           and m["triangles"] > TRI_BUDGET[m["size_class"]]]
    if bad:
        print("[PROP] BUDGET FAIL: %s" % ", ".join(
            "%s(%d)" % (m["id"], m["triangles"]) for m in bad))
        sys.exit(3)
    print("[PROP] budget check passed for %d prop(s)" % len(metas))


if __name__ == "__main__":
    main()
