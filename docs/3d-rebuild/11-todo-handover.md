# 11. 交接待办清单（2026-11-27 由 lead 整理）

> 用途：给下一个接手的人/模型一份**待办总表**。仓库现状、已完成项、血泪教训、资源清单见 [07-handoff.md](07-handoff.md)；本文件只回答：**接下来做什么、卡在哪、怎么验收**。
>
> 状态快照：`main` @ `a9d20d5`（R9/R10/R11 已提交，工作区干净）；EditMode **207/207 已本机复跑验证**（`game/TestResults/t1-t13-r9-full-fix4-20261127.xml`，见 §10.1）。 2026-11-27 三轮共 29 项对局结构已拍板（06 §8.1 / §8.2 / §8.4）。

---

## 0. 一分钟结论

- 生成器主线 **R1–R11 全部完成**（R9/R10/R11 于 2026-11-27 收尾并提交 `a9d20d5`）；M1 只剩 **60fps/灯池实测（需高性能机）、灰盒补齐、分块 LOD+每块 NavMesh、多边形化第 3–4 步**，以及运行时侧的多楼层装载（T3）——**运行时目前仍是单层沙盒**。
- **不依赖拍板、现在就能做**：T3（运行时多楼层）→ T4（MapHud 规则）→ T5（材质槽接渲染）→ T9/T10/T12。R10/T13/R9（T1/T13/T2）、文档冲突（T6）与三轮拍板写回（T11）已于 2026-11-27 完成。
- **2026-11-27 已定稿**（06 §8.1 结构 5 项 + §8.2 细节 9 项 + §8.4 第三轮 15 项 = 三轮 29 项）：不设楼层总数、**每局固定 4 层**、单局 30 分钟、**最多 16 人 = 4–6 队（含 AI 队）× 每队 1–4 人、真人 ≥8 开局**、**两端出生对端撤离**、**每对相邻层 2 条单层楼梯 + 中心电梯**、取消深度=难度、TTK 按护甲 × 子弹、封死只做围挡+封锁带、AI ≤4、允许单/双排、楼层号取真实随机号、Boss 每层 20%、价值梯度保留（只影响价值场）、同 buildingSeed 跨局复用同楼、死胡同 ≤8%、撤离点按 02 §5.5 揭示。**剩余待定**（06 §8.3）：经济数值、首发武器清单、AI 难度分档、美术采购、200×160 确认、Boss 战斗形式/掉落、TTK 数值。
- **两个隐藏缺口**（文档没写进 R9/R10，但会卡住后续）：
  1. **运行时仍是单层沙盒**：`SlicePlanner`/`RaidPlan`/`VerticalLinkPlan` 目前只被测试使用，`FloorRuntime`/`FloorStreamingRuntime` 一次只建一层（`FloorStreamingRuntime.Initialize(int seed,...)`）。多楼层装载、竖向通路（楼梯/电梯/楼板开洞）、切层是 M1 收尾到 M2 之间必须补的。
  2. **`MapHud` 只有单层 `MapFog`+`MapRaster`**，且 `DrawExtractions` 无条件画全部撤离点——这是旧规则；02 §5.5 的新规则（楼层条 / 拉闸房永久可见 / 拉闸后才揭示撤离点）一条都没实现。

---

## 1. 待办总表

