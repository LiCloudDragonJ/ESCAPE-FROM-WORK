# ADR-008: AI State Machine

**Status**: Proposed
**Date**: 2026-07-17
**Author**: Claude Code (/architecture-review)

---

## Summary

All enemies use a unified finite state machine (FSM) with 5 states: Idle → Patrol → Alert → Chase → Attack → Dead. The FSM is data-driven via `EnemyData` ScriptableObject. Detection uses a vision cone (120°, 15m) + hearing radius (8m) with raycast occlusion. Floor scaling multiplies base stats per floor number. A random variant system (30% chance) applies stat modifiers for variety. Bosses extend the base FSM with phase transitions.

---

## Context

The game has 8 common enemy types, 2 security types, and 4 bosses. Each needs distinct behavior but shares core FSM infrastructure. The existing `EnemyBase.cs` and `KPIZombie.cs` implement a 4-state FSM. This ADR formalizes the pattern, adds an Alert state, specifies detection as cone+raycast (fixing the current 360° OverlapSphere gap), and defines variant × scaling stacking.

### GDD Requirements Addressed

| TR-ID | Requirement |
|-------|-------------|
| TR-ENEMY-001 | FSM: Idle/Patrol → Chase → Attack → Dead, detection params |
| TR-ENEMY-002 | Floor-scaling: HP×1.03^(50-floor), etc. |
| TR-ENEMY-003 | Variant system: 30% chance, 5 types, weighted |
| TR-ENEMY-004 | Boss: multi-phase, phase-specific skills |
| TR-ENEMY-007 | "Enemy" tag for auto-aim, removed on death |

---

## Decision

### State Machine

```
         ┌──────────┐
         │   Idle   │ ←──────┐ (spawn)
         └────┬─────┘        │
              │ timer (3–8s) │
              ▼              │
         ┌──────────┐        │
    ┌───▶│  Patrol  │────────┘ (waypoint loop)
    │    └────┬─────┘
    │         │ detects player (vision cone OR hearing radius)
    │         ▼
    │    ┌──────────┐
    │    │  Alert   │ ←── NEW (not in current code)
    │    └────┬─────┘
    │         │ investigation timer ends OR re-detects player
    │         ▼
    │    ┌──────────┐   lose sight > 3s
    │    │  Chase   │ ───────────────▶ Alert (lastKnownPosition)
    │    └────┬─────┘
    │         │ in attackRange
    │         ▼
    │    ┌──────────┐   target leaves attackRange
    │    │  Attack  │ ─────────────────▶ Chase
    │    └────┬─────┘
    │         │ HP ≤ 0
    │         ▼
    │    ┌──────────┐
    │    │   Dead   │ (remove tag, drop loot, destroy after 3s)
    │    └──────────┘
    │
    └──── chaseRange exceeded (30m) → return to Patrol
```

### Alert State (New)

```
Alert is the "I think I heard something" state:
  - Enter: enemy detected player via hearing OR lost sight during chase
  - Behavior: move to lastKnownPlayerPosition at walk speed
  - On arrival: look around (rotate 90° left, 90° right, 2s each)
  - Exit: re-detect player → Chase; investigation complete (5s) → Patrol
  - Purpose: prevents binary "knows exactly where player is / completely forgets"
```

### Detection System

```csharp
bool CanDetectPlayer() {
    Vector3 toPlayer = player.position - transform.position;
    float distance = toPlayer.magnitude;

    // Vision check
    if (distance <= data.detectionRadius) {
        float angle = Vector3.Angle(transform.forward, toPlayer);
        if (angle <= data.detectionAngle * 0.5f) { // 120° cone = 60° half-angle
            if (!Physics.Raycast(eyesPosition, toPlayer.normalized, distance, wallMask)) {
                return true; // VISUAL DETECTION
            }
        }
    }

    // Hearing check (gunfire, footsteps)
    if (distance <= data.hearingRadius && playerMadeNoise) {
        return true; // AUDIO DETECTION → Alert state
    }

    return false;
}
```

### Floor Scaling

```
Applied at spawn time (in EnemySpawner):

enemyHP      = baseHP    × (1 + (50 - floorNumber) × 0.03)
enemyDamage  = baseDamage × (1 + (50 - floorNumber) × 0.02)
enemySpeed   = baseSpeed  × (1 + (50 - floorNumber) × 0.01)

50F (top):    ×1.00 HP,  ×1.00 Damage,  ×1.00 Speed
25F (mid):    ×1.75 HP,  ×1.50 Damage,  ×1.25 Speed
1F  (bottom): ×2.47 HP,  ×1.98 Damage,  ×1.49 Speed
```

