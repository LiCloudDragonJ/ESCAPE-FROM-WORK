# Systems Index -- ESCAPE FROM WORK

> **Draft**, generated 2026-07-15. Review and adjust before proceeding to /design-system.
> Status: READ-ONLY analysis. No game code was modified.

---

## System List

| # | System | Priority | Dependencies | Owner | GDD Status | Code Status | Key Files |
|---|--------|----------|--------------|-------|------------|-------------|-----------|
| 1 | Core Game Loop & Game Manager | **MVP** | (none -- foundational) | unity-specialist | Section in game-concept | **Partial** | GameManager.cs, QuickStart.cs, SceneBootstrap.cs, GameEvents.cs |
| 2 | Player Movement & Input | **MVP** | Core Game Manager | unity-specialist | Section in game-concept | **Implemented** | PlayerController.cs |
| 3 | Combat System (aim, shoot, melee, dodge, cover) | **MVP** | Player Movement, Weapon System, Enemy AI | unity-specialist | Section in game-concept | **Partial** | PlayerAim.cs, PlayerCombat.cs, PlayerHealth.cs |
| 4 | Weapon System | **MVP** | Core (IDamageable), Data definitions | unity-specialist | Needs separate GDD | **Partial** | WeaponBase.cs, RangedWeapon.cs, MeleeWeapon.cs, Projectile.cs, WeaponData.cs |
| 5 | Enemy AI | **MVP** | Core (IDamageable), Floor Generation | unity-specialist | Needs separate GDD | **Partial** | EnemyBase.cs, KPIZombie.cs, EnemySpawner.cs, EnemyData.cs |
| 6 | Floor Generation | **MVP** | (none -- provides rooms) | unity-specialist | Needs separate GDD | **Partial** | FloorGenerator.cs, FloorManager.cs, FloorState.cs, RoomModule.cs |
| 7 | Loot & Economy | **MVP** | Data definitions, Player Inventory | unity-specialist | Needs separate GDD | **Partial** | LootContainer.cs, LootTable.cs, PickupItem.cs, ItemData.cs |
| 8 | UI / HUD | **MVP** | Player Health, Inventory, Combat, Weapon System | unity-ui-specialist | Section in game-concept | **Partial** | HUDManager.cs, DeathScreen.cs, MemorialWall.cs |
| 9 | Death & Inheritance | **Post-MVP** | Save/Load, Loot & Economy, Base Building | unity-specialist | Needs separate GDD | **Not started** | DeathScreen.cs (UI only) |
| 10 | Elevator & Stairs | **Post-MVP** | Floor Generation, Power System | unity-specialist | Section in game-concept | **Not started** | -- |
| 11 | Power System | **Post-MVP** | Floor Generation | unity-specialist | Section in game-concept | **Not started** | -- |
| 12 | Evacuation Signals | **Post-MVP** | Floor Generation, Enemy AI, Loot & Economy | unity-specialist | Section in game-concept | **Not started** | -- |
| 13 | Narrative System | **Post-MVP** | Floor Generation, Key Items, Save/Load | game-designer | Section in game-concept | **Not started** | -- |
| 14 | Key Items (Badges, USB Drives) | **Post-MVP** | Loot & Economy, Narrative, Save/Load | unity-specialist | Section in game-concept | **Not started** | ItemData.cs (general, not key-item-specific) |
| 15 | Base Building (Tea Room) | **Post-MVP** | Loot & Economy, Death & Inheritance | unity-specialist | Needs separate GDD | **Not started** | -- |
| 16 | Save/Load & Meta-Progression | **Post-MVP** | (nearly everything -- save last) | unity-specialist | Needs separate GDD | **Not started** | -- |
| 17 | Camera System | **MVP** | Player Movement | unity-specialist | Not in GDD (implied) | **Implemented** | SimpleCameraFollow.cs, SimpleCameraSetup.cs |

---

## Dependency Graph

