# Death & Inheritance System

> **Status**: In Design (Post-MVP)
> **Author**: Claude Code (/design-system)
> **Last Updated**: 2026-07-17
> **Implements Pillar**: 赌徒时刻 (#3), 下坠之旅 (#2)

## Overview

Death & Inheritance System 定义 ESCAPE FROM WORK 中角色死亡、尸体回收、装备继承和纪念墙的完整规则。这是搜打撤核心循环的"风险"侧——死亡是永久且有代价的，但前辈的遗产（基地、蓝图、安全楼层进度）可以由新角色继承。系统通过 tea room 幸存者团体的叙事框架，将 permadeath 转化为推进故事的动力而非惩罚。

## Player Fantasy

死亡时玩家应该感到"真实的损失"——装备没了，角色永远留在了那层楼。但紧接着，玩家应该感受到"传承的力量"——前辈带回来的 U 盘解了锁、修建的医疗角还能用、墙上多了一块纪念牌。选一个新角色，穿上备用装备，回到那层楼——这次你知道敌人在哪了。死亡不是结束，是一个新的开始，带着前辈的遗产。

## Detailed Design

### Core Rules

1. **Permadeath**: 角色 HP ≤ 0 → 永久死亡。该角色的装备留在死亡楼层。
2. **Inheritance**: 基地设施、蓝图解锁、安全楼层进度、储物箱内容 → 全部保留。
3. **Corpse Recovery**: 新角色可返回死亡楼层捡回前辈装备。尸体可能被保安搜过。
4. **Memorial Wall**: 每个死去的角色在茶水间墙上多一块纪念牌。
5. **Character Select**: 每次死亡后从幸存者中选新角色（外观可选，属性相同）。

### Death Types

| DeathType | 触发条件 | 丢失 | 保留 | 尸体回收 |
|-----------|----------|------|------|---------|
| SafeFloor | 在安全楼层死亡 | 本次搜刮的全部物资 + NPC 收尸小费 | 装备武器、基地进度 | NPC 同事帮忙收尸取回装备（付固定小费） |
| DangerFloor | 在危险楼层死亡 | 本次搜刮物资 + 身上装备的武器和消耗品 | 基地进度、保险柜内物品 | 新角色可返回捡尸（保安可能已搜走部分） |
| BossKill | 被 Boss 击杀 | 同 DangerFloor + 保险柜物品也会丢失 | 仅基地进度 | 尸体在 Boss 房间，回收难度最高 |
| ExtractionFail | 撤离中死亡 | 同 DangerFloor | 同 DangerFloor | 尸体在楼梯间/电梯附近 |

### Death Flow

```
1. PlayerHealth.TakeDamage() → HP ≤ 0
2. Die() 调用:
   a. 播放死亡动画（角色倒地）
   b. 判断 DeathType（安全楼层 / 危险楼层 / Boss / 撤离中）
   c. DropEquipment() — 生成 LooseLoot 在死亡位置
   d. 生成 Corpse GameObject + CharacterBadge（工牌）
   e. 触发 GameEvents.OnPlayerDeath(deathContext)
   f. GameManager 切换到 Dead 状态
3. DeathScreen 显示:
   a. 角色名 + 死亡楼层 + 死因
   b. 丢失装备列表
   c. 保留资源摘要
   d. 纪念墙预览
4. 玩家点击"选择新角色"→ CharacterSelect
5. 新角色出生在茶水间基地
```

### Corpse Recovery

| 参数 | 值 |
|------|-----|
| 尸体持续存在 | 永久（该 run 内） |
| 保安搜尸概率 | 30%（危险楼层）/ 60%（Boss 层） |
| 被搜走比例 | 随机 1-3 件装备 |
| 回收装备状态 | 完好（不需要修复） |
| 尸体地图标记 | 在楼层地图上标记（如果情报板已解锁） |
| 多人尸体 | 同楼层可有多具前人尸体 |

### 保险柜 (Safe Box)

| 属性 | 值 |
|------|-----|
| 位置 | 每层茶水间 |
| 容量 | 3×3 = 9 格 |
| 存取费 | 存入免费 / 取出每格 5 回形针 |
| 死亡不掉 | ✅ |
| 跨角色共享 | ❌（仅该角色可存取） |
| Boss 层死亡 | 保险柜内容也会丢失 |

### Character Selection

| 属性 | 值 |
|------|-----|
| 可选角色数 | 3-5 个幸存者 |
| 外观差异 | 牛/马（两种动物外观，纯 cosmetic） |
| 属性 | 完全相同（不做职业差异） |
| 初始装备 | 基地储备的备用装备（2-3 套） |
| 名字 | 随机生成或玩家自定义 |

### 装备恢复

| 途径 | 成本 | 条件 |
|------|------|------|
| 基地改造台重制 | 回形针 × baseWeaponCost | 已解锁蓝图的基础武器 |
| 尸体回收 | 0（需要到达尸体位置） | 尸体上的装备完好 |
| NPC 收尸（安全楼层） | 固定小费（50 回形针） | 仅安全楼层死亡 |
| 幸存者储备 | 免费 | 基地日常储备 2-3 套备用装备 |

### Memorial Wall

| 属性 | 值 |
|------|-----|
| 位置 | 茶水间墙面 |
| 每块纪念牌 | 角色名、死亡楼层、死因、带回物资总价值 |
| 交互 | 点击查看详情 |
| 持久性 | 永久（跨所有 run） |
| 排序 | 按死亡时间倒序 |
| 上限 | 无上限（滚动列表） |

## Formulas

### Death Drop Calculation

```
lostItems = inventory.backpackItems + inventory.equippedWeapons + inventory.equippedConsumables
if deathType == SafeFloor:
    lostItems = inventory.raidLootOnly (本次搜刮物资)
    retainedItems = inventory.equippedWeapons + inventory.permanentItems

securityLootCount = Random.Range(1, 4)  // 被保安搜走 1-3 件
if Random.value < securitySearchChance:
    corpseItems.remove(random(securityLootCount))
```

### Insurance Cost (未来功能)

```
insuranceCost = totalEquipmentValue × 0.15
insurancePayout = totalEquipmentValue × 0.50  // 死亡后 NPC 补偿 50% 价值
```

### Memorial Value

```
memorialValue = sum(item.baseValue for item in extractedItems)
// 只在成功撤离时记录；死亡时不记录带回价值
```

## Edge Cases

1. **Boss 层死亡 → 保险柜内容丢失**: Boss 层死亡惩罚最重，保险柜也不安全。
2. **尸体在未探索区域**: 新角色需先到达该楼层并找到尸体位置（小地图标记）。
3. **尸体被保安搜过后消失**: 保安不移动尸体，只取走部分装备。尸体始终在原位。
4. **连续死亡（多个角色死在同一层）**: 每具尸体独立处理，各自有独立工牌。
5. **最后一个幸存者死亡**: 游戏结束。重新开始（保留基地和纪念墙？ → 保留）。
6. **在撤离动画中死亡**: 判定为 ExtractionFail，装备在楼梯间掉落。
7. **保险柜满**: 存入时提示"保险柜已满"，需先取出部分物品。
8. **新角色回到死亡楼层前该楼层被清理**: 尸体不受楼层状态影响，始终存在。
9. **所有幸存者都已用过**: 名字循环 + "(第二代)" 后缀。
10. **纪念墙超过 100 块**: 分页加载，每页 20 块。

## Dependencies

| 系统 | 方向 | 类型 | 接口 |
|------|------|------|------|
| Combat System | 上游 | Hard | PlayerHealth.Die() → OnPlayerDeath 事件 |
| Player Inventory | 上游 | Hard | 背包内容、装备列表 → 掉落计算 |
| Loot & Economy | 上游 | Hard | 装备价值计算、保险柜存取 |
| Base Building | 上游 | Hard | 基地设施状态、蓝图解锁列表 → 继承 |
| Floor Generation | 上游 | Soft | 尸体位置（楼层坐标） |
| UI / HUD | 下游 | Hard | DeathScreen、CharacterSelect、MemorialWall |
| Save/Load | 下游 | Hard | 纪念墙持久化、保险柜状态 |

## Tuning Knobs

| 参数 | 默认值 | 安全范围 | 描述 |
|------|--------|---------|------|
| securitySearchChance | 0.3 (危险) / 0.6 (Boss) | 0.0–1.0 | 保安搜尸概率 |
| securityLootCount | 1–3 | 1–5 | 被搜走装备数量 |
| safeBoxCapacity | 9 (3×3) | 4–16 | 保险柜容量（格） |
| safeBoxWithdrawCost | 5 回形针/格 | 0–20 | 保险柜取出手续费 |
| npcRecoveryFee | 50 回形针 | 20–200 | NPC 收尸小费 |
| reserveGearSets | 2–3 | 1–5 | 幸存者团体备用装备套数 |
| memorialPageSize | 20 | 10–50 | 纪念墙每页显示数 |
| insuranceCostRate | 0.15 | 0.05–0.30 | 保险费率（未来） |
| insurancePayoutRate | 0.50 | 0.30–0.80 | 保险赔付率（未来） |

## Visual/Audio Requirements

### VFX
- 死亡动画：角色倒地 + 屏幕红色渐暗（1.5s）
- 尸体外观：角色模型变为灰色 + 上方微弱光柱（可搜刮提示）
- 纪念墙新增：新工牌短暂金色闪烁（2s）

### Audio
- 死亡音效：心跳停止 + 低沉嗡声
- 纪念墙：新增工牌时播放短促钟声
- 角色选择：翻页/切换音效
- 尸体回收：拾取装备的"叮"声

## UI Requirements

- DeathScreen：全屏覆盖，暗色背景，信息居中
- CharacterSelect：横向排列 3-5 个角色卡片（牛/马外观）
- MemorialWall：垂直滚动列表，每块工牌显示名字/楼层/死因/价值
- Corpse loot 面板：复用 LootContainerUI（三栏面板）

## Acceptance Criteria

1. GIVEN 玩家在危险楼层 HP ≤ 0, WHEN 死亡流程完成, THEN 装备掉落在尸体旁, 基地进度保留
2. GIVEN 玩家在安全楼层 HP ≤ 0, WHEN 死亡流程完成, THEN 装备由 NPC 收回 (付 50 回形针小费)
3. GIVEN 前人尸体在楼层中, WHEN 新角色到达尸体位置按 E, THEN 打开搜刮面板显示尸体装备
4. GIVEN 尸体被保安搜过 (30% 概率), WHEN 新角色打开尸体, THEN 1-3 件装备缺失
5. GIVEN 新角色选择完成, WHEN 进入基地, THEN 所有设施和蓝图继承自前辈
6. GIVEN 死亡事件触发, WHEN 新角色访问茶水间, THEN 纪念墙新增一块工牌
7. GIVEN 玩家在 Boss 层死亡, WHEN 保险柜物品检查, THEN 保险柜内容丢失
8. GIVEN 所有幸存者已死亡, WHEN 最后一人死亡, THEN 游戏结束, 保留纪念墙

## Open Questions

1. **Game Over 后是否重新开始?** — 保留纪念墙和基地完全重建 vs 清零重来?
2. **保险系统** — 是否 MVP 就做保险？还是 Post-MVP?
3. **尸体持续时间** — 是否跨 session？（当前设计：仅当前 run）
4. **多人联机时尸体** — 其他玩家的尸体是否可见/可搜刮？
