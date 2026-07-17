# ADR-004: Damage Pipeline

**Status**: Proposed
**Date**: 2026-07-17
**Author**: Claude Code (/architecture-review)

---

## Summary

All damage in ESCAPE FROM WORK flows through a unified pipeline: `weapon fire → projectile → collision → tag check → IDamageable.TakeDamage() → FloatingDamageText → death check`. This ADR defines the damage interface contract, hit zone detection, damage type system, and the single-responsibility split between Combat (deals damage), IDamageable (receives damage), and UI (displays damage).

---

## Context

Damage flows across 4 systems (Combat, Weapon, Enemy, UI) and must handle: 4 weapon damage patterns, 3 hit zones (head/body/legs), cover reduction, beam penetration, AOE falloff, and floating damage text. The existing `IDamageable.cs` interface and `PlayerHealth.cs` / `EnemyBase.cs` implementations provide the skeleton. This ADR formalizes the contract so future systems (traps, environmental hazards, boss abilities) can plug into the same pipeline.

### GDD Requirements Addressed

| TR-ID | Requirement |
|-------|-------------|
| TR-COMBAT-003 | Damage pipeline: fire → projectile → collision → tag check → IDamageable → floating text → death |
| TR-COMBAT-002 | Hit zones: headshot ×1.5, legshot slow 30% 2s |
| TR-WEAPON-002 | Four damage patterns: semi, scatter, beam, AOE |
| TR-COMBAT-004 | Cover reduces damage by ×0.6 |
| TR-ENEMY-001 | IDamageable on all enemies |

---

## Decision

### Interface Contract

```csharp
public interface IDamageable {
    void TakeDamage(DamageContext ctx);
    bool IsAlive { get; }
    GameObject gameObject { get; } // for tag check
}

public struct DamageContext {
    public float baseAmount;
    public DamageType damageType;     // Physical, Beam, Explosive, StatusEffect
    public GameObject source;         // who fired/dealt damage
    public Vector3 hitPoint;
    public Vector3 hitNormal;
    public HitZone hitZone;           // Head, Body, Legs
    public bool isManualAim;          // was the shooter in manual aim?
    public bool ignoresCover;         // beam weapons set this true
}
```

### Hit Zone Detection

```
CapsuleCollider on enemy/player:
  Top 20% of capsule height   → HitZone.Head   (headshotMultiplier ×1.5)
  Bottom 30% of capsule height → HitZone.Legs   (×1.0 + slow 30% 2s)
  Middle 50%                    → HitZone.Body   (×1.0)

Hit zone only applies when isManualAim == true.
Auto-aim always hits Body.
```

### Damage Formula (Final)

```csharp
float CalculateFinalDamage(DamageContext ctx) {
    float dmg = ctx.baseAmount;

    // Hit zone (manual aim only)
    if (ctx.isManualAim && ctx.hitZone == HitZone.Head)
        dmg *= 1.5f;

    // Cover (bypassed by beam weapons and AOE)
    if (!ctx.ignoresCover && IsTargetInCover(ctx))
        dmg *= 0.6f;

    return Mathf.Max(1f, dmg); // minimum 1 damage (no zero-damage hits)
}
```

### Damage Types

| DamageType | Cover Bypass | Wall Bypass | Used By |
|------------|-------------|-------------|---------|
| Physical | No | No | Semiauto, Scatter, Melee |
| Beam | **Yes** | **No** | 投影仪射线枪 |
| Explosive | **Yes** | **No** | 马克杯投掷器, 邮件炸弹 |
| StatusEffect | **Yes** | **No** | C-class weapons, Boss abilities |

### Pipeline Flow

