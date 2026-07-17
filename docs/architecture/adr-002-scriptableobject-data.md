# ADR-002: ScriptableObject Data Architecture

**Status**: Proposed
**Date**: 2026-07-17
**Author**: Claude Code (/architecture-review)

---

## Summary

All game configuration data (weapons, enemies, items, loot tables, furniture, room sets) is stored in Unity ScriptableObject assets. This separates data from behavior, enables designer tuning without code changes, and provides a single source of truth for each entity type. Each data SO is a pure data container — no runtime logic, no Update() methods, no scene references.

---

## Context

The game has 13 weapons, 8-10 enemy types, 10 item types, 6 container types, 40+ furniture types, and 3 floor archetypes. Hardcoding these values in C# would make balancing slow and error-prone. ScriptableObjects are Unity's standard solution for data-driven design.

### GDD Requirements Addressed

| TR-ID | Requirement |
|-------|-------------|
| TR-WEAPON-001 | WeaponData SO with damage, fireRate, spread, magazineSize, reloadTime, range |
| TR-ENEMY-006 | EnemyData SO with HP, damage, speed, detection params, drops, variantAffix |
| TR-LOOT-001 | LootTable SO with minRolls/maxRolls, weighted LootEntry[] pool |
| TR-LOOT-004 | ItemData SO with 10 item types × 6 rarities |
| TR-FLOOR-005 | RoomFurnitureSet SO binding room types to furniture templates |
| TR-WEAPON-003 | Ammo type definitions with stack limits |

---

## Decision

### SO Hierarchy

```
ScriptableObjects/
├── Weapons/
│   ├── SO_Weapon_StaplerPistol.asset
│   ├── SO_Weapon_KeyboardShotgun.asset
│   ├── SO_Weapon_ProjectorRay.asset
│   ├── SO_Weapon_MugLauncher.asset
│   ├── SO_Weapon_PPTLauncher.asset
│   ├── SO_Weapon_MeetingStaff.asset
│   ├── SO_Weapon_MailBomb.asset
│   ├── SO_Weapon_CoffeeInjector.asset
│   ├── SO_Weapon_ShredderSaw.asset
│   ├── SO_Weapon_CableWhip.asset
│   ├── SO_Weapon_KPIHammer.asset
│   ├── SO_Weapon_KeyboardBrick.asset
│   └── SO_Weapon_MugFlail.asset
├── Enemies/
│   ├── SO_Enemy_KPIZombie.asset
│   ├── SO_Enemy_PPTWraith.asset
│   ├── SO_Enemy_MailGhost.asset
│   ├── SO_Enemy_MeetingDemon.asset
│   ├── SO_Enemy_Security.asset
│   └── SO_Enemy_EliteCaptain.asset
├── Items/
│   ├── SO_Item_Paperclip.asset
│   ├── SO_Item_PrinterPaper.asset
│   ├── SO_Item_CoffeeBean.asset
│   ├── SO_Item_USB.asset
│   └── ... (per item type)
├── Loot/
│   ├── SO_Loot_OfficeDesk.asset
│   ├── SO_Loot_FilingCabinet.asset
│   ├── SO_Loot_SupplyCloset.asset
│   ├── SO_Loot_Safe.asset
│   ├── SO_Loot_ServerRack.asset
│   └── SO_Loot_CEODesk.asset
├── Furniture/
│   └── ... (40+ furniture templates)
└── Rooms/
    └── ... (per room type furniture sets)
```

### Base Class Architecture

```csharp
// All data SOs share a common base with editor-friendly properties
public abstract class GameDataSO : ScriptableObject {
    [Tooltip("Unique identifier — used for save/load and cross-referencing")]
    public string dataId;
    
    [Tooltip("Display name in UI")]
    public string displayName;
    
    [TextArea(2, 4)]
    [Tooltip("Flavor text for tooltips")]
    public string description;
}

// WeaponData — defines one weapon type
[CreateAssetMenu(menuName = "EFW/Weapon Data")]
public class WeaponData : GameDataSO {
    public WeaponClass weaponClass;    // A, C, or Melee
    public AmmoType ammoType;          // enum
    public float baseDamage;
    public float fireRate;             // RPM
    public float spread;               // degrees
    public int magazineSize;
    public float reloadTime;           // seconds
    public float range;                // meters
    public DamagePattern damagePattern; // Semi, Scatter, Beam, AOE, Melee
    // Melee-specific
    public float lightDamage;
    public float heavyDamage;
    public float chargeUpTime;
    public float meleeRange;
    // C-class specific
    public SpecialEffectData specialEffect;
    // Mod slots
    public ModSlotData[] modSlots;     // MVP: array length 1, reserved up to 3
}

// EnemyData — defines one enemy type
[CreateAssetMenu(menuName = "EFW/Enemy Data")]
public class EnemyData : GameDataSO {
    public EnemyClass enemyClass;       // Common, Security, Boss
    public float maxHP;
    public float baseDamage;
    public float moveSpeed;
    public float detectionRadius;      // meters
    public float detectionAngle;       // degrees
    public float hearingRadius;        // meters
    public float chaseRange;
    public float attackRange;
    public float attackCooldown;
    public DropEntry[] guaranteedDrops;    // badge + paperclips
    public DropEntry[] possibleDrops;      // weighted random
    public VariantAffix[] possibleAffixes; // for random variant system
}

// ItemData — defines one item type
[CreateAssetMenu(menuName = "EFW/Item Data")]
public class ItemData : GameDataSO {
    public ItemType itemType;           // enum: 10 types
    public Rarity rarity;               // enum: 6 rarities
    public int baseValue;               // paperclip value
    public int maxStackSize;            // backpack stack limit
    public int gridWidth;               // inventory cells
    public int gridHeight;
    public bool isStackable;
    public float freshnessDurationMin;  // for coffee — 0 = no decay
}

// LootTable — defines drop pool for a container type
[CreateAssetMenu(menuName = "EFW/Loot Table")]
public class LootTable : GameDataSO {
    public int minRolls;
    public int maxRolls;
    public LootEntry[] entries;
}

[System.Serializable]
public class LootEntry {
    public ItemData item;
    [Range(0f, 100f)]
    public float weight;
    public int minCount;
    public int maxCount;
}
```

