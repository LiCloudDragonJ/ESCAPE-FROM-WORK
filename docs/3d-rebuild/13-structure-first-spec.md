# 13 · 生成器骨架重构规格（区域先行 / 非轴对齐）

状态：**草案 v2，待拍板**  日期：2026-11-28  前置：00/02/05/06/11

> v2 变更：按用户 2026-11-28 的纠正，组织原则从"斜向主脊"改为**"先划大区域 → 每区各自生成 → 区域间合理连通"**。主脊 / 环不再是显式概念，而是区域边界走廊网的副产品。

## 0. 为什么要做

现状诊断（2026-11-28）：`FloorGenerator` 是轴对齐 BSP 先把整层切完，后面二十来个 pass 在给它擦屁股。走廊按 BSP 父节点面积分级，分区只是四象限贴纸，地标零实现。F3 平面图 = Mondrian，玩家找不到主街，每个翼楼长得一样。

用户拍板的两个痛点：**房间太方正，不要轴对齐**；**走廊布局不合理**。

用户给的组织原则：

1. **先划分大区域**——整层 200×160 先切成 5–9 个大块，切线带角度
2. **根据区域再生成**——每个区域按自己的功能 / 语法 / 朝向，填房间、布区内走廊、放区域地标建筑
3. **区域间合理连通**——区域边界上跑主走廊，区内走廊接到边界走廊，形成"边界主街 → 区内主廊 → 区内支廊"的层级和环路

这和原型（`tools/plan-proto/plan_proto.py`）的 A/B 结论一致：**"分区各用一套语法"才是打破同质化的关键**（非矩形 32% → 57%），整层转一个角度无效。

## 1. 目标 / 非目标

**目标（第一期）**
- 区域划分带角度，房间不再全是轴对齐矩形：20 个 seed 非矩形率 ≥ 50%，每层至少 2 个区域明显斜（|Yaw mod 90°| > 5°）
- 走廊三级由结构决定：**边界走廊**（4.8 / 3.6m，沿区域共享边）→ **区内主廊**（3.6m）→ **区内支廊**（2.7m）；不再由 BSP 父节点面积决定
- 每个区域有自己的功能主题（数据中心 / 制造 / 冷链 / 办公 / 医疗 / 仓储 / 行政）和语法；相邻区域主题不同
- 1–2 个区域带**区域地标建筑**（该区域的主功能大厅：服务器大厅 / 车间 / 冷库大厅 / 装卸大厅 / 中庭 / 入口大厅）
- 区域邻接图连通；每个区域 ≥ 2 条边界走廊（不出现死胡同区域）；区内每条走廊都接到边界走廊或地标环廊
- 规则类测试全部保持通过；三个审计不退化：Sightline ≤ 45m、LevelAudit 零违规、RoomScale 中位 90–130㎡
- 确定性不变：同 seed 同结果，只用 `System.Random`

**非目标（第一期不碰）**
- 切片（封死区渲染、带形变化）
- 楼梯位置逻辑（仍是 `StairGraphPlan` 当 keep-out）
- 撤离 / 闸点 / 电梯卡逻辑（`ExtractionPlan` 原样）
- 设施语法反过来塑形普通房间——只做地标大厅按主题出、普通房间贴标签
- 不引入任何外部依赖

**和设计文档的口径差**（需要知道）：00 §6 的"一脊一环"在这里落成"区域边界走廊网"，不再有显式的脊 / 环对象；"每层 1–2 地标"落成"1–2 个区域带主功能大厅"。

## 2. 目标数据模型（`FloorTypes.cs`）

现有 `Room.Outline` 已经是权威字段（墙 / 地 / 导航 / 小地图都以它为准）。走廊要从 `RectF` 升级成多边形。

