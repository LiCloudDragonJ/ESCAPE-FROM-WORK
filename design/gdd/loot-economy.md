# Loot & Economy

> **Status**: In Design
> **Author**: 用户 + Claude Code
> **Last Updated**: 2026-07-17
> **Implements Pillar**: 赌徒时刻 (#3), 办公室即武器库 (#1)

## Overview

Loot & Economy System 定义 ESCAPE FROM WORK 中所有物品、搜刮容器、掉落表和经济循环。物品跨 6 个稀有度和 10 种类型，由 ItemData ScriptableObject 驱动。搜刮容器实现渐进式加载（按稀有度延迟显示），支持中断继续。经济以"回形针"为货币，包含弹药安全网和咖啡豆新鲜度等生存机制。

## Player Fantasy

每次打开办公桌抽屉都应该是一次心跳时刻——里面可能是一叠回形针，也可能是前员工误扔的万能门禁卡。保险柜的黑灯打开瞬间，金色光芒照在脸上，玩家应该屏住呼吸。搜刮是赌徒时刻的核心来源：不是所有桌子都值得翻，但你永远不会知道哪张桌子藏着一生的运气。

## Detailed Design

### 物品分类

| ItemType | 描述 | 示例 |
|----------|------|------|
| Currency | 货币 | 回形针 |
| Ammo | 弹药 | 订书钉、键帽、灯泡 |
| Consumable | 消耗品 | 能量棒、咖啡豆、止痛药 |
| Construction | 建材 | 螺丝钉、电线、金属板 |
| Electronics | 电子产品 | U盘、SSD、CPU、显卡 |
| OfficeSupply | 办公耗材 | 打印纸、墨盒、便利贴 |
| Luxury | 奢侈品 | CEO钢笔、威士忌、手办 |
| Intel | 情报 | 人事档案、财务报告、研发笔记 |
| KeyItem | 关键道具 | 万能门禁卡、工牌(Boss) |
| Collectible | 收藏品 | 年终奖函、升职推荐信、股权证书 |

### 稀有度

| 稀有度 | 颜色 | 渐进加载延迟 | 近似价值 |
|--------|------|-------------|---------|
| Common (白) | #AAAAAA | 0s (即时) | 1-50 |
| Uncommon (绿) | #44AA44 | 1s | 50-500 |
| Rare (蓝) | #4466CC | 2s | 500-2000 |
| Epic (紫) | #8844CC | 4s | 2000-8000 |
| Legendary (金) | #CCAA00 | 8s | 8000-50000 |
| Mythic (红) | #CC2222 | 12s | 50000+ |

### 容器类型

| ContainerType | 容量(格) | 渐进加载 | 出现位置 |
|---------------|---------|---------|---------|
| Desk | 4×3 | ✅ | 办公桌、前台、IT工位 |
| FilingCabinet | 3×4 | ✅ | 文件柜、档案柜 |
| SupplyCloset | 4×4 | ✅ | 饮水机、冰箱、清洁推车 |
| Safe | 3×3 | ✅ | 保险柜、酒柜、钥匙柜 |
| ServerRack | 4×2 | ✅ | 服务器机柜、UPS |
| CEODesk | 5×4 | ✅ | CEO大办公桌 |

### 渐进加载机制

```
首次打开容器 → 掷 LootTable → 填充 _pendingItems
  ↓
LoadRoutine() 启动:
  for each item in _pendingItems:
    等待 rarity_delay
    移动 item 从 pending → loaded
    调用 LootContainerUI.RefreshFromContainer()
  完成 → _allLoaded = true
  ↓
再次打开容器 → 继续加载剩余的 _pendingItems
  ↓
物品被转移 → OnItemTransferred() 从 _loadedItems 移除
```

### 变体系统

| 变体 | 触发 | 效果 |
|------|------|------|
| 普通 | 默认 | LootTable 基础概率 |
| 主管 | 每房间 1 个随机家具 | 品质 +20%，可能出情报/奢侈品 |
| CEO | CEO 房间必出 1 个 | 必出至少绿装，传说概率 ×3 |
| 破损 | 每房间 1 个随机家具 | 品质 -50%，数量 -50% |

### LootTable 结构

```csharp
class LootTable : ScriptableObject {
    int minRolls;       // 最少掷骰次数
    int maxRolls;       // 最多掷骰次数
    LootEntry[] entries; // 物品池
}

class LootEntry {
    ItemData item;      // 物品
    float weight;       // 权重（相对概率）
    int minCount;       // 最小堆叠数
    int maxCount;       // 最大堆叠数
}
```

### 经济参数

| 参数 | 值 |
|------|-----|
| 货币 | 回形针 |
| 弹药安全网触发线 | 总弹药 < 10 发 |
| 咖啡豆新鲜度计时 | 30分钟(新鲜) → 2小时(尚可) → 4小时(变质) |
| 安全楼层物资刷新 | 每 4 小时 |
| 24 小时递减 | 每额外访问 -25% 品质 |

## Formulas

### 掷骰逻辑

```
rolls = Random.Range(minRolls, maxRolls + 1)
for i in 0..rolls:
    item = WeightedRandom(entries, entry → entry.weight)
    count = Random.Range(item.minCount, item.maxCount + 1)
    if 变体=破损: count = ceil(count / 2)
    output.add(item, count)
```

### 稀有度价值

```
baseValue: 来自 ItemData（设计值）
实际卖价 = baseValue × sellMultiplier（NPC 交易用）
变质咖啡豆 = 0（只能卖 NPC 回收）
```

### 背包网格

```
背包容量 = 装备中 Backpack 的 backpackWidth × backpackHeight
是否可以放入 = 网格中有连续 width × height 的空位
堆叠 = 同 ItemData + stackCount < maxStackSize
```

### 储物箱堆叠

```
储物箱堆叠上限 = maxStackSize × stashMaxStackMultiplier (默认 ×10)
回形针: 999(背包) → 9999(储物箱)
```

## Edge Cases

1. **容器正在加载时转移到新场景** → _loadingRoutine 被 StopCoroutine 终止。
2. **所有物品被取走** → 容器保持打开状态（空），允许重新打开看到"空"。
3. **物品太大放不进背包** → 物品变灰，不允许拖拽。显示提示"背包空间不足"。
4. **堆叠满** → 新捡的同物品显示为独立格子（合并逻辑：优先满堆）。
5. **弹药安全网触发** → 下次进入楼层时在入口附近刷新紧急文具柜（保证至少有基础弹药）。
6. **变质咖啡豆** → 仍然可以卖给 NPC，但价值 = 0。

## Dependencies

| 系统 | 方向 | 类型 | 接口 |
|------|------|------|------|
| Data (ItemData) | 上游 | Hard | 物品数据模型 |
| Player Inventory | 上游 | Hard | 背包网格、AddItem、RemoveItem |
| Floor Generation | 上游 | Hard | 容器创建位置 |
| UI / HUD | 下游 | Hard | LootContainerUI（三栏面板） |
| Base Building | 下游 | Soft | 储物箱存储 |
| Weapon System | 下游 | Soft | 弹药消耗 |

## Tuning Knobs

| 参数 | 默认值 | 安全范围 | 描述 |
|------|--------|---------|------|
| minRolls | 2 | 1–10 | 每容器最少掷骰 |
| maxRolls | 5 | 2–20 | 每容器最多掷骰 |
| rarityDelay | 0/1/2/4/8/12s | — | 渐进加载延迟 |
| coffeeFreshDuration | 30min | 15–120min | 新鲜状态持续时间 |
| safeFloorRefreshInterval | 4h | 1–24h | 安全楼层物资刷新 |
| visitDecayRate | 0.25 | 0.1–0.5 | 每次重复访问品质衰减 |
| ammoSafetyThreshold | 10 | 5–30 | 弹药安全网触发线 |
| stashMaxStackMultiplier | 10 | 5–20 | 储物箱堆叠倍率 |

## Visual/Audio Requirements

### VFX
- 稀有物品发光：Epic+ 物品在容器中有微弱对应颜色光晕
- 保险柜开启粒子：金色粒子短暂喷射

### Audio
- 容器打开：抽屉/柜门打开音效（具体取决于容器类型）
- 物品拾取：短促的"咔"音效
- 稀有物品：Legendary+ 额外播放短促高音"叮"
- 保险柜：厚重金属解锁声

## UI Requirements

- LootContainerUI：三栏面板（装备\|背包\|容器），网格拖拽，双击快速转移，F 全收
- 物品悬浮提示：名称、稀有度颜色、描述、价值、重量
- 稀有度颜色映射：白=灰、绿=绿、蓝=蓝、紫=紫、金=金、红=红

## Acceptance Criteria

1. GIVEN 玩家打开办公桌容器, WHEN 容器为空且掷骰完成, THEN 物品显示在容器列中
2. GIVEN 玩家双击容器物品, WHEN 背包有空间, THEN 物品转移到背包
3. GIVEN 玩家按 F, WHEN 容器有物品, THEN 所有物品按稀有度降序转移
4. GIVEN 史诗物品在加载中, WHEN 玩家关闭容器再打开, THEN 继续加载剩余物品
5. GIVEN 回形针堆叠 = 199, WHEN 捡到 3 个回形针, THEN 堆叠 = 200（满了），多余 2 个新建堆叠
6. GIVEN 弹药 < 10, WHEN 进入新楼层, THEN 入口刷新紧急文具柜
7. GIVEN 咖啡豆获得超过 30 分钟, WHEN 使用, THEN 效果降为 70%

## Open Questions

1. **NPC 交易定价** — 买价和卖价的比例？1:0.5？1:0.3？
2. **保险柜存取费** — 游戏概念提到"存取消耗回形针"，具体多少？
3. **蓝图解锁** — U盘解锁的蓝图是全局共享还是分角色？