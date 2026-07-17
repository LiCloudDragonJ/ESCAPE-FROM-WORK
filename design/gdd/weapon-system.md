# Weapon System

> **Status**: In Design
> **Author**: 用户 + Claude Code
> **Last Updated**: 2026-07-17
> **Implements Pillar**: 办公室即武器库 (#1)

## Overview

Weapon System 定义 ESCAPE FROM WORK 中所有武器的数据模型、分类体系、弹药类型和改装规则。武器分为三类——A 类（办公用品改造远程武器）、C 类（创意/特殊效果武器）、近战武器。每把武器由可配置的 WeaponData ScriptableObject 驱动，使得新增武器无需代码变更。武器系统是 Combat System 的弹药供应方和伤害数据源。

## Player Fantasy

服务于"办公室即武器库"支柱。玩家拿起订书机手枪时，应该感受到一把有正经后座力的半自动武器——它不是玩具，它真的能杀人。键盘霰弹枪的键帽散射让近距离火力压制变得痛快。碎纸机锯的持续切割声让人想起加班时碎纸机的噪音——现在它切的是敌人。办公用品不是笑话——在这个地狱写字楼里，它们是你能找到的最可靠的武器。

## Detailed Design

### Core Rules

1. 所有武器数据由 WeaponData ScriptableObject 驱动，运行时不可变。
2. 玩家 Loadout = 1A + 1C + 1 近战，出发前在基地武器架选定。
3. 武器按类别消耗对应的弹药类型。近战不耗弹药。
4. 改装系统 MVP 仅开放 1 槽（瞄准具），架构预留 3 槽。

### Weapon Classes

#### A 类 — 办公用品改造远程武器

| 武器 | 弹药 | 伤害 | 射速 | 弹夹 | 换弹 | 射程 | 散射 | 特殊 |
|------|------|------|------|------|------|------|------|------|
| 订书机手枪 | Staple | 15 | 300 RPM | 15 | 1.5s | 40m | 3° | 半自动，可靠 |
| 键盘霰弹枪 | Keycap | 8×5粒 | 60 RPM | 8 | 2.5s | 12m | 12° | 近距离5发散射 |
| 投影仪射线枪 | BulbLife | 25/s | 持续 | 100(5s) | 3s | 30m | 1° | 穿透家具掩体，不穿墙 |
| 马克杯投掷器 | Mug | 60 | 30 RPM | 3 | 3s | 25m | 8° | 抛物线AOE（3m半径），不穿墙 |

#### C 类 — 创意/特殊效果武器

| 武器 | 弹药 | 效果 | 持续时间 | 冷却 | 射程 |
|------|------|------|---------|------|------|
| PPT发射器 | PPTPage | 致盲（屏幕全白） | 2s | 8s | 20m(锥形) |
| 会议邀请法杖 | MeetingLink | 范围定身（敌人冻结） | 3s | 15s | 15m(5m半径) |
| 邮件炸弹 | JunkMail | 延迟爆炸+嘲讽 | 2s延迟/5s嘲讽 | 12s | 15m(4m半径) |
| 咖啡因注射器 | CoffeeBean | 自身攻速+30%+移速+20% | 8s | 25s | 自身 |

#### 近战武器

| 武器 | 轻击伤害 | 重击伤害 | 范围 | 蓄力时间 | 轻击体力 | 重击体力 | 特殊 |
|------|---------|---------|------|---------|---------|---------|------|
| 碎纸机锯 | 12 | 40/s(2s持续) | 1.5m | 1s | 12 | 25 | 按住持续切割 |
| 网线鞭 | 10 | 28 | 3m | 1.5s | 10 | 22 | 中距离，可打多目标 |
| KPI报表锤 | 18 | 80 | 2m | 2s | 18 | 35 | 满蓄一击秒小怪 |
| 键盘板砖 | 14 | 35 | 1.2m | 0.8s | 10 | 18 | 可打断敌人攻击 |
| 马克杯流星锤 | 10 | 45 | 2.5m | 1.5s | 14 | 28 | 旋转AOE清围殴 |

### 弹药类型

| 弹药 | 对应武器 | 堆叠上限(背包) | 堆叠上限(储物箱) |
|------|---------|--------------|----------------|
| Staple(订书钉) | 订书机手枪 | 200 | 2000 |
| Keycap(键帽) | 键盘霰弹枪 | 100 | 1000 |
| BulbLife(灯泡) | 投影仪射线枪 | 50 | 500 |
| Mug(马克杯) | 马克杯投掷器 | 20 | 200 |
| PPTPage(PPT页) | PPT发射器 | 50 | 500 |
| MeetingLink(会议链接) | 会议邀请法杖 | 20 | 200 |
| JunkMail(垃圾邮件) | 邮件炸弹 | 30 | 300 |
| CoffeeBean(咖啡豆) | 咖啡因注射器 | 30 | 300 |

### 武器改装

| 槽位 | 效果 | MVP |
|------|------|-----|
| 瞄准具 | 散射 -20% 或 自动锁定范围 +5m | ✅ |
| 弹药转化 | 切换武器弹药类型 | ❌ Phase 2 |
| 特殊配件 | 武器专属效果 | ❌ Phase 2 |

### Interactions with Other Systems

- **Combat System** — 提供 WeaponBase.Fire()、Reload()、CanFire() 接口。Combat 负责调用时机和瞄准参数。
- **Loot & Economy** — 武器作为 ItemData 的子类型可被搜刮、交易、存储。弹药是消耗品。
- **Base Building** — 武器架展示拥有的武器，出发前选 Loadout。改造台执行武器改装。

## Formulas

### 伤害

```
Ranged:  finalDamage = baseDamage × headshotMultiplier × coverMultiplier
Melee:   finalDamage = meleeLightDamage（轻击，chargeRatio = 1.0）
         finalDamage = meleeHeavyDamage × chargeRatio（重击，chargeRatio 0–1.0）
Beam:    finalDamage = baseDamage × deltaTime × headshotMultiplier
         （穿透办公桌/文件柜等家具掩体，无视 coverMultiplier；
          但无法穿透结构墙体——命中墙体时投射物销毁）
AOE:     finalDamage = baseDamage（全范围无衰减）
         （无视家具掩体的 coverMultiplier，但墙体遮挡的敌人不受伤害）
```

### 弹药消耗

```
Semi/Scatter:  currentAmmo -= 1 per shot
Beam:          currentAmmo -= 20 × deltaTime
C-class:       currentAmmo -= 1 per use
Melee:         无消耗
```

### 换弹

```
reloadTime 来自 WeaponData（1.5–3s）
换弹中可闪避打断 → 保留已装填部分，需重新换弹
```

### 改装倍率

```
spreadModifier:  0.8（瞄准具生效时，原散射 × 0.8）
autoAimBonus:    +5m（瞄准具变体2生效时）
```

## Edge Cases

1. **武器数据缺失** → WeaponBase.Data == null 时 Fire() 直接 return，PlayerCombat 打印警告。
2. **弹夹满时按 R** → 无操作。HUD 不显示换弹提示。
3. **换弹中切换武器** → 旧武器保留未装填部分，切换后不自动换弹。
4. **投射物出射程** → 超过 WeaponData.range 后自动销毁（不造成伤害）。
5. **弹药堆叠超限** → PlayerInventory.AddItem 返回 false，超限部分丢弃并打印日志。
6. **空弹夹 + 无备用弹药** → 射击无反应。HUD 弹药数闪烁红色 2 秒。

## Dependencies

| 系统 | 方向 | 类型 | 接口 |
|------|------|------|------|
| Core (IDamageable) | 上游 | Hard | 伤害输出接口，TakeDamage(amount, source) |
| Data (WeaponData, ItemData) | 上游 | Hard | ScriptableObject 数据驱动 |
| Combat System | 下游 | Hard | Fire(from, dir, isManual, isHeadshot), Reload(), CanFire() |
| Loot & Economy | 下游 | Soft | 武器作为 ItemData 的 GearSlot 子类型流转 |
| Base Building | 下游 | Soft | 武器架展示 Loadout、改造台执行改装 |

## Tuning Knobs

| 参数 | 默认值 | 安全范围 | 描述 |
|------|--------|---------|------|
| baseDamage | 15 | 5–200 | 武器基础伤害 |
| fireRate | 300 RPM | 30–600 RPM | 射速 |
| spread | 3° | 0–15° | 散射角度 |
| magazineSize | 15 | 3–200 | 弹夹容量 |
| reloadTime | 1.5s | 0.5–5s | 换弹时间 |
| range | 40m | 5–60m | 有效射程 |
| meleeLightDamage | 14 | 5–30 | 近战轻击伤害 |
| meleeHeavyDamage | 35 | 20–120 | 近战满蓄重击伤害 |
| meleeRange | 1.5m | 0.5–4m | 近战范围 |
| chargeUpTime | 1.5s | 0.5–3s | 蓄力时间 |
| ammoStackLimit | 200 | 20–999 | 弹药堆叠上限(背包) |
| specialEffectDuration | 2s | 1–10s | C 类效果持续时间 |
| specialEffectCooldown | 8s | 5–60s | C 类冷却时间 |
| modSpreadReduction | 0.8 | 0.5–0.95 | 瞄准具散射倍率 |

## Visual/Audio Requirements

### VFX
- 枪口闪光：射击时 muzzlePoint 位置短暂白色/黄色点光源
- 投射物弹道：每种弹药独立的 trail renderer 颜色（Staple=银色、Keycap=彩色、Mug=棕色）
- AOE 爆炸：马克杯碎裂 + 棕色液体飞溅粒子（半径 3m）
- 近战弧线：沿挥击方向的短暂白色拖尾

### Audio
- 每把武器独立射击音效（订书机=金属撞击声、键盘=塑料碎裂声、投影仪=电流嗡声）
- 命中音效：身体命中（短促闷响）、头部命中（清脆金属声）
- 换弹音效：每种武器独立的换弹动画配音
- 蓄力充能音效：低频率渐强嗡声（0 → 满蓄 100% 音量）
- 空弹音效：轻脆的"咔"空仓声

## UI Requirements

- 武器架 UI：网格展示所有拥有的武器（每格 80×80），拖拽到 Loadout 槽位装备
- 弹药 HUD：右下角显示"当前弹夹 / 备用弹药"
- 换弹进度条：换弹时准星旁显示圆形进度
- 武器切换提示：切换武器时短暂显示武器名称 + 图标（0.5s 渐隐）

## Acceptance Criteria

1. GIVEN 玩家装备订书机手枪, WHEN 按左键射击, THEN 消耗 1 发 Staple, 枪口闪光, 投射物飞出
2. GIVEN 弹夹 = 0, WHEN 按左键, THEN 无射击, 弹药 HUD 闪烁红色
3. GIVEN 弹夹 = 0 + 有备用弹药, WHEN 按 R, THEN 开始 1.5s 换弹, 完成后弹夹 = 15
4. GIVEN 换弹中, WHEN 按空格闪避, THEN 换弹中断, 弹夹保留已装填部分
5. GIVEN 装备键盘霰弹枪, WHEN 射击近距离敌人, THEN 5 发散射投射物飞出（每发 8 伤害）
6. GIVEN 装备 PPT 发射器, WHEN 对敌人使用, THEN 敌人进入致盲状态 2s, 冷却开始
7. GIVEN KPI 报表锤蓄力 2s 满, WHEN 释放重击, THEN 造成 80 伤害（秒杀 KPI 丧尸）
8. GIVEN 投射物飞行超过 WeaponData.range, THEN 投射物自动销毁, 不造成伤害
9. GIVEN 玩家在武器架 UI, WHEN 拖拽武器到 A 槽, THEN 武器装配到 PlayerCombat.SlotA
10. GIVEN 安装瞄准具改装, WHEN 射击, THEN 散射角度 × 0.8

## Open Questions

1. **武器获取途径** — 初始武器从哪来？搜刮掉落？NPC 赠送？基地默认装备？
2. **武器稀有度加成** — 同型号武器是否有品质等级（白/绿/蓝）影响基础数值？还是稀有度仅影响掉落概率？