| # | 任务 | 依赖 | 等拍板 | 主要落点 | 验收 | 量级 |
|---|---|---|---|---|---|---|
| T1 | **R10 体检器接回归**：把 06 §10 指标变成 EditMode 测试 | 无 | 否 | 新 `LevelGen/LevelAudit.cs` + `tests/EditMode/LevelAuditTests.cs` | ✅ **2026-11-27 已完成**：`LevelAudit.cs` + `LevelAuditTests.cs`；走廊 30m / 侧出口 / 死路 / 房间挂接 / 环数 / 切片视线全部进全量回归 | 已完 |
| T2 | **R9 标识牌**：`SignageGenerator` + 场景渲染 | 无（素材已就绪） | 否 | 新 `LevelGen/Signage.cs`（或 `SignageGenerator.cs`）+ `FloorPlan.Signs` + 渲染层 | ✅ **2026-11-27 已完成**：`Signage.cs` + `SignageRenderer.cs` + `Resources/Signage/`；每层 ≥1 处 YOU ARE HERE，测试出图 `game/TestResults/r9-signage.png` 可读 | 已完 |
| T3 | **运行时多楼层**：`RaidPlan` 进运行时，竖向核与切层 | T1（护栏） | 否 | `FloorRuntime`/`FloorStreamingRuntime` + 新的楼层管理 | 固定 4 层同场景按真实标高叠放；2 条单层楼梯 + 中心电梯可跨层；切层后小地图换图 | 3–5 天 |
| T4 | **MapHud 按 02 §5.5 改**：楼层条 / 拉闸房 / 撤离点门控 | T3（楼层数据） | 否 | `UI/MapHud.cs` + `FloorPlan` 数据补齐 | 进层只给本端可用数量；远层免费点到层自动标出、近层拉闸后标出并全楼广播；对端免费点不显示、对端拉闸点显示不可用；楼梯/电梯无迷雾 | 1–2 天 |
| T5 | **材质槽接渲染**：主题材质 + 9 套未接材质 + per-room | T1 | 否 | `FloorMaterials` 扩槽 + `FloorChunkGeometry`/`FloorChunkManifest` 分槽 | 09 §5 MaterialSlotCoverage ≥14/19；机房/冷库/医疗一眼可辨 | 2–3 天 |
| T6 | **文档冲突修正**（只改文档） | 无 | 否 | 见 §7 | ✅ 2026-11-27 已完成（两轮 7 处 + 第三轮 06/02/00 再统一） | 已完 |
| T7 | **M1-B/C 60fps + 灯池实测** | 高性能机 | 否 | `M1FloorScene.Capture` + Profiler | 1080p 60fps（32,000㎡ 满 AI）；灯池不重复照亮 | 1 天（换机） |
| T8 | **M1-D 灰盒补齐**：道具命中率 89.6% → 更高 | 无 | 否 | `PropCatalog` / `tools/blender/gen_props.py` | 道具命中 ≥95%；无模型别名清零 | 1 天 |
| T9 | **M1-E 分块 LOD + 每块 NavMesh** | T3 | 否 | `FloorStreamer` / `FloorChunkPlan` | 60fps 下 LOD 生效；AI 可在已加载块寻路 | 2–3 天 |
| T10 | **多边形化第 3–4 步**：导航栅格 / 小地图 / 分块改用真实轮廓 | T1 | 否 | `FloorValidator` / `MapRaster` / `FloorChunkGrid` | 斜墙、L 形合并后仍全连通；地图与几何一致 | 3–5 天 |
| T11 | **06 §8 拍板落地**：三轮共 29 项已写回 06/00/02/03/07/09/11（2026-11-27） | — | 否 | 文档 + 相关实现 | ✅ 文档已完成；实现按拍板值（剩余待定见 06 §8.3） | 已完 |
| T12 | **M2 战斗原型**（3 武器 / ADS / 后坐力 / AI 视锥听觉） | T7 + TTK 数值（M2 实测） | 否（框架已定） | `Scripts/Gameplay/` | 手感可调参；AI 会压制与绕后 | 1–2 周 |
| T13 | **R11 竖向连接系统 + 两端撤离**：StairGraphPlan（每对相邻层 2 条独立单层楼梯 + 中心电梯）/ ExtractionPlan（每端 4 点、2 闸点房、电梯卡） | T1 | 否（第三轮已拍板） | 新 `LevelGen/StairGraphPlan.cs`、`LevelGen/ExtractionPlan.cs`；`FloorSpec`/`RaidPlan` 扩字段；废弃 `StairWestRect`/`StairEastRect`/`UseCores` | ✅ **2026-11-27 已完成**：`StairGraphPlan.cs` + `ExtractionPlan.cs`；`SlicePlanner` 固定 4 层 30–33k㎡；旧 `UseCores/StairWestRect/StairEastRect` 已删除 | 已完 |

> 顺序建议：T1 → T13 → T2 → T3 → T4 → T5 → T12；T7/T9 有高性能机时穿插。理由：先把 06 §10 变成测试护栏，再动会改生成器/场景的 R9 和材质。（T6 文档统一已于 2026-11-27 完成）

---

## 2. T1：R10 体检器接回归（建议第一个动）

**为什么先做**：R8 已经推翻了 06 §10 的环路口径（实测环数 49–69，是“房间数/12”的 5–6 倍），说明指标表本身有错；不先固化成测试，后面 R9/材质/多楼层改动没有护栏。

