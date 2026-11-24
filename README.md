# ESCAPE FROM WORK

办公题材第一人称搜打撤（extraction shooter，PvPvE）。一栋 50 层的程序化企业巨构：进楼 → 搜刮 → 交战 → 撤离，或者赌一把继续下坠。

- 引擎：团结引擎 1.11.1（基座 Unity 2022.3.62t18）+ URP 14.2.0-t1
- 平台：Windows PC（单机优先，联机预留）
- 当前阶段：M1 程序化楼层生成器（M0 地基已完成）

## 仓库结构

```
game/                        团结引擎工程（唯一工程，必须用 1.11.1 打开）
  Assets/_Project/Scripts/   Core / Data / LevelGen / Gameplay / AI / UI
  Assets/_Project/Scenes/    Whitebox.unity（M0 白盒）、FloorTest.unity（M1 生成楼层）
  Assets/_Project/Materials/ 灰盒材质
  Assets/tests/              EditMode / PlayMode 测试
docs/3d-rebuild/             3D 重构：决策、技术方案、关卡设计、里程碑、出图、AI 3D 参考
design/gdd/                  存档：旧 2.5D 版本 GDD，仅作素材/数值参考，不代表当前设计
```

## 打开与运行

1. 用团结引擎 **1.11.1** 打开 `game/`（不要用别的版本，会静默改写 ProjectVersion）。
2. 白盒：打开 `Assets/_Project/Scenes/Whitebox.unity` → Play → F3 看帧率。
3. 生成楼层：菜单 `Escape From Work/M1/Build 3D Floor Scene` 重建 `FloorTest.unity` → Play；按 M 开地图（区域级迷雾 + 本队撤离点常显，不暂停）。

## 测试

```bash
"<团结安装目录>/2022.3.62t18/Editor/Tuanjie.exe" -batchmode \
  -projectPath "<repo>/game" -runTests -testPlatform EditMode \
  -testResults "<repo>/game/m1-tests.xml" -logFile m1-tests.log
```

路径按本机安装位置替换（例如 `F:\Workspace\UNITY\2022.3.62t18\Editor\Tuanjie.exe`）。
注意：`-runTests` **不能带 `-quit`**（会 exit 0 但不生成 XML）；同一工程同时只允许一个编辑器实例。

## 生成器与编辑器工具

- `LevelGen/FloorGenerator.cs`：BSP 随机切分（随机切、切分时预留走廊带、多门、环路门、几何连通兜底、出生区吸附、撤离点散布自检）。同一 seed 完全确定。
- `LevelGen/CoverProps.cs`：走廊掩体三档双侧成组刷点 + 长走廊隔断墙。
- `LevelGen/FloorGeometry.cs`：灰盒几何合并、NavMesh 烘焙、灯池。
- `UI/MapHud.cs`：地图迷雾与撤离点标记。
- 菜单 `Tools/EFW/Floor Lab`：调参 + 俯视出图，`Floor Lab — 批量出图` 出多 seed 对比。

## 设计文档

| 文档 | 内容 |
|---|---|
| [00-decisions.md](docs/3d-rebuild/00-decisions.md) | 已定决策（FPS、PvPvE、逐层匹配、存档点、Boss、地图迷雾…） |
| [01-tech-plan.md](docs/3d-rebuild/01-tech-plan.md) | 技术方案（工程、包、asmdef、生成管线预算、存档、联机） |
| [02-level-design.md](docs/3d-rebuild/02-level-design.md) | 关卡设计（尺度、层高吊顶、走廊分级与掩体、出生撤离、地图迷雾） |
| [03-milestones.md](docs/3d-rebuild/03-milestones.md) | 里程碑与 M0/M1 验收状态 |
