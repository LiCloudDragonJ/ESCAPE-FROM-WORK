using System.Collections.Generic;
using EscapeFromWork.LevelGen;
using NUnit.Framework;
using UnityEngine;

/// <summary>走廊掩体：两侧都要有、大中小混刷、贴墙不外溢、留出通行净宽、不堵门。</summary>
public class CoverPropsTests
{
    static List<FloorPlan> Plans()
    {
        List<FloorPlan> plans = new List<FloorPlan>();
        for (int seed = 1; seed <= 6; seed++)
            plans.Add(FloorGenerator.Generate(new FloorSpec { Seed = seed }));
        return plans;
    }

    [Test]
    public void Props_SpawnOnBothSidesWithAllThreeSizes()
    {
        foreach (FloorPlan plan in Plans())
        {
            int near = 0, far = 0;
            bool[] size = new bool[3];
            for (int i = 0; i < plan.Props.Count; i++)
            {
                PropPlacement p = plan.Props[i];
                if (p.FarSide) far++; else near++;
                size[(int)p.Spec.Size] = true;
            }

            Assert.That(plan.Props.Count, Is.GreaterThan(60), "走廊上应该有掩体");
            Assert.That(near, Is.GreaterThan(8), "走廊近侧要刷掩体");
            Assert.That(far, Is.GreaterThan(8), "走廊远侧也要刷掩体");
            Assert.That(size[0], Is.True, "大件缺了");
            Assert.That(size[1], Is.True, "中件缺了");
            Assert.That(size[2], Is.True, "小件缺了");
        }
    }

    [Test]
    public void Props_StayInsideCorridor()
    {
        foreach (FloorPlan plan in Plans())
        {
            for (int i = 0; i < plan.Props.Count; i++)
            {
                PropPlacement p = plan.Props[i];
                RectF c = plan.Corridors[p.CorridorIndex].Rect;
                Assert.That(p.Rect.X, Is.GreaterThanOrEqualTo(c.X - 0.01f));
                Assert.That(p.Rect.Y, Is.GreaterThanOrEqualTo(c.Y - 0.01f));
                Assert.That(p.Rect.XMax, Is.LessThanOrEqualTo(c.XMax + 0.01f));
                Assert.That(p.Rect.YMax, Is.LessThanOrEqualTo(c.YMax + 0.01f));
            }
        }
    }

    [Test]
    public void Props_KeepWalkableLane()
    {
        foreach (FloorPlan plan in Plans())
        {
            for (int ci = 0; ci < plan.Corridors.Count; ci++)
            {
                RectF c = plan.Corridors[ci].Rect;
                bool horiz = c.W >= c.H;
                float width = horiz ? c.H : c.W;
                float length = horiz ? c.W : c.H;
                if (width < 3.0f) continue;
                float origin = horiz ? c.X : c.Y;

                // 沿走廊每 0.5m 采样：任意一处都得留出通行净宽（隔断墙也只占一侧）
                for (float t = 0.25f; t < length; t += 0.5f)
                {
                    float along = origin + t;
                    float nearDepth = 0f, farDepth = 0f;
                    for (int i = 0; i < plan.Props.Count; i++)
                    {
                        if (plan.Props[i].CorridorIndex != ci) continue;
                        RectF p = plan.Props[i].Rect;
                        float s = horiz ? p.X : p.Y;
                        float e = horiz ? p.XMax : p.YMax;
                        if (along < s || along > e) continue;
                        float d = horiz ? p.H : p.W;
                        if (plan.Props[i].FarSide) farDepth = Mathf.Max(farDepth, d);
                        else nearDepth = Mathf.Max(nearDepth, d);
                    }

                    Assert.That(width - nearDepth - farDepth, Is.GreaterThanOrEqualTo(plan.Spec.CorridorClearLane - 0.01f),
                        "走廊净宽被掩体吃掉太多");
                }
            }
        }
    }

    [Test]
    public void LongCorridors_AreBrokenByArchitectureNotProps()
    {
        // R2 之前是在走廊里砌"隔断墙"道具断视线，那套已经删了：断点改成建筑动作
        // （走廊每 28m 切段 + 门斗 + 并行服务廊）。所以这里不再数道具，而是数段长与门斗。
        foreach (FloorPlan plan in Plans())
        {
            Assert.That(CorridorBreaks.CountVestibules(plan), Is.GreaterThan(10), "整层没有建筑断点");

            for (int ci = 0; ci < plan.Corridors.Count; ci++)
            {
                RectF c = plan.Corridors[ci].Rect;
                float length = Mathf.Max(c.W, c.H);
                Assert.That(length, Is.LessThanOrEqualTo(32.5f), "有一段走廊超过 32.5m，视线能一路打穿");
            }
        }
    }

    [Test]
    public void LargeRooms_HaveMultipleDoors()
    {
        foreach (FloorPlan plan in Plans())
        {
            int large = 0, multi = 0;
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                Room r = plan.Rooms[i];
                if (r.Rect.Area < 80f) continue;
                large++;
                int doors = r.HasDoor ? 1 : 0;
                if (r.ExtraDoors != null) doors += r.ExtraDoors.Count;
                if (doors >= 2) multi++;
            }

            Assert.That(large, Is.GreaterThan(10), "大房间太少");
            Assert.That(multi / (float)large, Is.GreaterThan(0.5f), "大房间多门比例太低，堵一个门就没路");
        }
    }

    [Test]
    public void Props_DoNotBlockDoors()
    {
        foreach (FloorPlan plan in Plans())
        {
            List<Vector2> doors = new List<Vector2>();
            for (int i = 0; i < plan.Rooms.Count; i++)
                if (plan.Rooms[i].HasDoor) doors.Add(plan.Rooms[i].Door.Center);

            for (int i = 0; i < plan.Props.Count; i++)
            {
                RectF pr = plan.Props[i].Rect;
                for (int d = 0; d < doors.Count; d++)
                {
                    Vector2 c = doors[d];
                    float dx = Mathf.Max(pr.X - c.x, 0f, c.x - pr.XMax);
                    float dy = Mathf.Max(pr.Y - c.y, 0f, c.y - pr.YMax);
                    Assert.That(Mathf.Sqrt(dx * dx + dy * dy), Is.GreaterThanOrEqualTo(1.0f), "掩体堵门了");
                }
            }
        }
    }

    [Test]
    public void Props_DoNotOverlapEachOther()
    {
        foreach (FloorPlan plan in Plans())
        {
            for (int i = 0; i < plan.Props.Count; i++)
                for (int j = i + 1; j < plan.Props.Count; j++)
                    Assert.That(plan.Props[i].Rect.Overlaps(plan.Props[j].Rect), Is.False, "掩体互相重叠");
        }
    }
}
