# ADR-012: Death & Inheritance Architecture

**Status**: Proposed
**Date**: 2026-07-17
**Author**: Claude Code (/architecture-review)

---

## Summary

When the player dies, the Death & Inheritance system executes a multi-step flow: drop equipment → spawn corpse + badge → record memorial entry → transition to character select → new character inherits base state. Death penalty varies by floor safety status. The inheritance model is "base persists, equipment lost" — base facilities, blueprints, and stash contents survive death; carried equipment does not (on dangerous floors).

---

## Context

Death & Inheritance was the most common missing dependency across all MVP GDDs (6 hard dependencies). With the GDD now written, this ADR defines the code architecture for the death flow, corpse system, memorial wall data model, and character selection state.

### GDD Requirements Addressed

| TR-ID | Requirement |
|-------|-------------|
| TR-COMBAT-007 | Death flow: HP≤0 → death anim → DropEquipment → corpse+badge → GameManager → Dead state |
| TR-UI-004 | DeathScreen: character name, floor, cause, lost equipment, memorial preview |
| TR-UI-005 | MemorialWall: persistent list of dead character badges |
| (Death GDD) | Corpse recovery, safe box, character selection, equipment recovery |

---

## Decision

### Death Flow

```
PlayerHealth.Die()
  ├── Set IsDead = true
  ├── Determine DeathType:
  │     SafeFloor (floor % 5 == 0 || floor == 50)
  │     DangerFloor (default)
  │     BossKill (vs boss enemy)
  │     ExtractionFail (near extract point)
  ├── DropEquipment() — spawn LooseLoot at death position
  │     SafeFloor: NPC recovers equipment (pay 50 paperclips on next base visit)
  │     DangerFloor: equipment stays on corpse
  ├── SpawnCorpse() — instantiate Corpse prefab at death position
  │     Corpse has: visual mesh, LootContainer (equipment), CharacterBadge item
  ├── Build DeathContext:
  │     { characterName, floorNumber, deathType, causeOfDeath, lootValueReturned }
  ├── GameEvents.OnPlayerDeath(deathContext)
  ├── GameManager.SetState(GameState.Dead)
  └── DeathScreen.Show(deathContext)
```

### Corpse System

```csharp
public class Corpse : MonoBehaviour, IInteractable {
    public DeathContext context;
    public List<ItemData> carriedEquipment;   // equipment on the corpse
    public CharacterBadge badge;              // the dead character's badge
    public bool hasBeenSearchedBySecurity;    // security took some items?

    public void Interact(GameObject player) {
        // Open loot panel showing corpse equipment.
        // Same LootContainerUI flow as regular containers.
    }
}
```

Security search chance: 30% on danger floors, 60% on boss floors. If searched, 1-3 random equipment items are removed from the corpse.

### Memorial Wall Data

```csharp
[System.Serializable]
public class MemorialEntry {
    public string characterName;
    public int deathFloor;
    public string causeOfDeath;
    public int lootValueReturned;     // total paperclip value of items brought back
    public string deathDate;          // real-world date
    public bool badgeRecovered;       // did a successor recover the badge?
}

// Stored in a persistent list (survives character death, persists across runs).
// Serialized via SaveSystem when implemented.
public List<MemorialEntry> memorialWall = new List<MemorialEntry>();
```

### Safe Box

```
Per-floor tea room safe box: 3×3 grid, 9 slots.
- Deposit: free
- Withdraw: 5 paperclips per slot
- Death on safe floor: contents preserved (NPC colleague can access it)
- Death on danger floor: contents preserved
- Death on boss floor: contents LOST (CEO / boss confiscates)
- Cross-character: NOT shared — each character has their own safe box
```

### Character Selection

```
After death screen:
1. Show 3-5 survivor options (cow/horse appearance variants)
2. All survivors have identical stats
3. New character spawns in Tea Room base
4. Inherited state:
   ✅ Base facilities and upgrade levels
   ✅ Unlocked blueprints
   ✅ Safe floor progress (which floors are cleared)
   ✅ Stash contents (8×10 grid)
   ✅ Memorial wall entries
   ❌ Previous character's equipment (on corpse or recovered by NPC)
   ❌ Previous character's backpack contents
   ❌ Previous character's safe box contents (return to stash)
```

### State Machine Extension

```
Add to GameManager state enum:
  Dead → CharacterSelect → Base (new character)

Transition Dead → CharacterSelect:
  Triggered by "Select New Character" button on DeathScreen.

Transition CharacterSelect → Base:
  Player picks a survivor → new character instantiated in base.
```

### Rules

1. **Equipment drop uses LooseLoot system**: weapons and items spawn as physics-enabled pickups at death position.
2. **Corpse persists within the current run**: does not survive game quit/reload in MVP (save system not implemented).
3. **Badge is always on corpse**: even if security searches, the character badge (工牌) is never taken.
4. **Memorial wall is append-only**: entries are never deleted, only added.
5. **Base inheritance is instant**: no "rebuild" time — new character sees the base exactly as the predecessor left it.

---

## Engine Compatibility

All standard Unity — no engine-specific concerns. `ScriptableObject` for memorial data (or JSON serialization for persistence).

---

## ADR Dependencies

- **Depends On**: ADR-001 (Event Bus), ADR-003 (Scene Bootstrap/Game States), ADR-006 (Inventory — equipment drop)
- **Depended On By**: ADR-013 (Save/Load — memorial wall and safe box need serialization)
