using UnityEngine;
using EscapeFromWork.Data;

namespace EscapeFromWork.Weapons
{
    /// <summary>
    /// Ranged weapon (Type-A or Type-C). Spawns a <see cref="Projectile"/> prefab
    /// from a muzzle point, applies spread (reduced by half during manual aim),
    /// awards bonus headshot damage, and decrements ammo.
    /// </summary>
    public class RangedWeapon : WeaponBase
    {
        /// <summary>
        /// Prefab instantiated each time the weapon fires. Must carry a
        /// <see cref="Projectile"/> component at its root.
        /// </summary>
        [SerializeField] private GameObject projectilePrefab;

        /// <summary>
        /// World-space transform from which projectiles spawn.
        /// If null, the weapon's own position is used.
        /// </summary>
        [SerializeField] private Transform muzzlePoint;

        /// <summary>
        /// Multiplier applied to base damage when a headshot is confirmed.
        /// </summary>
        private const float HeadshotDamageMultiplier = 1.5f;

        // ---- Fire ----------------------------------------------------------------

        /// <inheritdoc />
        public override void Fire(Vector3 from, Vector3 direction, bool isManualAim, bool isHeadshot)
        {
            if (!CanFire())
                return;

            if (projectilePrefab == null)
            {
                Debug.LogWarning($"[RangedWeapon] {name}: no projectilePrefab assigned.");
                return;
            }

            // Record fire time for rate-of-fire gating.
            _lastFireTime = Time.time;

            // Spread: manual aim halves the weapon's base spread cone.
            float effectiveSpread = data.Spread * (isManualAim ? 0.5f : 1.0f);
            Vector3 fireDirection = ApplySpread(direction, effectiveSpread);

            // Spawn point.
            Vector3 spawnPosition = muzzlePoint != null ? muzzlePoint.position : from;

            // Instantiate the projectile.
            GameObject projectileObj = Instantiate(projectilePrefab, spawnPosition,
                Quaternion.LookRotation(fireDirection));

            Projectile projectile = projectileObj.GetComponent<Projectile>();
            if (projectile != null)
            {
                float finalDamage = data.BaseDamage;
                if (isHeadshot && data.HasHeadshotBonus)
                {
                    finalDamage *= HeadshotDamageMultiplier;
                }

                bool isTypeC = data.WeaponClass == WeaponClass.TypeC;
                projectile.Initialize(fireDirection, finalDamage, data.Range, isTypeC, data.SpecialEffect);
            }
            else
            {
                Debug.LogWarning($"[RangedWeapon] {name}: projectilePrefab is missing a Projectile component.");
            }

            // Consume one round.
            currentAmmo--;
        }

        // ---- Reload --------------------------------------------------------------

        /// <inheritdoc />
        public override void Reload()
        {
            if (data == null)
                return;

            // Don't reload if already full or already reloading.
            if (currentAmmo >= data.MagazineSize)
                return;

            if (_isReloading)
                return;

            // Begin reload timer.
            _isReloading = true;
            _reloadTimer = data.ReloadTime;
        }

        /// <inheritdoc />
        protected override void CompleteReload()
        {
            base.CompleteReload();
            currentAmmo = data.MagazineSize;
        }

        // ---- Spread --------------------------------------------------------------

        /// <summary>
        /// Rotates <paramref name="direction"/> by a random yaw angle within
        /// <c>[-spreadRadians, +spreadRadians]</c> to create a shot cone.
        /// Spread is applied around the world Y axis (top-down XZ plane).
        /// </summary>
        /// <param name="direction">Base aim direction (normalized).</param>
        /// <param name="spreadRadians">Half-angle of the spread cone in radians.
        /// 0 = pinpoint, ~1.0 = very wide (~57 deg half-angle).</param>
        /// <returns>A new normalized direction with spread applied.</returns>
        private static Vector3 ApplySpread(Vector3 direction, float spreadRadians)
        {
            if (spreadRadians <= 0f)
                return direction.normalized;

            float angleDegrees = Random.Range(-spreadRadians, spreadRadians) * Mathf.Rad2Deg;
            Quaternion yawRotation = Quaternion.Euler(0f, angleDegrees, 0f);
            return (yawRotation * direction).normalized;
        }
    }
}
