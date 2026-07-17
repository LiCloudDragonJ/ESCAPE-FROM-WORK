using NUnit.Framework;

namespace EscapeFromWork.Tests.EditMode
{
    /// <summary>
    /// Unit tests for the stamina system as specified in
    /// design/gdd/combat-system.md Formulas §2 and ADR-005.
    /// </summary>
    public class StaminaTest
    {
        private const float MaxStamina = 100f;
        private const float RegenRate = 15f;
        private const float RegenDelay = 0.5f;
        private const float DodgeCost = 25f;
        private const float ManualAimRate = 8f;
        private const float Tolerance = 0.01f;

        /// <summary>
        /// Simulates the StaminaComponent from ADR-005 without MonoBehaviour.
        /// </summary>
        private struct StaminaSim
        {
            public float current;
            public float lastDrainTime;

            public bool Drain(float amount, float time)
            {
                if (current < amount)
                {
                    current = 0f;
                    lastDrainTime = time;
                    return false;
                }
                current -= amount;
                lastDrainTime = time;
                return true;
            }

            public void Tick(float time, float deltaTime)
            {
                if (current >= MaxStamina) return;
                if (time - lastDrainTime < RegenDelay) return;
                current = UnityEngine.Mathf.Min(MaxStamina, current + RegenRate * deltaTime);
            }
        }

        // ---- Drain tests ----------------------------------------------------------

        [Test]
        public void Initial_StaminaIsFull()
        {
            var s = new StaminaSim { current = MaxStamina };
            Assert.AreEqual(MaxStamina, s.current, Tolerance);
        }

        [Test]
        public void Dodge_DrainsCorrectAmount()
        {
            var s = new StaminaSim { current = MaxStamina };
            bool ok = s.Drain(DodgeCost, 1.0f);
            Assert.IsTrue(ok);
            Assert.AreEqual(75f, s.current, Tolerance);
        }

        [Test]
        public void Drain_WhenEmpty_ReturnsFalse()
        {
            var s = new StaminaSim { current = 10f };
            bool ok = s.Drain(DodgeCost, 1.0f); // needs 25, has 10
            Assert.IsFalse(ok);
            Assert.AreEqual(0f, s.current, Tolerance);
        }

        [Test]
        public void Drain_WhenEmpty_ClampsToZero()
        {
            var s = new StaminaSim { current = 10f };
            s.Drain(DodgeCost, 1.0f);
            Assert.AreEqual(0f, s.current, Tolerance);
        }

        // ---- Regen tests ----------------------------------------------------------

        [Test]
        public void Regen_DoesNotStartDuringDelay()
        {
            var s = new StaminaSim { current = 75f };
            s.Drain(0f, 1.0f); // touch lastDrainTime without draining
            s.Tick(1.3f, 0.3f); // 0.3s after drain → still in delay
            Assert.AreEqual(75f, s.current, Tolerance);
        }

        [Test]
        public void Regen_StartsAfterDelay()
        {
            var s = new StaminaSim { current = 75f };
            s.lastDrainTime = 1.0f;
            s.Tick(1.6f, 0.6f); // 0.6s after last drain → delay passed → 0.6s × 15/s = 9.0
            Assert.AreEqual(84.0f, s.current, Tolerance);
        }

        [Test]
        public void Regen_StopsAtMax()
        {
            var s = new StaminaSim { current = 95f };
            s.lastDrainTime = 0f;
            s.Tick(2.0f, 2.0f); // 2s after drain → 1.5s regen = 22.5
            Assert.AreEqual(MaxStamina, s.current, Tolerance);
        }

        [Test]
        public void Regen_OneSecond_Recovers15()
        {
            var s = new StaminaSim { current = 50f };
            s.lastDrainTime = 1.0f;
            s.Tick(2.0f, 1.0f); // 1s elapsed since drain → delay passed → 1s × 15/s = 15
            Assert.AreEqual(65.0f, s.current, Tolerance);
        }

        // ---- Manual aim drain -----------------------------------------------------

        [Test]
        public void ManualAim_DrainsPerSecond()
        {
            var s = new StaminaSim { current = MaxStamina };
            // Simulate holding RMB for 3 seconds.
            for (float t = 0; t < 3f; t += 0.1f)
            {
                s.Drain(ManualAimRate * 0.1f, t);
            }
            // 3s × 8/s = 24 drained
            Assert.AreEqual(76f, s.current, 1f); // ~1f tolerance for float accumulation
        }

        // ---- Sequence tests -------------------------------------------------------

        [Test]
        public void TwoDodges_ThenRegen_ProducesExpectedValue()
        {
            var s = new StaminaSim { current = MaxStamina };

            // Dodge 1 at t=0
            s.Drain(DodgeCost, 0f);
            Assert.AreEqual(75f, s.current, Tolerance);

            // Dodge 2 at t=0
            s.Drain(DodgeCost, 0f);
            Assert.AreEqual(50f, s.current, Tolerance);

            // Regen: 2.5s elapsed since last drain → delay passed → 2.5s × 15 = 37.5
            s.Tick(2.5f, 2.5f);
            Assert.AreEqual(87.5f, s.current, Tolerance);
        }

        // ---- Per-weapon melee stamina (ADR-005: weapon-defined costs) --------------

        [Test]
        public void KPIHammer_HeavyCost_IsCorrect()
        {
            // GDD weapon-system.md: KPI报表锤 heavy stamina = 35
            const float kpiHammerHeavyCost = 35f;
            var s = new StaminaSim { current = MaxStamina };
            s.Drain(kpiHammerHeavyCost, 0f);
            Assert.AreEqual(65f, s.current, Tolerance);
        }

        [Test]
        public void KeyboardBrick_LightCost_IsCorrect()
        {
            // GDD weapon-system.md: 键盘板砖 light stamina = 10
            const float keyboardBrickLightCost = 10f;
            var s = new StaminaSim { current = MaxStamina };
            s.Drain(keyboardBrickLightCost, 0f);
            Assert.AreEqual(90f, s.current, Tolerance);
        }

        // ---- Boundary values -------------------------------------------------------

        [Test]
        public void FourDodges_ExactlyExhaustsStamina()
        {
            // 4 × 25 = 100 exactly
            var s = new StaminaSim { current = MaxStamina };
            s.Drain(DodgeCost, 0f);
            s.Drain(DodgeCost, 0f);
            s.Drain(DodgeCost, 0f);
            bool ok = s.Drain(DodgeCost, 0f);
            Assert.IsTrue(ok);
            Assert.AreEqual(0f, s.current, Tolerance);
        }

        [Test]
        public void FifthDodge_WhenEmpty_Fails()
        {
            var s = new StaminaSim { current = 0f };
            bool ok = s.Drain(DodgeCost, 0f);
            Assert.IsFalse(ok);
        }
    }
}
