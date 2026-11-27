# PBR 材质库（31 套）

> 本文合并了原来的「08 第二批 12 套」与「11 第三批 12 套」，加上首批 7 套，是材质库的**唯一清单**。
> 命名、目录、打包约定三批完全一致；新增一套就加一行，不要再开新文档。

## 1. 约定

```
game/Assets/_Project/Art/Textures/<Set>/T_<Set>_{Albedo,Normal,Mask}.png   # 每套 3 张 1024²
```

- Albedo：sRGB 反照率。
- Normal：切线空间法线（Sobel 自灰度高程）。
- Mask：**R=metallic，G=AO，B=255，A=smoothness**。
- `.meta` 由 Unity 首次导入生成；材质统一由 `MaterialArtLibrary` 构建，不要手搓 `.mat`。
- 「每米重复」= `MaterialArtLibrary.TilingFor()` 里该写的值（1 uv = 1 米）；括号内是一个 uv 循环的真实尺寸。

## 2. 31 套一览

### 首批 7 套（2026-11-24，灰盒期）

| Set | 用途 | 每米重复 |
|---|---|---|
| CarpetTile | 普通办公地毯 | 2.0 |
| CeilingTile | 矿棉吊顶板 | 1.667 |
| PaintedWall | 普通涂料墙 | 0.667 |
| ConcreteFloor | 混凝土楼板/楼梯间 | 0.667 |
| WoodPanel | 木饰面（柜体/家具） | 1.0 |
| DarkMetal | 深色金属（核心筒/设备） | 1.0 |
| BrushedMetal | 拉丝金属（小件道具） | 1.667 |

### 第二批 12 套（2026-11-26，设施层）

| Set | 用途 | 每米重复 | 循环尺寸 | smoothness | metallic | 备注 |
|---|---|---|---|---|---|---|
| TileFloor | 卫生间/洁净间/更衣室地面（30cm 陶瓷砖） | 0.833 | 1.2m = 4×4 块 30cm 砖 | 0.38 | 0 | 砖缝在边界上，必须 1.2m 循环 |
| VenueCarpet | 高管区/会议区深蓝灰地毯 | 1.0 | 1.0m | 0.08 | 0 | 比 CarpetTile 更深更密，别同房混用 |
| ConcreteWall | 核心筒/楼梯间/设备区清水混凝土 | 0.5 | 2.0m | 0.22 | 0 | 有气孔、无模板拼缝 |
| GlassPanel | 隔断/窗，不透明磨砂玻璃（偏青） | 0.667 | 1.5m | 0.70 | 0 | 不放反光斑，反光交给 URP |
| MetalDoor | 门/电梯门浅灰粉末喷涂钢板 | 1.0 | 1.0m | 0.42 | 1.0 | 近乎纯色，靠漆粒与法线出细节 |
| RubberStair | 楼梯黑色防滑橡胶踏步 | 1.0 | 1.0m，凸筋 12.5mm | 0.08 | 0 | 凸筋必须横向，UV 转错会变纵向 |
| ServerPanel | 机房机柜/设备间深灰穿孔板 | 1.667 | 0.6m，孔距 10mm | 0.55 | 1.0 | 孔阵是解析叠加的规则错排 |
| ColdMetal | 冷库保温板（冷蓝白金属板） | 1.0 | 1.0m，1 条板缝 | 0.55 | 1.0 | 墙面大时可拉到 0.833 |
| EpoxyFloor | 车间/机房中灰环氧自流平 | 0.5 | 2.0m | 0.38 | 0 | 主要「工业地面」底材 |
| CleanPanel | 洁净间白色高光墙板 | 0.833 | 1.2m，1 条板缝 | 0.62 | 0 | 极干净，别和 TileFloor 混贴 |
| DiamondPlate | 装卸区/管廊花纹钢板 | 1.0 | 1.0m，40mm 菱形 | 0.60 | 1.0 | 适合近景地面/踏板 |
| IndustrialPaint | 管廊/设备墙灰绿涂料钢板 | 0.5 | 2.0m | 0.40 | 1.0 | 做旧不过火，无锈斑 |

