# 美术资产管线（Blender 参数化 / 图生 3D / 导入 Unity）

> 本文合并了原来的「10 Blender 道具」与「12 图生 3D」，是 3D 资产管线的唯一入口。
> 贴图/材质在 [08-materials.md](08-materials.md)，命名与建模规范在 [04-3d-assets.md](04-3d-assets.md)，道具清单状态在 [07-handoff.md](07-handoff.md) §12。

## 1. 分工

| 来源 | 适合 | 不适合 |
|---|---|---|
| **Blender 参数化**（`tools/blender/gen_props.py`） | 规整家具/设备：柜、桌、椅、货架、机柜、储罐、病床、泵、隔断 | 有机形状、磨损细节 |
| **图生 3D**（TripoSR，`tools/img2mesh/`） | 杂物、破家具、管线簇、植物、单件摆设 | 需要精确尺寸/硬边的规整设备；细长结构会丢 |

两条管线都输出 **FBX + JSON（尺寸/tris/材质槽）**，交给同一个导入器 `Editor/Tools/PropPipeline.cs` 生成 prefab 并写 `PropCatalog`。

## 2. Blender 参数化道具（41 件）

```powershell
# 全量（会重导 41 件）
& "F:\Tools\blender\blender-4.2.9-windows-x64\blender.exe" -b -P "tools\blender\gen_props.py" -- --out "game\Assets\_Project\Art\Props"
# 单批
& "...\blender.exe" -b -P "tools\blender\gen_props.py" -- --out "...\Art\Props" --only office_chair,plant
# 联系表（Cycles CPU）
& "...\blender.exe" -b -P "tools\blender\gen_props.py" -- --out "...\Art\Props" --render "docs\3d-rebuild\screenshots\props-showcase-2.png"
```

规格：1 unit = 1m、pivot 底面中心、正面 +Z、**立方体投影 UV（1 UV = 1 米）**、材质按道具语义选套（见下）。
面数预算（04 规范）：大件 ≤1500 / 中件 ≤900 / 小件 ≤300；脚本逐件打印 `[PROP] <id> tris=<n> OK`，新道具超预算会 `BUDGET FAIL` 并以 exit 3 结束。

### UV 与材质：为什么之前像"瓦楞纸"

- **老问题**：Blender 基元自带 UV 是每个面 0–1，1.8m 柜子的木纹会被放大成一整块，法线贴图在平面上糊成横竖条纹（早期 `props-showcase` 就是这观感）。
- **现在**：`finish()` 里在合并顶点后做一次 `bpy.ops.uv.cube_project(cube_size=1.0)`，UV 直接以米为单位；Unity 侧扁平材质的 tiling 固定 1，于是"1 米 1 次"和场景墙面/地面的 `WorldUv` 完全一致，道具贴图不会跟房间对不上。
- **材质按道具选套**（`PropPipeline.SetByProp`，共 41 条）：木家具 → `WoodPanel`；机架/机柜/控制台 → `ServerPanel`/`DarkMetal`；冷库货架、储罐 → `ColdMetal`；泵、管架、油桶 → `RustSteel`；布艺座椅、沙发、隔断、床 → `FabricPanel`；玻璃隔断 → `FrostedGlass`；消防柜/灭火器 → `HazardPaint`；工具车 → `DiamondPlate`；纯净设备 → `CleanPanel`；绿植盆 → `ConcreteFloor`。
- 扁平材质只留 `_BaseMap` + `_BaseColor`（smoothness 按套给：布 0.06、木 0.22、玻璃 0.62、金属 0.4），去掉法线/金属贴图 —— 道具的形体靠几何、倒角与投影，不靠法线噪声。
- 历史材质 `M_Prop_PropLarge/PropMedium/PropSmall` 已删，换成 12 个按材质套命名的 `M_Prop_<Set>.mat`。

**第二批 20 件**（全部达标，`prop_manifest.json` 共 41 项）：

