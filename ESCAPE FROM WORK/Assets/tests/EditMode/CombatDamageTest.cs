using NUnit.Framework;

namespace EscapeFromWork.Tests.EditMode
{
    /// <summary>
    /// Unit tests for the combat damage formula as specified in
    /// design/gdd/combat-system.md Formulas §1.
    /// </summary>
    public class CombatDamageTest
    {
        private const float HeadshotMultiplier = 1.5f;
        private const float CoverMultiplier = 0.6f;
        private const float Tolerance = 0.001f;

        /// <summary>
        /// Pure function implementing the damage formula from ADR-004.
        /// Mirrors PlayerHealth.TakeDamage logic without MonoBehaviour dependencies.
        /// </summary>
        private static float CalculateDamage(
            float baseDamage,
            bool isHeadshot,
            bool isInCover,
            bool ignoresCover = false)
        {
            float dmg = baseDamage;

            if (isHeadshot)
                dmg *= HeadshotMultiplier;

            if (isInCover && !ignoresCover)
                dmg *= CoverMultiplier;

            return UnityEngine.Mathf.Max(1f, dmg);
        }

        // ---- Body shot (baseline) ------------------------------------------------

        [Test]
        public void BaseDamage_BodyShot_NoCover_ReturnsUnmodified()
        {
            float result = CalculateDamage(15f, false, false);
            Assert.AreEqual(15f, result, Tolerance);
        }

        // ---- Headshot -------------------------------------------------------------

        [Test]
        public void Headshot_AppliesMultiplier()
        {
            float result = CalculateDamage(15f, true, false);
            Assert.AreEqual(22.5f, result, Tolerance); // 15 × 1.5
        }

        [Test]
        public void Headshot_WithCover_AppliesBothMultipliers()
        {
            float result = CalculateDamage(15f, true, true);
            Assert.AreEqual(13.5f, result, Tolerance); // 15 × 1.5 × 0.6
        }

        // ---- Cover ----------------------------------------------------------------

        [Test]
        public void Cover_ReducesDamage()
        {
            float result = CalculateDamage(10f, false, true);
            Assert.AreEqual(6f, result, Tolerance); // 10 × 0.6
        }

        [Test]
        public void BeamWeapon_IgnoresCover()
        {
            float result = CalculateDamage(25f, false, true, ignoresCover: true);
            Assert.AreEqual(25f, result, Tolerance); // cover ignored
        }

        [Test]
        public void AOE_IgnoresCover()
        {
            float result = CalculateDamage(60f, false, true, ignoresCover: true);
            Assert.AreEqual(60f, result, Tolerance);
        }

        // ---- Edge cases -----------------------------------------------------------

        [Test]
        public void ZeroDamage_ClampsToOne_Minimum()
        {
            float result = CalculateDamage(0f, false, true);
            Assert.AreEqual(1f, result, Tolerance); // minimum 1 damage
        }

        [Test]
        public void VerySmallDamage_ClampsToOne()
        {
            float result = CalculateDamage(0.5f, false, true);
            Assert.AreEqual(1f, result, Tolerance); // 0.5 × 0.6 = 0.3 → clamped to 1
        }

        [Test]
        public void HighDamage_StaplerPistol_Headshot()
        {
            // StaplerPistol: baseDamage=15, headshot=22.5 (per GDD example)
            float result = CalculateDamage(15f, true, false);
            Assert.AreEqual(22.5f, result, Tolerance);
        }

        [Test]
        public void HighDamage_KPIHammer_FullCharge()
        {
            // KPI报表锤: full charge heavy damage = 80
            float result = CalculateDamage(80f, false, false);
            Assert.AreEqual(80f, result, Tolerance);
        }

        [Test]
        public void BeamWeapon_Headshot_InCover()
        {
            // Projector ray gun: base=25/s, headshot, beam ignores cover
            float result = CalculateDamage(25f, true, true, ignoresCover: true);
            Assert.AreEqual(37.5f, result, Tolerance); // 25 × 1.5, cover ignored
        }

        // ---- Boundary values from GDD Tuning Knobs ---------------------------------

        [Test]
        public void Boundary_MinimumWeaponDamage_Headshot()
        {
            // GDD tuning: baseDamage range 5–200
            float result = CalculateDamage(5f, true, false);
            Assert.AreEqual(7.5f, result, Tolerance);
        }

        [Test]
        public void Boundary_MaximumWeaponDamage_Headshot_Cover()
        {
            float result = CalculateDamage(200f, true, true);
            Assert.AreEqual(180f, result, Tolerance); // 200 × 1.5 × 0.6
        }
    }
}