| 类型 | 新增 | 保留 / 降级 |
|---|---|---|
| `Room` | `float Yaw`（rad，0 = 轴对齐） | `Outline` **必填**（轴对齐时 = `RectPoly`）；`Rect` 降级为 AABB |
| `Corridor` | `Vector2[] Outline`（CCW）、`float Yaw`、`Vector2 Dir`（单位长轴方向）、`float Length`、`float Width`、`CorridorRole Role`（Boundary / ZoneMain / ZoneSub / LandmarkRing） | `Rect` = AABB；`Vertical` 弃用（字段保留一版） |
| `Door` | `Vector2 Normal`（单位，从房间指向目标） | `Side` 保留，填 Normal 最近轴（老消费者过渡） |
| `Zone` | `Vector2[] Outline`（可非凸）、`List<Vector2[]> Pieces`（凸分解）、`float Yaw`、`FloorTheme Theme`、`string Grammar`、`bool HasLandmark`、`FacilityKind? LandmarkKind`、`float Area` | `Rect` = AABB；`Prototype` 弃用 |
| 新增 `ZoneAdjacency` | `int A, B`、`Vector2 Start, End`（共享边段）、`int CorridorIndex`、`bool Main` | |
| 新增 `Landmark` | `int ZoneIndex`、`FacilityKind Kind`、`Vector2[] Outline`、`RectF Rect`、`int RingCorridorIndex`（-1 无环） | |
| 新增 `FloorGrammar` | `MinRoomArea`、`MaxRoomArea`、`StopPTable`、`CorridorSpacing`、`CorridorWidth`、`YawJitter`、`AllowNonRect` | 每区域一份 |
| `FloorPlan` | `List<ZoneAdjacency> Adjacencies`、`List<Landmark> Landmarks` | |

**多边形口径**：`Zone.Outline` 可非凸，`Pieces` 必须凸分解。所有裁剪只对凸 `Pieces` 做，取交集最大块——和 `FloorEnvelope.ClipRect` 现在的行为一致。第一期不做凹多边形裁剪。

## 3. 工作包

依赖：**WP1 → WP2 → WP3 → WP4 → WP5 → WP6**。WP1 / WP2 可并行。每包独立提交、独立验收。

概念上顺序是"划区域 → 区域内生成 → 区域间连通"；**实现上边界走廊要在 WP4 之前先占位**（WP3 末尾），否则会把贴边的房间切烂。

### WP1 几何原语（新增 `LevelGen/PolyGeom.cs`，纯函数，零行为变化）

只许 `UnityEngine.Vector2` / `Mathf`（和 `Room.Outline` / `EnvelopeShape` / `RectF` 同口径，纯数学结构体）和 `RectF`；**不许** `UnityEngine.Random`、`GameObject`、`MonoBehaviour`、任何场景 API。单位米，多边形一律 CCW。（v2.1 修正：原文写 `System.Numerics`，与现有 LevelGen 全部用 `UnityEngine.Vector2` 不兼容。）

```
// 基本
static float Area(Vector2[] p);  static bool IsCcw(Vector2[] p);  static Vector2 Centroid(Vector2[] p)
static bool Contains(Vector2[] p, Vector2 q)
static Vector2[] RectPoly(RectF r)
static Vector2[] OrientedRectPoly(Vector2 center, Vector2 dir, float length, float width)
static Vector2[] Simplify(Vector2[] p, float tol)
static RectF BoundsOf(Vector2[] p)

// 裁剪 / 切分（区域划分的核心）
static Vector2[] ClipHalfPlane(Vector2[] subject, Vector2 a, Vector2 b)   // 保留左侧 (a→b)
static Vector2[] ClipConvex(Vector2[] subject, Vector2[] convexClip)
static Vector2[] IntersectConvex(Vector2[] a, Vector2[] b)
static (Vector2[] left, Vector2[] right) SplitByLine(Vector2[] p, Vector2 o, Vector2 dir)
static List<Vector2[]> ConvexDecompose(Vector2[] p)                       // 可转发 FloorEnvelope.Pieces 做法

// 邻接（区域连通 + 门策略的核心）
static bool ColinearOverlap(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2, float tol, out Vector2 s, out Vector2 e)
static SharedEdge? FindSharedEdge(Vector2[] p, Vector2[] q, float tol = 0.05f, float minLen = 1.0f)
   // SharedEdge { Vector2 Start, End; Vector2 Normal /*p→q*/; float Length }  取最长一条

// 反推
static OrientedRect FitOrientedRect(Vector2[] p)     // 最小面积外接矩形；{Center, Dir, Length, Width}
static Vector2 PrincipalDirection(Vector2[] p)       // PCA 主方向
static float DistanceToEdge(Vector2[] p, Vector2 q)
```

