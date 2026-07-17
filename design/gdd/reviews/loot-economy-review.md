# Design Review: Loot & Economy

**Date**: 2026-07-17
**Reviewer**: Claude Code (lean mode)
**Document**: [loot-economy.md](../loot-economy.md)
**Re-review**: No — first review

---

## Completeness: 8/8 sections present

| Section | Status | Notes |
|---------|--------|-------|
| Overview | ✅ | Clear: items, containers, loot tables, economy loop |
| Player Fantasy | ✅ | "每次打开办公桌抽屉都应该是一次心跳时刻" |
| Detailed Rules | ✅ | 10 item types, 6 rarities, 6 container types, progressive loading, variant system, loot table structure, economy params |
| Formulas | ✅ | Roll logic, rarity value, backpack grid, stash stacking — 4 formula groups |
| Edge Cases | ⚠️ | 6 cases — covers key scenarios but thin for system complexity |
| Dependencies | ✅ | 6 dependencies with direction, type, and interface |
| Tuning Knobs | ✅ | 8 knobs with defaults and safe ranges |
| Acceptance Criteria | ✅ | 7 ACs in GIVEN/WHEN/THEN format, all testable |

---

## Dependency Graph

| Dependency | Direction | Type | GDD Exists? |
|------------|----------|------|-------------|
| Data (ItemData) | Upstream | Hard | ✅ (ItemData.cs implemented) |
| Player Inventory | Upstream | Hard | ✅ (PlayerInventory.cs implemented) |
| Floor Generation | Upstream | Hard | ✅ [floor-generation.md](../floor-generation.md) |
| UI / HUD | Downstream | Hard | ✅ [ui-hud.md](../ui-hud.md) |
| Base Building | Downstream | Soft | ❌ No dedicated GDD |
| Weapon System | Downstream | Soft | ✅ [weapon-system.md](../weapon-system.md) |

---

## Required Before Implementation

1. **[BLOCKING] Progressive loading state on scene transition undefined**: Edge case #1 says "`_loadingRoutine` 被 StopCoroutine 终止" when transferring scenes. But this doesn't specify:
   - What state does the container save? (which items were loaded vs pending?)
   - When the player returns, does loading restart from scratch or resume?
   - What if the container was partially looted before scene transition?
   This is critical for the "leave and come back" gameplay loop. **Action**: Specify container persistence model (save _pendingItems + _loadedItems state per container).

2. **[BLOCKING] Coffee freshness uses wall-clock time**: The freshness timer (30min → 2hr → 4hr) uses real time. Edge cases:
   - What if player quits the game with fresh coffee beans and returns 5 hours later? Are they instantly spoiled?
   - What about system clock manipulation? (PC single-player, but still a design concern)
   - Does the timer run during pause/menu?
   **Action**: Specify whether freshness uses real-time (persistent) or play-time (session) clock, and how quit/reload affects timers.

---

## Recommended Revisions

1. **LootTable weight system has no rarity guarantee**: The `WeightedRandom` approach means a container could theoretically roll 5 common items or 5 legendary items. For game feel, consider a "rarity slot" system: e.g., Desk containers guarantee at least 1 Uncommon+ item per open.

2. **Economy sink missing**: The economy lists many faucets (enemy drops, containers, safe floor refresh) but few sinks (ammo crafting, insurance fees). Without sufficient sinks, players accumulate infinite wealth. The game-concept mentions:
   - Insurance柜存取费 (undefined amount)
   - Base building costs (not specified)
   - NPC trading (buy/sell ratio undefined)
   These sink values must be defined before the economy can be balanced.

3. **Stash dimensions not defined here**: The GDD references `stashMaxStackMultiplier ×10` but doesn't define stash grid dimensions. Game-concept says 8×10=80 slots. This value should be in the Loot GDD (not just game-concept) since it defines the economy's storage capacity.

4. **Ammo safety net edge cases**: "总弹药 < 10 发" — is this per-ammo-type or total across all ammo? A player with 9 staples but 200 keycaps shouldn't trigger the safety net. Also, what if the player discards ammo to intentionally trigger the safety net for free ammo?

5. **Item grid rotation mechanic needs specification**: The loot container UI references rotating items (R key during drag), but the loot GDD doesn't mention grid rotation. The `ItemData` needs a `canRotate` flag — some items (long weapons) shouldn't rotate.

6. **Container type "CEODesk" missing dimensions**: All other containers have grid dimensions (4×3, 3×4, etc.) but CEODesk is listed as 5×4 without the "格" unit. Consistent formatting would help.

---

## Nice-to-Have

- The 6-rarity progressive loading (0/1/2/4/8/12s delays) is innovative. Consider adding a "reveal all" button (consumes a consumable or costs stamina) for players who don't want to wait.
- 保险柜存取费: The game-concept mentions this but GDD doesn't. Add as a tuning knob.
- The visit decay formula (每额外访问 -25% 品质) could use clarification: is the -25% applied multiplicatively (100% → 75% → 56% → 42%) or additively (100% → 75% → 50% → 25%)?

---

## Scope Signal

**L** — Large: 10 item types × 6 rarities, 6 container types, progressive loading system, loot table weighted-random algorithm, economy with freshness timers and safety nets, grid-based inventory. Touches every system that produces or consumes items. Likely requires 2 ADRs: container persistence model and economy sink/faucet balance.

---

## Verdict: APPROVED (with advisory notes)

The Loot & Economy GDD is the most complex of the MVP set — it defines the reward structure that drives the entire extraction loop. The progressive loading mechanic is a standout feature. The two blocking items (container persistence and coffee timer model) are critical for the "leave and return" gameplay fantasy and must be resolved before implementation.

---

*Review by /design-review (lean mode). Next: economy sink values needed for balance.*
