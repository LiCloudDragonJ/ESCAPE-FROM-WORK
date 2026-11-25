# 3D 重构 · 里程碑（企业巨构版）

## 0. Vertical slice 定义

范围锁在 50–40 这一段：存档点 50/45/40（其中 50、40 是安全层，45 仍是危险区），9 层随机 + 1 层安全层，共 10 层；Boss 不固定层，按概率随机注入。
完成后能录一段 30 分钟 demo：进楼、搜刮、交战、撤离或下坠、死亡回档、跨存档点。

不做：联机、正式美术、经济系统、任务线、多语言。

## 1. 里程碑

| 阶段 | 内容 | 验收标准 |
|---|---|---|
| M0 地基 | 新建 game/ 工程；asmdef 分层；Input System；FPS 控制器（走/跑/蹲/跳/台阶/斜坡）；白盒测试房 | 1080p 60fps；输入可重绑定；手柄可用 |
| M1 生成器 | Floor Lab 编辑器工具；骨架/分区/走廊/房间/门/家具；8 块分块生成；运行时 NavMesh；灯池；地图（区域级迷雾 + 本队撤离点标记）；按队伍分配出生区与撤离点；校验器 | 1000 种子批量生成：零穿模、全连通、约束全过；出生→本队撤离点寻路 ≥150m 且不在视线内；32,000㎡ 单层 <3s；Floor Lab 可出俯视图人工审 |
| M2 战斗 | 3 把办公武器；ADS；后坐力；弹道下坠；护甲与命中区；敌人 AI（视锥、听觉、掩体、巡逻） | 手感可调参；AI 会压制与绕后；命中反馈清晰；40 AI 同屏不掉帧 |
| M3 搜打撤 | 容器搜刮；10×6 网格背包与负重；撤离读条；每 5 层存档点解锁；存档、仓库、死亡结算 | 完整跑通 50→45→40；死亡正确回最近存档点；重启游戏进度不丢 |
| M4 AI 对手 | 会搜刮、交战、抢撤离点的 AI 干员；遭遇导演；组队规则桩 | 玩家能感到有人在抢资源，而不是打木桩 |
| M5 内容 | 50–40 段模块美术；Boss 层；音频；UI 打磨；性能优化 | 60fps（GTX 1060）；可录 demo |
| M6 联机 | FishNet 专用服务器；按层匹配（下坠换局，60s 集结 + AI 补位）；服务器权威；基础反作弊 | 双人组队跑通完整循环（含下坠进新局） |

## 2. 验收清单（每个里程碑结束跑一遍）

- 编译零警告（至少零错误）。
- EditMode 测试全绿。
- 1000 种子生成批测通过。
- 60fps 目标机器实测（32,000㎡ 满 AI）。
- 无内存泄漏（连续进出 10 层）。
- 存档写入原子性（断电模拟）。
- 电梯 5s 内完成下一层生成。

## 3. 风险

| 风险 | 影响 | 对策 |
|---|---|---|
| 32,000㎡ 单层性能不达标 | 高 | 8 块流式 + 灯池 + 剔除组；先灰盒实测再定美术密度 |
| 程序化大平层不好看/不好玩 | 高 | Floor Lab 先审布局；每层 2–4 个功能区；灰盒先跑通 |
| 运行时生成慢 | 中 | 电梯 5s 覆盖 3s；NavMesh 异步；失败种子落盘 |
| Boss 层程序化不公平 | 中 | 约束注入 + 1000 种子抽检 + 人工试玩 |
| 全部程序化导致重复感 | 高 | 10 种原型 × 每层多分区 × 事件 × 尺寸差异 |
| 联机返工 | 高 | Rules 层纯 C#；seed 确定性；命令化输入 |
| 单人体量 | 高 | 先做 10 层 VS，验证后再铺 50 层 |

## 4. 明确不做

- 不做线性战役。
- 不做手工关卡。
- 不做载具、干员技能、藏身处。
- 不做抽卡、内购。
- M0–M5 不做联机。

## 5. M0 状态（2026-11-24，已完成）

**交付物**

