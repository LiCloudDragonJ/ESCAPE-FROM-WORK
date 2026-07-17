# UI / HUD

> **Status**: In Design
> **Author**: 用户 + Claude Code
> **Last Updated**: 2026-07-17
> **Implements Pillar**: 赌徒时刻 (#3)

## Overview

UI/HUD System 定义 ESCAPE FROM WORK 中所有玩家界面元素：战斗 HUD、搜刮面板、背包、基地界面、死亡屏幕和纪念墙。所有 UI 使用 Unity UGUI（Canvas + Text/Image/Slider）。HUD 通过轮询 PlayerCombat/PlayerHealth/PlayerInventory 获取实时数据。

## Player Fantasy

HUD 应该低调但信息密集——玩家一眼就能看到血量、体力、弹药和楼层信息。搜刮面板打开的瞬间应该有"翻箱倒柜"的切实感受——物品逐个出现、稀有度颜色分明。死亡屏幕应该是沉重但有希望的——纪念墙告诉你前辈死在了哪一层，带回多少东西。

## Detailed Design

### 战斗 HUD

| 元素 | 位置 | 绑定数据 |
|------|------|---------|
| 血条 (Slider) | 左上 480×30 | PlayerHealth.CurrentHealth / MaxHealth |
| 体力条 (Slider) | 血条下方 480×20 | PlayerCombat.CurrentStamina / MaxStamina |
| 弹药显示 (Text) | 左上第二行 330×80 | Weapon.CurrentAmmo / ReserveAmmo |
| 楼层信息 (Text) | 右上 330×100 | FloorManager.floorNumber + 楼层状态 |
| 准星 (Crosshair) | 屏幕中心 24×24 | 自动=半透明 / 手动=实心 |
| 交互提示 (Text) | 底部中心 | "按 E 搜刮" / "按 E 开门" 等 |
| 提取警告 (Text) | 屏幕中心 | 靠近提取点时显示"撤离!" |

### 搜刮面板

| 元素 | 绑定 |
|------|------|
| 标题文字 | 容器类型名称或"搜刮中..." |
| 装备栏（左 18%） | 5 个装备槽（A/C/近战/护甲/背包） |
| 背包网格（中 41%） | PlayerInventory 当前内容 |
| 容器网格（右 37%） | LootContainer 的 loaded/prepending 物品 |
| 物品悬浮提示 | 名称、稀有度颜色、描述、价值 |
| 丢弃区（底部红条） | 拖拽物品至此丢弃，生成 LooseLoot |

操作：
- 双击物品 → 快速转移到另一侧（容器↔背包）
- 拖拽物品 → 跨面板移动 / 装备槽装配 / 丢弃
- F 键 → 全部转移（容器→背包，按稀有度降序）
- Tab 键 → 打开/关闭背包（无容器时仅显示装备+背包两栏）
- 拖拽中按 R → 旋转物品（用于多格物品的网格适配）

### 基地界面

| 面板 | 触发 | 功能 |
|------|------|------|
| 储物箱 | E 靠近茶水间储物柜 | 三栏面板（装备\|背包\|储物箱），物品可双向转移 |
| 武器架 | E 靠近武器架 | 网格展示拥有武器，拖拽到 Loadout 槽位 |
| 公告板 | E 靠近公告板 | 左=可用任务 / 中=任务详情 / 右=进行中任务 |
| 改造台 | E 靠近改造台（Phase 2） | 武器改装界面 |

### 死亡屏幕

| 元素 | 内容 |
|------|------|
| 死亡信息 | 角色名、死亡楼层、死因 |
| 损失总结 | 丢失的装备列表 + 保留的资源 |
| 纪念墙预览 | 新工牌将出现在纪念墙上 |
| 继续按钮 | "选择新角色" → 回到基地 |

### 纪念墙

- 位置：茶水间墙上
- 显示：所有死去前人的工牌列表
- 每张工牌：姓名、死亡楼层、死因、带回物资价值
- 垂直滚动列表（如果工牌超过屏幕）

## Formulas

### HUD 更新频率

```
healthBar.value = lerp(current, target, 0.1)  // 平滑过渡
staminaBar.value = stamina / maxStamina        // 实时
ammoText = "{currentAmmo} / {reserveAmmo}"     // 每次射击更新
```

### 网格物品放置

```
cellSize = 60px（搜刮面板）/ 80px（武器架）
物品占位 = itemData.width × itemData.height 格
```

## Edge Cases

1. **物品旋转后超出面板** → 旋转失败，保持原方向。
2. **同时打开两个面板** → 新面板关闭旧面板（同一时间仅一个面板打开）。
3. **面板打开时玩家移动** → 面板打开时禁用玩家移动输入（LootContainerUI.IsOpen 检查）。
4. **容器加载中打开另一个容器** → 旧容器的加载协程停止。新容器的加载开始。
5. **装备槽已有物品** → 拖入新物品时旧物品自动卸载到背包。背包满则拒绝交换。

## Dependencies

| 系统 | 方向 | 类型 | 接口 |
|------|------|------|------|
| Player Health | 上游 | Hard | CurrentHealth, MaxHealth |
| PlayerCombat | 上游 | Hard | CurrentStamina, MaxStamina |
| Weapon System | 上游 | Hard | CurrentAmmo, ReserveAmmo |
| Player Inventory | 上游 | Hard | 背包内容 |
| Loot & Economy | 上游 | Hard | LootContainer 状态 |
| Death System | 上游 | Hard | DeathContext → 死亡屏幕 |
| Quest System | 上游 | Soft | 任务状态 → 公告板 |

## Tuning Knobs

| 参数 | 默认值 | 描述 |
|------|--------|------|
| cellSize | 60px | 搜刮面板网格大小 |
| weaponCellSize | 80px | 武器架网格大小 |
| healthBarSmoothing | 0.1 | 血条平滑过渡速度 |
| panelWidth | 1450px | 搜刮面板总宽度 |
| panelHeight | 820px | 搜刮面板总高度 |

## Visual/Audio Requirements

### VFX
- 面板打开/关闭：淡入淡出（0.15s）
- 物品拖拽：拖拽中的物品半透明 + 跟随鼠标
- 稀有物品闪烁：Legendary+ 物品在网格中周期性金色闪烁

### Audio
- 面板打开：短促翻页/掀盖音
- 物品拾取：短"咔"
- 物品丢弃：闷响落地
- 错误操作（空间不足等）：低沉短音

## Acceptance Criteria

1. GIVEN 玩家进入战斗, WHEN HUD 更新, THEN 血量、体力、弹药实时显示
2. GIVEN 玩家按 E 靠近容器, WHEN 容器面板打开, THEN 三栏面板显示且玩家不能移动
3. GIVEN 玩家双击物品, WHEN 目标面板有空间, THEN 物品转移
4. GIVEN 玩家按 F, WHEN 容器有物品, THEN 全部转移且按稀有度排序
5. GIVEN 玩家拖拽武器到 A 槽, WHEN 槽为空, THEN 武器装配成功
6. GIVEN 玩家死亡, WHEN 死亡事件触发, THEN 死亡屏幕显示（非服务端处理）
7. GIVEN 纪念墙存在, WHEN 新角色访问, THEN 前人所有工牌可见

## Open Questions

1. **ESC 菜单** — Phase 1 需要设置菜单吗？（音量、按键绑定）
2. **小地图** — Phase 1 需要小地图吗？还是纯靠环境导航？
3. **任务追踪** — HUD 上是否显示当前追踪的任务进度？