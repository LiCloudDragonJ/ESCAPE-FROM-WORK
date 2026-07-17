# Quest System

> **Status**: In Design (Post-MVP)
> **Author**: Claude Code (/design-system)
> **Last Updated**: 2026-07-17
> **Implements Pillar**: 下坠之旅 (#2), 活着的建筑 (#4)

## Overview

Quest System 定义 ESCAPE FROM WORK 中 NPC 任务、支线剧情和奖励发放的完整规则。任务系统为玩家提供有结构的探索目标——从"帮我找 5 个回形针"到"去 35F 财务部拿一份合同原件"。任务由茶水间幸存者 NPC 发放，完成后推进剧情并解锁稀有物品。任务系统是叙事系统的基础设施——故事通过任务传递。

## Player Fantasy

茶水间里有其他幸存者。有人想找回丢失的工牌，有人想知道某个同事的下落，有人想收集足够多的回形针买通保安。"帮我个忙"——你听到这句话时，应该感到这是一次有意义的选择，而不是无聊的 fetch quest。任务不是强制的——但它们让你在下楼的过程中有更多目的。

## Detailed Design

### Core Rules

1. 4 个 NPC 幸存者，每人有独立任务链。
2. 任务按楼层推进——NPC 的任务随楼层进度解锁。
3. 任务奖励：回形针、稀有物品、蓝图解锁、NPC 好感度。
4. 任务进度跨角色持久化（前辈接的任务，新角色可继续）。
5. 任务状态：未解锁 → 可接 → 进行中 → 可交 → 已完成。

### NPC 幸存者

| NPC | 身份 | 位置 | 任务链 |
|-----|------|------|--------|
| 老王 (Lao Wang) | IT 部老员工 | 50F 茶水间 | "数据恢复" — 收集 U 盘 + 电子元件 |
| 小美 (Xiao Mei) | 前台接待 | 41F 茶水间（清层后出现） | "失踪同事" — 寻找幸存者 + 回收遗物 |
| 大刘 (Da Liu) | 保安（叛变） | 21F 茶水间（击败经理后） | "内部情报" — 破坏安保系统 + 标记弱点 |
| 老周 (Lao Zhou) | 财务总监 | 35F 茶水间（清层后出现） | "账本追踪" — 收集财务文件 + 揭露贪污 |

### 任务结构

```csharp
class QuestData : ScriptableObject {
    string questId;           // "Q_WANG_001"
    string questName;         // "第一个 U 盘"
    NPC giver;                // 老王
    int minFloorUnlock;       // 50（楼层到达此层后解锁）
    QuestRequirement[] prerequisites; // 前置任务
    QuestObjective[] objectives;     // 任务目标
    QuestReward[] rewards;           // 奖励
    string[] completionDialog;       // 完成对话
}
```

### 任务类型

| 类型 | 描述 | 示例 |
|------|------|------|
| CollectItem | 收集指定物品 | "带回 5 个 U 盘" |
| KillEnemy | 击杀指定敌人 | "消灭 10 个 KPI 丧尸" |
| ReachFloor | 到达指定楼层 | "到达 27F IT 部" |
| FindBadge | 回收前人遗物工牌 | "找回上一个角色的工牌" |
| InteractObject | 交互指定物件 | "在 35F 金库使用钥匙" |
| EscortNPC | 护送 NPC 到指定位置 | "带老王去 27F 服务器房" |

### 老王任务链 — "数据恢复"

| ID | 任务 | 解锁楼层 | 目标 | 奖励 |
|----|------|---------|------|------|
| Q_WANG_001 | "第一个 U 盘" | 50F（初始） | 收集 3 个 U 盘 | 50 回形针 + 改造台折扣 20% |
| Q_WANG_002 | "服务器密码" | 40F | 到达 27F IT 部 + 交互服务器 | 100 回形针 + 解锁情报终端（50F 功能） |
| Q_WANG_003 | "全部数据" | 20F | 收集 10 个 U 盘 + 5 个电子元件 | 200 回形针 + 蓝图：投影仪射线枪伤害 +20% |
| Q_WANG_004 | "真相" | 10F | 到达 10F 档案室 + 读取关键文件 | 500 回形针 + 解锁隐藏结局线索 |

### 小美任务链 — "失踪同事"

| ID | 任务 | 解锁楼层 | 目标 | 奖励 |
|----|------|---------|------|------|
| Q_MEI_001 | "前台的伙伴" | 41F（清层后） | 回收 1 块前人遗物工牌 | 50 回形针 + 解锁新角色外观 |
| Q_MEI_002 | "工牌收集者" | 30F | 回收 5 块工牌（任意来源） | 100 回形针 + 工牌售出价格 +30% |
| Q_MEI_003 | "最后的归宿" | 15F | 回收 1 块特殊工牌（Boss 掉落） | 200 回形针 + 保险柜容量 +1 行 |

### 大刘任务链 — "内部情报"

| ID | 任务 | 解锁楼层 | 目标 | 奖励 |
|----|------|---------|------|------|
| Q_LIU_001 | "保安的弱点" | 21F（击败经理后） | 击杀 5 个保安 | 100 回形针 + 对保安伤害 +15% |
| Q_LIU_002 | "监控死角" | 15F | 到达 3F 保安部 + 交互监控台 | 200 回形针 + 解锁监控室功能（3F） |
| Q_LIU_003 | "系统的后门" | 5F | 交互 5 个安保终端（不同楼层） | 300 回形针 + 保安刷新率 -20% |

### 老周任务链 — "账本追踪"

| ID | 任务 | 解锁楼层 | 目标 | 奖励 |
|----|------|---------|------|------|
| Q_ZHOU_001 | "失踪的资金" | 35F（清层后） | 收集 3 个财务报告（Luxury 类掉落） | 100 回形针 + 回形针掉落量 +20% |
| Q_ZHOU_002 | "CEO 的秘密账户" | 20F | 到达 15F 法务部 + 交互合同柜 | 200 回形针 + 解锁保险柜免除手续费（35F 功能） |
| Q_ZHOU_003 | "钱去哪里了" | 10F | 收集 5 个股权证书（Legendary 稀有度） | 500 回形针 + 揭秘 CEO 契约资金来源 |

## Formulas

### 任务奖励计算

```
baseReward = objectiveDifficulty × floorLevel × rewardMultiplier
paperclipReward = baseReward × paperclipRate
itemReward = RandomFromLootTable(rewardLootTable, luckBonus)

objectiveDifficulty:
  CollectItem = 1.0 (per item)
  KillEnemy = 1.5 (per enemy)
  ReachFloor = 2.0
  FindBadge = 3.0
  InteractObject = 2.5
  EscortNPC = 4.0
```

### NPC 好感度

```
好感度范围: 0–100
初始好感: 0（老王初始 10）
完成任务: +10 好感
交付特殊物品: +5 好感
攻击 NPC: -50 好感（不可逆，失去该 NPC 所有任务）
好感 ≥ 50: 交易折扣 10%
好感 ≥ 80: 解锁隐藏任务
```

### 任务进度持久化

```
任务状态保存字段:
  questId: string
  status: enum { Locked, Available, Active, ReadyToTurnIn, Completed }
  progress: Dictionary<objectiveIndex, int>  // 每个目标的当前进度
  startedBy: characterName                    // 哪个角色接的任务
```

## Edge Cases

1. **前辈接了任务但死亡**: 任务进度保留 → 新角色可继续。但"FindBadge"类任务的目标工牌 = 前辈尸体上的工牌。
2. **任务目标物品被卖出**: 收集类任务的物品必须"在背包中"才能交。卖出后需要重新收集。
3. **NPC 所在楼层变为非安全**: NPC 暂时不可交互（躲起来了），需重新清扫楼层。
4. **任务目标楼层还未到达**: 任务可以接，但目标不可达。UI 显示"需要先到达 XX 层"。
5. **两个任务冲突**（同一物品被两个任务需要）: 物品优先计入先接的任务。后接的任务需额外收集。
6. **EscortNPC 任务中 NPC 死亡**: 任务失败但可重接（NPC 回到原位置）。好感 -5。
7. **所有任务完成后**: NPC 提供无限重复的日常任务（如"收集 10 个回形针"→ 小额奖励）。

## Dependencies

| 系统 | 方向 | 类型 | 接口 |
|------|------|------|------|
| Floor Generation | 上游 | Hard | 楼层号（解锁条件）、特殊房间位置（任务目标） |
| Loot & Economy | 上游 | Hard | 物品掉落（任务收集品）、奖励发放 |
| Player Inventory | 上游 | Hard | 背包内容检查（任务物品计数） |
| Death & Inheritance | 上游 | Hard | 任务进度跨角色持久化 |
| Narrative System | 下游 | Soft | 任务完成对话 → 推进叙事（信息发现弧线） |
| UI / HUD | 下游 | Hard | 公告板 UI、任务追踪 HUD |
| Save/Load | 下游 | Hard | 任务状态持久化 |

## Tuning Knobs

| 参数 | 默认值 | 安全范围 | 描述 |
|------|--------|---------|------|
| baseRewardMultiplier | 1.0 | 0.5–2.0 | 全局任务奖励倍率 |
| favorPerQuest | 10 | 5–20 | 每完成任务好感增加 |
| favorCap | 100 | 50–200 | 好感度上限 |
| discountThreshold | 50 | 25–75 | 交易折扣所需好感 |
| hiddenQuestThreshold | 80 | 60–95 | 隐藏任务所需好感 |
| favorLossOnNPCDeath | 50 | 30–100 | NPC 死亡好感扣除 |
| dailyRepeatQuestReward | 0.3 × normal | 0.1–1.0 | 日常重复任务奖励倍率 |

## Visual/Audio Requirements

### VFX
- 任务完成：屏幕短暂金色边框 + 任务完成标志
- 新任务解锁：公告板上新任务条闪烁
- NPC 对话气泡：NPC 头顶出现"!"标记

### Audio
- 接任务：纸张翻页/接收音效
- 完成任务：短促胜利音效 + 回形针计数音
- NPC 好感提升：NPC 语音短句（积极回应）

## UI Requirements

- **公告板**（茶水间）：3 栏面板——左=可用任务 / 中=任务详情（目标+进度+奖励）/ 右=进行中任务
- **任务追踪 HUD**（战斗中）：屏幕右侧显示当前追踪任务的目标和进度（可选开启/关闭）
- **NPC 对话**（交互时）：对话框 + 人物头像 + 任务选项
- **任务完成通知**：屏幕顶部居中弹出（2s 后自动消失）

## Acceptance Criteria

1. GIVEN 玩家与老王对话, WHEN 老王有可用任务, THEN 显示任务详情和奖励
2. GIVEN 玩家接受 Q_WANG_001, WHEN 背包中有 3 个 U 盘, THEN 任务标记为 ReadyToTurnIn
3. GIVEN 任务 ReadyToTurnIn, WHEN 玩家与老王对话, THEN 获得奖励 + 好感 +10 + 解锁下一任务
4. GIVEN 玩家死亡后选新角色, WHEN 查看公告板, THEN 进行中的任务保留进度
5. GIVEN NPC 好感 ≥ 50, WHEN 与 NPC 交易, THEN 物品价格 10% 折扣
6. GIVEN EscortNPC 任务进行中, WHEN NPC 被敌人击杀, THEN 任务失败可重接 + 好感 -5
7. GIVEN 未到达目标楼层, WHEN 接受跨层任务, THEN 任务显示"需要到达 XXF"

## Open Questions

1. **NPC 是否有 3D 模型** — MVP 用菜单 UI 还是 3D 角色站在茶水间里？
2. **NPC 语音** — 是否需要配音？还是纯文字？
3. **隐藏任务触发条件** — 除了好感度，是否需要其他条件（如特定楼层安全、特定 Boss 击败）？
4. **日常任务刷新** — 重复任务多久刷新一次？（每次进入基地 / 每天 / 每次死亡后）