### Variant × Scaling Stacking

```
Resolution (per user decision 2026-07-17): MULTIPLICATIVE stacking

Final stat = baseStat × floorMultiplier × variantMultiplier

Example: Tanky KPI丧尸 at floor 1
  HP = 60 × 2.47 × 2.0 = 296.4 HP
  Speed = 2.0 × 1.49 × 0.7 = 2.086 m/s

Variant roll: at spawn, roll 0–100:
  < 30 → apply variant (weighted: Elite=8, Swift=8, Tanky=8, Explosive=5, Regenerating=5)
  ≥ 30 → Normal
```

### Boss Phase Transitions

```csharp
// Bosses extend EnemyBase with:
class BossEnemy : EnemyBase {
    public int currentPhase;
    public BossPhaseData[] phases; // from EnemyData

    public override void TakeDamage(DamageContext ctx) {
        base.TakeDamage(ctx);
        CheckPhaseTransition();
    }

    void CheckPhaseTransition() {
        float hpPercent = currentHP / maxHP;
        foreach (var phase in phases) {
            if (hpPercent <= phase.triggerHPPercent && currentPhase < phase.phaseIndex) {
                TransitionToPhase(phase);
            }
        }
    }
}
```

### Rules

1. **Vision cone uses `data.detectionAngle × 0.5`** — the GDD value (120°) is the full cone, half-angle is 60° for `Vector3.Angle()`.
2. **Hearing triggers Alert, not Chase** — audio-only detection is less precise.
3. **Raycast uses `wallMask` layer** — furniture does not block vision, only structural walls.
4. **Dead enemies lose "Enemy" tag immediately** — prevents auto-aim lock on corpses.
5. **Floor scaling calculated once at spawn** — not recalculated when player changes floors.
6. **Variants are cosmetic + stat only** — no behavior changes (same FSM).

---

## Alternatives Considered

### A: Behavior Tree instead of FSM
- **Pros**: More modular, easier to compose complex behaviors
- **Cons**: Overkill for 5-state enemies, steeper learning curve, no Unity built-in BT
- **Verdict**: Rejected for MVP. FSM is simpler and sufficient. Reserve BT for boss AI in Post-MVP.

### B: Unity NavMesh for pathfinding
- **Pros**: Built-in, handles obstacle avoidance
- **Cons**: Requires baking, dynamic floor generation makes runtime baking complex
- **Verdict**: Deferred. MVP uses simple move-toward with obstacle checks. NavMesh for Post-MVP.

---

## Consequences

### Positive
- Unified FSM makes all enemies predictable and debuggable
- Data-driven detection params allow per-type tuning without code changes
- Alert state adds tactical depth (can juke enemies by breaking LOS)
- Multiplicative variant × scaling creates high stat variety with simple formulas

### Negative
- 5-state FSM is simple — complex behaviors (flanking, retreat, coordinating with other enemies) not supported
- No pathfinding mesh → enemies may get stuck on furniture (mitigate: push-out-on-collision)
- Variant × scaling can produce very tanky enemies at low floors (296 HP KPI丧尸)

---

## Engine Compatibility

| Aspect | Status |
|--------|--------|
| FSM | Pure C# enum + switch — no Unity dependency |
| Raycast | `Physics.Raycast` — standard Unity |
| OverlapSphere (deprecated for detection) | Replace with cone+raycast per this ADR |
| NavMesh | Not used in MVP |

---

## ADR Dependencies

- **Depends On**: ADR-001 (Event Bus — OnEnemyKilled), ADR-002 (SO Data — EnemyData), ADR-004 (Damage — IDamageable)
- **Depended On By**: None currently

---

## Implementation Notes

- `EnemyBase.cs` exists: FSM with Idle/Patrol/Chase/Attack/Dead
- `KPIZombie.cs` exists: concrete implementation
- `EnemyData.cs` exists: needs detection param fields added
- Known gaps (from gap analysis):
  - Detection is 360° OverlapSphere (gap #13 — P1) → replace with cone+raycast
  - No floor scaling (gap #7 — P1)
  - No random variants (gap #8 — P1)
  - Only 1 of 8 enemy types (gap #5 — P0)
  - Dead enemies keep "Enemy" tag (gap #24 — P2)
