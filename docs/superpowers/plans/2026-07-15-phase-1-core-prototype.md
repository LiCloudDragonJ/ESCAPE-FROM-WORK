# ESCAPE FROM WORK — Phase 1: Core Prototype Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a playable core prototype — player moves, shoots, loots, extracts, and dies in a single office floor. Prove the 搜打撤 loop.

**Architecture:** Tuanjie Engine 1.9.3 (Unity China), top-down 2.5D, C# scripting. ScriptableObject-driven data for weapons/items/enemies. Single-scene prototype with modular floor layout. All systems designed with interfaces for future expansion (multi-floor, networking, advanced AI).

**Tech Stack:** Tuanjie Engine 1.9.3, C# 9.0, Unity Input System, ScriptableObjects for game data

**Design Doc:** `design/gdd/game-concept.md`

---

## File Structure Map

```
Assets/_Project/
├── Scripts/
│   ├── Core/
│   │   ├── GameManager.cs          — top-level orchestrator, game state machine
│   │   ├── GameEvents.cs           — ScriptableObject event channel (decoupled communication)
│   │   ├── SaveManager.cs          — save/load floor state, base state, character memorial
│   │   └── DataManager.cs          — loads ScriptableObject data catalogs at runtime
│   ├── Player/
│   │   ├── PlayerController.cs     — WASD movement, collision
│   │   ├── PlayerAim.cs            — mouse aim + auto-aim lock-on logic
│   │   ├── PlayerCombat.cs         — shooting, melee, weapon swap, dodge roll
│   │   ├── PlayerInventory.cs      — backpack slots, equipment slots, quick bar
│   │   └── PlayerInteraction.cs    — E-key: loot containers, pickups, doors, extraction
│   ├── Enemies/
│   │   ├── EnemyBase.cs            — abstract: health, damage, aggro, death
│   │   ├── KPIZombie.cs            — slow melee, high HP, mutters "完成指标…"
│   │   └── EnemySpawner.cs         — spawns enemies at random points per floor entry
│   ├── Weapons/
│   │   ├── WeaponBase.cs           — abstract: fire rate, damage, ammo, reload
│   │   ├── RangedWeapon.cs         — projectile-based weapons (A-type)
│   │   ├── MeleeWeapon.cs          — swing-based weapons
│   │   └── Projectile.cs           — bullet/projectile behaviour
│   ├── Level/
│   │   ├── FloorManager.cs         — floor lifecycle: enter, populate, clear, extract
│   │   ├── FloorGenerator.cs       — assembles room modules into a floor layout
│   │   ├── RoomModule.cs           — single room template (office, hallway, tea room)
│   │   ├── FloorState.cs           — tracks: safe/dangerous, cleared enemies, loot timers
│   │   └── FloorTransition.cs      — stairs/elevator entry & extraction logic
│   ├── Loot/
│   │   ├── LootContainer.cs        — interactable container, pulls from LootTable
│   │   ├── LootTable.cs            — ScriptableObject: weighted item pool
│   │   └── PickupItem.cs           — world-space item that goes into inventory on pickup
│   ├── UI/
│   │   ├── HUDManager.cs           — health, ammo, minimap, timer, floor number
│   │   ├── InventoryUI.cs          — backpack grid, equipment slots, drag-drop
│   │   └── DeathScreen.cs          — memorial: fallen character summary, "select new rep"
│   └── Data/
│       ├── ItemData.cs             — ScriptableObject: item definition (name, icon, stack size)
│       ├── WeaponData.cs           — ScriptableObject: weapon stats, ammo type, damage
│       ├── EnemyData.cs            — ScriptableObject: enemy HP, speed, damage, drops
│       ├── FloorTemplateData.cs    — ScriptableObject: room pool, enemy pool, loot pool
│       └── SaveData.cs             — serializable: floor states, base state, memorial wall
├── Scenes/
│   └── Main.unity                  — single scene: GameManager + floor + UI canvas
├── Prefabs/
│   ├── Player/
│   │   └── PlayerCharacter.prefab
│   ├── Enemies/
│   │   └── KPIZombie.prefab
│   ├── Level/
│   │   ├── Room_Office.prefab
│   │   ├── Room_Hallway.prefab
│   │   ├── Room_TeaRoom.prefab
│   │   └── Room_Stairwell.prefab
│   └── UI/
│       └── HUDCanvas.prefab
├── ScriptableObjects/
│   ├── Items/
│   │   ├── SO_Item_Paperclip.asset
│   │   ├── SO_Item_PrinterPaper.asset
│   │   └── SO_Item_USB.asset
│   ├── Weapons/
│   │   ├── SO_Weapon_StaplerPistol.asset
│   │   └── SO_Weapon_KeyboardMelee.asset
│   ├── Enemies/
│   │   └── SO_Enemy_KPIZombie.asset
│   └── Floors/
│       └── SO_Floor_TestOffice.asset
└── Resources/
    └── (placeholder art assets)
```

---

## Phase 1 Tasks

### Task 1: Project Scaffolding

**Files:**
- Create: Unity project via Tuanjie Hub (external — user creates project)
- Create: `Assets/_Project/` folder structure above
- Create: `Assets/_Project/Scripts/Core/GameManager.cs`
- Create: `Assets/_Project/Scripts/Core/GameEvents.cs`
- Create: `Assets/_Project/Scenes/Main.unity`

- [ ] **Step 1: Create Tuanjie Engine project**

User creates a new 2D (or 3D with orthographic camera) project in Tuanjie Hub 1.9.3 named "ESCAPE FROM WORK".

- [ ] **Step 2: Create folder structure**

In Unity Editor, create the folder hierarchy under `Assets/_Project/`:
```
Scripts/Core, Scripts/Player, Scripts/Enemies, Scripts/Weapons,
Scripts/Level, Scripts/Loot, Scripts/UI, Scripts/Data,
Scenes, Prefabs/Player, Prefabs/Enemies, Prefabs/Level, Prefabs/UI,
ScriptableObjects/Items, ScriptableObjects/Weapons,
ScriptableObjects/Enemies, ScriptableObjects/Floors,
Resources
```

- [ ] **Step 3: Write GameEvents.cs — central event bus**

```csharp
// Assets/_Project/Scripts/Core/GameEvents.cs
using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(menuName = "Events/GameEvent")]
public class GameEvent : ScriptableObject
{
    private readonly UnityEvent _event = new UnityEvent();
    
    public void Raise() => _event.Invoke();
    public void AddListener(UnityAction action) => _event.AddListener(action);
    public void RemoveListener(UnityAction action) => _event.RemoveListener(action);
}

// Typed variant for data passing
public abstract class GameEvent<T> : ScriptableObject
{
    private readonly UnityEvent<T> _event = new UnityEvent<T>();
    
    public void Raise(T value) => _event.Invoke(value);
    public void AddListener(UnityAction<T> action) => _event.AddListener(action);
    public void RemoveListener(UnityAction<T> action) => _event.RemoveListener(action);
}
```

- [ ] **Step 4: Write GameManager.cs — game state machine**

