# Enemy AI System

> **Status**: In Design
> **Author**: 用户 + Claude Code
> **Last Updated**: 2026-07-17
> **Implements Pillar**: 赌徒时刻 (#3), 活着的建筑 (#4)

## Overview

Enemy AI System 定义 ESCAPE FROM WORK 中所有敌人的行为、属性和刷新规则。敌人分为三类——普通怪物（4 种）、安保（2 种）、Boss（4 种）。所有敌人实现 IDamageable 接口，由 EnemyData ScriptableObject 驱动。越底层的敌人战力越高——职级越低、怨念越深。

## Player Fantasy

敌人是压迫性办公室文化的具象化。KPI 丧尸是被绩效压垮的员工——他们不是"邪恶的"，他们是悲剧的。PPT 怨灵改了 47 版 PPT 后精神崩溃。会议恶魔永无止境地开会——现在他的结界就是你的牢笼。玩家面对它们时应该感到：这些也曾是和你一样的打工人。战斗是残酷的，但敌人是让人唏嘘的。

## Detailed Design

### Core Rules

1. 所有敌人实现 IDamageable，由 Combat System 调用 TakeDamage()。
2. 敌人生成由 EnemySpawner 按 FloorManager 的配置执行。
3. 敌人死亡由 EnemyBase.Die() 处理：播放死亡动画 → 掉落物品 → 销毁。
4. 楼层越低敌人越强（职级-战力反比）。

### 普通怪物

| 敌人 | 出现楼层 | HP | 伤害 | 移速 | 攻击方式 | 前身 |
|------|----------|-----|------|------|---------|------|
| KPI 丧尸 | 全楼层 | 60 | 12 | 2 m/s | 慢速近战 | 绩效压垮的员工 |
| PPT 怨灵 | 市场部/会议室 | 40 | 15(远程) | 3 m/s | 远程 PPT 弹幕 | 改了 47 版的市场人 |
| 邮件幽灵 | 开放办公区 | 30 | 20(自爆) | 4 m/s | 群体出现，死亡自爆 | 3000 封未读的行政 |
| 会议恶魔 | 管理层 | 80 | 18 | 2.5 m/s | 召开放慢速结界 | 永无止境的中层 |
| 打印机故障怪 | 办公区/打印室 | 50 | 10(远程) | 1.5 m/s | 喷射墨粉（致盲 1.5s） | 卡纸卡到崩溃的文员 |
| 饮水机漏电丧尸 | 茶水间/走廊 | 70 | 20(带电) | 2 m/s | 近战命中减速 2s | 被漏水+漏电害惨的维修工 |
| 午睡魔 | 休息区/茶水间 | 45 | 25 | 5 m/s(惊醒后) | 休眠→惊醒后极速冲刺 | 加班七天没睡的实习生 |
| 茶水间老鼠群 | 茶水间/储藏室 | 15×5只 | 5×5 | 6 m/s | 小型快速群体（5只） | 真实老鼠，因为茶水间太脏了 |

### 安保

| 敌人 | 刷新 | HP | 伤害 | 移速 | 特殊 |
|------|------|-----|------|------|------|
| 保安 | 全楼层随机 | 100 | 15 | 3 m/s | 高防、手电筒致盲（3s） |
| 精英保安队长 | 保安部/低层 | 200 | 25 | 2.5 m/s | 盾+电棍、冲锋技能、转身慢 |

### Boss

| Boss | 楼层 | HP | 阶段 | 技能 |
|------|------|-----|------|------|
| PPT 怨灵 Boss | 41F | 400 | 1 | 强化 PPT 弹幕 + 召幻灯片分身(2个) |
| 经理 | 21F | 600 | 2 | 一阶段正常 / 半血变身巨型怪兽(×2伤害, ×0.5移速) |
| 精英保安队长(Boss) | 3F | 800 | 1 | 召唤增援(3保安)、监控预判(闪避你的瞄准方向) |
| CEO | 1F | 1200 | 3 | 裁员通知(范围秒杀)、企业文化洗脑(控制反转)、加班轮回(减速) |

### 怪物随机变体

每个生成的敌人有 30% 概率获得一个随机变体。变体通过 EnemyData 中的 `variantAffix` 字段配置：

| 变体 | 效果 | 视觉 | 概率权重 |
|------|------|------|---------|
| Elite(精英) | HP×1.5, 伤害×1.3, 稀有掉落概率×2 | 体型略大、材质金色边缘 | 8 |
| Swift(迅捷) | 移速×1.4, HP×0.8, 攻击间隔-30% | 身形瘦长、移动时拖影 | 8 |
| Tanky(坦克) | HP×2.0, 移速×0.7, 不可打断 | 体型肥胖、材质暗色 | 8 |
| Explosive(爆炸) | 死亡自爆=原HP×0.2 AOE伤害(3m半径) | 材质暗红色、身体闪烁 | 5 |
| Regenerating(再生) | 每秒回复 HP×0.02, 最大HP×0.7 | 材质绿色脉动 | 5 |
| Normal(普通) | 无变体 | 默认外观 | 66 |

变体判定流程：
```
生成敌人 → roll 0-100:
  < 30 → 从变体表加权随机选一个（排除 Normal）
  ≥ 30 → Normal
```

### 行为状态机

```
           ┌──────────────────────────────────┐
           │                                  │
           ▼                                  │
  ┌──────────┐   detect    ┌────────┐  lose   │
  │  Idle/   │ ─────────→ │ Chase  │ ────────┘
  │  Patrol  │             └───┬────┘
  └──────────┘                 │ in range
       ▲                       ▼
       │ reset            ┌────────┐
       │                  │ Attack │
       │                  └───┬────┘
       │                      │ HP ≤ 0
       │                      ▼
       │                ┌────────┐
       └────────────────│  Dead  │
                        └────────┘
```

### 检测参数

| 参数 | 值 |
|------|-----|
| detectionRange | 15m（看到） / 8m（听到枪声/脚步声） |
| detectionAngle | 120°（前方锥形） |
| chaseRange | 30m（超过就回到巡逻） |
| attackRange | 1.5m（近战）/ 10m（远程） |
| attackCooldown | 1.5s |

### 掉落规则

- 必掉：对应敌人工牌（品级随楼层变化）+ 1–3 个回形针
- 概率掉：从 EnemyData.possibleDrops 中按 dropChance 随机
- Boss 必掉：特殊工牌（永久进度）+ 稀有武器/物品

## Formulas

### 楼层战力缩放

```
enemyHP     = baseHP    × (1 + (50 - floorNumber) × 0.03)
enemyDamage = baseDamage × (1 + (50 - floorNumber) × 0.02)
enemySpeed  = baseSpeed  × (1 + (50 - floorNumber) × 0.01)

floorNumber: 50=顶层(×1.0), 1=底层(×2.47 HP, ×1.98 伤害, ×1.49 移速)
```

### 掉落概率

```
guaranteedDrop: 100%（工牌 + 回形针）
possibleDrop[i]: dropChance（0–1.0，独立随机）
```

### 状态机转换

```
Idle → Patrol: 随机计时器（3–8s）
Patrol → Chase: detectionRadius 内发现玩家
Chase → Attack: attackRange 内
Chase → Patrol: 玩家超出 chaseRange
Attack → Chase: 玩家退出 attackRange
Any → Dead: HP ≤ 0
```

## Edge Cases

1. **死亡后仍被锁定** → 自动瞄准检测 tag "Enemy"，死亡后 tag 移除 → 自动切目标。
2. **多个敌人同时攻击** → 各自独立计算伤害，无连击加成。
3. **敌人在墙后** → 射线检测遮挡。被遮挡 = 不可见 = 不触发 Chase。
4. **敌人掉落物卡墙** → 掉落位置向外偏移 0.5m，避免穿墙。
5. **Boss 转换阶段时死亡** → 死亡判定优先，跳过阶段转换。
6. **敌人被 C 类武器定身时发现新玩家** → 定身优先，定身结束后重新检测。

## Dependencies

| 系统 | 方向 | 类型 | 接口 |
|------|------|------|------|
| Core (IDamageable) | 上游 | Hard | TakeDamage(amount, source) |
| Data (EnemyData) | 上游 | Hard | ScriptableObject：HP、伤害、掉落表 |
| Floor Generation | 上游 | Hard | 提供 spawn zones |
| Combat System | 上游 | Hard | 伤害源、浮动伤害数字 |
| Loot & Economy | 下游 | Soft | 死亡掉落物品 |
| UI / HUD | 下游 | Soft | Boss HP 条 |

## Tuning Knobs

| 参数 | 默认值 | 安全范围 | 描述 |
|------|--------|---------|------|
| detectionRange | 15m | 8–30m | 视觉检测距离 |
| detectionAngle | 120° | 60–180° | 视野锥形角度 |
| hearRange | 8m | 5–15m | 听觉检测距离 |
| chaseRange | 30m | 15–50m | 追击最大距离 |
| attackRange | 1.5m/10m | 0.5–20m | 攻击距离（近战/远程） |
| attackCooldown | 1.5s | 0.5–5s | 攻击间隔 |
| patrolRadius | 5m | 2–15m | 巡逻范围 |
| floorScalingHP | 0.03 | 0.01–0.05 | 每层 HP 缩放系数 |
| floorScalingDamage | 0.02 | 0.01–0.04 | 每层伤害缩放系数 |

## Visual/Audio Requirements

### VFX
- 敌人命中闪烁：受击时材质短暂变白（0.1s）+ 血粒子
- 死亡溶解：死亡时 mesh 从下到上溶解消失（1s）
- Boss 阶段转换：全屏短暂颜色偏移 + Boss 模型放大/变化
- 会议恶魔结界：绿色半透明球体，内部移速减慢

### Audio
- 每种敌人独立脚步声（丧尸=拖行 / 怨灵=飘浮嗡声 / 保安=靴子）
- Boss 出场/阶段转换：独立主题音效短句
- 敌人发现玩家时：简短尖音（KPI 丧尸=低吼 / PPT 怨灵=PPT 翻页音）

## UI Requirements

- Boss HP 条：屏幕顶部居中，显示 Boss 名称 + HP + 阶段标记
- 敌人被致盲/定身效果源显示短暂状态图标（如果被 C 类武器命中）

## Acceptance Criteria

1. GIVEN 玩家进入 KPI 丧尸 15m 内, WHEN 在丧尸前方, THEN 丧尸进入 Chase 状态
2. GIVEN 丧尸在 Chase 状态, WHEN 玩家脱离 30m, THEN 丧尸回到 Patrol
3. GIVEN 丧尸处于 Attack 范围, WHEN 攻击冷却结束, THEN 造成 12 伤害
4. GIVEN KPI 丧尸 HP ≤ 0, WHEN Die() 调用, THEN 掉落工牌 + 回形针 + 概率掉落
5. GIVEN 保安发现玩家, WHEN 距玩家 10m 内, THEN 使用手电筒致盲 3s
6. GIVEN PPT 怨灵 Boss HP 降至 0, WHEN Die(), THEN Boss 死亡动画, 掉特殊工牌
7. GIVEN 敌人在墙后, WHEN 玩家在 15m 内, THEN 敌人不发现（射线遮挡）
8. GIVEN 邮件幽灵 HP ≤ 0, WHEN Die(), THEN 爆炸造成 20 AOE 伤害

## Open Questions

1. **敌人刷新机制** — 安全楼层敌人重新刷新的间隔？数量？是否无限刷？
2. **敌人 AI 复杂度** — Phase 1 需要巡逻路径点还是随机 wander？
3. **Boss 战机制** — CEO 三阶段是否 Phase 1 全做？
