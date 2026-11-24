using UnityEngine;
using UnityEngine.InputSystem;

namespace EscapeFromWork.Gameplay
{
    /// Minimal FPS/ms readout so 1080p60 acceptance can be eyeballed in the whitebox.
    public sealed class PerfHud : MonoBehaviour
    {
        public Key toggleKey = Key.F3;
        public bool visible = true;

        readonly float[] frames = new float[120];
        int index;
        float timer;
        float fps;

        void Update()
        {
            frames[index++ % frames.Length] = Time.unscaledDeltaTime;
            timer += Time.unscaledDeltaTime;

            if (timer >= 0.25f)
            {
                timer = 0f;
                float sum = 0f;
                for (int i = 0; i < frames.Length; i++) sum += frames[i];
                fps = frames.Length / Mathf.Max(0.0001f, sum);
            }

            if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame) visible = !visible;
        }

        void OnGUI()
        {
            if (!visible) return;
            string text = string.Format("FPS {0:0}   {1:0.0} ms", fps, 1000f / Mathf.Max(1f, fps));
            GUI.color = Color.black;
            GUI.Label(new Rect(11f, 11f, 320f, 24f), text);
            GUI.color = Color.white;
            GUI.Label(new Rect(10f, 10f, 320f, 24f), text);
        }
    }
}