```csharp
// Assets/_Project/Scripts/Core/GameManager.cs
using UnityEngine;

public enum GameState { MainMenu, InRaid, BaseBuilding, Dead, Victory }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    
    [SerializeField] private GameEvent<int> onFloorEnter;
    [SerializeField] private GameEvent onFloorExtract;
    [SerializeField] private GameEvent<DeathContext> onPlayerDied;
    
    public GameState CurrentState { get; private set; } = GameState.MainMenu;
    public int CurrentFloorNumber { get; private set; } = 50;
    public CharacterMemorial LastDeath { get; private set; }
    
    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
    public void StartRaid(int floorNumber)
    {
        CurrentFloorNumber = floorNumber;
        CurrentState = GameState.InRaid;
        onFloorEnter.Raise(floorNumber);
    }
    
    public void ExtractFromFloor()
    {
        CurrentState = GameState.BaseBuilding;
        onFloorExtract.Raise();
    }
    
    public void PlayerDied(DeathContext context)
    {
        LastDeath = new CharacterMemorial(context);
        CurrentState = GameState.Dead;
        onPlayerDied.Raise(context);
    }
    
    public void SelectNewCharacter()
    {
        CurrentState = GameState.BaseBuilding;
        // base progress is preserved, new character inherits everything
    }
}

public class DeathContext
{
    public int floorNumber;
    public bool isSafeFloor;
    public string characterName;
    public int lootValueReturned;
    public string causeOfDeath;
}

public class CharacterMemorial
{
    public string name;
    public int deathFloor;
    public string causeOfDeath;
    public int lootValue;
    
    public CharacterMemorial(DeathContext ctx)
    {
        name = ctx.characterName;
        deathFloor = ctx.floorNumber;
        causeOfDeath = ctx.causeOfDeath;
        lootValue = ctx.lootValueReturned;
    }
}
```

- [ ] **Step 5: Create Main.unity scene with GameManager**

Create empty scene. Add GameObject "GameManager" with GameManager component. Create ScriptableObject event channels under `Assets/_Project/ScriptableObjects/Events/`:
- `Event_FloorEnter.asset`
- `Event_FloorExtract.asset`
- `Event_PlayerDied.asset`

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: project scaffolding — folder structure, GameManager, event system"
```

---

### Task 2: Data Definitions (ScriptableObjects)

**Files:**
- Create: `Assets/_Project/Scripts/Data/ItemData.cs`
- Create: `Assets/_Project/Scripts/Data/WeaponData.cs`
- Create: `Assets/_Project/Scripts/Data/EnemyData.cs`

- [ ] **Step 1: Write ItemData.cs**

```csharp
// Assets/_Project/Scripts/Data/ItemData.cs
using UnityEngine;

public enum ItemType { Currency, Ammo, Consumable, KeyItem, Collectible }
public enum AmmoType { None, Staple, Keycap, PPT, Coffee, Mug }

[CreateAssetMenu(menuName = "Data/Item")]
public class ItemData : ScriptableObject
{
    public string itemName;
    public string description;
    public ItemType itemType;
    public AmmoType ammoType;        // relevant only if ammo
    public Sprite icon;
    public int maxStackSize = 99;
    public int baseValue;            // sell price in 回形针
    public bool isUsableInRaid;      // can be consumed during raid
    public float freshnessDurationMinutes; // 0 = never expires (for coffee beans only)
}
```

- [ ] **Step 2: Write WeaponData.cs**

```csharp
// Assets/_Project/Scripts/Data/WeaponData.cs
using UnityEngine;

public enum WeaponClass { TypeA, TypeC, Melee }
public enum WeaponSlot { A, C, Melee }

[CreateAssetMenu(menuName = "Data/Weapon")]
public class WeaponData : ScriptableObject
{
    public string weaponName;
    public WeaponClass weaponClass;
    public WeaponSlot slot;
    public AmmoType ammoType;        // AmmoType.None for melee
    public float baseDamage;
    public float fireRate;           // shots per second
    public float range;
    public float spread;             // 0 = pinpoint, 1 = wide
    public int magazineSize;
    public float reloadTime;
    public bool hasHeadshotBonus;    // manual aim benefit
    public string specialEffect;     // description of C-type special effect
    public Sprite icon;
    public GameObject prefab;        // weapon model/world prefab
    // Melee-specific
    public float meleeRange;
    public float meleeArc;           // degrees, 360 = full AOE
    public float chargeUpTime;       // 0 = instant, >0 = hold to charge
}
```

- [ ] **Step 3: Write EnemyData.cs**

```csharp
// Assets/_Project/Scripts/Data/EnemyData.cs
using UnityEngine;

[CreateAssetMenu(menuName = "Data/Enemy")]
public class EnemyData : ScriptableObject
{
    public string enemyName;
    public string backstory;         // shown in bestiary
    public float maxHealth;
    public float moveSpeed;
    public float attackDamage;
    public float attackRange;
    public float attackCooldown;
    public float detectionRange;     // aggro radius
    public float detectionAngle;     // vision cone, 360 = omniscient
    // Drops
    public ItemData guaranteedDrop;  // 工牌 — always drops
    public ItemData[] possibleDrops; // random loot pool
    public float dropChance;         // per-item drop probability
    public int minCurrencyDrop;
    public int maxCurrencyDrop;
    public GameObject prefab;
}
```

- [ ] **Step 4: Create first data assets in Unity Editor**

Create ScriptableObjects under `Assets/_Project/ScriptableObjects/`:
- `SO_Item_Paperclip.asset` — 回形针, currency, maxStack 999, value 1
- `SO_Item_PrinterPaper.asset` — 打印纸, ammo, maxStack 200, value 2
- `SO_Item_CoffeeBean.asset` — 咖啡豆, consumable, maxStack 20, freshness 30min
- `SO_Item_USB.asset` — U盘, keyItem, maxStack 5, value 500
- `SO_Weapon_StaplerPistol.asset` — TypeA, Staple ammo, damage 25, fireRate 4, mag 15
- `SO_Weapon_KeyboardMelee.asset` — Melee, damage 40, arc 90, chargeUp 0 (fast)
- `SO_Enemy_KPIZombie.asset` — HP 100, speed 2, damage 15, range 1.5, detectionRadius 10

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: data definitions — ItemData, WeaponData, EnemyData + first assets"
```

---

### Task 3: Player Controller

**Files:**
- Create: `Assets/_Project/Scripts/Player/PlayerController.cs`
- Create: `Assets/_Project/Prefabs/Player/PlayerCharacter.prefab`

- [ ] **Step 1: Write PlayerController.cs**

```csharp
// Assets/_Project/Scripts/Player/PlayerController.cs
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float dodgeSpeed = 12f;
    [SerializeField] private float dodgeDuration = 0.2f;
    [SerializeField] private float dodgeCooldown = 0.8f;
    [SerializeField] private float dodgeDistancePenalty = 0.75f; // auto-aim penalty
    
    private Rigidbody2D _rb;
    private Vector2 _moveInput;
    private Vector2 _aimDirection;
    private bool _isDodging;
    private float _lastDodgeTime;
    
    public Vector2 AimDirection => _aimDirection;
    public bool IsDodging => _isDodging;
    public Vector2 Position => _rb.position;
    
    private void Awake() => _rb = GetComponent<Rigidbody2D>();
    
    private void FixedUpdate()
    {
        if (_isDodging) return;
        _rb.velocity = _moveInput * moveSpeed;
    }
    
    // Called by Input System
    public void OnMove(InputAction.CallbackContext ctx)
    {
        _moveInput = ctx.ReadValue<Vector2>();
    }
    
    public void OnAim(InputAction.CallbackContext ctx)
    {
        if (ctx.control.device is Mouse)
        {
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(ctx.ReadValue<Vector2>());
            _aimDirection = (mousePos - (Vector2)transform.position).normalized;
        }
        else
        {
            _aimDirection = ctx.ReadValue<Vector2>().normalized;
        }
    }
    
    public void OnDodge(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed || _isDodging) return;
        if (Time.time - _lastDodgeTime < dodgeCooldown) return;
        
        float distance = dodgeSpeed * dodgeDuration;
        // Apply auto-aim penalty: shorter dodge distance when locked on
        if (GetComponent<PlayerAim>().IsAutoAiming)
            distance *= dodgeDistancePenalty;
        
        Vector2 dodgeDir = _moveInput.magnitude > 0.1f ? _moveInput.normalized : -_aimDirection;
        StartCoroutine(DodgeRoutine(dodgeDir, distance));
    }
    
    private System.Collections.IEnumerator DodgeRoutine(Vector2 direction, float distance)
    {
        _isDodging = true;
        _lastDodgeTime = Time.time;
        float elapsed = 0f;
        Vector2 start = _rb.position;
        Vector2 end = start + direction * distance;
        
        while (elapsed < dodgeDuration)
        {
            _rb.MovePosition(Vector2.Lerp(start, end, elapsed / dodgeDuration));
            elapsed += Time.deltaTime;
            yield return null;
        }
        _rb.MovePosition(end);
        _isDodging = false;
    }
}
```

