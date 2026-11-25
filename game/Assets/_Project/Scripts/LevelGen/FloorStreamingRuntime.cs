using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace EscapeFromWork.LevelGen
{
    /// <summary>
    /// Runtime host for the chunked floor: generates the plan once at startup, then streams
    /// chunk geometry in/out around the viewer. The shell (slab, ceiling, outer walls) and the
    /// NavMesh stay resident; each chunk is one GameObject with merged meshes.
    /// </summary>
    public class FloorStreamingRuntime : MonoBehaviour
    {
        public int Seed = 1;
        public FloorBand Band = FloorBand.Tower;
        public FloorPrototype Prototype = FloorPrototype.OpenOffice;
        public int CorridorLevels = 3;
        public FloorMaterials Materials;

        [Tooltip("观看者；留空时用 Camera.main")]
        public Transform Viewer;

        [Tooltip("以观看者所在块为中心的加载半径（块环距离）")]
        public int LoadRadius = FloorStreamer.DefaultLoadRadius;

        [Tooltip("超出该半径的块会被卸载")]
        public int UnloadRadius = FloorStreamer.DefaultUnloadRadius;

        [Tooltip("每帧最多构建几个块，避免大帧卡顿")]
        public int BuildsPerFrame = FloorStreamer.DefaultBuildsPerUpdate;

        public bool BuildNavMesh = true;
        public bool ShowStats = true;

        public FloorPlan Plan { get; private set; }
        public FloorChunkGrid Grid { get; private set; }
        public FloorStreamer Streamer { get; private set; }

        public int ChunksLoaded { get { return Streamer == null ? 0 : Streamer.LoadedCount; } }
        public int ChunksPending { get { return Streamer == null ? 0 : Streamer.PendingCount; } }
        public int ChunksBuilt { get; private set; }
        public int ChunksUnloaded { get; private set; }
        public float LastBuildMs { get; private set; }
        public float PeakBuildMs { get; private set; }
        public float TotalBuildMs { get; private set; }

        GameObject _root;
        GameObject _shell;
        NavMeshData _navMesh;
        NavMeshDataInstance _navMeshInstance;

        readonly Dictionary<int, ChunkView> _views = new Dictionary<int, ChunkView>();
        readonly List<int> _buildQueue = new List<int>();
        readonly List<int> _unloadQueue = new List<int>();

        sealed class ChunkView
        {
            public GameObject Go;
            public List<Mesh> Meshes;
        }

        void Awake()
        {
            if (Plan == null) Initialize(Seed, Band, Prototype, CorridorLevels, Materials, null);
        }

        void OnDestroy()
        {
            int count = Grid != null ? Grid.Count : 0;
            for (int i = 0; i < count; i++) DestroyChunk(i);
            if (_root != null) Destroy(_root);
            if (_navMeshInstance.valid) NavMesh.RemoveNavMeshData(_navMeshInstance);
            if (_navMesh != null) Destroy(_navMesh);
        }

        public void Initialize(FloorPlan plan, FloorMaterials materials, Transform viewer)
        {
            Plan = plan != null ? plan : FloorRuntime.GeneratePlan(Seed, Band, Prototype, CorridorLevels);
            if (materials != null) Materials = materials;
            if (viewer != null) Viewer = viewer;

            _root = new GameObject("FloorStreamed_" + Plan.Spec.Seed);
            _root.transform.SetParent(transform, false);

            Grid = new FloorChunkGrid(Plan);
            FloorChunkManifest[] manifests = FloorChunkPlanner.Plan(Plan, Grid);
            Streamer = new FloorStreamer(Grid, manifests, LoadRadius, UnloadRadius, BuildsPerFrame);
            _shell = FloorChunkGeometry.BuildShell(Plan, Materials, _root.transform);

            if (BuildNavMesh)
            {
                _navMesh = FloorGeometry.BuildNavMesh(Plan);
                if (_navMesh != null) _navMeshInstance = NavMesh.AddNavMeshData(_navMesh);
            }

            Debug.Log("[FloorStreamingRuntime] plan " + Plan.Spec.Seed + " " + Grid.Columns + "x" + Grid.Rows
                + " chunks over " + Plan.Bounds.W + "x" + Plan.Bounds.H);
        }

        public void Initialize(int seed, FloorBand band, FloorPrototype prototype, int corridorLevels,
            FloorMaterials materials, Transform viewer)
        {
            Seed = seed; Band = band; Prototype = prototype; CorridorLevels = corridorLevels;
            Initialize(null, materials, viewer);
        }

        /// Rebuilds the whole floor with a new seed (play-test R key). Old chunks, shell and
        /// NavMesh are destroyed, then the streaming pipeline starts again around the viewer.
        public void Regenerate(int seed)
        {
            int count = Grid != null ? Grid.Count : 0;
            for (int i = 0; i < count; i++) DestroyChunk(i);
            _views.Clear();
            if (_root != null) Destroy(_root);
            if (_navMeshInstance.valid) NavMesh.RemoveNavMeshData(_navMeshInstance);
            _navMeshInstance = default;
            if (_navMesh != null) Destroy(_navMesh);
            _navMesh = null;
            Streamer = null;
            Grid = null;
            Plan = null;
            ChunksBuilt = 0;
            ChunksUnloaded = 0;
            LastBuildMs = 0f;
            PeakBuildMs = 0f;
            TotalBuildMs = 0f;

            Initialize(seed, Band, Prototype, CorridorLevels, Materials, Viewer);
        }

        void Update()
        {
            if (Streamer == null) return;
            Transform view = Viewer != null ? Viewer : (Camera.main != null ? Camera.main.transform : null);
            Vector2 viewer = view != null ? new Vector2(view.position.x, view.position.z) : Grid.Center(0);

            Streamer.Tick(viewer);

            _buildQueue.Clear();
            if (Streamer.PollBuilds(_buildQueue) > 0)
            {
                for (int i = 0; i < _buildQueue.Count; i++) BuildChunkNow(_buildQueue[i]);
            }

            _unloadQueue.Clear();
            if (Streamer.PollUnloads(viewer, _unloadQueue) > 0)
            {
                for (int i = 0; i < _unloadQueue.Count; i++) DestroyChunk(_unloadQueue[i]);
            }
        }

        void BuildChunkNow(int index)
        {
            FloorChunkManifest manifest = Streamer.Manifests[index];
            float start = Time.realtimeSinceStartup;

            List<Mesh> meshes;
            GameObject go = FloorChunkGeometry.BuildChunk(manifest, Materials, _root.transform, out meshes);
            var view = new ChunkView { Go = go, Meshes = meshes };
            _views[index] = view;

            LastBuildMs = (Time.realtimeSinceStartup - start) * 1000f;
            TotalBuildMs += LastBuildMs;
            if (LastBuildMs > PeakBuildMs) PeakBuildMs = LastBuildMs;
            ChunksBuilt++;
            Streamer.CompleteBuild(index);
        }

        void DestroyChunk(int index)
        {
            ChunkView view;
            if (!_views.TryGetValue(index, out view)) return;
            _views.Remove(index);
            if (view.Go != null) Destroy(view.Go);
            if (view.Meshes != null)
            {
                for (int i = 0; i < view.Meshes.Count; i++) if (view.Meshes[i] != null) Destroy(view.Meshes[i]);
            }
            Streamer.NotifyUnloaded(index);
            ChunksUnloaded++;
        }

        void OnGUI()
        {
            if (!ShowStats || Streamer == null) return;
            GUILayout.BeginArea(new Rect(12, 12, 330, 120), GUI.skin.box);
            GUILayout.Label("Chunks " + ChunksLoaded + "/" + Grid.Count + "  pending " + ChunksPending);
            GUILayout.Label("built " + ChunksBuilt + "  unloaded " + ChunksUnloaded);
            GUILayout.Label("build ms last " + LastBuildMs.ToString("0.0") + "  peak " + PeakBuildMs.ToString("0.0"));
            GUILayout.EndArea();
        }
    }
}
