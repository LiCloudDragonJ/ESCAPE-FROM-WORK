# AI 3D 模型生成 & MCP 自动化工作流参考

> 项目引擎：**团结引擎（Tuanjie Engine）1.8+**
> 生成时间：2026-07-15
> 状态：MCP 配置中，尚未验证完整工作流

---

## 一、整体方案架构

```
Claude Code (或其他 AI 客户端)
    │
    ├── STDIO ──► Unity MCP (CoplayDev/unity-mcp)
    │                  │
    │                  ▼
    │              团结引擎编辑器
    │                  │
    │     ┌────────────┼────────────┐
    │     ▼            ▼            ▼
    │  TJGenerators  AI Graph   外部 API
    │  (Tripo P1)   (混元3D)   (Meshy等)
    │     │            │            │
    │     └────────────┼────────────┘
    │                  ▼
    │          GLB/FBX → Assets/
    │          PBR材质自动挂载
    │          骨骼/动画可选
    │                  │
    │                  ▼
    │          场景中实例化
```

---

## 二、可用工具清单

### 🔌 MCP 自动化（目标方案）

| 产品 | 仓库/地址 | 价格 | 团结兼容版本 |
|------|-----------|------|-------------|
| **Unity MCP** (CoplayDev) | [github.com/CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp) | MIT 开源免费 | 团结 1.8+ |
| **Coplay MCP** | coplay.dev | 免费 Beta | 团结 1.8+ |
| **Coplay**（编辑器插件） | coplay.dev | 付费 | 团结 1.8+ |

### 🧩 团结引擎内置

| 工具 | 安装方式 | 价格 | 底层模型 |
|------|----------|------|----------|
| **TJGenerators** | 团结 Hub → 扩展管理 → 安装 | 公测免费（有额度） | Tripo P1 |
| **AI Graph** | Package Manager → Tuanjie Registry | 公测免费（有额度） | 腾讯混元 3D |
| **Codely** | 内置 | 公测免费（有额度） | GLM-5.2-MAX |

### 🌐 外部在线服务（网页生成 → 导出 GLB/FBX → 导入）

