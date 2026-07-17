using UnityEngine;
using EscapeFromWork.Core;
using EscapeFromWork.Data;
using EscapeFromWork.Weapons;

namespace EscapeFromWork.Player
{
    /// <summary>
    /// Central player combat controller. Manages three weapon slots
    /// (A / C / Melee), shooting, melee (V quick-melee / LMB when melee-slot),
    /// weapon cycling, reloading, and the stamina resource system.
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

        // ---- Stamina -------------------------------------------------------------

        [Header("Stamina")]
        [Tooltip("Maximum stamina pool.")]
        [SerializeField] private float maxStamina = 100f;

        [Tooltip("Stamina recovered per second after the regen delay.")]
        [SerializeField] private float staminaRegenRate = 15f;

        [Tooltip("Seconds after last stamina drain before regen begins.")]
        [SerializeField] private float staminaRegenDelay = 0.5f;

        [Tooltip("Stamina drained per dodge.")]
        [SerializeField] private float dodgeStaminaCost = 25f;

        [Tooltip("Stamina drained per second while holding manual aim.")]
        [SerializeField] private float manualAimStaminaRate = 8f;

        /// <summary>Current stamina value.</summary>
        private float _currentStamina;

        /// <summary>Time.unscaledTime of the last stamina drain.</summary>
        private float _lastStaminaDrainTime = float.MinValue;

        /// <summary>
        /// Current stamina (0–maxStamina). Read by HUD.
        /// </summary>
        public float CurrentStamina => _currentStamina;

        /// <summary>
        /// Maximum stamina pool. Read by HUD.
        /// </summary>
        public float MaxStamina => maxStamina;

        /// <summary>
        /// True when stamina is fully depleted — blocks dodge, melee, manual aim.
        /// </summary>
        public bool IsStaminaEmpty => _currentStamina <= 0f;

        /// <summary>Stamina cost of a dodge (read by PlayerController for TryDodge).</summary>
        public float DodgeStaminaCost => dodgeStaminaCost;

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
            _currentStamina = maxStamina;

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

            TickStamina();
            PollShootInput();
            PollQuickMeleeInput();
            PollSwapWeaponInput();
            PollReloadInput();
        }

        // ---- Stamina --------------------------------------------------------------

        /// <summary>
        /// Apply stamina regen each frame. Drain is applied immediately by action
        /// methods. Manual aim drain is continuous (per-frame while holding RMB).
        /// </summary>
        private void TickStamina()
        {
            // Continuous drain: manual aim costs stamina per second.
            if (playerAim != null && playerAim.IsManualAim && _currentStamina > 0f)
            {
                DrainStamina(manualAimStaminaRate * Time.deltaTime);
                if (_currentStamina <= 0f)
                {
                    // Force exit manual aim when stamina runs out (GDD edge case #8).
                    // PlayerAim polls RMB each frame, but with no stamina it can't sustain.
                }
            }

            // Regen: only after the delay has elapsed.
            if (_currentStamina < maxStamina && Time.time - _lastStaminaDrainTime >= staminaRegenDelay)
            {
                _currentStamina = Mathf.Min(maxStamina, _currentStamina + staminaRegenRate * Time.deltaTime);
            }
        }

        /// <summary>
        /// Consume stamina for an action. Returns true if sufficient stamina was available.
        /// If insufficient, drains to 0 and returns false (action should be blocked).
        /// </summary>
        /// <param name="amount">Stamina to drain.</param>
        /// <returns>True if the full amount was available and consumed.</returns>
        public bool DrainStamina(float amount)
        {
            if (amount <= 0f) return true;

            _lastStaminaDrainTime = Time.time;

            if (_currentStamina >= amount)
            {
                _currentStamina -= amount;
                return true;
            }

            _currentStamina = 0f;
            return false;
        }

        /// <summary>
        /// Returns true if the player has enough stamina for an action without
        /// actually consuming it. Used for input gating (block before drain).
        /// </summary>
        public bool HasStamina(float amount)
        {
            return _currentStamina >= amount;
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
        /// Fire the current weapon (LMB). When melee slot is active, LMB triggers
        /// melee light/charge/heavy instead of shooting.
        /// </summary>
        private void PollShootInput()
        {
            if (IsDead)
                return;

            WeaponBase weapon = CurrentWeapon;
            if (weapon == null)
                return;

            Vector3 from = transform.position;
            Vector3 direction = GetAimDirection();
            bool isManualAim = playerAim != null && playerAim.IsManualAim;

            // Melee-slot LMB: light tap or hold-to-charge.
            if (weapon.Data != null && weapon.Data.IsMelee)
            {
                MeleeWeapon melee = weapon as MeleeWeapon;
                if (melee == null) return;

                if (Input.GetMouseButtonDown(0))
                {
                    // Check stamina before starting melee.
                    float cost = melee.Data != null ? melee.Data.MeleeLightStaminaCost : 15f;
                    if (!HasStamina(cost)) return;
                    DrainStamina(cost);
                    melee.Fire(from, direction, false, false);
                }
                else if (Input.GetMouseButtonUp(0))
                {
                    if (melee.IsCharging)
                    {
                        melee.ReleaseCharge(from, direction);
                    }
                }
                return;
            }

            // Ranged: LMB single-shot.
            if (!Input.GetMouseButtonDown(0))
                return;

            bool isHeadshot = DetermineHeadshot();
            weapon.Fire(from, direction, isManualAim, isHeadshot);
        }

        // ---- Quick Melee (V key) --------------------------------------------------

        /// <summary>
        /// V-key quick melee: instant light attack with the melee-slot weapon
        /// without switching away from the current ranged slot.
        /// </summary>
        private void PollQuickMeleeInput()
        {
            if (!Input.GetKeyDown(KeyCode.V) || IsDead)
                return;

            MeleeWeapon melee = slotMelee as MeleeWeapon;
            if (melee == null)
                return;

            float cost = melee.Data != null ? melee.Data.MeleeLightStaminaCost : 15f;
            if (!HasStamina(cost)) return;

            DrainStamina(cost);

            Vector3 from = transform.position;
            Vector3 direction = GetAimDirection();
            melee.Fire(from, direction, false, false);
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
