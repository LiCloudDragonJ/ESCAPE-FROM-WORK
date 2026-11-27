using System.Collections.Generic;
using EscapeFromWork.LevelGen;
using NUnit.Framework;
using UnityEngine;

namespace EscapeFromWork.Tests.EditMode
{
    public class VerticalLinkPlanTests
    {
        static readonly int[] Seeds = { 1, 2, 3, 5, 7, 11 };

        [Test]
        public void ElevatorAlignsAcrossFloors()
        {
            foreach (int seed in Seeds)
            {
                RaidPlan raid = SlicePlanner.Generate(seed);
                Vector2 expected = raid.StairGraph.Elevator.Center;
                for (int f = 0; f < 4; f++)
                    Assert.That(Vector2.Distance(expected, raid.Floors[f].Plan.Core.Center), Is.LessThan(0.001f));
            }
        }

        [Test]
        public void StairPatternsAreDeliberatelyMisalignedBetweenPairs()
        {
            foreach (int seed in Seeds)
            {
                StairGraphPlan graph = SlicePlanner.Generate(seed).StairGraph;
                for (int pair = 0; pair < 2; pair++)
                {
                    List<StairLink> a = graph.LinksBetween(pair, pair + 1);
                    List<StairLink> b = graph.LinksBetween(pair + 1, pair + 2);
                    for (int i = 0; i < a.Count; i++)
                    for (int j = 0; j < b.Count; j++)
                        Assert.That(Vector2.Distance(a[i].Center, b[j].Center), Is.GreaterThan(1f),
                            "adjacent pairs must not form an aligned stair shaft");
                }
            }
        }

        [Test]
        public void EveryAdjacentPairHasTwoIndependentSeparatedStairs()
        {
            foreach (int seed in Seeds)
            {
                StairGraphPlan graph = SlicePlanner.Generate(seed).StairGraph;
                for (int pair = 0; pair < 3; pair++)
                {
                    List<StairLink> stairs = graph.LinksBetween(pair, pair + 1);
                    Assert.AreEqual(2, stairs.Count);
                    Assert.That(Vector2.Distance(stairs[0].Center, stairs[1].Center), Is.GreaterThanOrEqualTo(30f));
                    Assert.AreNotEqual(Quadrant(stairs[0].Center, graph.FloorCenter), Quadrant(stairs[1].Center, graph.FloorCenter));
                    for (int i = 0; i < 2; i++)
                    {
                        Assert.That(Vector2.Distance(stairs[i].Center, graph.FloorCenter), Is.LessThanOrEqualTo(45f));
                        Assert.AreEqual(2, stairs[i].LowerDoors.Count);
                        Assert.AreEqual(2, stairs[i].UpperDoors.Count);
                        Assert.AreNotEqual(stairs[i].LowerDoors[0].Side, stairs[i].LowerDoors[1].Side);
                    }
                }
            }
        }

        static int Quadrant(Vector2 p, Vector2 c)
        {
            if (p.x < c.x) return p.y < c.y ? 0 : 1;
            return p.y < c.y ? 3 : 2;
        }

        [Test]
        public void FourPortalsPerFloorIncludeSealedBoundaryStubs()
        {
            StairGraphPlan graph = SlicePlanner.Generate(9).StairGraph;
            Assert.AreEqual(16, graph.Portals.Count);
            for (int f = 0; f < 4; f++)
            {
                List<StairPortal> portals = graph.PortalsOnFloor(f);
                Assert.AreEqual(4, portals.Count);
                Assert.AreEqual(f == 0 || f == 3 ? 2 : 4, graph.RealPortalsOnFloor(f).Count);
                if (f == 0 || f == 3) Assert.AreEqual(2, portals.FindAll(p => p.IsBoundary && p.IsSealed).Count);
            }
        }

        [Test]
        public void GraphIsDeterministicPerSeed()
        {
            RaidPlan a = SlicePlanner.Generate(9);
            RaidPlan b = SlicePlanner.Generate(9);
            Assert.AreEqual(a.StairGraph.Fingerprint(), b.StairGraph.Fingerprint());
        }
    }
}
