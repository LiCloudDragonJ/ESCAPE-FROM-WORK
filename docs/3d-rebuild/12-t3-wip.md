# T3 运行时多楼层（2026-11-28 打通）

## 现在是什么状态

四层已经真的在运行时里叠起来了，出包能跑：

- `game/Assets/_Project/Scripts/Gameplay/TowerRuntime.cs`：`SlicePlanner.Generate(seed)` 拿到 4 层 `RaidPlan`，按 `FloorGeometry.FloorToFloor`（4.2m）逐层建几何；`ActiveRadius`/`NeighbourRadius` 控制只留当前层（±邻居）；NavMesh 每层用 `NavMesh.AddNavMeshData(data, Vector3.up * LevelY, identity)` 偏移，层与层之间互不串路；切层统一走 `SwitchTo(index)`，`FirstPersonController.Teleport` 到该层电梯中心并清速度。
- `game/Assets/_Project/Scripts/Gameplay/EscapeFromWork.Gameplay.asmdef`：references 加 `EscapeFromWork.LevelGen`（T3 之前编译失败的原因）。
- `game/Assets/_Project/Scripts/Editor/TowerSetup.cs`：菜单 `Escape From Work/T3/Build Tower Test Scene` 生成 `Assets/_Project/Scenes/TowerTest.unity`（Player + CameraRig + TowerRuntime，材质 `M1FloorScene.BuildMaterials()`，Catalog `PropCatalog.LoadOrCreateDefault()`）；菜单 `Escape From Work/T3/Build Windows Player` 出 `Builds/Windows/EFW_Tower.exe`。
- `game/Assets/_Project/Scripts/Editor/TowerCapture.cs`：菜单 `Escape From Work/T3/Capture Tower Screenshots` 出四张剖视图（每层隐藏天花板与灯盘、3/4 俯视）+ 一张爆炸视图；菜单 `Escape From Work/T3/Capture Wall Plan` 出语义平面图（白=墙、红=实心、蓝=地面）。
- `game/Assets/tests/EditMode/TowerPlanTests.cs`：四层连续、四层电梯同 rect、每对相邻层 1 电梯+2 楼梯、四层独立 seed、`LevelY(i)=i*4.2`。

## 实机证据

出包后带 `-towerCapture` 自动跑：逐层 `SwitchTo` → 传送到该层最长走廊中段 → 截图 `tower-run-f1..f4.png` → 退出。日志：

```
[Tower] F0 (world 45F) built in 566 ms
[Tower] F1 (world 46F) built in 581 ms
[Tower] F2 (world 47F) built in 572 ms
[Tower] F3 (world 48F) built in 523 ms
[Tower] autoplay capture done
```

四层建造 0.5–0.6s/层，进程正常退出（exit 0）。

## 打包时的坑

Player 里 `Shader.Find("Unlit/Transparent")` 会拿到 null（shader 只被字符串引用，打包时被剥离），标识牌直接抛
`InvalidOperationException: Signage requires the Unlit/Transparent shader.`。
现在 `SignageRenderer` 先 `Resources.Load<Material>("SignageUnlit")`，材质由菜单 `Escape From Work/Art/Rebuild Signage Material`
生成在 `Assets/_Project/Resources/SignageUnlit.mat`；拿不到才退 `Shader.Find`。

## 下一步

1. 电梯井现在四面是墙，玩家进层后要自己走出来；下一步给核心筒开电梯门（或把电梯厅并进走廊）。
2. 智能体/怪物 NavMesh 要按层登记，T9 的每块 NavMesh 与 T3 的层偏移共用一套。
3. `TowerTest.unity` 目前只验证切层，没接 HUD（楼层条属 T4）。
