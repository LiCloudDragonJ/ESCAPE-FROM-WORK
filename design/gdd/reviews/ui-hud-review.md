# Design Review: UI / HUD

**Date**: 2026-07-17
**Reviewer**: Claude Code (lean mode)
**Document**: [ui-hud.md](../ui-hud.md)
**Re-review**: No — first review

---

## Completeness: 8/8 sections present

| Section | Status | Notes |
|---------|--------|-------|
| Overview | ✅ | Clear: all player-facing UI, UGUI-based, polling architecture |
| Player Fantasy | ✅ | "低调但信息密集" — understated but dense |
| Detailed Rules | ✅ | 5 screen groups: combat HUD, loot panel, base UI, death screen, memorial wall |
| Formulas | ✅ | HUD update frequency, grid cell placement — 2 formula groups (thin) |
| Edge Cases | ⚠️ | 5 cases — thin for 5+ screens with complex interactions |
| Dependencies | ✅ | 7 dependencies (highest of any GDD) |
| Tuning Knobs | ✅ | 5 knobs (UI sizing/layout — appropriate for UI system) |
| Acceptance Criteria | ✅ | 7 ACs in GIVEN/WHEN/THEN format |

---

## Dependency Graph

| Dependency | Direction | Type | GDD Exists? |
|------------|----------|------|-------------|
| Player Health | Upstream | Hard | ✅ (PlayerHealth.cs implemented) |
| PlayerCombat | Upstream | Hard | ✅ [combat-system.md](../combat-system.md) |
| Weapon System | Upstream | Hard | ✅ [weapon-system.md](../weapon-system.md) |
| Player Inventory | Upstream | Hard | ✅ (PlayerInventory.cs implemented) |
| Loot & Economy | Upstream | Hard | ✅ [loot-economy.md](../loot-economy.md) |
| Death System | Upstream | Hard | ❌ No dedicated GDD — game-concept §13 only |
| Quest System | Upstream | Soft | ❌ No dedicated GDD — game-concept §11 references NPC quests |

---

## Required Before Implementation

1. **[BLOCKING] Death System GDD missing**: UI/HUD lists Death System as a **Hard** upstream dependency. The death screen displays: character name, death floor, cause of death, lost equipment list, retained resources, memorial wall preview. None of these data fields are formally specified — they come from a Death System that doesn't have a GDD. **Action**: Create Death & Inheritance GDD before implementing DeathScreen.cs data binding.

2. **[BLOCKING] Quest System GDD missing**: 公告板 (Quest Board) UI depends on Quest System for "可用任务 / 任务详情 / 进行中任务" data. Without a Quest System GDD, the quest UI has no data contract. **Action**: Create Quest System GDD before implementing quest board UI.

---

## Recommended Revisions

1. **Polling architecture performance concern**: The HUD uses polling (`HUDManager` reads `PlayerCombat.CurrentStamina` each frame). For a single-player PC game, polling 7-8 values per frame is negligible. However, the GDD should specify whether polling runs in `Update()` (every frame) or at a fixed interval (e.g., `InvokeRepeating` at 0.1s). This matters when the inventory panel is open (disabled movement) — does polling continue?

2. **Edge cases need expansion**: 5 cases for 5 screen groups is insufficient. Missing:
   - Death screen triggered while loot panel is open?
   - Two interaction prompts competing (e.g., E to loot AND E to open door at same position)?
   - What if player resizes window / changes resolution? UI scaling strategy?
   - Memorial wall with 50+ dead characters — pagination vs scroll performance?
   - Crosshair visibility when aiming at friendly NPC vs enemy vs lootable?

3. **Resolution/scaling strategy not specified**: The GDD specifies pixel dimensions (480×30 health bar, 1450×820 loot panel). These are absolute values. On ultrawide monitors (21:9) or lower resolutions (1366×768 laptop), these sizes may clip or leave excessive margin. Specify canvas scaling mode (Scale With Screen Size) and reference resolution.

4. **Input locking granularity**: "面板打开时禁用玩家移动输入" — this disables all movement, but what about:
   - Can the player still look around (mouse) with the panel open?
   - Does opening the panel cancel current actions (reload, charging melee)?
   - Tab toggles inventory — can the player walk while inventory is open (many extraction shooters allow this)?

5. **No settings/options menu specified**: The Open Question #1 asks about ESC menu. Even for MVP, a minimal pause menu (Resume, Quit to Base) is needed. Without it, a player who opens a panel can't exit the game cleanly.

6. **Floating damage text ownership**: Combat GDD specifies `FloatingDamageText.Spawn()`. UI GDD doesn't mention it. Is FloatingDamageText owned by Combat (world-space) or UI (screen-space)? This affects implementation ownership.

---

## Nice-to-Have

- The progressive loading integration with loot container UI is well-designed (三栏面板 with equipment/backpack/container). Consider adding a "sort by" button (by name, rarity, type, value).
- 纪念墙 (Memorial Wall) is a strong emotional feature. Consider adding a "most successful run" highlight (most loot extracted, deepest floor reached) to give dead characters personality.
- Weapon rack UI (80×80 grid cells) could show a mini-stat block (damage, fire rate) on hover rather than requiring a separate inspect screen.

---

## Scope Signal

**M** — Moderate complexity: 5 screen groups, 7 dependencies (highest of any GDD), UGUI-based. Primary challenge is integration — the UI touches nearly every system. Most implementation is Canvas layout and data binding, not algorithmically complex. No new ADRs required if death/quest GDDs exist first.

---

## Verdict: APPROVED (with advisory notes)

The UI/HUD GDD provides clear layout specs and data bindings for all major screens. The polling architecture is appropriate for a single-player PC game. The two blocking items (Death System and Quest System GDDs missing) are dependency sequencing issues — the UI can't display data that hasn't been designed yet. These don't reflect flaws in the UI design itself.

---

*Review by /design-review (lean mode). Next: Death & Inheritance and Quest System GDDs needed before UI implementation.*
