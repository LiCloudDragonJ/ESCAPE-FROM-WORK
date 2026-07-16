# Phase 1 Remaining — 完整实现计划

> **目标读者**：另一台设备上的 Claude Code 实例。
> **恢复上下文**：先读 `production/session-state/active.md` 和 `CLAUDE.md`。
> **最新 commit**: `e2ecf5a` — 代码清理已推送。

---

## 设计决策摘要（2026-07-16 确认）

| 决策 | 结论 |
|------|------|
| 楼层尺寸 | 60m × 50m (3,000 m²)，1 单位 = 1 米 |
| 玩家移速 | moveSpeed: 8→5 m/s, dodgeSpeed: 18→10 m/s |
| 布局算法 | 走廊驱动 + 规则约束（先画走廊，后在两侧放房间） |
| 墙壁 | Phase 1 用 Cube 拼墙（0.2m 厚 × 3m 高），后期换 AI 生成模块 |
| 楼梯/消防梯 | 左下角 / 右上角固定，A* 走廊必须连通 |
| 茶水间 | 每 5 层一个（50F, 45F, 40F...），靠近入口楼梯 |
| 家具 | 40 种，房间类型→家具模板→搜刮表，三层驱动 |
| 变体 | 普通/主管/CEO/破损 四种搜刮变体 |
| 垃圾桶 | 保留，90% 垃圾 10% 惊喜 |
| 基地储物箱 | 初始 8×10=80 格，可升级容量 + 堆叠上限 |
| 武器架 | 网格展示，选 Loadout，Phase 1 不实装改装 |
| NPC 任务 | 完整任务链（参考塔科夫），部门任务（参考三角洲） |

---

## Task 1: 比例校准

### 1.1 修改移动速度

**文件**: `Assets/_Project/Scripts/Player/PlayerController.cs:18,22`

将 `moveSpeed` 从 8 改为 5，`dodgeSpeed` 从 18 改为 10。

### 1.2 建筑常量

**文件**: `Assets/_Project/Scripts/Editor/SceneWirer.cs`，替换常量区：

```csharp
const float MapWidth = 60f;
const float MapDepth = 50f;
const float WallHeight = 3f;
const float WallThickness = 0.2f;
const float CorridorWidth = 2.5f;
const float DoorWidth = 1.5f;
```

---

## Task 2: 走廊驱动楼层生成器

### 核心算法（5 步）

**Step 1 — 放置锚点**
- 普通楼梯间：左下角，4m×4m
- 消防通道：右上角，4m×4m
- 茶水间（每5层）：靠近楼梯间，4m×5m

**Step 2 — A* 主走廊**
从楼梯间门口到消防通道门口，用 A* 在网格上找路径。在路径上随机加偏移（±2m 内波动）避免过于笔直。走廊宽 2.5m。

**Step 3 — 分支走廊**
从主走廊每隔 8-12m（随机）垂直分叉。分支长度 8-20m。生成 3-5 条分支。

**Step 4 — 分配房间**
沿走廊分配房间类型：
- CEO办公室 → 最深分支末端（离入口最远），10m×10m
- 茶水间 → 靠近入口的第一分支
- 会议室 → 靠外侧的大空间，8m×10m
- 服务器房 → 内侧位置（无外窗），6m×6m
- HR/财务 → 中层位置
- 开放办公区 → 填充其余空间，大小自适应

**Step 5 — 验证连通性**
确保每个房间门都能通到走廊。

### 2.1 创建 FloorLayoutData

