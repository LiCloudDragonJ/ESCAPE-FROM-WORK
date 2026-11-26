# 交接文档：给下一个模型 / 下一个会话

> 目的：这份文档要让人**不读聊天记录也能接手**。如果你只读一页，读 §2 + §7 + §8。
> 生成时间：2026-11-26。仓库 `ESCAPE FROM WORK`，分支 `main`，HEAD = `5e2a81c` 之后的一笔（含本文）。

---

## 1. 先读什么（顺序）

1. 本文。
2. [06-raid-structure.md](06-raid-structure.md) —— **最新的对局结构设计决策**（随机垂直切片 / 25 分钟 / 取消下坠）。这是接下来所有代码工作的目标。
3. [02-level-design.md](02-level-design.md) —— 空间规则与房间类型学（部分条目已被 06 与调研修订，见 §6）。
4. [docs/research/00-overview.md](../research/00-overview.md) —— 三份一手调研的索引 + 20 条修订清单。数值依据都在这里。
5. [05-plan-prototype.md](05-plan-prototype.md) —— 2D 平面原型（Python）的生成顺序与结论。
6. [00-decisions.md](00-decisions.md) —— 旧决策记录。**§3/§4/§9 已被 06 替换**，其余仍有效。

---

## 2. 现状一句话

团结 1.11.1 的 3D 搜打撤（PvPvE，程序生成巨构楼层）。**M0 骨架 + M1 单层可玩白盒已完成**（外廓多边形化、房间/墙/门/地/顶、道具、玩家、武器、雾图、分块流式）。
**R1 门数规则已完成**（2026-11-26，commit `0bd87f1`）：单门房 = 0、≥100㎡ 目标 3 门、近角门位、对门错位；全量 EditMode **103/103 通过**（398s，含新增 `SingleDoorTests` 4 例）。
**R2 走廊断点、R3 房间尺度尚未开工**；R4 及之后的 2–3 层切片结构也没开始。

---

## 3. 环境与命令

| 项 | 值 |
|---|---|
| 仓库根 | `D:/workspace/ESCAPE FROM WORK` |
| Unity 工程 | `game/`（团结 1.11.1 / 2022.3.62t18） |
| 引擎 | `D:/workspace/2022.3.62t18/Editor/Tuanjie.exe` |
| Python | `C:/Users/comme/.dsh/dsh-runtimes/dsh-primary-runtime/dependencies/python/python.exe`（numpy 2.3.5 + Pillow 12.3.0，**无** shapely/scipy/matplotlib） |
| 字体 | `C:/Windows/Fonts/msyh.ttc`、`msyhbd.ttc` |

**编译检查**（约 20–40s，日志里搜 `error CS`）：

```
"D:/workspace/2022.3.62t18/Editor/Tuanjie.exe" -batchmode -quit   -projectPath "D:/workspace/ESCAPE FROM WORK/game"   -logFile "D:/workspace/ESCAPE FROM WORK/game/build.log"
```

**全量 EditMode 测试**（约 3.5–4 分钟，**不要加 -quit**）：

