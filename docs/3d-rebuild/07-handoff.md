# 交接文档：给下一个模型 / 下一个会话

> 目的：这份文档要让人**不读聊天记录也能接手**。如果你只读一页，读 §2 + §7 + §8。
> 生成时间：2026-11-26（2026-11-27 R9/R10/R11 收尾后更新）。仓库 `ESCAPE FROM WORK`，分支 `main`，HEAD = `a9d20d5`（R9/R10/R11 已提交，工作区干净）。

---

## 1. 先读什么（顺序）

1. 本文。
2. [06-raid-structure.md](06-raid-structure.md) —— **最新的对局结构设计决策**（**固定 4 层 / 两端出生对端撤离 / 最多 16 人 = 4–6 队（含 AI 队）/ 竖向连接系统 / 取消下坠与深度=难度**；2026-11-27 三轮定稿）。这是接下来所有代码工作的目标。
3. [02-level-design.md](02-level-design.md) —— 空间规则与房间类型学（部分条目已被 06 与调研修订，见 §6）。
4. [docs/research/00-overview.md](../research/00-overview.md) —— 三份一手调研的索引 + 20 条修订清单。数值依据都在这里。
5. [05-plan-prototype.md](05-plan-prototype.md) —— 2D 平面原型（Python）的生成顺序与结论。
6. [00-decisions.md](00-decisions.md) —— 旧决策记录。**§3/§4/§9 已被 06 替换**，其余仍有效。
7. [11-todo-handover.md](11-todo-handover.md) —— **待办总表**（2026-11-27 整理：优先级 / 依赖 / 是否等拍板 / 落点 / 验收）。

---

## 2. 现状一句话

团结 1.11.1 的 3D 搜打撤（PvPvE，程序生成巨构楼层）。**M0 骨架 + M1 单层可玩白盒已完成**（外廓多边形化、房间/墙/门/地/顶、道具、玩家、武器、雾图、分块流式）。
**R1 门数规则已完成**（2026-11-26，commit `0bd87f1`）：单门房 = 0、≥100㎡ 目标 3 门、近角门位、对门错位。
**R2 走廊断点已完成**（2026-11-26，本笔提交）：主廊每 28m 切站、门斗双错位门、并行服务廊 1.8–2.1m 净宽、`隔断墙` 道具方案整段删除；全量 EditMode **109/109 通过**（R1 的 103 项 + 新增 `SightlineTests` 6 项）。
**R3 房间尺度已完成**（2026-11-26，本笔提交）：面积统计改按真实轮廓、房间加用途类型、`RoomScaleTests` 4 项；全量 EditMode **113/113 通过**。
**R4 设施语法库已完成**（2026-11-27）：`FacilityGrammar.cs` 18 类设施 × 7 主题、主题驱动切分、按用途配家具（每房 2–14 件）、大厅墙垛 `HallBreaks`、跨房间门对挡视屏；Blender 参数化道具管线（21 个 prefab + `PropCatalog`，1068/1191 件道具用上真模型）。
**R5 切片结构已完成**（2026-11-27 改口径）：`SlicePlanner` **固定 4 层**、可玩面积 **30,000–33,000㎡**、F1/F4 21–22% / F2/F3 26–28%、封死区带世界内原因；楼层号取真实随机世界楼层（`FloorSpec.WorldFloor`，40–50）。
**R6 竖向核对齐已完成（已被 R11 取代）**：「全楼对齐核 + 西/东楼梯」模型已废弃，只保留电梯逐层对齐；楼梯改由 `StairGraphPlan` 逐对相邻层独立生成（`UseCores`/`StairWestRect`/`StairEastRect` 已从 `FloorSpec` 删除）。
**R7 出生区图距离校验已完成**（2026-11-27）：`FloorValidator` 支持任意格宽（`BuildGrid(plan, cell)` + `Grid.Unit`），出生区按 2m 粗栅格做"最远点贪心"重排，出生区间步行距离 ≥ 本层锚点直径 35%；全量 EditMode **148/148 通过**（R7 那轮 132）。
**R8 环路校验已完成**（2026-11-27）：`LoopInjector` 量化了环数（实测 49–69，是"房间数/12"下限的 5–6 倍），`Apply` 在达标时零改动，只保留"低于下限时补门"的保险。
**R9（标识牌）/ R10（体检器接回归）/ R11（竖向连接系统 + 两端撤离）已完成**（2026-11-27）：新增 `Signage.cs`/`SignageRenderer.cs`/`LevelAudit.cs`/`StairGraphPlan.cs`/`ExtractionPlan.cs` 与 4 个新测试文件；EditMode **207/207 全绿**（`game/TestResults/t1-t13-r9-full-fix4-20261127.xml`）。**但运行时仍是单层沙盒**，多楼层装载属 T3。

---

## 3. 环境与命令

| 项 | 值 |
|---|---|
| 仓库根 | `D:/workspace/ESCAPE FROM WORK` |
| Unity 工程 | `game/`（团结 1.11.1 / 2022.3.62t18） |
| 引擎 | `D:/workspace/2022.3.62t18/Editor/Tuanjie.exe` |
| git | 已在 PATH（DSH 自带 winuxcmd 的 `git.exe`，直接 `git status/log/diff` 即可）；提交要显式 `-c user.name=LiCloudDragonJ -c user.email=LiCloudDragonJ@users.noreply.github.com` |
| Python | `C:/Users/17613/.dsh/dsh-runtimes/dsh-primary-runtime/dependencies/python/python.exe`（numpy 2.3.5 + Pillow 12.3.0，**无** shapely/scipy/matplotlib） |
| 字体 | `C:/Windows/Fonts/msyh.ttc`、`msyhbd.ttc` |

> 本表已按本轮实际执行环境校正；换机时重新确认路径。

**编译检查**（约 20–40s，日志里搜 `error CS`）：

```
"D:/workspace/2022.3.62t18/Editor/Tuanjie.exe" -batchmode -quit   -projectPath "D:/workspace/ESCAPE FROM WORK/game"   -logFile "D:/workspace/ESCAPE FROM WORK/game/build.log"
```

**全量 EditMode 测试**（约 4–5 分钟，**不要加 -quit**）：

```
"D:/workspace/2022.3.62t18/Editor/Tuanjie.exe" -batchmode   -projectPath "D:/workspace/ESCAPE FROM WORK/game"   -runTests -testPlatform EditMode   -testResults "D:/workspace/ESCAPE FROM WORK/game/test-results.xml"   -logFile "D:/workspace/ESCAPE FROM WORK/game/test.log"
```

