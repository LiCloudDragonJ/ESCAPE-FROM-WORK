# T3 进行中（2026-11-28 13:20 暂停，等重启）

## 为什么停

Windows 的 **Smart App Control** 从 12:52 起开始拦截团结编辑器的编译宿主：

```
CodeIntegrity 事件 3077/3033（12:56/12:57 等）：
netcorerun.exe 尝试加载 ...\Editor\Data\Tools\BuildPipeline\Bee.Tools.dll，
未满足签名要求/被应用程序控制策略阻止 (0x800711C7)
```

表现是编辑器只报一句 `Scripts have compiler errors`，日志里没有任何 `error CS`；清 `Library/Bee`、整个 `Library` 重建、直接跑 `ScriptCompilationBuildProgram.exe` 都是同一个报错。当前账号不是管理员，改不了该策略。用户已决定去关 Smart App Control 并重启。

## 本地未提交的 T3 代码（重启后继续）

- `game/Assets/_Project/Scripts/Gameplay/TowerRuntime.cs`：四层按 `FloorGeometry.FloorToFloor`（4.2m）叠放，`NeighbourRadius` 控制只留当前层；在核心筒内按 E/Q 上下一层；切层时 `FirstPersonController.Teleport` 到电梯中心；`FloorGeometry.BuildNavMesh` 的 NavMeshData 用 `NavMesh.AddNavMeshData(data, Vector3.up * LevelY, identity)` 按层高偏移。
- `game/Assets/_Project/Scripts/Gameplay/EscapeFromWork.Gameplay.asmdef`：references 加 `EscapeFromWork.LevelGen`（之前 T3 编译失败的原因之一）。
- `game/Assets/_Project/Scripts/Editor/TowerSetup.cs`：菜单 `Escape From Work/T3/Build Tower Test Scene` 生成 `Assets/_Project/Scenes/TowerTest.unity`（Player + CameraRig + TowerRuntime，材质用 `M1FloorScene.BuildMaterials()`，Catalog 用 `PropCatalog.LoadOrCreateDefault()`）；另一个菜单出 Windows 包。
- `game/Assets/_Project/Scripts/Editor/TowerCapture.cs`：菜单 `Escape From Work/T3/Capture Tower Screenshots`，四层叠放后每层从电梯口拍一张 `tower-f1..f4.png`，外加 `tower-stack.png` 外观。
- `game/Assets/tests/EditMode/TowerPlanTests.cs`：四层连续、电梯四层同 rect、每对相邻层 1 电梯+2 楼梯、四层 seed 独立、`TowerRuntime.LevelY(i) = i*4.2`。

以上都还没编译过（被 SAC 拦住），所以没有提交。

## 重启后的第一步

1. 全量编译（Tuanjie batchmode smoke），确认 `NO COMPILE ERRORS`；
2. `TowerPlanTests` 过滤跑；
3. `TowerSetup.Build` 生成 TowerTest 场景，`TowerCapture` 出五张图；
4. 进 TowerTest 里实机走一遍切层（E/Q）与电梯对位；
5. 全量 EditMode + 提交推送，再回到 goal 的剩余项（分区覆盖、道具密度、出包）。
