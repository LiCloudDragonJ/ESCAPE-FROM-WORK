# ESCAPE FROM WORK

办公题材俯视角 extraction shooter（搜打撤），Unity 6 + URP 开发。

## 快速开始

1. Clone 到本地
2. 用 Unity Hub 打开 `ESCAPE FROM WORK/` 目录（Unity 6000.x）
3. 打开场景即可运行

## 项目结构

```
├── README.md                    # 项目说明
├── ESCAPE FROM WORK/            # Unity 工程
│   └── Assets/_Project/         # 游戏代码和资源
│       ├── Scripts/             # C# 代码
│       ├── Prefabs/             # 预制体
│       └── ScriptableObjects/   # 数据资产
├── design/                      # 游戏设计文档
│   ├── gdd/                     # 设计文档
│   └── registry/                # 实体注册表
├── docs/                        # 参考文档
│   ├── engine-reference/        # Unity 引擎参考
│   ├── superpowers/             # 开发计划
│   └── tooling/                 # 工具参考
└── production/                  # 开发管理
    └── session-state/           # 会话状态
```

## CCGS 集成

本仓库配合 Claude Code Game Studios (CCGS) 使用。CCGS 是开发工具，独立安装在本地，不包含在此仓库中。通过 CCGS 的 `/start` 流程加载本项目目录即可使用全套 Agent + Skill 工作流。