- [ ] **Step 2: Write PlayerAim.cs**

```csharp
// Assets/_Project/Scripts/Player/PlayerAim.cs
using UnityEngine;

public class PlayerAim : MonoBehaviour
{
    [SerializeField] private float autoAimRadius = 10f;
    [SerializeField] private float autoAimLockDelay = 0.15f;
    
    private PlayerController _controller;
    private Transform _currentLockTarget;
    private float _lockTimer;
    private bool _isManualAim; // true when right mouse held
    
    public bool IsAutoAiming => !_isManualAim && _currentLockTarget != null;
    public Transform LockTarget => _currentLockTarget;
    public Vector2 AimPoint { get; private set; }
    
    private void Awake() => _controller = GetComponent<PlayerController>();
    
    private void Update()
    {
        if (_isManualAim)
        {
            // Manual aim: free aim at mouse position
            AimPoint = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            _currentLockTarget = null;
        }
        else
        {
            // Auto aim: find nearest enemy in radius
            _currentLockTarget = FindNearestEnemy();
            
            if (_currentLockTarget != null)
            {
                // Lock-on delay before full accuracy
                _lockTimer += Time.deltaTime;
                AimPoint = _currentLockTarget.position;
            }
            else
            {
                _lockTimer = 0f;
                // Default to look direction when no enemies
                AimPoint = (Vector2)transform.position + _controller.AimDirection * 3f;
            }
        }
    }
    
    public void OnManualAim(InputAction.CallbackContext ctx)
    {
        _isManualAim = ctx.performed;
    }
    
    private Transform FindNearestEnemy()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, autoAimRadius);
        Transform nearest = null;
        float minDist = float.MaxValue;
        
        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Enemy")) continue;
            float dist = Vector2.Distance(transform.position, hit.transform.position);
            if (dist < minDist) { minDist = dist; nearest = hit.transform; }
        }
        return nearest;
    }
    
    // Call from PlayerCombat: auto-aim has lock-in penalty
    public bool IsLockEstablished() => _lockTimer >= autoAimLockDelay;
}
```

- [ ] **Step 3: Setup Input Actions in Unity Editor**

Create Input Actions asset at `Assets/_Project/Settings/PlayerInput.inputactions`:
```
PlayerActionMap:
  Move:     WASD / Left Stick  [Vector2, pass-through]
  Aim:      Mouse Position / Right Stick [Vector2, pass-through]
  Shoot:    Mouse Left / RT [Button]
  Melee:    Mouse Right / LT [Button]  (tap = melee, hold = manual aim)
  Dodge:    Space / South Button [Button]
  Interact: E / West Button [Button]
  Reload:   R / North Button [Button]
  SwapWeapon: Scroll Wheel / LB+RB [Value]
  UseItem1-4: 1-4 / D-Pad [Button]
  Inventory: Tab / Select [Button]
```

- [ ] **Step 4: Create PlayerCharacter.prefab**

- CapsuleCollider2D (or CircleCollider2D)
- Rigidbody2D (dynamic, no gravity)
- PlayerController, PlayerAim, PlayerCombat, PlayerInventory, PlayerInteraction
- Placeholder sprite: colored capsule (ox = brown, horse = dark brown)

- [ ] **Step 5: Commit**

---

### Task 4: Player Combat & Weapons

**Files:**
- Create: `Assets/_Project/Scripts/Player/PlayerCombat.cs`
- Create: `Assets/_Project/Scripts/Player/PlayerInventory.cs`
- Create: `Assets/_Project/Scripts/Weapons/WeaponBase.cs`
- Create: `Assets/_Project/Scripts/Weapons/RangedWeapon.cs`
- Create: `Assets/_Project/Scripts/Weapons/MeleeWeapon.cs`
- Create: `Assets/_Project/Scripts/Weapons/Projectile.cs`

- [ ] **Step 1: Write WeaponBase.cs**

```csharp
// Assets/_Project/Scripts/Weapons/WeaponBase.cs
using UnityEngine;

public abstract class WeaponBase : MonoBehaviour
{
    [SerializeField] protected WeaponData data;
    
    public WeaponData Data => data;
    public int CurrentAmmo { get; protected set; }
    
    public virtual void Initialize(WeaponData weaponData)
    {
        data = weaponData;
        CurrentAmmo = data.magazineSize;
    }
    
    public abstract void Fire(Vector2 from, Vector2 direction, bool isManualAim, bool isHeadshot);
    public abstract void Reload();
    
    public bool CanFire() => CurrentAmmo > 0 || data.ammoType == AmmoType.None;
}
```

- [ ] **Step 2: Write RangedWeapon.cs**

```csharp
// Assets/_Project/Scripts/Weapons/RangedWeapon.cs
using UnityEngine;

public class RangedWeapon : WeaponBase
{
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform muzzlePoint;
    
    private float _lastFireTime;
    
    public override void Fire(Vector2 from, Vector2 direction, bool isManualAim, bool isHeadshot)
    {
        if (!CanFire()) return;
        if (Time.time - _lastFireTime < 1f / data.fireRate) return;
        
        // Apply spread: manual aim = tight, auto aim = wider
        float effectiveSpread = isManualAim ? data.spread * 0.5f : data.spread;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        angle += Random.Range(-effectiveSpread, effectiveSpread);
        Vector2 finalDir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
        
        GameObject proj = Instantiate(projectilePrefab, muzzlePoint.position, Quaternion.identity);
        Projectile p = proj.GetComponent<Projectile>();
        float damage = data.baseDamage * (isHeadshot ? 1.5f : 1f);
        bool isTypeC = data.weaponClass == WeaponClass.TypeC;
        p.Initialize(finalDir, damage, data.range, isTypeC, data.specialEffect);
        
        CurrentAmmo--;
        _lastFireTime = Time.time;
    }
    
    public override void Reload()
    {
        CurrentAmmo = data.magazineSize;
        // TODO: consume ammo from inventory (Task 5)
    }
}
```

- [ ] **Step 3: Write MeleeWeapon.cs**

```csharp
// Assets/_Project/Scripts/Weapons/MeleeWeapon.cs
using UnityEngine;

public class MeleeWeapon : WeaponBase
{
    private float _chargeStartTime;
    private bool _isCharging;
    private float _lastSwingTime;
    
    public override void Fire(Vector2 from, Vector2 direction, bool isManualAim, bool isHeadshot)
    {
        if (data.chargeUpTime > 0)
        {
            // Charge weapon (KPI Hammer)
            _chargeStartTime = Time.time;
            _isCharging = true;
        }
        else
        {
            // Instant weapon
            Swing(direction, 1f);
        }
    }
    
    public void ReleaseCharge(Vector2 direction)
    {
        if (!_isCharging) return;
        _isCharging = false;
        float chargeRatio = Mathf.Clamp01((Time.time - _chargeStartTime) / data.chargeUpTime);
        Swing(direction, chargeRatio);
    }
    
    private void Swing(Vector2 direction, float power)
    {
        if (Time.time - _lastSwingTime < 1f / data.fireRate) return;
        _lastSwingTime = Time.time;
        
        // Raycast / OverlapCircle for melee hit detection
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, data.meleeRange);
        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Enemy") && !hit.CompareTag("Breakable")) continue;
            
            Vector2 toTarget = (hit.transform.position - transform.position).normalized;
            float angle = Vector2.Angle(direction, toTarget);
            if (angle > data.meleeArc / 2f) continue;
            
            float damage = data.baseDamage * power;
            // Apply full-circle multiplier for AOE weapons (马克杯流星锤)
            if (data.meleeArc >= 360) damage *= 0.6f;
            
            hit.GetComponent<IDamageable>()?.TakeDamage(damage);
        }
    }
    
    public override void Reload() { } // Melee never reloads
}
```

