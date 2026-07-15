# Codebase Architecture Audit

**Project**: ESCAPE FROM WORK
**Analysis Date**: 2026-07-15
**Total Scripts**: 36 C# files (across 9 namespaces)
**ScriptableObjects**: 8 assets
**Prefabs**: 8
**Scenes**: 1 (SampleScene.scene)
**Unit Tests**: 0
**Assembly Definitions**: 0

---

## 1. System Map (ASCII Architecture Diagram)

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         GAME MANAGER (God Object)                       │
│                   GameManager (singleton, DontDestroyOnLoad)             │
│                   ┌──────────────────────────────────────────────┐      │
│                   │ State: MainMenu / InRaid / BaseBuilding /    │      │
│                   │        Dead / Victory                        │      │
│                   │ Tracks: CurrentFloorNumber (1-50), LastDeath │      │
│                   │ Events: onFloorEnter, onFloorExtract,        │      │
│                   │         onPlayerDied, onFloorCleared,        │      │
│                   │         onNewCharacterSelected               │      │
│                   └──────────────────────────────────────────────┘      │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
          ┌─────────────────────────┼─────────────────────────────┐
          │                         │                             │
          ▼                         ▼                             ▼
┌──────────────────┐    ┌───────────────────┐    ┌──────────────────────┐
│   CORE (7 files) │    │    EVENTS         │    │    DATA (3 files)    │
│                  │    │  GameEvent (SO)   │    │                      │
│ GameManager      │    │  GameEvent<T>     │    │ EnemyData (SO)       │
│ IDamageable      │    │  IntEvent         │    │ ItemData (SO)        │
│ DeathContext     │    │  FloatEvent       │    │ WeaponData (SO)      │
│ CharacterMemorial│    │  BoolEvent        │    │                      │
│ SceneBootstrap   │    │  StringEvent      │    │ Enum: ItemType       │
│ QuickStart       │    │  DeathContextEvent│    │ Enum: AmmoType       │
│ SimpleCameraSetup│    └───────────────────┘    │ Enum: WeaponClass    │
│ SimpleCameraFollow│                            │ Enum: WeaponSlot     │
└──────────────────┘                             └──────────────────────┘
          │
          ├──────────────────────────────────────────────────────────────┐
          │                                                              │
          ▼                                                              ▼
┌─────────────────────────┐                  ┌──────────────────────────────┐
│    PLAYER (6 files)     │                  │    WEAPONS (4 files)         │
│                         │                  │                              │
│ PlayerController        │◄────────────────►│ WeaponBase (abstract)        │
│   - Movement (Rigidbody)│                  │   ├── RangedWeapon           │
│   - Dodge (coroutine)   │                  │   │   - Projectile spawning  │
│   - Aim direction       │                  │   │   - Spread / headshot    │
│                         │                  │   └── MeleeWeapon            │
│ PlayerAim               │                  │       - Charge attack        │
│   - Auto aim (default)  │                  │       - OverlapSphere swing  │
│   - Manual aim (hold)   │                  │                              │
│   - Lock establishment  │                  │ Projectile                   │
│                         │                  │   - XZ flight                │
│ PlayerCombat            │                  │   - Damage on hit            │
│   - 3 weapon slots      │                  │   - Type-C special effects   │
│   - Shoot/Melee/Reload  │                  └──────────────────────────────┘
│   - IDamageable         │
│   - Death + drops       │
│                         │
│ PlayerHealth            │
│   - IDamageable         │
│   - Heal / Die          │
│                         │
│ PlayerInventory         │
│   - 16 backpack slots   │
│   - Add/Remove/Count    │
│   - Weapon equip/cycle  │
│   - Total value calc    │
│                         │
│ PlayerInteraction       │
│   - IInteractable       │
│   - OverlapSphere query │
│   - E key to interact   │
└─────────────────────────┘
          │
          ├──────────────────────────────────────────────────────────────┐
          │                                                              │
          ▼                                                              ▼
