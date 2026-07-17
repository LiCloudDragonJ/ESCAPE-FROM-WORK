# Design Review: Enemy AI System

**Date**: 2026-07-17
**Reviewer**: Claude Code (lean mode)
**Document**: [enemy-system.md](../enemy-system.md)
**Re-review**: No — first review

---

## Completeness: 8/8 sections present

| Section | Status | Notes |
|---------|--------|-------|
| Overview | ✅ | Clear classification: 4 common + 2 security + 4 bosses |
| Player Fantasy | ✅ | Thematic grounding — enemies are tragic, not evil |
| Detailed Rules | ✅ | 8 common enemies (expanded from 4!), variant system, state machine, detection params, drops |
| Formulas | ✅ | Floor scaling, drop probability, state machine transitions |
| Edge Cases | ⚠️ | 6 cases — thin for 10 enemy types + variant system + boss phases |
| Dependencies | ✅ | 6 dependencies with direction, type, and interface |
| Tuning Knobs | ✅ | 9 knobs with defaults and safe ranges |
| Acceptance Criteria | ✅ | 8 ACs in GIVEN/WHEN/THEN format, all testable |

---

## Dependency Graph

| Dependency | Direction | Type | GDD Exists? |
|------------|----------|------|-------------|
| Core (IDamageable) | Upstream | Hard | ✅ (implemented) |
| Data (EnemyData) | Upstream | Hard | ✅ (EnemyData.cs implemented) |
| Floor Generation | Upstream | Hard | ✅ [floor-generation.md](../floor-generation.md) |
| Combat System | Upstream | Hard | ✅ [combat-system.md](../combat-system.md) |
| Loot & Economy | Downstream | Soft | ✅ [loot-economy.md](../loot-economy.md) |
| UI / HUD | Downstream | Soft | ✅ [ui-hud.md](../ui-hud.md) |

All dependencies have existing GDDs or code. ✅

---

## Required Before Implementation

1. **[BLOCKING] Scope-creep: 8 common enemies vs game-concept's 4**: The Detailed Design table lists 8 common enemy types (KPI丧尸, PPT怨灵, 邮件幽灵, 会议恶魔, 打印机故障怪, 饮水机漏电丧尸, 午睡魔, 茶水间老鼠群), but `game-concept.md` §10 only lists 4 common types. The systems-index only tracks 4 common types. If all 8 are intended for MVP, the game-concept and systems-index need updating. If only 4 are MVP, the GDD should clearly separate MVP vs Post-MVP enemies.

2. **[BLOCKING] Variant × Floor Scaling interaction ambiguous**: The floor scaling formula multiplies base stats by up to ×2.47 at floor 1. The variant system applies separate multipliers (Elite: HP×1.5, Tanky: HP×2.0). The GDD doesn't specify whether these stack multiplicatively or additively:
   - Multiplicative: Tanky KPI丧尸 at floor 1 = 60 × 2.47 × 2.0 = 296 HP
   - Additive: Tanky KPI丧尸 at floor 1 = 60 × (1 + 1.47 + 1.0) = 208 HP
   This is a significant balance difference (42% gap) that must be resolved before implementation.

---

## Recommended Revisions

1. **Edge cases need expansion**: 6 cases for 10 enemy types is insufficient. Missing:
   - What happens when a variant-specific effect conflicts with a C-class weapon effect? (e.g., Tanky's "不可打断" vs 会议邀请法杖's "定身")
   - Boss phase transition interrupted by death? (Edge case #5 covers this — good)
   - Multiple enemies of different types sharing the same spawn zone?
   - Enemy falling off the map / getting stuck in geometry?
   - What if all spawn zones on a floor are inside the player's detection radius?
   - 茶水间老鼠群 (5 rats sharing one spawn) — if one rat dies, do the others flee or enrage?

2. **State machine too simple**: The 4-state FSM (Idle/Patrol → Chase → Attack → Dead) is minimal. Most extraction shooters benefit from an "Alert" state (investigating last known position, searching) between Patrol and Chase. Without it, enemies either know exactly where the player is or completely forget. **Suggestion**: Add "Alert" state with `lastKnownPosition` for more realistic behavior, even if MVP uses a simplified version.

3. **Detection parameters need per-type differentiation**: All enemies share the same `detectionRange = 15m, detectionAngle = 120°`. A 茶水间老鼠群 should have different detection than a 会议恶魔. The `EnemyData` SO should include per-type overrides for detection params.

4. **Boss skill specifications are narrative, not mechanical**: CEO's skills ("裁员通知-范围秒杀", "企业文化洗脑-控制反转") are flavorful but not mechanically specified:
   - "范围秒杀" — what radius? What wind-up time? Can player dodge?
   - "控制反转" — does the player shoot themselves? Walk toward enemies?
   - "加班轮回-减速" — what slow %? Duration? Stackable?
   Boss skills need the same mechanical rigor as player abilities.

5. **敌人刷新机制 is an open question**: The GDD's Open Question #1 asks about safe-floor enemy respawn timing. This directly impacts the gameplay loop — if safe floors re-populate, the concept of "safe" is undermined. Must be resolved.

---

## Nice-to-Have

- Per-enemy footstep audio is specified in V/A Requirements — good for immersion. Consider a shared `EnemyAudioProfile` SO to reduce per-prefab configuration.
- The detection formula could include a `noiseLevel` parameter (player gunfire = louder, walking = quieter) to create stealth gameplay depth.
- 茶水间老鼠群 as a 5-entity swarm may have performance implications. Consider a swarm-manager approach rather than 5 independent AI agents.

---

## Scope Signal

**L** — Large: 10 enemy types (or 4 MVP + 6 post-MVP), variant system with stacking multipliers, floor-scaling formula, boss phase transitions, state machine with per-type behavior differences. Likely requires 1 ADR for variant × scaling resolution and 1 ADR for boss skill architecture.

---

## Verdict: APPROVED (with advisory notes)

The Enemy AI GDD is well-structured and thematically rich. The variant system adds replayability, and floor-scaling ensures escalating difficulty. The two blocking items (enemy count scope and variant × scaling interaction) need producer/designer decisions before programmers can implement — but neither requires redesign.

---

*Review by /design-review (lean mode). Next: reconcile enemy count with game-concept and systems-index.*
