using System.Collections.Generic;
using System.IO;
using EscapeFromWork.LevelGen;
using NUnit.Framework;
using UnityEngine;

namespace EscapeFromWork.Tests.EditMode
{
    public class SignageTests
    {
        static List<FloorPlan> _plans;

        static List<FloorPlan> Plans()
        {
            if (_plans != null) return _plans;
            var plans = new List<FloorPlan>();
            for (int seed = 1; seed <= 6; seed++) plans.Add(FloorGenerator.Generate(FloorSpec.Default(seed)));
            _plans = plans;
            return _plans;
        }

        [Test]
        public void GeneratedFloors_HaveEveryRequiredSignFamily()
        {
            foreach (FloorPlan plan in Plans())
            {
                Assert.AreEqual(plan.Stairs.Count, plan.Signs.FindAll(s => s.Kind == SignKind.Stair).Count);
                Assert.GreaterOrEqual(plan.Signs.FindAll(s => s.Kind == SignKind.Elevator).Count, 1);
                Assert.GreaterOrEqual(plan.Signs.FindAll(s => s.Kind == SignKind.Floor).Count, 1);
                Assert.AreEqual(plan.Rooms.Count, plan.Signs.FindAll(s => s.Kind == SignKind.Room).Count,
                    "every room needs a dynamic room-number plate");
                Assert.GreaterOrEqual(plan.Signs.FindAll(s => s.Kind == SignKind.Evacuation).Count, 1,
                    "every floor needs a YOU ARE HERE board");
                SignPlacement floor = plan.Signs.Find(s => s.Kind == SignKind.Floor);
                Assert.AreEqual("F" + plan.Spec.WorldFloor, floor.Text);
                Assert.IsFalse(floor.Text.Contains("40F"));
            }
        }

        [Test]
        public void RaidFloors_UseDynamicOneBasedFloorAndRoomNumbers()
        {
            RaidPlan raid = SlicePlanner.Generate(1);
            for (int f = 0; f < 4; f++)
            {
                int worldFloor = raid.Floors[f].Spec.WorldFloor;
                Assert.That(worldFloor, Is.InRange(40, 50), "真实楼层号必须落在标识牌资产覆盖的 40F–50F");
                List<SignPlacement> signs = raid.Floors[f].Plan.Signs;
                Assert.AreEqual("F" + worldFloor, signs.Find(s => s.Kind == SignKind.Floor).Text);
                for (int i = 0; i < signs.Count; i++)
                    if (signs[i].Kind == SignKind.Room)
                        Assert.That(int.Parse(signs[i].Text), Is.InRange(worldFloor * 100 + 1, worldFloor * 100 + 999));
            }
        }

        [Test]
        public void EverySign_ClearsDeclaredDoorsAndCorridorOpenings()
        {
            foreach (FloorPlan plan in Plans())
            foreach (SignPlacement sign in plan.Signs)
            {
                Assert.That(sign.Size.x, Is.GreaterThan(0f));
                Assert.That(sign.Size.y, Is.GreaterThan(0f));
                Assert.That(sign.Normal.magnitude, Is.EqualTo(1f).Within(0.001f));
                RectF safe = sign.Footprint.Inflate(0.1f);
                for (int r = 0; r < plan.Rooms.Count; r++)
                {
                    Room room = plan.Rooms[r];
                    if (room.HasDoor) Assert.IsFalse(Hits(safe, room.Door), "sign covers room door " + sign.Text);
                    for (int d = 0; d < room.ExtraDoors.Count; d++)
                        Assert.IsFalse(Hits(safe, room.ExtraDoors[d]), "sign covers extra door " + sign.Text);
                }
                for (int d = 0; d < plan.LoopDoors.Count; d++) Assert.IsFalse(Hits(safe, plan.LoopDoors[d]));
                for (int d = 0; d < plan.CorridorOpenings.Count; d++) Assert.IsFalse(Hits(safe, plan.CorridorOpenings[d]));
            }
        }

        static bool Hits(RectF sign, Door door)
        {
            bool horizontal = door.Side == DoorSide.North || door.Side == DoorSide.South;
            Vector2 size = horizontal ? new Vector2(door.Width, 0.35f) : new Vector2(0.35f, door.Width);
            return sign.Overlaps(new RectF(door.Center.x - size.x * 0.5f, door.Center.y - size.y * 0.5f, size.x, size.y));
        }

