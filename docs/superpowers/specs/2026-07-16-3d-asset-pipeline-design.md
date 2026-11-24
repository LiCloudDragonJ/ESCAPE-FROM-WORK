# 3D 素材生成管线设计文档

> **Status**: Draft
> **Author**: 用户 + Claude Code
> **Date**: 2026-07-16
> **引擎**: 团结引擎 1.9.3 (Tuanjie Engine) + URP
> **硬件**: RTX 4070 Ti SUPER (16GB VRAM)

---

## 1. Overview

本文档定义 ESCAPE FROM WORK 项目的完整 3D 素材生成管线。采用**混合管线架构**：云端 AI 快速验证风格方向 → 本地 Hunyuan3D 2.1 批量生产 → Blender 脚本后处理 → Unity MCP 自动导入 → URP Cel-Shading 统一渲染。

**核心原则**：低多边形几何 + PBR 纹理细节 + Cel-shading 渲染 = "有质感但很风格化的办公废土"。

**覆盖范围**：角色（12个）、家具（40种）、建筑模块（5种）、武器（13种）、物品（32种），共计 ~100 个独立资产。

---

## 2. Player Fantasy（美术视角）

- **严肃玩法 × 荒诞皮囊**：射击手感、死亡惩罚、战术决策都是硬核搜打撤标准；但角色是 Q 版穿西装的动物，武器是订书机和键盘，敌人是 KPI 丧尸和 PPT 怨灵
- **2.5D 俯视角下的质感**：摄像机虽然远，但 4K 分辨率下每张贴图都经得起放大——木纹桌面有咖啡渍、西装有褶皱、金属有锈迹
- **越深越暗**：顶层办公楼明亮整洁，越往下灯光越昏暗、墙壁越破败——PBR 贴图的污损程度逐层递增

---

## 3. Detailed Rules

### 3.1 混合管线架构

```
概念阶段（云端）
  Unity MCP + Tripo/TJGenerators
  每品类生成 2-3 个样品，确认风格方向
         │ 风格锁定
         ▼
批量生产（本地 4070 Ti SUPER）
  ComfyUI + Hunyuan3D 2.1
  每资产跑 3-5 变体，人工挑最优
         │ GLB 原始输出 (~600K 面)
         ▼
后处理工站（本地 Blender 脚本）
  - Decimate 降面到品类预算
  - UV 修正
  - 尺寸归一米制
  - 轴心点 bottom-center
         │ 清洁 FBX + PNG 贴图
         ▼
引擎集成（Unity MCP + Shader Graph）
  - 自动创建 Material（Cel-Shading 变体）
  - 自动挂载 PBR 贴图
  - 自动创建 Prefab
  - 文件夹归位
         │
         ▼
质量门禁（脚本 + 人工）
  - 面数/尺寸/轴心自动检查
  - 角色额外检查：头身比、T-pose、Mixamo 兼容
  - FAIL → 退回重跑
```

### 3.2 角色管线

**核心路径**：Stable Diffusion 出 2D 参考图 → Hunyuan3D 2.1 image-to-3D → Blender 精修 → Mixamo 自动绑骨。

**Hunyuan3D 2.1 VRAM 策略**（16GB 卡）：
- 形状模型（10GB）：本地直接跑
- 纹理模型（21GB → 2K 模式 ~12GB）：本地 --lowvram 分 tile 跑
- 备选：SaladCloud 云端跑纹理 $0.009/个

**角色分级**：

| Tier | 对象 | 策略 | 迭代轮数 | 面数预算 |
|------|------|------|----------|----------|
| T1 | 牛主角、马主角 | SD 三视图 → 混元 image-to-3D → Blender 精修比例 | 3-5 | 15K-25K |
| T2 | CEO、经理Boss、PPT怨灵Boss、精英保安Boss | SD 出图 → 混元 image-to-3D → Blender 修正 | 3-5 | 12K-20K |
| T3 | KPI丧尸、PPT怨灵、邮件幽灵、会议恶魔、保安、精英保安队长 | 混元 text-to-3D → 挑最优 → 轻量 Blender | 1-3 | 8K-15K |
| T4 | 马主角 | 牛模型换头 + 贴图复用身体 | 1-2 | 复用 T1 |

