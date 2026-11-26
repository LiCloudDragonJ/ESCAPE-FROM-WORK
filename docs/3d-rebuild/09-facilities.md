# 09 设施语法库（R4 设计规格）

> 状态：2026-11-26 设计稿，供 R4 实现。只写规格，不含实现、不改代码。
> 上游：[06-raid-structure.md](06-raid-structure.md) D1/D3/D7、[07-handoff.md](07-handoff.md) §6/§7 R4、[02-level-design.md](02-level-design.md) §2/§4。
> 下游：R5 SlicePlanner（§6）、R6 VerticalLinkPlan、R8 环路、R9 Signage。

## 1. 目的与边界

**要解决的问题（数字）**：当前单层 200×160 = 32,000㎡，111–126 间房，中位 112–134㎡，分档 43/34/14/2%，全部来自同一套办公语法——房间是"面积不同、内容相同"的方块。R4 用 12 类设施 + 6 个主题包，让同一层的房间在**面积、净高、门、道具、材质、连通方式**上同时拉开差异。

| 设施层负责 | 设施层不负责 |
|---|---|
| 每间房是什么设施、目标面积区间、形状偏好 | BSP 怎么切、切几刀、叶节点多大（`FloorGenerator.SplitNode`） |
| 净高需求（声明）、门数与门宽目标、是否串联 | 门的具体坐标、门位距墙角（`RoomDoorPolicy`） |
| 道具集与权重、材质槽、地标资格、可封死性 | 墙/地/顶几何、灯盘、NavMesh（`FloorGeometry`） |
| 主题配比、每层 3–4 类主导设施 | 撤离点选哪几个、出生区落哪（`AddExtractions`/`AddSpawnZones`） |
| 给 R5 的"哪些房间不能封"元数据 | 切片选层、30–50% 楼板怎么切（`SlicePlanner`） |

职责分三层，**设施层只回写参数，不自己切分**：

1. `FloorGenerator` 负责几何：BSP 出叶、走廊带宽、门洞实体化。
2. `Zone`（现有 4 象限）负责分区：每个象限拿一个主题配比。
3. `FacilityGrammar` 负责语义：给 `SplitNode` 一个切分偏置、给 `RoomDoorPolicy` 一个门目标、给 `CoverPlacer` 一个道具集。

为什么这么切：BSP 是几何引擎，设施是语义层。设施层一旦直接改切分，就会变成第二个生成器，R1–R3 的 113 项测试（`SingleDoorTests`/`SightlineTests`/`RoomScaleTests`）全部要重写；只回写参数则现有管线不动，R4 可以独立回滚。

## 2. 12 类设施

### 2.1 空间属性

面积是**真实轮廓面积**（`RoomShape.AreaOf`，鞋带公式），不是包围盒。短边指房间轮廓最短边。净高是"本层完成面到吊顶/结构板"的需求值，由主题满足（见 §2.4）。

| id | 设施 | 典型面积㎡ | 短边≥ | 形状偏好 | 净高 | 门数×净宽 | 连通多个房间 |
|---|---|---|---|---|---|---|---|
| `ServerRoom` | 机房 | 180–420 | 10m | 矩形 / L | 4.2m（不吊顶） | 2–3 × 1.2/1.4 | 是：与变电、中控、管廊同层相通 |
| `ColdStore` | 冷库 | 120–300 | 8m | 矩形 / 长条 | 3.37m | 2 × 1.2（保温门+逃生门） | 是：与货梯、装卸相通；可 2 间串前室 |
| `ServiceGallery` | 管廊 | 40–160/段 | 4.2m | 长条 | 3.37m | 2/段 × 0.9 | **是：强制 2–4 段串联**，段间开门 |
| `Workshop` | 车间 | 300–800 | 14m | 矩形 / L | 6.0m | 3 × 1.9（长边两端各 1） | 是：与装卸、储罐、管廊 |
| `ControlRoom` | 中控 | 90–180 | 7m | 矩形 | 3.37m | 2 × 1.0 | 是：与机房/变电隔玻璃观察窗 |
| `Substation` | 变电 | 60–120 | 6m | 矩形 | 6.0m | 2 × 1.2（设备门+逃生门） | 是：**必须与机房同层**（供电链） |
| `TankFarm` | 储罐 | 150–350 | 8m | 矩形 / L | 8.0m | 3 × 1.4 | 是：与管廊、车间 |
| `Isolation` | 医疗隔离 | 80–160 | 7m | 矩形 | 3.37m | 3 × 1.0（含前室双门） | 是：与洁净间；最多 2 间串联 |
| `LoadingDock` | 装卸 | 250–600 | 12m | 矩形 / 长条 | 8.0m | 3 × 1.9（+2.4 卷帘见 §2.4） | 是：货梯、冷库、车间、管廊 |
| `Archive` | 档案 | 70–140 | 6m | 矩形 / 长条 | 3.37m | 2 × 1.0 | 是：与行政/中控；主题可放大档案库 |
| `CleanRoom` | 洁净间 | 100–220 | 8m | 矩形 | 3.37m | 2 × 1.0 + 风淋双门 | 是：与医疗隔离 |
| `Habitat` | 生活舱 | 56–110 | 6m | 矩形 / L | 3.37m | 2 × 0.9 | 否：只接走廊，不与其他设施串联 |

