# PBR 材质库（第三批 12 套 · 设施楼层）

本批继续用本机 ComfyUI 管线（`F:\ComfyUI\efw_gen_materials.py` + `efw_post_materials.py`）补齐
设施楼层缺的 12 套材质，目录、命名、1024²、Albedo/Normal/Mask 三件套全部沿用前两批，
**未改动已有 19 套的任何文件**（旧图 mtime 与 SHA256 与开工前快照一致）。

## 1. 产出位置

```
game/Assets/_Project/Art/Textures/<Set>/T_<Set>_{Albedo,Normal,Mask}.png
```

- Albedo：sRGB 反照率。
- Normal：切线空间法线（Sobel 自灰度高程，`nLen≈0.996~0.998`，基本单位长度）。
- Mask：**R=metallic，G=AO，B=255，A=smoothness**（与前两批一致；本批 12 套的 B 通道全部实测为 255）。
- `.meta` 交给 Unity 首次导入自动生成；材质仍由 `MaterialArtLibrary` 统一构建，不要手搓材质。

## 2. 12 套一览

「每米重复」= 推荐写进 `MaterialArtLibrary.TilingFor()` 的值（1 uv = 1 米，含义是贴图每米重复几次）。
「循环尺寸」= 一个 uv 循环对应的真实尺寸。

| Set | 用途 | 每米重复 | 循环尺寸 | smoothness | metallic | 备注 |
|-----|------|---------|---------|-----------|---------|------|
| MetalGrate | 架空地板 / 设备平台 / 夹层钢格栅 | 1.667 | 0.6m = 20 条 30mm 节距扁钢（约 18mm 钢 / 12mm 缝）+ 100mm 横杆 | 0.42 | 0.85 | 解析图案；扁钢中心正好压在贴图边界，铺出来永远是连续格栅 |
| RubberFloor | 机房 / 设备层 / 楼梯间橡胶地板 | 1.0 | 1.0m | 0.09 | 0 | 暗灰颗粒，几乎不反光；适合大面积铺 |
| FabricPanel | 办公隔断 / 会议室布面吸音板 | 2.0 | 0.5m | 0.05 | 0 | 细密平纹织物，近看才见织纹；不要和地毯混贴 |
| PlasterWall | 普通办公抹灰墙（微凹凸白灰） | 0.667 | 1.5m | 0.24 | 0 | 只靠极细颗粒和低频斑驳，大墙不会出现明显重复块 |
| WoodFloor | 行政 / 接待 / 会议条木地板 | 0.833 | 1.2m = 8 条 150mm 宽板，板长 600mm，错缝 300mm | 0.36 | 0 | 解析叠板缝+错缝；AI 底色先转 90° 让木纹顺着板长 |
| FrostedGlass | 隔断磨砂玻璃（浅青灰雾面） | 0.667 | 1.5m | 0.72 | 0 | 只有低频云雾，不带反光斑；透光和模糊交给 URP |
| CeramicWall | 卫生间 / 洁净区白瓷砖墙 | 0.833 | 1.2m = 4×2 块 300×600mm 墙砖（3mm 砖缝） | 0.55 | 0 | 解析砖缝，砖缝中线压在边界；釉面被压平，避免 SDXL 的划痕纹 |
| CeilingGrid | 办公区矿棉板吊顶 + T 型龙骨 | 0.833 | 1.2m = 2×2 块 600mm 板 + 24mm T 龙骨 | 0.22 | 0 | 解析图案；龙骨正好在边界和中线，2×2 平铺后是完整 T 格 |
| RustSteel | 老旧管廊 / 仓库锈蚀钢板 | 1.0 | 1.0m | 0.32 | 0.65（锈处衰减到约 0.10） | 锈斑 + 解析划痕（42 条）；metallic 按锈色自动衰减 |
| HazardPaint | 装卸区 / 车间警示漆地面 | 1.0 | 1.0m = 6 组 45° 黄黑斜条（法向节距约 118mm） | 0.30 | 0 | 解析条纹；周期 1024/6 整除，斜向接缝数学上完美 |
| VinylFloor | 医院 / 洁净区 PVC 卷材 | 0.5 | 2.0m | 0.31 | 0 | 浅色均质 + 1px 彩色杂点（PVC 切片），大面几乎无重复感 |
| ConcreteCeiling | 停车 / 管廊清水混凝土顶板 | 0.833 | 1.2m = 2 道 600mm 模板拼缝 + 20mm 对拉螺栓孔 | 0.20 | 0 | 气孔 + 模板痕；拼缝很浅，远看是整块清水面 |

> 说明：`WoodFloor` / `CeramicWall` / `HazardPaint` 这类解析图案的「循环尺寸」是**设计值**，
> 图案周期严格整除 1024，写进 TilingFor 后不会出现半块板/半条缝。

## 3. 需要补进 TilingFor 的数值

`MaterialArtLibrary.TilingFor()` 现在没有这 12 个名字，会落到 `default: 1.0`，其中 8 套比例不对：