| 工具 | 网站 | 免费额度 | 付费方案 |
|------|------|----------|----------|
| **Meshy AI** | [meshy.ai](https://meshy.ai) | 100 积分/月 | Pro $20/月（含商用授权） |
| **Tripo AI** | [tripo3d.ai](https://tripo3d.ai) | 300 积分/月 | Pro $19.9/月（含商用授权） |
| **Rodin (Hyper3D)** | [hyper3d.ai](https://hyper3d.ai) | 7 天试用 | Creator $24/月 |
| **Magic3D** | [magic3d.io](https://magic3d.io) | — | $19.9/月 |

### 🆓 开源本地方案（需 GPU）

| 项目 | 仓库 | VRAM 需求 | 输出格式 |
|------|------|-----------|----------|
| **腾讯混元3D 2.1** | [Tencent-Hunyuan/Hunyuan3D-2.1](https://github.com/tencent-hunyuan/hunyuan3d-2.1) | 10-21 GB | GLB/OBJ/FBX + PBR |
| **ComfyUI AI GameDev** | [mattwilliamson/comfyui-ai-gamedev](https://github.com/mattwilliamson/comfyui-ai-gamedev) | 同混元 | GLB |
| **Hybrid 2D-to-3D** | [OmerFarukMerey/Hybrid-2d-to-3d](https://github.com/OmerFarukMerey/Hybrid-2d-to-3d-asset-generation-pipeline) | 可变 | GLB/FBX |

---

## 三、Unity MCP 安装步骤（团结引擎）

### 前置条件

- 团结引擎 **1.8+**
- Python **3.10+**
- `pip install uv`

### 安装

```
1. Unity Package Manager → Add package from git URL →
   https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main

2. Window → MCP for Unity → Setup → 配置环境

3. Toggle MCP Window → 连接模式选 STDIO → 复制配置

4. 粘贴配置到 AI 客户端（Claude Code / Cursor / VS Code 等）
```

### 常见问题

| 问题 | 解决 |
|------|------|
| Python 环境报错 | 确保 `python --version` ≥ 3.10，`uv` 已安装 |
| 团结版本不兼容 | 升级到 1.8+ |
| 连接不上 | 检查连接模式是否为 STDIO（非 HTTP） |
| 防火墙拦截 | 允许团结引擎网络权限 |

> 📖 参考教程：[构建Unity(团结引擎)MCP+Trae/Cursor生产线](https://jishuzhan.net/article/2021532892539715585)

---

## 四、目标自动化工作流

MCP 配置完成后，在 Claude Code 中可直接用自然语言操控团结引擎：

```
用户: "生成一个低多边形骑士角色，写实PBR材质，带待机动画，放到场景原点"
         │
         ▼
Claude Code → Unity MCP → 团结引擎
         │
         ├── 1. 调用 Tripo P1 / Meshy 生成 3D 模型
         ├── 2. 下载 GLB 到 Assets/Models/
         ├── 3. 自动提取/挂载 PBR 材质
         ├── 4. 设置 Humanoid Rig（如适用）
         ├── 5. 在场景原点实例化 Prefab
         └── 6. 返回结果确认
```

### 典型指令示例

```
# 资产生成
"在团结引擎中生成一个赛博朋克风格的武器道具，导出为 FBX"
"把这张概念图转成 3D 模型，导入当前场景"

# 场景操作
"在当前场景创建一个空的 GameObject，挂上 Rigidbody"
"选中场景中所有敌人，批量添加 NavMeshAgent 组件"

# 代码生成
"为选中的角色生成一个第三人称控制器脚本"
"在当前场景创建一套完整的 UI 菜单：开始、设置、退出"
```

---

## 五、引擎与定价参考

### 团结引擎

| 版本 | 条件 | 费用 |
|------|------|------|
| 个人版 | 年收入 < 20 万美元（~150 万 RMB） | **免费** |
| 中小团队（小游戏） | 年收入 150 万–1500 万 RMB | ¥18,170/年/团队 |
| 企业版 | 年收入 > 1500 万 RMB | ¥18,170/年 + 构建费 |

### 资源商店

- 中国区：[assetstore.u3d.cn](https://assetstore.u3d.cn/)（海外 Asset Store 已停止中国区服务）
- 注意：国际版 Unity Asset Store 上的资源（如 PromptModel）可能不在中国商店

---

## 六、重要注意事项

1. **格式偏好**：团结引擎 AI 生成默认输出 **GLB**，如需 FBX 可用 FBX Exporter 插件转换
2. **商用授权**：免费生成通常有 CC 许可限制；付费版（$20/月左右）含完整商用授权
3. **面数优化**：AI 生成的模型面数偏高，导入后建议手动减面（手游 5K–15K，PC 15K–40K）
4. **角色绑定**：Tripo AI 自动绑骨效果最好，其他工具生成的角色可能需要在 Blender 中手动 Rig
5. **Steam 上架**：需在提交时披露是否使用 AI 生成资产
6. **MCP + 第三方 API**：Coplay/Unity MCP 自身免费，但底层 3D 生成（Meshy/Tripo API）可能产生费用

---

## 七、下一步

- [ ] 在另一个会话中完成 Unity MCP 安装与连接
- [ ] 验证基本连通性（如"在场景中创建一个 Cube"）
- [ ] 测试 3D 模型生成指令
- [ ] 建立标准的资产生成 SOP
- [ ] 确认 TJGenerators 公测结束后的正式定价

---

## 八、关键链接速查

| 资源 | 地址 |
|------|------|
| Unity MCP 仓库 | https://github.com/CoplayDev/unity-mcp |
| 团结引擎 AI 手册 | https://docs.unity.cn/cn/tuanjiemanual/Manual/TuanjieAI.html |
| 团结引擎中国资源商店 | https://assetstore.u3d.cn/ |
| MCP+团结配置教程 | https://jishuzhan.net/article/2021532892539715585 |
| 团结引擎开发者社区 | https://developer.unity.cn/ |
| Meshy AI | https://meshy.ai |
| Tripo AI | https://tripo3d.ai |
| Hyper3D Rodin | https://hyper3d.ai |
| 腾讯混元3D (GitHub) | https://github.com/tencent-hunyuan/hunyuan3d-2.1 |