        [Test]
        public void SignPlacement_IsDeterministic()
        {
            FloorPlan a = FloorGenerator.Generate(FloorSpec.Default(17));
            FloorPlan b = FloorGenerator.Generate(FloorSpec.Default(17));
            Assert.AreEqual(a.Signs.Count, b.Signs.Count);
            for (int i = 0; i < a.Signs.Count; i++)
            {
                Assert.AreEqual(a.Signs[i].Kind, b.Signs[i].Kind);
                Assert.AreEqual(a.Signs[i].Text, b.Signs[i].Text);
                Assert.AreEqual(a.Signs[i].Center, b.Signs[i].Center);
                Assert.AreEqual(a.Signs[i].Normal, b.Signs[i].Normal);
            }
        }

        [Test]
        public void ChunkManifests_AssignEverySignExactlyOnce()
        {
            FloorPlan plan = Plans()[0];
            FloorChunkGrid grid = new FloorChunkGrid(plan);
            FloorChunkManifest[] manifests = FloorChunkPlanner.Plan(plan, grid);
            int total = 0;
            for (int i = 0; i < manifests.Length; i++)
            {
                Assert.AreSame(plan, manifests[i].SourcePlan);
                total += manifests[i].Signs.Count;
                for (int s = 0; s < manifests[i].Signs.Count; s++)
                    Assert.AreEqual(i, grid.IndexAt(manifests[i].Signs[s].Center));
            }
            Assert.AreEqual(plan.Signs.Count, total);
        }

        [Test]
        public void SingleShotAndChunkBuilders_InstantiateTheirSigns()
        {
            FloorPlan plan = Plans()[0];
            GameObject floor = null, chunk = null;
            try
            {
                floor = FloorGeometry.Build(plan, null, null);
                Assert.IsNotNull(floor.transform.Find("Signage"));
                Assert.AreEqual(plan.Signs.Count, floor.transform.Find("Signage").childCount);
                FloorChunkGrid grid = new FloorChunkGrid(plan);
                FloorChunkManifest manifest = FloorChunkPlanner.Plan(plan, grid)[0];
                List<Mesh> meshes;
                chunk = FloorChunkGeometry.BuildChunk(manifest, null, null, out meshes);
                Assert.IsNotNull(chunk.transform.Find("Signage"));
                Assert.AreEqual(manifest.Signs.Count, chunk.transform.Find("Signage").childCount);
                for (int i = 0; i < meshes.Count; i++) if (meshes[i] != null) Object.DestroyImmediate(meshes[i]);
            }
            finally
            {
                if (floor != null) Object.DestroyImmediate(floor);
                if (chunk != null) Object.DestroyImmediate(chunk);
            }
        }

        [Test]
        public void Capture_DynamicFloorRoomAndRealEvacuationMap()
        {
            FloorPlan plan = Plans()[0];
            var signs = new List<SignPlacement>();
            SignPlacement floor = plan.Signs.Find(s => s.Kind == SignKind.Floor);
            floor.Center = new Vector2(0f, 0f); floor.Normal = Vector2.down; floor.Size = new Vector2(1f, 0.65f); floor.Elevation = 1.5f;
            SignPlacement room = plan.Signs.Find(s => s.Kind == SignKind.Room);
            room.Center = new Vector2(1.35f, 0f); room.Normal = Vector2.down; room.Size = new Vector2(1f, 0.5f); room.Elevation = 1.5f;
            SignPlacement evac = plan.Signs.Find(s => s.Kind == SignKind.Evacuation);
            evac.Center = new Vector2(3.45f, 0f); evac.Normal = Vector2.down; evac.Size = new Vector2(2.4f, 1.8f); evac.Elevation = 1.5f;
            signs.Add(floor); signs.Add(room); signs.Add(evac);

            GameObject root = null;
            GameObject cameraObject = new GameObject("R9_CaptureCamera");
            RenderTexture target = new RenderTexture(1400, 700, 24);
            RenderTexture previous = RenderTexture.active;
            try
            {
                root = SignageRenderer.Build(plan, signs, null);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 1.25f;
                cameraObject.transform.position = new Vector3(1.65f, 1.5f, -5f);
                cameraObject.transform.LookAt(new Vector3(1.65f, 1.5f, 0f));
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.08f, 0.09f, 0.11f);
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                Texture2D image = new Texture2D(1400, 700, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1400, 700), 0, 0);
                image.Apply();
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../TestResults"));
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "r9-signage.png");
                File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                Assert.Greater(new FileInfo(path).Length, 5000);
            }
            finally
            {
                RenderTexture.active = previous;
                if (root != null) Object.DestroyImmediate(root);
                Object.DestroyImmediate(cameraObject);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }
    }
}
