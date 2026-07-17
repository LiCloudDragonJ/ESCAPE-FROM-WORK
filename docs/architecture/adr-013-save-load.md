# ADR-013: Save/Load & Serialization

**Status**: Proposed
**Date**: 2026-07-17
**Author**: Claude Code (/architecture-review)

---

## Summary

Game state is serialized to a local JSON file on the player's PC. The save data model is a flat `SaveData` container holding: run metadata, base state, stash contents, memorial wall, quest progress, safe floor progress, unlocked blueprints, and badge flags. Save is triggered on: extraction success, character death, player quit. Load happens once at game start via `SceneBootstrap`. This is a PC-only single-player game — no cloud sync, no encryption (MVP), no save-scumming prevention.

---

## Context

The systems-index design dependency chain explicitly says: "Save/Load (#16) must be designed last because it serializes every other system's state." With 14 enemies, 13 weapons, 6 loot tables, base building, death inheritance, and quests now designed, the save data shape is finally knowable.

### GDD Requirements Addressed

| Source | Requirement |
|--------|-------------|
| Death GDD | Memorial wall persists across runs |
| Base GDD | Base facilities, stash, unlocked functions persist |
| Quest GDD | Quest progress persists across character deaths |
| Game Concept §7 | Safe floor progress persists |

---

## Decision

### Save Data Model

```csharp
[System.Serializable]
public class SaveData {
    // Run metadata
    public string saveVersion;         // "1.0"
    public string saveDate;            // ISO 8601
    public int runSeed;                // immutable seed for this run
    public int currentFloor;           // deepest reached
    public float playTimeSeconds;

    // Base state
    public int baseFloorNumber;        // which tea room is the base
    public int[] facilityLevels;       // [workbench, medical, intel, coffee]
    public int[] baseInvestedResources; // total paperclips invested in each

    // Stash (serialized as item ID → count pairs)
    public StashEntry[] stashContents; // { itemDataId, stackCount }

    // Memorial wall (append-only, never cleared)
    public MemorialEntry[] memorialWall;

    // Safe floor progress
    public int[] clearedFloors;        // floor numbers that are safe

    // Unlocked blueprints
    public string[] unlockedBlueprints; // itemDataId of craftable weapons

    // Badge flags (permanent progress)
    public string[] collectedBadges;   // badge itemDataId

    // Quest progress
    public QuestSaveEntry[] questStates;

    // Character (current run only — lost on death)
    public string currentCharacterName;
    public int currentCharacterHP;
    public StashEntry[] characterInventory;
    public string[] equippedWeaponIds; // A, C, Melee itemDataId
    public AmmoSaveEntry[] ammoReserve;
}
```

### Save Triggers

| Trigger | When | What's Saved |
|---------|------|-------------|
| Extraction | Player exits via stairwell/fire escape/elevator | Full save (character state + meta progress) |
| Death | PlayerHealth.Die() completes | Meta progress only (base, memorial, quests, blueprints, badges, safe floors) |
| Quit | Application.Quit() or Alt+F4 | Full save via `OnApplicationQuit` |
| Auto-save | Every 5 minutes (timer) | Full save to `autosave.json` |

### Save File Location

```
Windows: %USERPROFILE%/AppData/LocalLow/[Company]/[GameName]/saves/
  - save_01.json   (manual save / most recent)
  - autosave.json  (auto-save backup)
  - save_02.json   (additional slot, future)

MVP: single save slot. Save_01 is overwritten each time.
Post-MVP: multiple save slots (character select → which run to continue).
```

### Serialization Format

```csharp
// Newtonsoft.Json or Unity JsonUtility
string json = JsonUtility.ToJson(saveData, prettyPrint: true);
File.WriteAllText(savePath, json);

// Load
string json = File.ReadAllText(savePath);
SaveData data = JsonUtility.FromJson<SaveData>(json);
```

### Rules

1. **No save during combat**: Save is blocked while any enemy is in Chase/Attack state.
2. **Save on scene transition**: Before loading new floor, auto-save to prevent progress loss.
3. **Save file validation**: On load, verify `saveVersion` matches current. If not, reject with clear error message.
4. **Corrupt save handling**: If JSON parse fails, offer "Delete corrupted save and start fresh."
5. **No encryption for MVP**: Plain JSON. Post-MVP: optional base64 obfuscation to deter casual tampering.
6. **Character death = meta-progress-only save**: Character inventory and HP are NOT saved on death — only base/quest/memorial progress.

---

## Alternatives Considered

### A: BinaryFormatter
- **Pros**: Smaller file size, not human-readable (anti-tampering)
- **Cons**: .NET version dependent, fragile on schema changes, not debuggable
- **Verdict**: Rejected. JSON is debuggable, diffable, and migration-friendly.

### B: SQLite database
- **Pros**: Queryable, robust, industry standard
- **Cons**: Overkill for single-player with <100KB save file
- **Verdict**: Rejected for MVP. Keep as Post-MVP option if save data grows beyond 1MB.

---

## Engine Compatibility

| Aspect | Status |
|--------|--------|
| File I/O | `System.IO.File` — standard .NET, works on all Unity platforms |
| JsonUtility | Unity built-in — limited (no dictionaries, no nested arrays of custom types). Fallback: Newtonsoft.Json if complexity grows |
| Application.persistentDataPath | Standard Unity — cross-platform save location |

---

## ADR Dependencies

- **Depends On**: ADR-002 (SO Data — item IDs for serialization), ADR-003 (Scene Bootstrap — save load point), ADR-006 (Inventory), ADR-012 (Death — meta-progress save)
- **Depended On By**: None (last in dependency chain per systems-index)