**落点**：
- 新 `game/Assets/_Project/Scripts/LevelGen/LevelAudit.cs`：纯函数、无 UI、可被测试与 Floor Lab 共用。
- 新 `game/Assets/tests/EditMode/LevelAuditTests.cs`：按 06 §10 逐行断言。

**迁移清单**（`tools/plan-proto/audit_floors.py` + 06 §10，注意别重写已有测试）：

| 06 §10 指标 | 现状 | 处理 |
|---|---|---|
| 切片可玩面积 30,000–33,000㎡ / 固定 4 层 | `SlicePlannerTests` 已同步为 F1/F4 21–22%、F2/F3 26–28% | 保留并纳入本轮全量回归 |
| 房间中位 90–130㎡、<56㎡ ≤10% | `RoomScaleTests` 已覆盖 | 复用 |
| 单门房 0（保险库 ≤8%） | `SingleDoorTests` 已覆盖 | 复用；Vault 还没生成 |
| 最长直线视线 ≤45m | `SightlineAudit` + `SightlineTests` 已有（30–38m） | 扩到多楼层/切片层 |
| 走廊直段 100% ≤30m | 无专门测试（R2 实测 30.5–31.9m） | **新增**，数据来自 `plan.Corridors` |
| 环路数 | `LoopInjectorTests` 有下限断言，口径与 06 冲突 | 改 06 §10 口径为 `≥ max(1, 房间数/12)`，测试对齐 |
| 出生—撤离图距离 ≥50% | `ExtractionSpreadTests` 已有 | 复用 |
| 死路走廊 / 房间挂接率 | 只在 Python 侧 | **新增**：`FloorValidator.Grid` BFS 数走廊端点 |
| 房间覆盖率 / 房间+走廊 ≥0.78 | `FloorCoverageTests` 已有 | 复用 |

**注意**：批量审计不要用 1m 栅格（R7 教训：1000 种子从 ~2 分钟拖到 596s）；用 `FloorValidator.CoarseCell = 2f`。

---

## 3. T2：R9 SignageGenerator（素材已就绪）

**已就绪**：`game/Assets/_Project/Art/Signage/` 35 张 PNG + `generate_signage.py` + `README.md`（尺寸/摆放高度/导入设置/两种渲染方案都在里面）。
**本轮已补实现**：`Signage.cs`、`SignageRenderer.cs` 与 `FloorPlan.Signs`；静态 Quad 和流式分块两条渲染路径均已接入，最终以本轮回归与截图结果为准。

**数据层**（新增 `FloorPlan.Signs`，建议 `List<SignPlacement>{ Center, Normal(朝向), Width, Height, Kind, Text/Asset }`）：
- 楼梯门旁：`S_Way_Stairs`（250×250，中心 1500mm）+ `S_Exit_Green`（600×300，门楣 2100–2400mm）
- 楼梯间/电梯厅：`S_Floor_40F…50F`（800×400，中心 1500–1800mm）——**楼层号取 SlicePlanner 的 worldFloor，不是 0..n**
- 电梯厅：`S_Elevator_Hall`（700×350，2200–2400mm）；轿厢内 `S_Elevator_Panel`（200×400，900–1200mm）
- 疏散梯门口旁：`S_Evac_Map`（600×450，1400–1600mm）——**R9 验收的“每层 ≥1 处 YOU ARE HERE”就落在这里**
- 房间门旁：`S_Room_5001…5012`（250×125，1500–1600mm），按 `Room.Facility`/主题映射 8 个部门
- 走廊转角：`S_Way_Arrow` / `S_Exit_ArrowLeft|Right`（300×300，1400–1600mm）

**渲染层**（两个方案，README 二选一）：
- **推荐 Quad + Unlit/Transparent**：Blend SrcAlpha OneMinusSrcAlpha、ZWrite Off、Queue Transparent、贴面偏移 1–2cm；可静态合批，无 Decal 渲染成本。
- URP Decal Projector：Albedo→Base Map、Alpha→Opacity，省 quad，但会有投影开销。
- 贴图不是运行时资源（不在 Resources），建议参照 `Editor/Tools/PropPipeline.cs` 的做法：**编辑器工具把 35 张图 + 尺寸生成 `SignageCatalog`（材质/prefab 列表）**，运行时按 `plan.Signs` 实例化。

**验收**：每层 ≥1 处 YOU ARE HERE；`M1FloorScene.Capture` 出图肉眼可读；同 seed 幂等；标识不压门洞（`FloorGeometry.CollectDoors` 会挖掉门洞区域）。

