using System.Collections.Generic;
using EscapeFromWork.LevelGen;
using NUnit.Framework;
using UnityEngine;

namespace EscapeFromWork.Tests.EditMode
{
    public class SlicePlannerTests
    {
        const int Seeds = 6;
        static List<RaidPlan> _raids;

        static List<RaidPlan> Raids()
        {
            if (_raids == null)
            {
                _raids = new List<RaidPlan>();
                for (int s = 1; s <= Seeds; s++) _raids.Add(SlicePlanner.Generate(s));
            }
            return _raids;
        }

        [Test]
        public void Raids_HaveExactlyFourConsecutiveFloors()
        {
            foreach (RaidPlan raid in Raids())
            {
                Assert.AreEqual(4, raid.FloorCount);
                for (int i = 0; i < 4; i++) Assert.AreEqual(i, raid.Floors[i].FloorIndex);
            }
        }

        [Test]
        public void PlayableArea_AndPerFloorFractions_MatchR11Bands()
        {
            foreach (RaidPlan raid in Raids())
            {
                Assert.That(raid.PlayableArea, Is.InRange(SlicePlanner.MinPlayableArea, SlicePlanner.MaxPlayableArea),
                    "seed " + raid.Seed + " area=" + raid.PlayableArea);
                for (int i = 0; i < 4; i++)
                {
                    SliceFloor floor = raid.Floors[i];
                    float min, max;
                    SlicePlanner.FractionRange(i, out min, out max);
                    Assert.That(floor.OpenFraction, Is.InRange(min - 0.001f, max + 0.001f),
                        "seed " + raid.Seed + " F" + (i + 1) + " fraction=" + floor.OpenFraction);
                    Assert.AreEqual(raid.PlayableArea, Sum(raid), 0.01f);
                }
            }
        }

        static float Sum(RaidPlan raid)
        {
            float area = 0f;
            for (int i = 0; i < raid.Floors.Count; i++) area += raid.Floors[i].PlayableArea;
            return area;
        }

        [Test]
        public void EveryFloorOpensItsRealVerticalPortals()
        {
            foreach (RaidPlan raid in Raids())
            for (int f = 0; f < 4; f++)
            {
                SliceFloor floor = raid.Floors[f];
                Assert.IsTrue(floor.IsOpen(floor.Plan.Core.Center));
                Assert.AreEqual(raid.StairGraph.StairsOnFloor(f).Count, floor.Plan.Stairs.Count);
                for (int i = 0; i < floor.Plan.Stairs.Count; i++)
                    Assert.IsTrue(floor.IsOpen(floor.Plan.Stairs[i].Center), "sealed stair seed=" + raid.Seed + " floor=" + f);
            }
        }

        [Test]
        public void AdjacentFloorsHaveExactlyTwoStairsAndOneTrafficElevator()
        {
            foreach (RaidPlan raid in Raids())
            {
                Assert.AreEqual(9, raid.Links.Count);
                for (int lower = 0; lower < 3; lower++)
                {
                    List<VerticalLink> links = raid.Links.FindAll(l => l.LowerFloor == lower && l.UpperFloor == lower + 1);
                    Assert.AreEqual(3, links.Count);
                    Assert.AreEqual(2, links.FindAll(l => l.Kind == VerticalLinkKind.Stair).Count);
                    Assert.AreEqual(1, links.FindAll(l => l.Kind == VerticalLinkKind.Elevator).Count);
                }
            }
        }

        [Test]
        public void SealedAreasPlusOpenBandCoverTheFloor()
        {
            foreach (RaidPlan raid in Raids())
            foreach (SliceFloor floor in raid.Floors)
            {
                float sealedArea = 0f;
                foreach (SealedArea area in floor.Sealed) sealedArea += area.Rect.Area;
                Assert.That(sealedArea + floor.Band.Area, Is.EqualTo(floor.Plan.Bounds.Area).Within(2f));
                Assert.That(floor.Sealed.Count, Is.GreaterThan(0));
            }
        }

        [Test]
        public void MiddleFloorsHaveNoSpawnZones()
        {
            foreach (RaidPlan raid in Raids())
            {
                Assert.IsEmpty(raid.Floors[1].Plan.SpawnZones);
                Assert.IsEmpty(raid.Floors[2].Plan.SpawnZones);
            }
        }

        [Test]
        public void GeneratedRaids_PassTheStructuralLevelAudit()
        {
            foreach (RaidPlan raid in Raids())
            {
                LevelAudit.SliceReport report = LevelAudit.AuditSlice(raid);
                if (report.StructuralViolationCount > 0)
                {
                    string detail = "";
                    for (int f = 0; f < report.Floors.Count; f++)
                    {
                        LevelAudit.FloorReport fr = report.Floors[f];
                        if (fr.StructuralViolationCount == 0) continue;
                        detail += "\nF" + fr.FloorIndex + " (" + fr.StructuralViolationCount + ") " +
                            string.Join("; ", fr.Violations);
                    }
                    Assert.Fail("seed " + raid.Seed + detail);
                }
            }
        }

        [Test]
        public void SameSeed_SameRaidGraphAndLayout()
        {
            RaidPlan a = SlicePlanner.Generate(3);
            RaidPlan b = SlicePlanner.Generate(3);
            Assert.AreEqual(a.StairGraph.Fingerprint(), b.StairGraph.Fingerprint());
            Assert.AreEqual(a.PlayableArea, b.PlayableArea, 0.01f);
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(a.Floors[i].Band.Y, b.Floors[i].Band.Y, 0.001f);
                Assert.AreEqual(a.Floors[i].OpenFraction, b.Floors[i].OpenFraction, 0.0001f);
            }
        }
    }
}
