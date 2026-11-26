# PBR 材质库（第二批 12 套）

本批由本机 ComfyUI 管线（`F:\ComfyUI\efw_gen_materials.py` + `efw_post_materials.py`）生成，
沿用第一批 7 套的目录、命名与 1024²、Albedo/Normal/Mask 三件套约定，**未改动任何旧文件**。

## 1. 产出位置

```
game/Assets/_Project/Art/Textures/<Set>/T_<Set>_{Albedo,Normal,Mask}.png
```

- Albedo：sRGB 反照率。
- Normal：切线空间法线（Sobel 自灰度高程）。
- Mask：**R=metallic，G=AO，A=smoothness**（与第一批一致，B 不用）。
- `.meta` 交给 Unity 首次导入自动生成；材质仍由 `MaterialArtLibrary` 统一构建，不要手搓材质。

## 2. 12 套一览

「每米重复」= 推荐写进 `MaterialArtLibrary.TilingFor()` 的值（1 uv = 1 米，含义是贴图每米重复几次）。
一个 uv 循环对应的真实尺寸写在括号里。

| Set | 用途 | 每米重复 | 循环尺寸 | smoothness | metallic | 备注 |
|-----|------|---------|---------|-----------|---------|------|
| TileFloor | 卫生间/洁净间/更衣室地面（30cm 陶瓷砖） | 0.833 | 1.2m = 4×4 块 30cm 砖 | 0.38 | 0 | 砖缝在贴图边界上，1.2m 一循环正好 4 块砖，别改成非整数倍 |
| VenueCarpet | 高管区/会议区深蓝灰地毯 | 1.0 | 1.0m | 0.08 | 0 | 比 CarpetTile 更深更密，不要和它放同一间房 |
| ConcreteWall | 核心筒/楼梯间/设备区清水混凝土墙 | 0.5 | 2.0m | 0.22 | 0 | 有气孔、无模板拼缝（拼缝交给建模/UV，见第 5 节） |
| GlassPanel | 隔断/窗，不透明磨砂玻璃（偏青） | 0.667 | 1.5m | 0.70 | 0 | 纯平奶白青面，不放反光斑；反光交给 URP |
| MetalDoor | 门/电梯门的浅灰粉末喷涂钢板 | 1.0 | 1.0m | 0.42 | 1.0 | 近乎纯色，只靠极细漆面颗粒和法线出细节 |
| RubberStair | 楼梯黑色防滑橡胶踏步 | 1.0 | 1.0m，横向凸筋 12.5mm | 0.08 | 0 | 凸筋必须**横向**（垂直于行走方向），UV 旋转错了会变成纵向 |
| ServerPanel | 机房机柜/设备间深灰穿孔板 | 1.667 | 0.6m，孔距 10mm | 0.55 | 1.0 | 孔阵是程序化叠加的规则错排阵，近距离看是均匀网点 |
| ColdMetal | 冷库保温板（冷蓝白金属板） | 1.0 | 1.0m，每循环 1 条板缝 | 0.55 | 1.0 | 板缝很细，按 1m 一块板铺；墙面大时可拉到 0.833 |
| EpoxyFloor | 车间/机房中灰环氧自流平 | 0.5 | 2.0m | 0.38 | 0 | 半哑光、细微斑驳，是主要「工业地面」底材 |
| CleanPanel | 洁净间白色高光墙板 | 0.833 | 1.2m，每循环 1 条板缝 | 0.62 | 0 | 极干净，只有细板缝，别和 TileFloor 混贴 |
| DiamondPlate | 装卸区/管廊花纹钢板 | 1.0 | 1.0m，25×25 菱形块（40mm 间距） | 0.60 | 1.0 | 凸纹有明暗斜面，适合近景地面/踏板 |
| IndustrialPaint | 管廊/设备墙的灰绿涂料钢板 | 0.5 | 2.0m | 0.40 | 1.0 | 做旧但不过火，没有锈斑（原意图的「轻微锈迹」被模型放大成重度锈蚀，已砍掉） |

## 3. 需要补进 TilingFor 的数值

`MaterialArtLibrary.TilingFor()` 现在没有这些名字，会落到 `default: 1.0`，其中 6 套会明显偏大/偏小：

```csharp
case "TileFloor": return 0.833f;      // 1.2 m = 4 x 4 块 30 cm 砖
case "VenueCarpet": return 1.0f;
case "ConcreteWall": return 0.5f;     // 2 m
case "GlassPanel": return 0.667f;     // 1.5 m
case "MetalDoor": return 1.0f;
case "RubberStair": return 1.0f;      // 80 条凸筋 / 1 m
case "ServerPanel": return 1.667f;    // 0.6 m 穿孔板
case "ColdMetal": return 1.0f;
case "EpoxyFloor": return 0.5f;       // 2 m
case "CleanPanel": return 0.833f;     // 1.2 m 板宽
case "DiamondPlate": return 1.0f;
case "IndustrialPaint": return 0.5f;  // 2 m
```

