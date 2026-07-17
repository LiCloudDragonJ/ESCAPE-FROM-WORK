# Design Review: Floor Generation

**Date**: 2026-07-17
**Reviewer**: Claude Code (lean mode)
**Document**: [floor-generation.md](../floor-generation.md)
**Re-review**: No — first review

---

## Completeness: 8/8 sections present

| Section | Status | Notes |
|---------|--------|-------|
| Overview | ✅ | Clear: corridor-driven + rule-constrained generation |
| Player Fantasy | ✅ | Exploration tension, "real office" feel, not a random maze |
| Detailed Rules | ✅ | Map params, 3 archetypes, 6-step generation pipeline, 11 room types, furniture placement, pillars, high-value zones |
| Formulas | ✅ | Seed generation, archetype selection, exit BFS, room sizing — 4 formula groups |
| Edge Cases | ✅ | 6 cases with explicit fallbacks |
| Dependencies | ✅ | 4 dependencies with direction, type, and interface |
| Tuning Knobs | ✅ | 10 knobs with defaults and safe ranges |
| Acceptance Criteria | ✅ | 7 ACs in GIVEN/WHEN/THEN format, all testable |

---

## Dependency Graph

| Dependency | Direction | Type | GDD Exists? |
|------------|----------|------|-------------|
| Enemy AI | Downstream | Hard | ✅ [enemy-system.md](../enemy-system.md) |
| Loot & Economy | Downstream | Hard | ✅ [loot-economy.md](../loot-economy.md) |
| FurniturePlacer | Downstream | Hard | ❌ No GDD — exists as code only (FurniturePlacer.cs) |
| Base Building | Downstream | Soft | ❌ No dedicated GDD — game-concept §11 only |

---

## Required Before Implementation

1. **[BLOCKING] Furniture system fragmented across three locations**: The floor generation GDD delegates all furniture logic to `FurniturePlacer` (§家具放置), referencing `RoomFurnitureSet` and `FurnitureTemplate`. These exist as C# code but have no design document specifying:
   - Which furniture types exist for each room type?
   - What are the 4 variants (普通/主管/CEO/破损) and their spawn rules?
   - How does furniture ↔ loot table binding work?
   A programmer implementing `FurniturePlacer.PlaceFurniture()` needs this spec. **Action**: Either create a dedicated Furniture GDD or add a comprehensive Furniture appendix to this document.

2. **[FORMULA] Archetype selection formula is fragile**: 
   ```
   archetypeIndex = (floorNumber × 173 + 97) % 40
   ```
   This formula hardcodes constants (173, 97, 40) with no named variables. If any constant changes (e.g., adjusting weights from 45/30/25 to 50/30/20), the entire formula breaks and all existing seeds produce different floors. **Action**: Define named constants and use a weighted random approach:
   ```
   float roll = DeterministicRandom(floorSeed, 0f, 1f)
   if (roll < ringStandardWeight) → RingStandard
   else if (roll < ringStandardWeight + openPlanWeight) → OpenPlan
   else → Cellular
   ```

---

## Recommended Revisions

1. **Missing "upstream" dependency on game-concept**: The GDD doesn't reference the 10 special floors defined in `game-concept.md` §5. Special floors (CEO办公室 50F, 市场部 41F, etc.) have custom layouts that override procedural generation. The GDD should explicitly state: "For special floors (defined in game-concept §5), the procedural generator is bypassed and a hand-authored layout is used."

2. **Room-to-furniture mapping not specified**: The GDD lists 11 room types but doesn't define which `RoomFurnitureSet` applies to each. A table mapping RoomType → FurnitureSet would prevent implementation guesswork.

3. **Stairwell anchor placement algorithm underspecified**: "8 个消防楼梯间（四边各 2 个）" — are these at fixed positions (corners? midpoints?) or procedurally placed? If procedural, what are the constraints (minimum distance from core筒, minimum spacing between anchors)?

4. **High-value zone placement is vague**: "每层有且仅有一个高价值区域" — but how is the room chosen? Is it always the room farthest from entry? A specific room type? Random among qualifying rooms?

5. **Corridor connectivity validation**: Edge case #5 says "每段走廊必须连接两个房间门以上" — this is a post-validation step but the GDD doesn't specify what corrective action is taken if validation fails (regenerate? patch corridor?).

6. **Performance note**: BFS on a 100m×80m grid at 1m resolution = 8000 nodes. Fast enough for a single search, but if regeneration retries up to 5 times, worst case is 5 BFS runs. Acceptable for PC, but worth flagging.

---

## Nice-to-Have

- The generation pipeline (§生成流程) is described in prose. A pseudocode listing would help programmers verify they haven't missed steps.
- "动态破坏" (destructible walls) is an open question. Even if MVP skips it, reserving a `isDestructible` flag on wall data prevents schema migration.
- The environmental audio per room type (server room fan noise, tea room water sounds) is a nice touch — coordinate with audio director on trigger zones.

---

## Scope Signal

**L** — Large: Procedural generation algorithm, 3 archetypes, 11 room types, furniture placement subsystem, BFS validation, seed determinism. Multiple subsystems touched (Enemy, Loot, Furniture, Base). Likely requires 2 ADRs: seed determinism strategy and furniture binding architecture.

---

## Verdict: APPROVED (with advisory notes)

The Floor Generation GDD is solid — the corridor-first approach is well-chosen for creating office-like spaces rather than random mazes. The three archetypes provide visual variety, and BFS exit validation ensures playability. The blocking item (furniture system fragmentation) needs resolution before `FurniturePlacer` implementation can begin. The fragile archetype formula should be refactored for maintainability.

---

*Review by /design-review (lean mode). Next: furniture system spec needed.*
