using System.Collections.Generic;
using EscapeFromWork.LevelGen;
using NUnit.Framework;
using UnityEngine;

namespace EscapeFromWork.Tests.EditMode
{
    /// <summary>
    /// R5：一局 = 随机 3–4 层 × 每层 20–32% 楼板，合计 24,000–30,000㎡，
    /// 相邻层固定 3 条竖向通路（西梯 / 核心筒电梯 / 东梯），其余楼板封死。
    /// 这些断言只用生成器输出，不依赖场景，跟 R2/R3 的验收口径一致。
    /// </summary>
    public class SlicePlannerTests
    {
        const int Seeds = 6;

        static List<RaidPlan> Raids()
        {
            var raids = new List<RaidPlan>();
            for (int s = 1; s <= Seeds; s++) raids.Add(SlicePlanner.Generate(s));
            return raids;
        }

        [Test]
        public void Raids_HaveThreeOrFourConsecutiveFloors()
        {
            foreach (RaidPlan raid in Raids())
            {
                Assert.That(raid.FloorCount, Is.InRange(3, 4), "seed " + raid.Seed + " 层数应 3–4");
                for (int i = 1; i < raid.Floors.Count; i++)
                    Assert.AreEqual(raid.Floors[i - 1].FloorIndex + 1, raid.Floors[i].FloorIndex,
                        "seed " + raid.Seed + " 楼层必须连续");
            }
        }

        [Test]
        public void PlayableArea_IsBetween24And30Thousand()
        {
            foreach (RaidPlan raid in Raids())
            {
                Assert.That(raid.PlayableArea, Is.InRange(SlicePlanner.MinPlayableArea, SlicePlanner.MaxPlayableArea),
                    "seed " + raid.Seed + " 可玩面积 " + raid.PlayableArea.ToString("0") + "㎡ 不在 24,000–30,000");
            }
        }

        [Test]
        public void EachFloorOpensTwentyToThirtyTwoPercent()
        {
            foreach (RaidPlan raid in Raids())
            {
                foreach (SliceFloor floor in raid.Floors)
                {
                    Assert.That(floor.OpenFraction, Is.InRange(0.19f, 0.33f),
                        "seed " + raid.Seed + " 第 " + floor.FloorIndex + " 层开放比例 " +
                        floor.OpenFraction.ToString("0.000"));
                    Assert.That(floor.PlayableArea, Is.GreaterThan(4000f),
                        "seed " + raid.Seed + " 第 " + floor.FloorIndex + " 层可玩面积过小");
                }
            }
        }

        [Test]
        public void EveryFloorOpensItsVerticalCores()
        {
            foreach (RaidPlan raid in Raids())
            {
                foreach (SliceFloor floor in raid.Floors)
                {
                    Assert.IsTrue(floor.IsOpen(floor.Plan.Core.Center),
                        "seed " + raid.Seed + " 第 " + floor.FloorIndex + " 层核心筒未开放");
                    foreach (RectF stair in floor.Plan.Stairs)
                        Assert.IsTrue(floor.IsOpen(stair.Center),
                            "seed " + raid.Seed + " 第 " + floor.FloorIndex + " 层楼梯未开放");
                }
            }
        }

        [Test]
        public void AdjacentFloorsHaveThreeLinks_AcrossDifferentColumns()
        {
            foreach (RaidPlan raid in Raids())
            {
                for (int i = 0; i + 1 < raid.Floors.Count; i++)
                {
                    int lower = raid.Floors[i].FloorIndex;
                    int upper = raid.Floors[i + 1].FloorIndex;
                    var links = raid.Links.FindAll(l => l.LowerFloor == lower && l.UpperFloor == upper);

                    Assert.That(links.Count, Is.GreaterThanOrEqualTo(3),
                        "seed " + raid.Seed + " 第 " + lower + "→" + upper + " 层竖向通路不足 3 条");

                    var columns = new HashSet<int>();
                    bool stair = false, elevator = false;
                    foreach (VerticalLink link in links)
                    {
                        columns.Add(link.Column);
                        if (link.Kind == VerticalLinkKind.Stair) stair = true;
                        if (link.Kind == VerticalLinkKind.Elevator) elevator = true;
                    }
                    Assert.IsTrue(stair, "seed " + raid.Seed + " " + lower + "→" + upper + " 缺楼梯通路");
                    Assert.IsTrue(elevator, "seed " + raid.Seed + " " + lower + "→" + upper + " 缺电梯/管井通路");
                    Assert.That(columns.Count, Is.GreaterThanOrEqualTo(2),
                        "seed " + raid.Seed + " " + lower + "→" + upper + " 通路全挤在同一竖向核");
                }
            }
        }

        [Test]
        public void SealedAreasPlusOpenBandCoverTheFloor()
        {
            foreach (RaidPlan raid in Raids())
            {
                foreach (SliceFloor floor in raid.Floors)
                {
                    float sealedArea = 0f;
                    foreach (SealedArea area in floor.Sealed) sealedArea += area.Rect.Area;
                    float total = sealedArea + floor.Band.Area;
                    Assert.That(total, Is.EqualTo(floor.Plan.Bounds.Area).Within(2f),
                        "seed " + raid.Seed + " 第 " + floor.FloorIndex + " 层：封死区 + 开放带 应铺满整层");
                    Assert.That(floor.Sealed.Count, Is.GreaterThan(0),
                        "seed " + raid.Seed + " 第 " + floor.FloorIndex + " 层没有任何封死区");
                }
            }
        }

        [Test]
        public void SameSeed_SameRaid()
        {
            RaidPlan a = SlicePlanner.Generate(3);
            RaidPlan b = SlicePlanner.Generate(3);

            Assert.AreEqual(a.FloorCount, b.FloorCount);
            Assert.AreEqual(a.PlayableArea, b.PlayableArea, 0.01f);
            for (int i = 0; i < a.Floors.Count; i++)
            {
                Assert.AreEqual(a.Floors[i].FloorIndex, b.Floors[i].FloorIndex);
                Assert.AreEqual(a.Floors[i].Band.X, b.Floors[i].Band.X, 0.001f);
                Assert.AreEqual(a.Floors[i].Band.Y, b.Floors[i].Band.Y, 0.001f);
            }
        }
    }
}