---

## 4. T3：运行时多楼层（文档未列，但 T4/M2 都卡在这里）

**现状**：
- `SlicePlanner.Generate(seed)` 现产出固定 4 层、30–33k㎡ 的 `RaidPlan`，携带 `StairGraphPlan` 与 `ExtractionPlan`；运行时仍只消费单层 `FloorPlan`，多楼层装载/切层属于 T3。
- `FloorStreamingRuntime.Initialize(int seed, band, prototype, corridorLevels, materials, viewer)` / `Regenerate(seed)` 只生成**一层**；场景 `Whitebox/FloorTest/FloorStreamingTest` 都是单层沙盒。

**要做的**：
1. `RaidPlan` 进运行时：每层一个根节点，按真实标高叠放（层高见 02 §2.1：结构板 250mm + 设备层 800mm + 吊顶 20mm，净高 3.37m）；同一时间只实例化当前层 + 相邻层（流式）。
2. 竖向通路：`VerticalLinkPlan` 已保证相邻层 3 条通路且坐标对齐，需要把楼梯/电梯/楼板开洞做成**可通行几何**（现在 `FloorGeometry` 只画壳，跨层没连通）。
3. 切层：进电梯/楼梯后卸载旧层、生成新层（里程碑要求“电梯 5s 内完成下一层生成”）；`RaidPlan` 层是可提前生成的。
4. 小地图换图：给 `MapHud` 传 `RaidPlan + 当前层 index`（T4 依赖）。

**验收**：同 seed 下固定 4 层按标高叠放；单层楼梯只在所属层对对位、电梯跨层对位；从任意层能走到任意相邻层；切层 5s 内完成；内存不泄漏（连续进出 10 层）。

---

## 5. T4：MapHud 按 02 §5.5 改（规则已定，不需要拍板）

**现状（代码实证）**：`MapHud` = `MapFog`（2m 格）+ `MapRaster`（4px/m，画房间/走廊/区域色）+ OnGUI（地图 + 雾 + `DrawExtractions` + `DrawUnvisitedZoneNames` + `DrawChrome`）。
**问题**：`DrawExtractions` 无条件画 `Plan.Extractions` 全部图标 + 类型 + 读条——旧规则“撤离点常显”。

**三件事（02 §5.5）**：
1. **楼层条**：只列本局固定 4 层，每层只标“该层是否有可用撤离点”（实心/空心），不给位置/类型；本层高亮；进层只给“本层 N 个可用撤离点”。
2. **拉闸房永久可见**：轮廓 + 门 + “拉闸房”标签常显，不过迷雾，全队共享，阵亡保留。
3. **撤离点门控**：进层只显示数量；进入拉闸房/完成拉闸后才标出被激活的具体撤离点（图标 + 位置 + 类型），拉闸后常显；拉闸全楼广播。

**数据缺口**（T4 的实活在这里）：
- `FloorPlan` 没有 `Signs/IsSealed/SealReason`（`SliceFloor` 有 `IsSealed`）；没有“拉闸房”标记（09 §6 要求 `ControlRoom` 设施 + `IsSealable=false`）。
- `MapHud.Initialize(plan, viewer)` 只吃单层 `FloorPlan`；需要改成吃 `RaidPlan + index`（T3）。
- 已探索/未探索的表现规则在 02 §5.5：已探索 = 真实平面（墙/门/走廊），未探索 = 只显示功能区名称，不做逐像素雾。

**验收**：拉闸前截图无撤离点图标、只有数量；拉闸后出现图标且切层不消失；拉闸房始终可见；M 开合不暂停。

---

## 6. T5：材质槽接渲染（09-facilities 的剩余部分）

**现状**：
- `FacilityGrammar` 给每类设施写 `FloorMaterial/WallMaterial`（数据侧齐了），09 还要求 `Room.Materials/WeldDoor/IsLandmark` 字段——**代码没有**。
- `FloorMaterials` 有 13 个槽（Wall/Floor/Ceiling/Column/Core/Stair/PropLarge/Medium/Small/SightBreak/LightPanel/Extraction/Spawn），`MaterialArtLibrary.Slots` 只映射了 10 个；**9 套材质没槽位**：TileFloor / VenueCarpet / EpoxyFloor / CleanPanel / ServerPanel / DiamondPlate / IndustrialPaint / MetalDoor / GlassPanel。
- `FloorChunkGeometry` 已经是“每个材质槽一个 MeshAccum → 一个 GameObject”，所以加主题材质**不用拆 submesh**：给 9 套材质各加一个槽（→ 19 槽），让 `FloorChunkManifest` 的墙/地 box 按房间的 `FloorMaterial/WallMaterial` 分到不同 accum 即可。