- [ ] **Step 4: Write Projectile.cs**

```csharp
// Assets/_Project/Scripts/Weapons/Projectile.cs
using UnityEngine;

public class Projectile : MonoBehaviour
{
    private Vector2 _direction;
    private float _damage;
    private float _range;
    private bool _isTypeC;
    private string _specialEffect;
    private float _traveled;
    
    [SerializeField] private float speed = 20f;
    
    public void Initialize(Vector2 dir, float dmg, float range, bool isTypeC, string effect)
    {
        _direction = dir;
        _damage = dmg;
        _range = range;
        _isTypeC = isTypeC;
        _specialEffect = effect;
        _traveled = 0f;
        transform.right = dir;
    }
    
    private void Update()
    {
        float step = speed * Time.deltaTime;
        transform.position += (Vector3)(_direction * step);
        _traveled += step;
        
        if (_traveled >= _range)
        {
            Destroy(gameObject);
        }
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
        {
            other.GetComponent<IDamageable>()?.TakeDamage(_damage);
            if (_isTypeC) ApplySpecialEffect(other.gameObject);
        }
        else if (other.CompareTag("Wall") || other.CompareTag("Obstacle"))
        {
            // Bullet stops at walls
        }
        Destroy(gameObject);
    }
    
    private void ApplySpecialEffect(GameObject target)
    {
        switch (_specialEffect)
        {
            case "Blind": target.GetComponent<EnemyBase>()?.ApplyBlind(3f); break;
            case "Root": target.GetComponent<EnemyBase>()?.ApplyRoot(2f); break;
            case "Taunt": target.GetComponent<EnemyBase>()?.ApplyTaunt(5f); break;
        }
    }
}
```

- [ ] **Step 5: Write PlayerCombat.cs**

```csharp
// Assets/_Project/Scripts/Player/PlayerCombat.cs
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCombat : MonoBehaviour
{
    [SerializeField] private Transform weaponParent;
    
    private PlayerAim _aim;
    private PlayerInventory _inventory;
    private WeaponBase _currentWeapon;
    private MeleeWeapon _chargingMelee;
    
    private void Awake()
    {
        _aim = GetComponent<PlayerAim>();
        _inventory = GetComponent<PlayerInventory>();
    }
    
    public void OnShoot(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        if (_currentWeapon == null) return;
        
        bool isManual = !_aim.IsAutoAiming;
        bool isHeadshot = isManual && IsAimingAtHead();
        _currentWeapon.Fire(transform.position, _aim.AimPoint - (Vector2)transform.position, isManual, isHeadshot);
    }
    
    public void OnMelee(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
        {
            // Switch to melee weapon and attack
            _inventory.SwapToSlot(WeaponSlot.Melee);
            _currentWeapon?.Fire(transform.position, _aim.AimPoint - (Vector2)transform.position, false, false);
        }
        else if (ctx.canceled && _chargingMelee != null)
        {
            _chargingMelee.ReleaseCharge(_aim.AimPoint - (Vector2)transform.position);
            _chargingMelee = null;
        }
    }
    
    public void OnSwapWeapon(InputAction.CallbackContext ctx)
    {
        float scroll = ctx.ReadValue<float>();
        if (scroll > 0) _inventory.CycleWeapon(1);
        else if (scroll < 0) _inventory.CycleWeapon(-1);
    }
    
    public void OnReload(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        _currentWeapon?.Reload();
    }
    
    public void EquipWeapon(WeaponBase weapon)
    {
        if (_currentWeapon != null) _currentWeapon.gameObject.SetActive(false);
        _currentWeapon = weapon;
        _currentWeapon.gameObject.SetActive(true);
        if (weapon is MeleeWeapon mw) _chargingMelee = mw;
        else _chargingMelee = null;
    }
    
    private bool IsAimingAtHead()
    {
        if (_aim.LockTarget == null) return false;
        Vector2 hitPoint = _aim.AimPoint;
        Vector2 targetPos = _aim.LockTarget.position;
        // Head is roughly top 25% of the enemy sprite
        float enemyHeight = 1f; // normalized
        return hitPoint.y > targetPos.y + enemyHeight * 0.25f;
    }
}
```

- [ ] **Step 6: Commit**

---

### Task 5: Player Inventory & Interaction

**Files:**
- Create: `Assets/_Project/Scripts/Player/PlayerInventory.cs`
- Create: `Assets/_Project/Scripts/Player/PlayerInteraction.cs`

- [ ] **Step 1: Write PlayerInventory.cs**

```csharp
// Assets/_Project/Scripts/Player/PlayerInventory.cs
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class InventorySlot
{
    public ItemData item;
    public int count;
    public int maxStack => item != null ? item.maxStackSize : 0;
    public bool IsEmpty => item == null || count <= 0;
}

public class PlayerInventory : MonoBehaviour
{
    [SerializeField] private int backpackSlots = 16;
    [SerializeField] private WeaponBase[] equippedWeapons = new WeaponBase[3]; // [A, C, Melee]
    
    private List<InventorySlot> _backpack = new List<InventorySlot>();
    private int _currentWeaponIndex = 0;
    
    private void Awake()
    {
        for (int i = 0; i < backpackSlots; i++)
            _backpack.Add(new InventorySlot());
    }
    
    public bool AddItem(ItemData item, int count)
    {
        // Stack onto existing slot first
        foreach (var slot in _backpack)
        {
            if (slot.item == item && slot.count < slot.maxStack)
            {
                int canAdd = Mathf.Min(count, slot.maxStack - slot.count);
                slot.count += canAdd;
                count -= canAdd;
                if (count <= 0) return true;
            }
        }
        // New slot
        foreach (var slot in _backpack)
        {
            if (slot.IsEmpty)
            {
                slot.item = item;
                slot.count = Mathf.Min(count, slot.maxStack);
                return count <= slot.maxStack;
            }
        }
        return false; // inventory full
    }
    
    public int GetItemCount(ItemData item)
    {
        int total = 0;
        foreach (var slot in _backpack)
            if (slot.item == item) total += slot.count;
        return total;
    }
    
    public bool RemoveItem(ItemData item, int count)
    {
        if (GetItemCount(item) < count) return false;
        int remaining = count;
        foreach (var slot in _backpack)
        {
            if (slot.item != item) continue;
            int take = Mathf.Min(remaining, slot.count);
            slot.count -= take;
            remaining -= take;
            if (slot.count <= 0) { slot.item = null; slot.count = 0; }
            if (remaining <= 0) break;
        }
        return true;
    }
    
    public void SwapToSlot(WeaponSlot slot)
    {
        int index = (int)slot;
        _currentWeaponIndex = index;
        GetComponent<PlayerCombat>().EquipWeapon(equippedWeapons[index]);
    }
    
    public void CycleWeapon(int direction)
    {
        _currentWeaponIndex = (_currentWeaponIndex + direction + 3) % 3;
        GetComponent<PlayerCombat>().EquipWeapon(equippedWeapons[_currentWeaponIndex]);
    }
    
    public List<InventorySlot> GetBackpack() => _backpack;
    public WeaponBase GetEquippedWeapon(WeaponSlot slot) => equippedWeapons[(int)slot];
}
```

- [ ] **Step 2: Write PlayerInteraction.cs**