```
"D:/workspace/2022.3.62t18/Editor/Tuanjie.exe" -batchmode   -projectPath "D:/workspace/ESCAPE FROM WORK/game"   -runTests -testPlatform EditMode   -testResults "D:/workspace/ESCAPE FROM WORK/game/m1-tests-all.xml"   -logFile "D:/workspace/ESCAPE FROM WORK/game/m1-tests-all.log"
```

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
| `LevelGen/FloorGenerator.cs` | 1363 | **主生成器**：核心筒 / 楼梯 / 外廓 / 房间 / 走廊 / 门 / 道具 / 出生区 / 撤离点 | `Build(FloorSpec, seed) → FloorPlan`；`AddRoomClipped`、`Emit`、`EnsureExtractionSpread`、`AddSpawnZones`、`AddExtractions`、`SpawnCandidateInside`、`PullInsideFloor`；**`AddCornerCuts` 已被注释停用**（用户否决房间内切角） |
| `LevelGen/FloorEnvelope.cs` | 530 | 外廓多边形化：45° 截角 + 边缺口、房间按外廓裁剪、耳切三角化 | `FloorEnvelope.Build`、`ClipRect`、`Triangulate`、`Contains`、`DistanceToEdge`；`EnvelopeShape{Outline,Pieces,Area,NotchCount,Bounds}` |
| `LevelGen/FloorTypes.cs` | 259 | 数据结构 | `Room{Id,Rect,Outline,...}`（Rect 只作包围盒）、`FloorPlan{Envelope,...}`、`BoxSpec` |
| `LevelGen/FloorGeometry.cs` | 1108 | 实体化：地板/吊顶/墙/门/柱、流式外壳、覆盖率 | `Build`、`MeshAccum.AddTriangle/AddPrism`、`AddPlateBoxes`、`CollectShellBoxes`、`CollectVoidBoxes`、`ClipRunToEnvelope`、`EmitLines` |
| `LevelGen/FloorValidator.cs` | 271 | 校验与审计 | `FarThreshold(required, farthest)`（跨层推广的落点）、`AuditSpawns`、覆盖率/连通性断言 |
| `LevelGen/CoverProps.cs` | 429 | 掩体/家具摆放（按房间类型与密度） | 摆件规则、掩体档位 |
| `LevelGen/PropCatalog.cs` | 102 | 道具表（**目前是占位模型，M1-D 待填真模型**） | |
| `LevelGen/FloorChunk*.cs` | 481 | 分块：网格 / 计划 / 几何 / 流式 | `FloorChunkGeometry.BuildShell`、`FloorStreamer`、`FloorStreamingRuntime` |
| `LevelGen/FloorRuntime.cs` | 68 | 运行时装配 | |

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
| `Editor/Art/MaterialArtLibrary.cs` 等 | 223 | 程序化材质 |

### 4.4 测试（`game/Assets/tests/`）

EditMode 14 个文件：`FloorGeneratorTests`、`FloorGeneratorBatchTests`（多种子批量）、`FloorEnvelopeTests`、`FloorCoverageTests`、`WallLayoutTests`、`ExtractionSpreadTests`、`ChunkStreamingTests`、`CoverPropsTests`、`MapFogTests`、`MeshWindingTests`、`ShellColliderTests`、`PlayerMathTests`、`PlayerFeelTests`；PlayMode：`WhiteboxSmokeTests`。
**合计 EditMode 128 项，全绿**。改生成器任何规则前先跑一遍全量当基线。

---

## 5. 已完成（可依赖）

- **M0**：工程、URP、玩家、武器、HUD、截图管线。
- **M1 单层白盒**：程序生成 200×160 单层（房间 100–130、走廊、门、道具 900+、撤离 5、出生区 4–6）；外廓多边形化（四角 45° 截角 + 北侧缺口 + 房间按外廓裁剪）；墙/地/顶按多边形出；分块流式加载；雾图；离开（撤离）点的自适应阈值；出生区/撤离设施保证在外廓内。
- **R1 门数规则**（`RoomDoorPolicy.cs`，commit `0bd87f1`）：栅格连通修补后统一规整房门——普通房 ≥2 门且不同墙、≥100㎡ 目标 3 门、>400㎡ 大厅长边两端各 1 门；门洞边缘距最近墙角 0.15–0.50m / 0.90–1.20m；对墙门切向错位 ≥1.10m；≤3m 缺口用 1.4m 短喉道 / 绕行前厅补第二道门。
- **2D 平面原型**（`tools/plan-proto`，Python）：验证了"结构动作传播 + 分区空间语法"，八套空间语法库、A/B 两组对照图、体检器 `audit_floors.py`、规则示意图 `design_sketches.py`。
- **三份一手调研 + 复核清单**：`docs/research/`。

---

## 6. 不能违反的设计约束（改代码前先看）