`Tuanjie.exe` 起的一批处理会在 1–2s 内把控制权还给 shell，编辑器仍在跑：必须等到 `Get-Process Tuanjie` 为空，再从 `test-results.xml` 里读 `//test-case[@result='Failed']`。

单类测试把 `-testPlatform EditMode` 后面换成 `-testFilter "EscapeFromWork.Tests.FloorEnvelopeTests"`。

**出场景与截图**：

```
-executeMethod EscapeFromWork.EditorTools.M1FloorScene.Build
-executeMethod EscapeFromWork.EditorTools.M1FloorScene.Capture
```

**2D 平面原型**（约 15–25s）：

```
cd "D:/workspace/ESCAPE FROM WORK/tools/plan-proto"
"<python>" plan_proto.py --out ./out        # -> out/plan-proto-*.png, out/large/*.png
"<python>" audit_floors.py                  # 体检：房间/门/视线/环
"<python>" design_sketches.py               # -> out/design-sketches.png（四格规则示意）
```

**git push 会间歇失败**（`schannel: failed to receive handshake, SSL/TLS connection failed`）。不要以为是网络断了，后台重试 20+ 次通常能成功；`git ls-remote origin main` 可确认。

---

## 4. 代码地图（`game/Assets/_Project/Scripts/`）

### 4.1 生成（核心，改这里）

| 文件 | 行数 | 职责 | 关键入口 |
|---|---|---|---|
| `LevelGen/FloorGenerator.cs` | 1500 | **主生成器**：核心筒 / 楼梯 / 外廓 / 房间 / 走廊 / 门 / 道具 / 出生区 / 撤离点 | `Build(FloorSpec, seed) → FloorPlan`；`AddRoomClipped`、`Emit`、`EnsureExtractionSpread`、`AddSpawnZones`、`AddExtractions`、`SpawnCandidateInside`、`PullInsideFloor`；**`AddCornerCuts` 已被注释停用**（用户否决房间内切角） |
| `LevelGen/FloorEnvelope.cs` | 530 | 外廓多边形化：45° 截角 + 边缺口、房间按外廓裁剪、耳切三角化 | `FloorEnvelope.Build`、`ClipRect`、`Triangulate`、`Contains`、`DistanceToEdge`；`EnvelopeShape{Outline,Pieces,Area,NotchCount,Bounds}` |
| `LevelGen/FloorTypes.cs` | 262 | 数据结构（R3 加 `RoomType` / `Room.Type` / `RoomShape.AreaOf`） | `Room{Id,Rect,Outline,...}`（Rect 只作包围盒）、`FloorPlan{Envelope,...}`、`BoxSpec` |
| `LevelGen/FloorGeometry.cs` | 1108 | 实体化：地板/吊顶/墙/门/柱、流式外壳、覆盖率 | `Build`、`MeshAccum.AddTriangle/AddPrism`、`AddPlateBoxes`、`CollectShellBoxes`、`CollectVoidBoxes`、`ClipRunToEnvelope`、`EmitLines` |
| `LevelGen/CorridorBreaks.cs` | 416 | **R2 走廊断点**：走廊切站 / 门斗双错位门 / 服务廊侧向开口 / 服务廊兜底 | `Apply(plan, spec)`、`LongestSegment(plan)`、`CountVestibules(plan)` |
| `LevelGen/SightlineAudit.cs` | 210 | **真实几何视线审计**（R2 验收证据） | `Run(plan)` → `Report{CorridorMax, RoomMax, GlobalMax, From, To, Samples}` |
| `LevelGen/FloorValidator.cs` | 271 | 校验与审计 | `FarThreshold(required, farthest)`（跨层推广的落点）、`AuditSpawns`、覆盖率/连通性断言 |
| `LevelGen/FacilityGrammar.cs` | 560 | **R4 设施语法**：18 类设施 × 7 主题、主题驱动停止概率/最小面积、按用途配家具、大厅墙垛、跨房间门对挡视屏 | `Assign`、`AddRoomProps`、`AddHallBreaks`、`AddDoorScreens`、`ThemeOf`、`ZoneStopPBias`、`Profiles` |
| `LevelGen/SlicePlanner.cs` | 430 | **R5/R11 垂直切片**：固定 4 层、30–33k㎡、F1/F4 21–22% / F2/F3 26–28%、封死区、出生区重排；调用 `StairGraphPlan` + `ExtractionPlan` | `Generate(seed)` → `RaidPlan{Floors, Links, StairGraph, Extraction, PlayableArea}` |
| `LevelGen/VerticalLinkPlan.cs` | 130 | **R6 电梯对齐（部分废弃）**：`ApplyTo` 已标 `[Obsolete]`，仅供单层 Lab 兜底；Raid 走 `StairGraphPlan` | `Default(spec)`、`ApplyTo(spec, floorIndex)`、`Fingerprint()` |
| `LevelGen/LoopInjector.cs` | 200 | **R8 环路**：E−V+C 环数统计 + 低于下限时才补门 | `TargetCycles(plan)`、`CycleRank(plan)`、`Apply(plan, spec)` |
| `LevelGen/CoverProps.cs` | 429 | 掩体/家具摆放（按房间类型与密度） | 摆件规则、掩体档位 |
| `LevelGen/PropCatalog.cs` | 102 | 道具表（**R4 起由 Blender 管线自动填**，41 个模型 / 55 条别名） | `Entry{Name,Prefab,Size,Offset,AddBoxCollider}`、`LoadOrCreateDefault` |
| `LevelGen/FloorChunk*.cs` | 481 | 分块：网格 / 计划 / 几何 / 流式 | `FloorChunkGeometry.BuildShell`、`FloorStreamer`、`FloorStreamingRuntime` |
| `LevelGen/FloorRuntime.cs` | 68 | 运行时装配 | |

**R9/R10/R11 新增文件（2026-11-27，全部有单测）**

