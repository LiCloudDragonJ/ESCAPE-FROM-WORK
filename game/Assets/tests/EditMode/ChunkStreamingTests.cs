using System.Collections.Generic;
using EscapeFromWork.LevelGen;
using NUnit.Framework;
using UnityEngine;

namespace EscapeFromWork.Tests
{
    public class ChunkStreamingTests
    {
        static FloorSpec Spec(int seed)
        {
            FloorSpec spec = FloorSpec.Default(seed);
            spec.Band = FloorBand.Tower;
            spec.Width = 200f;
            spec.Height = 160f;
            spec.Prototype = (FloorPrototype)(seed % 6);
            spec.CorridorLevels = 3;
            return spec;
        }

        static FloorPlan Plan(int seed = 1) { return FloorGenerator.Generate(Spec(seed)); }

        [Test]
        public void Grid_TilesTheWholeFloorWithEightEqualChunks()
        {
            FloorPlan plan = Plan();
            FloorChunkGrid grid = new FloorChunkGrid(plan);

            Assert.AreEqual(8, grid.Count);
            Assert.AreEqual(2, grid.Columns);
            Assert.AreEqual(4, grid.Rows);

            float area = 0f;
            for (int i = 0; i < grid.Count; i++)
            {
                RectF r = grid.Rect(i);
                Assert.AreEqual(100f, r.W, 0.01f, "chunk " + i + " width");
                Assert.AreEqual(40f, r.H, 0.01f, "chunk " + i + " height");
                Assert.AreEqual(4000f, r.Area, 0.01f, "chunk " + i + " area");
                Assert.IsTrue(plan.Bounds.Contains(r.Center), "chunk " + i + " center inside bounds");
                area += r.Area;
            }
            Assert.AreEqual(plan.Bounds.Area, area, 0.5f);
        }

        [Test]
        public void Grid_ChunkRectsDoNotOverlap()
        {
            FloorChunkGrid grid = new FloorChunkGrid(Plan());
            for (int a = 0; a < grid.Count; a++)
            {
                for (int b = a + 1; b < grid.Count; b++)
                {
                    RectF ra = grid.Rect(a), rb = grid.Rect(b);
                    bool overlaps = ra.X < rb.XMax - 0.0001f && rb.X < ra.XMax - 0.0001f
                        && ra.Y < rb.YMax - 0.0001f && rb.Y < ra.YMax - 0.0001f;
                    Assert.IsFalse(overlaps, "chunks " + a + " and " + b + " overlap");
                }
            }
        }

        [Test]
        public void Planner_ClipsWallVolumeWithoutLoss()
        {
            FloorPlan plan = Plan();
            FloorChunkGrid grid = new FloorChunkGrid(plan);
            FloorChunkManifest[] manifests = FloorChunkPlanner.Plan(plan, grid);

            List<BoxSpec> walls = new List<BoxSpec>();
            FloorGeometry.CollectWallBoxes(plan, walls);
            float total = 0f;
            for (int i = 0; i < walls.Count; i++) total += walls[i].Volume;

            float clipped = 0f;
            for (int i = 0; i < manifests.Length; i++)
            {
                clipped += manifests[i].WallVolume;
                RectF rect = manifests[i].Rect;
                for (int b = 0; b < manifests[i].WallBoxes.Count; b++)
                {
                    BoxSpec box = manifests[i].WallBoxes[b];
                    Assert.IsTrue(box.Center.x >= rect.X - 0.001f && box.Center.x <= rect.XMax + 0.001f,
                        "chunk " + i + " wall box outside X");
                    Assert.IsTrue(box.Center.z >= rect.Y - 0.001f && box.Center.z <= rect.YMax + 0.001f,
                        "chunk " + i + " wall box outside Z");
                }
            }
            Assert.AreEqual(total, clipped, Mathf.Max(0.001f, total * 0.001f), "clipped wall volume");
        }