**验收**（09 §5）：MaterialSlotCoverage ≥14/19；ThemeMix 70–85%；截图里机房/冷库/医疗隔离一眼可辨；加槽前后 DrawCall/Chunk 数有对照。

---

## 7. T6：文档冲突修正（✅ 2026-11-27 已完成，7 处全部统一）

| # | 位置 | 现在写的 | 应对齐到 |
|---|---|---|---|
| 1 | `00-decisions.md` §10 | ~~「本队 2 个撤离点常显」~~ | ✅ 2026-11-27 已改为以 02 §5.5 为准 |
| 2 | `00-decisions.md` §12 表 | ~~一局 2–3 层 × 30–50% 楼板~~ | ✅ 2026-11-27 已改为现生成 3–4 层 × 20–32% |
| 3 | `06-raid-structure.md` §10 | ~~环路数 = 房间数/12 ±30%~~ | ✅ 2026-11-27 已改为 ≥ max(1, 房间数/12)，实测 5–6 倍 |
| 4 | `06-raid-structure.md` §9.2 #8 | ~~GraphDistance 校验“替换直线 FarThreshold”~~ | ✅ 2026-11-27 已标完成（R7 用图距离） |
| 5 | `02-level-design.md` §5 | ~~按队伍分配 2 个撤离点~~ | ✅ 2026-11-27 已改为层内 4–6 个共享撤离点 |
| 6 | `02-level-design.md` §4 | ~~死胡同房间 <15%~~ | ✅ 2026-11-27 改为 ≤8% |
| 7 | `09-facilities.md` §6 | ~~R5 描述“2–3 层 30–50%”~~ | ✅ 2026-11-27 已改为 3–4 层 × 20–32% |

顺手更新：04/10 号文档里“已知未完成”的状态；07 §10“R9–R10 未做”在 R9/R10 完成后改。

---

## 8. 拍板记录（2026-11-27，三轮共 29 项）

### 8.1 对局结构 5 项（第一轮）

| # | 项 | 决定 |
|---|---|---|
| 1 | 楼层总数 | **不设**；~~每局匹配现生成 3–4 层（3 层 55% / 4 层 45%）~~ → **第三轮改为固定 4 层**（§8.4 #1） |
| 2 | 单局时长 | **30 分钟** |
| 3 | TTK | 按**护甲等级 × 子弹等级矩阵**，具体数值 M2 实测后定 |
| 4 | 人数 | ~~最多 12 人 = 6 队 × ≤4 人~~ → **第三轮改为最多 16 人 = 4–6 队（含 AI 队）× 每队 1–4 人、真人 ≥8 开局**（§8.4 #2） |
| 5 | 深度=难度 | **取消**；楼层深度不再映射难度，深浅匹配池合并 |

### 8.2 细节口径 9 项（第二轮：用户答复“都按建议”）

| # | 项 | 决定 |
|---|---|---|
| 6 | 封死楼板表现 | v1 **只做施工围挡 + 封锁带**；断电/坍塌/水淹留作事件层。封死占比 = 每层 68–80% |
| 7 | AI 补位 | **AI ≤4 席**；真人 <8 不进局；等待上限 60s，之后带 AI 开局 |
| 8 | 队伍组合 | **允许单/双排**；整队匹配进同一局；~~12 席~~ → **16 席**先放整队，剩余 1–3 人队补位，仍不满用 AI |
| 9 | 楼层号与楼层条 | 楼层号用**随机真实号**（如 17F/23F/31F）；楼层条只列本局 **4** 层 |
| 10 | Boss 注入口径 | ~~每层独立 20% roll~~ → **第三轮改为每局 20% 一次、刷 F2 或 F3**；同局最多 1 个（§8.4 #11） |
| 11 | 竖向价值梯度 | **保留**：低层便宜 / 高层贵；只影响战利品价值场，不再=难度 |
| 12 | 同一栋楼跨局一致 | **保留**：同 buildingSeed 主题/结构固定，跨局复用同一栋楼 |
| 13 | 死胡同 | 死胡同房间 **≤8%**、死路走廊 **0** |
| 14 | 撤离点揭示 | **以 02 §5.5 为准**：进层只给本端可用数量；远层免费点到层自动标出、近层拉闸后才揭示并全楼广播；对端免费点不显示、对端拉闸点显示不可用（第三轮 §8.4 #5） |