| 文件 | 行数 | 职责 |
|---|---|---|
| `LevelGen/LevelAudit.cs` | 735 | **R10 体检器**：走廊直段合并后 ≤30m、20–30m 段必须有长墙侧出口、最大侧出口间距、死路走廊（Guo–Hall 骨架）、房间挂接（真实门 + 栅格同组件）、环数、切片视线 |
| `LevelGen/Signage.cs` | 181 | **R9 标识牌布局**：楼梯/电梯/楼层/房间/疏散图五类，确定性摆放，避开门洞与开口 |
| `LevelGen/SignageRenderer.cs` | 482 | **R9 渲染**：`Quad` + `Unlit/Transparent`，楼层/房号动态文字，疏散图按真实平面绘制 + YOU ARE HERE |
| `LevelGen/StairGraphPlan.cs` | 338 | **R11 竖向连接**：6 条独立单层楼梯（2 per pair）、16 个楼梯口（4/层，F1/F4 边界封死口补足）、电梯逐层对齐 |
| `LevelGen/ExtractionPlan.cs` | 413 | **R11 两端撤离**：每层 2 点共 8 点、每端 4 点不通用、2 个闸点房（F2/F3）、电梯交通卡、最后 3 分钟关 1 个免费点 |

### 4.2 玩法 / UI

| 文件 | 行数 | 职责 |
|---|---|---|
| `Gameplay/Player/FirstPersonController.cs` | 292 | 第一人称移动/跳跃/蹲 |
| `Gameplay/Player/PlayerMath.cs` | 55 | 纯函数（有单测） |
| `Gameplay/Weapons/HitscanWeapon.cs` + `ImpactFx.cs` | 132 | 命中扫描与弹着点 |
| `UI/MapHud.cs` | 476 | 雾图 / 小地图 / 撤离点常显（对应 00-decisions §10） |
| `UI/PlayTestControls.cs` | 149 | 试玩开关 |
| `Gameplay/PerfHud.cs` | 44 | 帧率/统计 |

### 4.3 编辑器工具

| 文件 | 行数 | 职责 |
|---|---|---|
| `Editor/M1FloorScene.cs` | 706 | 生成 FloorTest 场景 / 截图（俯视几何调试图、走廊/核心/出生点视角、柱子特写） |
| `Editor/FloorLabWindow.cs` | 382 | 楼层审图窗口（批量生成 + 浏览） |
| `Editor/M0Bootstrap.cs` / `M0Capture.cs` | 251 | M0 骨架与截图 |
| `Editor/Art/MaterialArtLibrary.cs` 等 | 223 | 程序化材质（31 套贴图集 → 10 个槽位） |
| `Editor/Tools/PropPipeline.cs` | 372 | **Blender 道具导入**：FBX → prefab（pivot 底心、单 BoxCollider、static batching）+ 写 `PropCatalog` |
| `Editor/Tools/SurveyCapture.cs` | 330 | 平面图 / 小地图 mock / 走廊特写出图 |

### 4.4 测试（`game/Assets/tests/`）

EditMode **25 个文件**：`FloorGeneratorTests`、`FloorGeneratorBatchTests`（多种子批量）、`FloorEnvelopeTests`、`FloorCoverageTests`、`WallLayoutTests`、`ExtractionSpreadTests`、`ChunkStreamingTests`、`CoverPropsTests`、`SightlineTests`（R2）、`RoomScaleTests`（R3）、`FacilityTests`（R4）、`SlicePlannerTests`（R5/R11）、`VerticalLinkPlanTests`（R6/R11）、`SpawnSpacingTests`（R7）、`LoopInjectorTests`（R8）、`MapFogTests`、`MeshWindingTests`、`ShellColliderTests`、`PlayerMathTests`、`PlayerFeelTests`、`SingleDoorTests`、`LevelAuditTests`（R10）、`SignageTests`（R9）、`StairGraphPlanTests`、`ExtractionPlanTests`（R11）；PlayMode：`WhiteboxSmokeTests`。
**合计 EditMode 207 项，全绿**（2026-11-27，R9/R10/R11 完成后实测：`game/TestResults/t1-t13-r9-full-fix4-20261127.xml`，纯测试约 18.5 分钟；历史上：148（R8 后）→ 132 → 129 → 124 → 113 → 109 → 103）。改生成器任何规则前先跑一遍全量当基线。

---

## 5. 已完成（可依赖）

- **M0**：工程、URP、玩家、武器、HUD、截图管线。
- **M1 单层白盒**：程序生成 200×160 单层（房间 100–130、走廊、门、道具 900+、撤离 5、出生区 4–6）；外廓多边形化（四角 45° 截角 + 北侧缺口 + 房间按外廓裁剪）；墙/地/顶按多边形出；分块流式加载；雾图；离开（撤离）点的自适应阈值；出生区/撤离设施保证在外廓内。
- **R1 门数规则**（`RoomDoorPolicy.cs`，commit `0bd87f1`）：栅格连通修补后统一规整房门——普通房 ≥2 门且不同墙、≥100㎡ 目标 3 门、>400㎡ 大厅长边两端各 1 门；门洞边缘距最近墙角 0.15–0.50m / 0.90–1.20m；对墙门切向错位 ≥1.10m；≤3m 缺口用 1.4m 短喉道 / 绕行前厅补第二道门。
- **R2 走廊断点**（`CorridorBreaks.cs` + `SightlineAudit.cs`）：主廊 28m 站距切 Normal/门斗，门斗双门错位 + 服务廊侧门；实测最长直线视线 30.4–38.0m、服务廊净宽 1.9m、每层开口 197–249 处。
- **2D 平面原型**（`tools/plan-proto`，Python）：验证了"结构动作传播 + 分区空间语法"，八套空间语法库、A/B 两组对照图、体检器 `audit_floors.py`、规则示意图 `design_sketches.py`。
- **三份一手调研 + 复核清单**：`docs/research/`。

---

## 6. 不能违反的设计约束（改代码前先看）

