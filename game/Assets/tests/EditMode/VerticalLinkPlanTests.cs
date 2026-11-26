using System.Collections.Generic;
using EscapeFromWork.LevelGen;
using NUnit.Framework;
using UnityEngine;

namespace EscapeFromWork.Tests.EditMode
{
    /// <summary>
    /// R6 验收：竖向核（西梯 / 核心筒电梯 / 东梯）一局只算一次，逐层同一坐标。
    /// 对齐是结构保证，所以断言可以直接卡 0.5m 容差，而不是"允许偏移"。
    /// </summary>
    public class VerticalLinkPlanTests
    {
        static readonly int[] Seeds = { 1, 2, 3, 5, 7, 11 };

        [Test]
        public void CoresAlignAcrossFloorsWithinHalfMeter()
        {
            foreach (int seed in Seeds)
            {
                RaidPlan raid = SlicePlanner.Generate(seed);
                Assert.That(raid.CorePlan, Is.Not.Null, "seed " + seed + " 没有核布局");
                Assert.That(raid.CorePlan.Cores.Count, Is.GreaterThanOrEqualTo(3),
                    "seed " + seed + " 竖直核少于 3 个");

                for (int f = 0; f < raid.Floors.Count; f++)
                {
                    FloorPlan plan = raid.Floors[f].Plan;
                    Vector2 coreCenter = plan.Core.Center;
                    Vector2 expected = CoreCenter(raid.CorePlan, VerticalLinkKind.Elevator, 1);
                    Assert.That(Vector2.Distance(coreCenter, expected), Is.LessThan(0.5f),
                        "seed " + seed + " 第 " + f + " 层核心筒与核布局对不上");

                    for (int s = 0; s < plan.Stairs.Count; s++)
                    {
                        float best = float.MaxValue;
                        for (int c = 0; c < raid.CorePlan.Cores.Count; c++)
                        {
                            VerticalCore core = raid.CorePlan.Cores[c];
                            if (core.Kind != VerticalLinkKind.Stair) continue;
                            best = Mathf.Min(best, Vector2.Distance(plan.Stairs[s].Center, core.Center));
                        }
                        Assert.That(best, Is.LessThan(0.5f),
                            "seed " + seed + " 第 " + f + " 层楼梯与核布局对不上（最近偏差 " + best.ToString("0.00") + "m）");
                    }
                }
            }
        }

        static Vector2 CoreCenter(VerticalLinkPlan plan, VerticalLinkKind kind, int column)
        {
            for (int i = 0; i < plan.Cores.Count; i++)
                if (plan.Cores[i].Kind == kind && plan.Cores[i].Column == column) return plan.Cores[i].Center;
            return new Vector2(-999f, -999f);
        }

        [Test]
        public void EveryAdjacentPairHasThreeLinksInAtLeastTwoColumns()
        {
            foreach (int seed in Seeds)
            {
                RaidPlan raid = SlicePlanner.Generate(seed);
                int pairs = raid.Floors.Count - 1;
                Assert.AreEqual(pairs * 3, raid.Links.Count,
                    "seed " + seed + " 通路数应为每对 3 条（实际 " + raid.Links.Count + "）");

                for (int i = 0; i < pairs; i++)
                {
                    int lower = raid.Floors[i].FloorIndex;
                    int upper = raid.Floors[i + 1].FloorIndex;
                    bool hasStair = false, hasElevator = false;
                    var columns = new HashSet<int>();
                    for (int l = 0; l < raid.Links.Count; l++)
                    {
                        VerticalLink link = raid.Links[l];
                        if (link.LowerFloor != lower || link.UpperFloor != upper) continue;
                        columns.Add(link.Column);
                        if (link.Kind == VerticalLinkKind.Stair) hasStair = true;
                        if (link.Kind == VerticalLinkKind.Elevator) hasElevator = true;
                    }
                    Assert.IsTrue(hasStair && hasElevator,
                        "seed " + seed + " " + lower + "→" + upper + " 缺楼梯或电梯");
                    Assert.That(columns.Count, Is.GreaterThanOrEqualTo(2),
                        "seed " + seed + " " + lower + "→" + upper + " 的竖向通路挤在同一个象限");
                }
            }
        }

        [Test]
        public void LinkCoordinatesAreIdenticalBetweenFloorPairs()
        {
            foreach (int seed in Seeds)
            {
                RaidPlan raid = SlicePlanner.Generate(seed);
                var byColumn = new Dictionary<int, Vector2>();
                for (int l = 0; l < raid.Links.Count; l++)
                {
                    VerticalLink link = raid.Links[l];
                    Vector2 seen;
                    if (!byColumn.TryGetValue(link.Column * 10 + (int)link.Kind, out seen))
                    {
                        byColumn[link.Column * 10 + (int)link.Kind] = link.Center;
                        continue;
                    }
                    Assert.That(Vector2.Distance(link.Center, seen), Is.LessThan(0.01f),
                        "seed " + seed + " 相邻层通路坐标漂移（column " + link.Column + "）");
                }
            }
        }

        [Test]
        public void CoresAreInsideOpenAreaOnEveryFloor()
        {
            foreach (int seed in Seeds)
            {
                RaidPlan raid = SlicePlanner.Generate(seed);
                for (int f = 0; f < raid.Floors.Count; f++)
                {
                    SliceFloor floor = raid.Floors[f];
                    for (int c = 0; c < raid.CorePlan.Cores.Count; c++)
                    {
                        Vector2 p = raid.CorePlan.Cores[c].Center;
                        Assert.IsTrue(floor.IsOpen(p),
                            "seed " + seed + " 第 " + floor.FloorIndex + " 层的竖向核落在封死区里");
                    }
                }
            }
        }

        [Test]
        public void CorePlanIsDeterministicPerSeed()
        {
            RaidPlan a = SlicePlanner.Generate(9);
            RaidPlan b = SlicePlanner.Generate(9);
            Assert.AreEqual(a.CorePlan.Fingerprint(), b.CorePlan.Fingerprint());
            Assert.AreEqual(a.Floors.Count, b.Floors.Count);
        }
    }
}