为什么这样定：

- **面积区间是从 R3 的 40/35/20/5 反推的，不是美术偏好**：中位段 56–200㎡ 必须占 75%，所以 12 类里 8 类的典型区间落在 60–220；只有机房/车间/储罐/装卸 4 类进 200+，且每层最多 1–2 间。否则 200–400 档会被设施本身顶穿，`RoomScaleTests` 当场红。
- **管廊最小短边 4.2m**：现有 `MinRoomSide = 3.2m`、`SplitNode` 的 `aLen/bLen ≥ 3.6m`，2–3m 宽的窄管廊根本切不出来；且 `Emit` 会把 >32m×<8m 的长条直接转成走廊。所以管廊做成"宽管廊"（一段 4.2–6.0m 宽 × 8–24m 长），不是 1.5m 的检修夹道。
- **生活舱最小 56㎡**：R3 规定 <56㎡ 只能是储藏/卫生间/管井。单舱（18–30㎡）不做成房间，用双层床/储物柜道具表达；否则 `OnlySmallRooms_GetServiceTypes` 会失败。
- **门宽集中在 0.9–1.9m**：`FloorGenerator` 现有门宽是 `Mathf.Clamp(..., 0.9f, 1.9f)`，超出部分必须新增门类型，不能只写在设施表里。

### 2.2 内容属性

道具档沿用 [04-3d-assets.md](04-3d-assets.md)：大件主尺寸 1.4–2.2m、中件 1.2–1.8m、小件 0.4–0.6m；面数大件 ≤1500、中件 ≤900、小件 ≤300，各带一级 LOD。新道具必须同时给"占地 W×D×H / 面数预算 / 单 BoxCollider"。