1. **单门房 = 0**。普通房 ≥2 门且不同面墙；≥100㎡ 目标 3 门；大厅 >400㎡ ≥3 门且 2 个在长边两端。唯一例外：保险库（≤8% 面积，2 门中 1 道焊死 + 进入报警）。**现状：已由 `RoomDoorPolicy`（commit `0bd87f1`）实现，`SingleDoorTests` 守着；保险库例外仍未做。**
2. **走廊不能一穿到底**。直段 ≤30m，每 20–30m 必须有侧向出口；断点用"交叉口 / 凹位·门斗 / 可穿行房间"，**不要砌墙**（砌墙只是把屠杀点前移）。**现状违规：主廊 160–200m 零断点。**
3. **房间尺度**：目标中位 90–130㎡；40% 56–113 / 35% 113–200 / 20% 200–400 / 5% >400；<56㎡ ≤10% 且只给储藏/卫生间/管井。**现状（R3，2026-11-26）：实测中位 100–123㎡、<56㎡ 7.0%、分档 43 / 34 / 14 / 2%，`RoomScaleTests` 守着。**
4. **一远一近**：每层至少一个 ≥60m 视线的大空间（中庭/涡轮厅）和一个 ≤8m 的紧凑空间。
5. **四条"不随机"的语法层**（06 号文档 §3）：竖向价值梯度、每层一脊一环、竖向核固定对位、每层 1–2 个地标。
6. **出生/撤离用图距离**（不是直线）：出生间距 ≥ 全图最长最短路的 35%；出生到最近撤离 ≥ 50%。
7. **不要手工层**：0 手工层，质量靠生成器约束 + 批量校验 + Floor Lab 审图。
8. **桌面尺度**：单层 200×160 = 32,000㎡ 是生成单位；**一局可玩面积必须落在 30,000–33,000㎡**（16 人 × 约 2,000㎡/人；2026-11-27 第三轮）。

---

## 7. 下一步任务（按依赖顺序，每项带验收）

> 前 4 项不依赖拍板项，可以现在就做；06 已定稿（§8.1 / §8.2），第 5 项之后也可直接做。

| # | 任务 | 落点 | 验收 |
|---|---|---|---|
| R1 | **门数规则**：废掉 `area<380→1 门`，按面积/类型出 2–3 门且不同面墙；门位距墙角 0.15–0.5m / 0.9–1.2m，对门错位 ≥1.10m | `FloorGenerator` 关门那段 + `FloorTypes.Room` 存 Door 列表 | 单门房 = 0（保险库除外）；`SingleDoorTests` 新增 |
| R2 | **走廊断点**：每 26–32m 插入合法断点，补并行服务廊 1.8–2.1m | `FloorGenerator` 走廊段 | 最长直线视线 ≤45m；`SightlineTests` |
| R3 | **房间尺度重分布**：中位 90–130㎡，按 40/35/20/5 分档 | `FloorGenerator` 的 BSP 终止条件 + `FloorSpec` 参数 | 中位数、<56㎡ 占比、分档直方图 |
| R4 | **设施语法库**（替换办公室）：12 类设施（机房/冷库/管廊/车间/中控/变电/储罐/医疗隔离/装卸/档案/洁净间/生活舱），每层 3–4 种主题 | 新 `FacilityGrammar.cs` + `PropCatalog` | 每层主题不同；房间尺寸方差显著上升 |
| R5 | **SlicePlanner**：随机 **3–4 层** × 每层 **20–32%** 楼板、面积 24–30k㎡、封死区、连通性（**第三轮改固定 4 层 / 21–28% / 30–33k㎡，待改**） | `LevelGen/SlicePlanner.cs` | 面积/连通/竖向通路 ≥3 且不同象限 |
| R6 | **VerticalLinkPlan**：竖向核跨层对齐 | 新 `VerticalLinkPlan.cs` + `FloorSpec` | 相邻层 ≥3 通路、坐标对齐误差 <0.5m |
| R7 | **图距离出生散布**：出生区按步行距离重排 | `FloorValidator`（`CoarseCell`/`CoarseDiameter`/`AuditSpawnSpacing`）+ `FloorGenerator.RepairSpawnSpread` | 出生区间距 ≥35%；`SpawnSpacingTests`。到撤离 ≥50% 由既有 `ExtractionSpreadTests` + `FarThreshold` 覆盖 |
| R8 | **环路**：环数（E−V+C）不低于"房间数/12"的下限；实测已是 5–6 倍 | `LevelGen/LoopInjector.cs` | 环数 ≥ 目标；达标时零改动 |
| R9 | **SignageGenerator**：疏散牌 / 楼层号 / 平面图牌 / 电梯面板 | 新 `Signage.cs` + 场景装饰 | 每层 ≥1 处"你在这里"牌；截图可读 |
| R10 | **体检器接回归**：把 `tools/plan-proto/audit_floors.py` 的指标搬进 EditMode 测试 | 新 `LevelAuditTests.cs` | 指标表（06 §10）进 CI |
| R11 | **竖向连接系统 + 两端撤离**（第三轮）：StairGraphPlan（每对相邻层 2 条独立单层楼梯 + 中心电梯、每层 4 楼梯口）/ ExtractionPlan（每端 4 点、2 闸点房、电梯卡） | 新 `StairGraphPlan.cs`、`ExtractionPlan.cs`；废弃 `StairWestRect`/`StairEastRect`/`UseCores` | 相邻层 3 通路、错开 ≥30m、跨层不对位；每端 4 撤离点两端不通用 |

> **R1–R11 全部已完成**：R1 103/103（commit `0bd87f1`）、R2 109、R3 113、R4 124、R5/R6 129、R7 132、R8 148、**R9/R10/R11 → 207/207**（2026-11-27，commit `a9d20d5`）。下一步是运行时侧：**T3（多楼层装载）→ T4（MapHud 规则）→ T5（材质槽）**，见 [11-todo-handover.md](11-todo-handover.md)。
> **对比图**：R2/R3 落地后已出 `r2-dollhouse.png`、`props-showcase*.png`、`plan-1/7*.png`；R7–R10 做完整层结构后重跑 `M1FloorScene.Capture` 出 after 图。

