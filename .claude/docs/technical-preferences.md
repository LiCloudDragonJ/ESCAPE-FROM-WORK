# Technical Preferences

<!-- Written by Claude Code, 2026-07-15. Updated as decisions are made throughout development. -->
<!-- All agents reference this file for project-specific standards and conventions. -->

## Engine & Language

- **Engine**: 团结引擎 1.9.3 (Tuanjie Engine — Unity 中国版, compatible with Unity 6000.x)
- **Language**: C# (.NET Standard 2.1)
- **Rendering**: URP (Universal Render Pipeline)
- **Physics**: Unity Physics (2D for gameplay colliders, 3D for visuals)

## Input & Platform

<!-- Read by /ux-design, /ux-review, /test-setup, /team-ui, and /dev-story -->
<!-- to scope interaction specs, test helpers, and implementation to the correct input methods. -->

- **Target Platforms**: PC (Windows)
- **Input Methods**: Keyboard/Mouse, Gamepad
- **Primary Input**: Keyboard/Mouse
- **Gamepad Support**: Partial (planned, not yet implemented)
- **Touch Support**: None
- **Platform Notes**: PC-only single player for MVP. Architecture reserves interfaces for potential co-op.

## Naming Conventions

- **Classes**: PascalCase (e.g. `PlayerController`, `EnemyBase`)
- **Variables**: camelCase (e.g. `moveSpeed`, `currentHealth`)
- **Serialized Fields**: camelCase with `[SerializeField]` attribute
- **Events**: PascalCase with `On` prefix (e.g. `OnPlayerDeath`, `OnFloorCleared`)
- **Files**: PascalCase matching class name
- **Scenes/Prefabs**: PascalCase, descriptive (e.g. `PlayerCharacter`, `KPIZombie`)
- **Constants**: UPPER_SNAKE_CASE

## Performance Budgets

- **Target Framerate**: 60 FPS
- **Frame Budget**: 16.67ms
- **Draw Calls**: TBD (2.5D top-down, relatively light per scene)
- **Memory Ceiling**: 4GB (PC target)

## Testing

- **Framework**: Unity Test Framework (UTF)
- **Minimum Coverage**: Core gameplay systems (combat, loot, floor generation)
- **Required Tests**: Combat damage formulas, loot table distributions, floor generation validity

## Forbidden Patterns

- `FindObjectOfType<>()` in `Update()` or hot paths
- Hardcoded magic numbers (use ScriptableObject data assets or named constants)
- Direct scene references across systems (use `GameEvents` event bus)

## Allowed Libraries / Addons

- Unity Input System
- Unity Addressables (for asset management)
- TextMeshPro (UI text)

## Architecture Decisions Log

<!-- Quick reference linking to full ADRs in docs/architecture/ -->
- [No ADRs yet — use /architecture-decision to create one]

## Engine Specialists

<!-- Read by /code-review, /architecture-decision, /architecture-review, and team skills -->
<!-- to know which specialist to spawn for engine-specific validation. -->

- **Primary**: unity-specialist
- **Language/Code Specialist**: (n/a — unity-specialist handles C#)
- **Shader Specialist**: unity-shader-specialist
- **UI Specialist**: unity-ui-specialist
- **Additional Specialists**: unity-addressables-specialist
- **Routing Notes**: This project uses the Unity agent set. For general Unity code, use unity-specialist. For DOTS/ECS, use unity-dots-specialist (not currently in use).

### File Extension Routing

<!-- Skills use this table to select the right specialist per file type. -->

| File Extension / Type | Specialist to Spawn |
|-----------------------|---------------------|
| Game code (.cs) | unity-specialist |
| Shader / material files (.shader, .shadergraph, .vfx) | unity-shader-specialist |
| UI / screen files (.uxml, .uss, Canvas) | unity-ui-specialist |
| Scene / prefab / level files (.unity, .prefab) | unity-specialist |
| Asset bundles / Addressables | unity-addressables-specialist |
| General architecture review | unity-specialist |
