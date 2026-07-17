# Architecture Review Report

**Date**: 2026-07-17
**Engine**: 团结引擎 1.9.3 (Tuanjie Engine — Unity 中国版, compatible with Unity 6000.x)
**GDDs Reviewed**: 6 (combat-system, weapon-system, enemy-system, floor-generation, loot-economy, ui-hud)
**ADRs Reviewed**: 0 (greenfield — architecture not yet established)
**Mode**: full (bootstrapping)

---

## Traceability Summary

| Status | Count | Description |
|--------|-------|-------------|
| ✅ Covered | 0 | No ADRs exist yet |
| ⚠️ Partial | 0 | — |
| ❌ Gaps | 42 | All requirements need ADRs |

**Coverage**: 0% — Architecture is completely unestablished. This is expected for a project transitioning from prototype to production.

---

## Technical Requirements Extracted (by System)

### Combat System (TR-COMBAT-001 through TR-COMBAT-008)

| TR-ID | Requirement | Domain | Priority |
|-------|-------------|--------|----------|
| TR-COMBAT-001 | Stamina resource system: 100 max, drain on dodge(25)/melee(15/30)/aim(8/s), regen 15/s after 0.5s delay | Core/Resources | P0 |
| TR-COMBAT-002 | Dual-mode aiming: auto-aim (35m, 120° cone) + manual aim (hold RMB, free-aim, headshot/legshot hit zones) | Input/Rendering | P0 |
| TR-COMBAT-003 | Damage pipeline: projectile → tag check → IDamageable.TakeDamage() → FloatingDamageText → death check | Core/Events | P0 |
| TR-COMBAT-004 | Cover system: proximity <1m to furniture → hitbox reduction 40%, distance-based (not raycast) | Physics/Collision | P0 |
| TR-COMBAT-005 | Dodge mechanic: Space key, 0.2s duration, 0.8s cooldown, 10 m/s speed, -25% distance during auto-lock | Input/Physics | P0 |
| TR-COMBAT-006 | Weapon slot system: 1A + 1C + 1Melee, scroll-wheel cycling, V-key quick melee (no swap) | Input/Weapons | P1 |
| TR-COMBAT-007 | Death flow: HP≤0 → death anim → DropEquipment() → spawn corpse + badge → GameManager → Dead state → return to base | Core/State | P0 |
| TR-COMBAT-008 | Reload interrupt: dodge during reload → cancel, retain partially loaded rounds | Weapons/State | P1 |

### Weapon System (TR-WEAPON-001 through TR-WEAPON-007)

| TR-ID | Requirement | Domain | Priority |
|-------|-------------|--------|----------|
| TR-WEAPON-001 | Data-driven weapons: WeaponData ScriptableObject with damage, fireRate, spread, magazineSize, reloadTime, range | Data/Assets | P0 |
| TR-WEAPON-002 | Four damage patterns: semi/scatter projectile, continuous beam (dmg×deltaTime), AOE (parabolic, 3m radius), melee (light/heavy) | Combat/Physics | P0 |
| TR-WEAPON-003 | Ammo system: 8 ammo types with per-type stack limits (backpack 20-200, stash ×10), reserve ammo inventory separate from magazine | Data/Inventory | P0 |
| TR-WEAPON-004 | Reload mechanic: weapon-specific reloadTime (1.5-3s), dodge-interruptible, round-by-round vs full-magazine reload | Weapons/State | P0 |
| TR-WEAPON-005 | C-class special effects: blind (2s), root (3s, 5m radius), delayed explosion+taunt (2s/5s), self-buff (8s, +30% atk spd, +20% move) | Combat/Effects | P1 |
| TR-WEAPON-006 | Mod system: MVP 1 slot (sights: -20% spread OR +5m auto-aim range), architecture reserves 3 slots (ammo conversion, special配件) | Data/Progression | P2 |
| TR-WEAPON-007 | Weapon acquisition: initial loadout + loot drops + NPC vendors + base weapon rack display | Loot/Economy | P1 |

### Enemy AI System (TR-ENEMY-001 through TR-ENEMY-007)