验收：新增 `tests/EditMode/PolyGeomTests.cs` ≥ 15 条（面积 / 包含 / 半平面裁剪 / 切分 / 共线重叠 / 旋转矩形往返 / 共享边法向 / PCA）。**不碰任何现有文件**。

### WP2 栅格与审计去轴对齐（`FloorValidator` / `LevelAudit` / `SightlineAudit`）

- `FloorValidator.BuildGrid`：房间按 `Outline` 栅格化（格心 `Contains`），走廊按 `Corridor.Outline`，门洞沿 `Door.Normal` 开
- `LevelAudit`：`CorridorVertical(c)` → `CorridorAxis(c)` 返回 `c.Dir`；along = `dot(p, Dir)`，across = `dot(p, Perp)`；`OnLongWall` 改为"门心到走廊长边 ≤ 0.75 且门法向 ⊥ Dir"
- `SightlineAudit`：已盒化，走廊采样点改用 `Outline`（车道沿 `Dir` / `Perp`）

**硬性要求：对当前轴对齐 plan 输出逐字节一致。** 第一个提交只能是 fixture（seed 1..20 的 `FloorReport` + `SightlineAudit.Report` + 栅格校验和 → `tests/EditMode/Fixtures/audit-seeds-1-20.json`），再改代码。新增 `AuditRegressionTests`。

### WP3 区域划分 + 边界走廊（新增 `LevelGen/ZonePartition.cs`、`LevelGen/ZoneConnect.cs`）

**3a 划分**（`ZonePartition.Split(floor, spec, rng, keepOuts)`）

```
regions = [楼板多边形]
N = 5 + rng.Next(5)                                   // 5–9 块
loop until count == N:
  取面积最大的 region（并列随机）
  切点 = 该 region 质心 + 高斯偏移（σ = 0.15 × √面积）
  切向 θ = 该 region 的 PrincipalDirection φ + rng ∈ (15°, 75°)   // 永远斜着切，不和主方向平行
  SplitByLine → 两半
  校验：两半面积都 ≥ MinZoneArea(1800)；长宽比 ≤ 3.5；两半各 ≥ 1 个凸块；
        keepOuts（核心筒 + 楼梯）不能被切开——含 keepOut 中心的那半必须整个包含它，否则重试
  失败 20 次后放宽长宽比到 4.5 再试 10 次，仍失败就换 region
```

分配：`Theme` 按 `FacilityGrammar.ThemeOf` 的分布抽，但**相邻区域主题不同**（重试）；`Grammar` 由主题映射（数据中心 → span / grid，制造 → strip / span，冷链 → strip / gallery，办公 → grid / cubicle / tilt±，医疗 → cubicle / grid，仓储 → strip / gallery，行政 → grid / hall）；`Yaw` = `PrincipalDirection(Outline)` + `Grammar.YawJitter`，保证 ≥ 2 块 |Yaw mod 90°| > 5°；`HasLandmark`：面积最大的 1–2 块，`LandmarkKind` 由主题定（数据中心 → ServerHall，制造 → Workshop，冷链 → ColdRoom，仓储 → LoadingDock，办公 → Atrium，行政 → Lobby，医疗 → IsolationRoom 大间）。

`GRAMMAR_LIBRARY`（移植原型 8 套，参数为初值，WP4 调）：

| 语法 | MinRoomArea | MaxRoomArea | CorridorSpacing | CorridorWidth | YawJitter |
|---|---|---|---|---|---|
| grid 阵列网格 | 40 | 90 | 16 | 2.7 | ±3° |
| cubicle 小隔间 | 25 | 60 | 12 | 2.7 | ±3° |
| gallery 管廊窄间 | 18 | 70 | 10 | 2.7 | ±2° |
| strip 长条仓区 | 120 | 400 | 24 | 3.6 | ±4° |
| span 无柱大跨 | 200 | 900 | 0 | — | ±5° |
| hall 展厅 | 150 | 500 | 0 | — | ±5° |
| tilt+14 斜网 | 40 | 130 | 16 | 2.7 | +14° |
| tilt−9 斜网 | 45 | 140 | 16 | 2.7 | −9° |

**3a′ 整局区域预算**（防"一局里四次数据中心"——`SlicePlanner` 协调，`FloorSpec` 下发）

区域性格不是单一主题，是 **主题 × 语法 × 朝向 × 密度** 的组合，再加 06 §5 的"相邻区域层高 / 地面材质 / 主色至少两项不同"。整局 4 层一起算账：