| id | 中文名 | W×D×H (m) | tris | 用途 |
|---|---|---|---|---|
| office_chair | 办公椅 | 0.59×0.56×1.02 | 292 | 办公桌/会议桌旁 |
| folding_chair | 折叠椅 | 0.44×0.43×0.82 | 160 | 替换 74 件灰盒折叠椅 |
| cold_fan | 冷风机 | 0.96×0.49×0.92 | 224 | 替换 49 件灰盒冷风机 |
| pump | 泵组 | 0.96×0.68×0.69 | 284 | 管道间/设备层 |
| screen_wall | 屏幕墙 | 1.54×0.26×2.11 | 224 | 控制室主墙 |
| vending_machine | 售货机 | 0.94×0.77×1.94 | 356 | 休息区/走廊端头 |
| water_cooler_v2 | 直饮水机 | 0.46×0.50×1.24 | 176 | 茶水间 |
| shelf_unit | 仓储货架 | 1.78×0.58×1.97 | 864 | 仓库/备件区 |
| pallet_stack | 托盘堆 | 1.22×1.04×0.67 | 216 | 装卸区/仓库 |
| barrel | 油桶 | 0.60×0.60×0.89 | 204 | 车间/油料间 |
| cable_spool | 电缆盘 | 1.14×0.92×1.08 | 332 | 弱电间/施工区 |
| sign_stand | 立式指示牌 | 0.50×0.40×1.33 | 148 | 牌面留白，等 signage 贴图 |
| plant | 绿植 | 0.61×0.51×1.31 | 336 | 大厅/前台/角落 |
| fire_cabinet | 消防柜 | 0.70×0.30×1.06 | 116 | 走廊墙边/楼梯口 |
| extinguisher | 灭火器 | 0.28×0.26×0.62 | 196 | 消防柜旁/配电间 |
| hospital_bed | 病床 | 0.98×2.02×1.52 | 396 | 医疗层病房 |
| medical_cart | 医疗推车 | 0.58×0.46×0.96 | 244 | 病房/护士站 |
| tool_cart | 工具车 | 0.92×0.62×1.02 | 332 | 车间/维修间 |
| workbench_v2 | 加固工作台 | 1.96×0.93×1.44 | 344 | 车间/装配工位 |
| glass_partition | 玻璃隔断 | 2.02×0.22×2.17 | 216 | 办公分隔/观察窗（需透明材质） |

首批 21 件（柜/桌/沙发/纸箱堆/垃圾桶/饮水机/机柜/冷库货架/工作台/控制台/配电柜/储罐/档案架/托盘/床/储物柜/医疗柜/隔断/管道支架/会议桌/打印机）里，
`archive_shelf` 3024、`cabinet_group` 1956、`sofa` 1004、`trash_bin` 356、`water_cooler` 324 超预算，列在 `LEGACY_OVER_BUDGET` 只告警。

### 踩过的坑

- **`finish()` 的 pivot bug**：旧写法直接减世界坐标包围盒、但没把活动物体 `location` 归零，mesh 底面落在 `-location.z`（`desk` zmin=-0.725、`bed` -0.22、`locker` -0.90）。`PropPipeline` 用 JSON 高度生成 `BoxCollider(center.y=height/2)`，pivot 偏了模型会陷进地板。已修为"先 `transform_apply` 再平移"，全量重跑后 41 件的 zmin 全为 0.000。
- `merge_manifest()`：`--only` 分批生成时必须读回旧清单再合并，否则会把其它道具条目冲掉。
- 新道具超预算要拦：`TRI_BUDGET` + `build_one` 自检 + `main()` exit 3。

## 3. 导入 Unity（`Editor/Tools/PropPipeline.cs`）

菜单 `Escape From Work/Props/Import Blender Props`（或 `-executeMethod EscapeFromWork.EditorTools.PropPipeline.ImportAll`）：