R2 已完成，落地方式（改过的东西，别再重复设计）：
- 删掉了 `CoverProps.AddSightBreaks` / `TryPlaceSightBreak` / `AlongOverlaps` / `DoorPoints`（26–32m `隔断墙` 道具方案作废）。
- 新增 `LevelGen/CorridorBreaks.cs`：`SplitNode` 里主廊（band ≥ MainCorridor）多并一条 `ServiceCorridor`（`FloorSpec.ServiceCorridor = 2.3f`，净宽 2.3-0.4 = 1.9m）；`Build` 在 `Emit` 之后、`AssignCorridorDoors` 之前调 `CorridorBreaks.Apply(plan, spec)`，把每条走廊按 28m 站距切成 Normal / 3m 门斗 / Normal，门斗两端横墙各开一扇门（主廊 1.4m、服务廊 0.8m，横向一左一右错开）；服务廊在门斗长墙上开 1.2m 侧门，`DropOrphanService` 丢掉没有共长边邻居的夹缝段，`EnsureServiceOpenings` 收尾复核每段服务廊至少一个开口。
- 所有走廊内部开口登记在 `FloorPlan.CorridorOpenings`，由 `FloorGeometry.CollectDoors` 统一挖洞（`CollectWallBoxes` 把每个 `Corridor.Rect` 的四条边都当实墙，不登记就是封死的墙）。
- 房间长边硬上限从 48/40 收到 **30m**（`SplitNode`），因为"30m 房间 + 31.8m 走廊"会拼出 49m 直线；被核心筒/楼梯切出来的 >32m × <8m 长条 cell 在 `Emit` 里转成走廊而不是房间。
- 视线证据：新增 `LevelGen/SightlineAudit.cs`（真实墙盒 + 眼高 1.62m + 0.25m 占用栅格 + 32 方向射线）+ `tests/EditMode/SightlineTests.cs` 6 项；`FloorValidator.HasLineOfSight` 仍是栅格近似，不当证据。
- 实测（8 种子）：最长走廊段 30.5–31.9m（>32m 的 0 段）、服务廊净宽全部 1.9m、每层开口 197–249 处、**最长直线视线 30.4–38.0m**（验收 ≤45m）；房间 111–126、中位 112–134㎡、<56㎡ 1–6 间、覆盖率 0.52–0.56。
- 回归：`FloorCoverageTests` 的裸地判定加了"门洞开启范围算可走"与 0.2m 采样容差（几何管线本身按 0.2m 栅格算实心，0.5m 撒点在矩形边界上必然错开一格）；`WallLayoutTests.Horizontal` 改成按墙厚判横竖（0.3m 短墙垛原先被误判成竖墙）；`ExtractionSpreadTests` 的失败是擦窗机只放两个，已改成四角各一个（`AddExtractions`）。

R3 已完成，落地方式（改过的东西，别再重复设计）：
- 面积统计改按**真实轮廓**：`RoomShape.AreaOf(room)`（`Outline` ≥3 点走 `FloorEnvelope.SignedArea` 鞋带公式，否则退回 `Rect.Area`）；`FloorPlan.RoomArea` 用它求和。包围盒在贴外廓/被核心筒裁过的房间上会虚大，用它统计会把小房间算成中房间。
- 房间用途：`RoomType { Normal, Storage, Toilet, Riser, Vault }`（`FloorTypes.cs`）+ `Room.Type` 字段；`FloorGenerator.AssignRoomTypes` 在 `RoomDoorPolicy.Apply` 之后跑——≥56㎡ 一律 Normal，<56㎡ 按 55/25/20 分 Storage/Toilet/Riser；`Vault` 留给 R4 的机制层，现在不生成。
- 碎片合并：`FloorGenerator.MergeAlignedPieces` 把被核心筒/楼梯切出来的 <56㎡ 碎块并进"边完全对齐且真的贴合"的邻块（`AlignedUnion` 要求共享一整条边，并校验并集不压核心筒/楼梯/其它碎块）。**注意**：探针显示现在 <56㎡ 的房间绝大多数是外廓裁剪出来的梯形（`ol=True`、边长 3–6m），不是核心筒碎片，所以这条规则实测几乎不改变分布——留着是为了以后换外廓/挪楼梯时不退化。
- **门数规则仍按包围盒面积**（`RoomDoorPolicy` 5 处 + `FloorGenerator` 的 `>= 80f` 第二门判断）：R1 的门数验收就是按 Rect 定的，改成轮廓面积会让贴外廓的大房间少一扇门（seed 1 room 26 掉到 2 门）。
- `stopP` 表**没动**（.06/.30/.62/.85/.95）。试过 220–400 档 .62→.68、120–220 档 .85→.88：中位飘到 134.3（seed 5）、seed 8 的 <56㎡ 从 8.6% 涨到 12.1%，已回退（代码里留了注释）。
- 新增 `tests/EditMode/RoomScaleTests.cs` 4 项：12 个种子合并统计（单层只有 100 多间房，逐种子卡分档参数一动就红）——中位 90–130㎡、<56㎡ 合计 ≤10% 且单层 ≤12%、分档 56–113 / 113–200 / 200–400 / 400+、Normal 必 ≥56㎡ 且服务间必 <56㎡。
- 实测（12 种子合并 1410 间）：中位 100–123㎡、<56㎡ **7.0%**、分档 **43.0 / 34.3 / 13.8 / 1.9%**（200–400 档仍偏少、56–113 档偏多，属可接受的第二版）。
- 覆盖率达到实测定线：房间覆盖率 300 种子最差 0.4474（seed 146）、1000 种子最差 0.4315（seed 305），测试底线定 0.44（R4 后 0.40）；真约束是"房间+走廊"联合覆盖率 >0.78（R3 时 0.80，R4 大厅墙垛/结构墙占面积后放宽）。

R4 已完成，落地方式（改过的东西，别再重复设计）：
- `LevelGen/FacilityGrammar.cs`：18 类设施 × 7 主题（Office/Admin/DataCenter/ColdChain/Manufacturing/Medical/Storage）。`ThemeOf` 用 `seed*5147 + zoneIndex*977 + 13` 决定象限主题；`ZoneStopPBias`（DataCenter 0.42 … Medical 1.35）与 `ZoneMinAreaScale`（DataCenter 1.6、Manufacturing 1.3、Medical 1.0）喂给 `SplitNode`，所以**停止概率按象限不同**（R4 之前全层一张表）。
- `Assign` 按设施面积区间算匹配代价（小于 MinArea 记全额差额、大于 MaxArea 记 0.35 倍），写 `Room.Facility`；`<56㎡` 时按 `RoomType` 覆盖成 Toilet/Riser/Storage，保证 R3 的"只有小房间能当服务间"仍成立。
- `AddRoomProps`：每房 `count = clamp(round(area/24), 2, 14)`，贴墙摆放，校验不压门（1.5m）、不压核心筒/楼梯/墙垛、互不重叠；**道具尺寸来自 `PropsOf(facility)`，名字与 PropCatalog 键一致**。
- `AddHallBreaks`：长边 >24m 且短边 ≥8m 的大厅，沿长轴每 8m 一道 0.4m 厚墙垛（交替两侧伸出，`stub = min(短边*0.55, 6.5)`），打断"30m 房间 + 28m 走廊 = 58m"的直线；登记 `FloorPlan.HallBreaks`，墙体生成与视线审计共用同一份数据。
- `AddDoorScreens`：全层收集门（room.Door + ExtraDoors + LoopDoors），找"相对边 + 切向差 ≤0.9m"的门对，在房间中点立 0.4m 厚墙屏（跨度、留边见代码）。seed 2 原来 52.4m 的"三间房上下叠、门都在一条线上"就是这样消掉的（修后 33.8m）。
- 道具管线：`tools/blender/gen_props.py`（纯 bpy，41 个道具，全部 ≤1500 tris）+ `Editor/Tools/PropPipeline.cs`（FBX → prefab：pivot 底心、1unit=1m、单 BoxCollider、static batching；写 `PropCatalog`）。材质走 `M_Prop_<slot>` 扁平材质（盒子 UV 上没有法线贴图，用 PBR 槽会拉成瓦楞纸）。
- 实测：设施分布每层 10–12 种、3 个主题；办公占比 28–39%；大设施中位 216–365㎡ vs 小设施 35㎡；seed 1 的 1191 件道具里 1068 件（89.6%）命中真模型。