- **每层一个主主题**，4 层主主题互不相同（"服务器层""冷库层""车间层""行政层"——楼层本身成为记忆锚点）；一层内 ≥ 60% 面积是主主题，其余是 1–2 个副主题
- **同主题整局 ≤ 3 次**，同层 ≤ 2 次且不相邻
- **地标大厅整局唯一**：一局只有一个 ServerHall / 一个 Workshop……大厅本身成为"这局"的导航锚点（"服务器大厅在 F2"）
- **主题 → 语法多对多随机**：两个同主题区域必须用不同语法（数据中心一块 span 一块 grid），朝向各抽各的
- `Zone.Value`（第一版只算不消费）：主题基准值 × 距本端出生区步行距离，给后面战利品 / AI 密度用；高价值区域自然落在离出生端远的位置——这是"紧张"的空间来源，不是新鲜感

实现上：`SlicePlanner.Generate` 在调 4 次 `FloorGenerator.Generate` 之前先抽 `RaidThemePlan { PerFloorDominant[4], GlobalThemeBudget, LandmarkAssignment }`，塞进每个 `FloorSpec`；`ZonePartition.Split` 读预算而不是自己抽。

**3a″ 主题池扩容 + 区域状态**（m00216 决策：**buildingSeed 每局重抽**，楼不持久）

可学性从楼级挪到类型级：玩家跨局学的是"怎么读一个区"（光色 / 地面 / 道具剪影 / 价值档 / 危险源），不是"背一张图"。所以每个主题的**签名必须跨局稳定**，而主题的数量决定这个学习有多深。

- **主题 7 → 12**，全部从现有 18 种 `FacilityKind` 分组得来，不新增内容：Office 办公 / Admin 行政 / DataCenter 数据机房 / ColdChain 冷链 / Manufacturing 制造 / Medical 医疗 / Storage 仓储 / **Logistics 装卸物流**（LoadingDock，truck dock + 托盘 + 宽门）/ **Utilities 变电储罐**（Substation + TankRoom，危险源 + 电力事件相关）/ **Control 中控安防**（ControlRoom + ServerRoom + Archive，"大脑"）/ **Infrastructure 管廊管井**（PipeGallery + Riser，窄、竖、闷）/ **Living 生活区**（LivingPod + Toilet）。每个主题一张 `ThemeProfile`：主色 / 光温 / 地面墙裙材质（T5 已有 21 套设施面层）/ 价值档 / 道具密度 / 合法语法集 / 地标种类 / 签名道具
- **区域状态**（同几何换皮，最便宜的多样度）：每区 roll 一个 `ZoneState ∈ { Normal, PoweredDown（应急灯、黑、NeedsPower 撤离失效）, UnderRenovation（围挡 + 脚手架 + 封锁带，复用 SealedArea 道具）}`；第一版只做 Normal + PoweredDown + UnderRenovation
- 账：12 主题 × 3 状态 × 每主题 3–4 套合法语法 ≈ **每个区域槽 100+ 种观感**；一局 ~24 槽、每主题 ≤3 → 每局用 **≥8 个不同主题 = 12 选 8 的子集每局不同**——"这局没有医疗区"本身就是紧张（没药），是经济层面的新鲜感，不只是视觉
- 代价说明：新主题的光 / 材质变体是主要成本，但 5 个新主题的设施面层在 T5 已有，分组即可；状态只碰光照与道具摆放，不碰几何
- `RaidThemePlan` 随 buildingSeed 每局重抽；`TowerRuntime.Initialize` 的种子来源改为对局种子（一行改动，工作包不动）

**3b 边界走廊**（`ZoneConnect.Carve(zones, rng)`）

```
邻接：每对 (i<j) 用 FindSharedEdge 找共享边 ≥ 6m → ZoneAdjacency
区域图介数：节点 = 区域，边 = 邻接；按最短路径经过次数排序，前 40% 的邻接 Main=true (4.8m)，其余 3.6m
边界走廊：沿共享边段的 OrientedRect，居中，长 = min(L − 2, 36)，宽 = w，Role = Boundary
两侧区域各让出 w/2：用走廊内侧边线 ClipHalfPlane 从 Zone.Outline 上切掉一条
交汇：边界走廊端点 3m 内有另一条边界走廊 → 端部外延 w/2 自然搭接
校验：区域图连通（构造上应成立，断言）；每块 ≥ 2 条边界走廊贴着，不够就延长某条
```