**主角三视图规范**（SD 生成的参考图标准）：
- 正面：T-pose，正对镜头
- 背面：T-pose，背对镜头
- 可选侧面：45° 或正侧
- 纯色背景（白色或中性灰），无阴影
- 全身占画面 80%+

**普通敌人可以用 text-to-3D 的原因**：
- KPI 丧尸："穿破烂西装的瘦削人形丧尸，办公室风格"——text prompt 足够描述
- PPT 怨灵 + 邮件幽灵：半透明/发光效果靠 Shader 补，模型本身简单
- 标准人形是混元 2.1 训练数据中最常见的类别

### 3.3 场景道具管线

**按复杂度分层**：

| Layer | 占比 | 策略 | 面数预算 | 贴图分辨率 |
|--------|------|------|----------|-----------|
| L0 | ~30% | Unity ProBuilder/原语拼接，不跑 AI | 50-200 | 512/纯色材质 |
| L1 | ~50% | 混元 text-to-3D 一次过 | 200-1000 | 1K-2K |
| L2 | ~20% | SD 出参考图 → 混元 image-to-3D | 300-1200 | 2K-4K |

**40 种家具分层清单**：

| L0（简单几何） | L1（单次 text-to-3D） | L2（image-to-3D） |
|---------------|----------------------|-------------------|
| 垃圾桶 | 员工办公桌 | CEO 办公桌 |
| 衣帽架 | 主管办公桌 | 酒柜 |
| 考勤板 | 文件柜 | 书架 |
| 白板 | 打印机 | 真皮沙发 |
| 碎纸机 | 饮水机 | 油画 |
| 公告板 | 咖啡机 | 保险柜 |
| 隔板 | 冰箱 | 鱼缸 |
| 建议箱 | 微波炉 | 服务器机架 |
| 钥匙柜 | 休息沙发 | UPS 电源 |
| 植物（大） | 零食架 | IT 工作台 |
| 天花板检修口 | 会议桌 | 前台接待桌 |
| | 投影仪 | |
| | 免提电话 | |
| | 配线架 | |
| | 行政办公桌 | |
| | 财务办公桌 | |
| | 报表架 | |
| | 等候沙发 | |
| | 档案墙 | |

**贴图分辨率标准**（4K 画质目标）：

| 品类 | 三角面 | 贴图分辨率 |
|------|--------|-----------|
| 房间建筑模块 | 300-800 | 4K |
| 大型家具（会议桌、沙发、CEO 桌） | 500-1200 | 2K-4K |
| 中小型家具（办公桌、文件柜） | 300-800 | 2K |
| 小道具（咖啡机、订书机） | 200-500 | 1K |
| 微型物品（回形针、U盘） | 100-200 | 512 |
| 武器 | 500-1000 | 2K |

### 3.4 武器管线

13 种武器全部走 L2 image-to-3D（武器是玩家盯着看的东西，质量要求高）：

```
SD 出参考图 → 混元 image-to-3D → Blender 精修
  → Prefab 必须包含 WeaponSocket 空节点（枪口/握柄位置）
  → 近战武器额外包含碰撞轨迹参考
```

**武器清单**：
- A 类远程：订书机手枪、键盘霰弹枪、投影仪射线枪、马克杯投掷器
- C 类脑洞：PPT 发射器、会议邀请函法杖、邮件炸弹、咖啡因注射器
- 近战：碎纸机锯、网线鞭、KPI 报表重锤、键盘板砖、马克杯流星锤

### 3.5 风格一致性保障

1. **SD checkpoint 统一**：所有 image-to-3D 的参考图使用同一套 SD 模型 + LoRA
2. **Prompt 模板**：所有 text prompt 统一追加风格后缀
3. **Cel-Shading 是最后的统一层**：无论 AI 模型输出有多差异，3 档 Shader 变体压平光影 → 视觉语言统一

### 3.6 Cel-Shading Shader 规范

**架构**（URP Shader Graph）：