| 项 | 状态 | 位置 |
|---|---|---|
| 新工程（团结 1.11.1 / 2022.3.62t18，URP 14.2.0-t1） | ✅ | `game/` |
| asmdef 分层 Core / Data / LevelGen / Gameplay / AI / UI / Editor / Tests | ✅ | `game/Assets/_Project/Scripts/**` |
| Input System 映射 + 运行时改键 + PlayerPrefs 存档 | ✅ | `Gameplay/Player/PlayerInputActions.cs` |
| 第一人称控制器（走/跑/蹲/跳/台阶/斜坡/空中控制/鼠标+手柄） | ✅ | `Gameplay/Player/FirstPersonController.cs` |
| 白盒测试场景（50×50 封闭楼层：结构板 + 3.37m 吊顶 + 灯盘、双房间、台阶、斜坡、蹲行横梁、柱/箱） | ✅ | `Assets/_Project/Scenes/Whitebox.unity` |
| 吊顶/层高构造（4.2m 层高、3.4m 净高、楼梯间不吊顶） | ✅ | `docs/3d-rebuild/02-level-design.md` §2.1 |
| 性能 HUD（F3：FPS / ms） | ✅ | `Gameplay/Player/PerfHud.cs` |
| 自动化测试 | ✅ 6 EditMode + 2 PlayMode | `Assets/tests/` |
| 白盒截图 | ✅ 1920×1080 | `docs/3d-rebuild/screenshots/m0-whitebox.png` |

**验收**

- EditMode 6/6、PlayMode 2/2 通过（`Tuanjie.exe -batchmode -runTests`）。
- 60fps 验收需人工：打开 `game/` 工程 → 载入 `Whitebox` → Play → F3 看帧率。batchmode 下 dt 失真，不能作为帧率依据。
- 已知：batchmode 中 `CharacterController.isGrounded` 在极小 dt 下会漏判，已加向下 Raycast 兜底；真实帧率下无影响。

**M0 遗留（不阻塞 M1）**

- 没有动画、音效、UI 框架、存档、武器——按里程碑本就属于后续阶段。
- 工程未提交 git（等用户确认）。

## 6. M1 状态（2026-11-24，生成器部分完成）

**交付物**

| 项 | 状态 | 位置 |
|---|---|---|
| BSP 随机切分生成器 v2（随机切、按切分预留走廊带、叶子里填房间） | ✅ | `game/Assets/_Project/Scripts/LevelGen/FloorGenerator.cs` |
| 走廊三级（主 4.8m / 次 3.6m / 三级 2.7m）+ 净宽规则 | ✅ | `FloorTypes.cs` `FloorSpec` |
| 走廊掩体：大/中/小三档、每组两侧各一件、同段至少两种尺寸、门口 1.2m 净空、中间留 ≥2.4m | ✅ | `LevelGen/CoverProps.cs` `CoverPlacer` |
| 长走廊隔断墙（26–32m 一道、只留 2.4m 通过、两侧交替、不堵门） | ✅ | `CoverProps.AddSightBreaks` |
| 多门（≥80㎡ 房间至少两道门；环路门；套间门） | ✅ | `AssignCorridorDoors` / `AddLoopDoors` |
| 几何连通兜底（1m 栅格泛洪 + 补门，防止只是"图连通"） | ✅ | `EnsureGridConnectivity` |
| 出生区吸附真实地面（四角取最近房间/走廊切 10×10） | ✅ | `AddSpawnZones` |
| Floor Lab 编辑器窗口（调参、单张出图、批量出图） | ✅ | `game/Assets/_Project/Scripts/Editor/FloorLabWindow.cs` |
| 3D 实体化：整块地板/吊顶 + 13 个合并 Mesh + 门洞 + 48 盏灯池 | ✅ | `LevelGen/FloorGeometry.cs`、`LevelGen/FloorRuntime.cs`、`Assets/_Project/Scenes/FloorTest.unity` |
| 运行时 NavMesh 烘焙（NavMeshBuilder，agent 0.35/1.8/45°/0.4） | ✅ 代码 | `FloorGeometry.BuildNavMesh` |
| 地图迷雾 HUD（区域级揭示、未探索只给区域名、撤离点常显、不暂停） | ✅ | `UI/MapHud.cs`（MapFog / MapRaster / MapHud） |
| 撤离点选点（固定设施候选 → 按出生区散布启用 4–6 个） | ✅ | `FloorGenerator.AddExtractions` / `EnsureExtractionSpread`、`FloorPlan.ExtractionCandidates` |
| 撤离点散布自检（行走距离 ≥60% 对角线、视线遮挡） | ✅ | `LevelGen/FloorValidator.cs`、`FloorGenerator.EnsureExtractionSpread` |
| 运行时 8 块分块流式几何（2×4=8 块、每块 4000㎡、外壳常驻、按观众距离加载/卸载） | ✅ | `LevelGen/FloorChunkGrid.cs`、`FloorChunkPlan.cs`、`FloorChunkGeometry.cs`、`FloorStreamer.cs`、`FloorStreamingRuntime.cs`、`Scenes/FloorStreamingTest.unity` |
| EditMode 测试（含 1000 种子批测、分块流式、分块几何成本） | ✅ 42/42 | `game/Assets/tests/EditMode/` |

