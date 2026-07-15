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
        /// Reload the weapon. Ranged weapons refill the magazine; melee weapons
        /// are a no-op (stamina management is external).
        /// </summary>
        public abstract void Reload();
    }
}