**新文件**: `Assets/_Project/Scripts/Level/FloorLayoutData.cs`

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace EscapeFromWork.Level
{
    [System.Serializable]
    public class FloorLayoutData
    {
        public int floorNumber;
        public int seed;
        public Vector2 entryStairsPos;      // 左下角楼梯世界坐标
        public Vector2 fireEscapePos;        // 右上角消防梯世界坐标
        public Vector2 teaRoomPos;           // 茶水间（每5层）
        public bool hasTeaRoom;
        public List<RoomDef> rooms = new List<RoomDef>();
        public List<CorridorSeg> corridors = new List<CorridorSeg>();
    }

    [System.Serializable]
    public class RoomDef
    {
        public RoomType roomType;
        public Vector2 worldPos;    // 左下角
        public Vector2 size;        // 宽×深
        public Vector2 doorPos;     // 门的世界坐标
    }

    [System.Serializable]
    public class CorridorSeg
    {
        public Vector2 start;
        public Vector2 end;
        public float width;
        public bool isMainCorridor;
    }
}
```

### 2.2 创建 FloorLayoutGenerator

**新文件**: `Assets/_Project/Scripts/Level/FloorLayoutGenerator.cs`

这是核心类，300+ 行。以下是关键方法签名和逻辑：

```csharp
namespace EscapeFromWork.Level
{
    public class FloorLayoutGenerator
    {
        // ---- 公开接口 ----
        public FloorLayoutData Generate(int floorNumber, int seed);

        // ---- 内部步骤 ----
        private void PlaceAnchors(FloorLayoutData layout);
        private List<Vector2Int> GenerateMainCorridor(FloorLayoutData layout);
        private List<Vector2Int> GenerateBranches(List<Vector2Int> mainCorridor, FloorLayoutData layout);
        private void AssignRooms(List<Vector2Int> allCorridors, FloorLayoutData layout);
        private bool ValidateConnectivity(FloorLayoutData layout);

        // ---- A* 实现 ----
        private List<Vector2Int> AStarPath(Vector2Int start, Vector2Int end, bool[,] blocked);

        // ---- 房间规则 ----
        private RoomType DetermineRoomType(Vector2 pos, int floorNumber, float distFromEntry);
        private Vector2 DetermineRoomSize(RoomType type, Vector2 availableSpace);
    }
}
```

**生成逻辑伪代码**：

```
Generate(floorNumber, seed):
    Random.InitState(seed)
    layout = new FloorLayoutData()

    // 1. 锚点
    layout.entryStairsPos = (2, 2)      // 左下角
    layout.fireEscapePos = (56, 46)     // 右上角
    layout.hasTeaRoom = (floorNumber % 5 == 0)
    if layout.hasTeaRoom:
        layout.teaRoomPos = (6, 2)      // 楼梯旁边

    // 2. 在 60×50 网格上做 A*
    grid = bool[60][50]
    标记锚点为阻挡
    mainPath = AStar(entryDoor, fireEscapeDoor, grid)
    沿路径随机偏移 ±2m
    layout.corridors.add(mainPath)

    // 3. 分支走廊
    沿主走廊每隔 8-12m:
        方向 = 随机(垂直于主走廊方向)
        长度 = Random(8, 20)
        画分支直到撞墙/撞其他房间
        layout.corridors.add(branch)

    // 4. 沿走廊两侧分配房间
    遍历走廊网格的每个边:
        可用空间 = 走廊边到下一走廊/边界
        if 可用空间 >= 4×4:
            roomType = DetermineRoomType(位置, 楼层, 距入口距离)
            roomSize = DetermineRoomSize(roomType, 可用空间)
            layout.rooms.add(new RoomDef(roomType, 位置, roomSize, 门位置))

    // 5. 验证
    if not ValidateConnectivity(layout):
        重新生成（最多重试 5 次）

    return layout
```

### 2.3 修改 SceneWirer 使用 FloorLayoutGenerator

**文件**: `Assets/_Project/Scripts/Editor/SceneWirer.cs`

重写 BuildScene() 的地板创建部分。当前是 5×5 彩色 Cube，变为：

```csharp
// ---- 2. 生成楼层布局 ----
int floorNum = 50;  // 原型：固定 50 楼
int seed = GameManager.RunSeed + floorNum;
var layoutGen = new FloorLayoutGenerator();
FloorLayoutData layout = layoutGen.Generate(floorNum, seed);

// ---- 3. 构建几何体 ----
foreach (var room in layout.rooms)
{
    BuildRoom(room, floorHolder);
}
foreach (var corr in layout.corridors)
{
    BuildCorridor(corr, floorHolder);
}
BuildDoors(layout, floorHolder);
```

### 2.4 构建辅助方法

在 SceneWirer.cs 中添加：

```csharp
// 建墙：4 面墙围成房间
static void BuildRoom(RoomDef room, Transform parent) { ... }

