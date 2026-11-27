using System.Collections.Generic;
using EscapeFromWork.LevelGen;
using NUnit.Framework;
using UnityEngine;

namespace EscapeFromWork.Tests.EditMode
{
    /// <summary>
    /// 设施分区视觉落地：每个房间都要有地面材质槽与墙面材质槽，几何要按槽合并出
    /// RoomFloor_* / RoomBand_* 网格；面层必须是贴地的薄片，墙裙不能高过门楣。
    /// </summary>
    public class RoomFinishTests
    {
        static FloorPlan TowerPlan(int seed)
        {
            FloorSpec spec = FloorSpec.Default(seed);
            spec.Band = FloorBand.Tower;
            spec.Width = 200f;
            spec.Height = 160f;
            return FloorGenerator.Generate(spec);
        }

        static List<string> SlotsFor(FloorPlan plan, bool floor)
        {
            List<string> slots = new List<string>();
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                FacilityProfile profile = FacilityGrammar.Get(plan.Rooms[i].Facility);
                string slot = floor ? profile.FloorMaterial : profile.WallMaterial;
                if (!string.IsNullOrEmpty(slot) && !slots.Contains(slot)) slots.Add(slot);
            }
            return slots;
        }

        [Test]
        public void EveryRoom_CarriesASurfaceSlot(
            [Values(1, 3)] int seed)
        {
            FloorPlan plan = TowerPlan(seed);
            Assert.Greater(plan.Rooms.Count, 5, "楼层应有足够多的房间");

            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                FacilityProfile profile = FacilityGrammar.Get(plan.Rooms[i].Facility);
                Assert.IsFalse(string.IsNullOrEmpty(profile.FloorMaterial),
                    "房间 " + i + "（" + profile.Name + "）缺地面材质槽");
                Assert.IsFalse(string.IsNullOrEmpty(profile.WallMaterial),
                    "房间 " + i + "（" + profile.Name + "）缺墙面材质槽");
            }

