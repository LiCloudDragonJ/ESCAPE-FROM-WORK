using UnityEngine;
using EscapeFromWork.Core;
using EscapeFromWork.Data;
using EscapeFromWork.Weapons;

namespace EscapeFromWork.Player
{
    /// <summary>
    /// Central player combat controller. Manages three weapon slots
    /// (A / C / Melee), shooting, melee (tap-instant / hold-charge), weapon
    /// cycling, and reloading.
    ///
    /// <para>Health is owned by <see cref="PlayerHealth"/>; this component
    /// forwards damage to it and exposes health properties for HUD binding.</para>
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class PlayerCombat : MonoBehaviour
    {
        // ---- Health --------------------------------------------------------------

        [Header("Health")]
        [Tooltip("Reference to the PlayerHealth component (sole IDamageable authority).")]
        [SerializeField] private PlayerHealth playerHealth;

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
        public WeaponBase SlotA => slotA;
        public WeaponBase SlotC => slotC;
        public WeaponBase SlotMelee => slotMelee;
        public ItemData SlotArmor => slotArmor;
        public ItemData SlotBackpack => slotBackpack;

        public void ClearSlot(GearSlot s)
        {
            switch (s) { case GearSlot.WeaponA: slotA = null; break; case GearSlot.WeaponC: slotC = null; break; case GearSlot.Melee: slotMelee = null; _currentMelee = null; break; case GearSlot.Armor: slotArmor = null; break; case GearSlot.Backpack: slotBackpack = null; break; }
        }

        public void SetSlotItem(GearSlot s, ItemData item)
        {
            switch (s) { case GearSlot.Armor: slotArmor = item; break; case GearSlot.Backpack: slotBackpack = item; break; }
        }

        public void SetSlotWeapon(WeaponBase w)
        {
            if (w?.Data == null) return;
            switch (w.Data.Slot) { case WeaponSlot.A: slotA = w; break; case WeaponSlot.C: slotC = w; break; case WeaponSlot.Melee: slotMelee = w; _currentMelee = w as MeleeWeapon; break; }
        }

        [Tooltip("Slot C — creative / special-effect weapon.")]
        [SerializeField] private WeaponBase slotC;

        [Tooltip("Melee slot — always available.")]
        [SerializeField] private WeaponBase slotMelee;

        [Tooltip("Armor item slot — damage reduction.")]
        [SerializeField] private ItemData slotArmor;

        [Tooltip("Backpack item slot — determines inventory capacity.")]
        [SerializeField] private ItemData slotBackpack;

        // ---- Private state -------------------------------------------------------

        /// <summary>0 = Slot A, 1 = Slot C, 2 = Melee.</summary>
        private int _currentSlotIndex;

        /// <summary>Cached reference to the melee weapon for charge checking.</summary>
        private MeleeWeapon _currentMelee;

        // ---- Public properties ---------------------------------------------------

        /// <summary>
        /// Maximum health pool, forwarded from <see cref="PlayerHealth"/>.
        /// </summary>
        public float MaxHealth => playerHealth != null ? playerHealth.MaxHealth : 100f;

        /// <summary>
        /// Current health value, forwarded from <see cref="PlayerHealth"/>.
        /// </summary>
        public float CurrentHealth => playerHealth != null ? playerHealth.CurrentHealth : 100f;

        /// <summary>
        /// The weapon in the currently active slot, or null if the slot is empty.
        /// </summary>
        public WeaponBase CurrentWeapon => GetWeaponInSlot(_currentSlotIndex);

        /// <summary>
        /// True when the player is dead, forwarded from <see cref="PlayerHealth"/>.
        /// </summary>
        public bool IsDead => playerHealth != null && playerHealth.IsDead;

        // ---- Unity lifecycle ----------------------------------------------------

        private void Awake()
        {
            // Auto-wire references if not set in the inspector.
            if (playerAim == null)
            {
                playerAim = GetComponent<PlayerAim>();
                if (playerAim == null)
                    Debug.LogWarning("[PlayerCombat] No PlayerAim found — aiming will use mouse fallback.", this);
            }

            if (playerHealth == null)
            {
                playerHealth = GetComponent<PlayerHealth>();
                if (playerHealth == null)
                    Debug.LogWarning("[PlayerCombat] No PlayerHealth found — damage forwarding disabled.", this);
            }
        }

        private void Update()
        {
            // Disable combat input when loot UI is open
            var lootUI = FindObjectOfType<EscapeFromWork.UI.LootContainerUI>();
            if (lootUI != null && lootUI.IsOpen) return;

            PollShootInput();
            PollMeleeInput();
            PollSwapWeaponInput();
            PollReloadInput();
        }

        // ---- Damage forwarding ---------------------------------------------------

        /// <summary>
        /// Forwards damage to <see cref="PlayerHealth"/>, the sole health authority.
        /// Kept for any external code that still calls PlayerCombat.TakeDamage().
        /// </summary>
        public void TakeDamage(float amount, GameObject source)
        {
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(amount, source);
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
        /// Returns the world-space aim direction. Third-person: uses screen-center
        /// crosshair raycast — shoot where you're looking.
        /// </summary>
        private Vector3 GetAimDirection()
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                // Screen center crosshair.
                Ray ray = new Ray(cam.transform.position, cam.transform.forward);
                Plane ground = new Plane(Vector3.up, Vector3.zero);
                if (ground.Raycast(ray, out float enter))
                {
                    Vector3 hitPoint = ray.GetPoint(enter);
                    Vector3 dir = hitPoint - transform.position;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.001f)
                        return dir.normalized;
                }
            }

            // Fallback: player's facing direction.
            Vector3 fwd = transform.forward; fwd.y = 0;
            return fwd.sqrMagnitude > 0.001f ? fwd.normalized : Vector3.forward;
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
        /// Drops equipped weapon prefabs at the player's death position so they
        /// can potentially be recovered on a future run. Called by
        /// <see cref="PlayerHealth"/> during the death sequence.
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