| id | 道具集（大 / 中 / 小） | 新道具（占地 m，面数） | 材质槽（地 / 墙 / 顶 / 门 / 点缀） |
|---|---|---|---|
| `ServerRoom` | 服务器机柜、精密空调 / 配电柜、桥架 / 地板送风口、工具箱 | 机柜 0.6×1.2×2.2，1200；精密空调 1.6×0.9×2.0，900；配电柜 1.0×0.6×2.2，700 | EpoxyFloor / ServerPanel / DarkMetal / MetalDoor / BrushedMetal |
| `ColdStore` | 冷库货架、制冷机组 / 保温箱、托盘 / 温度计、门帘 | 货架 1.6×0.8×2.0，900；制冷机组 1.8×0.9×1.8，1200 | EpoxyFloor / ColdMetal / ColdMetal / MetalDoor / IndustrialPaint |
| `ServiceGallery` | 管道组（4m 跨）、风管 / 阀门组、检修梯 / 压力表、工具箱 | 管道组 4.0×0.6×0.8，600；阀门组 0.8×0.8×1.6，400 | DiamondPlate / IndustrialPaint / ConcreteWall / MetalDoor / BrushedMetal |
| `Workshop` | 机床、料架、工作台 / 工具车、托盘 / 零件箱、灭火器 | 机床 1.8×1.2×1.6，1400；工作台 2.0×0.9×0.9，800；料架 1.6×0.8×2.0，900 | EpoxyFloor / IndustrialPaint / DarkMetal / MetalDoor / DiamondPlate |
| `ControlRoom` | 大屏墙、主控台组 / 控制台、机柜 / 办公椅、文件柜 | 主控台 2.4×1.0×1.2，900；大屏墙 3.0×0.2×1.8，600 | BrushedMetal / PaintedWall / CeilingTile / GlassPanel / WoodPanel |
| `Substation` | 变压器、高压柜 / 配电柜、绝缘围栏 / 绝缘垫、警示牌 | 变压器 1.8×1.2×1.6，1400；高压柜 1.0×0.6×2.2，700 | ConcreteFloor / ConcreteWall / DarkMetal / MetalDoor / IndustrialPaint |
| `TankFarm` | 储罐、泵组 / 管道、阀组 / 检修梯、仪表 | 储罐 φ2.4×5.0（占地 2.4×2.4），1500；泵组 1.2×0.8×1.2，600 | DiamondPlate / IndustrialPaint / DarkMetal / MetalDoor / ConcreteWall |
| `Isolation` | 病床、隔离舱 / 医疗柜、洗手台 / 输液架、垃圾桶 | 病床 2.0×0.9×0.65，900；隔离舱 1.2×0.8×2.0，700 | TileFloor / CleanPanel / CeilingTile / GlassPanel / PaintedWall |
| `LoadingDock` | 货托盘、叉车、装卸平台 / 集装箱、托盘 / 地锁、警示柱 | 叉车 2.0×1.2×1.5，1200；货托盘 1.2×1.0×1.4，500 | ConcreteFloor / ConcreteWall / DarkMetal / MetalDoor / DiamondPlate |
| `Archive` | 密集架、档案柜 / 文件柜、推车 / 碎纸机、纸箱 | 密集架 1.2×0.6×2.2，800 | CarpetTile / PaintedWall / CeilingTile / WoodPanel / MetalDoor |
| `CleanRoom` | 洁净工作台、传递窗 / 风淋室、更衣柜 / 垃圾桶、工具箱 | 洁净工作台 1.5×0.8×0.9，700；风淋室 1.2×1.2×2.2，600 | EpoxyFloor / CleanPanel / CleanPanel / GlassPanel / MetalDoor |
| `Habitat` | 双层床、储物柜 / 桌椅、冰箱 / 饮水机、垃圾桶 | 双层床 2.0×0.9×1.8，900；储物柜 0.9×0.6×1.8，500 | VenueCarpet / WoodPanel / CeilingTile / WoodPanel / PaintedWall |

- **19 套材质全部有归属**：上表 18 套 + 主题级 `RubberStair`（楼梯踏步，不属于房间槽）。验收要求 ≥14，实际 19/19，这样 R4 顺带把 [07-handoff.md](07-handoff.md) §10 里"9 套没有槽位"的债还掉。
- **新道具面数合计预算**：每间房同时可见新道具 ≤12 件，单层新道具三角面 ≤60k；超过就切 LOD1（40m 外 40% 面数）。为什么有这条：现有单层 825 件掩体 + 83k 顶点已经接近 draw call 预算，设施道具只许替换不许净增。
- **储罐/叉车是"结构道具"**：主尺寸超出 1.4–2.2m 档，不参与"大件挡视线"统计，单独按 ≥1.5m 高算硬遮挡；否则视线审计会漏算。

### 2.3 形状、门宽与现有管线的冲突

| 形状 | 当前能否生成 | 处置 |
|---|---|---|
| 矩形 | 100% | 直接可用 |
| 长条（管廊） | 可以，但短边必须 ≥4.2m、长边 8–24m | 用 `ServiceGallery` 的宽管廊；<3.2m 不做成房间 |
| 非矩形（外廓裁出的 5–8 边凸多边形） | 已交付（`FloorEnvelope.ClipRect`） | 计入"非矩形占比" |
| L 形（凹多边形） | **不能**：`ClipRect` 返回凸块，只保留最大块 | 建议 A：R4 验收只写"非矩形 ≥15%"；若后续改 B 做真 L 形，需先做 M1-A 第 4 步（合并叶子 + 斜墙隔断），会牵动 `RoomScaleTests`、门策略与分块归属 |

| 门类型 | 净宽 | 现状 | 用在哪 |
|---|---|---|---|
| 单开 | 0.9–1.2m | 已有 | 生活舱、档案、管廊、洁净间、医疗前室 |
| 双开 | 1.4–1.9m | 已有（clamp 上限 1.9） | 机房、车间、储罐、装卸人行门 |
| 卷帘 | 2.4–3.0m | **没有**，`DoorKind` 需新增 | 装卸车位门 |
| 服务门 | 0.8m | 已有（`CorridorBreaks.ServiceDoorWidth`） | 服务廊侧门，不属于设施 |