┌─────────────────────────┐                  ┌──────────────────────────────┐
│  ENEMIES (3 files)      │                  │    LEVEL (4 files)           │
│                         │                  │                              │
│ EnemyBase (abstract)    │                  │ FloorGenerator               │
│   - State machine       │                  │   - Grid-based generation    │
│     Idle/Patrol/Chase   │                  │   - Seeded determinism       │
│     /Attack/Dead        │                  │   - Room placement           │
│   - IDamageable         │                  │   - Connection computation   │
│   - Status effects      │                  │                              │
│     (Blind/Root/Taunt)  │                  │ FloorManager                 │
│   - Loot drops          │                  │   - Per-raid singleton       │
│   - MoveToward helpers  │                  │   - InitializeFloor()        │
│                         │                  │   - OnEnemyKilled()          │
│ KPIZombie               │                  │   - Extract()                │
│   - PatrolBehavior()    │                  │                              │
│   - OverlapSphere detect│                  │ FloorState                   │
│   - Melee PerformAttack │                  │   - isCleared, lastEntryTime │
│                         │                  │   - Loot decay multiplier    │
│ EnemySpawner             │                  │   - Container looted set     │
│   - Random spawn zones  │                  │   - 24h visit tracking       │
│   - SpawnFloorEnemies() │                  │                              │
│   - CountLivingEnemies()│                  │ RoomModule                   │
└─────────────────────────┘                  │   - RoomType                 │
                                             │   - Connection flags (NSEW)  │
                                             │   - enemySpawnZones[]        │
                                             │   - lootContainerSpawns[]    │
                                             └──────────────────────────────┘
          │
          ├──────────────────────────────────────────────────────────────┐
          │                                                              │
          ▼                                                              ▼
┌─────────────────────────┐                  ┌──────────────────────────────┐
│    LOOT (3 files)       │                  │     UI (3 files)             │
│                         │                  │                              │
│ LootTable (SO)          │                  │ HUDManager (singleton)       │
│   - Weighted selection  │                  │   - Health bar / ammo        │
│   - minRolls/maxRolls   │                  │   - Weapon slot icons        │
│   - Roll() returns []   │                  │   - Floor info / status      │
│                         │                  │   - Extraction timer         │
│ LootContainer           │                  │   - Interaction prompt       │
│   - IInteractable       │                  │   - Damage vignette          │
│   - Roll + Add to inv   │                  │   - Polling in Update()      │
│   - Overflow → Pickup   │                  │                              │
│   - Open/closed visuals │                  │ DeathScreen                  │
│                         │                  │   - Shows DeathContext       │
│ PickupItem              │                  │   - "New Character" button   │
│   - IInteractable       │                  │                              │
│   - Adds to inventory   │                  │ MemorialWall                 │
│   - Self-destructs      │                  │   - onPlayerDied subscription│
└─────────────────────────┘                  │   - UI entry instantiation   │
                                             └──────────────────────────────┘
          │
          ▼
┌─────────────────────────┐
│  EDITOR (2 files)       │
│                         │
│ SceneWirer              │
│   - Menu: Build Scene   │
│   - Procedural scene    │
│   - Floor grid + enem.  │
│                         │
│ WeaponWirer             │
│   - Menu: Wire Weapons  │
│   - Wires weapons/inv   │
└─────────────────────────┘
```

### Cross-Cutting Contracts

```
IDamageable (Core)
    ├── EnemyBase (Enemies)        → TakeDamage → Die → loot drops
    ├── PlayerCombat (Player)      → TakeDamage → Die → equipment drops
    └── PlayerHealth (Player)      → TakeDamage → Die → equipment drops

IInteractable (Player)
    ├── LootContainer (Loot)       → Roll & add to inventory
    └── PickupItem (Loot)          → Add to inventory, destroy self

GameEvents (Core namespace, ScriptableObject assets)
    ├── GameManager.Raise() → MemorialWall.OnPlayerDied
    ├── GameManager.Raise() → DeathScreen listener (via reflection/Find)
    └── GameManager.Raise() → HUDManager (via polling, not events)
