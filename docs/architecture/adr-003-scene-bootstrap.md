# ADR-003: Scene Bootstrap & Lifecycle

**Status**: Proposed
**Date**: 2026-07-17
**Author**: Claude Code (/architecture-review)

---

## Summary

ESCAPE FROM WORK uses a centralized `SceneBootstrap` → `GameManager` initialization chain. All systems register with GameManager during bootstrap, which then orchestrates the game loop states: Base → Raid → Dead → Base. This ensures consistent initialization order regardless of scene entry point (editor Play, build launch, scene reload).

---

## Context

The game has 17 systems with complex initialization dependencies. Unity's default `Awake()` / `Start()` order is non-deterministic across different GameObject hierarchies. The existing `SceneBootstrap.cs` and `GameManager.cs` already handle this partially — this ADR formalizes the pattern.

### GDD Requirements Addressed

| TR-ID | Requirement |
|-------|-------------|
| TR-COMBAT-007 | Death flow: Dead state → return to base → character select |
| TR-FLOOR-001 | Deterministic seed: runSeed set once at game start |
| TR-UI-006 | Canvas reference resolution and panel manager initialization |

---

## Decision

### Game States

```
                    ┌─────────┐
                    │  Boot   │ (SceneBootstrap.Awake)
                    └────┬────┘
                         ▼
                    ┌─────────┐
                    │  Base   │ (Tea room — stash, weapon rack, quest board, depart)
                    └────┬────┘
                         │ "Depart" → select floor
                         ▼
                    ┌─────────┐
              ┌────▶│  Raid   │ (Floor exploration, combat, loot)
              │     └────┬────┘
              │          │ HP ≤ 0          │ Extract success
              │          ▼                 ▼
              │     ┌─────────┐      ┌──────────┐
              │     │  Dead   │      │ Extract  │ (score screen, loot summary)
              │     └────┬────┘      └────┬─────┘
              │          │               │
              │          ▼               ▼
              │     ┌─────────┐      ┌─────────┐
              └─────│CharSelect│─────▶│  Base   │
                    └─────────┘      └─────────┘
```

### Initialization Order

```
Phase 0: SceneBootstrap.Awake()
  ├── Set runSeed (deterministic or random)
  ├── Load persistent data (save file if exists)
  └── Instantiate GameManager if not present

Phase 1: GameManager.Start()
  ├── Register core services (event bus, input)
  ├── Initialize UI (Canvas + HUDManager)
  └── Transition to Base state

Phase 2: On Enter Base
  ├── Load base scene additively (if not in base scene)
  ├── Initialize base UI panels
  └── Player can interact with stash, weapon rack, quest board

Phase 3: On Enter Raid (player selects floor → depart)
  ├── Set floorSeed = runSeed + floorNumber
  ├── Generate/enable floor
  ├── Spawn player at entry stairwell
  ├── Spawn enemies
  └── Enable combat HUD

Phase 4: On Raid End
  ├── Extract: save loot → transition to Base
  └── Dead: save death context → transition to CharSelect → Base
```

### Rules

1. **Single entry point**: `SceneBootstrap` is the only scene with auto-load. All other content is loaded additively or instantiated at runtime.
2. **No Awake() cross-references**: Systems must not access other systems in `Awake()`. Use `Start()` or subscribe to `GameEvents.OnGameStateChanged`.
3. **State transitions are atomic**: `GameManager.SetState(newState)` triggers `OnGameStateChanged(old, new)` — all listeners react before the next frame.
4. **runSeed is immutable**: Set once at game start, never changed. `floorSeed = runSeed + floorNumber` for deterministic floor generation.
5. **Persistent state survives scene reload**: GameManager is `DontDestroyOnLoad`. System managers that need persistence attach to GameManager's GameObject.

---

## Alternatives Considered

### A: Multiple entry-point scenes (one per game state)
- **Pros**: Each state has its own scene, simpler mental model
- **Cons**: State transitions require additive scene loading/unloading, complex state transfer, duplicate GameManagers
- **Verdict**: Rejected. Single bootstrap scene + additive loading is simpler for a single-player game.

### B: No central GameManager — systems self-initialize
- **Pros**: No coupling to a central manager
- **Cons**: Non-deterministic init order, race conditions, hard to debug
- **Verdict**: Rejected. 17 systems need coordinated init.

---

## Consequences

### Positive
- Deterministic initialization order regardless of scene setup
- Easy to add new systems: register with GameManager, subscribe to state changes
- Single `runSeed` guarantees reproducible floors for testing/debugging
- Clear state machine makes save/load boundaries obvious

### Negative
- GameManager becomes a dependency magnet — all systems reference it
- State transitions are blocking (no overlapping states)
- Boot time increases linearly with registered system count (acceptable for 17 systems on PC)

---

## Engine Compatibility

| Aspect | Status |
|--------|--------|
| Engine Version | 团结引擎 1.9.3 — MonoBehaviour lifecycle is core Unity |
| Scene Loading | `SceneManager.LoadSceneAsync` with `LoadSceneMode.Additive` — standard Unity API |
| DontDestroyOnLoad | Standard Unity — GameManager persists across additive scene loads |

---

## ADR Dependencies

- **Depends On**: ADR-001 (Event Bus — for state change notifications)
- **Depended On By**: ADR-007 (Floor Gen — needs runSeed), ADR-012 (Death — needs Dead state flow)

---

## Implementation Notes

- `SceneBootstrap.cs` exists at `Assets/_Project/Scripts/Core/SceneBootstrap.cs`
- `GameManager.cs` exists at `Assets/_Project/Scripts/Core/GameManager.cs`
- `QuickStart.cs` exists — provides editor-only fast-start for testing
- State enum already defined in GameManager. Missing: CharSelect state, Extract state.
- Known gap: no save/load hook in bootstrap. Reserve `LoadPersistentData()` call site.