        [Test]
        public void Planner_CoversEveryItemAcrossChunks()
        {
            FloorPlan plan = Plan(3);
            FloorChunkGrid grid = new FloorChunkGrid(plan);
            FloorChunkManifest[] manifests = FloorChunkPlanner.Plan(plan, grid);

            int extractions = 0, lights = 0, panels = 0;
            float propPieceArea = 0f, propSourceArea = 0f;
            for (int i = 0; i < manifests.Length; i++)
            {
                extractions += manifests[i].Extractions.Count;
                lights += manifests[i].Lights.Count;
                panels += manifests[i].PanelBoxes.Count;
                for (int l = 0; l < manifests[i].Lights.Count; l++)
                {
                    Assert.IsTrue(manifests[i].Rect.Contains(manifests[i].Lights[l]),
                        "light assigned to chunk " + i + " lies outside it");
                }
                for (int p = 0; p < manifests[i].Props.Count; p++) propPieceArea += manifests[i].Props[p].Rect.Area;
                for (int p = 0; p < manifests[i].SpawnPads.Count; p++)
                {
                    RectF pad = manifests[i].SpawnPads[p];
                    Assert.IsTrue(manifests[i].Rect.Inflate(0.001f).Contains(pad.Center), "spawn pad piece outside chunk " + i);
                }
            }
            for (int p = 0; p < plan.Props.Count; p++) propSourceArea += plan.Props[p].Rect.Area;

            Assert.AreEqual(plan.Extractions.Count, extractions);
            Assert.Greater(panels, 0);
            Assert.AreEqual(FloorGeometry.SampleLights(CollectLights(plan), FloorGeometry.LightBudget).Count, lights);
            Assert.AreEqual(propSourceArea, propPieceArea, Mathf.Max(0.01f, propSourceArea * 0.01f),
                "clipped prop pieces must cover the source props");
        }

        [Test]
        public void Planner_ClipsPropsAndPadsToTheChunkBorder()
        {
            FloorPlan plan = Plan(1);
            FloorChunkGrid grid = new FloorChunkGrid(plan);
            FloorChunkManifest[] manifests = FloorChunkPlanner.Plan(plan, grid);

            for (int i = 0; i < manifests.Length; i++)
            {
                RectF rect = manifests[i].Rect;
                for (int p = 0; p < manifests[i].Props.Count; p++)
                {
                    RectF prop = manifests[i].Props[p].Rect;
                    Assert.IsTrue(prop.X >= rect.X - 0.001f && prop.XMax <= rect.XMax + 0.001f
                        && prop.Y >= rect.Y - 0.001f && prop.YMax <= rect.YMax + 0.001f,
                        "chunk " + i + " prop piece " + p + " crosses the chunk border");
                }
                for (int p = 0; p < manifests[i].SpawnPads.Count; p++)
                {
                    RectF pad = manifests[i].SpawnPads[p];
                    Assert.IsTrue(pad.X >= rect.X - 0.001f && pad.XMax <= rect.XMax + 0.001f
                        && pad.Y >= rect.Y - 0.001f && pad.YMax <= rect.YMax + 0.001f,
                        "chunk " + i + " pad piece " + p + " crosses the chunk border");
                }
            }
        }

        [Test]
        public void Streamer_LoadsNearestChunkFirstAndRespectsBudget()
        {
            FloorPlan plan = Plan();
            FloorChunkGrid grid = new FloorChunkGrid(plan);
            FloorStreamer streamer = new FloorStreamer(grid, FloorChunkPlanner.Plan(plan, grid),
                loadRadius: 1, unloadRadius: 2, buildsPerUpdate: 1);
            Vector2 viewer = grid.Center(0);

            streamer.Tick(viewer);
            List<int> builds = new List<int>();
            Assert.AreEqual(1, streamer.PollBuilds(builds));
            Assert.AreEqual(0, builds[0], "viewer stands in chunk 0, it must stream first");
            Assert.AreEqual(1, streamer.PendingCount);
            Assert.IsFalse(streamer.IsLoaded(0));

            streamer.CompleteBuild(0);
            Assert.IsTrue(streamer.IsLoaded(0));
            Assert.AreEqual(0, streamer.PendingCount);

            streamer.Tick(viewer);
            streamer.LoadAll(viewer);
            Assert.AreEqual(grid.Ring(0, 1).Count, streamer.LoadedCount, "ring 1 around a corner chunk");
        }

        [Test]
        public void Streamer_UnloadsChunksBeyondTheUnloadRadius()
        {
            FloorPlan plan = Plan();
            FloorChunkGrid grid = new FloorChunkGrid(plan);
            FloorStreamer streamer = new FloorStreamer(grid, FloorChunkPlanner.Plan(plan, grid),
                loadRadius: 1, unloadRadius: 2, buildsPerUpdate: 8);

            Vector2 start = grid.Center(0);
            streamer.LoadAll(start);
            int loadedAtStart = streamer.LoadedCount;
            Assert.Greater(loadedAtStart, 0);

            Vector2 far = grid.Center(grid.Count - 1);
            streamer.Tick(far);
            List<int> unloads = new List<int>();
            streamer.PollUnloads(far, unloads);
            Assert.Greater(unloads.Count, 0);
            for (int i = 0; i < unloads.Count; i++)
            {
                streamer.NotifyUnloaded(unloads[i]);
                Assert.IsFalse(streamer.IsLoaded(unloads[i]));
            }
            Assert.Less(streamer.LoadedCount, loadedAtStart);

            streamer.LoadAll(far);
            // Hysteresis: chunks loaded near the old position stay while within unloadRadius,
            // and the new viewer's ring is fully loaded; nothing beyond the radius survives.
            List<int> ring = grid.Ring(grid.Count - 1, 1);
            for (int i = 0; i < ring.Count; i++)
                Assert.IsTrue(streamer.IsLoaded(ring[i]), "chunk " + ring[i] + " should be loaded at the far corner");
            for (int i = 0; i < grid.Count; i++)
            {
                if (!streamer.IsLoaded(i)) continue;
                Assert.LessOrEqual(grid.RingDistance(grid.Count - 1, i), 2,
                    "chunk " + i + " stayed loaded beyond the unload radius");
            }
        }

