using UnityEngine;
using EscapeFromWork.Core;
using EscapeFromWork.Data;

namespace EscapeFromWork.Weapons
{
    /// <summary>
    /// Melee weapon supporting instant swings and charge-up heavy attacks.
    /// Uses <see cref="Physics.OverlapSphere"/> for hit detection and filters
    /// targets by the weapon's configured swing arc.
    /// </summary>
    public class MeleeWeapon : WeaponBase
    {
        /// <summary>
        /// Damage multiplier applied when the melee arc is a full 360 degrees.
        /// </summary>
        private const float FullArcDamagePenalty = 0.6f;

        /// <summary>
        /// How long the swing arc visual persists before fading, in seconds.
        /// </summary>
        private const float SwingArcDuration = 0.1f;

        /// <summary>
        /// Number of line segments used to draw the swing arc. More = smoother.
        /// </summary>
        private const int SwingArcSegments = 16;

        /// <summary>
        /// Colour of the swing arc visual.
        /// </summary>
        private static readonly Color SwingArcColor = new Color(1f, 0.85f, 0.2f, 0.7f);

        /// <summary>
        /// Whether the player is currently charging a heavy attack.
        /// </summary>
        private bool _isCharging;

        /// <summary>
        /// Time.unscaledTime when the charge began.
        /// </summary>
        private float _chargeStartTime;

        /// <summary>
        /// True while the player is holding the melee button to charge a heavy attack.
        /// </summary>
        public bool IsCharging => _isCharging;

        /// <summary>
        /// Current charge progress as a 0-1 ratio. 1 means fully charged.
        /// </summary>
        public float ChargeProgress
        {
            get
            {
                if (!_isCharging || data == null || !data.HasChargeAttack)
                    return 0f;

                return Mathf.Clamp01((Time.time - _chargeStartTime) / data.ChargeUpTime);
            }
        }

        // ---- Fire ----------------------------------------------------------------

        /// <inheritdoc />
        public override void Fire(Vector3 from, Vector3 direction, bool isManualAim, bool isHeadshot)
        {
            if (data == null || !data.IsMelee)
                return;

            if (data.HasChargeAttack)
            {
                // Begin charging — the actual swing happens on ReleaseCharge.
                _isCharging = true;
                _chargeStartTime = Time.time;
            }
            else
            {
                // Instant swing at full power.
                PerformSwing(from, direction, 1f);
            }
        }

        // ---- Charge --------------------------------------------------------------

        /// <summary>
        /// Release a charged melee attack. Calculates the power ratio based on
        /// how long the button was held relative to <see cref="WeaponData.ChargeUpTime"/>.
        /// </summary>
        /// <param name="from">World-space origin of the swing.</param>
        /// <param name="direction">Attack direction on the XZ plane.</param>
        public void ReleaseCharge(Vector3 from, Vector3 direction)
        {
            if (!_isCharging || data == null)
                return;

            float chargeDuration = Time.time - _chargeStartTime;
            float powerRatio = Mathf.Clamp01(chargeDuration / data.ChargeUpTime);
            _isCharging = false;

            PerformSwing(from, direction, powerRatio);
        }

        /// <summary>
        /// Cancel an in-progress charge without swinging.
        /// </summary>
        public void CancelCharge()
        {
            _isCharging = false;
        }

        // ---- Swing ---------------------------------------------------------------

        /// <summary>
        /// Execute a melee swing: <see cref="Physics.OverlapSphere"/> search,
        /// arc-angle filtering, damage application.
        /// </summary>
        /// <param name="from">World-space center of the overlap sphere.</param>
        /// <param name="direction">Forward direction for the arc check.</param>
        /// <param name="powerRatio">0-1 ratio scaling damage from 0% (just tapped) to 100% (fully charged).</param>
        private void PerformSwing(Vector3 from, Vector3 direction, float powerRatio)
        {
            float range = data.MeleeRange;
            float arc = data.MeleeArc;
            float damage = data.BaseDamage * powerRatio;

            // A full 360-degree spin deals reduced damage per target.
            if (Mathf.Approximately(arc, 360f))
            {
                damage *= FullArcDamagePenalty;
            }

            Vector3 forward = direction.normalized;
            float halfArc = arc * 0.5f;

            Collider[] hits = Physics.OverlapSphere(from, range);

            foreach (Collider col in hits)
            {
                // Only affect enemy-tagged objects.
                if (!col.CompareTag("Enemy"))
                    continue;

                Vector3 toTarget = (col.transform.position - from);
                toTarget.y = 0f; // Project onto XZ plane for top-down arc check.

                if (toTarget.sqrMagnitude < 0.0001f)
                    continue;

                float angle = Vector3.Angle(forward, toTarget.normalized);

                if (angle <= halfArc)
                {
                    IDamageable damageable = col.GetComponent<IDamageable>();
                    if (damageable != null)
                    {
                        damageable.TakeDamage(damage, gameObject);
                    }
                }
            }

            _lastFireTime = Time.time;

            DrawSwingArc(from, forward, range, arc);
        }

        // ---- Swing arc visual ---------------------------------------------------

        /// <summary>
        /// Draws a temporary arc on the XZ plane using a <see cref="LineRenderer"/>
        /// to show the melee weapon's swing path. The arc self-destructs after
        /// <see cref="SwingArcDuration"/> seconds.
        /// </summary>
        /// <param name="origin">World-space center of the arc.</param>
        /// <param name="facing">Forward direction of the swing on the XZ plane.</param>
        /// <param name="radius">Reach of the weapon.</param>
        /// <param name="arcDegrees">Total arc angle in degrees.</param>
        private void DrawSwingArc(Vector3 origin, Vector3 facing, float radius, float arcDegrees)
        {
            GameObject arcObj = new GameObject("SwingArc");
            arcObj.transform.position = origin;

            LineRenderer lr = arcObj.AddComponent<LineRenderer>();
            lr.startWidth = 0.06f;
            lr.endWidth = 0.02f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = SwingArcColor;
            lr.endColor = new Color(SwingArcColor.r, SwingArcColor.g, SwingArcColor.b, 0f);
            lr.positionCount = SwingArcSegments + 1;
            lr.useWorldSpace = true;

            float halfArc = arcDegrees * 0.5f * Mathf.Deg2Rad;
            float baseAngle = Mathf.Atan2(facing.x, facing.z);
            float startAngle = baseAngle - halfArc;
            float step = (arcDegrees * Mathf.Deg2Rad) / SwingArcSegments;

            for (int i = 0; i <= SwingArcSegments; i++)
            {
                float angle = startAngle + step * i;
                float x = origin.x + Mathf.Sin(angle) * radius;
                float z = origin.z + Mathf.Cos(angle) * radius;
                lr.SetPosition(i, new Vector3(x, origin.y + 0.1f, z));
            }

            Object.Destroy(arcObj, SwingArcDuration);
        }

        // ---- Reload --------------------------------------------------------------

        /// <inheritdoc />
        /// <remarks>Melee weapons do not reload. Stamina is managed externally by the player controller.</remarks>
        public override void Reload()
        {
            // No-op: melee weapons use stamina, which is managed externally.
        }
    }
}