```
Albedo ← PBR Albedo Map
Metallic ← PBR Metallic Map
Roughness ← PBR Roughness Map
         │
    ┌────▼────┐
    │ Diffuse  │  N·L → 3-4 阶 ramp（亮面/灰面/暗面/阴影）
    │ Ramp     │  色阶分界点可调
    └────┬────┘
         │
    ┌────▼────┐
    │ Specular │  镜面高光 → 亮阶 ramp
    │ Ramp     │  保留金属感，不被 flat shading 吃掉
    └────┬────┘
         │
    ┌────▼────┐
    │ Rim      │  背光边缘亮边（Fresnel）
    │ Light    │  增强 2.5D 俯视角立体感
    └────┬────┘
         │
    ┌────▼────┐
    │ Outline  │  Inverted Hull 描边，可开关
    │          │  角色 1.5px / 家具 1px / 物品 0px
    └─────────┘
```

**3 种材质变体**：

| 变体 | Diffuse 阶数 | Outline | 适用对象 |
|------|------------|---------|---------|
| Standard | 3 阶 | 1px | 家具、建筑模块、武器 |
| Character | 4 阶 | 1.5px | 玩家、敌人、Boss |
| Item | 2 阶 | 无描边 | 微型物品、战利品掉落 |

### 3.7 Prefab 自动化流程

**Blender 导出规范**：
```
Assets/_Project/Models/{Category}/{Name}/
  ├── {Name}.fbx          # 几何体
  ├── {Name}_Albedo.png   # 4K/2K/1K 按品类
  ├── {Name}_Metallic.png
  └── {Name}_Roughness.png
```

**Unity MCP 自动导入步骤**：
1. `AssetDatabase.Refresh()`
2. 遍历 `Assets/_Project/Models/` 下新增 FBX
3. 读取同目录 PNG 贴图
4. 创建 Cel-Shading Material 实例（根据品类选变体）
5. 三张贴图挂入对应 slot
6. Material 赋值给所有 MeshRenderer
7. 创建 Prefab 到 `Assets/_Project/Prefabs/{Category}/{Name}.prefab`
8. 设置 scale（1 unit = 1 meter）

### 3.8 Blender 后处理脚本规范

批处理脚本对每个 GLB 执行：
1. **Decimate**：按品类预算降面（Collapse 模式，保持 UV）
2. **UV Check**：检测 UV 岛是否重叠/越界，自动修正常见错位
3. **Scale Normalize**：按品类标准尺寸缩放到米制
4. **Pivot**：轴心点移至 bottom-center（Y=模型最低点，XZ=包围盒中心）
5. **Export**：FBX + 三张 PNG 贴图到指定目录

---

## 4. Formulas

### 4.1 面数预算公式

```
角色: 8K-25K（Tier 决定）
大型家具: 500-1200
中小家具: 300-800
小道具/武器: 200-1000
微型物品: 100-200

PC 4K 目标：场景总面数 ≤ 500K（约 40 家具 + 5 模块 + 10 敌人 + 1 玩家）
```

### 4.2 贴图分辨率公式

```
分辨率 = min(品类上限, 屏幕占比 × 4K / 最小可辨尺寸)

俯视角摄像机高度 ~15m，视野宽度 ~25m：
- 大物体（会议桌 3m）屏幕占比 ~12% → 4K × 0.12 = 491px 需要 → 2K 裕量
- 小物体（咖啡杯 0.1m）屏幕占比 ~0.4% → 4K × 0.004 = 16px → 256 够用

实际取值取品类上限（详见 3.3 表格）
```

### 4.3 VRAM 预算（混元 2.1 本地跑）

```
形状模型: 10GB
纹理模型:
  4K 模式: ~21GB → 需 32GB 卡，不可本地
  2K 模式: ~12GB → 可本地（配合 --lowvram）
  1K 模式: ~8GB  → 轻松本地

流程：
  1. 形状模型常驻 VRAM
  2. 形状完成后 unload
  3. 纹理模型以 2K 模式加载
  4. 对于需要 4K 贴图的大物件：本地跑 2K → SaladCloud 跑 4K 兜底
```

