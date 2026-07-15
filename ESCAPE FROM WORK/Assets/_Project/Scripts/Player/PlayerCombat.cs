using UnityEngine;
using EscapeFromWork.Core;
using EscapeFromWork.Data;
using EscapeFromWork.Weapons;

namespace EscapeFromWork.Player
{
    /// <summary>
    /// Central player combat controller. Manages health, three weapon slots
    /// (A / C / Melee), shooting, melee (tap-instant / hold-charge), weapon
    /// cycling, reloading, and death (equipment drop, dog-tag spawn, GameManager
    /// notification).
    ///
    /// <para>Implements <see cref="IDamageable"/> so that enemies can damage
    /// the player through the same interface used for other damageable entities.</para>
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class PlayerCombat : MonoBehaviour, IDamageable
    {
        // ---- Health --------------------------------------------------------------

        [Header("Health")]
        [Tooltip("Maximum health points.")]
        [SerializeField] private float maxHealth = 100f;

        [Tooltip("Current health. Starts at maxHealth.")]
        [SerializeField] private float currentHealth;

        // ---- Aim -----------------------------------------------------------------

        [Header("Aim")]
        [Tooltip("Reference to the PlayerAim component for targeting info.")]
        [SerializeField] private PlayerAim playerAim;

        [Tooltip("Maximum distance at which a shot can be considered a headshot against the lock target.")]
        [SerializeField] private float headshotDistanceThreshold = 5f;

        // ---- Inventory (forward reference) ---------------------------------------

        [Header("Inventory")]
        [Tooltip("Reference to PlayerInventory (Task 5). Assign when component exists.")]
        [SerializeField] private MonoBehaviour playerInventory; // TODO: Replace with PlayerInventory type

        // ---- Weapon slots --------------------------------------------------------

        [Header("Weapon Slots")]
        [Tooltip("Slot A — primary ranged weapon.")]
        [SerializeField] private WeaponBase slotA;

        [Tooltip("Slot C — creative / special-effect weapon.")]
        [SerializeField] private WeaponBase slotC;

        [Tooltip("Melee slot — always available.")]
        [SerializeField] private WeaponBase slotMelee;

        // ---- Death drops ---------------------------------------------------------

        [Header("Death Drops")]
        [Tooltip("Dog-tag prefab spawned at the player's death position.")]
        [SerializeField] private GameObject dogTagPrefab;

        // ---- Private state -------------------------------------------------------

        /// <summary>0 = Slot A, 1 = Slot C, 2 = Melee.</summary>
        private int _currentSlotIndex;

        /// <summary>Cached reference to the melee weapon for charge checking.</summary>
        private MeleeWeapon _currentMelee;

        // ---- Public properties ---------------------------------------------------

        /// <summary>
        /// Maximum health pool.
        /// </summary>
        public float MaxHealth => maxHealth;

        /// <summary>
        /// Current health value. Clamped to [0, maxHealth].
        /// </summary>
        public float CurrentHealth => currentHealth;

        /// <summary>
        /// The weapon in the currently active slot, or null if the slot is empty.
        /// </summary>
        public WeaponBase CurrentWeapon => GetWeaponInSlot(_currentSlotIndex);

        /// <summary>
        /// True when the player is dead (health has reached zero).
        /// </summary>
        public bool IsDead { get; private set; }

        // ---- Unity lifecycle ----------------------------------------------------

        private void Awake()
        {
            currentHealth = maxHealth;
            IsDead = false;
        }

        private void Update()
        {
            PollShootInput();
            PollMeleeInput();
            PollSwapWeaponInput();
            PollReloadInput();
        }

        // ---- IDamageable ---------------------------------------------------------

        /// <inheritdoc />
        public void TakeDamage(float amount, GameObject source)
        {
            if (IsDead)
                return;

            currentHealth = Mathf.Max(0f, currentHealth - amount);

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        // ---- Weapon slot management ----------------------------------------------

        /// <summary>
        /// Returns the weapon equipped in the given slot index, or null.
        /// </summary>
        private WeaponBase GetWeaponInSlot(int index)
        {
            switch (index)
            {
                case 0: return slotA;
                case 1: return slotC;
                case 2: return slotMelee;
                default: return slotMelee;
            }
        }

        /// <summary>
        /// Assign a weapon to its correct slot based on its
        /// <see cref="WeaponData.Slot"/> value.
        /// </summary>
        /// <param name="weapon">The weapon instance to equip.</param>
        public void EquipWeapon(WeaponBase weapon)
        {
            if (weapon == null || weapon.Data == null)
                return;

            switch (weapon.Data.Slot)
            {
                case WeaponSlot.A:
                    slotA = weapon;
                    break;
                case WeaponSlot.C:
                    slotC = weapon;
                    break;
                case WeaponSlot.Melee:
                    slotMelee = weapon;
                    _currentMelee = weapon as MeleeWeapon;
                    break;
            }
        }

        /// <summary>
        /// Cycle to the next non-empty weapon slot. Wraps around.
        /// Called from <see cref="Update"/> via scroll wheel input.
        /// </summary>
        private void PollSwapWeaponInput()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) < 0.001f)
                return;

