# ADR-009: Weapon System & Mod Slots

**Status**: Proposed
**Date**: 2026-07-17
**Author**: Claude Code (/architecture-review)

---

## Summary

All weapons derive from `WeaponBase` (abstract MonoBehaviour). Ranged weapons use projectile-based hit detection with four damage patterns (semi, scatter, beam, AOE). Melee weapons use capsule-based sweep detection with light/heavy attacks and charge mechanics. The mod system reserves a 3-slot array, with MVP implementing only slot 0 (sights). All weapon stats are data-driven via `WeaponData` ScriptableObject.

---

## Context

The GDD defines 13 weapons across 3 classes with 8 ammo types. The existing code has `WeaponBase`, `RangedWeapon`, `MeleeWeapon`, and `Projectile`. This ADR formalizes the architecture for weapon extension, mod slot reservation, and ammo type management.

### GDD Requirements Addressed

| TR-ID | Requirement |
|-------|-------------|
| TR-WEAPON-001 | Data-driven weapons via WeaponData SO |
| TR-WEAPON-002 | Four damage patterns |
| TR-WEAPON-003 | Ammo system: 8 types, stack limits |
| TR-WEAPON-004 | Reload with timer + dodge interrupt |
| TR-WEAPON-005 | C-class special effects |
| TR-WEAPON-006 | Mod system: MVP 1 slot, array of 3 |

---

## Decision

### Class Hierarchy

```
WeaponBase (abstract MonoBehaviour)
├── RangedWeapon
│   ├── (can be further specialized: BeamWeapon, AOEWeapon)
│   └── Damage patterns selected by WeaponData.damagePattern enum
└── MeleeWeapon
    └── Charge mechanic: Fire() starts charing, ReleaseCharge() executes heavy
```

### Damage Patterns

| Pattern | Class | Implementation |
|---------|-------|---------------|
| Semi | RangedWeapon | Single projectile per Fire(), spread applied |
| Scatter | RangedWeapon | N projectiles per Fire() (N from WeaponData.pelletCount) |
| Beam | RangedWeapon | Continuous ray, damage per second, ignores cover |
| AOE | RangedWeapon | Parabolic projectile, explosion radius on impact |
| Melee | MeleeWeapon | Capsule sweep or arc overlap, no projectile |

### Mod Slot Architecture

```csharp
// In WeaponData:
[SerializeField] private ModSlotData[] modSlots = new ModSlotData[3];
// MVP: only modSlots[0] is enabled. [1] and [2] are reserved.

[System.Serializable]
public class ModSlotData {
    public ModSlotType slotType; // Sights, AmmoConversion, SpecialAccessory
    public bool isUnlocked;      // false for slots [1] and [2] in MVP
    public ItemData installedMod;
}
```

### Ammo Architecture

```
AmmoType enum (in ItemData.cs):
  None=0, Staple=1, Keycap=2, PPT=3, Coffee=4, Mug=5,
  BulbLife=6, MeetingLink=7, JunkMail=8

Ammo reserve: Dictionary<AmmoType, int> in PlayerInventory
  - Not grid items — flat O(1) lookup
  - Reload pulls from reserve: requested = magazineSize - currentAmmo
  - consumed = inventory.ConsumeAmmo(ammoType, requested)
  - actual reload amount = consumed

Stack limits (backpack / stash):
  Staple: 200 / 2000
  Keycap: 100 / 1000
  BulbLife: 50 / 500
  Mug: 20 / 200
  PPT: 50 / 500
  MeetingLink: 20 / 200
  JunkMail: 30 / 300
  Coffee: 30 / 300
```

### Rules

1. **Fire rate gate**: `CanFire()` checks `Time.time - _lastFireTime >= 60f / fireRate`.
2. **Spread application**: `actualSpread = baseSpread * (isManualAim ? 0.5f : 1.0f)`, random yaw rotation.
3. **Beam ignores cover but not walls**: `Physics.Raycast` with `wallMask` → destroy on wall hit. Furniture colliders do not block beam.
4. **AOE ignores cover but not walls**: Explosion sphere check, filter out targets behind walls via raycast.
5. **C-class cooldowns**: Tracked per-weapon, not per-player. Cooldown starts on cast.

---

## Engine Compatibility

All standard Unity — no engine-specific concerns. `ScriptableObject`, `MonoBehaviour`, `Physics.Raycast/OverlapSphere`, `Instantiate` — all core Unity APIs.

---

## ADR Dependencies

- **Depends On**: ADR-002 (SO Data), ADR-004 (Damage Pipeline), ADR-006 (Inventory)
- **Depended On By**: None currently