## 4. 槽位接入建议

`MaterialArtLibrary.Slots` 现有 10 个槽：Wall / Floor / Ceiling / Column / Core / Stair /
PropLarge / PropMedium / PropSmall / SightBreak。推荐改法（**本次没有改 C#，由根代理接线**）：

| 槽位 | 现在 | 建议 | 理由 |
|------|------|------|------|
| Floor | CarpetTile | 默认仍 CarpetTile；会议室/高管区换 **VenueCarpet**，车间/机房换 **EpoxyFloor**，卫生间换 **TileFloor** | 一套槽位只能挂一张图，分区只能靠新增槽位或分区材质 |
| Wall | PaintedWall | 保持；核心筒/楼梯间加 **ConcreteWall**，洁净间加 **CleanPanel**，冷库加 **ColdMetal** | 工业区味道主要靠这 3 套拉开 |
| Column | ConcreteFloor | 可换 **ConcreteWall**（柱子和清水墙同色更统一） | — |
| Core | DarkMetal | 可换 **ServerPanel** 或 **IndustrialPaint** | 设备间/核心筒 |
| Stair | ConcreteFloor | 建议改 **RubberStair** | 楼梯踏步本来就是防滑橡胶，且它和 ConcreteFloor 太像 |
| PropLarge | WoodPanel | 可加 **MetalDoor** 专槽（门是高频视线焦点） | 门建议独立槽，别和柜子共用 |
| SightBreak | PaintedWall | 可换 **GlassPanel** | 隔断墙做成磨砂玻璃观感更「办公楼」 |
| PropMedium | DarkMetal | 保持 | — |
| PropSmall | BrushedMetal | 保持 | — |

要真正按分区贴图，需要给 `Slots` 增加分区键（例如 `Floor.Venue` / `Wall.Core`），或给
`FloorMaterials` 加按房间覆盖的入口——这属于脚本侧改动，本次未做。

## 5. 生成方式与已知取舍

**分两类**：

1. **纯 AI（6 套）**：VenueCarpet、ConcreteWall、MetalDoor、EpoxyFloor、IndustrialPaint、GlassPanel。
   SDXL base 1.0 + SeamlessTile + CircularVAE 出 1024² albedo，再由后处理做接缝检测/法线/打包。
2. **AI 底 + 程序化图案（6 套）**：TileFloor、RubberStair、ServerPanel、ColdMetal、CleanPanel、DiamondPlate。
   原因：SDXL 无法稳定产出「严格等距、且左右上下可无缝拼接」的规则图案，试了多轮 seed 全是
   随意拼花、透视砖墙或产品照。现在改为 AI 只提供表面颜色/颗粒，规则几何（砖缝、凸筋、孔阵、
   菱形块、板缝）用解析函数叠加，周期整除 1024，所以接缝数学上完美、图案尺度也精确可控。

平坦涂层（GlassPanel / ColdMetal / CleanPanel）额外走了低通：模型对「纯白/纯蓝平滑板」总倾向于
脑补石材裂纹或纤维纹，这些大尺度结构平铺时会变成一个明显重复的大斑块，因此把低频压平、只留
细颗粒。这三套的 albedo 接近纯色，观感靠 URP 光照和法线撑。

**已知不足**：

- ConcreteWall 没有木模板拼缝（AI 出不来规则的板缝；需要的话按 1.2m×2.4m 在建模上用几何分缝）。
- MetalDoor 没有竖向压痕/门板凹槽（只有极细漆面颗粒），门型轮廓需要靠模型几何表达。
- IndustrialPaint 没有铆钉行（铆钉属于几何细节，贴图里带上会导致平铺错位）。
- ServerPanel 的接缝指标 `seam≈0.08` 看着偏高，是因为孔阵在贴图边界正好落在「相邻两排孔的
  过渡线」上，和图像内部任意两排孔之间的过渡完全同值（已用纯图案在平底色上验证：wrap 与内部
  过渡都等于 0.2476），视觉上连续，属指标口径问题，不是真缝。

## 6. 复现 / 重出单套

```powershell
# 生成（第三个参数是 seed offset，换一张就 +1）
F:\ComfyUI\venv\Scripts\python.exe -u F:\ComfyUI\efw_gen_materials.py <SetName> 0

# 后处理（扫描最新源图，写回 Unity 工程）
F:\ComfyUI\venv\Scripts\python.exe -u F:\ComfyUI\efw_post_materials.py <SetName>

# 2x2 平铺预览（肉眼验缝）
F:\ComfyUI\venv\Scripts\python.exe -u F:\ComfyUI\efw_check_tiling.py
```

`efw_post_materials.py` 里每套的参数集中在 `TUNE`（smoothness, metallic, normal_strength,
blur_radius），另有 `PATTERN` / `PATTERN_FNS`（程序化图案）、`SMOOTH_BASE` / `BASE_PICK` /
`FINE_GRAIN`（平坦涂层处理）。改参数后只需重跑后处理，不必重生成。
