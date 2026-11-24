# ADR-015: Base Building

**Status**: Proposed
**Date**: 2026-07-17
**Author**: Claude Code (/architecture-review)

---

## Summary

The base building system manages the tea room safe zone: 4 upgradeable facilities (workbench, medical corner, intel board, coffee machine), a stash (8×10 grid, upgradeable to 12×10), weapon rack for loadout selection, and floor-specific unlockable functions. Base state is the primary meta-progression that survives character death. Relocation between tea rooms costs 40% of invested resources.

---

## Context

The base is the "home" players return to between raids. All 4 facilities start at Lv1 and can be upgraded twice. The stash is shared across all characters (survivors). Floor-unique unlocks (e.g., IT server for blueprint upgrades) are permanent once earned.

### GDD Requirements Addressed

| Source | Requirement |
|--------|-------------|
| Base GDD | 4 facilities × 3 levels, relocation formula, stash, weapon rack |
| TR-UI-003 | Base UI: stash, weapon rack, bulletin board |

---

## Decision

### Facility Model

```csharp
public enum FacilityType { Workbench, MedicalCorner, IntelBoard, CoffeeMachine }

[System.Serializable]
public class FacilityState {
    public FacilityType type;
    public int level;            // 1-3
    public int investedResources; // total paperclips spent on this facility
}

// Upgrade formula:
// upgradeCost = baseCost * (1 + level * 0.5)
// baseCost per facility: Workbench=50, Medical=30, Intel=40, Coffee=20
```

### Stash Model

```csharp
// Stash uses the same grid system as backpack (ADR-006)
// Base: 8×10 = 80 slots
// Upgrade: +1 row = +10 slots, costs 200 paperclips per row
// Max: 12×10 = 120 slots
// Stack multiplier: stashMaxStack = item.maxStackSize × 10
```

### Relocation

```
relocationCost = totalInvested × 0.40
retainedValue  = totalInvested × 0.60

After 15F Legal Dept unlock: rate drops to 0.20 (80% retained)
```

### Floor Unlocks

| Floor | Unlock | Data Flag |
|-------|--------|-----------|
| 50F | Intel Terminal (view elevator/power status) | `unlock_intel_terminal` |
| 41F | Trade NPC (buy/sell items) | `unlock_trade_npc` |
| 35F | Vault Key (free safe box access) | `unlock_vault_key` |
| 27F | Server Room (USB blueprint upgrades) | `unlock_server_room` |
| 15F | Legal Review (relocation cost -50%) | `unlock_legal_review` |
| 3F | Surveillance (view enemy heatmap for any floor) | `unlock_surveillance` |

### Rules

1. **All base state survives death**: facilities, stash, unlocked floors, blueprints.
2. **Relocation requires target floor to be safe** (fully cleared).
3. **Facility upgrade is instant**: no build timer — confirms with cost dialog.
4. **Stash is shared across all characters**: no per-character stash.

---

## ADR Dependencies

- **Depends On**: ADR-002 (SO Data), ADR-006 (Inventory — stash grid), ADR-012 (Death — base inheritance), ADR-013 (Save/Load — base persistence)
- **Depended On By**: None
