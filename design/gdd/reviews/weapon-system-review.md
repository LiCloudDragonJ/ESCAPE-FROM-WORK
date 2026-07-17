# Design Review: Weapon System

**Date**: 2026-07-17
**Reviewer**: Claude Code (lean mode)
**Document**: [weapon-system.md](../weapon-system.md)
**Re-review**: No — first review

---

## Completeness: 8/8 sections present

| Section | Status | Notes |
|---------|--------|-------|
| Overview | ✅ | Clear scope: data model, classification, ammo, mods |
| Player Fantasy | ✅ | Anchored to "办公室即武器库" pillar |
| Detailed Rules | ✅ | 13 weapons across 3 classes, 8 ammo types, mod system, weapon rack UI |
| Formulas | ✅ | Damage, ammo consumption, reload, mod multiplier — 4 formula groups |
| Edge Cases | ⚠️ | Only 5 cases — thin for 13 weapons + 8 ammo types + mods |
| Dependencies | ✅ | 5 dependencies with direction, type, and interface |
| Tuning Knobs | ✅ | 14 knobs with defaults and safe ranges |
| Acceptance Criteria | ✅ | 10 ACs in GIVEN/WHEN/THEN format, all testable |

---

## Dependency Graph

| Dependency | Direction | Type | GDD Exists? |
|------------|----------|------|-------------|
| Core (IDamageable) | Upstream | Hard | ✅ (GameEvents.cs, IDamageable.cs implemented) |
| Data (WeaponData, ItemData) | Upstream | Hard | ✅ (WeaponData.cs, ItemData.cs implemented) |
| Combat System | Downstream | Hard | ✅ [combat-system.md](../combat-system.md) |
| Loot & Economy | Downstream | Soft | ✅ [loot-economy.md](../loot-economy.md) |
| Base Building | Downstream | Soft | ❌ No dedicated GDD — covered in game-concept §11 only |

---

## Required Before Implementation

1. **[BLOCKING] Beam weapon formula incomplete**: The Formulas section defines:
   ```
   Ranged:  finalDamage = baseDamage × headshotMultiplier × coverMultiplier
   Beam:    finalDamage = baseDamage × deltaTime × headshotMultiplier
   ```
   The beam formula omits `coverMultiplier`. Does the projector ray gun penetrate cover? If so, this should be stated as a feature. If not, the formula needs `× coverMultiplier`. A programmer implementing the ray gun has an ambiguous spec.

2. **[BLOCKING] Weapon acquisition path undefined**: The open question "武器获取途径" is critical for implementation. Without knowing where weapons come from (starting loadout? loot drops? NPC vendors? crafting?), the Weapon System's integration with Loot & Economy and Base Building can't be coded. **Action**: Resolve before implementing weapon spawning logic.

---

## Recommended Revisions

1. **Edge cases too thin**: 5 edge cases for a system spanning 13 weapons, 8 ammo types, mods, and weapon-rack UI. Missing cases:
   - What happens when a C-class weapon is used on a boss? (e.g., does PPT发射器 blind the CEO?)
   - What if the player tries to equip two weapons that share the same ammo pool?
   - Weapon switching during beam weapon firing — does the beam instantly stop?
   - C-class weapon with 0 ammo but effect still active from previous use?
   - Mod installation conflicts (e.g., two mods of same type)?

2. **Ammo economy balance concern**: Ammo stack limits in stash (2000 staples) are 10× backpack limits (200). An 80-slot stash could theoretically hold 160,000 staples. This dwarfs any reasonable consumption rate and makes ammo effectively infinite. Either stash limits need downward tuning or the multiplier formula needs a cap.

3. **Melee damage inconsistency with Combat System**: Weapon GDD shows `meleeHeavyDamage = 80` for KPI报表锤. Combat System edge case #14 says "蓄力满2秒自动释放". But Combat System Formulas §2 shows `meleeHeavyCost = 30`. Are these values per-weapon or global? The weapon table implies per-weapon values but Combat GDD suggests global stamina costs. **Cross-reference needed**: does each melee weapon have its own stamina cost or do all share the global 15/30?

4. **C-class weapon "cooldown" vs "duration" timing**: PPT发射器: 2s blind, 8s cooldown. Does cooldown start on cast or on effect end? If on cast, effective downtime is 6s. If on effect end, downtime is 8s. This matters for gameplay feel.

5. **Mod system architecture**: MVP has 1 slot (sights) but architecture reserves 3. The GDD doesn't specify whether the mod data structure should be an array (future-proof) or individual fields (MVP-simple). Array is recommended for Phase 1 to avoid data migration later.

---

## Nice-to-Have

- Weapon rarity/tier system: Open question #2 asks about quality tiers affecting base stats. Even if MVP doesn't use it, the `WeaponData` SO should reserve a `rarity` field to avoid schema migration.
- Projectile VFX colors are specified (Staple=银色, Keycap=彩色) but matching trail renderer materials aren't specified.
- The keyboard shotgun "5粒散射" — are these 5 independent projectiles each doing 8 damage, or 5 pellets doing 8 total? Table says `8×5粒` meaning 8 damage per pellet × 5 pellets = 40 potential total. Clarify in the damage formula.

---

## Scope Signal

**M** — Moderate complexity: 13 weapons × 3 classes, 8 ammo types, 4 formula groups, 5 dependencies. Substantial data entry work (13 WeaponData SOs + 8 AmmoData SOs) but mechanically straightforward. One ADR likely needed for mod slot architecture.

---

## Verdict: APPROVED (with advisory notes)

The Weapon System GDD provides a clear, data-driven weapon architecture. All 13 weapons have defined stats, ammo types have stack limits, and formulas cover the four damage patterns (ranged, melee, beam, AOE). The two blocking items (beam formula and acquisition path) need resolution before implementation, but neither requires redesign — just clarification.

---

*Review by /design-review (lean mode). Next: consistency-check with Combat System for stamina cost alignment.*