### Rules

1. **No runtime mutation**: SO fields are read-only at runtime. If a value needs to change during play, copy it to a runtime struct first.
2. **No scene references**: SOs must not reference scene objects (transforms, GameObjects in hierarchy). Only other SOs or prefab references.
3. **One SO per entity**: Each weapon, enemy type, item type gets its own `.asset` file. Do not combine multiple entities into one SO.
4. **Editor-only validation**: SOs include `OnValidate()` checks that warn if required fields are missing or values are outside safe ranges (per GDD Tuning Knobs).
5. **dataId naming**: LowerCamelCase, system-unique. Example: `"staplerPistol"`, `"kpiZombie"`, `"itemPaperclip"`.

---

## Alternatives Considered

### A: JSON/YAML configuration files
- **Pros**: Text-editable, diff-friendly, no Unity dependency
- **Cons**: No Unity inspector support, manual parsing, no built-in hot reload
- **Verdict**: Rejected for MVP. Consider for save files only.

### B: Hardcoded C# constants
- **Pros**: Compile-time safety, fast, no asset management
- **Cons**: Requires recompile for every balance change, designer-unfriendly
- **Verdict**: Rejected for data. Accepted for mathematical constants (maxStamina=100, dodgeCost=25).

### C: Addressables remote catalog
- **Pros**: Hot-update data without client patch, CDN delivery
- **Cons**: Adds complexity, requires network infrastructure
- **Verdict**: Rejected for MVP. Architecture reserves Addressables namespace for future.

---

## Consequences

### Positive
- Designers can tune values in Unity Inspector without code access
- New weapons/enemies/items can be added by creating new `.asset` files
- Git-friendly: `.asset` files are YAML-text, diffable
- Test-friendly: tests can create SO instances with `ScriptableObject.CreateInstance<>()`

### Negative
- Proliferation of asset files (40+ furniture, 13+ weapons, 10+ enemies, 50+ items)
- SO references break if files are moved/renamed (mitigate with dataId string fallback)
- No built-in migration when SO schema changes (mitigate with `OnValidate()` defaults)

---

## Engine Compatibility

| Aspect | Status |
|--------|--------|
| Engine Version | 团结引擎 1.9.3 — ScriptableObject is core Unity feature, fully supported |
| Performance | SO field access is direct memory read (~1ns). No runtime overhead vs hardcoded values |
| Post-Cutoff APIs | None used |
| Deprecated APIs | None |

---

## ADR Dependencies

- **Depends On**: ADR-001 (Event Bus) — for change notifications when data is hot-reloaded
- **Depended On By**: ADR-004 (Damage Pipeline), ADR-006 (Inventory), ADR-007 (Floor Gen), ADR-008 (AI), ADR-009 (Weapons), ADR-010 (Loot)

---

## Implementation Notes

- `WeaponData.cs` already exists at `Assets/_Project/Scripts/Data/WeaponData.cs`
- `EnemyData.cs` already exists at `Assets/_Project/Scripts/Data/EnemyData.cs`
- `ItemData.cs` already exists at `Assets/_Project/Scripts/Data/ItemData.cs`
- `FurnitureTemplate.cs` and `RoomFurnitureSet.cs` exist in Data/
- Existing SOs: `SO_Weapon_StaplerPistol.asset`, `SO_Weapon_KeyboardMelee.asset`, `SO_Loot_OfficeDesk.asset`, plus 5 item SOs
- Gap: only 3 of 13 weapons have SOs. Only 1 of 6 loot tables exists.