```
1. Weapon.Fire() → creates Projectile with DamageContext
2. Projectile.OnCollisionEnter(collider):
   a. Check collider.tag:
      "Enemy"  → collider.GetComponent<IDamageable>().TakeDamage(ctx)
      "Player" → collider.GetComponent<IDamageable>().TakeDamage(ctx)
      "Wall"   → destroy projectile (all damage types)
      "Furniture" → if ignoresCover: pass through; else: destroy projectile
   b. Spawn hit VFX at hitPoint
3. IDamageable.TakeDamage(ctx):
   a. Calculate finalDamage (hit zone + cover)
   b. Reduce HP
   c. GameEvents.OnDamageDealt(ctx with finalDamage)
   d. If damage type has status effect (legshot slow, C-class blind):
      ApplyStatusEffect(ctx)
   e. If HP ≤ 0: Die()
4. FloatingDamageText.Spawn(hitPoint, finalDamage, isHeadshot)
   (world-space, billboard to camera)
5. Die():
   a. Play death animation
   b. Drop loot (if enemy) or DropEquipment (if player)
   c. Remove "Enemy" tag (prevent dead-target lock)
   d. GameEvents.OnDeath(ctx)
```

### Rules

1. **All damage goes through IDamageable.TakeDamage()** — no direct HP manipulation.
2. **DamageContext is a struct** — passed by value, <64 bytes, no GC allocation.
3. **Hit zone only applies with manual aim** — auto-aim always hits body (×1.0).
4. **Cover check is distance-based for MVP**: target within 1m of Furniture-tagged collider → cover active.
5. **Wall check is collision-based**: projectile's own collider hits "Wall" tag → destroy.
6. **Minimum 1 damage**: No attack does zero damage (prevents degenerate invincibility).
7. **FloatingDamageText is world-space UI**: billboard to camera, owned by Combat system, not UI system.

---

## Alternatives Considered

### A: Interface with ref parameter for damage modification
- **Pros**: Receivers can modify incoming damage (armor, resistances)
- **Cons**: Mutable structs are error-prone, order-dependent
- **Verdict**: Reserved for future armor system. MVP: damage is calculated by sender, receiver only subtracts HP.

### B: Raycast-based hit detection instead of projectile colliders
- **Pros**: Instant, no physics tick dependency
- **Cons**: No travel time (feels less "physical"), harder to implement scatter/shotgun
- **Verdict**: Rejected. Projectiles with travel time match GDD spec and feel better.

---

## Consequences

### Positive
- Single interface for all damage — new damage sources (traps, hazards) require zero pipeline changes
- Hit zone system naturally rewards manual aim skill
- Cover bypass as a weapon property (not a formula exception) — clean, extensible
- World-space floating text avoids UI Canvas rebuild overhead

### Negative
- Physics-based projectiles mean damage is tied to FixedUpdate timing
- Distance-based cover check (not raycast) can feel unfair at boundaries
- DamageContext struct must stay small — adding fields bloats all call sites

---

## Engine Compatibility

| Aspect | Status |
|--------|--------|
| Physics collision | Unity Physics — `OnCollisionEnter` / `OnTriggerEnter` |
| Capsule collider zones | `collider.bounds` + `transform.position` — manual math, no engine dependency |
| World-space UI | `TextMeshPro` world-space or `Canvas` with `RenderMode.WorldSpace` |

---

## ADR Dependencies

- **Depends On**: ADR-001 (Event Bus), ADR-002 (SO Data — weapon damage values)
- **Depended On By**: ADR-005 (Stamina — damage may interact with stamina), ADR-008 (AI — enemies receive damage)

---

## Implementation Notes

- `IDamageable.cs` exists: currently has `TakeDamage(float amount, GameObject source)` — needs upgrading to `TakeDamage(DamageContext ctx)`
- `FloatingDamageText.cs` exists: world-space spawns
- `PlayerHealth.cs`: `TakeDamage()` implemented, needs DamageContext upgrade
- `EnemyBase.cs`: `TakeDamage()` implemented, needs hit zone detection
- Known gap: hit zone detection uses distance threshold, not capsule region math
- Known gap: no DamageType enum exists yet — all damage is Physical
