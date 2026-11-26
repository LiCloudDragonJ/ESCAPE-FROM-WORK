using System.Collections.Generic;
using EscapeFromWork.LevelGen;
using NUnit.Framework;
using UnityEngine;

namespace EscapeFromWork.Tests.EditMode
{
    /// <summary>
    /// R4 验收：设施语法库要真的把"办公室"换成"用途混杂的设施层"。
    /// 断言只看生成数据（主题/设施/面积/家具/墙垛），不依赖场景。
    /// </summary>
    public class FacilityTests
    {
        static readonly int[] Seeds = { 1, 2, 3, 7, 11, 19 };

        static FloorPlan Plan(int seed)
        {
            return FloorGenerator.Generate(FloorSpec.Default(seed));
        }

        [Test]
        public void EveryFloorMixesAtLeastEightFacilitiesAndTwoThemes()
        {
            foreach (int seed in Seeds)
            {
                FloorPlan plan = Plan(seed);
                var kinds = new HashSet<FacilityKind>();
                for (int i = 0; i < plan.Rooms.Count; i++) kinds.Add(plan.Rooms[i].Facility);

                var themes = new HashSet<FloorTheme>();
                for (int i = 0; i < plan.Zones.Count; i++) themes.Add(plan.Zones[i].Theme);

                Assert.That(kinds.Count, Is.GreaterThanOrEqualTo(8),
                    "seed " + seed + " 只有 " + kinds.Count + " 种设施，设施层应 ≥8");
                Assert.That(themes.Count, Is.GreaterThanOrEqualTo(2),
                    "seed " + seed + " 只有 " + themes.Count + " 个主题");
            }
        }

        [Test]
        public void FacilitySizesActuallyDiffer()
        {
            // 用机房大厅/车间这类"大设施"对比管井/卫生间这类"小设施"：
            // 中位数比值 <2 说明面积区间没生效，房间还是一种尺度。
            foreach (int seed in new[] { 1, 2, 3 })
            {
                FloorPlan plan = Plan(seed);
                var byKind = new Dictionary<FacilityKind, List<float>>();
                for (int i = 0; i < plan.Rooms.Count; i++)
                {
                    List<float> list;
                    if (!byKind.TryGetValue(plan.Rooms[i].Facility, out list))
                    {
                        list = new List<float>();
                        byKind[plan.Rooms[i].Facility] = list;
                    }
                    list.Add(RoomShape.AreaOf(plan.Rooms[i]));
                }

                float big = Median(byKind, FacilityKind.ServerHall, FacilityKind.Workshop, FacilityKind.LoadingDock);
                float small = Median(byKind, FacilityKind.Riser, FacilityKind.Toilet, FacilityKind.Storage);
                Assert.That(big, Is.GreaterThan(0f), "seed " + seed + " 没有大设施");
                Assert.That(small, Is.GreaterThan(0f), "seed " + seed + " 没有小设施");
                Assert.That(big / small, Is.GreaterThan(2f),
                    "seed " + seed + " 大设施中位 " + big.ToString("0") + "㎡ / 小设施中位 " +
                    small.ToString("0") + "㎡，比值应 >2");
            }
        }

        static float Median(Dictionary<FacilityKind, List<float>> byKind, params FacilityKind[] kinds)
        {
            var all = new List<float>();
            foreach (FacilityKind kind in kinds)
            {
                List<float> list;
                if (byKind.TryGetValue(kind, out list)) all.AddRange(list);
            }
            if (all.Count == 0) return 0f;
            all.Sort();
            return all[all.Count / 2];
        }

        [Test]
        public void RoomsGetFacilityProps_AndBigHallsGetStructuralBreaks()
        {
            foreach (int seed in new[] { 1, 2 })
            {
                FloorPlan plan = Plan(seed);

                var propsByRoom = new Dictionary<int, int>();
                for (int i = 0; i < plan.Props.Count; i++)
                {
                    int index = RoomIndexAt(plan, plan.Props[i].Rect.Center);
                    int n;
                    propsByRoom.TryGetValue(index, out n);
                    propsByRoom[index] = n + 1;
                }

                int roomsWithProps = 0, bigRooms = 0;
                for (int i = 0; i < plan.Rooms.Count; i++)
                {
                    float area = RoomShape.AreaOf(plan.Rooms[i]);
                    if (area < 60f) continue;
                    bigRooms++;
                    int n;
                    if (propsByRoom.TryGetValue(i, out n) && n > 0) roomsWithProps++;
                }

                Assert.That(roomsWithProps, Is.GreaterThan(bigRooms / 2),
                    "seed " + seed + " 只有 " + roomsWithProps + "/" + bigRooms + " 间大房间有家具");
                Assert.That(plan.HallBreaks.Count, Is.GreaterThan(0),
                    "seed " + seed + " 没有任何大厅墙垛/挡视屏");
            }
        }

        static int RoomIndexAt(FloorPlan plan, Vector2 p)
        {
            for (int i = 0; i < plan.Rooms.Count; i++)
                if (plan.Rooms[i].Rect.Contains(p)) return i;
            return -1;
        }

        [Test]
        public void FacilityAssignmentIsDeterministic()
        {
            FloorPlan a = Plan(5);
            FloorPlan b = Plan(5);
            Assert.AreEqual(a.Rooms.Count, b.Rooms.Count);
            for (int i = 0; i < a.Rooms.Count; i++)
            {
                Assert.AreEqual(a.Rooms[i].Facility, b.Rooms[i].Facility, "第 " + i + " 间房的设施不一致");
                Assert.AreEqual(a.Rooms[i].ZoneIndex, b.Rooms[i].ZoneIndex);
            }
        }
    }
}
