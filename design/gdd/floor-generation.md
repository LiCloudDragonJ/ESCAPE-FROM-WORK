# Floor Generation

> **Status**: In Design
> **Author**: 用户 + Claude Code
> **Last Updated**: 2026-07-17
> **Implements Pillar**: 下坠之旅 (#2), 活着的建筑 (#4)

## Overview

Floor Generation System 负责程序化生成 ESCAPE FROM WORK 中每一层写字楼的布局。生成算法采用"走廊驱动 + 规则约束"策略——先确定核心筒和环形走廊结构，再沿走廊两侧分配房间，最后放置家具和搜刮点。使用种子驱动的确定性随机生成——相同种子永远产生相同楼层。

## Player Fantasy

每次进入新楼层时，玩家应该感到探索未知的紧张。走廊转角后是什么？会议室里坐着什么？那个没有窗户的大房间里是不是有服务器机柜？楼层是写实写字楼的感觉——不是随机迷宫，而是一个"曾经有人在这里上班"的真实空间。茶水间里有咖啡机、办公区有工位、CEO办公室有大桌子和酒柜。

## Detailed Design

### 地图参数

| 参数 | 值 |
|------|-----|
| 地图尺寸 | 100m × 80m |
| 核心筒尺寸 | 16m × 12m（居中） |
| 环形走廊宽度 | 1.8m |
| 墙高 | 3.5m |
| 墙厚 | 0.2m |
| 玻璃隔断厚度 | 0.1m |
| 柱子间距 | 12m |
| 楼梯间尺寸 | 6m × 6m |
| 茶水间频率 | 每 5 层一个（50F, 45F, 40F...） |

### 布局原型

每层随机选择三种布局原型之一（基于种子哈希决定）：

| 原型 | 权重 | 描述 |
|------|------|------|
| RingStandard | 45% | 核心筒居中 + 完整环形走廊 + 沿外侧均匀分配房间 |
| OpenPlan | 30% | 核心筒居中 + 最少隔墙 + 工位海洋 + 局部隔断 |
| Cellular | 25% | 核心筒偏移 + 窄走廊 + 大量小房间 + 不规则隔墙 |

### 生成流程

```
1. 放置锚点：8 个消防楼梯间（四边各 2 个）
   - 入口随机选择 1 个楼梯间
   - 出口选择距入口最远（BFS 路径）的楼梯间
2. 构建核心筒墙壁 + 环形走廊
3. 应用布局原型规则：
   - RingStandard: 沿外侧按规则分配房间类型
   - OpenPlan: 放置群组工位 + 局部隔断墙
   - Cellular: 细分空间为小房间 + 窄走廊
4. 放置柱子：按 12m 间距 + 随机微调
5. 放置家具：按房间类型调用 FurniturePlacer
6. BFS 验证：确保入口到出口的路径存在
```

### 房间类型

| 房间类型 | 出现规则 | 最小尺寸 | 最大尺寸 |
|----------|---------|---------|---------|
| Stairwell(楼梯间) | 8 个固定锚点 | 6m×6m | 6m×6m |
| TeaRoom(茶水间) | 每 5 层, 近入口 | 4m×5m | 6m×6m |
| Office(开放办公区) | 填充主体 | 6m×8m | 14m×16m |
| ConferenceRoom(会议室) | 靠外侧大空间 | 6m×8m | 10m×12m |
| ServerRoom(服务器房) | 内侧无窗位置 | 4m×6m | 6m×8m |
| CEOOffice(CEO办公室) | 最深层（远离入口） | 8m×10m | 12m×12m |
| HROffice(HR部) | 中层位置 | 6m×8m | 8m×10m |
| FinanceOffice(财务部) | 中层位置 | 6m×8m | 8m×10m |
| Reception(前台) | 仅 1-9F | 6m×8m | 8m×10m |
| PrintRoom(打印室) | 随机附带 | 4m×4m | 4m×6m |
| Storage(储藏室) | 随机附带 | 3m×4m | 5m×6m |

### 家具放置

每个房间按 RoomFurnitureSet 配置放置家具。家具放置由 FurniturePlacer 执行：

```
FurniturePlacer.PlaceFurniture(room, furnitureSet, seed):
  1. 读取 furnitureSet.mandatoryFurniture → 至少放置 minCount 个
  2. 读取 furnitureSet.optionalFurniture → 按 spawnChance 随机
  3. 为每件家具创建几何体（Phase 1: Cube 拼接）
  4. 为搜刮家具绑定 LootContainer 组件
  5. 随机分配变体（普通/主管/CEO/破损）
  6. 返回 List<PlacedFurniture>
```

### 柱子

- 间距：12m（可配置）
- 随机偏移：±2m（避免完美网格感）
- 高度：3.5m（与墙同高）
- 碰撞：不可穿过

### 高价值区域

每层有且仅有一个高价值区域，类型随楼层段变化：

| 楼层段 | 高价值区域类型 |
|--------|---------------|
| 40-50F | CEO 办公室（酒柜 + 保险柜） |
| 30-39F | HR 档案柜墙（大量情报） |
| 20-29F | 服务器房（稀有电子元件） |
| 10-19F | 财务保险柜（股权证书 + 支票） |
| 1-9F | CEO 终极办公室 / 大堂金库 |

## Formulas

### 种子生成

```
floorSeed = runSeed + floorNumber
Random.InitState(floorSeed)
→ 后续所有 Random 调用都确定性
```

### 布局原型选择

```
archetypeIndex = (floorNumber × 173 + 97) % 40
if < 18 → RingStandard (45%)
if < 30 → OpenPlan (30%)
else → Cellular (25%)
```

### 出口选择

```
entryStairs = AnchorPositions[random]
exitStairs = BFS 搜索距 entry 最远的锚点
→ 确保出口距离入口尽可能远
```

### 房间大小

```
roomWidth  = maxRoomWidth  × clamp(Random.value, 0.6, 1.0)
roomDepth  = maxRoomDepth  × clamp(Random.value, 0.6, 1.0)
→ 每个房间大小有 ±20% 浮动
```

## Edge Cases

1. **BFS 无路径** → 重试生成（最多 5 次）。5 次失败 = 使用 RingStandard 强保底。
2. **空间不够分配房间** → 跳过该位置，记录日志。优先保证核心房间。
3. **茶水间层但没有空间** → 将茶水间替换最近的小房间。
4. **柱子与门重叠** → 柱子偏移 ±1m，避免阻挡门。
5. **隔断墙切断走廊** → 后处理验证：每段走廊必须连接两个房间门以上。
6. **家具放不下** → 缩小家具尺寸（×0.8）或跳过该家具。

## Dependencies

| 系统 | 方向 | 类型 | 接口 |
|------|------|------|------|
| Enemy AI | 下游 | Hard | 提供 spawn zones（RoomModule.enemySpawnZones） |
| Loot & Economy | 下游 | Hard | 提供 loot container spawn points |
| FurniturePlacer | 下游 | Hard | 按房间类型放置家具 + 绑定搜刮表 |
| Base Building | 下游 | Soft | 茶水间位置作为基地锚点 |

## Tuning Knobs

| 参数 | 默认值 | 安全范围 | 描述 |
|------|--------|---------|------|
| MapW | 100m | 60–150m | 楼层宽度 |
| MapD | 80m | 50–120m | 楼层深度 |
| CoreW | 16m | 12–20m | 核心筒宽度 |
| CoreD | 12m | 8–16m | 核心筒深度 |
| CorridorW | 1.8m | 1.5–3m | 走廊宽度 |
| WallH | 3.5m | 2.5–5m | 墙高 |
| ColSpacing | 12m | 8–16m | 柱子间距 |
| archetypeWeights | 45/30/25 | — | 三种原型的生成权重 |
| maxRetries | 5 | 3–10 | 生成失败重试次数 |

## Visual/Audio Requirements

### VFX
- 地面网格线：走廊和房间边界有微弱的网格线（只在 Editor 下可见）
- 高价值区域发光：高价值区域门上方淡金色光晕（玩家靠近时才可见）

### Audio
- 环境音：每种房间类型独立环境音（茶水间=水声、服务器房=风扇噪音、开放办公区=键盘打字声）

## UI Requirements

- 楼层号 HUD：进入楼层时短暂显示楼层号 + 楼层名称（1.5s 渐隐）
- 小地图（Phase 2）：显示已探索区域的结构轮廓

## Acceptance Criteria

1. GIVEN runSeed=12345 + floor=50, WHEN 生成两次, THEN 两次布局完全相同
2. GIVEN 不同 runSeed, WHEN 生成同楼层, THEN 布局不同
3. GIVEN 生成完成, WHEN BFS 验证, THEN 入口到出口路径存在
4. GIVEN floorNumber % 5 == 0, WHEN 生成, THEN 布局包含茶水间
5. GIVEN 办公区生成, WHEN FurniturePlacer 完成, THEN 至少 6 个工位被放置
6. GIVEN 高价值区域生成, WHEN 是 CEO 层, THEN 放置酒柜 + 保险柜
7. GIVEN 柱子放置, WHEN 检查门位置, THEN 柱子不挡门

## Open Questions

1. **动态破坏** — 墙壁/柱子是否可以破坏？Phase 1 不做，但架构是否预留？
2. **楼层差异** — 不同部门的楼层（财务 vs IT vs 高管）是否需要特殊的房间类型约束？
3. **特殊楼层手设计** — 10 个特殊楼层的手动设计何时开始？