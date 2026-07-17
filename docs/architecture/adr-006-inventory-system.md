# ADR-006: Inventory & Item System

**Status**: Proposed
**Date**: 2026-07-17
**Author**: Claude Code (/architecture-review)

---

## Summary

Player inventory is a grid-based system where items occupy `width × height` cells. Items stack when they share the same `ItemData` and are below `maxStackSize`. Equipment slots (A/C/Melee/Armor/Backpack) are separate from the backpack grid. Ammo reserve is tracked per ammo type as a dictionary of counters, not as grid items. Stash storage uses the same grid system with a ×10 stack multiplier. This ADR defines the data model, stacking rules, equipment interface, and ammo reserve architecture.

---

## Context

The extraction loop depends on inventory: players fill their backpack during raids, manage limited space, and stash loot in the tea room. The existing `PlayerInventory.cs` implements grid inventory. `ItemData.cs` defines item properties. This ADR formalizes the model and resolves open questions about ammo storage and grid rotation.

### GDD Requirements Addressed

| TR-ID | Requirement |
|-------|-------------|
| TR-LOOT-003 | Grid inventory: cell-based, stacking, drag-drop, double-click, F-take-all, R-rotate |
| TR-WEAPON-003 | Ammo system: 8 types, backpack stack limits (20–200), stash ×10 |
| TR-WEAPON-007 | Weapon acquisition: loot drops, NPC vendor, base weapon rack |
| TR-LOOT-004 | 10 item types × 6 rarities |

---

## Decision

### Data Model

```csharp
// Backpack grid
public class PlayerInventory {
    public int gridWidth;           // from equipped Backpack item
    public int gridHeight;
    InventorySlot[,] grid;
    public Dictionary<AmmoType, int> ammoReserve; // separate from grid
    public EquipmentSlot slotA;     // A-class weapon
    public EquipmentSlot slotC;     // C-class weapon
    public EquipmentSlot slotMelee; // Melee weapon
    public EquipmentSlot slotArmor; // (future)
    public EquipmentSlot slotBackpack; // determines grid size
}

public struct InventorySlot {
    public ItemData item;   // null = empty
    public int stackCount;
    public int gridX;       // position of top-left cell
    public int gridY;
}

// Item occupies multiple cells
// gridX, gridY is top-left; item occupies gridWidth × gridHeight cells
// Neighboring cells are marked with a reference to the root slot
```

### Stacking Rules

| Rule | Behavior |
|------|----------|
| Same ItemData | ✅ Stackable up to `maxStackSize` |
| Different ItemData | ❌ Separate slots |
| At maxStackSize | New stack created in next available space |
| Partial pickup | If 5 spaces remain in stack and 10 picked up → stack fills to max, 5 go to new stack |
| Ammo in grid | Ammo items CAN exist in grid (loose ammo pickups) |
| Ammo in reserve | Separate dictionary: `ammoReserve[ammoType]` — not in grid |

### Ammo Reserve Architecture

```
Ammo reserve is a flat dictionary, not grid items. Why:
  - Ammo consumption is frequent (every shot) — grid ops are O(n)
  - 8 ammo types × frequent checks = grid search on every shot is wasteful
  - Reserve is invisible to player during combat (only HUD number matters)

Reserve flow:
  Pick up ammo item → auto-convert to reserve (unless grid has space AND player drags)
  Fire weapon → subtract 1 from reserve (if magazine empty, reload pulls from reserve)
  Open inventory → reserve shown as numbers per ammo type (not grid items)
  Transfer to stash → ammo reserve numbers transfer directly
```

### Equipment Slots

```
Slot A (A-class weapon):
  - Accepts: ItemType.Weapon with WeaponClass.A
  - On equip: instantiates weapon prefab, binds to PlayerCombat.SlotA
  - On unequip: destroys instance, returns ItemData to backpack

Slot C (C-class weapon):
  - Accepts: ItemType.Weapon with WeaponClass.C

Slot Melee:
  - Accepts: ItemType.Weapon with WeaponClass.Melee

Slot Armor (future):
  - Reserved field, not functional in MVP

Slot Backpack:
  - Accepts: ItemType.Equipment with equipmentSlot = Backpack
  - On equip: resizes grid (preserves items, fails if new grid is smaller than occupied cells)
```

### Grid Rotation

```
R key during drag → rotate item 90°:
  gridWidth ↔ gridHeight swap
  
Constraints:
  - Rotation only during drag (not in-place)
  - Rotated item must fit in target grid position
  - Some items marked canRotate = false (long weapons, large furniture)
  - Rotation stored in InventorySlot as isRotated flag
```

### Stash Integration

```
Stash grid: 8×10 = 80 slots (upgradeable to 12×10 = 120)
Stash uses same grid system as backpack, with stack multiplier:
  stashMaxStack = item.maxStackSize × 10
  
Stash is serialized per run — persists across character deaths
Stash access: Tea room only, via LootContainerUI (3-column: equipment|backpack|stash)
```

### Rules

1. **Ammo auto-converts to reserve**: Picking up ammo adds to `ammoReserve[type]` unless player explicitly places in grid.
2. **Grid is the source of truth**: Equipment slots contain ItemData references that also occupy grid space (weapons are "in backpack" when unequipped).
3. **Stack at pickup**: Auto-merge with existing stacks before creating new slots.
4. **No partial grid fills**: An item either fully fits or doesn't place. No "half in, half out."
5. **Backpack swap preserves items**: If new backpack is smaller, items in now-invalid cells drop as LooseLoot.

---

## Alternatives Considered

### A: Weight-based inventory (no grid)
- **Pros**: Simpler implementation, no tetris
- **Cons**: Less tactical, no spatial organization, less "extraction shooter" feel
- **Verdict**: Rejected. Grid-based inventory is a core extraction shooter convention and the GDD specifies it.

### B: Ammo as grid items only
- **Pros**: Unified model, ammo takes visual space
- **Cons**: Every shot requires grid search, 8 ammo types clutter the grid
- **Verdict**: Rejected. Ammo reserve as dictionary is more performant and cleaner UX.

---

## Consequences

### Positive
- Grid tetris creates meaningful backpack management decisions
- Ammo reserve architecture is performant (O(1) lookup vs O(n) grid search)
- Stash shares grid code — single implementation, two use cases
- Equipment slots as grid-item references makes unequip→backpack natural

### Negative
- Grid search for placement is O(width × height) — worst case 100+ cells for large backpacks
- Backpack swap edge cases (smaller grid, item displacement) are complex
- Ammo reserve is invisible in-grid — players need HUD to see ammo counts

---

## Engine Compatibility

| Aspect | Status |
|--------|--------|
| Grid data structure | Pure C# — 2D array, no Unity dependency |
| UI rendering | UGUI Canvas + GridLayoutGroup or custom grid |
| Serialization | JsonUtility or BinaryFormatter for save/load |

---

## ADR Dependencies

- **Depends On**: ADR-001 (Event Bus — OnItemPickedUp/OnItemDropped), ADR-002 (SO Data — ItemData)
- **Depended On By**: ADR-010 (Loot Containers — items move container→backpack), ADR-011 (UI — inventory panel), ADR-012 (Death — equipment drop)

---

## Implementation Notes

- `PlayerInventory.cs` exists: grid-based inventory with AddItem/RemoveItem
- `ItemData.cs` exists: itemType, rarity, maxStackSize, gridWidth, gridHeight
- Known gap: ammo reserve dictionary not implemented (gap #4 — P0)
- Known gap: no R-key rotation during drag
- Known gap: backpack swap resize not implemented
