# Design Gap Analysis: ESCAPE FROM WORK

**Date:** 2026-07-15
**Scope:** `design/gdd/game-concept.md` (21 sections) vs. actual codebase implementation
**Method:** File-by-file read of all 36 C# scripts, 7 ScriptableObject assets, 8 prefabs, 2 Editor scripts, and all design docs
**Status:** Phase 1 Core Prototype — COMPLETE (per `production/session-state/active.md`)

---

## 1. GDD Section-by-Section Audit

### Section 1: 概要 (Overview)

| Aspect | Status | Details |
|--------|--------|---------|
| Top-down 2.5D extraction shooter | ⚠️ Partial | Uses 3D perspective with orthographic camera (3D physics, Rigidbody, 3D colliders) rather than true 2.5D (2D physics). Functional equivalent for MVP. |
| Office theme | ✅ Done | Enemy names reference office tropes (KPI, PPT, etc.), items are office-themed (paperclips, printer paper, coffee beans). |
| Hardcore extraction mechanics | ⚠️ Partial | Death penalty system exists. Equipment drop on dangerous floors exists. But no meaningful extraction decision-making. |

### Section 2: 设计支柱 (Design Pillars)

| Pillar | Status | Analysis |
|--------|--------|----------|
| 1. Office as Arsenal | ⚠️ Partial | 2 of 10 designed weapons implemented (StaplerPistol, KeyboardMelee). The "improvised weapon" fantasy exists in concept but isn't felt in gameplay. |
| 2. Descent Journey (50->1) | ❌ Missing | Single floor prototype only. No multi-floor descent, no elevator, no stairwells between floors. The spine of the game is absent. |
| 3. Gambler Moment | ⚠️ Partial | Death penalty code exists (drop weapons on dangerous floors, dog tag spawn). But no meaningful "push your luck" decision point — extraction is not implemented as a choice. |
| 4. Living Building | ❌ Missing | No power system, no dynamic floor events, no building-state changes. Room generation is static per seed. |