建议 A：R4 的装卸先用 1.9m 双开（不新增门类型），卷帘留到 M5 美术升级；若后续改 B，需要新增 `DoorKind.Rollup`、放宽 `FloorGenerator` 的 0.9–1.9 clamp，并让 `FloorGeometry.CutDoor` 支持 2.4m 门洞与门楣——这是几何改动，不是数据改动。

## 3. 每层主题组合

每层抽 **1 个主题包**；包内 **3–4 类主导设施**（占房间数 ≥70%）+ 1–3 类配套。这就是 07-handoff 说的"每层 3–4 种主题"——主题不是 12 类各来一点，而是 3–4 类主导设施撑起一层的读法。主题由 `(buildingSeed, floorIndex)` 决定，不随对局切片重 roll（理由见 §6）。

| 主题 | 楼层带 | 净高 | 允许组合（CorridorLevels + Prototype） | 主导 3–4 类 | 配套 | 地标（≥300㎡） |
|---|---|---|---|---|---|---|
| 数据中心层 `DataHall` | 塔身 26–40 | 4.2m | `3 + Utility`；`3 + Renovation`（旧厂房改数据中心） | 机房 / 中控 / 管廊 / 档案 | 变电、生活舱 | 主机房 300–420 |
| 冷链物流层 `ColdChain` | 裙房 1–6 / 地下 | 4.2m | `2 + Utility`；`3 + Utility` | 冷库 / 装卸 / 管廊 | 车间、档案、生活舱 | 装卸大厅 350–600 |
| 制造层 `Fabrication` | 裙房 / 设备层 | 6.0m | `3 + Renovation`；`3 + Utility` | 车间 / 储罐 / 变电 / 管廊 | 中控、装卸、生活舱 | 车间 500–800 |
| 医疗隔离层 `MedIsolation` | 塔身 7–25 | 3.37m | `2 + SmallOffice`；`2 + Meeting` | 医疗隔离 / 洁净间 / 中控 | 档案、生活舱、冷库 | 负压病区 280–420（隔离的区间覆盖） |
| 行政层 `Admin` | 塔身 15–35 | 3.37m | `2 + Executive`；`2 + Meeting` | 档案 / 生活舱 / 中控 | 机房、洁净间 | 大档案库 300–500（档案的区间覆盖） |
| 仓储层 `Storage` | 裙房 / 地下 | 6.0m | `3 + Utility`；`2 + Renovation` | 档案（高架库）/ 装卸 / 冷库 / 管廊 | 车间、储罐、生活舱 | 高架库 400–600（档案的区间覆盖） |

**每层配比（房间数占比，合计 100）**

| 主题 | 配比 |
|---|---|
| 数据中心层 | 机房 45 / 中控 15 / 管廊 15 / 档案 10 / 变电 10 / 生活舱 5 |
| 冷链物流层 | 冷库 40 / 装卸 15 / 管廊 15 / 车间 10 / 档案 10 / 生活舱 10 |
| 制造层 | 车间 35 / 管廊 15 / 储罐 10 / 变电 10 / 中控 10 / 装卸 10 / 生活舱 5 / 冷库 5 |
| 医疗隔离层 | 隔离 45 / 洁净 20 / 中控 10 / 档案 10 / 生活舱 10 / 冷库 5 |
| 行政层 | 档案 35 / 生活舱 25 / 中控 20 / 机房 10 / 洁净 10 |
| 仓储层 | 档案 25 / 装卸 15 / 冷库 15 / 管廊 15 / 车间 10 / 生活舱 10 / 储罐 10 |

**落位规则**

1. 4 个象限各认领 1 类主导设施，主导设施的房子必须落在该象限内（复用现有 `AddZones` 的四象限，不新增分区算法）。
2. 每层的大空间（≥300㎡）只放 1 个，且必须避开外廓缺口/截角，防止被 `ClipRect` 裁成 200㎡。
3. 管廊沿主廊并行走，不占象限中心；它必须 2–4 段串联，且每段两端各 1 门。
4. 同一象限内允许 1 类配套设施插花，但不得连续 3 间同设施——否则走廊两侧读起来还是复制粘贴。

