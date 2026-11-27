using System.Collections.Generic;
using EscapeFromWork.LevelGen;
using NUnit.Framework;
using UnityEngine;

namespace EscapeFromWork.Tests.EditMode
{
    /// <summary>
    /// R11：固定四层切片中，每层两个撤离设施、每端四个可用点。
    /// 测试只消费 SlicePlanner.Generate(seed) 的规划结果，不依赖场景或运行时经济系统。
    /// </summary>
    public class ExtractionPlanTests
    {
        const int Seeds = 6;

        static List<RaidPlan> _raids;

        static List<RaidPlan> Raids()
        {
            if (_raids == null)
            {
                var complete = new List<RaidPlan>();
                for (int s = 1; s <= Seeds; s++) complete.Add(SlicePlanner.Generate(s));
                _raids = complete;
            }
            return _raids;
        }

        static ExtractionPlan PlanFor(RaidPlan raid)
        {
            Assert.IsNotNull(raid.Extraction, "seed " + raid.Seed + " SlicePlanner 必须生成并挂接 ExtractionPlan");
            return raid.Extraction;
        }

        [Test]
        public void Generate_HasFourFloorsAndEightPointsSplitByFloorAndEnd()
        {
            foreach (RaidPlan raid in Raids())
            {
                ExtractionPlan plan = PlanFor(raid);
                Assert.AreEqual(4, raid.FloorCount, "seed " + raid.Seed + " 每局必须固定四层");
                Assert.AreEqual(8, plan.Points.Count, "seed " + raid.Seed + " 全局必须恰好八个撤离点");

                for (int i = 0; i < raid.Floors.Count; i++)
                {
                    int floorIndex = raid.Floors[i].FloorIndex;
                    Assert.AreEqual(2, plan.ForFloor(floorIndex).Count,
                        "seed " + raid.Seed + " 第 " + floorIndex + " 层必须恰好两个撤离点");
                }

                Assert.AreEqual(4, plan.ForEnd(SpawnEnd.Bottom).Count,
                    "seed " + raid.Seed + " F1 端必须恰好四个撤离点");
                Assert.AreEqual(4, plan.ForEnd(SpawnEnd.Top).Count,
                    "seed " + raid.Seed + " F4 端必须恰好四个撤离点");
            }
        }

        [Test]
        public void FloorRoles_MatchNearGatedAndFarFreeOwnership()
        {
            foreach (RaidPlan raid in Raids())
            {
                // 06 §5.3 表固定了归属：F1 免费点归 Top 端、F2 门禁归 Top 端、F3 门禁归 Bottom 端、F4 免费点归 Bottom 端。
                AssertFloorRole(raid, 0, SpawnEnd.Top, true);
                AssertFloorRole(raid, 1, SpawnEnd.Top, false);
                AssertFloorRole(raid, 2, SpawnEnd.Bottom, false);
                AssertFloorRole(raid, 3, SpawnEnd.Bottom, true);
            }
        }

        static void AssertFloorRole(RaidPlan raid, int floorSlot, SpawnEnd owner, bool free)
        {
            ExtractionPlan plan = PlanFor(raid);
            int floorIndex = raid.Floors[floorSlot].FloorIndex;
            List<PlannedExtraction> points = plan.ForFloor(floorIndex);
            Assert.AreEqual(2, points.Count, "seed " + raid.Seed + " 第 " + floorIndex + " 层点位数量错误");

            foreach (PlannedExtraction point in points)
            {
                string context = "seed " + raid.Seed + " 点 " + point.Id;
                Assert.AreEqual(owner, point.OwnerEnd, context + " 归属端错误");
                Assert.AreEqual(free, point.InitiallyOpen, context + " 免费/门禁状态错误");
                Assert.AreEqual(!free, point.Point.NeedsPower, context + " 拉闸供电需求错误");
                if (free) Assert.AreEqual(-1, point.GateId, context + " 免费点不得绑定门禁");
                else Assert.GreaterOrEqual(point.GateId, 0, context + " 门禁点必须绑定门禁");
            }
        }