R5 / R6 已完成，落地方式：
- `LevelGen/SlicePlanner.cs`：`Generate(seed)` 抽 3 层（55%）或 4 层（45%），起始层 `rng.Next(3, 52-floorCount)`，逐层 `FloorSpec.Default(seed*7919 + worldFloor*131)`；`Layout` 先按目标面积铺一条横带（`h = clamp(H*fraction, 24, H*0.32)`），再并上三个竖向核与 2.6m 连接短廊，最后迭代收敛开放比例（房间中心在带内但不完整时按包围盒会虚高，实测 35.3% → 5 轮缩到 ≤32%）。
- 封死区 = 主带南北两侧，原因从 `SealReason{Power, Construction, Collapse, Cordon, Flood}` 随机取；**结构上铺满整层**（`band + sealed` = 楼板），这是 `SlicePlannerTests` 的断言之一。
- `LevelGen/VerticalLinkPlan.cs`：一局只算一次核坐标（西梯/核心筒电梯/东梯），`ApplyTo(spec, floorIndex)` 写进 `FloorSpec{UseCores, CoreRect, StairWestRect, StairEastRect}`，`FloorGenerator.Build` 见到 `UseCores` 就直接采用（`ClampRect` 夹回楼层内），不再自己算——**跨层对齐是结构保证，不是巧合**；相邻层固定 3 条通路，坐标逐层完全相同（测试容差 0.01m，验收要求 <0.5m）。
- 实测：6 个种子层数 3–4、可玩面积 24,000–30,000㎡、每层 OpenFraction 0.19–0.33、相邻层 3 条通路且覆盖 3 个象限。

R7 已完成，落地方式（改过的东西，别再重复设计）：
- `FloorValidator` 支持任意格宽：`Grid.Unit` + `BuildGrid(plan, cell)`；`CoarseCell = 2f` 专给出生区散布用（1m 栅格逐候选 flood 曾把 1000 种子批测从 ~2 分钟拖到 596s）。`Flood`/`WalkDistanceAt`/`HasLineOfSight` 全部按 `g.Unit` 换算；粗栅格 `Stamp` 会按 `cell*0.3` 外扩，否则 1.9m 服务廊整格消失。
- 口径：`CoarseDiameter(plan, g)`（锚点 = 出生区 + ≤6 撤离候选 + 走廊中点，≤16 个，两两 flood 取最长最短路径）、`AuditSpawnSpacing(plan, g, diameter)`（`Required = diameter * 0.35f`，返回最近一对的 `MinDistance/A/B`）。
- 重排算法 `FloorGenerator.RepairSpawnSpread` 是**最远点贪心**：候选太多时按步长抽样到 ≤16 个，候选两两步行距离矩阵（每候选一次 flood），从"离第一个出生区最近的候选"起步，每轮取离已选集合最远的候选。**不要**退回"逐个替换最近一对"的写法：seed 79 在候选都挤在西侧时会卡在 16m（要求 105.7m），贪心版把它抬到 124m。
- `used` 必须与 `SpawnZones` 等长（兜底出生区记 -1），否则替换会错位换到别人的槽位。
- 实测：`R2Probe.SpawnBench` 120 个种子 `spacing_fail=0`、最差比值 1.02、`gen 172.6ms/种子`（不开修复 162.5ms，审计本身 3.5ms）；`SpawnSpacingTests` 3 项（8 种子 35% 断言 / 6 种子可达撤离 / 同种子确定性）。
- 到撤离的"≥50%"由既有 `ExtractionSpreadTests` + `FarThreshold`（图距离，本来就非直线）覆盖，没有另起一套。

R8 已完成，落地方式（结论与 R8 计划里写的不一样，按实测走）：
- **先量后做**：`R2Probe.Loops` 打 12 个种子，环数（E − V + C，节点 = 房间 + 走廊，边只取 `room.Door` + `room.ExtraDoors`）实测 **49–69**，而"房间数 / 12"只有 **9–11** —— 主廊 + 支廊 + 并行服务廊本来就把环数撑到 5–6 倍。R8 计划里"环数 ≈ 房间数/12，±30%"这个上限假设是错的：本生成器的房间图天生比它环通得多。
- 因此 `LevelGen/LoopInjector.cs` 的目标改成**下限**：`TargetCycles = max(1, rooms/12)`，`Apply` 只在 `rank < 0.7 * target` 时补门，正常分布下**零改动**（12 个种子实测 `added=0`）。
- 补门规则（真需要时）：找共享墙重叠 ≥2.4m、门距 ≥1.6m、且**当前图上已连通**的相邻房间（未连通时加的是桥，环数不涨），在共享墙中点开 1.0–1.6m 门，写进双方 `ExtraDoors` + `plan.LoopDoors`；`rng = seed*8647+211`。接线点在 `Build` 的 `AddDeadEndBypass` 之后、`EnsureConnectivity` 之前。
- 不读 `plan.LoopDoors` 做环数统计：它是同一扇门的重复登记，算两遍环数会翻倍。
- `LoopInjectorTests` 4 项 16 例：环数 ≥ 目标（6 种子）、已达标时 `Apply` 零改动且环数不变（6 种子）、同种子环数确定（3 种子）、每间房至少 1 扇门。全量 EditMode **148/148**。
- 真要再提"绕行感"，杠杆不在加门，而在 R4 的 `HallBreaks`/`AddDoorScreens` 与房间连通形状 —— 加门只会让环数从 5 倍变 6 倍，玩家感知不到。

---

## 8. 血泪教训（别重犯）

