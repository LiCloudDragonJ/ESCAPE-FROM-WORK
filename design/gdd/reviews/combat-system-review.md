# Design Review: Combat System

**Date**: 2026-07-17
**Reviewer**: Claude Code (lean mode)
**Document**: [combat-system.md](../combat-system.md)
**Re-review**: No — first review

---

## Completeness: 8/8 sections present

| Section | Status | Notes |
|---------|--------|-------|
| Overview | ✅ | Clear scope definition, contract role between subsystems |
| Player Fantasy | ✅ | Anchored to two design pillars, specific feel targets |
| Detailed Rules | ✅ | 9 subsections covering aiming, shooting, melee, dodge, stamina, weapon slots, cover, damage flow, states |
| Formulas | ✅ | 5 formulas with variable tables, value ranges, worked examples |
| Edge Cases | ✅ | 16 cases — comprehensive, each with explicit resolution |
| Dependencies | ✅ | 6 dependencies with direction, type, and interface |
| Tuning Knobs | ✅ | 15 knobs with defaults, safe ranges, and impact descriptions |
| Acceptance Criteria | ✅ | 10 ACs in GIVEN/WHEN/THEN format, all testable |

**Bonus sections**: Visual/Audio Requirements, UI Requirements, Open Questions — all present.

---

## Dependency Graph

| Dependency | Direction | Type | GDD Exists? |
|------------|----------|------|-------------|
| Player Movement | Upstream | Hard | ✅ (game-concept §8, PlayerController.cs implemented) |
| Weapon System | Upstream | Hard | ✅ [weapon-system.md](../weapon-system.md) |
| Enemy AI | Upstream | Hard | ✅ [enemy-system.md](../enemy-system.md) |
| UI / HUD | Downstream | Hard | ✅ [ui-hud.md](../ui-hud.md) |
| Loot & Economy | Downstream | Soft | ✅ [loot-economy.md](../loot-economy.md) |
| Death & Inheritance | Downstream | **Hard** | ❌ No dedicated GDD — covered in game-concept §13 only |

---

## Required Before Implementation

1. **[BLOCKING] Death & Inheritance GDD missing**: Combat lists Death & Inheritance as a **Hard** downstream dependency (death event → CharacterMemorial, equipment drop). The Death system only has a section in `game-concept.md` (§13) — no dedicated GDD with formulas, edge cases, or ACs. A programmer implementing `PlayerHealth.Die()` → `DeathScreen` flow has no spec for what to call. **Action**: Create `design/gdd/death-inheritance.md` before implementing death flow.

2. **[Formula validation] Enemy attack interaction undefined**: The stamina system (Formulas §2) covers player costs for dodge/melee/aim, but never states whether enemy attacks affect player stamina (knockback drain? stagger cost?). The open question at line 327 asks this explicitly but doesn't resolve it. A programmer implementing `TakeDamage()` needs to know: does receiving damage drain stamina? **Action**: Resolve Open Question #3 before implementation.

---

## Recommended Revisions

1. **Dodge formula inconsistency**: §5 Dodge states `dodgeSpeed = 10 m/s, dodgeDuration = 0.2s` → 2m distance. But Formulas §4 shows `dodgeDistance = dodgeSpeed × dodgeDuration × aimPenalty` where `aimPenalty = 0.75` during auto-lock. This correctly yields 1.5m during auto-lock. However, the Detailed Design table for dodge (§5) says "自动瞄准锁定中闪避距离 -25%" but the formula uses ×0.75 — the penalty applies to ALL dodging during auto-lock, not just distance. The naming is misleading. **Suggestion**: Rename `aimPenalty` to `autoAimDodgePenalty` and clarify it's a distance multiplier, not a stamina cost modifier.

2. **Melee charge auto-release timing**: Edge case #14 says "蓄力满 2 秒不释放 → 自动释放重击". But what direction? What if the player moved the mouse during the hold? The auto-release should fire in the direction the player was aiming at moment of release.

3. **Cover detection granularity**: §8 uses distance-based check (<1m from furniture). As noted, this is MVP-appropriate. But the edge cases mention "判定用距离而非视线" — this means a player 1m from a desk but on the wrong side (exposed to enemy) still gets cover benefits. For Production phase, consider a directional check (is the furniture between player and attacker?).

4. **Multiple damage types**: The damage formula is `baseDamage × headshotMultiplier × coverMultiplier` — purely physical. If C-class weapons or boss abilities introduce non-physical damage (e.g., "corporate dread" damage), the formula has no hook for damage type resistance. Architecture should reserve a `damageType` field even if MVP only uses "Physical".

---

## Nice-to-Have

- State machine diagram for player combat states is text-only (§States and Transitions table). A visual diagram would help programmers implement transitions.
- Reload interrupt behavior (edge case #3) doesn't specify whether "保留已装填部分" means partial magazine or full loss with animation reset. Clarify for implementation.
- The "enemy tag must be 'Enemy'" requirement in Dependencies (Enemy AI) couples combat to Unity's tag system. Consider an `IDamageable` component check instead for flexibility.

---

## Scope Signal

**M** — Moderate complexity: 1-2 formulas (damage + stamina), 5 core mechanics (aim, shoot, melee, dodge, cover), 6 dependencies. Primarily a single-system implementation with well-defined interfaces to other systems. No new ADRs required unless death system interaction changes architecture.

---

## Verdict: APPROVED (with advisory notes)

The Combat System GDD is complete, internally consistent, and implementable. All 8 required sections are present and well-specified. The 16 edge cases are thorough and give programmers clear resolution paths. Formulas have worked examples. ACs are testable.

The single blocking issue is the Death & Inheritance dependency — the Death system GDD must exist before the death-flow code in combat can be completed. This is a dependency sequencing issue, not a flaw in the combat design itself.

---

*Review by /design-review (lean mode). Next: consistency-check across all GDDs.*
