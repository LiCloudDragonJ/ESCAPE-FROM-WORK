using System.Collections.Generic;
using EscapeFromWork.LevelGen;
using NUnit.Framework;

namespace EscapeFromWork.Tests.EditMode
{
    /// <summary>
    /// R3 房间尺度验收：面积一律按真实轮廓（Outline 鞋带面积）算，不看包围盒——
    /// 贴着外廓/核心筒裁出来的房间 Rect 会虚大，用 Rect.Area 统计会把小房间算成中房间。
    ///
    /// 分档口径（2026-11-26 与用户确认）：56–113 / 113–200 / 200–400 / 400+ 约 40/35/20/5，
    /// &lt;56㎡ 只能是服务间（储藏/卫生间/管井）且占比 ≤10%。
    /// 单层只有 100 多间房，逐种子卡死分档会让参数一动就红；所以分档与中位数按 12 个种子**合并**统计，
    /// 单种子只卡"&lt;56㎡ 不超过 12%"这条软上限（防止某一层被边角料灌满）。
    /// </summary>
    public class RoomScaleTests
    {
        static readonly int[] Seeds = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };

        class Sample
        {
            public readonly List<float> Areas = new List<float>();
            public readonly List<Room> Rooms = new List<Room>();
            public readonly List<float> PerFloorTinyShare = new List<float>();
        }

        static Sample Gather()
        {
            Sample s = new Sample();
            for (int i = 0; i < Seeds.Length; i++)
            {
                FloorPlan plan = FloorGenerator.Generate(FloorSpec.Default(Seeds[i]));
                int tiny = 0;
                for (int r = 0; r < plan.Rooms.Count; r++)
                {
                    float a = RoomShape.AreaOf(plan.Rooms[r]);
                    s.Areas.Add(a);
                    s.Rooms.Add(plan.Rooms[r]);
                    if (a < 56f) tiny++;
                }
                s.PerFloorTinyShare.Add(plan.Rooms.Count == 0 ? 0f : (float)tiny / plan.Rooms.Count);
            }
            return s;
        }

        static float Median(List<float> values)
        {
            List<float> copy = new List<float>(values);
            copy.Sort();
            int n = copy.Count;
            if (n == 0) return 0f;
            return n % 2 == 1 ? copy[n / 2] : (copy[n / 2 - 1] + copy[n / 2]) * 0.5f;
        }

        static float Share(List<float> values, float min, float max)
        {
            int hit = 0;
            for (int i = 0; i < values.Count; i++)
                if (values[i] >= min && values[i] < max) hit++;
            return values.Count == 0 ? 0f : (float)hit / values.Count;
        }

        [Test]
        public void MedianRoomArea_IsNinetyToOneThirty()
        {
            Sample s = Gather();
            float median = Median(s.Areas);
            Assert.Greater(s.Areas.Count, 1200, "12 个种子合计至少 1200 间房才有统计意义");
            Assert.That(median, Is.InRange(90f, 130f), "房间面积中位数应落在 90–130㎡");
        }

        [Test]
        public void TinyRooms_StayUnderTenPercent()
        {
            Sample s = Gather();
            float share = Share(s.Areas, 0f, 56f);
            Assert.LessOrEqual(share, 0.10f, "<56㎡ 的房间占比应 ≤10%");
            for (int i = 0; i < s.PerFloorTinyShare.Count; i++)
                Assert.LessOrEqual(s.PerFloorTinyShare[i], 0.12f,
                    "种子 " + Seeds[i] + " 的 <56㎡ 占比超过 12%：这一层被边角料灌满了");
        }

        [Test]
        public void AreaHistogram_MatchesFortyThirtyFiveTwentyFive()
        {
            Sample s = Gather();
            float b1 = Share(s.Areas, 56f, 113f);
            float b2 = Share(s.Areas, 113f, 200f);
            float b3 = Share(s.Areas, 200f, 400f);
            float b4 = Share(s.Areas, 400f, 100000f);
            Assert.That(b1, Is.InRange(0.32f, 0.48f), "56–113㎡ 应占 32–48%");
            Assert.That(b2, Is.InRange(0.28f, 0.44f), "113–200㎡ 应占 28–44%");
            Assert.That(b3, Is.InRange(0.10f, 0.24f), "200–400㎡ 应占 10–24%");
            Assert.That(b4, Is.InRange(0.00f, 0.08f), "400㎡+ 应占 0–8%");
        }

        [Test]
        public void OnlySmallRooms_GetServiceTypes()
        {
            Sample s = Gather();
            int service = 0;
            for (int i = 0; i < s.Rooms.Count; i++)
            {
                Room room = s.Rooms[i];
                float a = s.Areas[i];
                if (room.Type == RoomType.Normal)
                {
                    Assert.GreaterOrEqual(a, 56f, "Normal 房间不能小于 56㎡");
                }
                else
                {
                    Assert.Less(a, 56f, "只有 <56㎡ 的小间才允许是服务间（" + room.Type + "）");
                    Assert.AreNotEqual(RoomType.Vault, room.Type, "保险库留给 R4 机制层，当前不应生成");
                    service++;
                }
            }
            Assert.Greater(service, 0, "12 个种子合计应该出现服务间");
        }
    }
}