```
                                        ┌──────────────────────────────────┐
                                        │      Core Game Loop & Game      │
                                        │          Manager (#1)           │
                                        └───────┬──────────────────────────┘
                                                │
                ┌───────────────────────────────┼───────────────────────────────┐
                │                               │                               │
                ▼                               ▼                               ▼
    ┌───────────────────────┐       ┌───────────────────────┐       ┌───────────────────────┐
    │ Player Movement &     │       │     Floor Generation  │       │  Save/Load & Meta-    │
    │ Input (#2)            │       │         (#6)          │       │  Progression (#16)    │
    └───────────┬───────────┘       └───────────┬───────────┘       └───────────────────────┘
                │                               │
                ▼                               ├──────────────┬─────────────────┬──────────────────┐
    ┌───────────────────────┐                   │              │                 │                  │
    │   Combat System (#3)  │                   ▼              ▼                 ▼                  ▼
    └──┬──────────────┬─────┘        ┌───────────────────┐ ┌────────────┐ ┌───────────────┐ ┌──────────────┐
       │              │              │   Enemy AI (#5)   │ │   Power    │ │  Evacuation   │ │  Narrative   │
       ▼              ▼              └─────────┬─────────┘ │  System    │ │  Signals (#12)│ │  System (#13)│
    ┌───────────┐  ┌──────────────┐            │           │   (#11)    │ └───────────────┘ └──────┬───────┘
    │  Weapon   │  │  Player      │            │           └────────────┘                           │
    │  System   │  │  Inventory   │            │                                                     │
    │   (#4)    │  │  (in #3)    │            │                                                     │
    └───────────┘  └──────────────┘            │                                                     │
                                              │                                                     │
                                              ▼                                                     ▼
                                    ┌──────────────────────┐                            ┌──────────────────────┐
                                    │   Loot & Economy     │                            │    Key Items (#14)    │
                                    │        (#7)          │                            └──────────────────────┘
                                    └──────────┬───────────┘
                                               │
                                               ▼
                                    ┌──────────────────────┐
                                    │   Base Building (#15) │
                                    └──────────┬───────────┘
                                               │
                                               ▼
                                    ┌──────────────────────┐
                                    │    Death & Inherit-   │
                                    │    ance (#9)           │
                                    └──────────────────────┘

                        ┌───────────────────────────────────────────┐
                        │         UI / HUD (#8) [cross-cutting]     │
                        │  depends on: Health, Inventory, Combat,   │
                        │  Weapons, Death, Memorial Wall            │
                        └───────────────────────────────────────────┘

                        ┌───────────────────────────────────────────┐
                        │    Elevator & Stairs (#10)                │
                        │  depends on: Floor Generation, Power      │
                        └───────────────────────────────────────────┘
```

### Dependency Rules