为什么这样定：

- **同层风格统一、方差大**：主导设施占 ≥70%，所以一层的墙/地/顶主色只有 2–3 套（`FloorMaterials` 可按主题换全局槽）；但"一个象限 500㎡ 车间 + 另一个象限 60㎡ 变电"把 p90/p10 拉到 2.2 以上。
- **地标必须是设施，不是中庭**：06 §3.4 要"1–2 个不可混淆的地标"。用 ≥300㎡ 的机房/车间/装卸/档案库当地标，比再挖一个中庭便宜，且地标自带"能看 ≥60m"的视线。
- **主题按楼层带分配**：地下/裙房放冷链/仓储/制造（低价值、AI 多、撤离多），塔身中层放医疗/行政，塔身高区放数据中心——对应 06 §3.1 的竖向价值梯度，玩家"往上走更值钱"不需要地图。

## 4. 数据模型（只给签名，不实现）

```csharp
public enum FloorTheme { DataHall, ColdChain, Fabrication, MedIsolation, Admin, Storage }

public enum FacilityKind
{
    ServerRoom, ColdStore, ServiceGallery, Workshop, ControlRoom, Substation,
    TankFarm, Isolation, LoadingDock, Archive, CleanRoom, Habitat
}

[System.Flags] public enum FacilityShape { Rect = 1, LShape = 2, Strip = 4 }
public enum DoorKind { Swing, Double, Rollup }

public struct FacilityMaterialSlots
{
    // 存 19 套材质的 byte 索引，不存 string：Room 是 struct，会被按值复制上千次
    public byte Floor, Wall, Ceiling, Door, Accent;
}

public struct PropWeight
{
    public string Name;      // 必须与 PropCatalog.Entry.Name 完全一致
    public PropSize Size;    // Large / Medium / Small
    public float Weight;     // 组内 0–1，归一化后抽
    public int TriBudget;    // 1500 / 900 / 300
    public bool IsNewAsset;  // 需要新建模（04 §2）
}

public sealed class FacilityProfile
{
    public FacilityKind Kind;
    public string DisplayName;
    public Vector2 AreaRange;        // 典型区间，见 §2.1
    public float MinShortSide;
    public FacilityShape Shapes;
    public float ClearHeight;        // 3.37 / 4.2 / 6.0 / 8.0
    public float SplitBias;          // stopP 乘子：<1 更愿意继续切，>1 更早停
    public int MinDoors, TargetDoors;
    public float DoorWidth;
    public DoorKind DoorKind;
    public bool SerialRun;           // 管廊：段间必须开门
    public int MinRunSegments;
    public FacilityMaterialSlots Materials;
    public PropWeight[] PropSet;
    public bool MayProduceVault;     // 只有 Archive / ServerRoom 为 true
    public RoomType CoarseType;      // 派生到粗分类，见 §4.2
}

public struct FacilitySlot
{
    public FacilityKind Kind;
    public float Weight;
    public Vector2 AreaOverride;     // 主题级放大，如行政层的档案库
    public int MinRooms, MaxRooms;   // 每层硬约束，防止配比漂移
}

public sealed class ThemeMix
{
    public FloorTheme Theme;
    public FacilitySlot[] Slots;
    public int CorridorLevels;       // 2 / 3
    public FloorPrototype Prototype; // 兼容旧枚举
    public float ClearHeight;
    public FacilityKind LandmarkKind;
}

public static class FacilityGrammar
{
    public static FloorTheme PickTheme(FloorSpec spec, int seed);              // 只读 buildingSeed + floorIndex
    public static ThemeMix Mix(FloorTheme theme);
    public static FacilityProfile Profile(FacilityKind kind);

    // 阶段 1：AddZones 内，给 4 个象限写主导设施与配比
    public static void PlanZones(FloorPlan plan, FloorSpec spec, System.Random rng);

    // 阶段 2：SplitNode 内，按象限主导设施返回 stopP 乘子
    public static float SplitBias(Zone zone);

    // 阶段 3：Emit 之后、CorridorBreaks 之前，给每间房定设施
    public static void Assign(FloorPlan plan, FloorSpec spec, System.Random rng);
    public static bool Fits(FacilityProfile p, Room room);

    // 阶段 4：RoomDoorPolicy 读门目标；CoverPlacer 读道具集
    public static void ApplyDoorPolicy(Room room, FacilityProfile p);
    public static void Decorate(FloorPlan plan, System.Random rng);

    // 阶段 5：供 R5 查询
    public static bool IsSealable(FloorPlan plan, Room room);
    public static bool IsLandmark(Room room);
    public static int RunId(Room room);          // 管廊段所属 run，非管廊返回 -1
    public static float EstimateRooms(FloorTheme theme, float area);
}
```

