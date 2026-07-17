# Save/Load & Meta-Progression

> **Status**: In Design (Post-MVP)
> **Author**: Claude Code (/design-system)
> **Last Updated**: 2026-07-17

## Overview

Save/Load System 负责游戏所有持久化状态的序列化和反序列化。存储格式为本地 JSON 文件。保存触发点：撤离成功、角色死亡（仅元进度）、退出游戏、自动保存（每5分钟）。元进度（基地、纪念墙、任务、蓝图、安全楼层）跨角色持久化；角色状态（装备、背包、HP）仅当前角色有效。单存档槽，PC 本地存储。

## Player Fantasy

玩家不应该感受到"存档系统"的存在——它应该在幕后安静工作。退出游戏再回来，茶水间还是那个茶水间，纪念墙上还是那些名字，你的蓝图和基地进度完好无损。死亡了？基地还在，但你的装备没了——这是搜打撤的代价。

## Detailed Design

### Core Rules

1. 单存档槽：`save_01.json` + `autosave.json`（自动备份）
2. JSON 明文存储，不加密（MVP）
3. 战斗中有敌人在追击/攻击状态时禁止保存
4. 场景切换前自动保存
5. 损坏存档 → 提示"存档已损坏，是否删除并重新开始？"

### 保存数据结构

```csharp
SaveData {
    // 运行元数据
    saveVersion, saveDate, runSeed, currentFloor, playTimeSeconds

    // 基地状态（跨角色持久化）
    baseFloorNumber, facilityLevels[], baseInvestedResources[]
    stashContents[]          // { itemDataId, stackCount }
    unlockedBlueprints[]     // 已解锁的可制作武器
    clearedFloors[]          // 安全楼层列表

    // 纪念墙（永久，跨 run）
    memorialWall[]           // { name, floor, cause, value, date }

    // 任务进度（跨角色持久化）
    questStates[]            // { questId, status, objectiveProgress }

    // 工牌收集（永久 flag）
    collectedBadges[]

    // 角色状态（仅当前角色，死亡时丢失）
    currentCharacterName, currentCharacterHP
    characterInventory[]     // 背包内容
    equippedWeaponIds[]      // A/C/Melee
    ammoReserve[]            // { ammoType, count }
}
```

### 保存触发

| 触发 | 时机 | 内容 |
|------|------|------|
| 撤离成功 | 玩家通过楼梯间/消防通道/电梯离开 | 完整保存（角色+元进度） |
| 角色死亡 | PlayerHealth.Die() 完成 | 仅元进度（基地/纪念墙/任务/蓝图/楼层） |
| 退出游戏 | Application.Quit() / Alt+F4 | 完整保存（通过 OnApplicationQuit） |
| 自动保存 | 每 5 分钟计时器 | 完整保存到 autosave.json |

### 加载流程

```
SceneBootstrap.Awake()
  → 检查 save_01.json 是否存在
    → 存在：加载 → 验证 saveVersion → 进入 Base 状态
    → 不存在：新游戏 → 生成 runSeed → 进入 50F 序章
```

### 存档位置

```
Windows: %USERPROFILE%/AppData/LocalLow/ESCAPE FROM WORK/saves/
  save_01.json    — 主存档
  autosave.json   — 自动备份
```

## Formulas

### 存档大小估算

```
单条纪念墙记录: ~200 bytes
单个物品: ~80 bytes (itemDataId + count)
单个任务状态: ~100 bytes
武器 ID: ~40 bytes each

典型存档大小:
  50 条纪念墙 + 80 格储物箱 + 16 格背包 + 14 任务 + 3 武器 + 30 蓝图
  ≈ 10KB + 6.4KB + 1.3KB + 1.4KB + 0.12KB + 2.4KB ≈ 22KB
```

## Edge Cases

1. **存档写入中途崩溃** → 先写入临时文件 `save_01.tmp`，写入完成后原子重命名为 `save_01.json`。
2. **旧版本存档** → `saveVersion != currentVersion` → 提示"存档版本不兼容"。
3. **存档文件被手动篡改** → JSON 解析失败 → 提示"存档已损坏"。
4. **磁盘空间不足** → 捕获 IOException → 提示"无法保存：磁盘空间不足"。
5. **自动保存和手动保存同时触发** → 互斥锁（lock），后者等待前者的文件写入完成。
6. **无存档时加载** → 直接进入新游戏流程，不报错。

## Dependencies

| 系统 | 方向 | 类型 | 接口 |
|------|------|------|------|
| Base Building | 上游 | Hard | facilityLevels, stashContents, unlockedBlueprints |
| Death & Inheritance | 上游 | Hard | memorialWall, clearedFloors |
| Quest System | 上游 | Hard | questStates |
| Player Inventory | 上游 | Hard | characterInventory, ammoReserve |
| Floor Generation | 上游 | Hard | currentFloor, clearedFloors |

## Tuning Knobs

| 参数 | 默认值 | 描述 |
|------|--------|------|
| autoSaveInterval | 300s | 自动保存间隔 |
| saveSlotCount | 1 (MVP) | 存档槽数量 |
| maxSaveFileSize | 1MB | 存档文件大小上限 |

## Acceptance Criteria

1. GIVEN 玩家在基地, WHEN 退出游戏, THEN 基地/储物箱/纪念墙/任务进度完整保存
2. GIVEN 存档文件存在, WHEN 启动游戏, THEN 恢复所有持久化状态
3. GIVEN 角色死亡, WHEN 死亡流程完成, THEN 基地/纪念墙/任务进度保存, 装备不保存
4. GIVEN 撤离成功, WHEN 到达基地, THEN 完整保存（角色+元进度）
5. GIVEN 存档文件损坏, WHEN 加载, THEN 提示错误，提供"重新开始"选项

## Open Questions

1. **多存档槽** — Post-MVP 是否需要多个存档槽（不同角色/不同 run）？
2. **云存档** — 是否需要 Steam Cloud 或其他云同步？
3. **存档加密** — 是否需要简单的反篡改措施？