### 4.4 生成时间预估

```
单角色（image-to-3D + 2K 纹理）: ~200-250 秒/个
单家具（text-to-3D + 1K 纹理）: ~120-150 秒/个
单武器（image-to-3D + 2K 纹理）: ~180-200 秒/个

全部 100 个资产粗略估算:
  12 角色 × 4 分钟 = 48 分钟
  40 家具 × 2.5 分钟 = 100 分钟
  13 武器 × 3 分钟 = 39 分钟
  5 建筑模块 × 2 分钟 = 10 分钟
  32 物品 × 1 分钟 = 32 分钟
  ────────────────────────────
  合计 ≈ 229 分钟 ≈ 4 小时

  加上选优、Blender 后处理、Unity 导入 → 预估 2-3 个完整工作日
```

---

## 5. Edge Cases

| 场景 | 处理方式 |
|------|----------|
| 混元 2.1 生成结果完全走形（比例错乱、结构崩塌） | 重跑。同一 prompt 最多重跑 3 次，3 次失败则切换为 SD+Tripo 云端备选路径 |
| 贴图 UV 接缝太大无法自动修复 | 标记为需手动处理，放入 `_manual_fix/` 队列，不在自动管线中卡住 |
| 混元 VRAM OOM（16GB 不够） | 形状模型正常 → 纹理阶段降为 1K 或切换到 SaladCloud |
| 动物 Q 版比例 AI 总是跑偏（头太大/太小） | Blender 后处理阶段缩放头部骨骼，通过脚本统一头身比 |
| 两个不同 AI 工具生成的资产风格不一致 | Cel-Shading Shader 是最后的统一层；如果仍然明显不一致，表示 2D 参考图风格不够统一 → 重做 SD checkpoint |
| 武器握持点和动画不匹配 | WeaponSocket 空节点位置可手动调整，不阻塞入库 |
| 建筑模块拼装时漏光 | 增加模块边缘 overlap（5cm），或加一个全局遮挡面 |
| ComfyUI 批量生成中途崩溃 | 断点续跑设计——已完成的 GLB 不会被覆盖，ComfyUI 重启后继续未完成的品类 |

---

## 6. Dependencies

### 本管线依赖的外部系统

| 系统 | 依赖内容 | 状态 |
|------|----------|------|
| Unity MCP (CoplayDev) | 团结引擎内 AI 生成 + 场景操控 | ✅ 已安装 |
| ComfyUI Desktop | 本地 AI 3D 生成宿主 | 🔧 安装中 |
| Hunyuan3D 2.1 (ComfyUI nodes) | 核心 3D 生成模型 | ⏳ 待安装 |
| Stable Diffusion (ComfyUI / WebUI) | 2D 参考图生成 | ⏳ 待配置 |
| Blender (≥ 4.0) | 后处理批脚本运行环境 | ⏳ 待配置 |
| Mixamo (在线) | 角色自动绑骨 | 免费，需 Adobe 账号 |
| SaladCloud (备选) | 4K 纹理云端生成 | 备选，按需 |

### 依赖本管线的系统