1. **盒子侧面绕序**：反了以后背面剔除只剩远侧内壁。存三角形前若 `Vector3.Dot(Vector3.Cross(b-a,c-a), n) < 0` 就交换 b/c。已有 `MeshWindingTests` 守着。
2. **`ClipHalfPlane` 保留侧**必须是 `dot(p,n) >= offset`（n 指向内部，`offset = Dot(corner,n) + size*0.7071`）。写反 → `Pieces` 全空 → 房间数 0。
3. **外廓缺口方向**：CCW 走法下北/西边缺口方向相反，必须换算 `side2: w - r.XMax, w - r.X`、`side3: h - r.YMax, h - r.Y`，否则缺口镜像到对面。
4. **`CleanOutline`**：耳切与 0.25m 采样都不能容忍重复点与共线点，建完外廓必须清理。
5. **出生区必须在外廓内**（`SpawnCandidateInside` / `PullInsideFloor`）：被截角裁过的房间，包围盒中心已经在楼外，会让相机悬空在楼外看天空。
6. **撤离阈值**：大层（280×280）用固定 238m 物理不可能满足，`FarThreshold = min(required, farthest*0.7)`。
7. **`AddCornerCuts` 保持停用**：用户明确否决"在房间里装挡板切角"（外壳仍是矩形，外部看不出差异）。形状只能来自外廓 + 结构动作。
8. **`M1FloorScene.Capture` 相机**：俯视要按 `max(H/2, W/2/aspect) + 8` 算 orthoSize，否则左右被裁；正下方看要直接 `Quaternion.Euler(90,0,0)`，`LookAt` 垂直向下会翻滚。
9. **不要以"直线距离"做室内空间约束**，室内一律用图距离。
10. **Unity 单次调用常 >120s**：跑测试/编译要用后台任务，不要同步等。

---

## 9. 禁区

- 不引入 shapely / scipy / matplotlib（环境没有，几何都是手写的；`tools/plan-proto/plan_proto.py` 里有现成的多边形裁剪/轮廓追踪/耳切）。
- 不做手工层、不做静态场景资产（全部程序生成）。
- 不复用旧 2.5D 工程（在 `legacy-2.5d` 分支）。
- 不删 `FloorEnvelope` 的多边形管线退回矩形板（外廓多边形化已交付并推送）。
- 不要在没跑全量 EditMode 的情况下提交生成器改动。

---

## 10. 已知未完成 / 已知缺陷

- **M1-B/C**：60fps 与灯池实测需要高性能机（当前机器不够）。
- **M1-D**：`PropCatalog` 已填 41 个 Blender 模型 / 55 条别名，seed 1 命中率 89.6%；仍有零星灰盒（无模型的别名）。
- **M1-E**：分块 LOD 与"每块 NavMesh"未做。
- **多边形化第 3–4 步**：导航栅格 / 小地图 / 分块仍按房间矩形；斜墙隔断 + L 形合并未做。
- **R1–R11 已完成**（2026-11-27 收尾轮，EditMode 207/207）：门数规则、走廊断点（最长视线 30–38m）、房间尺度（中位 100–123㎡、<56㎡ 7.0%）、设施语法库（18 类 × 7 主题）、垂直切片（固定 4 层 / 30–33k㎡ / F1F4 21–22%、F2F3 26–28%）、出生区图距离散布（≥35% 直径）、环数校验（49–69）、**体检器接回归**（走廊 30m / 侧出口 / 死路 / 房间挂接 / 切片视线）、**标识牌生成**（五类牌 + 动态楼层房号 + 真实疏散图）、**竖向连接 + 两端撤离**（6 条单层楼梯 / 16 楼梯口 / 8 撤离点 / 2 闸点房 / 电梯卡 / 最后 3 分钟关点）。
- **R9 证据**：`game/TestResults/r9-signage.png`（1400×700）已肉眼确认可读；`docs/3d-rebuild/screenshots/m1-floor3d-signage.png` **未生成**（`M1FloorScene.Capture` 未在本轮执行）。
- **R10/R11 口径说明**：免费撤离点的「对方出生区 80m」按**可步行距离**计算（200m 长条楼层上欧氏 80m 对四个出生区无解），且仍要求几何不可通视；`ReseatSpawnZones` 把 F1/F4 出生区收回开放带两端。
- **美术资源**：31 套 PBR（原 7 + 两批 24）、35 张标识牌、35 个 HUD 图标、15 张贴花、41 个 Blender 道具、5 张菜单图；`MaterialArtLibrary` 只接了 10 个槽位，**per-room 材质槽还没做**（设施已有 `FloorMaterial/WallMaterial` 数据，但没有落到几何上）。
- **视觉待打磨**（用户试玩后会提）：墙面竖向拉丝/噪点（WorldUv + 高频法线）、整体偏暗（环境光 0.42、无后处理）、道具仍是低模扁平材质、无 LOD1。
- **设计已定稿**（2026-11-27 三轮共 29 项，06 §8.1 / §8.2 / §8.4）：不设楼层总数、每局**固定 4 层**、30 分钟、**最多 16 人 = 4–6 队（含 AI 队）× 每队 1–4 人、真人 ≥8 开局**、取消深度=难度、TTK 护甲 × 子弹、封死只做施工围挡+封锁带、AI ≤4、允许单/双排、楼层号取真实随机号、Boss **每局 20% 刷 F2/F3**、**两端出生对端撤离**、**每对相邻层 2 条单层楼梯 + 中心电梯**、U 形价值、标识按局程序化且无迷雾、死胡同 ≤8%。
- **剩余待定**（06 §8.3）：经济数值与物价曲线、首发武器清单（3 把 / 13 把）、AI 难度分档、美术自建 vs 采购、200×160 最终确认、Boss 战斗形式/掉落/广播、TTK 具体数值。Boss 出现概率已定：每局 20% 一次、刷 F2 或 F3。

---

## 11. 工具与上下文限制（上一轮踩过）

- 本会话后期 `tools.compress` / `ops_bash` / `read_image` 曾被 auto-review 以 `CONTEXT_WINDOW_EXCEEDED` 全部拒绝，只能靠 `run_code` 内 node `fs` / `child_process` 读写文件、spawn python。压缩成功后恢复。若再遇到：**不要反复重试 compress**（reviewer 看的是全量历史，与 summary 长短无关），改用 `run_code` 直接干活。
- `tools.read` 偶发 120s 超时：一次只读一个小范围，或改用 node fs。
- 压缩 summary 不要用模板字符串拼长文本（会被反引号咬断，报 `Identifier cannot follow number`），用数组 `join`。

