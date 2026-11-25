using UnityEngine;
using UnityEngine.InputSystem;
using EscapeFromWork.Gameplay;
using EscapeFromWork.LevelGen;

namespace EscapeFromWork.UI
{
    /// Play-test helper for the floor sandbox.
    /// R: regenerate the floor with a new seed. H: toggle the control sheet. F1/F3/M live on other components.
    public class PlayTestControls : MonoBehaviour
    {
        public FloorStreamingRuntime Runtime;
        public MapHud Hud;
        public FirstPersonController Player;
        public int SeedStep = 17;

        bool _showHelp = true;
        string _message;
        float _messageUntil;
        Vector3 _spawnPoint;
        float _spawnYaw;

        /// <summary>Below this height the player has left the slab; drop them back on the spawn pad.</summary>
        const float FallLimitY = -4f;

        void Start()
        {
            if (Runtime == null) Runtime = FindObjectOfType<FloorStreamingRuntime>();
            if (Player == null) Player = FindObjectOfType<FirstPersonController>();
            if (Hud == null) Hud = FindObjectOfType<MapHud>();

            if (Player != null) _spawnPoint = Player.transform.position;
            if (Runtime != null && Runtime.Plan != null && Runtime.Plan.SpawnZones.Count > 0)
            {
                Vector2 c = Runtime.Plan.SpawnZones[0].Center;
                _spawnPoint = new Vector3(c.x, 0.2f, c.y);
                _spawnYaw = Player != null ? Player.transform.eulerAngles.y : 0f;
            }
        }

        void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (kb.hKey.wasPressedThisFrame) _showHelp = !_showHelp;
            if (kb.rKey.wasPressedThisFrame) Rebuild();

            if (Player != null)
            {
                if (kb.leftBracketKey.wasPressedThisFrame) BumpSensitivity(-0.01f);
                if (kb.rightBracketKey.wasPressedThisFrame) BumpSensitivity(0.01f);
                RescueIfFallen();
            }
        }

        /// Safety net until the slab collider and chunk streaming are proven: a player who slips
        /// below the floor is put back on the spawn pad instead of falling forever.
        void RescueIfFallen()
        {
            if (Player.transform.position.y > FallLimitY) return;
            Player.Teleport(_spawnPoint, _spawnYaw);
            _message = "坠落已救回出生点 (y < " + FallLimitY.ToString("0") + ")";
            _messageUntil = Time.time + 4f;
        }

        /// Live mouse-sensitivity tuning so the feel can be dialled in without leaving Play mode.
        void BumpSensitivity(float delta)
        {
            Player.lookSensitivity = Mathf.Clamp(Player.lookSensitivity + delta, 0.02f, 0.30f);
            _message = "灵敏度 " + Player.lookSensitivity.ToString("0.00");
            _messageUntil = Time.time + 2.5f;
        }

        public void Rebuild()
        {
            if (Runtime == null) return;

            int seed = Runtime.Seed + SeedStep;
            Runtime.Regenerate(seed);

            FloorPlan plan = Runtime.Plan;
            if (Hud != null) Hud.Rebuild(plan);

            if (Player != null && plan.SpawnZones.Count > 0)
            {
                Vector2 c = plan.SpawnZones[0].Center;
                Vector2 look = new Vector2(plan.Core.Center.x - c.x, plan.Core.Center.y - c.y);
                float yaw = look.sqrMagnitude > 0.01f
                    ? Quaternion.LookRotation(new Vector3(look.x, 0f, look.y).normalized, Vector3.up).eulerAngles.y
                    : 0f;
                _spawnPoint = new Vector3(c.x, 0.2f, c.y);
                _spawnYaw = yaw;
                Player.Teleport(_spawnPoint, yaw);
            }

            _message = "seed " + seed + "   rooms " + plan.Rooms.Count + "   props " + plan.Props.Count;
            _messageUntil = Time.time + 4f;
        }

        static Texture2D _px;

        void DrawCrosshair()
        {
            if (Cursor.lockState != CursorLockMode.Locked) return;
            if (_px == null)
            {
                _px = new Texture2D(1, 1);
                _px.SetPixel(0, 0, Color.white);
                _px.Apply();
            }

            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.55f);

            // centre dot
            GUI.DrawTexture(new Rect(cx - 1f, cy - 1f, 2f, 2f), _px);
            // four ticks, 7px long, 3px gap
            GUI.DrawTexture(new Rect(cx - 0.5f, cy - 10f, 1f, 7f), _px);
            GUI.DrawTexture(new Rect(cx - 0.5f, cy + 3f, 1f, 7f), _px);
            GUI.DrawTexture(new Rect(cx - 10f, cy - 0.5f, 7f, 1f), _px);
            GUI.DrawTexture(new Rect(cx + 3f, cy - 0.5f, 7f, 1f), _px);

            GUI.color = prev;
        }

        void OnGUI()
        {
            DrawCrosshair();
            int w = 330;
            int h = _showHelp ? 214 : 22;
            Rect area = new Rect(12f, Screen.height - h - 12f, w, h);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label(_showHelp ? "控制 (H 收起)" : "控制 (H 展开)");
            if (_showHelp)
            {
                GUILayout.Label("WASD 移动    Shift 冲刺    Ctrl 蹲    Space 跳");
                GUILayout.Label("左键 射击（按住连发）    M 地图    F3 帧率");
                GUILayout.Label("R 换一层 (新种子)    Esc 解锁鼠标 (左键锁回)");
                GUILayout.Label("F1 视角晃动");
                GUILayout.Label("灵敏度 " + (Player != null ? Player.lookSensitivity.ToString("0.00") : "-") + "    [ ] 调节");
            }
            if (!string.IsNullOrEmpty(_message) && Time.time < _messageUntil)
                GUILayout.Label(_message);
            GUILayout.EndArea();
        }
    }
}