```

---

## 2. Implementation Status

### Legend
| Icon | Meaning |
|------|---------|
| DONE | Complete, production-ready |
| PROT | Prototype quality (works but needs hardening) |
| PART | Partial implementation (stub/todo present) |
| MISS | Not implemented at all |

### 2.1 Core Systems

| System | Status | Notes |
|--------|--------|-------|
| GameManager state machine | DONE | 5 states, floor tracking, death handling. Solid singleton pattern. |
| ScriptableObject event bus | DONE | GameEvent, GameEvent<T>, typed events. Good decoupling. |
| IDamageable interface | DONE | Clean contract. PlayerCombat and EnemyBase both implement. |
| DeathContext / CharacterMemorial | DONE | Data classes with factory constructor. |
| SceneBootstrap auto-setup | PROT | Heavy use of FindObjectOfType -- acceptable for bootstrap but not production. |
| QuickStart debug scene | PROT | Uses reflection to set private fields. Dev-only, acceptable. |
| Camera follow (clamped) | DONE | Well-implemented edge clamping. |
| Camera setup | DONE | Simple static config. |

### 2.2 Player Systems

| System | Status | Notes |
|--------|--------|-------|
| Movement (Rigidbody) | DONE | WASD, Rigidbody velocity, FreezeRotation. Solid. |
| Dodge (coroutine) | DONE | Cooldown, direction from move input or backward. Auto-aim penalty. |
| Aim -- auto-aim | DONE | OverlapSphere search, lock timer, nearest enemy. |
| Aim -- manual aim | DONE | Mouse-to-ground raycast. Functional. |
| Combat -- weapon slots | DONE | A/C/Melee, cycling, equip/swap. |
| Combat -- shooting | DONE | Delegates to WeaponBase.Fire(). |
| Combat -- melee input | DONE | RMB press/release, charge support. |
| Combat -- reload | DONE | R key, delegates to WeaponBase.Reload(). |
| Combat -- headshots | PART | headshotDistanceThreshold check. Headshot bonus defined but no actual aim-at-head mechanic. |
| Health system | DONE | TakeDamage, Heal, Die sequence. |
| Death -- equipment drops | DONE | DropEquipment() on dangerous floors. |
| Death -- dog tag spawn | DONE | Dog tag prefab instantiated. |
| Inventory -- backpack | DONE | 16 slots, stacking, AddItem with overflow. |
| Inventory -- weapon management | DONE | Equip, Swap, Cycle. |
| Inventory -- valuation | DONE | CalculateTotalValue() for death screen. |
| Interaction detection | DONE | OverlapSphere + IInteractable, E key. |
| Interaction prompt | DONE | GetPromptText() interface method. |

### 2.3 Weapon Systems

| System | Status | Notes |
|--------|--------|-------|
| WeaponBase abstract | DONE | Fire(), Reload(), CanFire(), Initialize(). Solid foundation. |
| RangedWeapon | DONE | Projectile spawn, spread, headshot multiplier, ammo consumption. |
| MeleeWeapon | DONE | OverlapSphere, arc filtering, charge attack. |
| Projectile | DONE | XZ flight, range limit, collision, Type-C special effects. |
| Spread mechanic | DONE | Manual aim halves spread. |
| Fire-rate gating | DONE | Time-based cooldown in WeaponBase. |
| Reload (ranged) | DONE | Magazine refill. |
| Melee charge | DONE | ChargeUpTime, ReleaseCharge, CancelCharge. |
| Type-C special effects | PART | Method stub in Projectile -- only logs, no actual effect application. |
| Weapon sounds | MISS | TODO comments in MeleeWeapon and RangedWeapon. |
| Weapon modification | MISS | GDD specifies 1 slot MVP, 3 slots reserved. No implementation. |
| Ammo types | DONE | Enums defined (Staple, Keycap, PPT, Coffee, Mug). |

### 2.4 Enemy Systems

| System | Status | Notes |
|--------|--------|-------|
| EnemyBase state machine | DONE | Idle/Patrol/Chase/Attack/Dead. Well-structured abstract base. |
| Patrol behavior | PROT | Abstract -- only KPIZombie implements. |
| Chase behavior | DONE | XZ movement, target tracking. |
| Attack behavior | DONE | Range check, cooldown, FaceTarget. |
| Status effects (Blind/Root/Taunt) | DONE | Timer-based, stacking by longer duration. |
| Loot drops | DONE | Guaranteed + random drops, scatter. |
| Player detection | PROT | Only via OnTriggerEnter (proximity). No vision cone. |
| KPIZombie implementation | DONE | Waypoint patrol, OverlapSphere detection, melee attack. |
| Pathfinding | MISS | No NavMesh or A*. Uses direct MoveToward. |
| Other enemy types | MISS | Only KPI Zombie exists. No PPT怨灵, 邮件幽灵, 会议恶魔, 保安. |
| Boss enemies | MISS | None implemented. |
| EnemySpawner | DONE | Random count, random prefab, random zone. |
| Vision cone / detection angle | PROT | DetectionAngle defined in EnemyData but never used in detection logic. |

### 2.5 Level / Floor Systems

| System | Status | Notes |
|--------|--------|-------|
| FloorGenerator | DONE | Grid-based, seeded, room placement. |
| Room type weighting | DONE | 60% Office, 30% Hallway, 10% Conference. |
| Connection flags | DONE | North/South/East/West adjacency. |
| RoomModule component | DONE | RoomType, gridPosition, spawn zones. |
| FloorManager | DONE | InitializeFloor, OnEnemyKilled, Extract. |
| FloorState persistence | PROT | Static dictionary only. Save() is a no-op stub. |
| Floor clearance tracking | DONE | isCleared, MarkCleared, enemy count reconciliation. |
| Loot decay multiplier | DONE | 0.75^visits formula. |
| Loot refresh timing | DONE | 4-hour real-time check. |
| 24h visit tracking | DONE | consecutiveVisits24h with reset. |
| Special floor logic | MISS | No special floor behavior (power, boss triggers, narrative events). |
| 10 hand-crafted floors | MISS | None created. Only procedural generation. |
| 40 random floor templates | MISS | Only the basic generator exists. No template data. |

### 2.6 Loot Systems

| System | Status | Notes |
|--------|--------|-------|
| LootTable (SO) | DONE | Weighted selection, roll count range, quantity range. |
| LootContainer | DONE | IInteractable, Roll + inventory add, overflow → PickupItem. |
| PickupItem | DONE | IInteractable, adds to inventory, self-destructs. |
| Container looted tracking | DONE | Instance ID + FloorState HashSet. |
| Container visuals | DONE | Open/closed toggle. |
| Inventory overflow handling | DONE | SpawnPickupItem on overflow. |

### 2.7 UI Systems

| System | Status | Notes |
|--------|--------|-------|
| HUDManager | DONE | Singleton, health/ammo/weapons/floor/extraction/prompt. |
| Health bar | DONE | Slider + text. |
| Ammo display | DONE | Current/max + ammo type name. |
| Weapon slot icons | DONE | 3 slots + active highlight. |
| Floor info | DONE | Number + safety status (CHN text). |
| Extraction timer | DONE | Flashing red warning + countdown. |
| Interaction prompt | DONE | Dynamic text, shown/hidden. |
| Damage vignette | DONE | Fades in below 50% HP. |
| DeathScreen | DONE | Shows DeathContext, "New Character" button. |
| MemorialWall | PROT | Works with events. LoadMemorials() is a stub. |
| Base building UI | MISS | No menus for crafting/upgrades. |
| Equipment screen | MISS | No inventory UI (only backpack data exists). |
| Map / minimap | MISS | Not implemented. |
| Main menu | MISS | Not implemented. |

### 2.8 Resources / Economy

| System | Status | Notes |
|--------|--------|-------|
| Paperclips (currency) | DONE | ItemData asset created. |
| Printer paper (ammo) | PART | ItemData created. Not connected to ammo system. |
| Coffee beans (perishable) | PART | FreshnessDurationMinutes=30. No spoilage system. |
| USB drives (blueprint) | PART | ItemData created. No blueprint system. |
| Work badges (工牌) | PART | Drop logic exists. No badge inventory/display. |
| Ammo type system | DONE | Enums defined in ItemData/AmmoType. |

### 2.9 Systems Not Yet Started

Based on the GDD, the following major systems have zero code:

- **Base/Tea Room building system** (upgrade stations, crafting)
- **Elevator system** (floor-to-floor transit, power gating)
- **Stairwell system** (animated descent, encounter chance)
- **Fire escape system** (diagonal extraction)
- **Power system** (floor power state, flashlight)
- **Extraction signal system** (保安巡逻广播, 加班铃)
- **Narrative progression** (story events per floor, key item tracking)
- **Special floor mechanics** (boss fights, puzzles)
- **NPC survivors** (trading, quests)
- **Save/load system** (only in-memory state)
- **Blueprint/upgrade system** (weapon modification)
- **Insurance/safe system** (per-floor safes)
- **New game+ / campaign persistence**

---

## 3. Code Quality Notes

### 3.1 Strengths

1. **Consistent namespace structure**: All code under `EscapeFromWork.{System}` -- clean and navigable.
2. **Good use of ScriptableObjects**: GameEvents, EnemyData, ItemData, WeaponData, LootTable all data-driven.
3. **Interface-based design**: IDamageable and IInteractable enable polymorphic behavior without coupling.
4. **Event bus pattern**: ScriptableObject-based GameEvent system decouples GameManager from listeners.
5. **Comprehensive XML doc comments**: Every public class, method, and field documented. Rare and commendable.
6. **Defensive coding**: Null checks, state guards, edge case handling in most methods.
7. **Editor tooling**: SceneWirer and WeaponWirer show investment in dev workflow.
8. **Gizmo debugging**: Almost every MonoBehaviour has OnDrawGizmosSelected -- excellent for level design.

### 3.2 Issues Found

#### Architectural Issues

| Issue | Location | Severity | Details |
|-------|----------|----------|---------|
| PlayerCombat and PlayerHealth **both** implement IDamageable | Player/ | MEDIUM | Both handle death, both drop equipment. Ambiguous which is authoritative. PlayerHealth has more complete death logic (references PlayerCombat for DropEquipment). PlayerCombat's death logic may conflict or double-fire. |
| playerInventory typed as MonoBehaviour | PlayerCombat line 42 | LOW | `[SerializeField] private MonoBehaviour playerInventory` with TODO to replace with `PlayerInventory`. Works but bypasses type safety. |
| No assembly definitions | Project root | MEDIUM | Every .cs file compiles together. No namespace boundary enforcement, slower recompilation. |
| HUDManager uses polling in Update() | UI/HUDManager.cs | LOW | Explicitly acknowledged as MVP. Acceptable for prototype. Should use events for production. |
| FloorManager.GatherSpawnZones is non-functional | Level/FloorManager.cs:193-216 | MEDIUM | Iterates rooms but never assigns the zones to EnemySpawner. EnemySpawner uses its own inspector-assigned zones. Dead code. |
| No pathfinding | Enemies/ | HIGH | EnemyBase.MoveToward moves directly toward target. Enemies will clip through walls. Critical for any real gameplay. |
| Detection angle not used | Enemies/EnemyBase.cs | MEDIUM | EnemyData.DetectionAngle is defined and serialized (120 deg for KPIZombie) but never used in detection logic. Only OnTriggerEnter and OverlapSphere are used. |

#### Coding Standard Violations

| Issue | Location | Severity | Details |
|-------|----------|----------|---------|
| `FindObjectOfType<PlayerController>()` in production code | SceneBootstrap.cs:45 | MEDIUM | Violates the "never use FindObjectOfType in production" rule. Bootstrap context makes it borderline acceptable. |
| `GameObject.FindGameObjectWithTag("Player")` in hot path | HUDManager.cs:335 | MEDIUM | Called every frame when player reference is null. Should use event or subscription. |
| `Input.GetMouseButton(0)` etc. used directly | PlayerAim.cs, PlayerCombat.cs | MEDIUM | Uses legacy input system. GDD specifies new Input System package. Migrate when more input complexity arrives. |
| `PlayerCombat` and `PlayerHealth` both handle death | Player/ | MEDIUM | Duplicated death logic violates DRY. PlayerHealth is newer and more correct. PlayerCombat.Die() should probably be removed. |
| No `SerializeField` naming convention | Various | LOW | Some serialized fields use `data` (lowercase), some use `_currentHealth` (underscore prefix). No consistent convention enforced. |

#### Potential Runtime Bug

| Issue | Location | Severity | Details |
|-------|----------|----------|---------|
| `Projectile.ApplySpecialEffect` does nothing | Weapons/Projectile.cs:127-148 | MEDIUM | Blind/Root/Taunt effects are only logged. The status effect APIs exist on EnemyBase (ApplyBlind, ApplyRoot, ApplyTaunt) but Projectile never calls them. |
| Floor clearance double-fire safety | FloorManager.cs:140 | LOW | `_enemiesRemaining <= 0` triggers a recount, then another <= 0 check. If the recount also returns 0, MarkCleared fires. Edge case: stalled spawner could keep adding enemies. |
| `playerAim` auto-aim uses `Physics.OverlapSphere` (non-alloc variant available but not used) | PlayerAim.cs:119 | LOW | Uses allocating `OverlapSphere` every frame. Should use `OverlapSphereNonAlloc`. |
| `FloorState.Save()` is a no-op | Level/FloorState.cs:203-209 | MEDIUM | All floor state is lost on quit. Save system is a placeholder. |

### 3.3 Anti-Patterns

1. **Two death handlers**: `PlayerCombat.Die()` and `PlayerHealth.Die()` both exist. According to comments, PlayerHealth was extracted from PlayerCombat for "separation of concerns" but the old death code was left in PlayerCombat. This creates a code fork that could diverge.

2. **Dead code in FloorManager**: `GatherSpawnZones()` iterates spawn zones but never assigns them to the EnemySpawner. The method exists, logs a count, and is called -- but has no effect.

3. **Singleton sprawl**: GameManager, FloorManager, and HUDManager are all singletons. For a prototype this is fine, but for production, dependency injection or service locator would reduce coupling.

4. **Reflection to set private fields in QuickStart**: `SetPrivate()` uses `BindingFlags.NonPublic | BindingFlags.Instance` to set `[SerializeField] private` fields. This is fragile and defeats the purpose of serialization. Acceptable for dev-only tooling.

5. **No `.asmdef` files**: Every script compiles into a single assembly. As the project grows, this will cause increasingly long recompile times and no boundary enforcement.

---

## 4. Dependency Graph

```
GameManager [Core]
    ├── IntEvent [Core]
    ├── GameEvent [Core]
    ├── DeathContextEvent [Core]
    └── CharacterMemorial [Core]