**验收数据（2026-11-24）**

- EditMode 42/42 通过，整轮 191.7s（含 1000 种子批测、120 种子出生区散布检查、分块流式 10 项与 8 块几何成本测试；修正撤离点选点后重跑）。
- 1000 种子：房间 >8、撤离点 4–6、出生区 4、柱子 >20、覆盖率 0.5–0.96、房间门率 ≥90%、1m 栅格连通率 >98%。
- 生成速度（编辑器单线程）：平均 186ms/层；塔身 200×160 约 20–60ms，裙房 280×280 约 400ms，最慢种子 789ms。仍在 3s 预算内。
- 单层布局指标（seed 1，200×160）：268 房 / 覆盖 59% / 走廊 30% / 空腔 11% / 53 条走廊 2146m / 掩体 724 件。

**3D 与 HUD 验收数据（2026-11-24，本机 batchmode）**

- FloorTest（seed 1，200×160）：237 房 / 102 走廊 / 825 掩体 / 5 撤离点（旧选点逻辑生成的场景；重建后按新选点取 4–6 个），出生点 (12.02, 0.20, 13.88)。
- 几何合计 83,448 顶点、13 个渲染器（Walls 30,408 顶点 / Floor、Ceiling 各 8,208 / Columns 10,704 / Props 22,984 / LightPanels 5,688 / SightBreaks 816），bounds 全在楼体范围内。
- 截图：`screenshots/m1-floor3d-spawn.png`、`m1-floor3d-corridor.png`、`m1-floor3d-core.png`。
- 地图迷雾：`MapFog` 2m 格 + 30m/视锥揭示；`MapRaster` 800×640（4px/m）。

**分块流式验收数据（2026-11-24，本机 batchmode）**

- `FloorStreamingTest.unity`（seed 1，200×160，237 房）：FloorStreamingRuntime 加载半径 1 / 卸载半径 2 / 每帧 2 块，外壳常驻、8 块按观众所在块流式进出；出生点 (12.02, 0.20, 13.88) 与 FloorTest 一致。
- 几何成本：单块 0.9–2.2ms（311–368 个构件）、外壳 3.8ms、8 块合计 13.4ms（编辑器实测，不含渲染与碰撞体烘焙）。
- 裁剪正确性：分块后墙体体积之和与整层原值一致（误差 <0.1%）；掩体/出生台/灯板按块裁剪后无缺失；撤离点与灯光按中心唯一归属。
- 流式策略：最近块优先、每帧预算 1–2 块、卸载带 2 块滞回；EditMode 10 项测试覆盖网格铺满/不重叠、裁剪守恒、归属唯一、预算、优先序、卸载滞回、确定性、外壳+块构建。

**AI 材质管线（2026-11-24，本机 ComfyUI）**

