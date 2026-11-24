# CLAUDE.md

## 项目

ESCAPE FROM WORK —— 办公题材第一人称搜打撤（PvPvE），团结引擎 1.11.1 + URP。唯一工程在 `game/`。

## 技术栈

- 引擎：团结引擎 1.11.1（基座 Unity 2022.3.62t18），URP 14.2.0-t1，Input System 1.14.4-t3
- 语言：C#（.NET Standard 2.1）
- 平台：Windows PC

## 目录

```
game/Assets/_Project/Scripts/    Core / Data / LevelGen / Gameplay / AI / UI（各自 asmdef）
game/Assets/_Project/Scenes/     Whitebox.unity（M0）、FloorTest.unity（M1）
game/Assets/tests/               EditMode / PlayMode
docs/3d-rebuild/                 决策 / 技术方案 / 关卡设计 / 里程碑
design/gdd/                      存档：旧 2.5D GDD，仅供参考，不代表当前设计
```

## 约定

- 只用团结 1.11.1 打开 `game/`；同一工程同时只允许一个编辑器实例。
- 生成器必须确定性：同一 seed 结果完全一致，禁止 `UnityEngine.Random`（用 `System.Random`）。
- asmdef 分层：LevelGen 不依赖 Gameplay；Rules 类逻辑保持纯 C#（为联机预留）。
- 测试命令见 README；`-runTests` 不能带 `-quit`。
- 设计文档只写进 `docs/3d-rebuild/`；旧的 `design/gdd` 不再更新。
- 未经用户明确指示不要 commit / push。

## 当前进度

M0 完成（工程、FPS 控制器、白盒、PerfHud）。M1 生成器完成主体（BSP、掩体、隔断墙、多门、连通兜底、出生区吸附、撤离点散布自检、Floor Lab、3D 实体化、地图迷雾 HUD）。剩余：运行时 8 块分块生成、60fps/灯池实测、PropCatalog 美术接口。