**`Room` 新增字段**（保持 struct 小）：

```csharp
public FacilityKind Facility;          // 4 字节
public FacilityMaterialSlots Materials; // 5 字节
public bool WeldDoor;                  // 保险库焊死的那扇门
public bool IsLandmark;
```

**`FloorSpec` 新增**：`FloorTheme Theme`、`float ClearHeight`、`int BuildingSeed`（主题按楼栋种子而不是对局种子抽）。

### 4.1 `FacilityGrammar.Assign` 的职责

1. 按 `Zone` 配比 + 房间面积/短边/Outline，给每间房选一个 `FacilityKind`；`Fits` 不满足的，优先换同类小/大档，其次降级为配套设施，最后退回 `RoomType.Normal`。
2. 写 `Room.Facility`、`Room.Materials`、`Room.IsLandmark`、`Room.WeldDoor`。
3. 写门目标：`MinDoors`/`TargetDoors`/`DoorWidth`，供 `RoomDoorPolicy` 读取（替换现在硬编码的 `>=100㎡ ? 3 : 2`）。
4. 派生 `Room.Type`：设施 → 粗分类（下表）；不再由面积单独决定。
5. 记 `RunId`：管廊按"同象限、共享长边、间隔 ≤1 门"串成 run，2–4 段。
6. 保险库：仅当设施是 `Archive`/`ServerRoom`、房间 28–55㎡、且本层 Vault 面积 ≤8% 时，把 `Type` 置 `Vault`、两门其中一扇 `WeldDoor=true`。

### 4.2 与 `RoomType` 的关系

**建议 A：`RoomType` 保留为粗分类，`FacilityKind` 是细分设施，`Vault` 只由设施层产生。**

| FacilityKind | RoomType | 谁在用这个粗分类 |
|---|---|---|
| 机房/冷库/车间/中控/变电/储罐/隔离/装卸/档案/洁净/生活舱 | `Normal` | 门策略、地图区域名、战利品表 |
| 管廊 | `Normal`（加 `IsService` 语义，不进高价值池） | 导航、撤离距离（管廊是捷径） |
| 保险库（附属在 Archive/ServerRoom 内） | `Vault` | 门策略例外（2 门 1 焊死）、报警 |
| 未命中任何设施的 <56㎡ 边角 | `Storage`/`Toilet`/`Riser` | 沿用 R3 的 55/25/20 兜底 |

为什么保留 `RoomType`：`SingleDoorTests`、`RoomScaleTests`、`MapHud` 的区域名都只关心"这间房是不是服务间"，不关心它是冷库还是变电；把 12 类塞进 `RoomType` 会让这些系统被迫理解设施语义。`Vault` 必须是 `RoomType` 而不是 `FacilityKind`，因为它是"门的例外规则 + 报警"，不是一种空间。

### 4.3 管线顺序（必须按这个顺序接线）

```
Build:
  AddZones            → FacilityGrammar.PlanZones
  SplitNode           → FacilityGrammar.SplitBias(zone) 乘进 stopP
  Emit                → AddRoomClipped
  FacilityGrammar.Assign          ← 设施 / 材质 / 门目标 / RoomType
  CorridorBreaks.Apply            ← 会改走廊，必须排在门之前
  RoomDoorPolicy.Apply            ← 读设施门目标
  AssignRoomTypes                 ← 退化为兜底（未命中设施时才跑）
  AddExtractions / AddSpawnZones
  CoverPlacer.Place → Decorate    ← 按 PropSet 加权 + 设施专属摆法
```

为什么 `Assign` 必须早于 `RoomDoorPolicy`：门数/门宽是设施属性（车间要 3 门 1.9m），如果先跑门策略再贴设施标签，就只能"事后改门"，而 `RoomDoorPolicy` 已经把不满足策略的房间 `DissolveNoncompliantRooms` 掉了。

## 5. 验收标准