- 生成端：`F:\ComfyUI`（SDXL base 1.0）+ `efw_gen_materials.py`（SeamlessTile 用于可平铺）+ `efw_post_materials.py`（接缝检测/offset-blend、Sobel 法线、AO/金属/光滑度打包）。
- 7 种 1024² 可平铺贴图：CarpetTile、ConcreteFloor、WoodPanel、BrushedMetal、CeilingTile、PaintedWall、DarkMetal；后处理输出 Albedo / Normal / Mask 到 `game/Assets/_Project/Art/Textures/`。
- Unity 端：`MaterialArtTextureImporter`（sRGB/法线/线性 alpha 自动设置）、`MaterialArtLibrary`（10 个槽位映射 + tiling，菜单 Escape From Work/Art/Build Material Library From Textures，`RebuildAll` 依次重建材质、两个场景与截图）。
- 几何补平铺 UV：`FloorGeometry.WorldUv`（1 uv = 1 米，按法线主轴投影），否则灰盒无 UV 贴图无法映射。
- 截图已用 AI 材质重出（`m1-floor3d-spawn/corridor/core.png`）；灯光/曝光仍偏亮偏平，手感与光照调优留给 Play 模式。

**M1 剩余**

- 真实帧率下的 60fps 与灯池验收（batchmode 测不出帧率，需高性能机 Play 模式实测）。
- 灯光/曝光调优：当前 batchmode 截图偏亮、材质对比弱（灯池 48 盏与 ambient 需要实机调）。
- PropCatalog（名字→prefab + 尺寸档 + 碰撞盒），打通灰盒与真实 3D 资产。
- 分块 LOD / 剔除组与每块独立 NavMesh（设计文档口径，代码仍是整层 NavMesh）。

## 7. 手感迭代（2026-11-25，供 Play 模式实测）

**目标**：先让人能"跑起来"感受移动/镜头，再谈战斗。改动集中在控制器与跑测工具，生成器参数未动。

**FirstPersonController 手感改动**（`game/Assets/_Project/Scripts/Gameplay/Player/FirstPersonController.cs`）

| 项 | 旧 | 新 | 理由 |
|---|---|---|---|
| 走 / 冲刺 / 蹲 | 4.2 / 7.0 / 1.8 | **4.4 / 7.6 / 1.9** | 12 人 / 3,200㎡ 的层，走速 4.2 略拖 |
| 地面加速 | 30 单一值 | **加速 40 / 减速 48** 分开 | 旧值松手后滑行过长，像冰面 |
| 空中控制 | 6 | **8 加速 / 2 阻力** | 跳上掩体时能微调落点 |
| 跳跃 | 1.05m，无缓冲 | **1.0m + 0.12s 跳跃缓冲** | 落地前一帧按跳不再吞输入 |
| 重力 | -24 | **-26** | 下落更利落，配合 1m 跳高 |
| 冲刺 FOV | 无 | **+7° 缓动（7/s）** | 速度感 |
| 头部晃动 | 无 | **走 2.6cm / 冲刺 4cm，步幅 1.7m，含横向 35%** | 移动有重量；F1 可关 |
| 落地缓冲 | 无 | **按落地速度下沉，上限 14cm，9/s 恢复** | 从掩体跳下不再"贴地滑走" |

**跑测工具**

- `UI/PlayTestControls.cs`（十字准星也在这里画）：**R 换一层（种子 +17）**，会重建分块几何、NavMesh、地图迷雾并把玩家传送到新出生区、朝向核心筒；**H 收起/展开控制说明**。
- `FirstPersonController.Teleport(pos, yaw)`：换层时清速度/晃动相位，避免带着旧速度摔进新层。
- `FloorStreamingRuntime.Regenerate(seed)`：销毁旧块/外壳/NavMesh，重置统计后重新 Initialize。
- `MapHud.Rebuild(plan)`：换层后地图、迷雾、撤离点标记一起重建。
- 场景入口：`Assets/_Project/Scenes/FloorStreamingTest.unity`（Build Settings 第一项）。

**操作**：WASD 移动 / Shift 冲刺 / Ctrl 蹲 / Space 跳 / M 地图 / F1 视角晃动 / F3 帧率 / R 换层 / `[` `]` 实时调鼠标灵敏度 / Esc 解锁鼠标 / 左键锁回。锁定时屏幕中央有十字准星。

**本轮未做**（下一步）：武器与射击手感、体力条、脚步/环境音、灯光曝光调优、PropCatalog。