| TR-ID | Requirement | Domain | Priority |
|-------|-------------|--------|----------|
| TR-ENEMY-001 | Enemy FSM: Idle/Patrol → Chase → Attack → Dead, with detection params (vision 15m/120°, hearing 8m, chase 30m, attackCooldown 1.5s) | AI/State | P0 |
| TR-ENEMY-002 | Floor-scaling formula: HP×1.03^(50-floor), Damage×1.02^(50-floor), Speed×1.01^(50-floor) — up to ×2.47 HP at floor 1 | AI/Balance | P1 |
| TR-ENEMY-003 | Random variant system: 30% chance, 5 variant types (Elite/Swift/Tanky/Explosive/Regenerating) with weighted probability | AI/Data | P1 |
| TR-ENEMY-004 | Boss system: multi-phase fights (1-3 phases), phase-specific skills, phase transition VFX, HP thresholds for transitions | AI/Combat | P2 |
| TR-ENEMY-005 | Drop system: guaranteed badge + 1-3 paperclips per kill, probabilistic drops from EnemyData.possibleDrops, boss guaranteed special badge | Loot/Economy | P1 |
| TR-ENEMY-006 | EnemyData ScriptableObject: HP, damage, speed, detection params, attack pattern, drops, variantAffix — all data-driven | Data/Assets | P0 |
| TR-ENEMY-007 | Enemy tag requirement: all enemies must have "Enemy" tag for auto-aim lock-on to function | Core/Tags | P0 |

### Floor Generation (TR-FLOOR-001 through TR-FLOOR-007)

| TR-ID | Requirement | Domain | Priority |
|-------|-------------|--------|----------|
| TR-FLOOR-001 | Deterministic seed-based generation: floorSeed = runSeed + floorNumber, same seed = same layout every time | Core/Algorithm | P0 |
| TR-FLOOR-002 | Three layout archetypes: RingStandard(45%), OpenPlan(30%), Cellular(25%) — weighted random from seed hash | Algorithm | P0 |
| TR-FLOOR-003 | Corridor-first pipeline: core筒(16m×12m) → ring corridor(1.8m) → room allocation → pillar placement(12m spacing) → furniture → BFS validation | Algorithm | P0 |
| TR-FLOOR-004 | 8 stairwell anchors: 4 sides × 2 each, entry random, exit = BFS farthest from entry, one-way fire escape at diagonal | Algorithm/Nav | P0 |
| TR-FLOOR-005 | 11 room types with size constraints and placement rules; furniture placed per RoomFurnitureSet (mandatory min + optional chance) | Data/Level | P0 |
| TR-FLOOR-006 | Special floor override: 10 hand-designed floors (game-concept §5) bypass procedural generation entirely | Level/Data | P2 |
| TR-FLOOR-007 | High-value zone: exactly 1 per floor, type varies by floor segment (CEO office 40-50F, HR archives 30-39F, server room 20-29F, finance vault 10-19F, grand lobby 1-9F) | Level/Loot | P1 |

### Loot & Economy (TR-LOOT-001 through TR-LOOT-007)

| TR-ID | Requirement | Domain | Priority |
|-------|-------------|--------|----------|
| TR-LOOT-001 | LootTable ScriptableObject: weighted random with minRolls/maxRolls, LootEntry[] with ItemData + weight + count range | Data/Algorithm | P0 |
| TR-LOOT-002 | Progressive container loading: 6 rarity delays (0/1/2/4/8/12s), interruptible by closing container, resumable on reopen | UI/State | P1 |
| TR-LOOT-003 | Grid inventory: cellSize 60px, item occupies width×height cells, stacking (same ItemData, below maxStackSize), drag-drop, double-click transfer, F-take-all | UI/Data | P0 |
| TR-LOOT-004 | 10 item types × 6 rarities: Currency/Ammo/Consumable/Construction/Electronics/OfficeSupply/Luxury/Intel/KeyItem/Collectible | Data | P0 |
| TR-LOOT-005 | Container persistence: container state (loadedItems, pendingItems) must survive scene transitions for "leave and return" gameplay | State/Persistence | P1 |
| TR-LOOT-006 | Coffee freshness: real-time decay 30min(100%) → 2hr(70%) → 4hr(40%) → >4hr(0%), persists across sessions | Economy/Time | P1 |
| TR-LOOT-007 | Ammo safety net: total ammo <10 → emergency supply cabinet spawns on next floor entry | Economy/Balance | P2 |

### UI / HUD (TR-UI-001 through TR-UI-006)