### 第三批 12 套（2026-11-27，设施层）

| Set | 用途 | 每米重复 | 循环尺寸 | smoothness | metallic | 备注 |
|---|---|---|---|---|---|---|
| MetalGrate | 架空地板/设备平台/夹层钢格栅 | 1.667 | 0.6m = 20 条 30mm 节距扁钢 + 100mm 横杆 | 0.42 | 0.85 | 扁钢中心压在边界，铺出来永远连续 |
| RubberFloor | 机房/设备层/楼梯间橡胶地板 | 1.0 | 1.0m | 0.09 | 0 | 暗灰颗粒，几乎不反光 |
| FabricPanel | 办公隔断/会议布面吸音板 | 2.0 | 0.5m | 0.05 | 0 | 细密平纹，别和地毯混贴 |
| PlasterWall | 普通办公抹灰墙 | 0.667 | 1.5m | 0.24 | 0 | 大墙无重复块 |
| WoodFloor | 行政/接待/会议条木地板 | 0.833 | 1.2m = 8 条 150mm 板，板长 600、错缝 300 | 0.36 | 0 | 木纹先转 90° 顺着板长 |
| FrostedGlass | 隔断磨砂玻璃（浅青灰雾面） | 0.667 | 1.5m | 0.72 | 0 | 只有低频云雾 |
| CeramicWall | 卫生间/洁净区白瓷砖墙 | 0.833 | 1.2m = 4×2 块 300×600 砖（3mm 缝） | 0.55 | 0 | 砖缝中线压边界 |
| CeilingGrid | 办公矿棉板吊顶 + T 型龙骨 | 0.833 | 1.2m = 2×2 块 600 板 + 24mm 龙骨 | 0.22 | 0 | 2×2 平铺是完整 T 格 |
| RustSteel | 老旧管廊/仓库锈蚀钢板 | 1.0 | 1.0m | 0.32 | 0.65（锈处衰减到 ~0.10） | 锈斑 + 42 条解析划痕 |
| HazardPaint | 装卸区/车间警示漆地面 | 1.0 | 1.0m = 6 组 45° 黄黑斜条 | 0.30 | 0 | 周期整除 1024，斜接缝完美 |
| VinylFloor | 医院/洁净区 PVC 卷材 | 0.5 | 2.0m | 0.31 | 0 | 浅色均质 + 1px 杂点 |
| ConcreteCeiling | 停车/管廊清水混凝土顶板 | 0.833 | 1.2m = 2 道 600 拼缝 + 20mm 螺栓孔 | 0.20 | 0 | 拼缝很浅，远看整块清水面 |

## 3. TilingFor（31 套全覆盖，已接线）

```csharp
case "CarpetTile": return 2.0f;  case "CeilingTile": return 1.667f;  case "PaintedWall": return 0.667f;
case "ConcreteFloor": return 0.667f;  case "WoodPanel": return 1.0f;  case "DarkMetal": return 1.0f;
case "BrushedMetal": return 1.667f;
case "TileFloor": return 0.833f;  case "VenueCarpet": return 1.0f;  case "ConcreteWall": return 0.5f;
case "GlassPanel": return 0.667f;  case "MetalDoor": return 1.0f;  case "RubberStair": return 1.0f;
case "ServerPanel": return 1.667f;  case "ColdMetal": return 1.0f;  case "EpoxyFloor": return 0.5f;
case "CleanPanel": return 0.833f;  case "DiamondPlate": return 1.0f;  case "IndustrialPaint": return 0.5f;
case "MetalGrate": return 1.667f;  case "RubberFloor": return 1.0f;  case "FabricPanel": return 2.0f;
case "PlasterWall": return 0.667f;  case "WoodFloor": return 0.833f;  case "FrostedGlass": return 0.667f;
case "CeramicWall": return 0.833f;  case "CeilingGrid": return 0.833f;  case "RustSteel": return 1.0f;
case "HazardPaint": return 1.0f;  case "VinylFloor": return 0.5f;  case "ConcreteCeiling": return 0.833f;
```