### 8.3 剩余待定（06 §8.3）
经济数值与物价曲线；首发武器清单（slice 3 把 / 目标 13 把）；AI 难度分档（M4）；美术自建 vs 采购；200×160 最终确认；Boss 战斗形式/掉落/广播；TTK 具体数值；封死五种表现的具体占比。

---

### 8.4 第三轮 15 项（2026-11-27，全文见 06 §8.4）

| # | 项 | 决定 |
|---|---|---|
| 1 | 每局层数 | **固定 4 层**，起始层号随机；楼层条只列这 4 层 |
| 2 | 人数与队伍 | **≤16 人 / 4–6 队（含 AI 队）/ 单队 1–4 人 / 真人 ≥8 开局**；F1/F4 各 2–3 队；两端队差 ≤1、人数差 ≤2；AI ≤4 人 |
| 3 | 出生层 | **只在 F1/F4 出生**，中段两层不刷出生点 |
| 4 | 面积与形状 | 可玩 **30,000–33,000㎡**；F1/F4 各 21–22%、F2/F3 各 26–28% |
| 5 | 撤离点 | 每层 2 个（一局 8 个）；**每端只有 4 个**：近层 2 拉闸 / 远层 2 免费；两端不通用；对端免费点不显示、对端拉闸点显示不可用 |
| 6 | 撤离点约束 | 类型池按层分配；**电梯不做撤离点**；读条 20–30s；不得压对方出生区（80m/视线内）、不得与楼梯口重叠 |
| 7 | 最后 3 分钟 | 每个出生端各关 1 个免费点，每端仍保 ≥3 个可用 |
| 8 | 竖向连接 | 每对相邻层 **2 条独立单层楼梯**（中央带、彼此 ≥30m、双门楼梯间）+ **中心电梯** = 3 通路；每层 4 楼梯口；连续上下必须平面转移 |
| 9 | 电梯 | 中心区 1 部；全局 **2 张卡各 1 次**；召唤 5–10s + 门开暴露/楼层广播；卡可带出换钱 |
| 10 | 拉闸 | **2 个闸点房**（一在 F2、一在 F3）；拉下激活本端近层 2 点 + **全楼广播**；拉闸房永久可见 |
| 11 | Boss | **每局 20% 刷一次**，位置 **F2 或 F3**（避开电梯卡层）；同局最多 1 个 |
| 12 | 价值曲线 | **U 形**：F1 低 / F2 高 / F3 高 / F4 中低；只影响战利品价值场 |
| 13 | 标识 | **按局程序化生成**；疏散图/平面图牌**无迷雾**（整层真实平面）；玩家地图仍有探索迷雾 |
| 14 | 倒地救援 | 参考三角洲：倒地 60s 流血、可爬不可开枪；队友救援 5s（可打断）、救起 40% 血；单排 1 支自救针；全队倒地 = 灭队 |
| 15 | 卖点 | **每局都是全新楼层**：楼梯/电梯/撤离点/闸点/容器全随机，不靠记忆、靠标识与探索 |

## 9. 环境差异与受限（换机前先确认）

- **M1-B/C 60fps 与灯池实测**：i5-13400 那台无独显做不了；**9800X3D + RTX 4070 Ti SUPER + 32GB 那台可直接做**（2026-11-27 在本机实测为该配置）：跑 `M1FloorScene.Capture` + Profiler。
- **M1-E**：分块 LOD 与每块 NavMesh 未做（AI 寻路依赖，M2 前必须）。
- **M1-D**：道具命中 89.6%，零星灰盒。
- **多边形化第 3–4 步**：导航栅格 / 小地图 / 分块仍按房间矩形；斜墙隔断 + L 形合并未做。
- **美术脚本不在仓库**：`F:\ComfyUI\efw_gen_materials.py`、`efw_post_materials.py`（本机 ComfyUI 目录）；换机要带。
- **本机编辑器路径**：`D:/workspace/2022.3.62t18/Editor/Tuanjie.exe`（07 交接文档里的 `F:/Workspace/UNITY/...` 路径已过期；`game/ProjectSettings/ProjectVersion.txt` = 2022.3.62t18 / 1.11.1）。

