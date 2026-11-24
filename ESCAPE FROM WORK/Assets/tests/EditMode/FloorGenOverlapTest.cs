using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EscapeFromWork.Level;

namespace EscapeFromWork.Tests.EditMode
{
    /// <summary>
    /// Regression tests for procedural floor assembly geometry:
    /// generated walls and furniture must not interpenetrate (穿模).
    /// Guards against coordinate-order bugs in FloorAssembler wall
    /// placement (see FloorAssembler.CreateWallSeg conventions).
    /// </summary>
    public class FloorGenOverlapTest
    {
        private const float MinPenetration = 0.05f; // ignore hairline contact
        private const float MinOverlapLen = 0.5f;   // ignore tiny touches
        private const float CornerFootprint = 0.35f; // perpendicular wall junctions are legal

        private readonly List<GameObject> createdRoots = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in createdRoots)
                if (go != null) Object.DestroyImmediate(go);
            createdRoots.Clear();
        }

        [TestCase(1)]
        [TestCase(3)]
        [TestCase(5)]
        public void test_assembled_floor_has_no_significant_mesh_overlaps(int floorNumber)
        {
            // Deterministic: FloorBuilder is floor-seeded; Assemble uses
            // UnityEngine.Random for cabinets/tea bar, so pin the state.
            Random.InitState(floorNumber * 1000 + 7);

            // Snapshot scene roots so we only inspect newly created objects
            // (FloorAssembler currently creates walls at scene root).
            var before = new HashSet<GameObject>(GetSceneRoots());

            var layout = FloorBuilder.Build(floorNumber);
            FloorAssembler.Assemble(layout);

            foreach (var root in GetSceneRoots())
                if (!before.Contains(root)) createdRoots.Add(root);

            var renderers = new List<Renderer>();
            foreach (var root in createdRoots)
                renderers.AddRange(root.GetComponentsInChildren<Renderer>());

            var items = new List<Renderer>();
            foreach (var r in renderers)
                if (!IsExcluded(r.name)) items.Add(r);

            var offenders = new List<string>();
            for (int i = 0; i < items.Count; i++)
            {
                for (int j = i + 1; j < items.Count; j++)
                {
                    var a = items[i].bounds;
                    var b = items[j].bounds;
                    if (!a.Intersects(b)) continue;

                    float ix = Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x);
                    float iy = Mathf.Min(a.max.y, b.max.y) - Mathf.Max(a.min.y, b.min.y);
                    float iz = Mathf.Min(a.max.z, b.max.z) - Mathf.Max(a.min.z, b.min.z);

                    float minPen = Mathf.Min(ix, Mathf.Min(iy, iz));
                    float maxLen = Mathf.Max(ix, Mathf.Max(iy, iz));
                    if (minPen < MinPenetration || maxLen < MinOverlapLen) continue;

                    // Perpendicular wall corner junctions overlap by roughly
                    // thickness x thickness — legal construction, not clipping.
                    if (ix < CornerFootprint && iz < CornerFootprint) continue;

                    offenders.Add(
                        $"{items[i].name}@{a.center:F1} <-> {items[j].name}@{b.center:F1} " +
                        $"pen=({ix:F2},{iy:F2},{iz:F2})");
                }
            }

            Assert.IsEmpty(offenders,
                $"Floor {floorNumber}: {offenders.Count} significant mesh overlap(s):\n" +
                string.Join("\n", offenders));
        }

        private static bool IsExcluded(string name)
        {
            // Floor tiles / ground: everything legitimately sits on them.
            if (name.StartsWith("F_") || name == "Ground") return true;
            // Flat floor markers, cosmetic by design (tracked separately).
            if (name.StartsWith("ExtractPoint") || name.StartsWith("EntryPoint")) return true;
            if (name.StartsWith("HighValue")) return true;
            return false;
        }

        private static List<GameObject> GetSceneRoots()
        {
            var roots = new List<GameObject>();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            scene.GetRootGameObjects(roots);
            return roots;
        }
    }
}
