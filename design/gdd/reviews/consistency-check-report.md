# Consistency Check Report

**Date**: 2026-07-17
**Registry entries checked**: 0 (registry empty — first population)
**GDDs scanned**: 6 (combat-system, weapon-system, enemy-system, floor-generation, loot-economy, ui-hud)
**Reference docs**: game-concept.md, systems-index.md, session-state/active.md

---

## Registry Status: EMPTY

`design/registry/entities.yaml` exists but contains no registered entities, items, formulas, or constants. This is the first consistency check run on this project. The registry should be populated from the 6 MVP GDDs before architecture begins.

---

## 🔴 Conflicts Found (3 resolved, 1 pending)

### ✅ C1: Enemy Count — RESOLVED (2026-07-17)
| Source | Value |
|--------|-------|
| [enemy-system.md](../enemy-system.md) §Detailed Design | **8** common enemy types |
| [game-concept.md](../game-concept.md) §10 | **8** common enemy types (updated) |
| [systems-index.md](../systems-index.md) §5 | **8** common enemy types tracked (updated) |

**Resolution**: 采用 8 种敌人。game-concept.md §10 和 systems-index.md 已同步更新。

### ✅ C2: Beam Weapon Cover Multiplier — RESOLVED (2026-07-17)

**Resolution**: 光束武器（投影仪射线枪）穿透掩体——这是特性而非 Bug。Weapon GDD 已更新：光束公式明确标注"无视 coverMultiplier"，A 类武器表中投影仪射线枪特殊属性标注"穿透敌人+掩体"。

### ⏳ C3: Map Dimensions — PENDING
| Source | Width | Depth |
|--------|-------|-------|
| [floor-generation.md](../floor-generation.md) §Map Params | 100m | 80m |
| [game-concept.md](../game-concept.md) §5 | 100m | 80m |
| [session-state/active.md](../../production/session-state/active.md) | **60m** | **50m** |

**Status**: 地图尺寸尚未最终敲定。GDD 和 session plan 存在差异，待后续确定。

### ✅ C4: Melee Stamina Costs — RESOLVED (2026-07-17)

**Resolution**: 采用每武器独立体力消耗。Combat GDD 和 Weapon GDD 已同步更新。近战武器表新增"轻击体力"和"重击体力"字段，5 把武器各有独立值（范围 10-18 / 18-35）。

---

## ⚠️ Broken Dependencies (Hard deps on non-existent GDDs)

| Dependent GDD | Missing Dependency | Dependency Type | Impact |
|---------------|-------------------|----------------|--------|
| combat-system.md | Death & Inheritance | Hard | Death flow cannot be implemented |
| ui-hud.md | Death System | Hard | Death screen has no data contract |
| ui-hud.md | Quest System | Soft | Quest board UI has no data source |
| weapon-system.md | Base Building | Soft | Weapon rack/mod station have no spec |
| floor-generation.md | Base Building | Soft | Tea room anchor integration undefined |
| loot-economy.md | Base Building | Soft | Stash storage integration undefined |

**Action**: Create Post-MVP GDDs for Death & Inheritance, Base Building, and Quest System. Until then, these 6 dependency edges are untestable.

---

## ⚠️ Underspecified Cross-System Interfaces

### I1: Enemy Spawn Zone Data Format
- Enemy GDD: "敌人生成由 EnemySpawner 按 FloorManager 的配置执行"
- Floor GDD: "提供 spawn zones（RoomModule.enemySpawnZones）"
- **Neither defines**: The data structure of a "spawn zone" — is it a Transform[]? A ScriptableObject with enemy type weights? A bounding box with density?

### I2: Loot Container ↔ Furniture Binding
- Floor GDD: "为搜刮家具绑定 LootContainer 组件"
- Loot GDD: "容器创建位置" (from Floor Generation)
- **Neither defines**: Which LootTable maps to which FurnitureTemplate? Is the binding in FurnitureTemplate data or FloorGenerator logic?

### I3: Floating Damage Text Ownership
- Combat GDD: `FloatingDamageText.Spawn()` (Combat initiates)
- UI GDD: No mention of FloatingDamageText
- **Undefined**: Is FloatingDamageText a world-space UI (Combat-owned) or screen-space UI (UI-owned)?

### I4: Enemy Tag Requirement
- Combat GDD: "敌人 tag 必须为 'Enemy' 以支持自动瞄准锁定"
- Enemy GDD: No mention of tag requirement
- **Risk**: Enemy prefab creator may not set the tag, breaking auto-aim silently.

---

## ℹ️ Unverifiable References

These cross-references exist but can't be verified without data neither GDD fully specifies:

- Weapon GDD: Ammo types (Staple, Keycap, etc.) — Loot GDD has "Ammo" ItemType but doesn't enumerate ammo subtypes
- Combat GDD: "Death & Inheritance → CharacterMemorial" — no such class/spec exists yet
- UI GDD: "Quest System → 任务状态" — quest status enum not defined
- Enemy GDD: Boss drops reference "特殊工牌（永久进度）" — badge system has no GDD

---

## ✅ Consistent Values (no issues found)

| Value | GDD 1 | GDD 2 | Match |
|-------|-------|-------|-------|
| maxStamina = 100 | combat-system.md §6 | game-concept.md §8 | ✅ |
| dodgeCost = 25 | combat-system.md §6 | game-concept.md §8 | ✅ |
| staminaRecovery = 15/s | combat-system.md §6 | game-concept.md §8 | ✅ |
| dodgeDuration = 0.2s | combat-system.md §5 | combat-system.md Formulas §4 | ✅ |
| headshotMultiplier = 1.5 | combat-system.md §3 | combat-system.md Formulas §1 | ✅ |
| coverMultiplier = 0.6 | combat-system.md §8 | combat-system.md Formulas §1 | ✅ |
| playerLoadout = 1A+1C+1Melee | combat-system.md §7 | weapon-system.md §Core Rules | ✅ |
| corridorWidth = 1.8m | floor-generation.md §Map | game-concept.md §5 | ✅ |
| teaRoomFrequency = every 5 floors | floor-generation.md §Map | game-concept.md §5 | ✅ |
| autoAimRange = 35m | combat-system.md §1 | combat-system.md Tuning | ✅ |
| staplePistol damage = 15 | weapon-system.md §A类 | combat-system.md Formulas §1 example | ✅ |

---

## Verdict: 🟡 CONCERNS — 3/4 Conflicts Resolved, 1 Pending (map dimensions)

### Priority Resolution Order
1. **C1 (Enemy count)** — blocks enemy implementation scope
2. **C2 (Beam cover)** — blocks projector ray gun implementation
3. **C3 (Map dimensions)** — plan vs GDD mismatch
4. **Broken dependencies** — resolved by creating Post-MVP GDDs
5. **Underspecified interfaces** — resolved during architecture review

---

*Report by /consistency-check. Registry population to follow in architecture review.*