1. **No downward arrows in the graph reverse.** If System A depends on B, B's design must complete before A's detailed design can be finalised.
2. **Cross-cutting systems** (UI/HUD) can be designed in parallel but require stable interfaces from their dependents.
3. **Save/Load (#16)** must be designed last because it serialises every other system's state -- designing it early risks expensive rework.
4. **Elevator & Stairs (#10)** depends on Power System because elevators require electricity to function.

---

## Design Order

Recommended sequence for detailed GDD authoring (each "needs separate GDD" or "section in game-concept" gets an authoring pass).

### MVP Tier (playable extraction loop)

| Order | System | Why This Order |
|-------|--------|----------------|
| 1 | **Core Game Loop & Game Manager** | Foundational. Must exist for anything else to run. Already in code. |
| 2 | **Player Movement & Input** | Player needs to exist and move before anything interacts. Already implemented. |
| 3 | **Combat System** | Core of the extraction loop. Aim, shoot, melee, dodge formulas need precise specification. |
| 4 | **Weapon System** | The tools of combat. Per-weapon stats, ammo types, loadout rules need GDD. Currently has StaplerPistol + KeyboardMelee data only. |
| 5 | **Enemy AI** | Targets for combat. Needs behavior specs for all 4 common enemy types. Currently only KPI Zombie implemented. |
| 6 | **Floor Generation** | The arena. Module placement, room types, fixed vs random, special floor structure. Currently 5 room prefabs exist. |
| 7 | **Loot & Economy** | Reward loop. Containers, loot tables, drop rates, resource economy (paperclips, paper, coffee beans, USB). Desktop loot table exists, others needed. |
| 8 | **UI / HUD** | Player feedback. Health bar, ammo count, floor indicator, inventory screen, death screen. Scripts exist but not wired in scene. |

### Post-MVP Tier (inheritance, base, traversal)

| Order | System | Why This Order |
|-------|--------|----------------|
| 9 | **Death & Inheritance** | Defines the stakes of extraction. Permadeath, corpse recovery, insurance, memorial wall. Must define before other systems balance around risk. |
| 10 | **Base Building** | The "safe space" progression. Tea room facilities, upgrade tree, relocation costs. Depends on Loot (resources to spend) and Death (why you need a base). |
| 11 | **Save/Load & Meta-Progression** | Serialization of all persistent state. Badge flags, base upgrades, resource totals, floor progress. Must know every other system's save data shape. |
| 12 | **Elevator & Stairs** | Floor traversal mechanics. Speed vs risk tradeoffs, elevator noise, stair encounter chance formula. |
| 13 | **Power System** | Blackout conditions, flashlight gameplay, server rack puzzles. Scripted in MVP but needs system design. |
| 14 | **Evacuation Signals** | Dynamic pressure systems. Security broadcasts, overtime bells, risk/reward escalation. |
| 15 | **Key Items** | Badge (permanent flags), USB (blueprint unlocks). Already referenced in economy but needs standalone design for unlock flow. |
| 16 | **Narrative System** | Story delivery per floor, information discovery arc, four endings. Must know which floors are special and what key items unlock them. |

---

## Design Dependency Chain (Critical Path)

```
Player Input ───> Combat ───> Enemy AI
                      │
                      └──> Weapon System
Core Game Loop ───> Floor Generation ───> Loot & Economy ───> Base Building ───> Death & Inheritance
                                                │                                  │
                                                └────> Save/Load <────────────────┘
```

**Critical path through MVP**: Core -> Player -> Combat + Weapons -> Enemy AI -> Floor Gen -> Loot -> UI/HUD. Everything else branches off this spine.

---

## System Details

### 1. Core Game Loop & Game Manager
- **Priority**: MVP
- **Dependencies**: None (foundational)
- **Owner**: unity-specialist
- **GDD Status**: Section 4 in game-concept
- **Code Status**: Partial. GameManager.cs, GameEvents.cs, IDamageable.cs, SceneBootstrap.cs, QuickStart.cs exist. Handles event bus and scene bootstrapping. Phase 1 core loop (enter -> loot -> fight -> extract) is the stated next step.
- **Key Files**: `GameManager.cs`, `GameEvents.cs`, `IDamageable.cs`, `SceneBootstrap.cs`, `QuickStart.cs`

### 2. Player Movement & Input
- **Priority**: MVP
- **Dependencies**: Core Game Manager
- **Owner**: unity-specialist
- **GDD Status**: Section 8 (controls table) in game-concept
- **Code Status**: Implemented. WASD movement, mouse aiming direction, dodge (Space), all wired in SampleScene.
- **Key Files**: `PlayerController.cs`

### 3. Combat System
- **Priority**: MVP
- **Dependencies**: Player Movement, Weapon System, Enemy AI (for targeting)
- **Owner**: unity-specialist
- **GDD Status**: Section 8 in game-concept. Covers auto-aim vs manual aim, cover, melee basics.
- **Code Status**: Partial. PlayerAim.cs, PlayerCombat.cs, PlayerHealth.cs exist. Auto-aim finds enemies but bullet direction needs fixing. Known issue in session state.
- **Key Files**: `PlayerAim.cs`, `PlayerCombat.cs`, `PlayerHealth.cs`
- **Needs GDD for**: Formula details (damage calculation, aim cone, dodge i-frames, cover reduction %), edge cases (multiple enemies in auto-aim cone, dodge through obstacles).

### 4. Weapon System
- **Priority**: MVP
- **Dependencies**: Core (IDamageable), Data definitions (WeaponData)
- **Owner**: unity-specialist
- **GDD Status**: Section 9 in game-concept. All 4 A-class, 4 C-class, 5 melee weapons listed with ammo types and behavior descriptions.
- **Code Status**: Partial. WeaponBase.cs (abstract), RangedWeapon.cs, MeleeWeapon.cs, Projectile.cs exist. Data assets: SO_Weapon_StaplerPistol (ranged), SO_Weapon_KeyboardMelee (melee). One ranged and one melee wired.
- **Key Files**: `WeaponBase.cs`, `RangedWeapon.cs`, `MeleeWeapon.cs`, `Projectile.cs`, `WeaponData.cs`
- **Needs GDD for**: Per-weapon stat tables (damage, fire rate, range, reload time, ammo capacity), mod slot system (MVP 1-slot, architecture for 3), ammo resource conversions, weapon tier/rarity system (if any).

### 5. Enemy AI
- **Priority**: MVP
- **Dependencies**: Core (IDamageable), Floor Generation (navmesh/spawn points)
- **Owner**: unity-specialist
- **GDD Status**: Section 10 in game-concept. 4 common types, 2 security types, 4 bosses listed with behaviors and spawn floors.
- **Code Status**: Partial. EnemyBase.cs (abstract), KPIZombie.cs (patrol/chase/attack states), EnemySpawner.cs, EnemyData.cs (SO data). Only KPI Zombie implemented with basic AI.
- **Key Files**: `EnemyBase.cs`, `KPIZombie.cs`, `EnemySpawner.cs`, `EnemyData.cs`
- **Needs GDD for**: Each enemy type's behavior tree (states, transitions, parameters), detection radius, attack patterns, damage values, spawn rules per floor, boss phase transitions.

### 6. Floor Generation
- **Priority**: MVP
- **Dependencies**: None (provides rooms for other systems)
- **Owner**: unity-specialist
- **GDD Status**: Sections 5-6 in game-concept. 50-floor structure, 10 special floors, 40 random floors, module-based generation, module types (offices, conference, hallway, stairwell, tea room).
- **Code Status**: Partial. FloorGenerator.cs (5x5 grid), FloorManager.cs, FloorState.cs, RoomModule.cs. 5 room prefabs exist (Office, Conference, Hallway, Stairwell, TeaRoom). Basic grid generation works.
- **Key Files**: `FloorGenerator.cs`, `FloorManager.cs`, `FloorState.cs`, `RoomModule.cs`
- **Needs GDD for**: Module placement algorithm (rules for adjacency, corridor connectivity, dead-end prevention), special floor blueprints, loot container seed positions, enemy spawn point placement rules.

### 7. Loot & Economy
- **Priority**: MVP
- **Dependencies**: Data definitions, Player Inventory (subsystem of Combat)
- **Owner**: unity-specialist
- **GDD Status**: Section 12 in game-concept. 5 resources (paperclips, printer paper, coffee beans, USB, badges), currency, ammo safety net, coffee freshness timer, container refresh rules.
- **Code Status**: Partial. LootContainer.cs, LootTable.cs, PickupItem.cs, ItemData.cs exist. Item SO assets: Paperclip, PrinterPaper, CoffeeBean, USB. One loot table asset (SO_Loot_OfficeDesk). Inventory in PlayerInventory.cs.
- **Key Files**: `LootContainer.cs`, `LootTable.cs`, `PickupItem.cs`, `ItemData.cs`, `PlayerInventory.cs`
- **Needs GDD for**: Loot table probabilities for each container type, resource stack sizes, vendor pricing (paperclip as currency), coffee freshness decay formula, ammo crafting recipes, badge tier values.

### 8. UI / HUD
- **Priority**: MVP
- **Dependencies**: Player Health, Inventory, Combat, Weapon System, Death (for death screen)
- **Owner**: unity-ui-specialist
- **GDD Status**: Scattered across sections (controls table in 8, death in 13, memorial wall in 17). No consolidated UI section.
- **Code Status**: Partial. HUDManager.cs, DeathScreen.cs, MemorialWall.cs exist but are not instantiated in the scene (known issue).
- **Key Files**: `HUDManager.cs`, `DeathScreen.cs`, `MemorialWall.cs`
- **Needs GDD for**: Full UI layout specification (HUD elements, inventory screen, death screen, memorial wall, settings), input-to-UI mapping, screen flow (death -> character select -> base -> elevator select -> floor).

### 9. Death & Inheritance
- **Priority**: Post-MVP
- **Dependencies**: Save/Load, Loot & Economy, Base Building
- **Owner**: unity-specialist
- **GDD Status**: Section 13 in game-concept. Permadeath, character selection, corpse recovery, insurance, base inheritance, memorial wall, safe room vault.
- **Code Status**: Not started. DeathScreen.cs exists (UI placeholder). No death/permadeath logic, no character selection, no corpse recovery.
- **Needs GDD for**: Death event flow (trigger -> loot calculation -> character select -> respawn), drop rules per floor safety status, corpse recovery mechanics (map marker, time limit, scavenging by security), insurance cost calculation.

### 10. Elevator & Stairs
- **Priority**: Post-MVP
- **Dependencies**: Floor Generation, Power System
- **Owner**: unity-specialist
- **GDD Status**: Section 14 in game-concept. Elevator (safe floors only, instant, noise risk), stairs (always available, per-floor animation, encounter chance formula), fire escape (one-way exit, cross-map), decision triangle.
- **Code Status**: Not started. No scripts for elevator or stair traversal.
- **Needs GDD for**: Elevator noise radius and duration, stair encounter probability formula (base 15% per floor, scaling with floor count), animation timing, UI for floor selection.

### 11. Power System
- **Priority**: Post-MVP
- **Dependencies**: Floor Generation
- **Owner**: unity-specialist
- **GDD Status**: Section 15 in game-concept. Scripted (not dynamic) for MVP. Blackout floors with flashlight cone, IT server puzzle.
- **Code Status**: Not started.
- **Needs GDD for**: Flashlight cone angle/range, darkness visibility radius, generator puzzle mechanics (IT server rack interaction), power restoration effects (lights on, elevator enabled, noise spawn).

### 12. Evacuation Signals
- **Priority**: Post-MVP
- **Dependencies**: Floor Generation, Enemy AI, Loot & Economy
- **Owner**: unity-specialist
- **GDD Status**: Section 16 in game-concept. Security broadcasts (random timer, 3-min evacuation, reinforcement swarm) and overtime bell (loot threshold trigger, speed buff, loot bonus).
- **Code Status**: Not started.
- **Needs GDD for**: Signal trigger conditions and probabilities, spawn reinforcement count and composition, overtime loot multiplier, UI warnings.

### 13. Narrative System
- **Priority**: Post-MVP
- **Dependencies**: Floor Generation, Key Items, Save/Load
- **Owner**: game-designer
- **GDD Status**: Section 18 in game-concept. Full story, 10-floor information discovery arc, 4 endings.
- **Code Status**: Not started.
- **Needs GDD for**: Story delivery method (pickups, terminals, enemy drops, environmental), trigger conditions per special floor, branching endings, dialogue/terminal text assets.

### 14. Key Items
- **Priority**: Post-MVP
- **Dependencies**: Loot & Economy, Narrative, Save/Load
- **Owner**: unity-specialist
- **GDD Status**: Section 17 in game-concept. Badge system (dog tag equivalent, tiers, permanent flags), USB drives (blueprint unlock, stackable).
- **Code Status**: Not started. ItemData.cs exists as generic item data. No badge-specific or USB-specific logic.
- **Needs GDD for**: Badge data fields and tier system, permanent flag save format, USB consumption mechanics, badge NPC vendor pricing formula.

### 15. Base Building
- **Priority**: Post-MVP
- **Dependencies**: Loot & Economy, Death & Inheritance
- **Owner**: unity-specialist
- **GDD Status**: Section 11 in game-concept. Tea room base, 4 facilities (workbench, medical corner, intel board, coffee machine), per-floor unique unlocks, relocation formula.
- **Code Status**: Not started.
- **Needs GDD for**: Facility upgrade trees (costs, effects per level), relocation formula, per-floor unique function list, menu UI design.

### 16. Save/Load & Meta-Progression
- **Priority**: Post-MVP (design last)
- **Dependencies**: Nearly everything (serialises state from all systems)
- **Owner**: unity-specialist
- **GDD Status**: Not explicitly in GDD (implied by base inheritance, badge permanent flags).
- **Code Status**: Not started.
- **Needs GDD for**: Save data model (permanent flags, base state, resource totals, floor progress, inventory), save trigger points (on extraction, on death, on quit), save file format and location, meta-progression that crosses death boundaries.

### 17. Camera System (supplementary)
- **Priority**: MVP
- **Dependencies**: Player Movement
- **Owner**: unity-specialist
- **GDD Status**: Not in GDD (technical implementation)
- **Code Status**: Implemented. SimpleCameraFollow.cs (follow + boundary clamping), SimpleCameraSetup.cs.
- **Key Files**: `SimpleCameraFollow.cs`, `SimpleCameraSetup.cs`

---

## Code-Implementation Gap Analysis

### MVP Systems Status

| System | Code Files | Wired in Scene? | Missing for Playable Loop |
|--------|------------|-----------------|---------------------------|
| Core Game Loop | 5 files | Yes (Bootstrap -> GameManager) | Extraction trigger (what happens when player exits) |
| Player Movement | 1 file | Yes (on PlayerCharacter prefab) | -- |
| Combat System | 3 files | Partial. Aim + Combat on Player. | Bullet direction fix (known issue), dodge cooldown not tuned |
| Weapon System | 4 files + 2 SOs | Partial. StaplerPistol + KeyboardMelee wired. | 6 more weapons not implemented, mod system not started |
| Enemy AI | 4 files + 1 SO | Partial. KPI Zombie placed in scene. | 3 more common types + 2 security types not implemented |
| Floor Generation | 4 files | Partial. 5x5 grid works. | Room connectivity, door placement, loot/enemy seed integration |
| Loot & Economy | 4 files + 5 item SOs + 1 table SO | No. Desktop loot table exists but no containers in scene. | Container spawning in floors, item pickup interaction, inventory UI |
| UI / HUD | 3 files | No. Scripts not instantiated. | Canvas prefab, health bar, ammo display, inventory screen instantiation |

### Quick Wins (smallest effort to complete MVP loop)

1. **Fix bullet direction** -- `PlayerAim.cs` targeting calculation (known issue, one function)
2. **Wire HUD canvas** -- Instantiate HUDManager + canvas with health bar and ammo display
3. **Add loot containers** -- Place LootContainer in room prefabs, wire LootTable
4. **Implement extraction trigger** -- Zone trigger at fire escape / stairwell, end raid flow
5. **Implement basic floor lock/unlock** -- Elevator UI to select floors, transition between generated floors

---

## Open Questions for /design-system

1. **Loot table depth**: Should MVP have one loot table per container type (desk, cabinet, safe) or one global table with position-based weighting?
2. **Death penalty severity**: Should MVP enforce full death rules immediately, or use a lighter version until other systems are in place?
3. **Floor vertical slice**: Should the first playable demo use all 50 floors (thin) or 5-8 floors (thick, polished)?
4. **Save system scope**: JSON-based local save acceptable for MVP, or invest in binary serialisation early?
5. **Mod slot architecture**: Build for 3 now (with only 1 active in MVP) or add slots incrementally?

---

*End of systems index. Review this document, adjust priorities, then proceed to /design-system for detailed GDD authoring of individual systems.*