1. **单门房 = 0**。普通房 ≥2 门且不同面墙；≥100㎡ 目标 3 门；大厅 >400㎡ ≥3 门且 2 个在长边两端。唯一例外：保险库（≤8% 面积，2 门中 1 道焊死 + 进入报警）。**现状：已由 `RoomDoorPolicy`（commit `0bd87f1`）实现，`SingleDoorTests` 守着；保险库例外仍未做。**
2. **走廊不能一穿到底**。直段 ≤30m，每 20–30m 必须有侧向出口；断点用"交叉口 / 凹位·门斗 / 可穿行房间"，**不要砌墙**（砌墙只是把屠杀点前移）。**现状违规：主廊 160–200m 零断点。**
3. **房间尺度**：目标中位 90–130㎡（现状 125–219 偏大）；40% 56–113 / 35% 113–200 / 20% 200–400 / 5% >400；<56㎡ ≤10% 且只给储藏/卫生间/管井。
4. **一远一近**：每层至少一个 ≥60m 视线的大空间（中庭/涡轮厅）和一个 ≤8m 的紧凑空间。
5. **四条"不随机"的语法层**（06 号文档 §3）：竖向价值梯度、每层一脊一环、竖向核固定对位、每层 1–2 个地标。
6. **出生/撤离用图距离**（不是直线）：出生间距 ≥ 全图最长最短路的 35%；出生到最近撤离 ≥ 50%。
7. **不要手工层**：0 手工层，质量靠生成器约束 + 批量校验 + Floor Lab 审图。
8. **桌面尺度**：单层 200×160 = 32,000㎡ 是生成单位；**一局可玩面积必须落在 24,000–30,000㎡**（12 人 × 2,100–2,400㎡/人）。

---

## 7. 下一步任务（按依赖顺序，每项带验收）

> 前 4 项**不依赖待拍板项**，可以现在就做；第 5 项之后需要用户确认 06 号文档。

| # | 任务 | 落点 | 验收 |
|---|---|---|---|
| R1 | **门数规则**：废掉 `area<380→1 门`，按面积/类型出 2–3 门且不同面墙；门位距墙角 0.15–0.5m / 0.9–1.2m，对门错位 ≥1.10m | `FloorGenerator` 关门那段 + `FloorTypes.Room` 存 Door 列表 | 单门房 = 0（保险库除外）；`SingleDoorTests` 新增 |
| R2 | **走廊断点**：每 26–32m 插入合法断点，补并行服务廊 1.8–2.1m | `FloorGenerator` 走廊段 | 最长直线视线 ≤45m；`SightlineTests` |
| R3 | **房间尺度重分布**：中位 90–130㎡，按 40/35/20/5 分档 | `FloorGenerator` 的 BSP 终止条件 + `FloorSpec` 参数 | 中位数、<56㎡ 占比、分档直方图 |
| R4 | **设施语法库**（替换办公室）：12 类设施（机房/冷库/管廊/车间/中控/变电/储罐/医疗隔离/装卸/档案/洁净间/生活舱），每层 3–4 种主题 | 新 `FacilityGrammar.cs` + `PropCatalog` | 每层主题不同；房间尺寸方差显著上升 |
| R5 | **SlicePlanner**：随机 2–3 层 × 30–50% 楼板、面积 24–30k㎡、封死区、连通性 | 新 `RaidPlan.cs` / `SlicePlanner.cs` | 面积/连通/竖向通路 ≥3 且不同象限 |
| R6 | **VerticalLinkPlan**：竖向核跨层对齐 | 新 `VerticalLinkPlan.cs` + `FloorSpec` | 相邻层 ≥3 通路、坐标对齐误差 <0.5m |
| R7 | **GraphDistance 校验**：出生/撤离改图距离 | `FloorValidator`（替换 `FarThreshold` 直线版） | 出生间距 ≥35%、到撤离 ≥50% |
| R8 | **环路注入**：外环 + 内环 + 局部环，环数 ≈ 房间数/12 | `FloorGenerator` / `SlicePlanner` | E−V+1 落在 ±30% |
| R9 | **SignageGenerator**：疏散牌 / 楼层号 / 平面图牌 / 电梯面板 | 新 `Signage.cs` + 场景装饰 | 每层 ≥1 处"你在这里"牌；截图可读 |
| R10 | **体检器接回归**：把 `tools/plan-proto/audit_floors.py` 的指标搬进 EditMode 测试 | 新 `LevelAuditTests.cs` | 指标表（06 §10）进 CI |