```csharp
// Assets/_Project/Scripts/Player/PlayerInteraction.cs
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private float interactRange = 2f;
    [SerializeField] private LayerMask interactableMask;
    
    private PlayerInventory _inventory;
    private IInteractable _currentTarget;
    
    private void Awake() => _inventory = GetComponent<PlayerInventory>();
    
    private void Update()
    {
        // Highlight nearest interactable
        Collider2D hit = Physics2D.OverlapCircle(transform.position, interactRange, interactableMask);
        _currentTarget = hit?.GetComponent<IInteractable>();
        // TODO: show interaction prompt UI
    }
    
    public void OnInteract(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed || _currentTarget == null) return;
        _currentTarget.Interact(gameObject);
    }
}

public interface IInteractable
{
    void Interact(GameObject interactor);
    string GetPromptText();
}
```

- [ ] **Step 3: Commit**

---

### Task 6: Enemy Base + KPI Zombie

**Files:**
- Create: `Assets/_Project/Scripts/Enemies/EnemyBase.cs`
- Create: `Assets/_Project/Scripts/Enemies/KPIZombie.cs`
- Create: `Assets/_Project/Scripts/Enemies/EnemySpawner.cs`
- Create: `Assets/_Project/Prefabs/Enemies/KPIZombie.prefab`

- [ ] **Step 1: Write EnemyBase.cs**

```csharp
// Assets/_Project/Scripts/Enemies/EnemyBase.cs
using UnityEngine;
using System.Collections;

public interface IDamageable
{
    void TakeDamage(float damage);
}

public enum EnemyState { Idle, Patrol, Chase, Attack, Dead }

public abstract class EnemyBase : MonoBehaviour, IDamageable
{
    [SerializeField] protected EnemyData data;
    
    protected EnemyState _state = EnemyState.Patrol;
    protected Transform _target;
    protected float _currentHealth;
    protected float _lastAttackTime;
    
    // Status effects
    protected bool _isBlinded;
    protected float _blindTimer;
    protected bool _isRooted;
    protected float _rootTimer;
    protected bool _isTaunted;
    protected float _tauntTimer;
    
    protected virtual void Awake() => _currentHealth = data.maxHealth;
    
    protected virtual void Update()
    {
        UpdateStatusEffects();
        
        switch (_state)
        {
            case EnemyState.Idle: break;
            case EnemyState.Patrol: PatrolUpdate(); break;
            case EnemyState.Chase: ChaseUpdate(); break;
            case EnemyState.Attack: AttackUpdate(); break;
        }
    }
    
    protected virtual void PatrolUpdate()
    {
        // Simple wander or stationary
        if (_target != null && DistanceToTarget() < data.detectionRange)
            _state = EnemyState.Chase;
    }
    
    protected virtual void ChaseUpdate()
    {
        if (_target == null) { _state = EnemyState.Patrol; return; }
        if (_isRooted) return;
        
        float dist = DistanceToTarget();
        if (dist <= data.attackRange)
            _state = EnemyState.Attack;
        else if (dist > data.detectionRange * 1.5f)
            _state = EnemyState.Patrol;
        else
            MoveToward(_target.position);
    }
    
    protected virtual void AttackUpdate()
    {
        if (_target == null) { _state = EnemyState.Patrol; return; }
        if (DistanceToTarget() > data.attackRange) { _state = EnemyState.Chase; return; }
        
        if (Time.time - _lastAttackTime >= data.attackCooldown)
        {
            _lastAttackTime = Time.time;
            PerformAttack();
        }
    }
    
    protected abstract void PatrolUpdate();
    protected abstract void PerformAttack();
    
    public virtual void TakeDamage(float damage)
    {
        _currentHealth -= damage;
        if (_currentHealth <= 0) Die();
    }
    
    protected virtual void Die()
    {
        _state = EnemyState.Dead;
        // Spawn 工牌 dog tag drop
        // Spawn random loot
        // Trigger death animation
        Destroy(gameObject, 0.5f);
    }
    
    protected void MoveToward(Vector2 target)
    {
        Vector2 dir = (target - (Vector2)transform.position).normalized;
        transform.position += (Vector3)(dir * data.moveSpeed * Time.deltaTime);
    }
    
    protected float DistanceToTarget() =>
        _target != null ? Vector2.Distance(transform.position, _target.position) : float.MaxValue;
    
    public void SetTarget(Transform t) => _target = t;
    
    // Status effects
    public void ApplyBlind(float duration) { _isBlinded = true; _blindTimer = duration; }
    public void ApplyRoot(float duration) { _isRooted = true; _rootTimer = duration; }
    public void ApplyTaunt(float duration) { _isTaunted = true; _tauntTimer = duration; }
    
    private void UpdateStatusEffects()
    {
        float dt = Time.deltaTime;
        if (_isBlinded) { _blindTimer -= dt; if (_blindTimer <= 0) _isBlinded = false; }
        if (_isRooted) { _rootTimer -= dt; if (_rootTimer <= 0) _isRooted = false; }
        if (_isTaunted) { _tauntTimer -= dt; if (_tauntTimer <= 0) _isTaunted = false; }
    }
}
```

- [ ] **Step 2: Write KPIZombie.cs**

```csharp
// Assets/_Project/Scripts/Enemies/KPIZombie.cs
using UnityEngine;

public class KPIZombie : EnemyBase
{
    [SerializeField] private float patrolRadius = 3f;
    private Vector2 _patrolCenter;
    private Vector2 _patrolTarget;
    
    protected override void Awake()
    {
        base.Awake();
        _patrolCenter = transform.position;
        PickNewPatrolTarget();
    }
    
    protected override void PatrolUpdate()
    {
        base.PatrolUpdate();
        
        // Wander within patrol radius
        if (Vector2.Distance(transform.position, _patrolTarget) < 0.3f)
            PickNewPatrolTarget();
        MoveToward(_patrolTarget);
    }
    
    protected override void PerformAttack()
    {
        // Melee swipe with KPI report
        if (_target != null && DistanceToTarget() < data.attackRange + 0.5f)
        {
            _target.GetComponent<IDamageable>()?.TakeDamage(data.attackDamage);
            // Flavor: mutter "完成指标…完成指标…"
        }
    }
    
    private void PickNewPatrolTarget()
    {
        _patrolTarget = _patrolCenter + Random.insideUnitCircle * patrolRadius;
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            _target = other.transform;
            _state = EnemyState.Chase;
        }
    }
    
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player") && other.transform == _target)
        {
            _target = null;
            _state = EnemyState.Patrol;
        }
    }
}
```

- [ ] **Step 3: Write EnemySpawner.cs**

```csharp
// Assets/_Project/Scripts/Enemies/EnemySpawner.cs
using UnityEngine;
using System.Collections.Generic;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private GameObject[] enemyPrefabs;
    [SerializeField] private int minEnemies = 5;
    [SerializeField] private int maxEnemies = 12;
    [SerializeField] private Transform[] spawnZones; // regions within floor
    
    public void SpawnFloorEnemies()
    {
        int count = Random.Range(minEnemies, maxEnemies + 1);
        for (int i = 0; i < count; i++)
        {
            if (spawnZones.Length == 0) break;
            Transform zone = spawnZones[Random.Range(0, spawnZones.Length)];
            Vector2 spawnPos = (Vector2)zone.position + Random.insideUnitCircle * zone.localScale.x * 0.5f;
            GameObject prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
            Instantiate(prefab, spawnPos, Quaternion.identity);
        }
    }
}
```

- [ ] **Step 4: Create KPIZombie.prefab**

- CircleCollider2D (trigger for detection, separate collider for physics)
- Rigidbody2D (kinematic)
- SpriteRenderer with placeholder (green capsule with tie)
- KPIZombie component referencing SO_Enemy_KPIZombie
- Tag: "Enemy"

- [ ] **Step 5: Commit**

---

### Task 7: Floor System (Test Floor)

**Files:**
- Create: `Assets/_Project/Scripts/Level/FloorManager.cs`
- Create: `Assets/_Project/Scripts/Level/FloorGenerator.cs`
- Create: `Assets/_Project/Scripts/Level/RoomModule.cs`
- Create: `Assets/_Project/Scripts/Level/FloorTransition.cs`
- Create room prefabs under `Assets/_Project/Prefabs/Level/`

