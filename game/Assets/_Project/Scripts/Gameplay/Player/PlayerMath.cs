using UnityEngine;

namespace EscapeFromWork.Gameplay
{
    /// Pure math used by the controller, kept static so EditMode tests can hit it.
    public static class PlayerMath
    {
        /// v = sqrt(2 * h * |g|)
        public static float JumpVelocity(float height, float gravity)
        {
            return Mathf.Sqrt(2f * Mathf.Max(0f, height) * Mathf.Abs(gravity));
        }

        /// Frame-rate independent exponential damping.
        public static float Damp(float current, float target, float lambda, float deltaTime)
        {
            float k = 1f - Mathf.Exp(-Mathf.Max(0f, lambda) * Mathf.Max(0f, deltaTime));
            return Mathf.Lerp(current, target, k);
        }

        public static float ClampPitch(float pitch, float maxPitch)
        {
            return Mathf.Clamp(pitch, -Mathf.Abs(maxPitch), Mathf.Abs(maxPitch));
        }

        /// Moves current toward target by at most maxDelta (frame-rate independent when fed delta*speed).
        public static float Approach(float current, float target, float maxDelta)
        {
            if (maxDelta <= 0f) return current;
            return Mathf.MoveTowards(current, target, maxDelta);
        }

        /// Vertical head-bob offset for a phase in radians; never exceeds amplitude.
        public static float BobOffset(float phase, float amplitude)
        {
            return Mathf.Sin(phase) * Mathf.Abs(amplitude);
        }

        /// Camera dip on landing: fallSpeed is negative when falling, dip is positive and capped.
        public static float LandingDip(float fallSpeed, float scale, float maxDip)
        {
            float dip = -fallSpeed * Mathf.Max(0f, scale);
            return Mathf.Clamp(dip, 0f, Mathf.Max(0f, maxDip));
        }

        public static Vector3 ClampHorizontal(Vector3 velocity, float maxSpeed)
        {
            Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
            if (flat.magnitude <= maxSpeed) return velocity;
            flat = flat.normalized * maxSpeed;
            return new Vector3(flat.x, velocity.y, flat.z);
        }
    }
}