> **R1 已完成**（commit `0bd87f1`，EditMode 103/103）。下一步从 **R2** 开始；R2/R3 验收仍按上表原文。
> **对比图**：改动前基线已存 `docs/3d-rebuild/screenshots/r1-r3-before-{spawn,corridor,core}.png`；R1–R3 全部完成后再跑 `M1FloorScene.Capture` 出 after 图。

R2 施工要点（待实施）：
- 删掉 `CoverProps.AddSightBreaks` / `TryPlaceSightBreak` 的 26–32m `隔断墙` 道具；改成建筑性断点。
- 长廊按 Normal / Vestibule / Normal 切段（站距 ≤30m），前厅里开两个错位门洞 + 一个侧向出口；并行服务廊宽 1.8–2.1m，走廊—走廊开口要显式记录，避免合并墙把口封死。
- 插入点在 `Emit` 之后、`AssignCorridorDoors` 之前；`SightlineTests` 必须用真实后墙盒在眼高 1.62m 做 0.5m 采样 + ≥32 方向射线，最长视线 ≤45m；`FloorValidator.HasLineOfSight` 是栅格近似，不能当证据。

R3 施工要点（待实施）：
- 面积统计用裁剪后的 `Outline`，不是 `Rect.Area`。
- 给房间加类型（Normal / Storage / Toilet / Riser / Vault），<56㎡ 才允许后四类。
- 先扫 `SplitNode.stopP`（现 .06 / .30 / .62 / .85 / .95）与 `PrototypeLeafBias`（.55–1.6，占主导）；<56㎡ 碎片在关门之前做合并/归一化；走廊宽度不动。
- 测试加中位数 90–130㎡、<56㎡ ≤10%、40/35/20/5 分档。

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
- **M1-D**：`PropCatalog` 里的道具还是占位，需要真模型。
- **M1-E**：分块 LOD 与"每块 NavMesh"未做。
- **多边形化第 3–4 步**：导航栅格 / 小地图 / 分块仍按房间矩形；斜墙隔断 + L 形合并未做。
- **R2/R3 未做**：走廊直贯 160–200m（最长视线 ≤45m 未达标）、房间中位 125–219㎡（目标 90–130㎡）。R1 单门房已完成（`0bd87f1`）。
- **设计待拍板**（06 §8）：楼层总数、切片层数比例、封死楼板的表现、25 还是 30 分钟、TTK、AI 玩家上限、深度是否保留难度分层。
- **Boss 规则**仍待用户定（概率曲线已定：随深度 10/15/20/25%，其余待定）。

---

## 11. 工具与上下文限制（上一轮踩过）

- 本会话后期 `tools.compress` / `ops_bash` / `read_image` 曾被 auto-review 以 `CONTEXT_WINDOW_EXCEEDED` 全部拒绝，只能靠 `run_code` 内 node `fs` / `child_process` 读写文件、spawn python。压缩成功后恢复。若再遇到：**不要反复重试 compress**（reviewer 看的是全量历史，与 summary 长短无关），改用 `run_code` 直接干活。
- `tools.read` 偶发 120s 超时：一次只读一个小范围，或改用 node fs。
- 压缩 summary 不要用模板字符串拼长文本（会被反引号咬断，报 `Identifier cannot follow number`），用数组 `join`。

---

## 12. 一句话交接

**单层生成器已经能跑、外廓已多边形化；R1 已砍掉"单门房"（`0bd87f1`，103/103）。接下来按顺序做 R2 走廊断点、R3 房间尺度，再出 before/after 对比图；之后才是 06 号文档的 2–3 层切片。**