// 建走廊地板
static void BuildCorridor(CorridorSeg corr, Transform parent) { ... }

// 在房间门位置开口
static void BuildDoors(FloorLayoutData layout, Transform parent) { ... }
```

每面墙是一个拉伸的 Cube：
```csharp
GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
wall.transform.position = new Vector3(x, WallHeight/2, z);
wall.transform.localScale = new Vector3(length, WallHeight, WallThickness);
```

---

## Task 3: 家具 + 搜刮绑定系统

### 3.1 创建 FurnitureTemplate

**新文件**: `Assets/_Project/Scripts/Data/FurnitureTemplate.cs`

```csharp
namespace EscapeFromWork.Data
{
    public enum FurnitureType
    {
        // 办公区 (10种)
        EmployeeDesk, SupervisorDesk, FileCabinet, Printer, TrashCan,
        CoatRack, AttendanceBoard, Shredder, WaterDispenser, CubiclePartition,
        // 茶水间 (5种)
        CoffeeMachine, Fridge, Microwave, BreakSofa, SnackShelf,
        // 会议室 (4种)
        ConferenceTable, Projector, Whiteboard, SpeakerPhone,
        // 服务器/IT (4种)
        ServerRack, UPSPower, PatchPanel, ITWorkbench,
        // CEO/高管 (6种)
        CEODeck, WineCabinet, Bookshelf, LeatherSofa, OilPainting, Safe,
        // HR/行政 (3种)
        ArchiveWall, AdminDesk, KeyCabinet,
        // 财务 (2种)
        FinanceDesk, ReportShelf,
        // 前台/大厅 (3种)
        ReceptionDesk, WaitingSofa, PlantLarge,
        // 特殊/隐藏 (3种)
        CeilingHatch, FishTank, SuggestionBox,
    }

    [CreateAssetMenu(menuName = "ESCAPE/Furniture Template")]
    public class FurnitureTemplate : ScriptableObject
    {
        public FurnitureType furnitureType;
        public ContainerType containerType;    // 搜刮容器类型
        public Vector2Int gridSize = Vector2Int.one;  // 占地
        public LootTable lootTable;            // 搜刮表引用
        public GameObject visualPrefab;        // Phase 2 用，Phase 1 留空
        public string visualDescription;       // Phase 1 用，描述如何用 Cube 拼
    }
}
```

### 3.2 房间→家具映射规则

**新文件**: `Assets/_Project/Scripts/Data/RoomFurnitureSet.cs`

```csharp
[CreateAssetMenu(menuName = "ESCAPE/Room Furniture Set")]
public class RoomFurnitureSet : ScriptableObject
{
    public RoomType roomType;
    public FurniturePlacement[] mandatoryFurniture;   // 必放
    public FurniturePlacement[] optionalFurniture;    // 概率放
}

[System.Serializable]
public class FurniturePlacement
{
    public FurnitureTemplate template;
    [Range(0, 20)] public int minCount = 1;
    [Range(0, 20)] public int maxCount = 2;
    public float spawnChance = 1f;
    public bool canBeSupervisor;   // 是否可以有主管变体
    public bool canBeBroken;       // 是否可以有破损变体
}
```

### 3.3 开放办公区配置示例

```yaml
RoomType: OpenOffice
Mandatory:
  - EmployeeDesk:  min=6, max=10, canBeSupervisor=true, canBeBroken=true
  - FileCabinet:   min=2, max=4
  - TrashCan:      min=2, max=4, canBeBroken=true
  - WaterDispenser: min=1, max=1
Optional:
  - Printer:       min=0, max=1, spawnChance=0.7
  - CoatRack:      min=1, max=3, spawnChance=0.6
  - Shredder:      min=0, max=1, spawnChance=0.4
  - AttendanceBoard: min=0, max=1, spawnChance=0.3
