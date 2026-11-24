# ADR-001: Event Bus Architecture

**Status**: Proposed
**Date**: 2026-07-17
**Author**: Claude Code (/architecture-review)

---

## Summary

ESCAPE FROM WORK uses a centralized event bus (`GameEvents`) as the primary cross-system communication mechanism. All inter-system notifications (damage dealt, item picked up, enemy died, player died, floor changed) flow through the event bus rather than direct references. This decouples systems, enables future multiplayer architecture, and makes UI data binding straightforward.

---

## Context

The project has 17 planned systems (from `systems-index.md`), many with cross-cutting concerns. Direct references between systems create tight coupling that makes testing, refactoring, and multiplayer adaptation difficult. The existing codebase already has `GameEvents.cs` with a basic event bus pattern. This ADR formalizes the pattern as the architectural standard.

### GDD Requirements Addressed

| TR-ID | Requirement |
|-------|-------------|
| TR-COMBAT-003 | Damage pipeline must notify UI (floating text) and game state |
| TR-COMBAT-007 | Death flow must notify DeathScreen, MemorialWall, Loot (equipment drop) |
| TR-UI-004 | Death screen triggered by death event, not direct call |
| TR-LOOT-007 | Ammo safety net triggered by ammo count change event |
| TR-ENEMY-005 | Enemy death drop must notify Loot system |
| TR-FLOOR-001 | Floor generation completion must notify EnemySpawner, Loot placer |

---

## Decision

### Architecture

```
┌─────────────────────────────────────────────────────────┐
│                     GameEvents (Static)                  │
│                                                         │
│  Combat Events:           Loot Events:                  │
│    OnDamageDealt           OnItemPickedUp                │
│    OnEnemyKilled           OnItemDropped                 │
│    OnPlayerDeath           OnContainerOpened             │
│    OnPlayerDamaged         OnLootTableRolled             │
│                                                         │
│  Floor Events:            UI Events:                    │
│    OnFloorGenerated        OnPanelOpened                 │
│    OnFloorEntered          OnPanelClosed                 │
│    OnFloorCleared          OnHUDRefresh                  │
│                                                         │
│  Game State Events:        Weapon Events:               │
│    OnGameStarted           OnWeaponFired                 │
│    OnGamePaused            OnWeaponReloaded              │
│    OnRunCompleted          OnAmmoChanged                 │
└─────────────────────────────────────────────────────────┘
```

### Rules

1. **Publish-subscribe**: Systems subscribe to events they care about via `GameEvents.On[X] += Handler` in `OnEnable()` and unsubscribe in `OnDisable()`.
2. **Fire-and-forget**: Event publishers do not expect a return value. Events are notifications, not requests.
3. **Order independence**: No system may assume a specific order of handler execution. If ordering matters, use sequential events (e.g., `OnPreDeath` → `OnDeath` → `OnPostDeath`).
4. **Null-safe invocation**: All events use `?.Invoke()` to handle empty subscriber lists gracefully.
5. **No Unity null propagation**: Event handlers check for destroyed objects in their bodies — do not rely on Unity's fake-null for safety.

### Data Payload

Events carry a lightweight context struct (not the full source object):

```csharp
public struct DamageEvent {
    public GameObject target;
    public GameObject source;
    public float amount;
    public DamageType type;
    public bool isHeadshot;
    public Vector3 hitPoint;
}

public struct DeathEvent {
    public GameObject deceased;
    public DeathType deathType;
    public int floorNumber;
    public Vector3 deathPosition;
}
```

### Anti-Patterns Prohibited

- ❌ Direct `FindObjectOfType<SomeManager>()` calls for cross-system access
- ❌ Singleton `Instance` property access across system boundaries
- ❌ Events that carry full MonoBehaviour references (use IDs or lightweight structs)
- ❌ Subscribing in `Awake()` before all systems are initialized (use `OnEnable()` or `Start()`)

---

## Alternatives Considered

### A: Direct References (current partial pattern)
- **Pros**: Simple, no indirection, easy to trace in IDE
- **Cons**: Tight coupling, hard to test, blocks multiplayer adaptation
- **Verdict**: Rejected for cross-system communication. Accepted for intra-system calls (e.g., PlayerCombat → PlayerHealth within Player system).

### B: UnityEvents with Inspector Wiring
- **Pros**: Visual in editor, no-code wiring for designers
- **Cons**: Fragile (references break on prefab changes), no compile-time safety, slow for many connections
- **Verdict**: Rejected for code-driven systems. Accepted for designer-tunable local connections (e.g., button onClick).

### C: ScriptableObject Event Channels (Unity Atoms pattern)
- **Pros**: Decoupled, inspector-visible, testable
- **Cons**: Proliferation of SO assets (one per event type), adds project complexity
- **Verdict**: Rejected for MVP. Keep as future option if event count exceeds ~30 types.

---

## Consequences

### Positive
- Systems can be developed and tested independently
- UI layer can bind to events without knowing data sources
- Multiplayer adaptation: replace local event bus with network-synced events
- Easy to add logging/analytics by subscribing to all events

### Negative
- Indirection: harder to trace execution flow in debugger (mitigate with event logging in debug builds)
- Subscription leaks: forgetting to unsubscribe causes null reference errors (mitigate with `OnDisable` convention)
- Memory: event structs passed by value — keep structs small (<64 bytes recommended)

---

## Engine Compatibility

| Aspect | Status |
|--------|--------|
| Engine Version | 团结引擎 1.9.3 / Unity 6000.x — static event pattern is core C# feature, no engine dependency |
| Performance | C# delegate invocation is fast (~5ns per call). With <50 subscribers per event, no frame budget impact |
| Post-Cutoff APIs | None used — pure C# pattern |
| Deprecated APIs | None |

---

## ADR Dependencies

- **Depends On**: None (Foundation layer)
- **Depended On By**: ADR-004 (Damage Pipeline), ADR-005 (Stamina), ADR-006 (Inventory), ADR-008 (AI), ADR-011 (UI)

---

## Implementation Notes

- `GameEvents.cs` already exists at `Assets/_Project/Scripts/Core/GameEvents.cs`
- Existing events: `OnDamageDealt`, `OnEnemyKilled`, `OnItemPickedUp`, `OnGameStateChanged`
- Missing events to add per this ADR: stamina events, reload events, floor events, container events
- Convention: event names use `On[Subject][PastTenseVerb]` format
