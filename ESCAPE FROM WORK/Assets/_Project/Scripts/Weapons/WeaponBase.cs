using UnityEngine;
using EscapeFromWork.Data;

namespace EscapeFromWork.Weapons
{
    /// <summary>
    /// Abstract base class for all wieldable weapons in the game.
    /// Provides shared state (ammo, fire-rate gating, data reference) and
    /// defines the contract that ranged, melee, and special weapons must fulfill.
    /// </summary>
    public abstract class WeaponBase : MonoBehaviour
    {
        /// <summary>
        /// The ScriptableObject asset defining this weapon's identity and combat stats.
        /// </summary>
        [SerializeField] protected WeaponData data;

        /// <summary>
        /// Rounds currently loaded in the magazine. Melee weapons ignore this value.
        /// </summary>
        [SerializeField] protected int currentAmmo;

        /// <summary>
        /// Time.unscaledTime of the last Fire call. Used to enforce fire-rate gating.
        /// Initialized to <see cref="float.MinValue"/> so the weapon is ready immediately.
        /// </summary>
        protected float _lastFireTime = float.MinValue;

        // ---- Public properties ---------------------------------------------------

        /// <summary>
        /// The weapon data asset that defines this weapon's stats.
        /// </summary>
        public WeaponData Data => data;

        /// <summary>
        /// Current rounds in the magazine. Always returns 0 for melee weapons.
        /// </summary>
        public int CurrentAmmo => currentAmmo;

        // ---- Reload state ---------------------------------------------------------

        /// <summary>True while a reload is in progress.</summary>
        protected bool _isReloading;

        /// <summary>Countdown timer for the current reload.</summary>
        protected float _reloadTimer;

        /// <summary>
        /// True when the weapon is currently reloading (HUD display gating).
        /// </summary>
        public bool IsReloading => _isReloading;

        /// <summary>
        /// Progress of the current reload (0–1). Used by HUD for the circular
        /// reload indicator.
        /// </summary>
        public float ReloadProgress
        {
            get
            {
                if (!_isReloading || data == null || data.ReloadTime <= 0f) return 0f;
                return 1f - (_reloadTimer / data.ReloadTime);
            }
        }

        // ---- Initialization ------------------------------------------------------

        /// <summary>
        /// Bind this weapon instance to a <see cref="WeaponData"/> asset and
        /// fill the magazine to capacity.
        /// </summary>
        /// <param name="weaponData">The data asset to bind.</param>
        public virtual void Initialize(WeaponData weaponData)
        {
            data = weaponData;
            currentAmmo = weaponData != null ? weaponData.MagazineSize : 0;
            _lastFireTime = float.MinValue;
        }

        // ---- Fire-rate gating ----------------------------------------------------

        /// <summary>
        /// Returns true when the weapon is ready to fire:
        /// has valid data, has ammo (ranged), and the fire-rate cooldown has elapsed.
        /// Melee weapons always pass the ammo check but fire rate is handled externally.
        /// </summary>
        public virtual bool CanFire()
        {
            if (data == null)
                return false;

            // Melee weapons don't consume ammo or obey fire rate here.
            if (data.IsMelee)
                return true;

            if (currentAmmo <= 0)
                return false;

            float effectiveFireRate = data.EffectiveFireRate;
            if (effectiveFireRate <= 0f)
                return false;

            float cooldown = 1f / effectiveFireRate;
            return Time.time - _lastFireTime >= cooldown;
        }

        // ---- Reload lifecycle -----------------------------------------------------

        /// <summary>
        /// Ticks the reload timer. Subclasses that override Update must call
        /// base.Update() to keep reload timing.
        /// </summary>
        protected virtual void Update()
        {
            if (!_isReloading) return;

            _reloadTimer -= Time.deltaTime;
            if (_reloadTimer <= 0f)
            {
                CompleteReload();
            }
        }

        /// <summary>
        /// Called when the reload timer expires. Subclasses override to apply
        /// weapon-specific reload logic (fill magazine from reserve etc.).
        /// </summary>
        protected virtual void CompleteReload()
        {
            _isReloading = false;
            _reloadTimer = 0f;
        }

        /// <summary>
        /// Interrupt an in-progress reload. Called when the player dodges or
        /// switches weapons. Partially loaded rounds are preserved.
        /// </summary>
        public virtual void CancelReload()
        {
            if (!_isReloading) return;
            _isReloading = false;
            _reloadTimer = 0f;
        }

        // ---- Abstract contract ---------------------------------------------------

        /// <summary>
        /// Fire the weapon from a world-space origin in the given direction.
        /// Subclasses implement weapon-specific projectile spawning, melee swing,
        /// or special-effect logic.
        /// </summary>
        /// <param name="from">World-space origin of the attack (typically the muzzle or player position).</param>
        /// <param name="direction">Normalized world-space direction of the attack on the XZ plane.</param>
        /// <param name="isManualAim">True when the player is manually aiming (right mouse held), which reduces spread.</param>
        /// <param name="isHeadshot">True when the shot qualifies for bonus headshot damage.</param>
        public abstract void Fire(Vector3 from, Vector3 direction, bool isManualAim, bool isHeadshot);

        /// <summary>
        /// Reload the weapon. Ranged weapons refill the magazine from reserve ammo;
        /// melee weapons are a no-op (stamina management is external).
        /// </summary>
        public abstract void Reload();
    }
}
