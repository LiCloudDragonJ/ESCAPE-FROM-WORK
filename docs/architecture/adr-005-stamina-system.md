# ADR-005: Stamina & Resource System

**Status**: Proposed
**Date**: 2026-07-17
**Author**: Claude Code (/architecture-review)

---

## Summary

Stamina is a single floating-point resource (0–100) managed by `PlayerCombat`. It drains on dodge (25), melee (weapon-defined, 10–35), and manual aim (8/s). It regenerates at 15/s after a 0.5s idle delay. Empty stamina prevents all consuming actions and triggers HUD warning. This ADR defines the resource model, drain/regen timing, and the architecture for future resources (armor, shield, weapon heat).

---

## Context

The GDD defines stamina as a tightly-coupled combat resource with frame-precise drain (manual aim drains per-frame). The current code has stamina variables commented out — stamina drain, regen, and empty-state enforcement need implementation. This is P0 gap #1.

### GDD Requirements Addressed

| TR-ID | Requirement |
|-------|-------------|
| TR-COMBAT-001 | Stamina: 100 max, dodge(25)/melee(weapon-defined)/aim(8/s), regen 15/s, 0.5s delay |
| TR-COMBAT-005 | Dodge consumes 25 stamina, 0.8s cooldown |
| TR-COMBAT-006 | V-key quick melee consumes stamina per weapon |
| TR-UI-001 | Stamina bar in HUD, flashes when empty |

---

## Decision

### Resource Model

```csharp
[System.Serializable]
public struct StaminaComponent {
    public float current;
    public float max;           // 100
    public float regenRate;     // 15/s
    public float regenDelay;    // 0.5s
    float lastDrainTime;

    public bool IsEmpty => current <= 0f;
    public float Percent => current / max;

    public void Drain(float amount) {
        current = Mathf.Max(0f, current - amount);
        lastDrainTime = Time.time;
    }

    public void Tick(float deltaTime) {
        if (current >= max) return;
        if (Time.time - lastDrainTime < regenDelay) return;
        current = Mathf.Min(max, current + regenRate * deltaTime);
    }
}
```

### Drain Sources

| Action | Drain | Type | Source |
|--------|-------|------|--------|
| Dodge | 25 | Instant | PlayerCombat.Dodge() |
| Quick Melee (V) | weapon.meleeLightCost (10–18) | Instant | PlayerCombat.QuickMelee() |
| Melee Light | weapon.meleeLightCost | Instant | WeaponBase.MeleeLight() |
| Melee Heavy | weapon.meleeHeavyCost (18–35) | Instant (on release) | WeaponBase.MeleeHeavy() |
| Manual Aim | 8/s | Per-frame (× deltaTime) | PlayerCombat while RMB held |

### Empty-State Enforcement

```
When stamina.current ≤ 0:
  ❌ Dodge (Space)        → no response
  ❌ Quick Melee (V)      → no response
  ❌ Melee Light/Heavy    → no response
  ❌ Manual Aim (RMB)     → force exit to auto-aim
  ✅ Shoot (LMB)          → allowed (doesn't consume stamina)
  ✅ Reload (R)           → allowed
  ✅ Move (WASD)          → allowed
  ✅ Interact (E)         → allowed
  ⚠️  Stamina bar flashes red in HUD
```

### Regen Timing (frame-precise)

```
Every Update():
  stamina.Tick(Time.deltaTime)

Regen starts 0.5s after last drain.
Partial regen is fine — if delay elapsed for 0.3s, regen 15 × 0.3 = 4.5 stamina.

Example timeline:
t=0.0: dodge → stamina 100→75, lastDrainTime=0.0
t=0.3: no regen (delay not met)
t=0.5: regen begins, stamina +0.0
t=0.6: stamina +1.5 → 76.5
t=1.0: stamina +7.5 → 84.0
```

### Architecture for Future Resources

```csharp
// ResourceComponent<T> not needed for MVP — but stamina architecture
// reserves the pattern for armor, shield, weapon heat in Post-MVP:
//
// All resources follow: current/max + drain + regen(delay, rate) + empty penalty
// Each resource gets its own component struct
// PlayerCombat owns all player-side resources
// EnemyBase owns enemy-side resources (stamina for enemy dodge/block, future)
```

### Rules

1. **Stamina is authoritative on server (future)** — for MVP single-player, `PlayerCombat` is the single source of truth.
2. **No fractional stamina display** — HUD shows integer (ceiling). Internal float for smooth regen.
3. **Drain is immediate** — the full cost is deducted at action start, not spread over the action duration.
4. **Interrupts drain stamina** — if a melee charge is interrupted by dodge, the heavy cost was already paid.
5. **Over-drain is allowed** — if stamina=20 and dodge costs 25, stamina goes to 0 (not -5). No debt.

---

## Alternatives Considered

### A: Stamina as a ScriptableObject-managed resource
- **Pros**: Inspector-visible, designers can tune without code
- **Cons**: SOs shouldn't hold runtime state; would need a runtime copy anyway
- **Verdict**: Values (max, regen rate, costs) from SO; runtime state in struct.

### B: Stamina bar segmented (multiple chunks, like Zelda)
- **Pros**: Visual clarity, partial regen milestones
- **Cons**: GDD specifies continuous 0–100 bar; complexity not justified for MVP
- **Verdict**: Reserved for Post-MVP if playtesting shows readability issues.

---

## Consequences

### Positive
- Simple float → minimal GC, trivial to serialize
- Frame-precise regen feels responsive
- Per-weapon stamina costs make weapon choice meaningful (heavy hitter vs agile fighter)
- Architecture reserves pattern for future resources without over-engineering MVP

### Negative
- Per-frame manual aim drain means stamina cost depends on framerate (mitigate: `× deltaTime`)
- Empty-state enforcement is scattered across input handlers — risk of missing a check
- No visual/audio feedback for "almost empty" — only "empty flash"

---

## Engine Compatibility

| Aspect | Status |
|--------|--------|
| Per-frame update | `Update()` or `FixedUpdate()` — `Update()` chosen for input responsiveness |
| deltaTime | `Time.deltaTime` — standard Unity |
| Serialization | `[System.Serializable]` struct — compatible with Unity JsonUtility |

---

## ADR Dependencies

- **Depends On**: ADR-001 (Event Bus — OnStaminaChanged for HUD), ADR-002 (SO Data — stamina costs in WeaponData)
- **Depended On By**: ADR-011 (UI — stamina bar HUD)

---

## Implementation Notes

- `PlayerCombat.cs`: currentStamina, maxStamina fields commented out — uncomment and wire
- Stamina drain calls need insertion points: Dodge(), QuickMelee(), MeleeLight(), MeleeHeavy()
- Manual aim drain: check in Update() while `isManualAiming == true`
- HUD stamina bar: `HUDManager.cs` — stamina display not yet implemented (gap #19)
