# Combat System

> **Status**: In Design
> **Author**: 用户 + Claude Code
> **Last Updated**: 2026-07-17
> **Implements Pillar**: 办公室即武器库 (#1), 赌徒时刻 (#3)

## Overview

Combat System 是 ESCAPE FROM WORK 中所有战斗行为的规格文档。它定义了玩家如何使用办公用品武器进行瞄准、射击、近战、闪避，规定了伤害计算、武器切换、掩体利用的完整规则。同时，它作为 Weapon System、Enemy AI、Player Movement 三个子系统之间的合同接口——武器如何造成伤害、敌人如何承受伤害、玩家如何规避伤害，都在本 GDD 中定义。

## Player Fantasy

Combat System 服务于两个设计支柱：

**"赌徒时刻"** — 第三人称越肩视角下，玩家在狭窄的办公走廊中移动，每一发订书钉都是有限资源。掩体后的探头射击、闪避翻滚的时机判断、蓄力近战的读秒——每次交火都是"再打一发还是撤退"的赌局。死亡意味着装备全丢。

**"办公室即武器库"** — 订书机手枪有正经的后座力和枪声反馈，键盘霰弹枪散射键帽时屏幕震动，KPI报表锤蓄满力砸下去时敌人被击飞。武器外观荒诞，但手感按正经射击游戏来做——玩家会真的觉得这些办公用品很猛。

视角：第三人称越肩（架构预留 FPS 切换可能）。节奏：战术性、紧张感驱动。

## Detailed Design

### Core Rules

#### 1. 瞄准

| 属性 | 值 |
|------|-----|
| 视角 | 第三人称越肩，鼠标水平旋转，15° 下俯角 |
| 准星 | 屏幕中心十字准星 |
| 自动瞄准 | 锁定最近带 "Enemy" 标签的目标，射向身体，无暴击 |
| 手动瞄准 | 自由瞄准，可瞄头（+50% 伤害）、瞄腿（减速 30%，持续 2 秒） |
| 切换方式 | 按住鼠标右键进入手动瞄准（消耗体力 8/秒），松开回到自动瞄准 |
| 自动瞄准锁定范围 | 35m，前方 120° 锥形 |

#### 2. 射击

| 属性 | 值 |
|------|-----|
| 输入 | 鼠标左键 |
| 弹药 | 每发消耗，打完需换弹（R 键） |
| 判定 | 投射物（非即时命中），有飞行时间 |
| 散射 | 手动瞄准散射×0.5，自动瞄准散射×1.0 |
| 射速 | 由武器数据决定 |

#### 3. 快速近战

| 属性 | 值 |
|------|-----|
| 输入 | V 键 |
| 行为 | 瞬发轻击，不切换当前武器槽 |
| 体力消耗 | 15 |
| 弹药 | 不消耗 |

#### 4. 近战武器槽

| 属性 | 值 |
|------|-----|
| 切换 | 鼠标滚轮切到近战槽位 |
| 轻击 | 鼠标左键点击 = 瞬间挥击（体力 15） |
| 蓄力 | 鼠标左键按住 = 蓄力（最多 2 秒），释放 = 重击（体力 30） |
| 蓄力中断 | 闪避或切换武器取消蓄力 |
| 弹药 | 不消耗 |

#### 5. 闪避

| 属性 | 值 |
|------|-----|
| 输入 | 空格键 |
| 体力消耗 | 25 |
| 方向 | 有 WASD 输入时向移动方向闪；静止时向后闪（远离瞄准方向） |
| 持续时间 | 0.2 秒 |
| 冷却 | 0.8 秒 |
| 距离 | ~2m（10 m/s × 0.2s） |
| 惩罚 | 自动瞄准锁定中闪避距离 -25% |

#### 6. 体力系统

| 属性 | 值 |
|------|-----|
| 最大体力 | 100 |
| 闪避消耗 | 25/次 |
| 快速近战消耗 | 15/次 |
| 蓄力近战消耗 | 30/次 |
| 手动瞄准消耗 | 8/秒 |
| 恢复速度 | 15/秒（停止消耗 0.5 秒后开始恢复） |
| 空体惩罚 | 不能闪避、不能近战、无法进入手动瞄准 |
| UI | HUD 体力条（血条下方），空体时闪烁 |

#### 7. 武器槽与切换

| 槽位 | 类型 | 切换方式 |
|------|------|---------|
| A 槽 | 主远程武器（办公用品改造） | 滚轮循环 |
| C 槽 | C 类武器（创意/特殊效果） | 滚轮循环 |
| 近战 | 近战武器（不耗弹药） | 滚轮循环 |
| Loadout | 1A + 1C + 1 近战 | 出发前在基地武器架选定 |

#### 8. 掩体

| 属性 | 值 |
|------|-----|
| 判定 | 靠近办公桌、文件柜等家具 1m 内 = 进入掩体 |
| 效果 | 玩家受击判定体积缩小 40% |
| MVP | 不实装自动贴墙动画，仅判定生效 |

#### 9. 伤害流程

```
武器开火 → 投射物飞行 → 命中碰撞体
   ↓
检查 tag:
   "Enemy" → 对 EnemyBase.TakeDamage(damage, source)
   "Player" → 对 PlayerHealth.TakeDamage(damage, source)
   else → 投射物销毁
   ↓
伤害计算:
   finalDamage = baseDamage × headshotBonus(1.0 or 1.5) × coverReduction(1.0 or 0.6)
   ↓
浮动伤害数字生成 (FloatingDamageText)
   ↓
目标检查死亡 → 死亡流程
```

### States and Transitions

玩家战斗状态：

| 状态 | 描述 | 可触发动作 |
|------|------|-----------|
| Idle | 默认，未进行战斗动作 | 射击、近战、闪避、瞄准、换弹 |
| Aiming (Manual) | 按住右键，体力持续消耗 | 射击、近战（中断瞄准）、闪避（中断瞄准）、换弹（中断瞄准） |
| Melee (Light) | 立即释放，单帧动画 | — |
| Melee (Charging) | 蓄力中，体力已扣除 30 | 释放重击、闪避取消、切换武器取消 |
| Dodging | 闪避中（0.2s） | 不可中断 |
| Reloading | 换弹动画中 | 不可中断（切武器或闪避中断换弹） |
| Dead | 死亡 | 不可操作 |

死亡状态：

| 属性 | 值 |
|------|-----|
| 触发 | HP ≤ 0 |
| 流程 | 播放死亡动画 → 掉落装备 → 生成尸体 + 工牌 → GameManager → Dead 状态 → 返回基地 |

### Interactions with Other Systems

#### 输入：Player Movement
- PlayerController 提供：Position、AimDirection、IsDodging、IsCrouching
- Combat 读取：输入状态、移动方向（用于闪避方向）

#### 输出：Weapon System
- 所有武器实现 WeaponBase 接口：Fire()、Reload()、CanFire()
- Combat 持有武器引用，负责调用 Fire() 和传递瞄准参数

#### 输入：Enemy AI
- 所有敌人实现 IDamageable：TakeDamage(amount, source)
- 敌人 tag 必须为 "Enemy" 以支持自动瞄准锁定

#### 输出：UI / HUD
- 血量、体力、弹药数 → HUDManager 轮询
- 浮动伤害数字 → FloatingDamageText.Spawn()
- 死亡事件 → DeathScreen + MemorialWall

#### 输出：Loot & Economy
- 敌人死亡掉落由 EnemyBase.Die() 处理
- 玩家死亡掉落由 PlayerHealth.Die() → DropEquipment() 处理

## Formulas

### 1. 伤害公式

```
finalDamage = baseDamage × headshotMultiplier × coverMultiplier
```

| 变量 | 符号 | 类型 | 范围 | 描述 |
|------|------|------|------|------|
| 基础伤害 | baseDamage | float | 5–200 | 来自 WeaponData |
| 暴击倍率 | headshotMultiplier | float | 1.0 or 1.5 | 身体=1.0，头部=1.5 |
| 掩体倍率 | coverMultiplier | float | 1.0 or 0.6 | 无掩体=1.0，掩体后=0.6 |

**输出范围**: 3–300
**示例**: 订书机手枪 baseDamage=15 → 身体=15，暴头=22.5，掩体后命中身体=9

### 2. 体力公式

```
stamina = clamp(stamina + delta, 0, 100)
```

| 变量 | 符号 | 类型 | 值 | 描述 |
|------|------|------|-----|------|
| 闪避消耗 | dodgeCost | float | 25 | 每次闪避 |
| 轻击消耗 | meleeLightCost | float | 15 | 每次快速近战 |
| 重击消耗 | meleeHeavyCost | float | 30 | 每次蓄力近战 |
| 瞄准消耗率 | aimRate | float | 8/s | 按住右键时按帧扣除 |
| 恢复开始延迟 | recoveryDelay | float | 0.5s | 停止消耗后等待 |
| 恢复速度 | recoveryRate | float | 15/s | 延迟结束后恢复 |

**示例**: 满体 100 → 闪避后 75 → 等 0.5s → 1 秒后恢复到 90

### 3. 暴击判定（手动瞄准）

| 命中部位 | 倍率 | 判定区域 |
|----------|------|---------|
| 头部 | ×1.5 | 敌人 capsule collider 顶部 20% |
| 身体 | ×1.0 | 其余 80% |
| 腿部 | ×1.0 + 减速 30% | 敌人 capsule collider 底部 30%（减速持续 2s） |

### 4. 闪避距离

```
dodgeDistance = dodgeSpeed × dodgeDuration × aimPenalty
```

| 变量 | 值 |
|------|-----|
| dodgeSpeed | 10 m/s |
| dodgeDuration | 0.2s |
| aimPenalty | 1.0（无自动锁定）\| 0.75（自动锁定中）|

**示例**: 无锁 = 2m，锁定中 = 1.5m

### 5. 散射

```
actualSpread = baseSpread × aimModeMultiplier
```

| 变量 | 来源 | 自动瞄准 | 手动瞄准 |
|------|------|---------|---------|
| baseSpread | WeaponData（0–15°） | ×1.0 | ×0.5 |

**示例**: 键盘霰弹 baseSpread=12° → 自动=12°，手动=6°

## Edge Cases

1. **零体力** → 无法闪避/近战/手动瞄准。UI 体力条闪烁。当前正在进行的蓄力被取消。
2. **零弹药** → 点击射击无反应。HUD 显示 0/N。自动提示换弹。
3. **空弹换弹** → R 键换弹。换弹中可闪避打断（保留已装填部分）。
4. **换弹中被攻击** → 换弹不可格挡，伤害正常计算。
5. **闪避撞墙** → 闪避路径被墙体阻挡时立即停止，不穿过墙壁。
6. **闪避中死亡** → 死亡判定优先，中断闪避，播放死亡动画。
7. **射击+近战同时输入** → 近战优先（V 键立即执行），射击输入被忽略。
8. **手动瞄准中体力耗尽** → 强制退出手动瞄准，回到自动瞄准模式。
9. **多个敌人都在自动锁定范围** → 锁最近的。该敌人死亡后自动切到下一个。
10. **武器切换中挨打** → 切换无动画延迟（即时），伤害正常计算。
11. **死亡时正在蓄力** → 蓄力取消，死亡流程优先。
12. **掩体边界模糊** → 半个身体在掩体内 = 掩体生效。判定用距离而非视线。
13. **闪避冷却中按空格** → 无反应。无提示（冷却 0.8s 太短，不值得 UI 反馈）。
14. **蓄力满 2 秒不释放** → 自动释放重击（防止无限蓄力）。
15. **武器数据缺失** → WeaponBase.Data == null 时不能开火。PlayerCombat 打印警告。
16. **敌人在掩体后** → 自动瞄准射身体（可能被掩体阻挡）。手动瞄准可瞄头（暴露部分）。投射物命中掩体 = 销毁（不穿墙）。

## Dependencies

| 系统 | 方向 | 类型 | 接口 |
|------|------|------|------|
| Player Movement | 上游 | Hard | Position, AimDirection, IsDodging, IsCrouching, 输入状态 |
| Weapon System | 上游 | Hard | WeaponBase.Fire(), Reload(), CanFire(), weaponData |
| Enemy AI | 上游 | Hard | IDamageable.TakeDamage(amount, source), tag "Enemy" |
| UI / HUD | 下游 | Hard | 血量、体力、弹药显示、浮动伤害数字、死亡界面 |
| Loot & Economy | 下游 | Soft | 敌人死亡掉落、玩家死亡掉落 |
| Death & Inheritance | 下游 | Hard | 死亡事件 → CharacterMemorial, 装备掉落 |

## Tuning Knobs

| 参数 | 默认值 | 安全范围 | 影响 |
|------|--------|---------|------|
| maxStamina | 100 | 80–150 | 体力上限 |
| dodgeCost | 25 | 15–35 | 闪避消耗 |
| meleeLightCost | 15 | 10–25 | 轻击消耗 |
| meleeHeavyCost | 30 | 20–45 | 重击消耗 |
| aimStaminaRate | 8/s | 5–15/s | 手动瞄准体力消耗率 |
| recoveryRate | 15/s | 10–25/s | 体力恢复速度 |
| recoveryDelay | 0.5s | 0.3–1.0s | 恢复开始等待时间 |
| headshotMultiplier | 1.5 | 1.2–2.0 | 暴头伤害倍率 |
| coverMultiplier | 0.6 | 0.4–0.8 | 掩体伤害倍率 |
| autoAimRange | 35m | 20–50m | 自动瞄准锁定距离 |
| autoAimConeAngle | 120° | 90–150° | 自动瞄准检测锥形 |
| dodgeSpeed | 10 m/s | 7–14 m/s | 闪避速度 |
| dodgeDuration | 0.2s | 0.15–0.3s | 闪避持续时间 |
| dodgeCooldown | 0.8s | 0.5–1.5s | 闪避冷却时间 |

## Visual/Audio Requirements

### VFX
- 枪口闪光：每次射击时武器枪口短暂白色/黄色闪光（位置：muzzlePoint）
- 投射物弹道：Staple/Keycap 弹头为小立方体，带 trail renderer
- 命中火花：投射物命中碰撞体时短促火花粒子
- 浮动伤害数字：命中后从命中点浮起，0.5s 后渐隐，白色（普攻）/ 黄色（暴头）
- 近战弧线：挥击时沿弧线方向短暂轨迹染变色

### Audio
- 射击音效：每把武器独立音效（订书机=金属撞击脆响，键盘=塑料碎响）
- 命中音效：区分身体命中（闷响）和头部命中（清脆咔嚓）
- 闪避音效：衣服摩擦/脚步滑音
- 蓄力充能音效：低沉的嗡声渐强

## UI Requirements

- HUD 体力条在血条下方，空体时闪烁红色
- 弹药数显示在右下角（当前弹夹 / 总弹药）
- 手动瞄准时准星变实心（自动时半透明）
- 交互提示在屏幕底部中央

## Acceptance Criteria

1. GIVEN 玩家体力满 100, WHEN 按空格闪避, THEN 体力变为 75, 玩家移动约 2m
2. GIVEN 玩家体力 = 0, WHEN 按空格/V/右键, THEN 无反应（不能闪避/近战/手动瞄准）
3. GIVEN 玩家按住右键, WHEN 体力消耗至 0, THEN 强制退出瞄准回到自动模式
4. GIVEN 自动瞄准锁定敌人, WHEN 按左键射击, THEN 投射物飞向敌人身体中心
5. GIVEN 手动瞄准敌人头部, WHEN 命中头部区域, THEN 造成 baseDamage × 1.5
6. GIVEN 玩家靠近办公桌 1m 内, WHEN 敌人射击命中, THEN 伤害 × 0.6
7. GIVEN 武器弹药 = 0, WHEN 按左键, THEN 不射击, HUD 提示换弹
8. GIVEN 近战武器蓄力 2 秒, WHEN 不释放, THEN 自动释放重击
9. GIVEN 玩家在闪避冷却中, WHEN 按空格, THEN 不触发闪避
10. GIVEN KPI 丧尸头部被订书机手枪命中 (15 × 1.5 = 22.5), THEN 浮动数字显示 "22.5" 并漂起消失

## Open Questions

1. **FPS 切换** — Phase 1 是否实现第一人称视角切换？架构已预留，但 MVP 只做第三人称。
2. **肢解系统** — 近战重击秒杀时是否需要肢体断裂效果？技术可行但 Phase 1 不做。
3. **敌人攻击玩家是否也走体力系统？** — 目前体力仅玩家侧。敌人闪避/格挡待设计。