```

### 3.4 变体系统

搜刮表变体在 LootTable 层面实现：

```
办公桌搜刮表 (LootTable)
├── Desk_Common (普通员工)     → 回形针60%, 便利贴20%, 打印纸15%, 能量棒5%
├── Desk_Supervisor (主管)    → 回形针30%, U盘25%, 人事档案15%, 奢侈品1%
├── Desk_CEO (老板)          → 必出绿+, 可能出传说, CEO钢笔10%
└── Desk_Ransacked (破损)    → 品质-50%, 数量-50%, 稀有品不出
```

生成房间时逻辑：
```
放置 8 张 EmployeeDesk:
  1 张随机选为 supervisor → 用 Desk_Supervisor 表
  1 张随机选为 ransacked  → 用 Desk_Ransacked 表
  6 张普通                → 用 Desk_Common 表
```

### 3.5 垃圾桶特殊逻辑

```csharp
// 垃圾桶：高概率垃圾，低概率惊喜
LootTable TrashTable:
  ShreddedPaper (无价值/白)    50%
  EnergyBar (消耗品/白)        20%
  CoffeeGrounds (无价值/白)    15%
  MistakenlyDiscardedUSB (电子/蓝) 3%
  MistakenlyDiscardedFile (情报/紫) 2%
```

### 3.6 家具放置器

**新文件**: `Assets/_Project/Scripts/Level/FurniturePlacer.cs`

```csharp
namespace EscapeFromWork.Level
{
    public class FurniturePlacer
    {
        /// <summary>
        /// 在给定房间内放置家具，返回所有放置的家具+搜刮容器引用。
        /// </summary>
        public static List<PlacedFurniture> PlaceFurniture(
            RoomDef room,
            RoomFurnitureSet furnitureSet,
            int seed)
        { ... }

        /// <summary>
        /// 为一件家具创建 Cube 几何体 + LootContainer 组件。
        /// Phase 1 用 Cube 拼；Phase 2 换成 Prefab。
        /// </summary>
        private static GameObject BuildFurnitureVisual(
            FurnitureTemplate template,
            Vector3 worldPos,
            Transform parent)
        { ... }
    }

    public class PlacedFurniture
    {
        public FurnitureTemplate template;
        public Vector3 position;
        public LootContainer lootContainer;  // 搜刮交互
        public bool isSupervisorVariant;
        public bool isRansacked;
    }
}
```

---

## Task 4: 茶水间基地系统

### 4.1 BaseState — 持久化存储

**新文件**: `Assets/_Project/Scripts/Level/BaseState.cs`

```csharp
namespace EscapeFromWork.Level
{
    [System.Serializable]
    public class BaseState
    {
        // 储物箱：item asset path → count
        public Dictionary<string, int> storedItems = new();
        // 储物箱容量
        public int stashWidth = 8;
        public int stashHeight = 10;
        public int stashMaxStackMultiplier = 10;  // raid堆叠上限×10
        // 武器架
        public List<string> ownedWeaponPaths = new();  // 拥有的武器 AssetPath
        // 统计
        public int raidsCompleted;
        public int deepestFloorReached = 50;
        public int totalValueStashed;
        // 升级等级
        public int stashUpgradeLevel;
        public int weaponRackUpgradeLevel;

        // 静态单例
        private static BaseState _instance;
        public static BaseState Instance => _instance ??= new BaseState();

        public static void Reset() { _instance = new BaseState(); }

        // 存/取物品
        public bool StoreItem(string assetPath, int count) { ... }
        public bool RetrieveItem(string assetPath, int count) { ... }
        public int GetItemCount(string assetPath) { ... }

        // 升级
        public bool UpgradeStash() { ... }
    }
}
```

### 4.2 BaseManager — 基地操作

**新文件**: `Assets/_Project/Scripts/Level/BaseManager.cs`

```csharp
namespace EscapeFromWork.Level
{
    public class BaseManager : MonoBehaviour
    {
        public static BaseManager Instance { get; private set; }

        // 打开储物箱 UI（复用 LootContainerUI）
        public void OpenStash(PlayerInventory playerInv) { ... }
        public void CloseStash() { ... }

