# Key Items (Badges & USB Drives)

> **Status**: In Design (Post-MVP)
> **Author**: Claude Code (/design-system)
> **Last Updated**: 2026-07-17
> **Implements Pillar**: 赌徒时刻 (#3), 下坠之旅 (#2)

## Overview

Key Items System 定义两种永久进度物品——工牌（Badge）和 U盘（USB Drive）。工牌是类似 Tarkov 狗牌的身份标识系统：每个敌人和角色都有一张工牌，收集后可出售或挂在纪念墙上。U盘是蓝图解锁和升级的消耗品，找到后可在基地服务器使用。

## Player Fantasy

每张工牌都是一个故事。普通员工的工牌让你想起茶水间墙上那些名字。Boss的工牌是战利品——挂在纪念墙最显眼的位置。U盘里可能存着前人的研究笔记，或者CEO的秘密文件。在27F服务器上插入U盘，看着蓝图升级进度条走满——这是你在基地里最接近"安全升级"的时刻。

## Detailed Design

### 工牌系统

| 属性 | 值 |
|------|-----|
| 来源 | 所有敌人必掉（普通工牌）、精英/Boss必掉（特殊工牌）、前人角色尸体（前人遗物） |
| 信息 | 姓名、部门/职位、员工编号、在职年限 |
| 品级 | 随楼层变化——越低层敌人职级越低但工牌越值钱（怨念越深） |

#### 工牌来源

| 来源 | 类型 | 用途 |
|------|------|------|
| 普通敌人掉落 | 普通工牌 | 卖回形针，品级越高越值钱 |
| 精英/Boss掉落 | 特殊工牌 | 解锁楼层权限（永久flag）+ 高价值收藏品 |
| 前人角色尸体 | 前人遗物 | 回收后挂在纪念墙/卖给NPC |

#### 工牌品级

| 楼层段 | 品级 | 价值（回形针） |
|--------|------|---------------|
| 40-50F | 高管级 | 500-2000 |
| 20-39F | 中层管理 | 100-500 |
| 1-19F | 底层员工 | 50-200 |

### U盘系统

| 属性 | 值 |
|------|-----|
| 来源 | 稀有掉落（Epic+）、服务器房容器、Boss掉落 |
| 用途 | 基地服务器使用 → 永久解锁/升级武器蓝图 |
| 死亡掉落 | 会掉（占背包格） |
| 使用后 | 消耗（蓝图永久解锁，多余的U盘可升级已有蓝图或卖NPC） |

#### U盘升级

| 操作 | 消耗 | 效果 |
|------|------|------|
| 解锁基础蓝图 | 1 U盘 | 改造台可重制该武器 |
| 升级蓝图 Lv1 | 2 U盘 | 武器伤害+10% |
| 升级蓝图 Lv2 | 4 U盘 | 武器射速+10% 或 换弹时间-15% |
| 升级蓝图 Lv3 | 8 U盘 | 特殊效果（如订书机手枪追加穿透1个敌人） |

## Formulas

```
工牌价值 = baseValue × floorMultiplier × rarityMultiplier
  floorMultiplier = 1 + (50 - floorNumber) × 0.03  // 越低层越高
  rarityMultiplier: 普通=1.0, 精英=5.0, Boss=20.0

U盘掉落率 = 基础概率 × 楼层加成
  基础概率: 服务器房5%, Boss 100%, 普通容器0.5%
  楼层加成: 越底层概率略高（1F约1.5x）
```

## Edge Cases

1. **工牌可出售** → 一旦出售不可回购。纪念墙上对应条目不受影响（墙上是永久记录）。
2. **U盘蓝图已满级** → 多余U盘只能卖NPC或升级其他蓝图。
3. **前人遗物工牌** → 回收后自动记录到纪念墙。出售的话墙上有名但标注"工牌已出售"。

## Dependencies

| 系统 | 方向 | 类型 |
|------|------|------|
| Loot & Economy | 上游 | Hard |
| Death & Inheritance | 上游 | Hard |
| Base Building | 下游 | Soft |
| Save/Load | 下游 | Hard |

## Tuning Knobs

| 参数 | 默认值 | 描述 |
|------|--------|------|
| badgeBaseValue | 50 | 普通工牌基础价值 |
| usbServerRoomDropRate | 0.05 | 服务器房U盘掉落率 |
| usbBossDropRate | 1.0 | Boss必掉U盘 |
| blueprintUpgradeCostLv1 | 2 | Lv1升级消耗U盘数 |
| blueprintUpgradeCostLv2 | 4 | Lv2升级消耗U盘数 |
| blueprintUpgradeCostLv3 | 8 | Lv3升级消耗U盘数 |

## Acceptance Criteria

1. GIVEN 击杀任意敌人, WHEN Die()完成, THEN 掉落对应工牌物品
2. GIVEN 回收前人尸体工牌, WHEN 返回基地, THEN 纪念墙新增条目
3. GIVEN 玩家将U盘插入服务器（27F功能）, WHEN 确认使用, THEN 蓝图永久解锁
4. GIVEN 蓝图已解锁, WHEN 在改造台消耗回形针, THEN 可重制该基础武器