```csharp
case "MetalGrate":      return 1.667f;   // 0.6 m 钢格栅
case "RubberFloor":     return 1.0f;
case "FabricPanel":     return 2.0f;     // 0.5 m 织物
case "PlasterWall":     return 0.667f;   // 1.5 m
case "WoodFloor":       return 0.833f;   // 1.2 m 板长
case "FrostedGlass":    return 0.667f;   // 1.5 m
case "CeramicWall":     return 0.833f;   // 1.2 m = 4x2 块 300x600 墙砖
case "CeilingGrid":     return 0.833f;   // 1.2 m 矿棉板
case "RustSteel":       return 1.0f;
case "HazardPaint":     return 1.0f;
case "VinylFloor":      return 0.5f;     // 2 m
case "ConcreteCeiling": return 0.833f;   // 1.2 m 模板格
```

`MetalDoor`、`ServerPanel` 等前两批的 case 保持不动；本批**没有改 C#**，接线由根代理处理。

## 4. 槽位接入建议（未改 C#）

`MaterialArtLibrary.Slots` 仍是 10 个槽，一套槽位只能挂一张图，本批的用法是「按房间/分区换整套材质」：

| 槽位 | 建议 |
|------|------|
| Floor | 默认仍 `CarpetTile`；机房换 **RubberFloor**，行政/接待换 **WoodFloor**，医疗/洁净换 **VinylFloor**，装卸/车间换 **HazardPaint**，设备平台/架空地板换 **MetalGrate** |
| Wall | 普通办公保持 `PaintedWall`；会议/隔断加 **FabricPanel**，卫生间/洁净加 **CeramicWall**，管廊/仓库加 **RustSteel**，普通办公大白墙可换 **PlasterWall** |
| Ceiling | 默认换 **CeilingGrid**（办公），停车/管廊换 **ConcreteCeiling** |
| SightBreak | 换 **FrostedGlass**（隔断玻璃），比 `GlassPanel` 更雾、更青 |
| Core | 可换 **RustSteel** 或 `ServerPanel` |
| Column | 可换 **PlasterWall**（和办公墙同色更干净） |

要真正按分区贴图，仍需要给 `Slots` 增加分区键（例如 `Floor.Medical` / `Ceiling.Parking`），
或给 `FloorMaterials` 加按房间覆盖入口——这是脚本侧改动，本次未做。

## 5. 生成方式

**分两类（各 6 套）**：

1. **纯 AI（6 套）**：RubberFloor、FabricPanel、PlasterWall、FrostedGlass、RustSteel、VinylFloor。
   SDXL base 1.0 + SeamlessTile + CircularVAE 出 1024² albedo，后处理做接缝检测 / 法线 / 打包。
   - RustSteel 重出过一版：第一版被模型画成「灰色石块 + 橙色砖缝」，改 prompt 强调
     `rust mottling / rust streaks / no bricks, no stone` 后得到钢板锈斑；另叠加 42 条解析划痕
     （环形距离，跨边界的划痕会在对边正确接续）。
   - VinylFloor 在低通之后加了 5% 密度的 1px 彩色杂点（PVC 切片），iid 噪声天然可平铺。
2. **AI 底 + 程序化图案（6 套）**：MetalGrate、WoodFloor、CeramicWall、CeilingGrid、HazardPaint、ConcreteCeiling。
   原因同前两批：SDXL 无法稳定产出「严格等距且可无缝拼接」的规则图案。现在 AI 只提供
   表面颜色/颗粒，几何（格栅、板缝、砖缝、T 龙骨、斜条纹、模板缝）全部由解析函数叠加，
   周期整除 1024，所以接缝数学上完美、尺度精确可控。
   - WoodFloor 额外做了 90° 旋转：模型把「horizontal grain」画成了竖向纹理，转 90° 后木纹
     顺着板长方向，板缝仍然是解析叠加的。

平坦涂层（FrostedGlass / CeramicWall / PlasterWall / VinylFloor / CeilingGrid）都额外走了低通：
模型对「纯白/纯灰平滑面」总倾向于脑补划痕、石纹或竖向拉丝，平铺时会变成明显的重复大斑，
因此压掉低频大结构、只保留细颗粒。

## 6. 接缝与贴图自检

`efw_report2.py` 的实测值（`seamLR`/`seamTB` = 贴图左右/上下边界差；`inner` = 图内相邻像素平均差；
正常应满足 `seam ≤ inner`，即边界过渡不比内部突兀）：

| Set | seamLR | seamTB | inner | 判定 |
|-----|--------|--------|-------|------|
| MetalGrate | 0.0150 | 0.0133 | 0.0159 | 通过 |
| RubberFloor | 0.0441 | 0.0456 | 0.0463 | 通过 |
| FabricPanel | 0.0380 | 0.0442 | 0.0471 | 通过 |
| PlasterWall | 0.0189 | 0.0226 | 0.0208 | 通过（TB 高 8%，是极细颗粒的统计噪声） |
| WoodFloor | 0.0179 | 0.0768 | 0.0529 | 边界=板缝，见下 |
| FrostedGlass | 0.0032 | 0.0032 | 0.0032 | 通过（几乎恒等） |
| CeramicWall | 0.0053 | 0.0090 | 0.0031 | 边界=砖缝中线，见下 |
| CeilingGrid | 0.0000 | 0.0000 | 0.0238 | 通过（T 龙骨压在边界，完全对齐） |
| RustSteel | 0.0237 | 0.0232 | 0.0297 | 通过 |
| HazardPaint | 0.0115 | 0.0112 | 0.0109 | 通过（与内部条纹过渡同值） |
| VinylFloor | 0.0226 | 0.0214 | 0.0221 | 通过 |
| ConcreteCeiling | 0.0233 | 0.0168 | 0.0198 | 通过 |