        [Test]
        public void ExtractionKindsAndChannels_MatchFloorPools()
        {
            ExtractionKind[][] pools =
            {
                new[] { ExtractionKind.FireStair, ExtractionKind.Freight },
                new[] { ExtractionKind.FireStair, ExtractionKind.WindowBasket },
                new[] { ExtractionKind.FireStair, ExtractionKind.Freight },
                new[] { ExtractionKind.RoofRappel, ExtractionKind.WindowBasket }
            };

            foreach (RaidPlan raid in Raids())
            {
                ExtractionPlan plan = PlanFor(raid);
                for (int slot = 0; slot < pools.Length; slot++)
                {
                    int floorIndex = raid.Floors[slot].FloorIndex;
                    List<PlannedExtraction> points = plan.ForFloor(floorIndex);
                    var actualKinds = new List<ExtractionKind>();
                    foreach (PlannedExtraction point in points)
                    {
                        ExtractionKind kind = point.Point.Kind;
                        string context = "seed " + raid.Seed + " 点 " + point.Id;
                        actualKinds.Add(kind);
                        Assert.AreNotEqual(ExtractionKind.Elevator, kind, context + " 交通电梯永不作为撤离点");
                        Assert.AreEqual(ExpectedChannel(kind), point.Point.ChannelSeconds, 0.001f,
                            context + " 撤离读条必须为消防梯 20 / 货梯与擦窗机 25 / 索降 30 秒");
                        if (kind == ExtractionKind.Freight)
                            Assert.AreEqual(25f, point.Point.ChannelSeconds, 0.001f, context + " 货梯必须读条 25 秒");
                    }
                    CollectionAssert.AreEquivalent(pools[slot], actualKinds,
                        "seed " + raid.Seed + " 第 " + floorIndex + " 层撤离类型池错误");
                }
            }
        }

        static float ExpectedChannel(ExtractionKind kind)
        {
            switch (kind)
            {
                case ExtractionKind.FireStair: return 20f;
                case ExtractionKind.Freight:
                case ExtractionKind.WindowBasket: return 25f;
                case ExtractionKind.RoofRappel: return 30f;
                default:
                    Assert.Fail("交通电梯或未知类型不得进入撤离读条表: " + kind);
                    return 0f;
            }
        }

        [Test]
        public void Gates_AreOnSeparateMiddleFloorsBroadcastAndControlCorrectEnd()
        {
            foreach (RaidPlan raid in Raids())
            {
                ExtractionPlan plan = PlanFor(raid);
                Assert.AreEqual(2, plan.Gates.Count, "seed " + raid.Seed + " 必须恰好两个门禁");

                var gateFloors = new List<int>();
                var gateIds = new HashSet<int>();
                var controlledEnds = new HashSet<SpawnEnd>();
                foreach (ExtractionGate gate in plan.Gates)
                {
                    string context = "seed " + raid.Seed + " 门禁 " + gate.Id;
                    Assert.IsTrue(gateIds.Add(gate.Id), context + " Id 必须唯一");
                    gateFloors.Add(gate.FloorIndex);
                    controlledEnds.Add(gate.ControlsEnd);
                    Assert.IsTrue(gate.BroadcastOnActivation, context + " 激活必须全楼广播");
                    Assert.IsTrue(gate.ControlsEnd == SpawnEnd.Bottom || gate.ControlsEnd == SpawnEnd.Top,
                        context + " 控制端无效");

                    // 闸房必在 F2/F3（哪端在哪层随机），它控制本端近层的两个点。
                    int controlledFloor = gate.ControlsEnd == SpawnEnd.Top ? raid.Floors[1].FloorIndex : raid.Floors[2].FloorIndex;
                    List<PlannedExtraction> controlled = plan.Points.FindAll(p => p.GateId == gate.Id);
                    Assert.AreEqual(2, controlled.Count, context + " 必须控制本端近层两个点");
                    foreach (PlannedExtraction point in controlled)
                    {
                        Assert.AreEqual(gate.ControlsEnd, point.OwnerEnd, context + " 控制了错误端的点 " + point.Id);
                        Assert.AreEqual(controlledFloor, point.FloorIndex, context + " 控制点不在正确近层");
                        Assert.IsFalse(point.InitiallyOpen, context + " 控制点必须初始关闭");
                    }
                }

                CollectionAssert.AreEquivalent(
                    new[] { raid.Floors[1].FloorIndex, raid.Floors[2].FloorIndex }, gateFloors,
                    "seed " + raid.Seed + " 两个门禁必须分别在 F2/F3");
                Assert.AreEqual(2, controlledEnds.Count, "seed " + raid.Seed + " 两个门禁必须分别控制两端");
            }
        }