SceneBootstrap [Core]
    ├── GameManager [Core]
    ├── FloorManager [Level]
    ├── PlayerController [Player]
    └── HUDManager / DeathScreen [UI]

PlayerController [Player]
    └── PlayerAim [Player]

PlayerAim [Player]
    ├── PlayerController [Player]
    └── (physics intersection with Enemy-tagged objects)

PlayerCombat [Player]
    ├── PlayerAim [Player]
    ├── PlayerInventory [Player] (as MonoBehaviour)
    ├── WeaponBase [Weapons]
    ├── MeleeWeapon [Weapons]
    ├── DeathContext [Core]
    ├── GameManager [Core]
    └── IDamageable [Core]

PlayerHealth [Player]
    ├── PlayerCombat [Player]
    ├── DeathContext [Core]
    ├── GameManager [Core]
    └── IDamageable [Core]

PlayerInventory [Player]
    ├── PlayerCombat [Player]
    ├── ItemData [Data]
    └── WeaponBase [Weapons]

PlayerInteraction [Player]
    └── IInteractable [Player] (defined in same file)

EnemyBase [Enemies]
    ├── IDamageable [Core]
    ├── EnemyData [Data]
    └── ItemData [Data]

KPIZombie [Enemies]
    ├── EnemyBase [Enemies]
    └── IDamageable [Core] (via base)