        // 打开武器架 UI
        public void OpenWeaponRack(PlayerCombat combat) { ... }

        // 出发去 Raid
        public void LaunchRaid(int targetFloor) { ... }
    }
}
```

### 4.3 NPC 任务系统

**新文件**: `Assets/_Project/Scripts/Level/QuestSystem.cs`

```csharp
namespace EscapeFromWork.Level
{
    public enum QuestType
    {
        Collect,      // 收集 N 个物品
        Eliminate,    // 消灭 N 个敌人
        Extract,      // 从特定楼层提取物品
        Explore,      // 到达指定楼层并撤离
        StoryChain,   // 剧情任务链（多步）
    }

    public enum QuestStatus { Locked, Available, Active, Completed, TurnedIn }

    [System.Serializable]
    public class QuestDef
    {
        public string questId;
        public string title;
        public string description;
        public string giverName;       // NPC 名字
        public QuestType type;
        public QuestRequirement[] requirements;
        public QuestReward[] rewards;
        public string[] prerequisiteQuestIds;  // 前置任务
        public string[] unlockQuestIds;        // 完成後解锁
        public string loreText;                // 世界观文本
    }

    [System.Serializable]
    public class QuestRequirement
    {
        public string itemAssetPath;    // 收集类用
        public int count;
        public int targetFloor;         // 提取/探索类用
        public string enemyTag;         // 消灭类用
    }

    [System.Serializable]
    public class QuestReward
    {
        public string itemAssetPath;
        public int count;
        public int currencyAmount;      // 回形针
        public bool isStashUpgrade;
        public bool isNewWeapon;
    }

    public class QuestManager : MonoBehaviour
    {
        public static QuestManager Instance { get; private set; }
        public List<QuestDef> allQuests;
        public Dictionary<string, QuestStatus> questStates = new();

        public void AcceptQuest(string questId) { ... }
        public void CheckProgress() { ... }    // 每次撤离后调用
        public void TurnInQuest(string questId) { ... }
        public List<QuestDef> GetAvailableQuests() { ... }
        public List<QuestDef> GetActiveQuests() { ... }
    }
}
```

### 4.4 Phase 1 任务列表（8 个）

| ID | 任务 | 类型 | NPC | 前置 | 解锁 |
|----|------|------|-----|------|------|
| Q01 | 收集 5 卷打印纸 | Collect | 李阿姨 | — | Q02 |
| Q02 | 从 45 楼茶水间带回咖啡豆 | Extract | 李阿姨 | Q01 | Q03 |
| Q03 | 消灭 10 个 KPI 丧尸 | Eliminate | 老张 | Q02 | — |
| Q04 | 找到 27 楼服务器房的加密U盘 | Extract | 小王 | — | Q05 |
| Q05 | 黑掉 27 楼服务器（到达并撤离） | Explore | 小王 | Q04 | — |
| Q06 | 从 35 楼财务部带回年终奖名单 | Extract | 陈总 | — | Q07 |
| Q07 | 从 50 楼 CEO 保险柜拿到万能门禁卡 | Collect | 陈总 | Q06 | Q08 |
| Q08 | 到达 1 楼大堂并撤离 | Explore | 陈总 | Q07 | — |

### 4.5 公告板 UI

**新文件**: `Assets/_Project/Scripts/UI/QuestBoardUI.cs`

在基地场景中，E 键交互茶水间公告板：
- 左栏：可用任务列表
- 中栏：选中任务的详情（描述、要求、奖励、世界观文本）
- 右栏：当前进行中的任务

用 Text 组件拼，风格与 LootContainerUI 一致。

---

## Task 5: 武器架 UI

### 5.1 WeaponRackUI

**新文件**: `Assets/_Project/Scripts/UI/WeaponRackUI.cs`

```csharp
namespace EscapeFromWork.UI
{
    public class WeaponRackUI : MonoBehaviour
    {
        public GameObject panel;
        public Transform weaponGridParent;
        public float cellSize = 80f;