验收：新增 `ZonePartitionTests`——区域数 5–9、面积和 = 楼板面积、无重叠、全在 `FloorEnvelope` 内；相邻主题不同；≥ 2 块明显斜；核心筒 / 楼梯不被切；`ZoneConnectTests`——邻接图连通、每块 ≥ 2 条边界走廊、边界走廊不压 keepOut；确定性。`FloorLabWindow` 分层画区域（按主题色）/ 边界走廊（粗）/ 邻接线——**第一张能看出"几个翼楼"的图**。

### WP4 区域内生成（新增 `LevelGen/ZoneFill.cs`；`FloorGenerator.SplitNode` 退役）

对每个区域（`Outline` 已扣除边界走廊）：

```
1 地标：若 HasLandmark，按 LandmarkKind 出大厅 OrientedRect（ServerHall 24×36 / Workshop 20×40 / ColdRoom 18×24 /
        LoadingDock 16×32 / Atrium 18–28 方 / Lobby 12×20），放在区域内部、一条长边贴最近边界走廊的内侧
        （大厅直接开口到主街）；大厅短边 > 20 时加 3.6m 环廊 (Role = LandmarkRing)；登记 Landmark
2 局部坐标：区域 Outline 旋转 −Yaw
3 区内走廊：按 Grammar.CorridorSpacing 沿局部轴交替方向切，宽 = Grammar.CorridorWidth（区域面积 > 1500 用 3.6 否则 2.7）
        每条贯穿整个区域（ClipConvex 到 Pieces），所以两端都碰到区域边界 → 开口到边界走廊
        Role = ZoneMain (3.6) / ZoneSub (2.7)
4 叶子 → 房间：Outline = 裁剪后多边形；Rect = AABB；Yaw = 区域 Yaw，被斜切的叶用 FitOrientedRect.Dir
        叶子 < MinRoomArea 并入邻叶；span / hall 语法不出区内走廊（CorridorSpacing 0），整块是大房间
5 区内走廊必须与某条边界走廊或环廊共享 ≥ 2m 边，否则整条并入邻叶（不允许孤岛走廊）
```

stopP 表按 `FloorGrammar`；`ZoneStopPBias` / `ZoneMinAreaScale` 那套按 Theme 的系数删掉。`SlicePlanner` / `ExtractionPlan` 里用 `Rect` 的地方第一期不改，AABB 近式可接受。

验收：20 seed 非矩形率 ≥ 50%；`RoomScaleTests` / `FloorGeneratorTests`（不重叠 / 不出界 / 填满）全过；每条区内走廊两端都碰到边界走廊（新测试）；`AddCornerCuts` 仍停用。

### WP5 门 + 走廊段（`RoomDoorPolicy` / `CorridorBreaks`）

- `RoomDoorPolicy.BuildCandidates`：从"4 向 Rect 邻接"改为"共享边邻接"——对每对 (room, corridor|room) 调 `FindSharedEdge`，门中心在共享边上按角部规则放（角距 ∈ [0.15, 0.5] ∪ [0.9, 1.2] 沿边切向）。`Door.Normal` = 共享边法向。`Place` / `StaggerOpposite` / `MeetsPolicy` 在切向坐标下原样复用——**角距、错位 ≥ 1.10、双面门、大厅端部规则一个都不改**。`NeedsConnector` 喉道改 `OrientedRect`。
- 区内走廊 ↔ 边界走廊 / 环廊：共享边 ≥ 2m 处开 3.0m `CorridorOpenings`（无门，平接）。
- `CorridorBreaks`：沿 `Dir` 每 28m 切段 + 3m 门斗，门斗 = `OrientedRect`。**`AddCrossDoor` 的 `(FloorToInt(line/3) + FloorToInt(across)) & 1` 取侧规则整段删掉**，改为"横墙门沿切向按站号奇偶交替 ±(带宽 × 0.25)"——无 seed 补丁，靠 WP2 审计回归验证。
- `AddHallBreaks` / `AddDoorScreens`：保留，但只在该房间审计报违规时触发。

