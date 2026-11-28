using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using EscapeFromWork.LevelGen;

namespace EscapeFromWork.Gameplay
{
    /// <summary>
    /// T3: a raid is four floors of one tower. Floors are generated from the SlicePlanner raid and
    /// stacked at <see cref="FloorGeometry.FloorToFloor"/> intervals; only the current floor (and,
    /// optionally, its immediate neighbours) exists. The core is the elevator: standing inside it
    /// and pressing E walks one floor up, Q one floor down.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TowerRuntime : MonoBehaviour
    {
        [Header("Raid")]
        public int Seed = 1;

        [Header("Assets (auto-filled when built from the scene tool)")]
        public FloorMaterials Materials;
        public PropCatalog Catalog;

        [Header("Streaming")]
        [Tooltip("How many floors above/below the active one stay built. 0 = only the current floor.")]
        [Range(0, 1)] public int NeighbourRadius = 0;
        public bool BuildNavMeshForFloors = true;

        [Header("Player")]
        [Tooltip("Player root. Left empty, the runtime finds a FirstPersonController in the scene.")]
        public Transform Player;
        [Tooltip("Horizontal distance from the elevator centre where the player is placed after a switch.")]
        public float ArrivalRadius = 1.1f;

        [Header("HUD")]
        public bool ShowHud = true;
        public Key UpKey = Key.E;
        public Key DownKey = Key.Q;

        public RaidPlan Raid { get; private set; }
        public int ActiveFloor { get; private set; }
        public int FloorCount => Raid != null ? Raid.FloorCount : 0;
        public float LevelY(int index) => index * FloorGeometry.FloorToFloor;

        readonly Dictionary<int, GameObject> floors = new Dictionary<int, GameObject>();
        readonly Dictionary<int, NavMeshDataInstance> navMeshes = new Dictionary<int, NavMeshDataInstance>();
        readonly List<int> wanted = new List<int>(3);
        FirstPersonController controller;
        float lastBuildMs;
        string hint = "";

        void Start()
        {
            if (Raid == null) Initialize(Seed);
            for (int i = 0; i < System.Environment.GetCommandLineArgs().Length; i++)
                if (System.Environment.GetCommandLineArgs()[i] == "-towerCapture")
                {
                    StartCoroutine(AutoCapture());
                    break;
                }
        }

        /// <summary>
        /// Build smoke evidence: a packaged player walks the raid by switching floors through the same
        /// path the elevator input uses, screenshots each floor, then quits. Enabled with -towerCapture.
        /// </summary>
        System.Collections.IEnumerator AutoCapture()
        {
            yield return new WaitForSeconds(0.6f);
            for (int i = 0; i < Raid.FloorCount; i++)
            {
                SwitchTo(i);
                // 电梯是个封闭小间，站在里面截图只有四面墙。走到这一层最长的那条走廊中间，
                // 顺着走廊方向拍，画面才是这层的真实外观；只影响 -towerCapture 的证据截图。
                FloorPlan plan = Raid.Floors[i].Plan;
                Vector2 spot2 = plan.Core.Center;
                float yaw2 = 180f;
                float best = -1f;
                for (int c = 0; c < plan.Corridors.Count; c++)
                {
                    RectF r = plan.Corridors[c].Rect;
                    float len = Mathf.Max(r.W, r.H);
                    if (len <= best) continue;
                    best = len;
                    spot2 = r.Center;
                    yaw2 = r.H >= r.W ? 0f : 90f;
                }
                Vector3 spot = new Vector3(spot2.x, LevelY(i) + 0.15f, spot2.y);
                if (controller != null) controller.Teleport(spot, yaw2);
                yield return new WaitForSeconds(0.7f);
                string path = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(),
                    "tower-run-f" + (i + 1) + ".png");
                ScreenCapture.CaptureScreenshot(path);
                yield return new WaitForSeconds(0.5f);
            }
            Debug.Log("[Tower] autoplay capture done");
            yield return new WaitForSeconds(0.5f);
            Application.Quit();
        }

        void OnDestroy()
        {
            foreach (KeyValuePair<int, NavMeshDataInstance> pair in navMeshes)
                if (pair.Value.valid) pair.Value.Remove();
            navMeshes.Clear();
        }

        public void Initialize(int seed)
        {
            Seed = seed;
            Raid = SlicePlanner.Generate(seed);
            if (Player == null)
            {
                FirstPersonController found = FindObjectOfType<FirstPersonController>();
                if (found != null) Player = found.transform;
            }
            controller = Player != null ? Player.GetComponent<FirstPersonController>() : null;
            ActiveFloor = 0;
            RebuildAround(0);
            PlacePlayerAtElevator(0);
        }

        /// <summary>Rebuild the floors around <paramref name="index"/> and make it the active one.</summary>
        public void SwitchTo(int index)
        {
            if (Raid == null) return;
            index = Mathf.Clamp(index, 0, Raid.FloorCount - 1);
            if (index == ActiveFloor && floors.ContainsKey(index)) return;
            ActiveFloor = index;
            RebuildAround(index);
            PlacePlayerAtElevator(index);
        }

