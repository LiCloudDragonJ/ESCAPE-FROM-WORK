# Evacuation Signals

> **Status**: In Design (Post-MVP)
> **Author**: Claude Code (/design-system)
> **Last Updated**: 2026-07-17
> **Implements Pillar**: 赌徒时刻 (#3), 活着的建筑 (#4)

## Overview

Evacuation Signals System 为 Raid 中的玩家施加动态时间压力。两个主要信号——保安巡逻广播（随机触发）和加班铃（搜刮价值触发）——改变楼层状态，迫使玩家做出"继续搜刮还是立刻撤离"的决策。

## Player Fantasy

你正蹲在财务部的保险柜前，屏幕突然闪红——"保安巡逻广播：所有安保人员请注意，发现未经授权的活动。3分钟内封锁楼层。"你还有3分钟。保险柜里是金色的股权证书。你决定赌一把——打开保险柜，然后冲向消防通道。

## Detailed Design

### 保安巡逻广播

| 属性 | 值 |
|------|-----|
| 触发 | 进入楼层后随机计时器（5-15分钟） |
| 预警 | 屏幕闪红 + 广播文字 + 倒计时3分钟 |
| 效果 | 3分钟内不撤离 → 增援保安队（3-5人）到达 |
| 增援位置 | 楼梯间和电梯口 |
| 可取消 | 否——一旦触发，必须应对 |

### 加班铃

| 属性 | 值 |
|------|-----|
| 触发 | 背包中搜刮物品总价值达到阈值（默认500回形针） |
| 效果 | 全层敌人移速+30%，视野+50%，击杀奖励翻倍 |
| 持续时间 | 直到玩家撤离或死亡 |
| 可取消 | 否 |

### 信号叠加

两个信号可同时激活。叠加效果：保安增援+敌人强化——极高压力，但极高回报。

## Formulas

```
保安广播触发时间 = Random.Range(300s, 900s)  // 进入楼层后5-15分钟
增援数量 = Random.Range(3, 6)                // 3-5个保安
加班铃阈值 = 500（基础）× floorDifficulty  // 越底层阈值越低

加班铃效果:
  enemySpeed *= 1.3
  enemyDetectionRange *= 1.5
  killRewardMultiplier = 2.0
```

## Edge Cases

1. **广播触发时已在撤离途中** → 广播仍生效，但玩家可能已在楼梯间（不受影响）。
2. **加班铃触发时背包已满** → 依然触发——压力拉满但没空间捡更多东西。
3. **两个信号同时触发** → 效果叠加，UI同时显示两个计时器/状态。
4. **Boss战中触发** → Boss战楼层不触发广播（Boss战已有足够压力），但加班铃可触发。

## Dependencies

| 系统 | 方向 | 类型 |
|------|------|------|
| Enemy AI | 下游 | Hard |
| Loot & Economy | 上游 | Soft |

## Tuning Knobs

| 参数 | 默认值 | 范围 |
|------|--------|------|
| broadcastMinTime | 300s | 120–600s |
| broadcastMaxTime | 900s | 300–1800s |
| broadcastWarningDuration | 180s | 60–300s |
| reinforcementCount | 3–5 | 1–8 |
| overtimeValueThreshold | 500 | 200–2000 |
| overtimeSpeedBonus | 1.3x | 1.1–1.5x |
| overtimeRewardMultiplier | 2.0x | 1.5–3.0x |

## Acceptance Criteria

1. GIVEN 玩家进入楼层, WHEN 5-15分钟后, THEN 广播触发, 3分钟倒计时开始
2. GIVEN 倒计时归零, WHEN 玩家仍在楼层, THEN 3-5个保安在楼梯间生成
3. GIVEN 背包物品价值≥500, WHEN 检查触发, THEN 加班铃激活, 敌人强化
4. GIVEN 两个信号叠加, WHEN 效果检查, THEN 增援+强化同时生效