全部可写成 NUnit `[Test]`，风格对齐 `RoomScaleTests`：多样本合并统计，避免单层 100 多间房时参数一动就红。

| 测试 | 断言 | 阈值 | 样本 |
|---|---|---|---|
| `AreaSpread_AtLeast22` | 房间面积 p90 / p10 | ≥ 2.2 | 12 种子合并 |
| `EveryTheme_HasALargeSpace` | 每个主题包每层至少 1 间 ≥300㎡ | 6 主题 × 12 种子，72/72 层 | 强制 `FloorSpec.Theme` |
| `NonRectangular_AtLeast15Percent` | `Outline.Length ≥5` 且 `面积/包围盒 ≤0.92` 的占比 | ≥ 15% | 12 种子合并 |
| `FacilityPropSetHitRate_AtLeast80` | 房内道具名 ∈ 该设施 `PropSet` 的数量占比 | 每类设施 ≥80% | 12 种子，按设施分组 |
| `MaterialSlotCoverage_AtLeast14` | 6 主题 × 12 设施用到的材质集并集 | ≥14 / 19（目标 19） | 静态表 + 生成结果 |
| `FacilityAreaFits_AtLeast90` | 房间面积落在 `[0.85, 1.25]×AreaRange` | ≥90%（外廓裁剪允许 10% 溢出） | 12 种子 |
| `ThemeMix_KeepsThreeToFourLeads` | 每层主导设施 3–4 类，合计占比 | 70–85% | 6 主题 × 12 种子 |
| `DoorPolicy_ByFacility` | 非 Vault 房满足 `MinDoors` 且 ≥2 面墙 | 100% | 全种子 |
| `Vault_UnderEightPercent` | Vault 面积 / 本层面积；门数=2、焊死=1 | ≤8%；100% | 12 种子 |
| `Gallery_RunsAreSerial` | 每段管廊属于 2–4 段 run，段间有门 | 100% | 12 种子 |
| `ClearHeight_Respected` | 设施 `ClearHeight ≤ plan.Spec.ClearHeight` | 100% | 全种子 |
| `R3_Regressions` | 中位 90–130㎡；<56㎡ ≤10%；分档 40/35/20/5 | 沿用 `RoomScaleTests` 原阈值 | 12 种子 |
| `Batch1000_StillPasses` | 零穿模、全连通、单层 <3s | 沿用 M1 批测 | 1000 种子 |

为什么阈值是这些数：

- **p90/p10 ≥2.2**：现在中位 112–134、最大 ~600、最小 ~40，但 p90/p10 只有 ~1.8；2.2 对应"p10 ≈60㎡、p90 ≈320㎡"，正好是"变电/生活舱 vs 车间/装卸"的差距。
- **≥300㎡ 每主题 1 间**：地标不能靠中庭（R4 不生成中庭），只能靠设施体量；300 是"能看 60m"的下限（06 §5 一远一近）。
- **非矩形 ≥15%**：外廓截角/缺口已经能裁出 5–8 边房间，R3 实测这类房间占比约 20–30%，写 15% 是留回退余量；不写"L 形"是因为当前管线出不了凹多边形（§2.3）。
- **道具命中 ≥80%**：允许 20% 是"通用掩体"（纸箱、折叠椅）故意混入，保持走廊/房间的可读性一致；100% 会让每间房像展厅。
- **材质 ≥14/19**：R4 之前只有 10 个全局槽；14 是"至少把 9 套未接材质里的 4 套真正用上"的底线，19 是目标。

## 6. 与 R5（SlicePlanner）的接口

R5 负责"抽 2–3 层、每层 30–50% 楼板、合计 24,000–30,000㎡、封死区、连通性"。设施语法只提供只读查询，不参与切片决策。

| 接口 | 签名/字段 | R5 用它做什么 | 硬约束 |
|---|---|---|---|
| 主题 | `PickTheme(spec, seed)` | 预知每层主题，不重 roll | 主题 = f(buildingSeed, floorIndex)，与切片种子无关 |
| 地标 | `IsLandmark(room)`、`ThemeMix.LandmarkKind` | 地标必须整间进切片或整间封死 | 不允许"切一半车间" |
| 可封死 | `IsSealable(plan, room)` | 过滤封死候选 | 以下一律 false：地标、拉闸房（指定 ControlRoom）、撤离设施所在房、竖向核 4m 内、管廊 run 任一段、变电（供电链） |
| 必经 | `RunId(room) ≥0`、`IsService` | 封死后复检连通 | 管廊/门斗是捷径，封掉后必须保证还有 ≥2 条路径 |
| 面积估算 | `EstimateRooms(theme, area)` | 估算切片房间数，保出生区 4–6 个 | 误差 ≤15% |
| 楼层差异 | `ThemeSignature(theme)`（主题 + 净高 + 地面材质） | 相邻层至少 1 项不同（06 §5） | 3 层切片不允许 3 层同签名 |
| 切片边界 | `Room.Rect` / `Room.Outline` | 封死线吸附房间边界 | 不沿房间中线切 |