        public void Open(PlayerCombat combat, BaseState baseState) { ... }
        public void Close() { ... }
        public bool IsOpen { get; private set; }

        // 网格展示所有拥有的武器
        private void BuildGrid() { ... }

        // 选中的武器装配到 Loadout
        private void AssignToSlot(WeaponData weapon, GearSlot slot) { ... }
    }
}
```

---

## Task 6: 集成 — 修改 SceneWirer

### 6.1 新的 BuildScene() 流程

```csharp
public static void BuildScene()
{
    // 1. Clean
    // 2. 生成 FloorLayoutData
    // 3. 构建墙壁+地板+门 (from layout)
    // 4. 放置家具+搜刮容器 (FurniturePlacer)
    // 5. 放置敌人 (基于房间类型计数)
    // 6. 放置玩家 (楼梯入口处)
    // 7. 放置提取触发器 (消防梯处)
    // 8. 相机
    // 9. 灯光
    // 10. HUD
    // 11. BaseManager + QuestManager (只在茶水间层)
    // 12. FloorManager + GameManager
    // 13. Wire 武器+掉落表
    // 14. Save scene
}
```

### 6.2 新的 BuildRoom（替代 CreateWall）

每面墙 = 一个 Cube (宽×WallHeight×WallThickness 或 WallThickness×WallHeight×长)：

```csharp
static void BuildWall(string name, Vector3 position, float length, float thickness, 
                       float height, bool isXAxis, Material mat, Transform parent)
{
    var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
    w.name = name;
    w.transform.SetParent(parent);
    w.transform.position = position;
    if (isXAxis)
        w.transform.localScale = new Vector3(length, height, thickness);
    else
        w.transform.localScale = new Vector3(thickness, height, length);
    w.GetComponent<MeshRenderer>().sharedMaterial = mat;
    w.isStatic = true;
}
```

---

## Task 7: 验证 Checklist

- [ ] Build Scene 无错误
- [ ] 楼层 60×50，墙壁可见，走廊连通左下→右上
- [ ] 玩家移速 5 m/s，闪避 2m
- [ ] 每层布局不同（不同 seed）
- [ ] 茶水间只在 50F 出现（50%5==0）
- [ ] 家具按房间类型放置，搜刮表正确绑定
- [ ] 垃圾桶确有垃圾+偶尔惊喜
- [ ] 主管工位搜刮品质优于普通工位
- [ ] 基地储物箱可存/取，死亡不丢
- [ ] 武器架展示武器，可装配到 Loadout
- [ ] NPC 任务可接/追踪/交
- [ ] 完整提取循环：进场→战斗→搜刮→提取→基地
- [ ] 0 个编译错误

---

## 实现顺序

```
Task 1 (比例校准)          ← 先做，改动小
    ↓
Task 2 (走廊驱动布局生成器) ← 核心，最复杂
    ↓
Task 3 (家具+搜刮绑定)      ← 依赖 Task 2 的房间输出
    ↓
Task 4 (基地系统)           ← 独立，可与 Task 5 并行
Task 5 (武器架 UI)          ← 独立
    ↓
Task 6 (SceneWirer 集成)    ← 粘合所有
    ↓
Task 7 (验证)              ← 端到端测试
```

每完成一个 Task commit 一次。

---

## 关键文件清单

| 文件 | 操作 |
|------|------|
| `PlayerController.cs` | 修改：速度值 |
| `SceneWirer.cs` | 重写：使用布局生成器替代硬编码地板 |
| `SceneWirer.Loot.cs` | 修改：MkFurniture() 辅助方法 |
| `FloorLayoutData.cs` | 新建 |
| `FloorLayoutGenerator.cs` | 新建（300+ 行核心算法） |
| `FurnitureTemplate.cs` | 新建 |
| `RoomFurnitureSet.cs` | 新建 |
| `FurniturePlacer.cs` | 新建 |
| `BaseState.cs` | 新建 |
| `BaseManager.cs` | 新建 |
| `QuestSystem.cs` | 新建 |
| `QuestBoardUI.cs` | 新建 |
| `WeaponRackUI.cs` | 新建 |
