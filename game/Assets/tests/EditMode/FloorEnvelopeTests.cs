using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EscapeFromWork.LevelGen;

namespace EscapeFromWork.Tests
{
    /// <summary>
    /// 楼层不再是矩形板：外廓有 45° 截角和缺口，房间是贴着外廓裁出来的多边形。
    /// 这组测试守住三件事：外廓真的不是矩形、外廓不会吃掉核心筒/楼梯、房间轮廓都落在外廓里。
    /// </summary>
    public class FloorEnvelopeTests
    {
        static readonly FloorPrototype[] Protos =
        {
            FloorPrototype.OpenOffice, FloorPrototype.Meeting, FloorPrototype.SmallOffice,
            FloorPrototype.Utility, FloorPrototype.Executive, FloorPrototype.Renovation,
        };
        static readonly FloorBand[] Bands = { FloorBand.Tower, FloorBand.Podium, FloorBand.Top };

        static FloorPlan PlanFor(int seed)
        {
            FloorSpec spec = FloorSpec.Default(seed);
            spec.Prototype = Protos[seed % Protos.Length];
            spec.Band = Bands[seed % Bands.Length];
            return FloorGenerator.Generate(spec);
        }

        [Test]
        public void Envelope_IsNotTheRectangularPlate([Values(1, 2, 3, 4, 5, 6, 7, 8)] int seed)
        {
            FloorPlan plan = PlanFor(seed);
            Assert.IsNotNull(plan.Envelope, "seed " + seed + ": no envelope");
            Assert.Less(plan.Envelope.Area, plan.Bounds.Area * 0.968f,
                "seed " + seed + ": envelope " + plan.Envelope.Area + " is still the full plate " + plan.Bounds.Area);

            int diagonals = 0;
            Vector2[] outline = plan.Envelope.Outline;
            for (int i = 0; i < outline.Length; i++)
            {
                Vector2 a = outline[i];
                Vector2 b = outline[(i + 1) % outline.Length];
                float dx = Mathf.Abs(b.x - a.x);
                float dy = Mathf.Abs(b.y - a.y);
                if (dx > 1f && dy > 1f && Mathf.Abs(dx - dy) < 0.05f) diagonals++;
            }
            Assert.GreaterOrEqual(diagonals, 1, "seed " + seed + ": envelope has no 45 degree edge");
        }

        [Test]
        public void Envelope_StaysInsideThePlateAndTriangulates([Values(1, 3, 5, 9)] int seed)
        {
            FloorPlan plan = PlanFor(seed);
            Vector2[] outline = plan.Envelope.Outline;
            Assert.GreaterOrEqual(outline.Length, 4);
            for (int i = 0; i < outline.Length; i++)
            {
                Assert.GreaterOrEqual(outline[i].x, -0.01f, "seed " + seed + ": outline leaves the plate on x");
                Assert.LessOrEqual(outline[i].x, plan.Bounds.W + 0.01f, "seed " + seed + ": outline leaves the plate on x");
                Assert.GreaterOrEqual(outline[i].y, -0.01f, "seed " + seed + ": outline leaves the plate on z");
                Assert.LessOrEqual(outline[i].y, plan.Bounds.H + 0.01f, "seed " + seed + ": outline leaves the plate on z");
            }

            float pieceArea = 0f;
            for (int i = 0; i < plan.Envelope.Pieces.Count; i++)
                pieceArea += Mathf.Abs(FloorEnvelope.SignedArea(plan.Envelope.Pieces[i]));
            Assert.AreEqual(plan.Envelope.Area, pieceArea, plan.Envelope.Area * 0.02f, "seed " + seed + ": convex pieces do not add up to the outline");

            List<int> tris = FloorEnvelope.Triangulate(outline);
            Assert.AreEqual((outline.Length - 2) * 3, tris.Count, "seed " + seed + ": triangulation did not produce n-2 triangles");
            float triArea = 0f;
            for (int i = 0; i < tris.Count; i += 3)
            {
                Vector2 a = outline[tris[i]], b = outline[tris[i + 1]], c = outline[tris[i + 2]];
                triArea += Mathf.Abs((b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y)) * 0.5f;
            }
            Assert.AreEqual(plan.Envelope.Area, triArea, plan.Envelope.Area * 0.02f, "seed " + seed + ": triangles do not add up to the outline");
        }

        [Test]
        public void Envelope_KeepsCoreAndStairsInside([Values(1, 2, 3, 4, 5, 6, 7, 8, 9, 10)] int seed)
        {
            FloorPlan plan = PlanFor(seed);
            Vector2[] outline = plan.Envelope.Outline;
            Assert.IsTrue(FloorEnvelope.Contains(outline, plan.Core.Center), "seed " + seed + ": notch ate the core");
            for (int i = 0; i < plan.Stairs.Count; i++)
                Assert.IsTrue(FloorEnvelope.Contains(outline, plan.Stairs[i].Center), "seed " + seed + ": notch ate a stair");
        }

        [Test]
        public void Rooms_AreClippedToTheEnvelope([Values(1, 2, 3, 4, 5, 6, 7, 8)] int seed)
        {
            FloorPlan plan = PlanFor(seed);
            Vector2[] outline = plan.Envelope.Outline;
            int conforming = 0;

            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                Room room = plan.Rooms[i];
                if (room.Outline == null || room.Outline.Length < 3) continue;
                float area = Mathf.Abs(FloorEnvelope.SignedArea(room.Outline));
                Assert.Greater(area, 1f, "seed " + seed + " room " + i + ": degenerate outline");
                Assert.LessOrEqual(area, room.Rect.Area + 0.5f, "seed " + seed + " room " + i + ": outline is bigger than its rect");

                if (area < room.Rect.Area - 1f) conforming++;

                for (int v = 0; v < room.Outline.Length; v++)
                {
                    Vector2 p = room.Outline[v];
                    bool inside = FloorEnvelope.Contains(outline, p) || FloorEnvelope.DistanceToEdge(outline, p) < 0.05f;
                    Assert.IsTrue(inside, "seed " + seed + " room " + i + ": vertex " + p + " is outside the envelope");
                }
            }

            Assert.GreaterOrEqual(conforming, 1, "seed " + seed + ": no room follows the envelope shape at all");
        }

        [Test]
        public void Rooms_StillFillTheFloor([Values(1, 2, 3, 4)] int seed)
        {
            FloorPlan plan = PlanFor(seed);
            Assert.Greater(plan.Rooms.Count, 40, "seed " + seed + ": envelope clipping deleted too many rooms");
            float roomArea = 0f;
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                Room room = plan.Rooms[i];
                roomArea += room.Outline != null && room.Outline.Length >= 3
                    ? Mathf.Abs(FloorEnvelope.SignedArea(room.Outline))
                    : room.Rect.Area;
            }
            Assert.Greater(roomArea, plan.Envelope.Area * 0.25f, "seed " + seed + ": rooms cover too little of the envelope");
        }
    }
}