EnemySpawner [Enemies]
    └── (instantiates tagged Enemy prefabs)

FloorGenerator [Level]
    └── RoomModule [Level]

FloorManager [Level]
    ├── FloorGenerator [Level]
    ├── EnemySpawner [Enemies]
    ├── FloorState [Level]
    └── GameManager [Core]

FloorState [Level]
    └── (no script dependencies -- pure C#)

RoomModule [Level]
    └── (no script dependencies -- pure C#)

WeaponBase [Weapons]
    └── WeaponData [Data]

RangedWeapon [Weapons]
    ├── WeaponBase [Weapons]
    ├── Projectile [Weapons]
    └── WeaponData [Data]

MeleeWeapon [Weapons]
    ├── WeaponBase [Weapons]
    ├── IDamageable [Core]
    └── WeaponData [Data]

Projectile [Weapons]
    └── IDamageable [Core]

LootTable [Loot]
    └── ItemData [Data]

LootContainer [Loot]
    ├── LootTable [Loot]
    ├── IInteractable [Player]
    ├── PlayerInventory [Player]
    ├── PickupItem [Loot]
    ├── ItemData [Data]
    └── FloorManager [Level]

PickupItem [Loot]
    ├── IInteractable [Player]
    └── PlayerInventory [Player]

HUDManager [UI]
    ├── PlayerCombat [Player]
    ├── PlayerInventory [Player]
    ├── PlayerInteraction [Player]
    ├── WeaponBase [Weapons]
    └── FloorManager [Level]

DeathScreen [UI]
    ├── DeathContext [Core]
    └── GameManager [Core]

MemorialWall [UI]
    ├── DeathContextEvent [Core]
    └── CharacterMemorial [Core]
```

### Dependency Analysis

- **Tightest coupling**: FloorManager depends on FloorGenerator, EnemySpawner, FloorState, and GameManager (4 external types).
- **Most independent**: RoomModule, FloorState, LootTable -- these are pure data/state holders with no script dependencies.
- **Circular dependencies**: None detected. The graph is a DAG, which is healthy.
- **External coupling**: Events are the primary decoupling mechanism. GameManager raises events; MemorialWall subscribes. HUDManager bypasses events entirely via polling.

---

## 5. GDD-to-Code Traceability

### 5.1 GDD Sections with Code Coverage

| GDD Section | Coverage | Details |
|-------------|----------|---------|
| **4. Core Loop** | PARTIAL | Raid entry (StartRaid) and extraction (ExtractFromFloor) exist. Base building is GameState only, no implementation. |
| **5. Floor Structure** | PARTIAL | Procedural grid generation works (6x6 default). Special floor flags exist in FloorManager (commented-out template). 40 random floor templates not created. |
| **6. Randomization System** | PARTIAL | Floor layout seeded (DONE). Container positions (DONE via RoomModule). Loot content (DONE via LootTable). Enemy spawn positions (DONE). Tea room location (DONE). Stairwell location (DONE). |
| **7. Floor State** | PARTIAL | Safe/dangerous detection (DONE in PlayerHealth.IsSafeFloor). Full clearance tracking (DONE). Loot decay (DONE). 4-hour refresh (DONE via ShouldRefreshLoot). 24h decrement (DONE). Enemy respawn (MISS). NPC survivors (MISS). |
| **8. Combat System** | DONE | WASD (DONE). Aim (DONE). Shoot (DONE). Melee (DONE). Dodge (DONE). Interact (DONE). Weapon switch (DONE). |
| **8a. Dual Aim** | PARTIAL | Auto/manual mode switching (DONE). Auto lock delay (DONE). Headshot detection (PARTIAL -- distance-based, no actual "head" hitbox). You can't actually aim for limbs. |
| **8b. Cover** | MISS | No cover mechanics implemented. |
| **9. Weapon System** | DONE | WeaponData SO (DONE). RangedWeapon (DONE). MeleeWeapon (DONE). Projectile (DONE). A/C/Melee classification (DONE). |
| **9a. Specific Weapons** | PARTIAL | StaplerPistol (Type-A) exists as data. KeyboardMelee (Keyboard板砖) exists. All other GDD weapons (PPT发射器, 会议邀请函法杖, 键盘霰弹枪, etc.) are MISS. |
| **9b. Weapon Mods** | MISS | No modification system. |
| **10. Enemy System** | PARTIAL | KPI Zombie (DONE). All other enemies (PPT怨灵, 邮件幽灵, 会议恶魔, 保安, Bosses) are MISS. |
| **11. Base System** | MISS | Tea room base is not implemented. No facilities (改造台, 医疗角, 情报板, 咖啡机). |
| **12. Resource/Economy** | PARTIAL | ItemData types defined (DONE). Currency/ammo/consumable/key items (DONE). Coffee freshness (PARTIAL -- data exists, no runtime spoilage). Ammo safety net (MISS). |
| **13. Death System** | DONE | DeathContext (DONE). CharacterMemorial (DONE). Equipment drop on dangerous floor (DONE). Dog tag spawn (DONE). New character selection (DONE). Memorial wall (DONE). |
| **13a. Corpse Recovery** | PARTIAL | Dog tags drop. Corpse recovery as a mechanic (return to death floor) is MISS. |
| **13b. Insurance Safe** | MISS | Per-floor safe not implemented. |
| **14. Elevator/Stairs** | MISS | Elevator and stairwell mechanics not implemented. RoomModule has Stairwell type and isExtractionPoint flag, but no transit logic. |
| **15. Power System** | MISS | Scripted-trigger approach mentioned but not implemented. |
| **16. Extraction Signals** | MISS | 保安巡逻广播 and 加班铃 not implemented. |
| **17. Key Items** | PARTIAL | 工牌 (badge) drops exist. USB items exist. Badge inventory/display is MISS. |
| **18. Narrative** | MISS | No narrative systems, dialogue, or story events. |
| **19. Multiplayer** | MISS | Architecture does not reserve netcode hooks yet. |
| **20. Dev Principles** | N/A | Engine/tools compliance checks. |

### 5.2 GDD Requirements with Zero Code

The following significant GDD features have no corresponding implementation:

1. **Base building** (设施系统: 改造台, 医疗角, 情报板, 咖啡机)
2. **Elevator system** (floor transit, "ding" alarm mechanic)
3. **Stairwell traversal** (animated descent, random encounter)
4. **Fire escape** (diagonal extraction route)
5. **Power system** (outage, flashlight, server room puzzle)
6. **Extraction signals** (保安广播, 加班铃)
7. **Cover mechanics** (no implementation)
8. **Weapon modification** (no slot system beyond data definition)
9. **All special floor content** (10 hand-crafted floors: 50F CEO, 41F Boss, 35F vault, 27F IT, 21F Boss, etc.)
10. **All bosses** (CEO, 经理, 保安队长, PPT怨灵, etc.)
11. **Most weapons** (only 2 of ~13 GDD-listed weapons exist)
12. **Most enemies** (only 1 of ~8 GDD-listed enemies exist)
13. **NPC survivors / trading**
14. **Save/load system**
15. **Ammo safety net** (daily NPC gift, emergency cabinet)
16. **Coffee freshness spoilage** (runtime system)
17. **Insurance safe** (per-floor loot preservation)
18. **Corpse recovery gameplay** (return to death floor)
19. **Narrative/story events** (no quest, dialogue, or progression)
20. **Main menu** (GameState.MainMenu exists but no UI)
21. **Map / minimap**
22. **Inventory UI** (backpack data exists but no visual screen)

### 5.3 GDD Phasing vs Reality

The GDD specifies an MVP strategy: "全部系统保留设计，分层实现" (keep all systems in design, layer implementation). The current state is consistent with very early prototyping focused on:

**Green (working prototype)**: Core movement, aiming, shooting, melee, basic enemies, procedural floors, loot tables, inventory, interaction, death/memorial, HUD.

**Yellow (scaffolded)**: Floor state persistence (in-memory only), special effects (stubs), weapon data (missing prefabs/icons), enemy detection (no vision cone).

**Red (not started)**: All content-heavy systems (special floors, bosses, weapons, enemies), all meta-systems (base building, save/load, narrative, economy), all traversal (elevator, stairs, fire escape).

---

## 6. Summary Statistics

| Metric | Value |
|--------|-------|
| Total C# files | 36 |
| Total lines of code (estimated) | ~4,500 |
| Namespaces | 9 (Core, Data, Editor, Enemies, Level, Loot, Player, UI, Weapons) |
| ScriptableObjects | 8 (2 weapons, 1 enemy, 4 items, 1 loot table) |
| Prefabs | 8 (1 player, 5 rooms, 1 enemy, 1 projectile) |
| Scenes | 1 (SampleScene.scene) |
| Assembly definitions | 0 |
| Unit tests | 0 |
| TODO comments | 7 |
| Interfaces | IDamageable, IInteractable |
| Abstract classes | EnemyBase, WeaponBase, GameEvent<T> |
| Singletons | GameManager, FloorManager (per-raid), HUDManager |

### Overall Assessment

The project is in **very early prototype (Phase 1)**. The core gameplay loop scaffolding is complete: the player can move, aim (auto + manual), shoot a ranged weapon, swing a melee weapon, dodge, loot containers, collect items, die, and see a death screen. Procedural floor generation works with a seeded grid system.

The architecture is **well-structured for this phase**: clean namespaces, interface-driven design, data-driven configuration via ScriptableObjects, and an event bus that will scale. The code quality is **well above average for a prototype** -- comprehensive XML docs, defensive coding, and editor gizmo support throughout.

**Critical gaps** that must be addressed before the prototype becomes playable:
1. **Pathfinding** -- enemies walk through walls
2. **Detection angle ignored** -- all enemies see 360 degrees regardless of config
3. **Two death handlers** -- PlayerCombat.Die() and PlayerHealth.Die() will conflict
4. **Type-C special effects** -- Blind/Root/Taunt APIs exist on EnemyBase but Projectile doesn't call them
5. **Content** -- 1 enemy type, 2 weapon types, 0 bosses, 0 special floors

**Recommended immediate priorities**:
1. Resolve the dual-IDamageable/death-handler conflict (remove PlayerCombat death logic)
2. Implement actual vision cone detection using DetectionAngle
3. Add NavMesh or simple A* pathfinding
4. Wire Projectile special effects to EnemyBase.ApplyBlind/ApplyRoot/ApplyTaunt
5. Create assembly definitions to enforce namespace boundaries