            Assert.GreaterOrEqual(SlotsFor(plan, true).Count, 3,
                "一层楼只有一种地面就还是办公室——设施分区至少要三种地面");
        }

        [Test]
        public void RoomFloorOverlay_IsAThinSlabAboveTheStructure(
            [Values(1, 3)] int seed)
        {
            FloorPlan plan = TowerPlan(seed);
            int checkedRooms = 0;
            for (int i = 0; i < plan.Rooms.Count && i < 6; i++)
            {
                FloorGeometry.MeshAccum acc = new FloorGeometry.MeshAccum();
                FloorGeometry.AddRoomFloor(plan, plan.Rooms[i], acc);
                if (acc.Vertices.Count == 0) continue; // 房间被外廓裁没了

                Bounds bounds = new Bounds(acc.Vertices[0], Vector3.zero);
                for (int v = 1; v < acc.Vertices.Count; v++) bounds.Encapsulate(acc.Vertices[v]);
                Assert.GreaterOrEqual(bounds.min.y, -0.001f, "面层不能沉到楼板里");
                Assert.LessOrEqual(bounds.max.y, 0.02f, "面层应是 12 mm 薄片");
                RectF rect = plan.Rooms[i].Rect;
                Assert.GreaterOrEqual(bounds.min.x, rect.X - 0.05f, "面层超出房间西界");
                Assert.LessOrEqual(bounds.max.x, rect.XMax + 0.05f, "面层超出房间东界");
                Assert.GreaterOrEqual(bounds.min.z, rect.Y - 0.05f, "面层超出房间南界");
                Assert.LessOrEqual(bounds.max.z, rect.YMax + 0.05f, "面层超出房间北界");
                checkedRooms++;
            }
            Assert.Greater(checkedRooms, 0, "至少检查到一个房间面层");
        }

        [Test]
        public void WallBand_RisesToItsFacilityHeight(
            [Values(1, 3)] int seed)
        {
            FloorPlan plan = TowerPlan(seed);
            int checkedRooms = 0;
            for (int i = 0; i < plan.Rooms.Count && checkedRooms < 4; i++)
            {
                Room room = plan.Rooms[i];
                if (room.Rect.W < 2.5f || room.Rect.H < 2.5f) continue;
                FacilityProfile profile = FacilityGrammar.Get(room.Facility);
                float height = profile.WallBandHeight;
                Assert.Greater(height, 0.5f, profile.Name + " 的墙裙高度必须大于踢脚");

                FloorGeometry.MeshAccum acc = new FloorGeometry.MeshAccum();
                FloorGeometry.AddRoomWallBand(room, acc, height);
                if (acc.Vertices.Count == 0) continue;

                for (int v = 0; v < acc.Vertices.Count; v++)
                {
                    float y = acc.Vertices[v].y;
                    Assert.GreaterOrEqual(y, -0.001f, "墙裙不能低于地面");
                    Assert.LessOrEqual(y, height + 0.01f,
                        profile.Name + " 的墙裙高过自己的设施高度");
                }
                checkedRooms++;
            }
            Assert.Greater(checkedRooms, 0, "至少检查到一个房间墙裙");
        }

        [Test]
        public void CleanFacilities_BandAlmostReachesTheCeiling()
        {
            // 洁净/机房/冷库靠通高面板区分，办公靠 1.02m 踢脚墙裙——两种高度都要存在
            FacilityProfile clean = FacilityGrammar.Get(FacilityKind.CleanRoom);
            FacilityProfile server = FacilityGrammar.Get(FacilityKind.ServerHall);
            FacilityProfile cold = FacilityGrammar.Get(FacilityKind.ColdRoom);
            FacilityProfile office = FacilityGrammar.Get(FacilityKind.Office);

            Assert.GreaterOrEqual(clean.WallBandHeight, 2.4f, "洁净间墙裙应接近吊顶");
            Assert.GreaterOrEqual(server.WallBandHeight, 2.4f, "机房大厅墙裙应接近吊顶");
            Assert.GreaterOrEqual(cold.WallBandHeight, 2.4f, "冷库墙裙应接近吊顶");
            Assert.LessOrEqual(office.WallBandHeight, 1.1f, "办公区应只做踢脚墙裙");
        }

        [Test]
        public void Build_EmitsOneMeshPerSurfaceSlot(
            [Values(1, 3)] int seed)
        {
            FloorPlan plan = TowerPlan(seed);
            FloorMaterials mats = FloorGeometry.FallbackMaterials();
            List<string> surfaceSlots = new List<string>();
            surfaceSlots.AddRange(SlotsFor(plan, true));
            foreach (string slot in SlotsFor(plan, false))
                if (!surfaceSlots.Contains(slot)) surfaceSlots.Add(slot);

            List<Material> surfaceMats = new List<Material>();
            for (int i = 0; i < surfaceSlots.Count; i++) surfaceMats.Add(mats.Floor);
            mats.SurfaceSlots = surfaceSlots.ToArray();
            mats.SurfaceMaterials = surfaceMats.ToArray();

            GameObject root = FloorGeometry.Build(plan, mats, null);
            try
            {
                int floorMeshes = 0, bandMeshes = 0;
                foreach (Transform child in root.transform)
                {
                    if (child.name.StartsWith("RoomFloor_")) floorMeshes++;
                    if (child.name.StartsWith("RoomBand_")) bandMeshes++;
                }

                Assert.AreEqual(SlotsFor(plan, true).Count, floorMeshes,
                    "每种地面材质槽应合并成一个 RoomFloor_* 网格");
                Assert.GreaterOrEqual(bandMeshes, 1, "应生成墙裙网格");
                foreach (string slot in SlotsFor(plan, true))
                    Assert.IsNotNull(root.transform.Find("RoomFloor_" + slot),
                        "缺少地面网格 RoomFloor_" + slot);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
