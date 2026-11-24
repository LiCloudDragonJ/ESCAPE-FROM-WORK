using UnityEngine;

namespace EscapeFromWork.Core
{
    /// <summary>
    /// Spawns a short-lived floating damage number above a world position.
    /// Uses Unity's built-in TextMesh (3D text) — no TextMeshPro dependency.
    /// </summary>
    public static class FloatingDamageText
    {
        private static readonly Color DamageColor = new Color(1f, 0.85f, 0.2f);
        private const float FloatSpeed = 2.5f;
        private const float Lifetime = 1.0f;
        private const float FadeStart = 0.4f; // when fading begins as fraction of lifetime

        /// <summary>
        /// Spawn a floating damage number at the given world position.
        /// </summary>
        public static void Spawn(Vector3 worldPosition, float damage)
        {
            GameObject textObj = new GameObject("DamageText");
            textObj.transform.position = worldPosition + Vector3.up * 2.5f;

            TextMesh tm = textObj.AddComponent<TextMesh>();
            tm.text = Mathf.RoundToInt(damage).ToString();
            tm.fontSize = 48;
            tm.characterSize = 0.15f;
            tm.color = DamageColor;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.fontStyle = FontStyle.Bold;

            // Face the camera.
            Camera cam = Camera.main;
            if (cam != null)
            {
                textObj.transform.rotation = cam.transform.rotation;
            }

            textObj.AddComponent<FloatingTextBehaviour>().Init(FloatSpeed, Lifetime, FadeStart);
        }

        /// <summary>
        /// Drives the float-up-and-fade animation, then self-destructs.
        /// </summary>
        private class FloatingTextBehaviour : MonoBehaviour
        {
            private float _floatSpeed;
            private float _lifetime;
            private float _fadeStart;
            private float _elapsed;
            private TextMesh _tm;

            public void Init(float floatSpeed, float lifetime, float fadeStart)
            {
                _floatSpeed = floatSpeed;
                _lifetime = lifetime;
                _fadeStart = fadeStart;
                _tm = GetComponent<TextMesh>();
            }

            private void Update()
            {
                _elapsed += Time.deltaTime;

                // Float upward.
                transform.position += Vector3.up * _floatSpeed * Time.deltaTime;

                // Fade out near the end.
                if (_tm != null && _elapsed > _lifetime * _fadeStart)
                {
                    float fadeT = (_elapsed - _lifetime * _fadeStart) / (_lifetime * (1f - _fadeStart));
                    Color c = _tm.color;
                    c.a = 1f - Mathf.Clamp01(fadeT);
                    _tm.color = c;
                }

                if (_elapsed >= _lifetime)
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