---

## 10. 交接注意事项

### 10.1 常用命令

```bash
# 全量 EditMode（必须后台跑，约 5–20 分钟；不要 -quit，等 Tuanjie 进程退出再读结果）
"D:/workspace/2022.3.62t18/Editor/Tuanjie.exe" -batchmode -projectPath "D:/workspace/ESCAPE FROM WORK/game" \
  -runTests -testPlatform EditMode \
  -testResults "D:/workspace/ESCAPE FROM WORK/game/TestResults/editmode-YYYYMMDD.xml" \
  -logFile "D:/workspace/ESCAPE FROM WORK/game/TestResults/editmode-YYYYMMDD.log"

# 重建美术材质（改 Art/Textures 后）
"D:/workspace/2022.3.62t18/Editor/Tuanjie.exe" -batchmode -projectPath game \
  -executeMethod EscapeFromWork.EditorTools.MaterialArtLibrary.BuildAll -quit
```

- EditMode 基线：**207/207 通过**（2026-11-27 R9/R10/R11 收尾轮，结果 `game/TestResults/t1-t13-r9-full-fix4-20261127.xml`；纯测试约 18.5 分钟，本机无独显）。上一基线 148/148 见 `game/TestResults/editmode-t1-baseline-20261127.xml`。
- **结果判定**：XML 的 `testcasecount/result` 才是结论；Unity batch 在编辑器退出前就已写好 XML，可轮询文件出现。`-testResults` 尽量传绝对路径（相对路径会落到 `game/game/...`）。
- 测试结果 XML 里 `testcasecount` 才是用例数（`[Test]` 特性只有 95 个，参数化 `[TestCase]` 才会到 148）。

### 10.2 禁区与硬约束

- 不引入 shapely / scipy / matplotlib（几何全部手写；`tools/plan-proto/plan_proto.py` 里有可移植多边形算法）。
- 不做手工层、不做静态场景资产（全部程序生成）；不复用旧 2.5D 工程（`legacy-2.5d` 分支）。
- 不删 `FloorEnvelope` 的多边形管线退回矩形板。
- 生成器改动必须跑全量 EditMode 再提交。
- `AddCornerCuts` 保持停用（用户明确否决“房间里装挡板切角”）。

### 10.3 工具/上下文坑（本会话踩过）

- 本会话 `tools.compress` / `ops_bash` / `read_image` 曾被 auto-review 以 `CONTEXT_WINDOW_EXCEEDED` 拒绝；**不要反复重试 compress**，改用 `run_code` 内 node `fs`/`child_process`。
- `tools.read` 偶发 120s 超时：一次读小范围。
- 压缩 summary 用数组 `join`，不要用模板字符串（反引号会咬断）。
- Unity 单次调用常 >120s：跑测试/编译用后台任务。

---

## 11. 下次会话开场

1. 先读 [07-handoff.md](07-handoff.md)（现状 / 教训 / 禁区 / 资源）→ 本文件（待办）。
2. `git log --oneline -8` 对齐 HEAD（`a9d20d5`）；`git status` 应干净。R9/R10/R11 已在 `a9d20d5` 一次性提交（53 文件 / +4625 / −332）。
3. 先跑 §10.1 的 EditMode 确认 **207/207** 基线，再动手。
4. 下一步按 **T3（运行时多楼层）→ T4（MapHud）→ T5（材质槽）**；T7（60fps 实测）/T9（LOD+NavMesh）需要高性能机，T10（多边形化）与 T12（战斗原型）按需穿插。每完成一项在 §1 表里改状态并提交（commit message 沿用 `生成器(Rx): ...` / `文档: ...` 风格）。
5. 06 已定稿（§8.1/§8.2/§8.4 共 29 项）；剩余待定见 06 §8.3，未定前不要改「经济数值 / 首发武器 / AI 难度分档 / Boss 战斗形式」相关实现。
6. 本轮踩过的坑：改完 `.cs` 必须 touch（`ops_edit_file` 保留旧 mtime，Unity 会跑旧 DLL）；测试前先 `Stop-Process` 残留的 Tuanjie 进程，否则报 “Multiple Tuanjie instances cannot open the same project” 直接崩；日志常是无效 UTF8，用 PowerShell `Select-String` 取行。