- ModelImporter：`globalScale 1`、`useFileScale false`、`materialImportMode None`、无动画、无自动碰撞、压缩关、weld 开。
- prefab 落 `Assets/_Project/Prefabs/Props/P_<id>.prefab`：单 **BoxCollider**（center.y = H/2，size = W×H×D），`BatchingStatic | OccludeeStatic`。
- 写 `Assets/_Project/Data/PropCatalog.asset`：先写真实道具，再写 `Aliases`（同名以真实道具为准，别留重复条目）。
- 材质：**扁平材质** `Assets/_Project/Materials/Props/M_Prop_<slot>.mat`（只留 `_BaseMap` + `_BaseColor`，smoothness 0.18、metallic 0、instancing 开）。原因：盒子 UV 每个面 0–1，直接挂 PBR 槽的法线/金属贴图会拉成"瓦楞纸"。
- 出总览：菜单 `Escape From Work/Props/Capture Prop Showcase` → `docs/3d-rebuild/screenshots/props-showcase*.png`。

别名现状（`Aliases`）：`工具柜→tool_cart`、`隔离床→hospital_bed`、`货架→shelf_unit`，已删掉 `屏幕墙→control_console`、`泵组→tank`（改由真实道具命中）。`冷风机`/`折叠椅` 现在直接命中新模型。

## 4. 图生 3D（TripoSR，本机可跑）

**结论：局部接入**——杂物/破家具/管线簇/植物用它，规整家具设备仍用 Blender 参数化。

| 项 | 实测 |
|---|---|
| 方案对比 | TripoSR ✅；InstantMesh ❌（Windows 需 cp310 预编译 Triton）；StableFast3D ⚠️（需 VS2022 编译 CUDA kernel）；Hunyuan3D-2 暂缓；TRELLIS ❌（≥16GB + 多编译依赖） |
| 权重 | 1.56GB（`hf-mirror.com` 8.5MB/s；`huggingface.co` 超时） |
| 显存 / 耗时 | 峰值 **2.32GB**；512² 单图 mc256 **6.37s**、mc512 14.0s |
| 后处理 | 83,728 → 1,500 面减面 0.05s；xatlas 1024 烘焙 0.37s；Blender 转 FBX 0.9s → **≈8s/件** |
| 质量 | 体块/比例准；细长结构会丢（椅子 4 条腿只出 3 条）；硬边磨圆；只有顶点色/烘焙贴图；朝向 **+X** |

```powershell
# 一条命令：生成 + 减面 + 烘焙 + 导出 FBX（GPU 约 6.4s）
F:\ComfyUI\venv\Scripts\python.exe tools\img2mesh\run_tsr.py --image <png> --out tools\img2mesh\out\<name> --mc 256
```

- 权重/上游源码/out/inputs 都在 `.gitignore` 里（共 1.6GB+），换机要重建：见 `tools/img2mesh/README.md`。
- 样例：`out/chair/`（官方示例图）、`out/plant/`（用仓库 `P_plant.fbx` 反渲成输入图再重建）。
- 朝向 +X，接入 `PropCatalog` 前要转成 +Z；目前只验证到样品，没有进 `PropCatalog`。

## 5. 已知缺口

1. **没有 LOD1**：41 件只有 LOD0，tris 余量足够但远距离切换还没做。
2. **玻璃仍不完全透明**：扁平材质只贴 albedo，`FrostedGlass` 给的是灰白磨砂面（看起来是磨砂玻璃，但没有真正透明/折射）。要真透明得单开一套 URP 透明材质并在 `BuildPrefab` 里按材质名保留 FBX 上的原始分槽。
3. **尺寸与 PropSpec 有出入**：折叠椅 0.82m（Spec 0.5）、泵组深 0.68m（Spec 0.6）、病床 1.52m 含输液架——贴墙摆放可能嵌墙，碰撞盒高度可在 prefab 上单独调。
4. **UV 是立方体投影**：`sign_stand` 的牌面还只是"1 米 1 次"的木纹/金属纹，没有单独排牌面 UV；贴 signage 图需要给它一块独立 UV 区。
5. **道具仍偏"扁平"**：没有法线贴图/粗糙度变化，观感靠几何与光照；远景可接受，近景需要贴图或顶点 AO。