        [Test]
        public void Planner_IsDeterministicForTheSameSeed()
        {
            FloorPlan plan = Plan(5);
            FloorChunkGrid grid = new FloorChunkGrid(plan);
            FloorChunkManifest[] a = FloorChunkPlanner.Plan(plan, grid);
            FloorChunkManifest[] b = FloorChunkPlanner.Plan(plan, grid);

            for (int i = 0; i < a.Length; i++)
            {
                Assert.AreEqual(a[i].WallBoxes.Count, b[i].WallBoxes.Count);
                Assert.AreEqual(a[i].Props.Count, b[i].Props.Count);
                Assert.AreEqual(a[i].Lights.Count, b[i].Lights.Count);
                Assert.AreEqual(a[i].ItemCount, b[i].ItemCount);
            }
        }

        [Test]
        public void ChunkGeometry_BuildsShellAndAChunk()
        {
            FloorPlan plan = Plan();
            GameObject parent = new GameObject("ChunkTestParent");
            try
            {
                GameObject shell = FloorChunkGeometry.BuildShell(plan, null, parent.transform);
                Assert.AreEqual(3, shell.GetComponentsInChildren<MeshRenderer>().Length,
                    "shell = floor slab + ceiling + outer walls");

                FloorChunkGrid grid = new FloorChunkGrid(plan);
                FloorChunkManifest[] manifests = FloorChunkPlanner.Plan(plan, grid);
                List<Mesh> meshes;
                GameObject chunk = FloorChunkGeometry.BuildChunk(manifests[0], null, parent.transform, out meshes);
                Assert.Greater(chunk.GetComponentsInChildren<MeshRenderer>().Length, 0);
                Assert.Greater(meshes.Count, 0, "chunk meshes must be tracked for unloading");
                Assert.Greater(chunk.GetComponentsInChildren<Collider>().Length, 0, "walls need colliders");
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void ChunkGeometry_BuildsAllEightChunksAndReportsCost()
        {
            FloorPlan plan = Plan();
            FloorChunkGrid grid = new FloorChunkGrid(plan);
            FloorChunkManifest[] manifests = FloorChunkPlanner.Plan(plan, grid);
            GameObject parent = new GameObject("ChunkBench");
            List<Mesh> meshes = new List<Mesh>();
            try
            {
                System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                FloorChunkGeometry.BuildShell(plan, null, parent.transform);
                float shellMs = (float)sw.Elapsed.TotalMilliseconds;

                float total = shellMs, worst = 0f;
                for (int i = 0; i < manifests.Length; i++)
                {
                    sw.Restart();
                    List<Mesh> chunkMeshes;
                    FloorChunkGeometry.BuildChunk(manifests[i], null, parent.transform, out chunkMeshes);
                    float ms = (float)sw.Elapsed.TotalMilliseconds;
                    meshes.AddRange(chunkMeshes);
                    total += ms;
                    if (ms > worst) worst = ms;
                    Debug.Log("[ChunkBench] chunk " + i + " " + ms.ToString("0.0") + " ms, items " + manifests[i].ItemCount);
                }
                Debug.Log("[ChunkBench] shell " + shellMs.ToString("0.0") + " ms, all 8 chunks " + total.ToString("0.0")
                    + " ms, worst chunk " + worst.ToString("0.0") + " ms (tech-plan budget 2500 ms)");
                Assert.Less(total, 10000f, "chunk build cost must stay in the same order as the generation budget");
            }
            finally
            {
                for (int i = 0; i < meshes.Count; i++) if (meshes[i] != null) Object.DestroyImmediate(meshes[i]);
                Object.DestroyImmediate(parent);
            }
        }

        static List<Vector2> CollectLights(FloorPlan plan)
        {
            List<Vector2> candidates = new List<Vector2>();
            FloorGeometry.CollectLightPositions(plan, candidates);
            return candidates;
        }
    }
}
