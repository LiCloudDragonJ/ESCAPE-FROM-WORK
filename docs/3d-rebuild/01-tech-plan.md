# 3D 重构 · 技术方案（企业巨构版）

## 1. 引擎与包

| 包 | 版本 | 用途 |
|---|---|---|
| URP | 14.2.0-t1 | 渲染（沿用旧工程，零迁移） |
| Input System | 1.7.x（按 1.11.1 registry 解析） | 输入与重绑定 |
| AI Navigation | 1.1.x | NavMeshSurface 运行时烘焙（分块） |
| Addressables | 1.21.x | 模块套件与美术资源加载 |
| TextMeshPro | 3.0.9 | 文本 |
| Test Framework | 1.1.33 | EditMode / PlayMode 测试 |
| Newtonsoft Json | 3.2.2 | 存档 |
| UniTask | 2.x（可选） | 异步加载 |

创建工程后先验证 1.11.1 的 registry 是否提供上述版本，缺失则用兼容版本并在本文档记录。

## 2. 目录与 asmdef

    game/Assets/_Project/Scripts/
      Core/        Bootstrapper、ServiceRegistry、EventBus、SaveService、TimeService
      Rules/       纯 C#：FloorPlan、DamageModel、Ballistics、GridInventory、LootRoller、RunState
      LevelGen/    FloorGenerator、SkeletonBuilder、CorridorBuilder、RoomPacker、
                   ZoneComposer、TypologyLibrary、PropPlacer、NavBaker、LightPlanner、MapRenderer、FloorValidator
      Gameplay/    FpsController、WeaponController、Interaction、ExtractionPoint
      AI/          Perception、EnemyBrain、RivalBrain、EncounterDirector
      UI/          HUD、InventoryUI、MapUI、MenuUI（UI Toolkit）
      Data/        ScriptableObject 定义
      Editor/      FloorLab
    game/Assets/_Project/Art/        模块套件、材质、贴图
    game/Assets/_Project/Data/       SO 资产
    game/Assets/tests/               EditMode / PlayMode

asmdef：EscapeFromWork.Core / .Rules / .LevelGen / .Gameplay / .AI / .UI / .Data / .Editor / .Tests。
依赖方向单向：Gameplay → Rules；UI → Rules；LevelGen → Rules。Rules 不依赖任何上层，也不引用 UnityEngine 运行时对象。

## 3. 分层

- Rules 层：纯 C# 类，不继承 MonoBehaviour，只依赖 System 与数学库。伤害、掉落、背包、经济、楼层平面数据都在这里。
- Presentation 层：MonoBehaviour、GameObject、特效、动画，订阅 Rules 层事件。
- 目的：M6 接联机时，服务器直接跑 Rules 层，客户端只做表现，不用重写规则。

## 4. 生成管线（32,000㎡ 单层）

| 阶段 | 产出 | 预算 |
|---|---|---|
| Spec | FloorSpec（面积、层高、分区、事件、难度） | 5ms |
| Skeleton | 柱网、核心筒、消防梯、管井 | 20ms |
| Corridor | 主次走廊图 | 40ms |
| Zone | 2–4 个功能区划分（办公/实验/宿舍/…） | 25ms |
| Room | 房间切分与类型分配 | 80ms |
| Door | 门洞与门禁 | 25ms |
| Prop | 家具、掩体、战利品锚点 | 200ms |
| Validate | 连通、公平、穿模 | 60ms |
| Geometry | 墙体、地面、天花、道具实例化（8 块并行） | 800ms |
| NavMesh | 分块烘焙（8 块，可异步） | 1,200ms |
| Light | 灯位与房间灯组 | 60ms |
| Map | 俯视示意图贴图 | 40ms |

总计约 2.5s，留 0.5s 余量，目标 3s 内完成。

## 5. 分块与流式（32,000㎡ 的核心）

- 楼层切成 2×4 = 8 块，每块 4,000㎡（50×80m）。
- 每块独立生成几何、独立烘焙 NavMesh、独立挂 LOD 与剔除组。
- 生成顺序：骨架 → 走廊 → 房间数据（全层）→ 8 块几何并行实例化 → 8 块 NavMesh 依次烘焙。
- 玩家电梯下坠的 5 秒动画覆盖 3 秒生成窗口，不需要额外 loading 界面。
- 上一层卸载：先卸载几何与 NavMesh，再释放贴图与实例池；连续进出 10 层内存不增长。
- 远处块只保留碰撞体与低模，不渲染；进入 40m 才激活道具与灯光。

## 6. 楼层平面数据模型

    FloorPlan {
      int seed; FloorSpec spec;
      Rect bounds;                 // 200×160 / 280×280 / 160×120
      List<Column> columns;        // 8m 柱网
      Core core; List<Stair> stairs;
      List<Zone> zones;            // 2–4 个功能区，每个带原型
      List<Corridor> corridors;
      List<Room> rooms;
      List<Door> doors;
      List<LootAnchor> loot;
      List<ExtractionPoint> extractions;
      SpawnSet spawns;
      List<Chunk> chunks;          // 2×4
      MapModel map;                // 区域多边形 + 撤离点，供迷雾地图用
    }

    MapModel { List<MapRegion> regions; }   // { id, kind, polygon(4–8 顶点), zoneId }
    ExploredMask { BitArray perRegion; }    // 队伍级，随 run 存档