        [Test]
        public void ElevatorCards_AreTwoOneUseRecordsOnOneMiddleFloorWithBossOnOther()
        {
            foreach (RaidPlan raid in Raids())
            {
                ExtractionPlan plan = PlanFor(raid);
                int f2 = raid.Floors[1].FloorIndex;
                int f3 = raid.Floors[2].FloorIndex;
                Assert.IsTrue(plan.ElevatorCardFloor == f2 || plan.ElevatorCardFloor == f3,
                    "seed " + raid.Seed + " 电梯卡只能刷在 F2 或 F3");
                Assert.AreEqual(plan.ElevatorCardFloor == f2 ? f3 : f2, plan.BossSafeFloor,
                    "seed " + raid.Seed + " BossSafeFloor 必须是另一个中层");
                Assert.IsTrue(plan.ElevatorCardsAreOneUse, "seed " + raid.Seed + " 电梯卡必须一次性使用");

                Assert.AreEqual(2, plan.ElevatorCardRooms.Count,
                    "seed " + raid.Seed + " 必须恰好记录两个电梯卡房间");
                SliceFloor cardFloor = raid.At(plan.ElevatorCardFloor);
                Assert.IsNotNull(cardFloor, "seed " + raid.Seed + " 电梯卡层必须属于本局切片");
                var uniqueRooms = new HashSet<int>();
                foreach (int roomIndex in plan.ElevatorCardRooms)
                {
                    Assert.IsTrue(uniqueRooms.Add(roomIndex), "seed " + raid.Seed + " 电梯卡房间记录不得重复");
                    Assert.That(roomIndex, Is.InRange(0, cardFloor.Plan.Rooms.Count - 1),
                        "seed " + raid.Seed + " 电梯卡房间索引必须属于卡层");
                }
            }
        }

        [Test]
        public void FinalThreeMinutes_ClosesOneFreePointPerEndAndLeavesAtLeastThree()
        {
            foreach (RaidPlan raid in Raids())
            {
                ExtractionPlan plan = PlanFor(raid);
                Assert.AreEqual(3f, ExtractionPlan.FinalClosureMinutes, 0.001f, "关闭窗口必须是最后三分钟");
                var closingIds = new List<string>();

                foreach (SpawnEnd end in new[] { SpawnEnd.Bottom, SpawnEnd.Top })
                {
                    List<PlannedExtraction> owned = plan.ForEnd(end);
                    List<PlannedExtraction> closing = owned.FindAll(p => p.ClosesInFinalThreeMinutes);
                    Assert.AreEqual(1, closing.Count, "seed " + raid.Seed + " " + end + " 端必须恰关一个点");
                    Assert.IsTrue(closing[0].InitiallyOpen,
                        "seed " + raid.Seed + " " + end + " 端最后关闭的必须是免费点");
                    closingIds.Add(closing[0].Id);

                    // 近层两个门禁点激活后仍可用，加上未关闭的一个远层免费点。
                    int remaining = owned.FindAll(p => !p.ClosesInFinalThreeMinutes).Count;
                    Assert.GreaterOrEqual(remaining, 3,
                        "seed " + raid.Seed + " " + end + " 端关闭后必须至少保留三个可用点");
                }

                Assert.AreEqual(2, plan.FinalClosureIds.Count, "seed " + raid.Seed + " 关闭清单必须只有两项");
                Assert.AreEqual(2, new HashSet<string>(plan.FinalClosureIds).Count,
                    "seed " + raid.Seed + " 两端不得关闭同一个点");
                CollectionAssert.AreEquivalent(closingIds, plan.FinalClosureIds,
                    "seed " + raid.Seed + " 关闭清单必须与点位标记一致");
            }
        }

        [Test]
        public void MiddleFloors_HaveNoSpawnZonesAfterExtractionPlanning()
        {
            foreach (RaidPlan raid in Raids())
            {
                PlanFor(raid);
                for (int slot = 1; slot <= 2; slot++)
                {
                    SliceFloor floor = raid.Floors[slot];
                    Assert.AreEqual(0, floor.Plan.SpawnZones.Count,
                        "seed " + raid.Seed + " 中间层第 " + floor.FloorIndex + " 层不得保留出生区");
                }
            }
        }

        [Test]
        public void EveryPoint_IsInOpenFloorAndClearsTrafficStairsAndCore()
        {
            foreach (RaidPlan raid in Raids())
            {
                ExtractionPlan plan = PlanFor(raid);
                foreach (SliceFloor floor in raid.Floors)
                {
                    foreach (PlannedExtraction point in plan.ForFloor(floor.FloorIndex))
                    {
                        string context = "seed " + raid.Seed + " 点 " + point.Id;
                        Assert.IsTrue(floor.IsOpen(point.Point.Center), context + " 中心必须位于开放区");
                        foreach (RectF stair in floor.Plan.Stairs)
                        {
                            Assert.IsFalse(point.Point.Rect.Overlaps(stair.Inflate(1f)),
                                context + " 不得压住交通楼梯及其一米净空");
                        }
                        Assert.IsFalse(point.Point.Rect.Overlaps(floor.Plan.Core.Inflate(1f)),
                            context + " 不得压住交通核心筒及其一米净空");
                    }
                }
            }
        }