- [ ] **Step 1: Write RoomModule.cs**

```csharp
// Assets/_Project/Scripts/Level/RoomModule.cs
using UnityEngine;

public class RoomModule : MonoBehaviour
{
    public enum RoomType { Office, Hallway, TeaRoom, Stairwell, ServerRoom, ConferenceRoom }
    
    public RoomType roomType;
    public Vector2Int gridPosition;    // position in floor grid
    public Vector2Int gridSize = Vector2Int.one; // modules can be multi-tile
    public bool isExtractionPoint;     // fire escape?
    
    // Connection points for adjacency matching
    public bool connectionNorth;
    public bool connectionSouth;
    public bool connectionEast;
    public bool connectionWest;
    
    public Transform[] lootContainerSpawns; // fixed spawn points within this room
    public Transform[] enemySpawnZones;
}
```

- [ ] **Step 2: Write FloorGenerator.cs**

```csharp
// Assets/_Project/Scripts/Level/FloorGenerator.cs
using UnityEngine;
using System.Collections.Generic;

public class FloorGenerator : MonoBehaviour
{
    [SerializeField] private GameObject[] roomPrefabs;
    [SerializeField] private int gridWidth = 6;
    [SerializeField] private int gridHeight = 6;
    [SerializeField] private Vector2 tileSize = new Vector2(25f, 25f); // 150x150 total
    
    private RoomModule[,] _grid;
    
    public void GenerateFloor(int seed)
    {
        Random.InitState(seed);
        _grid = new RoomModule[gridWidth, gridHeight];
        
        // Step 1: Place fixed rooms
        // Tea room at center-bottom (near stairs entry)
        PlaceRoom(roomPrefabs[2], gridWidth / 2, 0); // TeaRoom
        
        // Stairwell at bottom-left (normal stairs)
        PlaceRoom(roomPrefabs[3], 0, 0); // Stairwell
        
        // Fire escape at top-right (diagonal from stairs)
        RoomModule fireEscape = PlaceRoom(roomPrefabs[3], gridWidth - 1, gridHeight - 1);
        fireEscape.isExtractionPoint = true;
        
        // Step 2: Fill with random office/hallway rooms
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                if (_grid[x, y] != null) continue;
                // Weighted random: 60% office, 30% hallway, 10% conference
                int roll = Random.Range(0, 10);
                int prefabIndex = roll < 6 ? 0 : (roll < 9 ? 1 : 4);
                PlaceRoom(roomPrefabs[prefabIndex], x, y);
            }
        }
        
        // Step 3: Update connection flags based on adjacency
        UpdateConnections();
    }
    
    private RoomModule PlaceRoom(GameObject prefab, int x, int y)
    {
        Vector3 worldPos = new Vector3(x * tileSize.x, y * tileSize.y, 0);
        GameObject obj = Instantiate(prefab, worldPos, Quaternion.identity, transform);
        RoomModule module = obj.GetComponent<RoomModule>();
        module.gridPosition = new Vector2Int(x, y);
        _grid[x, y] = module;
        return module;
    }
    
    private void UpdateConnections()
    {
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                if (_grid[x, y] == null) continue;
                _grid[x, y].connectionNorth = y < gridHeight - 1 && _grid[x, y + 1] != null;
                _grid[x, y].connectionSouth = y > 0 && _grid[x, y - 1] != null;
                _grid[x, y].connectionEast = x < gridWidth - 1 && _grid[x + 1, y] != null;
                _grid[x, y].connectionWest = x > 0 && _grid[x - 1, y] != null;
            }
        }
    }
}
```

- [ ] **Step 3: Write FloorManager.cs**

```csharp
// Assets/_Project/Scripts/Level/FloorManager.cs
using UnityEngine;
using System;
using System.Collections.Generic;

public class FloorManager : MonoBehaviour
{
    [SerializeField] private FloorGenerator generator;
    [SerializeField] private EnemySpawner enemySpawner;
    [SerializeField] private int floorNumber = 50;
    [SerializeField] private FloorTemplateData floorData;
    
    public FloorState State { get; private set; }
    public bool IsSafe => State.isCleared;
    
    private int _enemiesRemaining;
    
    public void InitializeFloor(int floorNum, int seed)
    {
        floorNumber = floorNum;
        generator.GenerateFloor(seed);
        enemySpawner.SpawnFloorEnemies();
        _enemiesRemaining = CountEnemies();
        
        State = FloorState.LoadOrCreate(floorNumber);
        State.lastEntryTime = DateTime.Now;
    }
    
    public void OnEnemyKilled()
    {
        _enemiesRemaining--;
        if (_enemiesRemaining <= 0)
        {
            State.isCleared = true;
            State.clearedTime = DateTime.Now;
            // Floor is now "safe"
            GameManager.Instance?.OnFloorCleared(floorNumber);
        }
    }
    
    private int CountEnemies()
    {
        return GameObject.FindGameObjectsWithTag("Enemy").Length;
    }
    
    public void Extract(bool useFireEscape)
    {
        // Save floor state
        State.Save();
        GameManager.Instance.ExtractFromFloor();
    }
}
```

- [ ] **Step 4: Write FloorState.cs**

```csharp
// Assets/_Project/Scripts/Level/FloorState.cs
using System;
using System.Collections.Generic;

[System.Serializable]
public class FloorState
{
    public int floorNumber;
    public bool isCleared;
    public DateTime clearedTime;
    public DateTime lastEntryTime;
    public int consecutiveVisits24h; // for loot decay tracking
    public HashSet<int> lootedContainerIds = new HashSet<int>(); // specific containers already emptied
    
    private static Dictionary<int, FloorState> _allFloors = new Dictionary<int, FloorState>();
    
    public static FloorState LoadOrCreate(int floorNum)
    {
        if (_allFloors.TryGetValue(floorNum, out var state))
            return state;
        
        var newState = new FloorState { floorNumber = floorNum, isCleared = false };
        _allFloors[floorNum] = newState;
        return newState;
    }
    
    public float GetLootDecayMultiplier()
    {
        // 24h decay: each visit within 24h reduces quality
        if (!isCleared) return 1f;
        float hoursSinceFirstEntry = (float)(DateTime.Now - lastEntryTime).TotalHours;
        if (hoursSinceFirstEntry >= 24f) { consecutiveVisits24h = 0; return 1f; }
        float decay = 1f - (consecutiveVisits24h * 0.25f); // 25% per visit
        return Mathf.Max(0.1f, decay);
    }
    
    public bool ShouldRefreshLoot()
    {
        if (!isCleared) return false;
        return (DateTime.Now - clearedTime).TotalHours >= 4f;
    }
    
    public void Save() { /* serialize to SaveData */ }
}
```

- [ ] **Step 5: Write FloorTransition.cs**

```csharp
// Assets/_Project/Scripts/Level/FloorTransition.cs
using UnityEngine;

public class FloorTransition : MonoBehaviour
{
    public enum TransitionType { NormalStairs, FireEscape, Elevator }
    
    [SerializeField] private TransitionType type;
    [SerializeField] private GameObject promptUI;
    
    private bool _playerInRange;
    
    public TransitionType Type => type;
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            _playerInRange = true;
            promptUI.SetActive(true);
        }
    }
    
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            _playerInRange = false;
            promptUI.SetActive(false);
        }
    }
    
    private void Update()
    {
        if (!_playerInRange) return;
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (type == TransitionType.Elevator)
            {
                // Only works if floor is safe and power is on
                FloorManager fm = FindObjectOfType<FloorManager>();
                if (fm != null && !fm.IsSafe)
                {
                    // Show "电梯仅限安全楼层使用"
                    return;
                }
                // Trigger elevator event: "叮" + enemies converge
            }
            
            if (type == TransitionType.FireEscape && Input.GetKey(KeyCode.E))
            {
                // Extract immediately via fire escape
                FindObjectOfType<FloorManager>()?.Extract(true);
            }
        }
    }
}
```