        public GameObject BuildFloor(int index)
        {
            if (Raid == null) return null;
            index = Mathf.Clamp(index, 0, Raid.FloorCount - 1);
            GameObject existing;
            if (floors.TryGetValue(index, out existing) && existing != null) return existing;

            SliceFloor slice = Raid.Floors[index];
            GameObject holder = new GameObject("Tower_F" + index);
            holder.transform.SetParent(transform, false);
            holder.transform.localPosition = new Vector3(0f, LevelY(index), 0f);

            float started = Time.realtimeSinceStartup;
            FloorGeometry.Build(slice.Plan, Materials, holder.transform, Catalog);
            lastBuildMs = (Time.realtimeSinceStartup - started) * 1000f;

            if (BuildNavMeshForFloors)
            {
                NavMeshData data = FloorGeometry.BuildNavMesh(slice.Plan);
                if (data != null)
                    navMeshes[index] = NavMesh.AddNavMeshData(data, Vector3.up * LevelY(index), Quaternion.identity);
            }

            floors[index] = holder;
            Debug.Log(string.Format("[Tower] F{0} (world {1}F) built in {2:0} ms", index, slice.Spec.WorldFloor, lastBuildMs));
            return holder;
        }

        public void UnloadFloor(int index)
        {
            GameObject holder;
            if (floors.TryGetValue(index, out holder))
            {
                floors.Remove(index);
                if (holder != null) Destroy(holder);
            }
            NavMeshDataInstance nav;
            if (navMeshes.TryGetValue(index, out nav))
            {
                navMeshes.Remove(index);
                if (nav.valid) nav.Remove();
            }
        }

        void RebuildAround(int index)
        {
            wanted.Clear();
            for (int i = index - NeighbourRadius; i <= index + NeighbourRadius; i++)
                if (i >= 0 && i < Raid.FloorCount) wanted.Add(i);

            List<int> stale = new List<int>();
            foreach (KeyValuePair<int, GameObject> pair in floors)
                if (!wanted.Contains(pair.Key)) stale.Add(pair.Key);
            for (int i = 0; i < stale.Count; i++) UnloadFloor(stale[i]);
            for (int i = 0; i < wanted.Count; i++) BuildFloor(wanted[i]);
        }

        public Vector2 ElevatorCenter(int index)
        {
            if (Raid == null) return Vector2.zero;
            return Raid.Floors[Mathf.Clamp(index, 0, Raid.FloorCount - 1)].Plan.Core.Center;
        }

        void PlacePlayerAtElevator(int index)
        {
            if (Player == null) return;
            Vector2 c = ElevatorCenter(index);
            Vector3 position = new Vector3(c.x, LevelY(index) + 0.15f, c.y);
            // 从核心筒里出来时要面朝楼层，而不是怼着核心筒北墙。核心筒在本层北侧，电梯门朝南，
            // 所以默认面朝 -Z；核心筒落到南半区时反过来。朝向取整会让切层后的第一眼总是开阔区。
            FloorPlan plan = Raid.Floors[Mathf.Clamp(index, 0, Raid.FloorCount - 1)].Plan;
            float yaw = plan.Core.Center.y >= plan.Spec.Height * 0.5f ? 180f : 0f;
            Vector2 dir = new Vector2(plan.Spec.Width * 0.5f, plan.Spec.Height * 0.5f) - plan.Core.Center;
            if (Mathf.Abs(dir.x) > 6f) yaw = Mathf.Atan2(dir.x, Mathf.Abs(dir.y)) * Mathf.Rad2Deg + (dir.y > 0f ? 0f : 180f);
            if (controller != null) controller.Teleport(position, yaw);
            else Player.position = position;
        }

        void Update()
        {
            if (Raid == null || Player == null) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            bool inside = InsideElevator();
            bool up = keyboard[UpKey].wasPressedThisFrame;
            bool down = keyboard[DownKey].wasPressedThisFrame;
            hint = inside ? "电梯：E 上一层 / Q 下一层" : "";

            if (!inside) return;
            if (up) SwitchTo(ActiveFloor + 1);
            else if (down) SwitchTo(ActiveFloor - 1);
        }

        bool InsideElevator()
        {
            if (Raid == null) return false;
            Vector3 p = Player.position;
            RectF core = Raid.Floors[Mathf.Clamp(ActiveFloor, 0, Raid.FloorCount - 1)].Plan.Core;
            float dx = p.x - (core.X + core.W * 0.5f);
            float dz = p.z - (core.Y + core.H * 0.5f);
            return Mathf.Abs(dx) <= core.W * 0.5f - 0.15f
                && Mathf.Abs(dz) <= core.H * 0.5f - 0.15f
                && Mathf.Abs(p.y - LevelY(ActiveFloor)) <= 2.4f;
        }

        void OnGUI()
        {
            if (!ShowHud || Raid == null) return;
            int world = Raid.Floors[Mathf.Clamp(ActiveFloor, 0, Raid.FloorCount - 1)].Spec.WorldFloor;
            GUI.Label(new Rect(16f, 14f, 460f, 26f), string.Format("楼层 {0} / 4   （世界 {1}F）", ActiveFloor + 1, world));
            GUI.Label(new Rect(16f, 36f, 460f, 26f), string.Format("E 上一楼 / Q 下一楼　　建造 {0:0} ms", lastBuildMs));
            if (!string.IsNullOrEmpty(hint)) GUI.Label(new Rect(16f, 58f, 460f, 26f), hint);
        }
    }
}
