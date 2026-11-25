using NUnit.Framework;
using UnityEngine;
using EscapeFromWork.Gameplay;

namespace EscapeFromWork.Tests.EditMode
{
    public class PlayerFeelTests
    {
        [Test]
        public void Approach_ConvergesAndClamps()
        {
            Assert.AreEqual(1.5f, PlayerMath.Approach(0f, 3f, 1.5f), 0.0001f);
            Assert.AreEqual(3f, PlayerMath.Approach(2.9f, 3f, 1f), 0.0001f);
            Assert.AreEqual(2f, PlayerMath.Approach(2f, 0f, 0f), 0.0001f);
        }

        [Test]
        public void BobOffset_StaysWithinAmplitude()
        {
            for (int i = 0; i < 64; i++)
            {
                float v = PlayerMath.BobOffset(i * 0.37f, 0.04f);
                Assert.LessOrEqual(Mathf.Abs(v), 0.04f + 0.0001f);
            }
            Assert.AreEqual(0f, PlayerMath.BobOffset(0f, 0.04f), 0.0001f);
        }

        [Test]
        public void LandingDip_ScalesWithFallSpeedAndCaps()
        {
            Assert.AreEqual(0f, PlayerMath.LandingDip(0f, 0.014f, 0.14f), 0.0001f);
            Assert.AreEqual(0.014f, PlayerMath.LandingDip(-1f, 0.014f, 0.14f), 0.0001f);
            Assert.AreEqual(0.14f, PlayerMath.LandingDip(-50f, 0.014f, 0.14f), 0.0001f);
        }

        [Test]
        public void JumpVelocity_ReachesRequestedHeight()
        {
            float gravity = -26f;
            float v = PlayerMath.JumpVelocity(1f, gravity);
            float apex = v * v / (2f * Mathf.Abs(gravity));
            Assert.AreEqual(1f, apex, 0.01f);
        }
    }
}