- [ ] **Step 6: Create room prefabs in Unity Editor**

Create placeholder room prefabs:
- `Room_TeaRoom.prefab` — 20×20 room, couch + water cooler + cabinet sprites
- `Room_Office.prefab` — 25×25 room, desk + chair + cubicle wall sprites
- `Room_Hallway.prefab` — 25×10 corridor
- `Room_Stairwell.prefab` — 15×15, stairs sprite + FloorTransition(Stairs)
- `Room_Conference.prefab` — 30×20, table + chairs

Each room needs:
- SpriteRenderer for floor/walls
- RoomModule component
- Empty child GameObjects as `lootContainerSpawns` and `enemySpawnZones`

- [ ] **Step 7: Commit**

---

### Task 8: Loot System

**Files:**
- Create: `Assets/_Project/Scripts/Loot/LootContainer.cs`
- Create: `Assets/_Project/Scripts/Loot/LootTable.cs`
- Create: `Assets/_Project/Scripts/Loot/PickupItem.cs`

- [ ] **Step 1: Write LootTable.cs (ScriptableObject)**

```csharp
// Assets/_Project/Scripts/Loot/LootTable.cs
using UnityEngine;

[System.Serializable]
public class LootEntry
{
    public ItemData item;
    [Range(0f, 1f)] public float weight;
    public int minCount = 1;
    public int maxCount = 1;
}

[CreateAssetMenu(menuName = "Data/LootTable")]
public class LootTable : ScriptableObject
{
    public LootEntry[] entries;
    public int minRolls = 1;
    public int maxRolls = 3;
    
    public (ItemData item, int count)[] Roll()
    {
        // Weighted random selection
        float totalWeight = 0f;
        foreach (var e in entries) totalWeight += e.weight;
        
        int rolls = Random.Range(minRolls, maxRolls + 1);
        var results = new (ItemData, int)[rolls];
        
        for (int i = 0; i < rolls; i++)
        {
            float roll = Random.Range(0f, totalWeight);
            float cumulative = 0f;
            foreach (var entry in entries)
            {
                cumulative += entry.weight;
                if (roll <= cumulative)
                {
                    int count = Random.Range(entry.minCount, entry.maxCount + 1);
                    results[i] = (entry.item, count);
                    break;
                }
            }
        }
        return results;
    }
}
```

- [ ] **Step 2: Write LootContainer.cs**

```csharp
// Assets/_Project/Scripts/Loot/LootContainer.cs
using UnityEngine;

public class LootContainer : MonoBehaviour, IInteractable
{
    [SerializeField] private LootTable lootTable;
    [SerializeField] private GameObject openVisual;
    [SerializeField] private GameObject closedVisual;
    
    private bool _isLooted;
    private int _containerId; // unique per floor, for FloorState tracking
    
    private void Awake() => _containerId = GetInstanceID();
    
    public void Interact(GameObject interactor)
    {
        if (_isLooted) return;
        
        // Check if container was already looted in this floor state
        FloorManager fm = FindObjectOfType<FloorManager>();
        if (fm != null && fm.State.lootedContainerIds.Contains(_containerId))
        {
            _isLooted = true;
            ShowEmpty();
            return;
        }
        
        // Roll loot
        var loot = lootTable.Roll();
        PlayerInventory inv = interactor.GetComponent<PlayerInventory>();
        
        foreach (var (item, count) in loot)
        {
            if (item == null) continue;
            bool added = inv.AddItem(item, count);
            if (!added)
            {
                // Inventory full — spawn world pickup
                SpawnWorldPickup(item, count);
            }
        }
        
        _isLooted = true;
        fm?.State.lootedContainerIds.Add(_containerId);
        closedVisual.SetActive(false);
        openVisual.SetActive(true);
    }
    
    private void SpawnWorldPickup(ItemData item, int count)
    {
        GameObject pickupObj = new GameObject($"Pickup_{item.itemName}");
        pickupObj.transform.position = transform.position + Vector3.up;
        PickupItem pickup = pickupObj.AddComponent<PickupItem>();
        pickup.Initialize(item, count);
        pickupObj.AddComponent<CircleCollider2D>().isTrigger = true;
        pickupObj.AddComponent<SpriteRenderer>().sprite = item.icon;
    }
    
    private void ShowEmpty() { closedVisual.SetActive(false); openVisual.SetActive(true); }
    
    public string GetPromptText() => _isLooted ? "[空]" : "[F] 搜刮";
}
```

- [ ] **Step 3: Write PickupItem.cs**

```csharp
// Assets/_Project/Scripts/Loot/PickupItem.cs
using UnityEngine;

public class PickupItem : MonoBehaviour, IInteractable
{
    private ItemData _item;
    private int _count;
    
    public void Initialize(ItemData item, int count)
    {
        _item = item;
        _count = count;
    }
    
    public void Interact(GameObject interactor)
    {
        PlayerInventory inv = interactor.GetComponent<PlayerInventory>();
        if (inv.AddItem(_item, _count))
        {
            Destroy(gameObject);
        }
    }
    
    public string GetPromptText() => $"[F] 拾取 {_item.itemName} x{_count}";
}
```

- [ ] **Step 4: Create LootTable ScriptableObjects in Unity Editor**

Create `SO_Loot_OfficeDesk.asset` — weighted table:
- 回形针: weight 0.4, 5-20
- 打印纸: weight 0.3, 3-10
- 咖啡豆: weight 0.1, 1-3
- U盘: weight 0.02, 1

- [ ] **Step 5: Commit**

---

### Task 9: Death, Memorial & New Character Flow

**Files:**
- Modify: `Assets/_Project/Scripts/Player/PlayerCombat.cs` — add health/damage
- Create: `Assets/_Project/Scripts/UI/DeathScreen.cs`

- [ ] **Step 1: Add health/damage to player**

Add to PlayerCombat.cs:
```csharp
[SerializeField] private float maxHealth = 100f;
private float _currentHealth;

// In Awake():
_currentHealth = maxHealth;

// Implement IDamageable:
public void TakeDamage(float damage)
{
    if (_isDead) return;
    _currentHealth -= damage;
    if (_currentHealth <= 0) Die();
}

private void Die()
{
    _isDead = true;
    
    // Build death context
    FloorManager fm = FindObjectOfType<FloorManager>();
    int lootValue = _inventory.CalculateTotalValue();
    
    DeathContext ctx = new DeathContext
    {
        floorNumber = fm != null ? fm.FloorNumber : 50,
        isSafeFloor = fm != null && fm.IsSafe,
        characterName = GameManager.Instance.CurrentCharacterName,
        lootValueReturned = fm != null && fm.IsSafe ? lootValue : 0,
        causeOfDeath = "被KPI丧尸击杀" // derive from last damage source
    };
    
    // Drop equipment + loot on ground (if dangerous floor)
    if (!ctx.isSafeFloor)
    {
        DropEquipmentOnGround();
    }
    
    GameManager.Instance.PlayerDied(ctx);
    // Show death screen
    FindObjectOfType<DeathScreen>()?.Show(ctx);
}

private void DropEquipmentOnGround()
{
    foreach (WeaponBase weapon in equippedWeapons)
    {
        if (weapon != null)
        {
            GameObject drop = Instantiate(weapon.Data.prefab, transform.position + Random.insideUnitCircle, Quaternion.identity);
            drop.AddComponent<PickupItem>().Initialize(weapon.Data, 1); // weapon as pickup
        }
    }
    // Spawn player's 工牌 as dog tag
    // ...
}
```

- [ ] **Step 2: Write DeathScreen.cs**