三处「指标 > inner」都是**图案落在边界上**的定义问题，不是真缝：

- **WoodFloor** 上下边界正好是一条 3mm 板缝（板缝中线在 y=0），裁剪后两边各半条缝，
  和内部任意两条板缝的过渡完全同构；2×2 预览里板缝间距、错缝节奏都连续。
- **CeramicWall** 上下边界是 3mm 砖缝中线，理由同上。
- **CeilingGrid** 的 T 龙骨中心正好压在四条边界上，边界与内部都是「龙骨—板—龙骨」，
  实测边界差正好 0，是最理想的情况。

2×2 平铺预览（每张 1024²，2×2 后缩到 1024）：

```
F:\ComfyUI\output\materials\_preview2\prev_<Set>.png
F:\ComfyUI\output\materials\_preview2\sheet_all2.png     # 4 列 × 3 行总览
```

12 张逐一肉眼检查：格栅、砖缝、吊顶 T 格、警示斜条、混凝土地缝在四条边界上都连续，
没有方向性接缝，没有半块板/半条缝。

## 7. 已知不足

- **MetalGrate**：没有真实几何厚度，孔洞只是近黑伪造深度，掠射角下会显得像「开槽板」而不是
  有厚度的格栅；近景（<2m）建议用模型几何补承载扁钢侧面。
- **WoodFloor**：AI 木纹比真实橡木更密、更「等高线」，板面缺少节疤/色差（每块板只有 9% 的
  随机明度抖动）；板缝只有 2~3mm，远景基本看不见，需要靠法线在光照下显形。
- **CeramicWall**：釉面被低通压平（为了去掉 SDXL 的竖向划痕），近看缺少釉面起伏；砖缝是
  3mm 细线，没有倒角高光的几何感。
- **CeilingGrid**：T 龙骨与矿棉板的明度差很小（0.93 vs 0.79），远看吊顶接近纯白；矿棉板的
  针孔在 1.2m 循环下只有 1~2px。
- **FrostedGlass**：贴图只给颜色，磨砂的「透光模糊」必须由 URP 的 Transparent + 粗糙度/
  Refraction 表达，单看 albedo 会像一块浅青灰塑料板。
- **HazardPaint**：只有干净的黄黑漆，没有磨穿到金属的斑块和轮胎印；做旧程度比 `EpoxyFloor` 轻。
- **ConcreteCeiling**：模板拼缝只有 5% 压暗，2×2 预览里几乎看不出分格；对拉螺栓孔是 20mm 的
  浅坑，需要法线+光照才明显。
- **RubberFloor / FabricPanel / PlasterWall / VinylFloor**：都是「细颗粒 + 大面均匀」型，
  1~2m 外几乎只剩颜色；这是刻意的（大面积平铺不重复），近景细节靠法线图。
- 本批没有重出旧 19 套；旧图的 mtime / SHA256 与开工前 `F:\ComfyUI\_old19_inventory.csv` 一致。

## 8. 复现 / 重出单套

```powershell
# 生成（第二个参数是 seed offset，换一张就 +1）
F:\ComfyUI\venv\Scripts\python.exe -u F:\ComfyUI\efw_gen_materials.py <SetName> 0

# 后处理（自动取该 set 最新源图，写回 Unity 工程；不带参数=只跑本批 12 套）
F:\ComfyUI\venv\Scripts\python.exe -u F:\ComfyUI\efw_post_materials.py <SetName>

# 2x2 平铺预览 + 总览
F:\ComfyUI\venv\Scripts\python.exe -u F:\ComfyUI\efw_check_tiling2.py

# 接缝 / Mask / 法线自检（输出 _preview2\_report2.txt）
F:\ComfyUI\venv\Scripts\python.exe -u F:\ComfyUI\efw_report2.py
```

`efw_post_materials.py` 里本批参数集中在：

- `TUNE`（smoothness, metallic, normal_strength, blur_radius）——12 套各一行；
- `PATTERN` / `PATTERN_FNS`（解析图案）——`pattern_grate` / `pattern_planks` /
  `pattern_tile_rect` / `pattern_ceiling` / `pattern_hazard` / `pattern_concrete_boards`；
- `SMOOTH_BASE` / `LOWCUT` / `DESAT` / `GAIN`（平坦涂层处理）；
- `ROTATE`（WoodFloor 转 90°）、`SPECKLE`（VinylFloor 杂点）、`SCRATCH`（RustSteel 划痕）、
  `METALLIC_RUST`（锈处 metallic 衰减）。

改参数后只需重跑后处理，不必重新生成；改图案周期时务必保持整除 1024。