## 4. 槽位现状（还没做 per-room）

`MaterialArtLibrary.Slots` 只有 **10 个全局槽**：Wall / Floor / Ceiling / Column / Core / Stair /
PropLarge / PropMedium / PropSmall / SightBreak。现在接的是：

| 槽位 | 当前材质集 |
|---|---|
| Wall | PaintedWall |
| Floor | CarpetTile |
| Ceiling | CeilingTile |
| Column | ConcreteWall（R2 时换） |
| Core | DarkMetal |
| Stair | RubberStair（R2 时换） |
| PropLarge | WoodPanel |
| PropMedium | DarkMetal |
| PropSmall | BrushedMetal |
| SightBreak | PaintedWall |

**31 套里还有 21 套没有槽位**。要按房间/设施换材质，得给 `FloorMaterials`/`MaterialArtLibrary` 加 per-room 覆盖入口（`Room.FloorMaterial/WallMaterial` 数据已经在 `FacilityGrammar` 里算好，只是没落到几何上）。设施与材质的对应建议见 [09-facilities.md](09-facilities.md)。

## 5. 生成方式与已知取舍

- **纯 AI（18 套）**：SDXL base 1.0 + SeamlessTile + CircularVAE 出 1024² albedo，后处理做接缝检测 / offset-blend / Sobel 法线 / Mask 打包。
- **AI 底色 + 程序化图案（13 套）**：TileFloor、RubberStair、ServerPanel、ColdMetal、CleanPanel、DiamondPlate、MetalGrate、WoodFloor、CeramicWall、CeilingGrid、HazardPaint、ConcreteCeiling（以及 RustSteel 的划痕）。原因：SDXL 出不了「严格等距且四边可无缝」的规则图案，改为 AI 只给颜色颗粒，规则几何用解析函数叠加、周期整除 1024，接缝数学上完美。
- **平坦涂层低通**：GlassPanel / ColdMetal / CleanPanel / FrostedGlass 这类纯色板，模型会脑补石材裂纹，平铺成大斑块，所以压平低频、只留细颗粒。

已知不足：

- ConcreteWall 没有模板拼缝、MetalDoor 没有门板凹槽、IndustrialPaint 没有铆钉行——这些都该由建模几何表达，贴图带上会平铺错位。
- ServerPanel 的接缝指标 `seam≈0.08` 偏高是口径问题（孔阵边界正好落在相邻两排孔的过渡线上），2×2 预览连续，不是真缝。
- 旧 7 套的 smoothness/metallic 沿用首批 `TUNE`，本轮没有重出。

## 6. 复现

```powershell
# 生成（第二个参数是 seed offset，换一张就 +1）
F:\ComfyUI\venv\Scripts\python.exe -u F:\ComfyUI\efw_gen_materials.py <SetName> 0
# 后处理（自动取该 set 最新源图，写回 Unity 工程）
F:\ComfyUI\venv\Scripts\python.exe -u F:\ComfyUI\efw_post_materials.py <SetName>
# 2x2 平铺预览（肉眼验缝）
F:\ComfyUI\venv\Scripts\python.exe -u F:\ComfyUI\efw_check_tiling.py
```

每套参数集中在 `efw_post_materials.py` 的 `TUNE`（smoothness, metallic, normal_strength, blur_radius），
plus `PATTERN` / `PATTERN_FNS`（解析图案）、`SMOOTH_BASE` / `BASE_PICK` / `FINE_GRAIN`（平坦涂层）。
改参数只重跑后处理即可。ComfyUI 起法：`F:\ComfyUI` 下 `main.py --listen 127.0.0.1 --port 8188 --disable-auto-launch`。