```csharp
// Assets/_Project/Scripts/UI/DeathScreen.cs
using UnityEngine;
using UnityEngine.UI;

public class DeathScreen : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Text nameText;
    [SerializeField] private Text floorText;
    [SerializeField] private Text causeText;
    [SerializeField] private Text lootText;
    [SerializeField] private Text memorialText;
    [SerializeField] private Button newCharacterButton;
    
    public void Show(DeathContext ctx)
    {
        panel.SetActive(true);
        nameText.text = $"安息吧, {ctx.characterName}";
        floorText.text = $"倒在第{ctx.floorNumber}层";
        causeText.text = $"死因: {ctx.causeOfDeath}";
        lootText.text = $"带回物资价值: {ctx.lootValueReturned} 回形针";
        memorialText.text = "茶水间纪念墙上多了一枚工牌。\n幸存者们将选出下一位代表。";
        
        newCharacterButton.onClick.AddListener(() =>
        {
            panel.SetActive(false);
            GameManager.Instance.SelectNewCharacter();
            // Reload base scene / show character select
        });
    }
}
```

- [ ] **Step 3: Commit**

---

### Task 10: Minimal HUD

**Files:**
- Create: `Assets/_Project/Scripts/UI/HUDManager.cs`
- Create: `Assets/_Project/Prefabs/UI/HUDCanvas.prefab`

- [ ] **Step 1: Write HUDManager.cs**

```csharp
// Assets/_Project/Scripts/UI/HUDManager.cs
using UnityEngine;
using UnityEngine.UI;

public class HUDManager : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private Slider healthBar;
    [SerializeField] private Text healthText;
    
    [Header("Ammo")]
    [SerializeField] private Text ammoText;
    [SerializeField] private Text ammoTypeText;
    
    [Header("Weapons")]
    [SerializeField] private Image weaponAIcon;
    [SerializeField] private Image weaponCIcon;
    [SerializeField] private Image weaponMeleeIcon;
    [SerializeField] private GameObject activeWeaponHighlight;
    
    [Header("Floor Info")]
    [SerializeField] private Text floorNumberText;
    [SerializeField] private Text floorStatusText; // "危险" / "安全"
    
    [Header("Extraction")]
    [SerializeField] private Text extractionTimerText;
    [SerializeField] private GameObject extractionWarning;
    
    [Header("Pickup Prompt")]
    [SerializeField] private Text interactionPrompt;
    
    private PlayerCombat _player;
    private PlayerInventory _inventory;
    
    public void UpdateHealth(float current, float max)
    {
        healthBar.value = current / max;
        healthText.text = $"{Mathf.CeilToInt(current)}/{max}";
    }
    
    public void UpdateAmmo(int current, int max, string ammoType)
    {
        ammoText.text = $"{current}/{max}";
        ammoTypeText.text = ammoType;
    }
    
    public void UpdateFloor(int number, bool isSafe)
    {
        floorNumberText.text = $"{number}F";
        floorStatusText.text = isSafe ? "安全" : "危险";
        floorStatusText.color = isSafe ? Color.green : Color.red;
    }
    
    public void ShowExtractionTimer(int seconds)
    {
        extractionWarning.SetActive(true);
        extractionTimerText.text = $"{seconds / 60}:{seconds % 60:D2}";
    }
    
    public void HideExtractionTimer()
    {
        extractionWarning.SetActive(false);
    }
    
    public void ShowInteractionPrompt(string text)
    {
        interactionPrompt.text = text;
        interactionPrompt.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }
}
```

- [ ] **Step 2: Create HUDCanvas.prefab in Unity Editor**

- Canvas (Screen Space Overlay, scale with screen)
  - HealthBar (top-left): slider + text
  - AmmoDisplay (bottom-center): text
  - WeaponIcons (bottom-right): 3 icon slots
  - FloorInfo (top-right): "50F | 危险"
  - ExtractionWarning (center): "3:00 内撤离!" (red, flashing)
  - InteractionPrompt (bottom-center, above ammo): "[F] 搜刮"
  - Minimap placeholder (bottom-left corner)

- [ ] **Step 3: Commit**

---

### Task 11: Integration — Playable Test Scene

**Files:**
- Modify: `Assets/_Project/Scenes/Main.unity`
- Create: `Assets/_Project/Prefabs/Level/FloorParent.prefab`

- [ ] **Step 1: Build Main.unity integration scene**

Add to scene:
1. **GameManager** GameObject (already from Task 1)
2. **FloorManager** GameObject with FloorGenerator + EnemySpawner
3. **PlayerCharacter** (instantiated from prefab at tea room position)
4. **HUDCanvas** (from prefab)
5. **DeathScreen** (from prefab)
6. **Main Camera** — orthographic, size 8, follows player via simple script:
```csharp
// SimpleCameraFollow.cs
public class SimpleCameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothSpeed = 5f;
    private void LateUpdate()
    {
        if (target == null) return;
        Vector3 targetPos = new Vector3(target.position.x, target.position.y, -10f);
        transform.position = Vector3.Lerp(transform.position, targetPos, smoothSpeed * Time.deltaTime);
    }
}
```

- [ ] **Step 2: Wire up Input System**

Create a `PlayerInput` component on the Player prefab, bind to the InputActions asset. In PlayerController, use `GetComponent<PlayerInput>().actions["Move"]` etc.

Alternatively, use `SendMessage` / `UnityEvent` binding in the InputActions asset directly.

- [ ] **Step 3: Create test flow script**

```csharp
// TestGameFlow.cs — simple script to bootstrap a test session
public class TestGameFlow : MonoBehaviour
{
    private void Start()
    {
        // Start at floor 50
        GameManager.Instance.StartRaid(50);
        
        // Initialize floor
        FloorManager fm = FindObjectOfType<FloorManager>();
        fm.InitializeFloor(50, Random.Range(0, 99999));
        
        // Spawn player at tea room
        // Give starter equipment
        PlayerInventory inv = FindObjectOfType<PlayerInventory>();
        inv.AddItem(/* 打印纸 */, 30);
        // Equip starter weapons
    }
}
```

- [ ] **Step 4: Playtest checklist**

- [ ] Player moves with WASD — feels responsive
- [ ] Aim follows mouse — cursor visible in orthographic camera
- [ ] Shoot fires projectile toward mouse — enemies take damage
- [ ] KPI zombie chases player — melee attack reduces player health
- [ ] Press E near container — loot rolls, items appear in inventory
- [ ] Walk to stairwell — E to extract
- [ ] Player dies — death screen shows, "new character" returns to base
- [ ] Melee weapon works — swing hits enemies in arc
- [ ] Health bar and ammo display update correctly

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: integration — playable test scene with core loop"
```

---

## Phase 1 Complete: What We Have

After these 11 tasks, we have:
- ✅ Player: move, aim, shoot, melee, dodge, interact, loot
- ✅ 1 enemy type: KPI zombie (patrol, chase, attack, die)
- ✅ 1 test floor: procedural room assembly, tea room, stairs, fire escape
- ✅ Loot: containers with weighted tables, pickups, inventory
- ✅ Extraction: normal stairs + fire escape
- ✅ Death: health system, death screen, new character flow
- ✅ HUD: health, ammo, floor info, interaction prompts
- ✅ 2 weapons: stapler pistol (ranged) + keyboard melee

## Next Phases (future plans)

- **Phase 2:** More weapons (8+), weapon mod slot, ammo types
- **Phase 3:** More enemies (PPT怨灵, 保安), Boss framework
- **Phase 4:** Base building (menu UI, facility upgrades)
- **Phase 5:** Multi-floor (elevator, floor transitions, power system)
- **Phase 6:** Economy (trading, daily NPC, coffee freshness)
- **Phase 7:** Special floors (hand-designed 10 floors)
- **Phase 8:** Narrative (environmental storytelling, memorial wall, endings)
- **Phase 9:** Save/Load, persistence, room module templates expansion
- **Phase 10:** Polish, juice, art assets, audio