**R5 必须遵守的三条**

1. **封死区只能吃 `IsSealable=true` 的房间**，且封死后重跑 `EnsureGridConnectivity`。为什么：封掉管廊一段，可能把整层切成两个不连通 halves；设施层知道哪段是 run，R5 不该自己猜。
2. **关键设施不能落在封死区**：拉闸房、撤离点、变电、地标、竖向核。否则玩家进层"没电、没出口、没开关"，这局直接不可玩。
3. **主题随楼层变化，但不随对局变化**：同一 buildingSeed 下，40 层永远是数据中心层、12 层永远是行政层。为什么：R6 的竖向核与 R9 的标识要跨层一致；"每局不一样"由切片选层提供，不靠重 roll 主题，否则玩家学不会这栋楼。

**建议 A**：R5 先按"整房间"粒度封死（封死区 = 若干完整房间 + 走廊段），不做像素级切楼板。理由：现有 `FloorGeometry` 按房间矩形/Outline 出墙，半间房会留下没有墙的开口；若后续改 B 做"半层封死"，需要动 `FloorGeometry.CollectWallBoxes`、`CollectVoidBoxes` 与分块归属，成本高于 R5 本身。

## 7. 落地顺序与已知风险

**实现顺序（每步可独立回滚）**

1. 数据层：`FloorTheme`/`FacilityKind`/`FacilityProfile`/`ThemeMix` + 12 类静态表（不改行为，先让测试能读表）。
2. `PlanZones` + `SplitBias`：主题进 `FloorSpec`，象限配比进 `Zone`；只观察房间面积分布变化。
3. `Assign` + `RoomType` 映射：设施标签、材质槽、门目标；`RoomDoorPolicy` 改为读目标。
4. `Decorate`：道具集加权 + 新道具进 `PropCatalog.asset`；跑道具命中率。
5. 验收测试：§5 全表 + R3 回归。
6. 材质真实接线：`MaterialArtLibrary` 的 per-room 覆盖（最后由根代理接线，见 [08-materials.md](08-materials.md) §4）。

**风险（按"最可能被实现打脸"排序）**

| 风险 | 触发点 | 先验证什么 | 建议 A / 若改 B 要动什么 |
|---|---|---|---|
| 300–800㎡ + 6–8m 净高在塔身排不下 | 塔身层高 4.2m、净高 3.37m；BSP 长边硬上限 30m | 6 主题 × 12 种子，统计最大房间与净高分布 | 建议 A：车间/储罐/装卸只允许裙房/设备层，塔身主题上限压到 400㎡；若改 B 抬层高，需动 `FloorSpec`、楼板/吊顶、上层地板复用 |
| 材质槽没有 per-room 落地管线 | `MaterialArtLibrary` 只有 10 个全局槽，9 套未接 | 确认 `FloorMaterials` 能否按房间 key 分组 | 建议 A：R4 只写 `Room.Materials` 数据，渲染仍用全局槽，验收只测"数据覆盖 14/19"；若改 B 真按房间换材质，需动 `FloorMaterials`/`FloorChunkGeometry` 的材质分组与合批 |
| 非矩形只能出凸多边形 | `ClipRect` 只保留最大凸块 | 统计 `Outline.Length ≥5` 的真实占比 | 建议 A：验收写"非矩形 ≥15%"，L 形留 M1-A 第 4 步；若改 B，需先做合并叶子 + 斜墙隔断 |
| 2.4m 卷帘被 1.9 clamp 挡住 | `FloorGenerator` 门宽 clamp | 跑一遍装卸门宽 | 建议 A：先用 1.9m 双开；若改 B，新增 `DoorKind.Rollup` + 放宽 clamp + 门洞几何 |
