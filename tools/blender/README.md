# Blender 道具生成 / 校验工具

低模道具全部由脚本生成，不手工建模。产物直接写进 Unity 美术管线：

```
game/Assets/_Project/Art/Props/P_<id>.fbx     # 网格（单 mesh、单材质、无贴图）
game/Assets/_Project/Art/Props/P_<id>.json    # sidecar，与 prop_manifest.json 同结构
game/Assets/_Project/Art/Props/prop_manifest.json
```

Editor 侧由 `game/Assets/_Project/Scripts/Editor/Tools/PropPipeline.cs` 读取 sidecar
（width/depth/height/size_class/material_slots）生成 prefab：轴心在底面中心、
单个 BoxCollider、BatchingStatic + OccludeeStatic、按槽名套一个平色材质。

## 约定（详见 docs/3d-rebuild/04-3d-assets.md）

| 项 | 约定 |
| --- | --- |
| 单位 | 1 unit = 1 m |
| 轴心 | 底面中心（`finish()` 会把 bbox 平移到底面中心在原点） |
| 朝向 | Blender 里面朝 **-Y**；FBX 以 `axis_forward="-Z" / axis_up="Y"` 导出，到 Unity 即 **+Z**。sidecar 写 `"facing": "+Z"`，摆放时按墙法线再转 0°/90° |
| 面数 | small ≤300、medium ≤900、large ≤1500（09 的 §2.2 设施表用更紧的上限） |
| 材质 | 每件只用 1 个材质块（`material_slots[0]`），槽名取 `WoodPanel / CleanPanel / ColdMetal / MetalDoor / BrushedMetal / IndustrialPaint / DiamondPlate / ServerPanel / GlassPanel / PaintedWall / FabricPanel / DarkMetal` 等文档槽名 |
| 命名 | `P_<小写下划线>.fbx`，显示名走 `DISPLAY_NAME`（中文，与 FacilityGrammar.PropsOf 的点名对齐） |

## gen_props.py — 生成

```powershell
# 生成全部（会重写同名 FBX/JSON，但 merge_manifest 不会丢其它条目）
F:\Tools\blender\blender-4.2.9-windows-x64\blender.exe -b -P tools\blender\gen_props.py

# 只生成指定几件（安全：绝不碰其它道具的 FBX/JSON）
F:\Tools\blender\blender-4.2.9-windows-x64\blender.exe -b -P tools\blender\gen_props.py -- --only forklift,container

# 自检：超预算时退出码 3
```

每一批生成后脚本会打印逐件的面数、包围盒与预算校验结果；新增一批道具的做法是
在 `BUILDERS` 前追加 `gen_<id>()`，返回 `(objs, (w, d), height, size_class, (slot_a, slot_b))`。

## render_props.py — 抽查渲染

重新导入已导出的 FBX（不是重新生成），按同一 3/4 视角渲 512×512 到
`docs/3d-rebuild/screenshots/props/`，并打印每件的 mesh 数 / 面数 / 包围盒 /
`min_z`（应为 ±0.000，验证轴心贴地）/ `facing`。

```powershell
F:\Tools\blender\blender-4.2.9-windows-x64\blender.exe -b -P tools\blender\render_props.py -- --props forklift,container,main_console
# 可选：--out <目录> --fbx <道具目录> --size 512 --samples 32
```
