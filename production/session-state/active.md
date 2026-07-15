# Session State — ESCAPE FROM WORK

## Current Status: Phase 1 Core Prototype — Playable

**Date:** 2026-07-16

### ✅ Complete
- Player: WASD, aim (Left Shift toggle), shoot, melee, dodge
- Enemies: KPI zombie AI (patrol/chase/attack), floating damage numbers
- Floor: 5×5 grid, boundary walls, dark ground
- Camera: 2.5D perspective (SimpleCameraFollow)
- HUD: health bar, ammo, floor info, extraction warning, interaction prompt
- Loot containers: 5 types (desk/cabinet/safe/supply/server), progressive loading
- Container UI: 3-column (equip|backpack|container), double-click/drag/F-key transfer
- Equipment: weapons/armor/backpack slots, equip/unequip via drag
- Backpack: grid display with stack counts, Tab to open
- Extraction points: stairs (SW) + fire escape (NE)
- Loose loot: 8 big valuables, floating+rotating
- 30+ item types across 6 rarities (white/green/blue/purple/gold/red)
- Scene builder: one-click Build Scene menu

### Build Steps (Unity Editor)
1. `ESCAPE FROM WORK → Build Scene`
2. Play

### Key Files
- GDD: `design/gdd/game-concept.md`
- Scene: `Assets/Scenes/SampleScene.scene`
- Main code: `Assets/_Project/Scripts/`
- Editor: `Assets/_Project/Scripts/Editor/SceneWirer.cs`
- Loot UI: `Assets/_Project/Scripts/UI/LootContainerUI.cs`

### Next Steps
1. Fix shooting direction (bullets → mouse cursor)
2. Random floor generation per save
3. Test full extraction loop (enter → fight → loot → extract)
4. Tea room base / hideout building system
5. Code cleanup: split bloated SceneWirer.cs, remove BENGLAOTOU imports

<!-- STATUS -->
Epic: Core Prototype
Feature: Loot & Inventory System
Task: Container state + equipment equip/unequip
<!-- /STATUS -->