| TR-ID | Requirement | Domain | Priority |
|-------|-------------|--------|----------|
| TR-UI-001 | Combat HUD: health bar, stamina bar, ammo display, floor info, crosshair (auto=translucent, manual=solid), interaction prompt, extraction warning | UI/Rendering | P0 |
| TR-UI-002 | Loot panel: 3-column (equipment 18% | backpack 41% | container 37%), drag-drop, double-click transfer, F-take-all (rarity-descending), item tooltip with rarity color | UI/Input | P0 |
| TR-UI-003 | Base UI panels: stash (3-column), weapon rack (grid + loadout slots), bulletin board (quest list + detail + active), workbench (mod UI, phase 2) | UI/Input | P1 |
| TR-UI-004 | Death screen: character name, death floor, cause, lost equipment list, retained resources, memorial wall preview, "select new character" button | UI/State | P1 |
| TR-UI-005 | Memorial wall: scrollable list of dead character badges showing name, death floor, cause, loot value brought back | UI/Data | P2 |
| TR-UI-006 | UGUI Canvas: Scale With Screen Size, reference resolution (1920×1080 recommended), panel-open disables player movement input | UI/Rendering | P0 |

---

## Coverage Gaps (No ADR Exists)

All 42 technical requirements are currently uncovered. The following ADRs are needed, prioritized by architectural layer:

### Foundation Layer (must exist first — everything depends on these)
1. **ADR-001: Event Bus Architecture** — Covers TR-COMBAT-003, TR-COMBAT-007, TR-UI-004. GameEvents.cs already exists; formalize as the cross-system communication backbone.
2. **ADR-002: ScriptableObject Data Architecture** — Covers TR-WEAPON-001, TR-ENEMY-006, TR-LOOT-001, TR-LOOT-004. WeaponData, EnemyData, ItemData, LootTable all use SO pattern; needs standardization.
3. **ADR-003: Scene Bootstrap & Lifecycle** — Covers TR-COMBAT-007, TR-FLOOR-001. SceneBootstrap.cs → GameManager → FloorManager ordering; entry/exit/restart flow.

### Core Layer (depend on Foundation)
4. **ADR-004: Damage Pipeline** — Covers TR-COMBAT-003, TR-WEAPON-002, TR-ENEMY-001. IDamageable interface, projectile collision, hit zones, damage formula, floating text.
5. **ADR-005: Stamina & Resource System** — Covers TR-COMBAT-001. Stamina drain/regen, empty-state penalties, UI binding.
6. **ADR-006: Inventory & Item System** — Covers TR-LOOT-003, TR-LOOT-004, TR-WEAPON-003. Grid inventory, stacking, equipment slots, ammo reserve, stash.
7. **ADR-007: Procedural Floor Generation** — Covers TR-FLOOR-001 through TR-FLOOR-005. Seed determinism, archetype selection, corridor-first algorithm, BFS validation.
8. **ADR-008: AI State Machine** — Covers TR-ENEMY-001, TR-ENEMY-002, TR-ENEMY-003. FSM architecture, detection system, floor scaling, variant system.

### Feature Layer (depend on Core)
9. **ADR-009: Weapon System & Mod Slots** — Covers TR-WEAPON-004 through TR-WEAPON-007. WeaponBase interface, fire patterns, reload, mod architecture.
10. **ADR-010: Loot Container & Persistence** — Covers TR-LOOT-002, TR-LOOT-005. Progressive loading, container state persistence, scene transition handling.
11. **ADR-011: UI Framework** — Covers TR-UI-001 through TR-UI-006. UGUI Canvas hierarchy, panel manager, input locking, data polling vs event-driven.
12. **ADR-012: Death & Inheritance** — Covers TR-COMBAT-007 downstream. Death flow, equipment drop, corpse/badge, memorial wall, character select. (Depends on Death GDD existing first.)

### Post-MVP Layer (future)
13. **ADR-013: Save/Load & Serialization** — Meta-progression persistence across sessions.
14. **ADR-014: Quest System** — Quest state, NPC dialogue, reward distribution.
15. **ADR-015: Base Building** — Facility upgrades, resource costs, tea room relocation.

---

## Cross-ADR Conflicts

**None** — No ADRs exist to conflict. This section will be populated in subsequent reviews.

---

## ADR Dependency Order (Topologically Sorted)

