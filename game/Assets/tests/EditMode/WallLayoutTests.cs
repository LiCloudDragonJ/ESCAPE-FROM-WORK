using System.Collections.Generic;
using EscapeFromWork.LevelGen;
using NUnit.Framework;

namespace EscapeFromWork.Tests
{
    /// <summary>
    /// 墙只能有一层：相邻房间共享同一条边界线时必须合并成一道 0.2 m 隔墙，
    /// 不能各自留一道墙、中间夹一条能看穿的空腔（旧实现的"悬空纸片墙"）。
    /// </summary>
    public class WallLayoutTests
    {
        static bool Horizontal(BoxSpec b) { return b.Size.x >= b.Size.z; }

        static float SpanStart(BoxSpec b, bool horizontal) { return horizontal ? b.Center.x - b.Size.x * 0.5f : b.Center.z - b.Size.z * 0.5f; }
        static float SpanEnd(BoxSpec b, bool horizontal) { return horizontal ? b.Center.x + b.Size.x * 0.5f : b.Center.z + b.Size.z * 0.5f; }
        static float Line(BoxSpec b, bool horizontal) { return horizontal ? b.Center.z : b.Center.x; }

        [Test]
        public void Walls_NeverStackTwoLayersOnTheSameLine(
            [Values(1, 3, 7, 11, 19)] int seed)
        {
            FloorSpec spec = FloorSpec.Default(seed);
            spec.Band = FloorBand.Tower;
            spec.Width = 200f; spec.Height = 160f;
            FloorPlan plan = FloorGenerator.Generate(spec);

            List<BoxSpec> boxes = new List<BoxSpec>();
            FloorGeometry.CollectWallBoxes(plan, boxes);
            Assert.Greater(boxes.Count, 50, "seed " + seed + " walls=" + boxes.Count);

            for (int i = 0; i < boxes.Count; i++)
            {
                for (int j = i + 1; j < boxes.Count; j++)
                {
                    bool hi = Horizontal(boxes[i]);
                    bool hj = Horizontal(boxes[j]);
                    if (hi != hj) continue;
                    if (System.Math.Abs(Line(boxes[i], hi) - Line(boxes[j], hj)) > 0.02f) continue;
                    float overlap = System.Math.Min(SpanEnd(boxes[i], hi), SpanEnd(boxes[j], hj))
                                  - System.Math.Max(SpanStart(boxes[i], hi), SpanStart(boxes[j], hj));
                    Assert.LessOrEqual(overlap, 0.05f,
                        "seed " + seed + " 同一条墙线上叠了两层墙（会夹出空腔）：overlap=" + overlap);
                }
            }
        }

        [Test]
        public void EveryDoorHasALintelAboveIt(
            [Values(1, 5, 9)] int seed)
        {
            FloorSpec spec = FloorSpec.Default(seed);
            spec.Band = FloorBand.Tower;
            spec.Width = 200f; spec.Height = 160f;
            FloorPlan plan = FloorGenerator.Generate(spec);

            List<BoxSpec> boxes = new List<BoxSpec>();
            FloorGeometry.CollectWallBoxes(plan, boxes);

            List<Door> doors = new List<Door>();
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                if (plan.Rooms[i].HasDoor) doors.Add(plan.Rooms[i].Door);
                if (plan.Rooms[i].ExtraDoors != null) doors.AddRange(plan.Rooms[i].ExtraDoors);
            }
            doors.AddRange(plan.LoopDoors);
            Assert.Greater(doors.Count, 20, "seed " + seed + " doors=" + doors.Count);

            float lintelY = (FloorGeometry.DoorHeight + FloorGeometry.CeilingY) * 0.5f;
            for (int d = 0; d < doors.Count; d++)
            {
                Door door = doors[d];
                bool hasLintel = false;
                bool blockedAtBodyHeight = false;
                for (int b = 0; b < boxes.Count; b++)
                {
                    BoxSpec box = boxes[b];
                    float dx = System.Math.Abs(box.Center.x - door.Center.x);
                    float dz = System.Math.Abs(box.Center.z - door.Center.y);
                    if (dx > 0.35f || dz > 0.35f) continue;
                    if (System.Math.Abs(box.Center.y - lintelY) <= 0.05f) hasLintel = true;
                    if (box.Center.y < FloorGeometry.DoorHeight - 0.05f) blockedAtBodyHeight = true;
                }
                // 短墙段（< 0.6 m）会被整段删掉，门变成通高门洞，此时不需要门楣；
                // 但只要该处还有墙，就必须有门楣，否则就是"头顶悬空的洞"。
                Assert.IsTrue(hasLintel || !blockedAtBodyHeight,
                    "seed " + seed + " 门 (" + door.Center.x + "," + door.Center.y + ") 有墙无门楣");
            }
        }
    }
}