**Anti-pillar violations:** None significant yet (the game is so early it can't violate its own identity).

### Section 3: 玩家体验 (Player Experience)

| Aspect | Status | Details |
|--------|--------|---------|
| "Serious extraction shooter + office humor resonance" | ⚠️ Partial | Mechanical foundation exists. Humor exists in naming (KPI zombie, StaplerPistol) but isn't felt in gameplay. Player doesn't feel tension because risk/reward decisions aren't presented. |
| Core fantasy: "Today you pick up the stapler and fight your way out" | ❌ Missing | No narrative framing. No office environment aesthetics (all primitives). No sense of "I've been exploited for 10 years." |

### Section 4: 核心玩法循环 (Core Loop)

| Loop Element | Status | Details |
|-------------|--------|---------|
| Tea room base -> select floor | ❌ Missing | No base scene, no floor selection. Player starts directly in a floor. |
| Floor raid (loot + fight + extract) | ⚠️ Partial | Moving, shooting, looting containers all work. Extraction exists in code (`FloorManager.Extract()`) but has no in-scene trigger. Player can't actually extract. |
| Bring resources back | ❌ Missing | No "return to base" flow. Loot stays in inventory but there's no base to spend it in. |
| Base construction | ❌ Missing | No base building system at all. |
| Mainline progress | ❌ Missing | No key items, no special floor unlocks, no narrative progress. |

**Verdict:** The loop is broken at "extract" and "build." Player can only kill enemies, loot containers, and die.

### Section 5: 楼层结构 (Floor Structure)

| Aspect | Status | Details |
|--------|--------|---------|
| 50 floors total | ❌ Missing | 1 floor implemented (single scene). |
| 10 hand-designed special floors | ❌ Missing | None exist. |
| 40 random normal floors | ❌ Missing | None exist. FloorGenerator produces a single 5x5 grid per scene. |
| 150x150 game units per floor | ⚠️ Partial | Tile math works out: 5x5 grid x 20-unit tiles = 100x100 (not 150). GDD says 6x6 x 25-unit = 150x150. QuickStart uses 5x5 x 20-unit = 100x100. |
| 8-12 functional spaces per floor | ⚠️ Partial | 5x5 grid = 25 rooms. Function space variety: Office, Hallway, TeaRoom, ConferenceRoom defined. ServerRoom exists in RoomType enum but isn't placed by FloorGenerator. |
| Tea room per floor (fixed position) | ✅ Done | Placed at (gridWidth/2, 0). |
| Stairwell/fire escape position fixed | ✅ Done | Bottom-left for entry, top-right for extraction. |

### Section 6: 随机化系统 (Randomization)

| Randomization | Status | Details |
|--------------|--------|---------|
| Floor map layout: module random assembly | ✅ Done | FloorGenerator uses weighted random (60% Office, 30% Hallway, 10% Conference). |
| Loot container positions: generated with map | ⚠️ Partial | RoomModule has `lootContainerSpawns` array but it's read-only (only in gizmos). No actual loot-container spawning logic in FloorGenerator or FloorManager. |
| Container contents: random per entry | ✅ Done | LootTable.Roll() with weighted random selection. |
| Enemy spawn points: random in zones | ✅ Done | EnemySpawner uses random spawn zones per entry. |
| Key items: fixed positions | ❌ Missing | No key items placed. |

### Section 7: 楼层状态 (Floor State)

| Feature | Status | Details |
|---------|--------|---------|
| Safe floor conditions (all enemies dead) | ✅ Done | FloorState.isCleared + FloorManager.OnEnemyKilled(). |
| Container loot refresh: 4 hours | ✅ Done | FloorState.ShouldRefreshLoot() checks 4h timer. |
| Loot decay on repeat visits | ✅ Done | FloorState.GetLootDecayMultiplier(): 0.75^(visits-1). |
| 24h reset | ✅ Done | FloorState.RecordEntry() resets after 24h. |
| NPC survivors appear | ❌ Missing | No NPC system. |
| Floor-unique functions on clear | ❌ Missing | Not implemented. |

### Section 8: 战斗系统 (Combat)

| Input/Feature | Status | Details |
|--------------|--------|---------|
| WASD movement | ✅ Done | PlayerController with Rigidbody-based movement. |
| Mouse aiming | ✅ Done | Screen-to-world raycast against Y=0 plane. |
| LMB Shoot | ✅ Done | RangedWeapon fires projectiles. |
| RMB Melee | ✅ Done | MeleeWeapon support (instant + charged). |
| Space dodge | ✅ Done | Dodge coroutine in PlayerController with cooldown. |
| E Interact | ✅ Done | PlayerInteraction with IInteractable interface. |
| Scroll weapon switch | ✅ Done | Mouse scroll triggers CycleWeapon(). |
| Number keys for items | ❌ Missing | No item use system. |
| Tab inventory | ❌ Missing | No inventory UI. |
| Auto-aim mode | ✅ Done | PlayerAim finds nearest enemy in radius. |
| Manual aim mode (hold RMB) | ⚠️ Broken | Conflicts with RMB melee. Both PlayerCombat and PlayerAim read RMB. Manual aim toggle is `Input.GetMouseButton(1)` but melee is `GetMouseButtonDown(1)` — they coexist but feel wrong. |
| Headshot bonus (+50%) | ✅ Done | Implemented in RangedWeapon.Fire() via HeadshotDamageMultiplier. |
| Leg shot (slow) | ❌ Missing | GDD specifies "leg shots slow enemies" — not implemented. |
| Dodge distance penalty in auto-aim (-25%) | ✅ Done | PlayerController applies `dodgeDistancePenalty` when auto-aim lock is established. |
| Cover system (desks, cabinets) | ❌ Missing | GDD says no auto-cover for MVP. Natural cover from level geometry doesn't exist because there's no geometry. |

**Known bug:** "Auto-aim finds enemies but bullets fly straight (direction fix needed)" — documented in session state.

### Section 9: 武器系统 (Weapons)

| Weapon | Status | Details |
|--------|--------|---------|
| **A-Class (Office Supplies)** | | |
| Stapler Pistol (semi-auto) | ✅ Done | SO_Weapon_StaplerPistol.asset + RangedWeapon. |
| Keyboard Shotgun (close spread) | ❌ Missing | Not implemented. |
| Projector Raygun (beam) | ❌ Missing | Not implemented. |
| Mug Thrower (parabolic AOE) | ❌ Missing | Not implemented. |
| **C-Class (Creative)** | | |
| PPT Launcher (blind) | ❌ Missing | Not implemented. |
| Meeting Invitation Wand (root) | ❌ Missing | Not implemented. |
| Email Bomb (delay + taunt) | ❌ Missing | Not implemented. |
| Caffeine Injector (self-buff) | ❌ Missing | Not implemented. |
| **Melee (no ammo)** | | |
| Shredder Saw (continuous) | ❌ Missing | Not implemented. |
| Network Cable Whip (mid-range) | ❌ Missing | Not implemented. |
| KPI Report Hammer (charge heavy) | ❌ Missing | Not implemented. |
| Keyboard Brick (fast light) | ✅ Done | SO_Weapon_KeyboardMelee.asset + MeleeWeapon. |
| Mug Flail (rotating AOE) | ❌ Missing | Not implemented. |
| **Loadout system** | ✅ Done | 3-slot loadout (A, C, Melee) implemented in PlayerCombat. |
| **Weapon modification** | ❌ Missing | 1-slot MVP not implemented. Architecture for 3 slots not reserved. |

**Implementation ratio: 2 out of 13 weapons (15%).**

| Weapon System Feature | Status | Details |
|----------------------|--------|---------|
| Ammo system | ⚠️ Partial | AmmoType enum defined (Staple, Keycap, PPT, Coffee, Mug). But no ammo-item-to-weapon linking. Reload currently refills to max without consuming inventory ammo. |
| Fire rate gating | ✅ Done | WeaponBase.CanFire() enforces cooldown = 1/fireRate. |
| Spread system | ✅ Done | Manual aim halves spread. RangedWeapon.ApplySpread(). |
| Projectile | ✅ Done | Projectile.cs with speed, range, lifetime. |
| Special effects (blind/root/taunt) | ⚠️ Stub | Projectile.cs has cases for each effect but all are `TODO: Integrate with status-effect system` — only Debug.Log calls. |

### Section 10: 敌人系统 (Enemies)

| Enemy | Status | Details |
|-------|--------|---------|
| KPI Zombie (slow melee, high HP) | ✅ Done | Full AI: patrol, chase, attack, die. Loot drops. |
| PPT Ghost (ranged, projectile, blind) | ❌ Missing | Not implemented. |
| Email Ghost (group, self-destruct) | ❌ Missing | Not implemented. |
| Meeting Demon (slow zone) | ❌ Missing | Not implemented. |
| Security Guard (high defense, flash) | ❌ Missing | Not implemented. |
| Elite Security Captain (shield + stun) | ❌ Missing | Not implemented. |
| PPT Ghost Boss (41F) | ❌ Missing | Not implemented. |
| Manager Boss (21F) | ❌ Missing | Not implemented. |
| Elite Security Boss (3F) | ❌ Missing | Not implemented. |
| CEO Boss (1F) | ❌ Missing | Not implemented. |

**Implementation ratio: 1 out of 10 enemy types (10%).**

| Enemy System Feature | Status | Details |
|---------------------|--------|---------|
| State machine (Idle/Patrol/Chase/Attack/Dead) | ✅ Done | EnemyBase.cs full implementation. |
| Status effects (blind/root/taunt) | ✅ Done | Full timer-based system in EnemyBase. |
| Patrol AI (waypoint wandering) | ✅ Done | KPIZombie with patrol radius. |
| Detection (proximity + trigger) | ✅ Done | Physics.OverlapSphere + OnTriggerEnter. |
| Loot drop (guaranteed + random) | ⚠️ Partial | Guaranteed drop works. Random drops work. **Currency drop (min/maxCurrencyDrop) is never spawned** — EnemyBase.Die() only spawns GuaranteedDrop and PossibleDrops. |
| Drop placeholder visuals | ⚠️ Primitive | Drops use `GameObject.CreatePrimitive(PrimitiveType.Cube)` — labeled "工牌_X" but not proper pickups. |
| Vision cone detection | ❌ Missing | EnemyData has `detectionAngle` field but EnemyBase never uses it for cone checking. Currently omnidirectional. |

### Section 11: 基地系统 (Base Building)

| Facility | Status | Details |
|----------|--------|---------|
| Workbench (weapon modding) | ❌ Missing | Not implemented. |
| Med Station (healing items) | ❌ Missing | Not implemented. |
| Intel Board (floor intel) | ❌ Missing | Not implemented. |
| Coffee Machine (buff crafting) | ❌ Missing | Not implemented. |
| Floor-unique functions (50F intel, 41F trader, etc.) | ❌ Missing | Not implemented. |
| Base relocation (40% cost) | ❌ Missing | Not implemented. |

**Entire section: 0% implemented.**

### Section 12: 资源与经济 (Resources & Economy)

| Resource | Status | Details |
|----------|--------|---------|
| Paperclip (currency) | ✅ Done | SO_Item_Paperclip.asset exists. BaseValue = 1. But there's nothing to spend it on. |
| Printer Paper (ammo crafting) | ✅ Done | SO_Item_PrinterPaper.asset exists. |
| Coffee Bean (buff, freshness timer) | ✅ Done | SO_Item_CoffeeBean.asset exists. FreshnessDurationMinutes = 30. But freshness system not implemented. |
| USB Drive (blueprint unlock) | ⚠️ Partial | SO_Item_USB.asset exists. But no server/blueprint system to consume it. |
| Badge (floor unlock flag) | ❌ Missing | Not implemented as a system. Enemy drops cubes labeled "工牌" but no badge data, no floor unlock mechanic. |

| Economy Feature | Status | Details |
|----------------|--------|---------|
| NPC daily ammo gift | ❌ Missing | Not implemented. |
| Emergency ammo cabinet (<10 total) | ❌ Missing | Not implemented. |
| Coffee freshness (30min/2h/4h decay) | ❌ Missing | ItemData has freshnessDurationMinutes but no system processes it. |
| Coffee inventory cap (freshness + stack) | ❌ Missing | Not implemented. |
| Floor loot decay (repeat visits) | ✅ Done | FloorState.GetLootDecayMultiplier() works. |
| Enemy currency drop | ❌ Missing | EnemyData has min/maxCurrencyDrop but Die() never calls it. |

### Section 13: 死亡系统 (Death)

| Feature | Status | Details |
|---------|--------|---------|
| Death = permanent character loss | ✅ Done | GameManager.PlayerDied() transitions to Dead state. |
| New character from survivors | ⚠️ Partial | GameManager.SelectNewCharacter() exists. No styled character select UI. |
| Previous body in death floor | ❌ Missing | No corpse object left in world. |
| Memorial wall | ✅ Done | MemorialWall.cs subscribes to death events, displays name/floor/cause. |
| Equipment drop (dangerous floors) | ✅ Done | PlayerCombat.DropEquipment() spawns weapon prefabs at death position. |
| Equipment preserved (safe floors) | ✅ Done | IsSafeFloor() check (every 5th floor). |
| Body recovery mechanic | ❌ Missing | No way to return to death floor and recover gear. |
| Security patrol may loot body | ❌ Missing | Not implemented. |
| NPC body retrieval (safe floor, tip fee) | ❌ Missing | Not implemented. |
| Insurance box (tea room) | ❌ Missing | Not implemented. |
| Base preservation on death | ⚠️ Partial | Code intent exists (GameManager preserves state). But no base to preserve. |
| Dog tag (工牌) spawn on death | ✅ Done | dogTagPrefab spawned at death position. |
| Memorial wall badge display details | ⚠️ Partial | Shows name/floor/cause. GDD specifies: name, death floor, cause, loot value. Loot value field exists in CharacterMemorial but not displayed in MemorialWall.AddMemorial(). |

### Section 14: 电梯与楼梯 (Elevators & Stairs)

| Feature | Status | Details |
|---------|--------|---------|
| Elevator (safe/powered floors only) | ❌ Missing | Not implemented. |
| Elevator "ding" sound attracts enemies | ❌ Missing | Not implemented. |
| Stairs (always available, 3s/floor) | ❌ Missing | Not implemented. |
| Stairs enemy encounter chance (15%) | ❌ Missing | Not implemented. |
| Fire escape (diagonal, extraction only) | ⚠️ Partial | RoomModule.isExtractionPoint exists. FloorManager.Extract(useFireEscape) exists. No in-scene trigger/volume. No animated door. |

**Entire section: roughly 5% implemented.**

### Section 15: 电力系统 (Power System)

| Feature | Status | Details |
|---------|--------|---------|
| MVP: scripted power triggers | ❌ Missing | Not implemented. |
| Flashlight cone vision during blackout | ❌ Missing | Not implemented. |
| IT floor (27F) server rack puzzles | ❌ Missing | Not implemented. |
| Power restore = elevator + light + noise | ❌ Missing | Not implemented. |

**Entire section: 0% implemented.**

### Section 16: 撤离信号系统 (Extraction Signals)

| Feature | Status | Details |
|---------|--------|---------|
| Security broadcast (3min warning + reinforcements) | ❌ Missing | Not implemented. |
| Overtime bell (loot threshold + speed boost + double drops) | ❌ Missing | Not implemented. |
| HUD extraction warning | ⚠️ Stub | HUDManager.ShowExtractionTimer() and HideExtractionTimer() exist. No gameplay logic calls them. |

**Entire section: functionally 0% (HUD UI stub only).**

### Section 17: 关键道具 (Key Items)

| Feature | Status | Details |
|---------|--------|---------|
| Badge (dog tag) system | ⚠️ Partial | Enemy death spawns cube labeled "工牌_X". But badge data (name, department, ID, years) not defined. No badge trading. No badge-as-progression system. |
| Badge types (normal/elite/boss/legacy) | ❌ Missing | Not implemented. |
| Badge selling for paperclips | ❌ Missing | No NPC trader. |
| Badge elite drops = floor unlock flags | ❌ Missing | Not implemented. |
| USB blueprint consumption | ❌ Missing | SO exists. No IT server room to use it. |
| USB upgrade/unlock system | ❌ Missing | Not implemented. |

### Section 18: 叙事 (Narrative)

| Feature | Status | Details |
|---------|--------|---------|
| Story premise (CEO "permanent closed-loop") | ❌ Missing | Not implemented in any form. |
| Enemy lore (manifestations of work obsession) | ❌ Missing | Not implemented. EnemyData has `backstory` field but nothing displays it. |
| Information discovery arc (10 story beats across floors) | ❌ Missing | No story text, no environmental narrative, no cutscenes. |
| Final confrontation with CEO | ❌ Missing | Not implemented. |
| Ending (walk through revolving door) | ❌ Missing | Not implemented. |

**Entire section: 0% implemented.**

### Section 19: 联机 (Multiplayer)

| Feature | Status | Details |
|---------|--------|---------|
| Single-player MVP first | ✅ Correct | Intentional deferral. Architecture does not reserve multiplayer interfaces. |

### Section 20: 开发原则 (Dev Principles)

| Principle | Status | Details |
|-----------|--------|---------|
| MVP layered design | ✅ Done | All systems have minimal implementation with hooks for future depth. |
| 10 special floors all built | ❌ Not yet | Phase 1. |
| 40 random floors with 2-3 templates first | ⚠️ Partial | 5 room templates exist as prefabs. No multi-floor generation. |
| Verify core loop first | ⚠️ Partial | Loop is broken (no extraction, no base). |

### Section 21: 参考游戏 (Reference Games)

Documented for design reference. No implementation required.

---

## 2. Implementation Summary Table

| System | Line Count (scripts) | GDD Coverage | Status |
|--------|---------------------|--------------|--------|
| Core (GameManager, Events, Camera, Bootstrap) | 8 files | ~5% | Core loop flow defined |
| Player (Movement, Combat, Inventory, Interaction, Health, Aim) | 6 files | ~30% | Movement/combat solid, inventory basic |
| Enemies (Base, KPIZombie, Spawner) | 3 files | ~10% | 1/10 enemy types |
| Weapons (Base, Ranged, Melee, Projectile) | 4 files | ~15% | 2/13 weapons |
| Level (Generator, Manager, State, Room) | 4 files | ~20% | Single floor only |
| Loot (Container, Table, Pickup) | 3 files | ~40% | Loot system solid, containers not placed in scene |
| UI (HUD, Death, Memorial) | 3 files | ~25% | Code exists, no Canvas in scene |
| Data (Item, Weapon, Enemy) | 3 files | ~50% | Data layer well-structured |
| Editor tools | 2 files | — | Build + Wire tools for prototyping |
| **Total** | **36 files** | **~20% overall** | **Core extraction loop not fully playable** |

---

## 3. Priority Recommendations

### Tier 1: Core Loop Completeness (BLOCKING — extraction loop not functional)

1. **Add extraction trigger to scene** — `FloorManager.Extract()` exists but nothing calls it. Place a trigger volume on the fire-escape room that calls Extract(). This closes the "enter -> fight -> leave" loop. **(~1 hour)**

2. **Build HUD Canvas** — HUDManager.cs exists but no Canvas prefab is instantiated. Without HUD, player can't see health, ammo, floor info. SceneBootstrap has hudPrefab field but asset doesn't exist. **(~2 hours)**

3. **Fix aiming direction bug** — Known issue: "bullets fly straight (direction fix needed)." PlayerCombat.GetAimDirection() projects AimPoint correctly but the bug is in the PlayerAim-to-PlayerCombat pipeline. **(~1 hour)**

4. **Place loot containers in scene** — LootContainer and LootTable code exist but no containers are placed. This blocks the "loot" part of the extraction loop. **(~1 hour)**

### Tier 2: Player Experience (makes it feel like a game, not a tech demo)

5. **Add extraction signals** — Connect the extraction timer display in HUD to actual gameplay triggers. Even a simple 60-second countdown when entering the fire escape room creates tension. **(~2 hours)**

6. **Wire ammo consumption from inventory** — Currently, Reload() refills to max mag without consuming any item. Reload should consume PrinterPaper or appropriate ammo type. **(~2 hours)**

7. **Implement weapon spread visual** — Add a crosshair/reticle that widens/shrinks based on auto/manual aim mode. Critical for the dual-mode aim system to feel intentional. **(~2 hours)**

8. **Add one more enemy type** — PPT Ghost (ranged, projectile) would be the highest-impact addition. Uses existing Projectile + EnemyBase infrastructure. **(~3-4 hours)**

### Tier 3: Design Dependencies (what must come before what)

9. **Multi-floor system** — This gates all "descent journey" content. Requires:
    - Floor transition trigger (stairs/elevator)
    - FloorManager lifecycle across multiple scenes or in-scene floor data
    - CurrentFloor tracking in GameManager (already exists)
    - **Estimate: ~8-12 hours**

10. **Base building (menu UI)** — This gates economy. Without a base, paperclips have no sink, coffee has no brewing station, USBs have no server. Start with the 4-facility upgrade menu. **(~6-8 hours)**

11. **Economy hookup** — After base exists:
    - NPC trader at tea room
    - Paperclip sink (buy ammo, gear)
    - Ammo safety net system
    - Badge selling for paperclips
    - **(~4-6 hours)**

---

## 4. Design Debt

Items where the GDD has detailed specs but code doesn't implement them yet:

| GDD Section | Specification | Implementation Gap | Impact |
|-------------|--------------|-------------------|--------|
| 10 | Enemy currency drop | EnemyData has min/maxCurrencyDrop but code only drops GuaranteedDrop + PossibleDrops | Player never gets paperclips from kills |
| 12 | Coffee freshness decay | ItemData has freshnessDurationMinutes but no timer system | Coffee beans have no decay behavior |
| 13 | Cause of death | Hardcoded to "Combat" in both PlayerCombat and PlayerHealth | Death screen always shows "Combat" regardless of actual cause |
| 8 | Leg shot = slow effect | GDD specifies leg shots slow enemies. No damage location system. | Manual aim only grants headshot bonus, no utility |
| 9 | Weapon modifications | GDD specifies 1-slot MVP with 3-slot architecture reserve. Neither exists. | Weapons can't be customized |
| 10 | Vision cone | EnemyData.detectionAngle field exists but never checked | All enemies effectively have 360-degree omniscient detection |
| 11 | Base relocation formula | GDD specifies relocation cost = 40% of invested resources | No base to relocate |
| 14 | Elevator/stair decision triangle | GDD details 3 extraction options per floor | Only fire escape path exists (and even that isn't in-scene) |
| 17 | Badge data (name, department, ID, years) | Only a primitive cube placeholder | Badge flavor/progression system can't start |

---

## 5. Quick Wins (< 2 hours, high impact)

| Item | Estimate | Impact |
|------|----------|--------|
| 1. Place extraction trigger in scene | 30 min | Closes the core loop -- player can now extract |
| 2. Add Canvas prefab for HUD | 1.5 hours | Player can see health, ammo, floor info |
| 3. Fix aiming direction bug | 1 hour | Combat actually works as intended |
| 4. Place a loot container in scene | 30 min | Player can loot something |
| 5. Wire enemy currency drops | 1 hour | Player gets paperclips from kills |
| 6. Make loot drops use PickupItem instead of primitive cubes | 1 hour | Drops are proper interactable pickups |
| 7. Add a single extraction signal (overtime bell trigger) | 1.5 hours | Creates tension and HUD feedback |

---

## 6. Entity Registry Status

`design/registry/entities.yaml` is an empty template — all sections show `[]` with example entries commented out. No cross-system facts have been registered despite multiple systems (combat, loot, economy, enemies) existing in code. When the next GDD is written for a subsystem, the registry should be populated first.

---

## 7. Key File Inventory

**Exists and working:**
- 36 C# scripts across 8 namespaces
- 7 ScriptableObject assets (4 items, 2 weapons, 1 enemy, 1 loot table)
- 8 prefabs (player, 5 room types, zombie, projectile)
- 2 Editor tools (Build Scene, Wire Weapons)

**Exists in code but not instantiated/in-scene:**
- HUDManager (no Canvas prefab)
- LootContainer (no in-scene containers)
- FloorManager.Extract() (no trigger calls it)

**Planned but never created (from Phase 1 plan):**
- SaveManager.cs (save/load floor state)
- DataManager.cs (catalog loading)
- FloorTransition.cs (stairs/elevator logic)
- InventoryUI.cs (backpack grid UI)
- HUDCanvas.prefab
- FloorParent.prefab
- FloorTemplateData.cs (ScriptableObject)
- SaveData.cs (serializable state)
- SO_Floor_TestOffice.asset
- Main.unity (dedicated scene)
- PlayerInput.inputactions (Unity Input System)

**Architecture observations:**
- Uses raw `UnityEngine.Input` instead of Unity Input System (deviation from Phase 1 plan)
- 3D physics throughout (Rigidbody, Collider) rather than 2D physics
- ScriptableObject event system is well-structured (GameEvents.cs with typed variants)
- Code quality is solid: XML doc comments, dependency injection via SerializeField, namespaces consistent
- No unit tests exist despite coding standards requiring them

---

## 8. Summary

```
Overall GDD implementation coverage:  ~20%
Core extraction loop completeness:    BROKEN (missing extraction trigger + base)
Combat system:                        PLAYABLE (with known direction bug)
Enemy variety:                        1/10 types implemented
Weapon variety:                       2/13 weapons implemented
Floor progression:                    1/50 floors implemented
Base building:                        0% implemented
Economy:                              Paperclips and items exist but no sinks
Narrative:                            0% implemented
Death system:                         Death works, body recovery + badge system missing
```

The Phase 1 prototype successfully validates that the tech stack works and the combat feel is achievable. However, the core extraction loop is still broken at the extraction point, and the game's defining features (descent journey, base building, gamble moments) are entirely absent. The next phase should focus on closing the extraction loop and building the multi-floor infrastructure before adding content depth.