```
Foundation (no dependencies):
  1. ADR-001: Event Bus Architecture
  2. ADR-002: ScriptableObject Data Architecture
  3. ADR-003: Scene Bootstrap & Lifecycle

Depends on Foundation:
  4. ADR-004: Damage Pipeline (requires ADR-001, ADR-002)
  5. ADR-005: Stamina & Resource System (requires ADR-001)
  6. ADR-006: Inventory & Item System (requires ADR-001, ADR-002)
  7. ADR-007: Procedural Floor Generation (requires ADR-003)
  8. ADR-008: AI State Machine (requires ADR-001, ADR-002, ADR-004)

Feature layer:
  9. ADR-009: Weapon System & Mod Slots (requires ADR-002, ADR-004, ADR-006)
  10. ADR-010: Loot Container & Persistence (requires ADR-002, ADR-006)
  11. ADR-011: UI Framework (requires ADR-001, ADR-005, ADR-006)
  12. ADR-012: Death & Inheritance (requires ADR-001, ADR-003, ADR-006)

Post-MVP:
  13. ADR-013: Save/Load (requires ADR-002, ADR-006, ADR-007, ADR-012)
  14. ADR-014: Quest System (requires ADR-001, ADR-003, ADR-013)
  15. ADR-015: Base Building (requires ADR-006, ADR-013)
```

---

## Engine Compatibility

**Engine**: 团结引擎 1.9.3 (Tuanjie Engine — Unity 中国版, compatible with Unity 6000.x)
**Rendering**: URP
**Physics**: Unity Physics (3D for visuals, 2D for gameplay colliders)

### Engine-Specific Notes
- 团结引擎 is Unity 中国版 — compatible API surface with Unity 6000.x but may have China-specific features (WeChat mini-game, etc.)
- URP limits: single-pass rendering, no deferred rendering in default config
- Unity Physics: collider-based hit detection, no built-in hitbox system (head/body/legs need custom capsule region checks)
- ScriptableObjects are the standard data-driven pattern in Unity — well-supported

### Engine Risk Assessment
| Risk | Level | Mitigation |
|------|-------|------------|
| 团结引擎 API divergence from mainline Unity | LOW | Project already compiling; 47 scripts running |
| URP performance for 100m×80m floor with furniture | MEDIUM | Profile after FloorBuilder integration; consider LOD for distant rooms |
| No ECS/DOTS usage | LOW | 47 MonoBehaviour scripts — acceptable for PC single-player at current scope |
| Unity Physics limits for projectile count | LOW | <50 projectiles expected simultaneously at current weapon fire rates |

---

## GDD Revision Flags (Architecture → Design Feedback)

No engine-level revision flags — all GDD assumptions are consistent with Unity/URP capabilities.

However, two design-level flags from consistency check:

| GDD | Issue | Action |
|-----|-------|--------|
| weapon-system.md | Beam formula missing coverMultiplier | Resolve: feature (beam penetrates cover) or bug (add coverMultiplier) |
| enemy-system.md | 8 common enemies vs game-concept's 4 | Resolve scope: MVP=4 or MVP=8 |

---

## Blocking Issues

1. **🔴 No ADRs exist** — 0% architectural coverage. 42 requirements need ADR coverage before Production phase can be considered architecturally sound.
2. **🔴 No TR registry** — `docs/architecture/tr-registry.yaml` does not exist. Must be created before stories can reference TR-IDs.
3. **🔴 Death & Inheritance GDD missing** — 6 Hard dependencies point to a non-existent GDD. ADR-012 cannot be written without it.
4. **🔴 Quest System GDD missing** — UI/HUD depends on it; quest board has no data contract.
5. **🔴 Base Building GDD missing** — 4 Soft dependencies point to it.
6. **🟡 No tests exist** — Test framework not scaffolded. Required before Production gate.

---

## Verdict: **FAIL**

**Reason**: Architecture layer is completely unestablished — 0 ADRs, 0% coverage, no TR registry. This is **expected and normal** for a project transitioning from prototype to production, but it means the architecture gate cannot pass until Foundation-layer ADRs are written.

### Immediate Actions (Priority Order)
1. Create TR Registry (`docs/architecture/tr-registry.yaml`) — populates all 42 TR-IDs
2. Write **ADR-001: Event Bus Architecture** — foundational, GameEvents.cs already exists
3. Write **ADR-002: ScriptableObject Data Architecture** — all systems use SOs
4. Write **ADR-003: Scene Bootstrap & Lifecycle** — core loop flow
5. Create Death & Inheritance, Base Building, Quest System GDDs
6. Write remaining Core-layer ADRs (004-008)
7. Run `/test-setup` to scaffold test framework

---

*Report by /architecture-review. Next: populate TR registry, write foundation ADRs.*
