# ADR-010: Loot Container & Persistence

**Status**: Proposed
**Date**: 2026-07-17
**Author**: Claude Code (/architecture-review)

---

## Summary

All lootable objects implement `ILootable` and carry a `LootContainer` component. Containers use a weighted-random `LootTable` ScriptableObject for drop generation, with progressive loading by rarity (0/1/2/4/8/12s delays). Container state (which items are loaded vs pending) persists within the current run via a container ID system — closing and reopening a partially-loaded container resumes from where it left off. Full persistence across scene transitions and save/load is reserved for Post-MVP.

---

## Context

Progressive loading is the core UX innovation of this game's loot system — rare items "reveal" after a delay, creating anticipation. The existing `LootContainer.cs` implements this partially but container state doesn't survive scene transitions (gap #1 in loot-economy review). This ADR defines the persistence model.

### GDD Requirements Addressed

| TR-ID | Requirement |
|-------|-------------|
| TR-LOOT-001 | LootTable SO: minRolls/maxRolls, weighted LootEntry[] |
| TR-LOOT-002 | Progressive loading: 6 rarity delays, interruptible, resumable |
| TR-LOOT-005 | Container persistence: state survives scene transitions |

---

## Decision

### Container State Model

```csharp
public class LootContainer : MonoBehaviour {
    [SerializeField] private LootTable lootTable;
    [SerializeField] private ContainerType containerType;
    [SerializeField] private string containerId; // unique per container instance

    private List<LootEntry> _pendingItems;  // not yet revealed
    private List<LootEntry> _loadedItems;   // visible/transferable
    private bool _allLoaded;
    private Coroutine _loadRoutine;

    // Container state for persistence (per-run, not per-session)
    public ContainerState GetState() => new ContainerState {
        containerId = this.containerId,
        pendingItemIds = _pendingItems.Select(i => i.item.dataId).ToArray(),
        pendingCounts = _pendingItems.Select(i => i.count).ToArray(),
        loadedItemIds = _loadedItems.Select(i => i.item.dataId).ToArray(),
        loadedCounts = _loadedItems.Select(i => i.count).ToArray(),
        allLoaded = _allLoaded
    };

    public void RestoreState(ContainerState state) {
        // Rebuild _pendingItems and _loadedItems from state.
        // Resume _loadRoutine from current position.
    }
}
```

### Persistence Scope

| Persistence Level | What Survives | MVP |
|------------------|---------------|-----|
| Run-level | Container state within a single game session (floor transitions, death → new character) | ✅ |
| Session-level | Container state across game quit/reload | ❌ Post-MVP |

Run-level persistence uses a static registry:
```csharp
public static class ContainerRegistry {
    private static Dictionary<string, ContainerState> _states = new();

    public static void Save(string id, ContainerState state) => _states[id] = state;
    public static ContainerState Load(string id) =>
        _states.TryGetValue(id, out var state) ? state : null;
    public static void Clear() => _states.Clear(); // new run
}
```

### Loading Flow

```
1. Player interacts (E) near container
2. If first open:
     Roll lootTable: rolls = Random.Range(minRolls, maxRolls+1)
     For each roll: WeightedRandom(entries) → item + count
     _pendingItems = rolled items sorted by rarity (ascending)
     _loadedItems = []
     StartCoroutine(LoadRoutine())
3. If re-open (previously opened but interrupted):
     Resume LoadRoutine() from current position
4. LoadRoutine():
     foreach item in _pendingItems:
       wait for rarityDelay[item.rarity]
       move item from _pendingItems → _loadedItems
       LootContainerUI.Refresh()
     _allLoaded = true
5. On close (player walks away / presses Tab):
     StopCoroutine(_loadRoutine) — items stay in current state
     ContainerRegistry.Save(containerId, GetState())
6. Player transfers items:
     Double-click / drag / F → move from _loadedItems to backpack
     OnItemTransferred → remove from _loadedItems
     ContainerRegistry.Save(containerId, GetState())
```

### Rarity Delays

| Rarity | Delay | Visual |
|--------|-------|--------|
| Common | 0s | Instant |
| Uncommon | 1s | Short reveal animation |
| Rare | 2s | Pulse glow |
| Epic | 4s | Purple particle burst |
| Legendary | 8s | Gold shimmer |
| Mythic | 12s | Red lightning + screen shake |

### Container Types

| Type | Grid | LootTable |
|------|------|-----------|
| Desk | 4×3 | SO_Loot_OfficeDesk |
| FilingCabinet | 3×4 | SO_Loot_FilingCabinet |
| SupplyCloset | 4×4 | SO_Loot_SupplyCloset |
| Safe | 3×3 | SO_Loot_Safe |
| ServerRack | 4×2 | SO_Loot_ServerRack |
| CEODesk | 5×4 | SO_Loot_CEODesk |

### Furniture Variant Integration

```
FurniturePlacer assigns variant (普通/主管/CEO/破损).
LootContainer reads variant and applies modifiers:
  - 主管: quality +20%, can spawn Intel/Luxury
  - CEO: guaranteed Uncommon+, Legendary weight ×3
  - 破损: quality -50%, count -50%
  - 普通: no modifier
```

### Rules

1. **One roll per container lifetime**: loot is rolled on first open, not on every open.
2. **Interrupt is lossless**: items not yet loaded remain in `_pendingItems` and will load on re-open.
3. **F-take-all is rarity-descending**: best items transfer first.
4. **Empty container stays open**: shows "Empty" text, can be re-inspected.
5. **Container ID is floor+position based**: `$"floor{floorNumber}_room{roomId}_container{index}"` ensures uniqueness within a run.

---

## Engine Compatibility

Standard Unity — `Coroutine`, `ScriptableObject`, `Dictionary<string, T>`. No engine-specific concerns.

---

## ADR Dependencies

- **Depends On**: ADR-001 (Event Bus), ADR-002 (SO Data), ADR-006 (Inventory)
- **Depended On By**: None currently