验收：`SingleDoorTests` / `SightlineTests` / `LoopInjectorTests` / `ExtractionSpreadTests` / `LevelAuditTests` 全过；`OverlongCorridors` = 0；环路数 ≥ 区域数（新测试）。

### WP6 几何生成 + 清理（`FloorGeometry` / `FloorChunkPlan` / 删补丁）

- `FloorGeometry.CollectWallBoxes`：房间按 `Outline` 每条边发 `BoxSpec`（yaw = atan2）——复用 `CollectShellBoxes` 对外廓的做法。墙线 horizontal/vertical 字典改"共线分组"（方向 + 偏移聚类，tol 0.05），保住合并和 `CutDoor` 门楣。`CollectVoidBoxes` 用 `Outline` 栅格化。
- `FloorChunkPlan.Clip`：已支持带 yaw 的 `BoxSpec`，验证即可。
- 清理评估（每项独立提交，删了必须 220 全过）：`CorridorBreaks` 奇偶规则（WP5 已删）、`AddCornerCuts` 死代码、`FloorGenerator.SplitNode` / `CorridorWidth(parent)` / `AddZones` 四象限 / `ZoneOrder`、`FillWallSlivers`、`LoopInjector`（改成下限检查）、`AddHallBreaks` / `AddDoorScreens`（看审计还报不报）。

验收：`WallLayoutTests` / `ChunkStreamingTests` / `MeshWindingTests` / `FloorCoverageTests` 过；`TowerCapture` 出 4 层图；220 全过；**性能**：单层生成 ≤ 现状 × 1.5，`SightlineAudit` ≤ 现状 × 2。

## 4. 每包验收流程

1. 工人自测：全量 EditMode（`Tuanjie.exe -batchmode -projectPath game -runTests -testPlatform EditMode -testResults <绝对路径> -logFile <路径>`，不带 `-quit`，约 10 分钟），报 XML 的 `testcasecount` / `result`
2. 架构验收（我）：读 diff；重跑 EditMode；`FloorLab` 出 seed 1 / 7 / 42 三张平面图；跑 20 seed 的 `LevelAudit` + `SightlineAudit` + `RoomScale` + 非矩形率统计
3. **打回标准**：任一规则类测试挂；任一审计退化；非矩形率不达标；区域图不连通 / 有死胡同区域；用了 `UnityEngine.Random`；动了 §5 禁区；改了 fixture
4. 提交风格沿用：`生成器(WPx): ...`

## 5. 禁区

- 不引入外部依赖（C# 只许 `System.Numerics` / Unity 数学）
- 不删 `FloorEnvelope` 多边形管线
- `AddCornerCuts` 保持停用（用户 2026-11-26 否决）
- 第一期不动切片 / 楼梯 / 撤离逻辑
- 不用 `UnityEngine.Random`
- 改完 `.cs` 必须 touch，否则 Unity 跑旧 DLL
- 中文 Markdown 不用 PowerShell 改写

## 6. 风险

| 风险 | 对策 |
|---|---|
| 凹多边形裁剪 | 第一期只裁凸 `Pieces` 取最大块；`Zone.Outline` 允许非凸只是数据 |
| 区域被切得太碎 / 太扁 | MinZoneArea 1800、长宽比 ≤ 3.5、面积最大者优先切；N 目标 5–9 |
| 核心筒 / 楼梯骑在切线上 | 含 keepOut 中心的那半必须整个包含它，否则重试 |
| 边界走廊在交汇处重叠 | 端部外延 w/2 搭接，重叠区是路口，`LevelAudit` 的 run 合并口径要跟上 |
| 斜墙与门楣 / 墙体合并 | `FloorGeometry` 线字典改共线聚类，WP6 最大工作量；先做 `WallLayoutTests` fixture |
| `DoorSide` 老消费者 | `Side` 保留一版（填最近轴） |
| 性能 | `FindSharedEdge` O(n²) 对 ≈ 4.5k 对，可接受；`SightlineAudit` 盒子数约 ×4 |
| 工人模型能力 | WP1 / WP3 / WP6 几何重，再切子包 |

## 7. 第一期之后的 backlog

切片做实（封死区渲染 + 带形 L / U / 双叶 + 楼梯放进开放区）→ 设施语法反过来塑形普通房间 → 地标大厅层高 / 光色 / 声音 → Boss 层。