        [Test]
        public void EveryFreePoint_PassesOpposingSpawnDistanceAndLineOfSightSafety()
        {
            foreach (RaidPlan raid in Raids())
            {
                ExtractionPlan plan = PlanFor(raid);
                foreach (PlannedExtraction point in plan.Points)
                {
                    if (!point.InitiallyOpen) continue;
                    Assert.IsTrue(ExtractionPlan.IsFreePointSafe(raid, point),
                        "seed " + raid.Seed + " 免费点 " + point.Id +
                        " 必须距对方出生区至少 " + ExtractionPlan.OpposingSpawnClearance + "m 且无视线");
                }
            }
        }

        [Test]
        public void SameSeed_ReproducesPositionsKindsOwnershipClosuresAndCardFloor()
        {
            RaidPlan a = SlicePlanner.Generate(23);
            RaidPlan b = SlicePlanner.Generate(23);
            ExtractionPlan first = PlanFor(a);
            ExtractionPlan second = PlanFor(b);

            List<PlannedExtraction> firstPoints = SortedPoints(first);
            List<PlannedExtraction> secondPoints = SortedPoints(second);
            Assert.AreEqual(firstPoints.Count, secondPoints.Count, "同 seed 撤离点数量必须一致");
            for (int i = 0; i < firstPoints.Count; i++)
            {
                PlannedExtraction expected = firstPoints[i];
                PlannedExtraction actual = secondPoints[i];
                string context = "同 seed 点 " + expected.Id;
                Assert.AreEqual(expected.Id, actual.Id, context + " Id 不一致");
                Assert.AreEqual(expected.FloorIndex, actual.FloorIndex, context + " 楼层不一致");
                Assert.AreEqual(expected.OwnerEnd, actual.OwnerEnd, context + " 归属端不一致");
                Assert.AreEqual(expected.Point.Kind, actual.Point.Kind, context + " 类型不一致");
                Assert.AreEqual(expected.InitiallyOpen, actual.InitiallyOpen, context + " 初始开放状态不一致");
                Assert.AreEqual(expected.GateId, actual.GateId, context + " 门禁绑定不一致");
                Assert.AreEqual(expected.ClosesInFinalThreeMinutes, actual.ClosesInFinalThreeMinutes,
                    context + " 最终关闭标记不一致");
                Assert.AreEqual(expected.Point.Center.x, actual.Point.Center.x, 0.0001f, context + " X 坐标不一致");
                Assert.AreEqual(expected.Point.Center.y, actual.Point.Center.y, 0.0001f, context + " Y 坐标不一致");
                Assert.AreEqual(expected.Point.ChannelSeconds, actual.Point.ChannelSeconds, 0.001f,
                    context + " 读条时间不一致");
            }

            CollectionAssert.AreEqual(SortedStrings(first.FinalClosureIds), SortedStrings(second.FinalClosureIds),
                "同 seed 最终关闭清单必须一致");
            Assert.AreEqual(first.ElevatorCardFloor, second.ElevatorCardFloor, "同 seed 电梯卡层必须一致");
            Assert.AreEqual(first.BossSafeFloor, second.BossSafeFloor, "同 seed Boss 安全层必须一致");
            Assert.AreEqual(first.ElevatorCardsAreOneUse, second.ElevatorCardsAreOneUse, "同 seed 电梯卡次数必须一致");
            CollectionAssert.AreEqual(SortedInts(first.ElevatorCardRooms), SortedInts(second.ElevatorCardRooms),
                "同 seed 电梯卡房间记录必须一致");
        }

        static List<PlannedExtraction> SortedPoints(ExtractionPlan plan)
        {
            var points = new List<PlannedExtraction>(plan.Points);
            points.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return points;
        }

        static List<string> SortedStrings(List<string> values)
        {
            var sorted = new List<string>(values);
            sorted.Sort(string.CompareOrdinal);
            return sorted;
        }

        static List<int> SortedInts(List<int> values)
        {
            var sorted = new List<int>(values);
            sorted.Sort();
            return sorted;
        }
    }
}