            if (IsDead)
                return;

            CycleWeapon();
        }

        /// <summary>
        /// Advance to the next occupied weapon slot, skipping empty ones.
        /// Cancels any in-progress melee charge on the outgoing weapon.
        /// </summary>
        private void CycleWeapon()
        {
            // Cancel charge on the current melee weapon before switching.
            CancelCurrentMeleeCharge();

            int attempts = 0;
            do
            {
                _currentSlotIndex = (_currentSlotIndex + 1) % 3;
                attempts++;
            }
            while (GetWeaponInSlot(_currentSlotIndex) == null && attempts < 3);

            // Update melee reference after switching.
            _currentMelee = GetWeaponInSlot(_currentSlotIndex) as MeleeWeapon;
        }

        /// <summary>
        /// Swap directly to the melee slot, cancelling any charge on the previous
        /// melee weapon first.
        /// </summary>
        private void SwapToMeleeSlot()
        {
            CancelCurrentMeleeCharge();
            _currentSlotIndex = 2;
            _currentMelee = slotMelee as MeleeWeapon;
        }

        /// <summary>
        /// Cancel an in-progress melee charge so we don't leave a stale charge
        /// when switching away from the melee slot.
        /// </summary>
        private void CancelCurrentMeleeCharge()
        {
            if (_currentMelee != null && _currentMelee.IsCharging)
            {
                _currentMelee.CancelCharge();
            }
        }

        // ---- Shooting ------------------------------------------------------------

        /// <summary>
        /// Fire the current weapon. Polled via left mouse button in Update.
        /// Determines manual-aim status and headshot eligibility before firing.
        /// </summary>
        private void PollShootInput()
        {
            if (!Input.GetMouseButtonDown(0) || IsDead)
                return;

            WeaponBase weapon = CurrentWeapon;
            if (weapon == null || (weapon.Data != null && weapon.Data.IsMelee))
                return;

            if (weapon.Data != null && weapon.Data.IsMelee)
                return;

            Vector3 from = transform.position;
            Vector3 direction = GetAimDirection();
            bool isManualAim = playerAim != null && playerAim.IsManualAim;
            bool isHeadshot = DetermineHeadshot();

            weapon.Fire(from, direction, isManualAim, isHeadshot);
        }

        /// <summary>
        /// Returns the world-space aim direction (XZ plane) based on the current
        /// targeting mode.
        /// </summary>
        private Vector3 GetAimDirection()
        {
            if (playerAim != null)
            {
                Vector3 toAim = playerAim.AimPoint - transform.position;
                toAim.y = 0f;
                if (toAim.sqrMagnitude > 0.001f)
                    return toAim.normalized;
            }

            // Fallback: forward.
            return transform.forward;
        }

        /// <summary>
        /// Determines whether the current shot qualifies as a headshot.
        /// A headshot requires an auto-aim lock target within
        /// <see cref="headshotDistanceThreshold"/> world units.
        /// </summary>
        /// <returns>True when the shot should receive bonus headshot damage.</returns>
        private bool DetermineHeadshot()
        {
            if (playerAim == null)
                return false;

            if (playerAim.LockTarget == null)
                return false;

            float distance = Vector3.Distance(transform.position, playerAim.LockTarget.position);
            return distance <= headshotDistanceThreshold;
        }

        // ---- Melee ---------------------------------------------------------------

        /// <summary>
        /// Melee attack input polled via right mouse button in Update.
        /// On button press: swaps to the melee slot and either performs an instant
        /// swing (no-charge weapons) or begins charging (charge weapons).
        /// On button release: releases the charged swing.
        /// </summary>
        private void PollMeleeInput()
        {
            if (IsDead)
                return;

            Vector3 from = transform.position;
            Vector3 direction = GetAimDirection();

            if (Input.GetMouseButtonDown(1))
            {
                // Button pressed — swap to melee slot.
                SwapToMeleeSlot();

                MeleeWeapon melee = slotMelee as MeleeWeapon;
                if (melee != null)
                {
                    _currentMelee = melee;
                    // Fire begins charging (charge weapons) or instant-swings (no-charge).
                    melee.Fire(from, direction, false, false);
                }
            }
            else if (Input.GetMouseButtonUp(1))
            {
                // Button released — if charging, release the heavy attack.
                if (_currentMelee != null && _currentMelee.IsCharging)
                {
                    _currentMelee.ReleaseCharge(from, direction);
                }
            }
        }

        // ---- Reload --------------------------------------------------------------

        /// <summary>
        /// Reload the currently equipped weapon. Polled via R key in Update.
        /// </summary>
        private void PollReloadInput()
        {
            if (!Input.GetKeyDown(KeyCode.R))
                return;

            if (IsDead)
                return;

            WeaponBase weapon = CurrentWeapon;
            if (weapon == null)
                return;

            weapon.Reload();
        }

        // ---- Death ---------------------------------------------------------------

        /// <summary>
        /// Handle player death: drop equipment on dangerous floors, spawn a
        /// dog tag, and notify <see cref="GameManager"/>.
        /// </summary>
        private void Die()
        {
            if (IsDead)
                return;

            IsDead = true;
            currentHealth = 0f;

            int currentFloor = GameManager.Instance != null
                ? GameManager.Instance.CurrentFloorNumber
                : 0;

            bool isDangerousFloor = !IsSafeFloor(currentFloor);

            // Drop equipped weapons on dangerous floors (permadeath penalty).
            if (isDangerousFloor)
            {
                DropEquipment();
            }

            // Spawn dog tag (工牌) at death location.
            if (dogTagPrefab != null)
            {
                Instantiate(dogTagPrefab, transform.position, Quaternion.identity);
            }

            // Build death context and notify GameManager.
            DeathContext ctx = new DeathContext
            {
                floorNumber = currentFloor,
                isSafeFloor = !isDangerousFloor,
                characterName = gameObject.name,
                causeOfDeath = "Combat",
                lootValueReturned = 0
            };

            if (GameManager.Instance != null)
            {
                GameManager.Instance.PlayerDied(ctx);
            }
            else
            {
                Debug.LogError("[PlayerCombat] GameManager.Instance is null — death not reported.");
            }
        }

        /// <summary>
        /// Drops equipped weapon prefabs at the player's death position so they
        /// can potentially be recovered on a future run. Public so
        /// <see cref="PlayerHealth"/> can invoke it during the death sequence.
        /// </summary>
        public void DropEquipment()
        {
            WeaponBase[] weapons = { slotA, slotC, slotMelee };

            foreach (WeaponBase weapon in weapons)
            {
                if (weapon == null || weapon.Data == null)
                    continue;

                GameObject prefab = weapon.Data.Prefab;
                if (prefab == null)
                    continue;

                // Toss weapons in a small random radius around the death point.
                Vector3 dropPos = transform.position + Random.insideUnitSphere * 0.5f;
                dropPos.y = 0f;

                Instantiate(prefab, dropPos, Quaternion.identity);
            }
        }

        /// <summary>
        /// Determines whether a floor number represents a safe floor where
        /// equipment is preserved on death. Safe floors occur every 5 levels
        /// (5, 10, 15, ..., 50). The lobby (floor 50) is always safe.
        /// </summary>
        /// <param name="floorNumber">The floor to check (1-50).</param>
        /// <returns>True when the floor is a safe zone.</returns>
        private static bool IsSafeFloor(int floorNumber)
        {
            // Every 5th floor and the starting lobby are safe rooms / rest stops.
            return floorNumber % 5 == 0 || floorNumber == 50;
        }

        // ---- Gizmos -------------------------------------------------------------

        private void OnDrawGizmosSelected()
        {
            // Headshot distance ring.
            if (playerAim != null && playerAim.LockTarget != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position, headshotDistanceThreshold);
            }

            // Current weapon slot indicator.
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, Vector3.one * 0.3f);
        }
    }
}
