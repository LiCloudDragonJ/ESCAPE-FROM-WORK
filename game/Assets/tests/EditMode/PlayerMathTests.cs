using NUnit.Framework;
using UnityEngine;
using EscapeFromWork.Gameplay;

namespace EscapeFromWork.Tests
{
    public class PlayerMathTests
    {
        [Test]
        public void JumpVelocity_MatchesHeightFormula()
        {
            float v = PlayerMath.JumpVelocity(1.05f, -24f);
            Assert.That(v, Is.EqualTo(Mathf.Sqrt(2f * 1.05f * 24f)).Within(1e-3f));
        }

        [Test]
        public void JumpVelocity_ZeroHeightIsZero()
        {
            Assert.That(PlayerMath.JumpVelocity(0f, -24f), Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void Damp_ReachesTargetAfterEnoughTime()
        {
            float value = 0f;
            for (int i = 0; i < 200; i++) value = PlayerMath.Damp(value, 100f, 12f, 0.02f);
            Assert.That(value, Is.EqualTo(100f).Within(0.1f));
        }

        [Test]
        public void Damp_IsFrameRateIndependent()
        {
            float a = 0f;
            for (int i = 0; i < 100; i++) a = PlayerMath.Damp(a, 100f, 8f, 0.01f);

            float b = 0f;
            for (int i = 0; i < 200; i++) b = PlayerMath.Damp(b, 100f, 8f, 0.005f);

            Assert.That(a, Is.EqualTo(b).Within(0.5f));
        }

        [Test]
        public void ClampPitch_ClampsBothSides()
        {
            Assert.That(PlayerMath.ClampPitch(120f, 85f), Is.EqualTo(85f));
            Assert.That(PlayerMath.ClampPitch(-120f, 85f), Is.EqualTo(-85f));
            Assert.That(PlayerMath.ClampPitch(30f, 85f), Is.EqualTo(30f));
        }

        [Test]
        public void ClampHorizontal_KeepsVerticalAndLimitsFlatSpeed()
        {
            Vector3 result = PlayerMath.ClampHorizontal(new Vector3(30f, -5f, 40f), 7f);
            Assert.That(new Vector3(result.x, 0f, result.z).magnitude, Is.EqualTo(7f).Within(1e-3f));
            Assert.That(result.y, Is.EqualTo(-5f));
        }
    }
}