地图两级详略：detail 层 = 完整平面几何（墙/隔间/门/走廊）烘成 1024² RenderTexture；label 层 = 功能区名称 + 质心，仅未探索区显示。运行时用 ExploreMask（区域位图）裁 detail 层，只更新遮罩与撤离点状态，不重绘平面；单层烘焙 <15ms。

生成器只吃 seed，不读全局状态，保证同种子同结果。禁止使用 UnityEngine.Random（全局态），统一用 System.Random 实例。

## 7. 运行时 NavMesh

- NavMeshSurface 挂在每个 chunk 根节点，8 块分别 BuildNavMesh()。
- Agent：半径 0.35m、高度 1.8m、最大坡度 45°、台阶 0.4m。
- 烘焙体素 0.15m；单块目标 < 200ms，8 块合计 < 1.2s。
- 门、家具、可破坏物按静态烘焙；动态障碍用 NavMeshObstacle 局部挖洞。
- 30–40 个 AI 用 NavMeshAgent + 局部避让；远处 AI 降频（0.2s 一次决策）。

## 8. 光照

运行时生成的关卡无法烘焙 lightmap，2022.3 也没有 APV，所以：

- 每个 8×8m 柱网单元 1–2 块自发光吊顶板（emissive，不产生实时灯）。
- 实时灯池：只保留离玩家最近的 16 盏点光/聚光，其余关闭；切换时用 0.2s 淡入避免跳变。
- 阴影只给主方向光和 4 盏关键点光，阴影距离 20m。
- 后处理：SSAO、Bloom、Vignette、Color Grading、Film Grain。
- 停电事件：关闭实时灯，切应急灯（低强度 + 自发光），只保留出口指示灯。

## 9. 性能预算

| 指标 | 目标 |
|---|---|
| 分辨率/帧率 | 1080p 60fps（GTX 1060）/ 30fps（Iris Xe） |
| Draw Call | < 3,500 |
| 实时灯 | < 16 |
| 单层生成 | < 3s |
| 单层内存 | < 400MB |
| AI 同屏 | 30–40 |

手段：SRP Batcher、GPU Instancing、运行时 StaticBatchingUtility.Combine（按 chunk）、房间级剔除组、道具 LOD、远景块不渲染。

## 10. 输入

- Input System Action Maps：Player、Combat、UI、Map。
- 支持键鼠与手柄，全套重绑定，灵敏度与 ADS 倍率可调。
- 输入在 Rules 层抽象成命令（MoveCommand、FireCommand、InteractCommand），便于联机重放。

## 11. 存档

- 路径：%USERPROFILE%/AppData/LocalLow/<Company>/EscapeFromWork3D/profile.json
- 内容：已解锁存档层（5 的倍数）、仓库、装备、统计、设置、版本号。
- 原子写：先写 .tmp 再替换；版本号做迁移。
- 单机允许断点续跑（run 字段非空则恢复本局）。

## 12. 联机预留（M6）

- 方案：FishNet + 自建专用服务器（国内云）。
- 服务器权威：伤害、拾取、撤离、移动校验全部在服务端。
- 生成：服务端出 seed，客户端本地生成，校验哈希。
- 反作弊基线：命中校验、速度校验、射速校验、物资白名单。
- 对局模型：**每层是一局独立匹配**。下坠 = 客户端离开当前对局实例、向服务端申请下一层的对局；撤离 = 结束本局。
  - 下一层优先分配"正在集结"的对局，60s 未满即带 AI 补位开局（可调）。
  - 整队进入同一局；服务器保留队伍关系，换局不换队。
  - 同一层的不同玩家各在自己那一局，撤离/下坠互不影响。
  - Boss 在生成时就 roll 定（属于该局），无跨局冷却。
  - 出生区与撤离点由服务端按队伍分配：每队抽取出生区，再分配 2 个"离本队最远"的撤离点，客户端只接收结果。
  - 撤离点分配基于寻路距离（不是直线），保证绕行时间真实。

## 13. 测试

- EditMode：DamageModel、GridInventory、LootRoller、FloorValidator。
- 生成批测：1000 个种子断言不变量（全连通、无穿模、撤离距离、战利品平衡、分块边界不切断房间）。
- PlayMode：控制器、撤离读条、死亡结算、连续 10 层内存。

## 14. 风险

| 风险 | 对策 |
|---|---|
| 32,000㎡ 单层性能不达标 | 8 块流式 + 灯池 + 剔除组；先灰盒实测再定美术密度 |
| 运行时生成慢 | 电梯 5s 覆盖 3s；NavMesh 异步；失败种子落盘 |
| 大平层重复感 | 每层 2–4 个功能区 + 6 种原型 + 事件；Floor Lab 批量审图 |
| 生成器 bug 导致不可玩 | 1000 种子批测进 CI；失败种子可复现 |
| 联机后期返工 | Rules 层纯 C#；seed 确定性；输入命令化 |
| 单层 12 人匹配人口碎片 | 先 AI 对手；联机按层匹配 + AI 补位；60s 未满即开局 |
