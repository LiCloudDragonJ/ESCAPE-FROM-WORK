# ESCAPE FROM WORK

办公题材俯视角 extraction shooter（搜打撤），Unity 6 + URP 开发。

## Technology Stack

- **Engine**: 团结引擎 1.9.3 (Tuanjie Engine — Unity 中国版)
- **Language**: C#
- **Rendering**: URP
- **Platform**: PC (Windows)
- **Version Control**: Git

## Project Structure

```
├── README.md                    # 项目说明
├── CLAUDE.md                    # 本文件 (AI 助手指引)
├── ESCAPE FROM WORK/            # Unity 工程
│   └── Assets/_Project/
│       ├── Scripts/             # C# 代码
│       │   ├── Core/            # 核心系统 (GameManager、事件、相机)
│       │   ├── Player/          # 玩家 (移动、战斗、背包、交互)
│       │   ├── Enemies/         # 敌人 (KPI 丧尸、敌人生成器)
│       │   ├── Weapons/         # 武器 (近战、远程、投射物)
│       │   ├── Level/           # 楼层生成
│       │   ├── Loot/            # 战利品系统
│       │   ├── UI/              # HUD、死亡界面、纪念墙
│       │   ├── Data/            # ScriptableObject 数据定义
│       │   └── Editor/          # 编辑器工具
│       ├── Prefabs/             # 预制体
│       └── ScriptableObjects/   # 数据资产 (武器、敌人、物品)
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

## Engine Version Reference

@docs/engine-reference/README.md

## Technical Preferences

@.claude/docs/technical-preferences.md

## Coordination Rules

@.claude/docs/coordination-rules.md

## Collaboration Protocol

**User-driven collaboration, not autonomous execution.**
Every task follows: **Question -> Options -> Decision -> Draft -> Approval**

- Agents MUST ask "May I write this to [filepath]?" before using Write/Edit tools
- Agents MUST show drafts or summaries before requesting approval
- Multi-file changes require explicit approval for the full changeset
- No commits without user instruction

See `docs/COLLABORATIVE-DESIGN-PRINCIPLE.md` for full protocol and examples.

## Coding Standards

@.claude/docs/coding-standards.md

## Context Management

@.claude/docs/context-management.md

## CCGS Integration

本仓库集成了 Claude Code Game Studios (CCGS) 全套 Agent + Skill 工作流。
包含 49 个专业 Agent、73 个 Skill、12 个 Hook 和 11 个路径级编码规范。

### 快速开始

1. 用 Unity Hub 打开 `ESCAPE FROM WORK/` 目录 (Unity 6000.x / 团结引擎 1.9.3)
2. 在本目录启动 Claude Code，运行 `/start` 初始化开发会话
3. 当前处于 **原型阶段 (Phase 1)**，核心搜打撤循环已跑通

### 常用命令

| 命令 | 用途 |
|------|------|
| `/start` | 初始化/恢复开发会话 |
| `/dev-story` | 实现一个 Story |
| `/code-review` | 代码审查 |
| `/design-system` | 设计新系统 |
| `/brainstorm` | 头脑风暴新功能 |
| `/sprint-status` | 查看 Sprint 进度 |
| `/help` | 完整命令列表 |
