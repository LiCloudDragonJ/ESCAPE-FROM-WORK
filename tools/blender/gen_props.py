"""ESCAPE FROM WORK 道具模型生成器（Blender 4.2 headless）

用法（在仓库根执行）：
  & "F:\Tools\Blender\blender-4.2.9-windows-x64\blender.exe" -b -P tools/blender/gen_props.py -- ^
      --out "game/Assets/_Project/Art/Props" --only cabinet_group,server_rack

每个道具导出两个文件：
  P_<id>.fbx   —— pivot 在底面中心、正面朝 +Z、1 unit = 1 m，Unity 直接拖进场景
  P_<id>.json  —— 占地/高度/面数/材质槽，PropImporter 用它生成 prefab 与填 PropCatalog

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
    """纸箱堆 1.4×0.9×1.4：三层错位纸箱。"""
    o = []
    layout = [
        (0.62, 0.58, 0.44, -0.32, 0.10),
        (0.62, 0.58, 0.44, 0.32, -0.08),
        (0.60, 0.56, 0.42, -0.28, -0.10),
        (0.58, 0.54, 0.40, 0.30, 0.12),
        (0.56, 0.52, 0.38, 0.0, 0.0),
    ]
    z = 0.0
    for i, (w, d, h, x, y) in enumerate(layout):
        o.append(box("crate_%d" % i, (w, d, h), (x, y, z + h * 0.5), BODY if i % 2 == 0 else WHITE, 0.012))
        o.append(box("tape_%d" % i, (w * 0.5, 0.02, 0.012), (x, y - d * 0.5 - 0.005, z + h - 0.02), ACCENT, 0.0))
        z += h
    return o, (1.4, 0.9), 1.4, "large", ("WoodPanel", "IndustrialPaint")


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
