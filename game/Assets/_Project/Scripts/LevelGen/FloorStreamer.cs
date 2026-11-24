using System;
using System.Collections.Generic;
using UnityEngine;

namespace EscapeFromWork.LevelGen
{
    /// <summary>
    /// Pure streaming policy for the chunked floor. No GameObjects here: the MonoBehaviour
    /// runtime calls Tick() with the viewer position, then asks for the chunks to build
    /// (nearest first, budgeted) and for the chunks to unload, and reports completions back.
    /// Kept free of Unity object creation so EditMode tests can drive it directly.
    /// </summary>
    public sealed class FloorStreamer
    {
        public const int DefaultLoadRadius = 1;
        public const int DefaultUnloadRadius = 2;
        public const int DefaultBuildsPerUpdate = 2;

        public readonly FloorChunkGrid Grid;
        public readonly FloorChunkManifest[] Manifests;

        readonly bool[] _loaded;
        readonly bool[] _building;
        readonly int[] _desired;          // -1 when the slot is empty
        readonly int _loadRadius;
        readonly int _unloadRadius;
        readonly int _buildsPerUpdate;

        int _desiredCount;

        public FloorStreamer(FloorChunkGrid grid, FloorChunkManifest[] manifests,
            int loadRadius = DefaultLoadRadius, int unloadRadius = DefaultUnloadRadius,
            int buildsPerUpdate = DefaultBuildsPerUpdate)
        {
            if (grid == null) throw new ArgumentNullException("grid");
            if (manifests == null) throw new ArgumentNullException("manifests");
            if (manifests.Length != grid.Count)
                throw new ArgumentException("manifest count " + manifests.Length + " != chunk count " + grid.Count);

            Grid = grid;
            Manifests = manifests;
            _loaded = new bool[grid.Count];
            _building = new bool[grid.Count];
            _desired = new int[grid.Count];
            for (int i = 0; i < _desired.Length; i++) _desired[i] = -1;
            _loadRadius = Mathf.Max(0, loadRadius);
            _unloadRadius = Mathf.Max(_loadRadius, unloadRadius);
            _buildsPerUpdate = Mathf.Max(1, buildsPerUpdate);
        }

        public int ChunkCount { get { return Grid.Count; } }

        public int LoadedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _loaded.Length; i++) if (_loaded[i]) n++;
                return n;
            }
        }

        public int PendingCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _building.Length; i++) if (_building[i]) n++;
                return n;
            }
        }

        public bool IsLoaded(int index) { return index >= 0 && index < _loaded.Length && _loaded[index]; }

        public bool IsPending(int index) { return index >= 0 && index < _building.Length && _building[index]; }

        /// <summary>Chunk indices that still need to be built for the current viewer, nearest first.</summary>
        public List<int> Desired
        {
            get
            {
                List<int> list = new List<int>(_desiredCount);
                for (int i = 0; i < _desiredCount; i++) list.Add(_desired[i]);
                return list;
            }
        }

        /// <summary>Recompute the residence set from the viewer. Safe to call every frame.</summary>
        public void Tick(Vector2 viewer)
        {
            int center = Grid.IndexAt(viewer);
            List<int> ring = Grid.Ring(center, _loadRadius);
            ring.Sort((a, b) =>
            {
                int c = Grid.DistanceTo(a, viewer).CompareTo(Grid.DistanceTo(b, viewer));
                return c != 0 ? c : a.CompareTo(b);
            });

            _desiredCount = 0;
            for (int i = 0; i < ring.Count; i++)
            {
                int index = ring[i];
                if (!_loaded[index] && !_building[index]) _desired[_desiredCount++] = index;
            }
        }

        /// <summary>Fill output with up to the per-update budget of chunks to build; marks them pending.</summary>
        public int PollBuilds(List<int> output)
        {
            if (output == null) throw new ArgumentNullException("output");
            int added = 0;
            for (int i = 0; i < _desiredCount && added < _buildsPerUpdate; i++)
            {
                int index = _desired[i];
                if (index < 0 || _loaded[index] || _building[index]) continue;
                _building[index] = true;
                output.Add(index);
                added++;
            }
            if (added > 0) _desiredCount = Compact(_desired, _desiredCount);
            return added;
        }

        /// <summary>Chunks that may be unloaded: loaded but beyond the unload radius.</summary>
        public int PollUnloads(Vector2 viewer, List<int> output)
        {
            if (output == null) throw new ArgumentNullException("output");
            int center = Grid.IndexAt(viewer);
            int count = 0;
            for (int i = 0; i < _loaded.Length; i++)
            {
                if (!_loaded[i]) continue;
                if (Grid.RingDistance(center, i) > _unloadRadius) { output.Add(i); count++; }
            }
            return count;
        }

        public void CompleteBuild(int index)
        {
            if (index < 0 || index >= _loaded.Length) return;
            _building[index] = false;
            _loaded[index] = true;
            _desiredCount = Compact(_desired, _desiredCount);
        }

        public void NotifyUnloaded(int index)
        {
            if (index < 0 || index >= _loaded.Length) return;
            _loaded[index] = false;
            _building[index] = false;
        }

        /// <summary>Test helper: load everything around a viewer in as many ticks as needed.</summary>
        public void LoadAll(Vector2 viewer, int maxTicks = 64)
        {
            List<int> builds = new List<int>();
            for (int tick = 0; tick < maxTicks; tick++)
            {
                Tick(viewer);
                builds.Clear();
                if (PollBuilds(builds) == 0) break;
                for (int i = 0; i < builds.Count; i++) CompleteBuild(builds[i]);
            }
        }

        static int Compact(int[] list, int count)
        {
            int write = 0;
            for (int i = 0; i < count; i++)
            {
                int index = list[i];
                if (index >= 0) list[write++] = index;
            }
            for (int i = write; i < count; i++) list[i] = -1;
            return write;
        }
    }
}