---

## 12. 美术资源（2026-11-26 由子代理批量生成）

全部是**离线生成的贴图/图标**，不依赖运行时，不进 Play 模式。生成脚本都留在仓库里可重跑。

| 目录 | 内容 | 规格 | 生成脚本 | 文档 |
|---|---|---|---|---|
| `game/Assets/_Project/Art/Textures/` | 31 套 PBR（首批 7 + 第二批 12 + 第三批 12） | 每套 `T_<Set>_{Albedo,Normal,Mask}.png` 1024²，Mask=RGBA（R metallic / G AO / B 255 / A smoothness） | `F:\ComfyUI\efw_gen_materials.py` + `efw_post_materials.py`（**本机 ComfyUI 目录，不在仓库里**：AI 出颜色颗粒 + 解析图案叠加） | `docs/3d-rebuild/08-materials.md` |
| `game/Assets/_Project/Art/Signage/` | 35 张标识牌（出口 / 楼层 40F–50F / 疏散图 / 电梯面板与厅牌 / 导向 / 门牌 5001–5012） | 512²–1024×512 RGBA，圆角透明边 | `Art/Signage/generate_signage.py` | 同目录 `README.md` + `_contact_sheet.png` |
| `game/Assets/_Project/Art/UI/` | 35 个 UI：5 准星（512²）、20 图标（256²）、10 地图标记 + 4 罗盘（256²/128²） | 纯白 #FFFFFF + 透明底，方便着色 | `Art/UI/generate_ui_icons.py` | 同目录 `README.md` + `_contact_sheet.png` |
| `game/Assets/_Project/Art/Decals/` | 15 张贴花（弹孔 / 渗水 / 霉斑 / 裂纹 / 漆皮 / 擦痕 / 咖啡圈 / 泥痕 / 锈水 / 油渍 / 污渍 / 烟熏 / 流挂） | 512² RGBA，alpha 形状程序化 | `tools/art/gen_decals.py` | 同目录 `README.md` + `_contact_sheet.png` |
| `game/Assets/_Project/Art/Props/` | **41 个 Blender 参数化道具**（柜/桌/椅/货架/机柜/储罐/病床/泵/冷风机/隔断…） | FBX + JSON，1unit=1m、pivot 底心、≤1500 tris | `tools/blender/gen_props.py` | `docs/3d-rebuild/10-art-pipeline.md` |
| `game/Assets/_Project/Art/Menu/` | 5 张菜单图（主菜单/加载/阵亡/两版 Logo） | 1920×1080 / 1600×600 | `tools/art/gen_menu.py` | 同目录 `README.md` |
| `tools/img2mesh/` | TripoSR 本地图生 3D 管线（跑通，2.3GB 显存 / ≈8s 一件 / 减到 1500 面） | 样例 `out/chair/*.fbx`、`out/plant/*.fbx` | `tools/img2mesh/run_tsr.py` | `docs/3d-rebuild/10-art-pipeline.md` |

- 材质库：`Editor/Art/MaterialArtLibrary.cs` 的 `Slots` 把 `FloorMaterials` 的 10 个槽位映射到材质集（R2 后 `Column → ConcreteWall`、`Stair → RubberStair`），`TilingFor()` 已补全 19 套的每米重复；改完材质跑菜单 `Escape From Work/Art/Build Material Library From Textures` 或 `-executeMethod EscapeFromWork.EditorTools.MaterialArtLibrary.BuildAll` 重建 `Materials/Art/*.mat`。
- **还没接进场景**：TileFloor / VenueCarpet / EpoxyFloor / CleanPanel / ServerPanel / DiamondPlate / IndustrialPaint / MetalDoor / GlassPanel 这 9 套没有槽位，等 R4 设施语法库（房间类型 → 材质集）再接。
- ComfyUI 环境：`F:\ComfyUI`（`main.py --listen 127.0.0.1 --port 8188 --disable-auto-launch`），SDXL 出颜色/颗粒、**规则图案由解析函数叠加**（AI 出不了严格等距可无缝的砖缝/孔阵），所以每套的接缝在数学上是完美的；细节取舍见 `08-materials.md` 与各 README 的"已知不足"。

---

## 13. 一句话交接

**单层生成器 R1–R11 全部完成**：外廓多边形化；门数规则；走廊 28m 站 + 门斗 + 并行服务廊（最长直线视线 30–38m）；面积按真实轮廓 + 房间用途（中位 100–123㎡）；设施语法库（18 类 × 7 主题 + 41 个 Blender 道具）；**固定 4 层、30–33k㎡、F1/F4 21–22%、F2/F3 26–28%**；**6 条独立单层楼梯 + 逐层电梯 + 每层 4 楼梯口**；**每层 2 撤离点共 8 点、每端 4 点不通用 + 2 闸点房 + 电梯卡 + 最后 3 分钟关点**；**五类标识牌 + 动态楼层房号 + 真实疏散图（YOU ARE HERE）**；**体检器进回归**（走廊 30m / 侧出口 / 死路 / 房间挂接 / 环数 / 切片视线）。EditMode **207/207 全绿**（`t1-t13-r9-full-fix4-20261127.xml`），已提交 `a9d20d5`。
接下来是运行时侧：T3（多楼层装载 + 楼层切换 + 楼梯可走）→ T4（MapHud 楼层条 / 拉闸房可见性 / 本端撤离点门控）→ T5（per-room 材质槽）；同时按用户试玩反馈打磨视觉（墙面拉丝/偏暗/道具材质）。

**地图/撤离可见性规则（2026-11-26 用户确认）记在 `02-level-design.md` §5.5**：小地图只画当前层；楼层条只标"哪层还有可用撤离点"；拉闸房没有迷雾、永久可见；进层只显示"本层 N 个可用撤离点"，**进入拉闸房之后**才标出被激活的具体撤离点（图标 + 位置 + 类型），拉闸全楼广播。`MapHud.cs` 目前只有 `MapFog` + 单层 `MapRaster`，楼层条 / 拉闸房可见性 / 撤离点门控都还没做。

**接下来做什么**：见 [11-todo-handover.md](11-todo-handover.md)（T1–T13 状态表；R10/R9/R11 已完成，剩运行时多楼层 T3、MapHud T4、材质槽 T5、性能/LOD/多边形化，以及三轮 29 项拍板记录与剩余待定 06 §8.3）。