| 系统 | 需要本管线提供 |
|------|--------------|
| Floor Generation (#6) | 房间建筑模块 Prefab（5 种） |
| Loot & Economy (#7) | 战利品容器模型（办公桌、文件柜、保险柜、服务器、补给柜） |
| Weapon System (#4) | 13 种武器模型 + WeaponSocket |
| Enemy AI (#5) | 10 种敌人模型 + Animator |
| Player (#2) | 2 种主角模型 + 全套动画 |
| UI/HUD (#8) | 物品图标（可从 3D 模型渲染生成） |

---

## 7. Tuning Knobs

| 参数 | 默认值 | 范围 | 影响 |
|------|--------|------|------|
| Decimate 比率 | 品类预算 | 50%-200% 预算 | 面数越低性能越好，越高细节越多 |
| Diffuse Ramp 阶数 | 3 (Standard) | 2-5 阶 | 阶数越多越写实，越少越卡通 |
| Outline 粗细 | 1px | 0-3px | 描边越粗越漫画感 |
| Rim Light 强度 | 0.3 | 0-1 | 背光边缘亮度，太高会失真 |
| SD 参考图 checkpoint | （待定） | 任意 SD 1.5/XL 模型 | 换 checkpoint 会改变整个 2D→3D 风格基调 |
| 混元纹理分辨率 | 2K | 1K/2K/4K | 影响 VRAM 和最终贴图质量 |
| 每资产变体数 | 3 | 1-5 | 越多选择余地越大，但生成时间线性增长 |
| 重试次数上限 | 3 | 1-5 | 超过上限切换到备选路径 |

---

## 8. Acceptance Criteria

### 管线就绪标准

- [ ] ComfyUI Desktop 成功运行，Hunyuan3D 2.1 节点可生成 GLB
- [ ] SD 可在本地生成正面 T-pose 参考图
- [ ] Blender 批处理脚本对测试模型执行 decimate + UV fix + scale + pivot + export 全流程无报错
- [ ] Unity MCP 脚本对新 FBX 执行材质创建 + Prefab 创建一次通过
- [ ] URP Cel-Shading Shader 在 Game View 4K 分辨率下渲染正确

### 资产质量标准

- [ ] 一个牛主角完整走通：SD 出图 → 混元 3D → Blender → Mixamo 绑骨 → Unity Animator Idle 动画无扭曲
- [ ] 一个 KPI 丧尸完整走通：text-to-3D → Blender → Unity，10 分钟内完成
- [ ] 一个办公桌完整走通：text-to-3D → Blender → Unity Prefab + LootContainer 组件
- [ ] 3 个代表资产（角色 + 家具 + 武器）在 4K Game View 截图中通过人眼检查：风格统一、无可见瑕疵

### 批量生产标准

- [ ] 第一批（风格验证）：1 桌子 + 1 文件柜 + 1 KPI 丧尸 + 1 牛主角，全部通过质量门禁
- [ ] 第二批（场景底座）：5 种建筑模块 + 走廊 + 门，拼装测试无漏光
- [ ] 第三批（填充家具）：第一批 10 种家具入库
- [ ] 全部 100 个资产在 3 个工作日内完成入库

---

## 附录 A：ComfyUI 环境清单

```
基础:
  ComfyUI Desktop (已安装中)

Custom Nodes:
  ComfyUI-Hunyuan3D-2    — 混元 2.1 核心节点
  ComfyUI-KJNodes         — 批量处理辅助
  ComfyUI-Manager         — 节点管理
  Efficiency Nodes        — 性能优化

Checkpoints:
  SD 1.5 或 SDXL checkpoint（用于 2D 参考图生成）
  Hunyuan3D-Shape-v2-1（形状模型，10GB）
  Hunyuan3D-Paint-v2-1（纹理模型，21GB/2K 模式 ~12GB）
```

## 附录 B：资产优先级与批次规划

```
Batch 1 — 风格验证 (Day 0, ~1h)
  Desk_Employee_01, Cabinet_File_01, Enemy_KPIZombie, Player_Ox

Batch 2 — 场景底座 (Day 1 AM, ~2h)
  Room_Office, Room_Conference, Room_Hallway, Room_Stairwell, Room_TeaRoom
  + Corridor_Straight, Door_Single

Batch 3 — 核心家具 (Day 1 PM, ~2h)
  前 10 种高优先级家具（EmployeeDesk, FileCabinet, CoffeeMachine,
  ConferenceTable, ServerRack, Safe, Printer, WaterDispenser, SupplyCabinet, ReceptionDesk）

Batch 4 — 角色全套 (Day 2 AM, ~2h)
  6 普通敌人 + 2 主角变体

Batch 5 — Boss + 剩余家具 (Day 2 PM, ~2h)
  4 Boss + 额外 10 种家具

Batch 6 — 武器 + 物品 (Day 3, ~3h)
  13 武器 + 32 物品（物品大部分 L0 跳过 AI，集中在武器）
```

---

*End of 3D Asset Pipeline Design Document.*
