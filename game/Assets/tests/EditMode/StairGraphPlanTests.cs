using System.Collections.Generic;
using EscapeFromWork.LevelGen;
using NUnit.Framework;
using UnityEngine;

namespace EscapeFromWork.Tests.EditMode
{
    public class StairGraphPlanTests
    {
        const float Width = 160f;
        const float Height = 120f;
        const float Epsilon = 0.001f;

        [Test]
        public void FixedFourFloorsHaveSixLinksAndSixteenPortals()
        {
            StairGraphPlan plan = StairGraphPlan.Generate(1, Width, Height);
            Assert.AreEqual(4, StairGraphPlan.FloorCount);
            Assert.AreEqual(6, plan.Links.Count);
            Assert.AreEqual(16, plan.Portals.Count);

            HashSet<string> ids = new HashSet<string>();
            for (int floor = 0; floor < StairGraphPlan.FloorCount; floor++)
                Assert.AreEqual(4, plan.PortalsOnFloor(floor).Count, "floor " + floor);

            for (int lower = 0; lower < 3; lower++)
            {
                List<StairLink> links = plan.LinksBetween(lower, lower + 1);
                Assert.AreEqual(2, links.Count, "pair " + lower);
                Assert.Greater(plan.Elevator.W, 0f);
                Assert.Greater(plan.Elevator.H, 0f);
                for (int i = 0; i < links.Count; i++)
                {
                    StairLink link = links[i];
                    Assert.IsTrue(ids.Add(link.Id), "duplicate link id " + link.Id);
                    Assert.AreSame(link, plan.FindLink(link.Id));
                    Assert.AreEqual(lower, link.LowerFloor);
                    Assert.AreEqual(lower + 1, link.UpperFloor);
                    AssertRect(link.LowerRect, link.UpperRect, "single-storey rect");
                    Assert.AreEqual(2, plan.PortalsForLink(link.Id).Count);
                }
            }

            // The one traffic elevator is vertically aligned and is not an extra stair link.
            for (int floor = 1; floor < 4; floor++)
                AssertRect(plan.ElevatorForFloor(0), plan.ElevatorForFloor(floor), "elevator alignment");
        }

        [Test]
        public void EveryPairHasSeparatedCentralStairsInDifferentQuadrants()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                StairGraphPlan plan = StairGraphPlan.Generate(seed, Width, Height);
                for (int lower = 0; lower < 3; lower++)
                {
                    List<StairLink> pair = plan.LinksBetween(lower, lower + 1);
                    float distance = Vector2.Distance(pair[0].Center, pair[1].Center);
                    Assert.GreaterOrEqual(distance, StairGraphPlan.MinStairCenterDistance,
                        "seed " + seed + " pair " + lower + " spacing");
                    Assert.AreNotEqual(Quadrant(pair[0].Center, plan.FloorCenter),
                        Quadrant(pair[1].Center, plan.FloorCenter), "seed " + seed + " quadrants");

                    for (int i = 0; i < pair.Count; i++)
                    {
                        float fromCenter = Vector2.Distance(pair[i].Center, plan.FloorCenter);
                        Assert.LessOrEqual(fromCenter, StairGraphPlan.MaxStairCenterDistance + Epsilon,
                            "seed " + seed + " central band");
                        Assert.IsFalse(pair[i].LowerRect.Overlaps(plan.Elevator), "stair overlaps elevator");
                        if (i > 0) Assert.IsFalse(pair[i].LowerRect.Overlaps(pair[0].LowerRect), "stairs overlap");
                    }
                }
            }
        }

        [Test]
        public void AdjacentPairsAreDeliberatelyMisaligned()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                StairGraphPlan plan = StairGraphPlan.Generate(seed, Width, Height);
                for (int lower = 0; lower < 2; lower++)
                {
                    List<StairLink> current = plan.LinksBetween(lower, lower + 1);
                    List<StairLink> next = plan.LinksBetween(lower + 1, lower + 2);
                    for (int a = 0; a < 2; a++)
                    {
                        for (int b = 0; b < 2; b++)
                        {
                            string message = "seed " + seed + " pairs " + lower + "/" + (lower + 1);
                            Assert.AreNotEqual(Quadrant(current[a].Center, plan.FloorCenter),
                                Quadrant(next[b].Center, plan.FloorCenter), message + " reused quadrant");
                            Assert.Greater(Vector2.Distance(current[a].Center, next[b].Center), 12f,
                                message + " aligned stair center");
                        }
                    }
                }
            }
        }

        [Test]
        public void EachLinkEndHasTwoDifferentDoorDirections()
        {
            StairGraphPlan plan = StairGraphPlan.Generate(7, Width, Height);
            for (int i = 0; i < plan.Links.Count; i++)
            {
                StairLink link = plan.Links[i];
                Assert.AreEqual(2, link.LowerDoors.Count, "lower doors " + link.Id);
                Assert.AreEqual(2, link.UpperDoors.Count, "upper doors " + link.Id);
                Assert.AreNotEqual(link.LowerDoors[0].Side, link.LowerDoors[1].Side);
                Assert.AreNotEqual(link.UpperDoors[0].Side, link.UpperDoors[1].Side);
                for (int door = 0; door < 2; door++)
                {
                    Assert.AreEqual(link.LowerDoors[door].Side, link.LowerDoors[door].Direction);
                    Assert.AreEqual(link.LowerDoors[door].Side, link.UpperDoors[door].Side);
                }
            }
        }

        [Test]
        public void PortalDirectionsAndBoundaryStubsMatchTheFourFloorBuilding()
        {
            StairGraphPlan plan = StairGraphPlan.Generate(11, Width, Height);
            for (int floor = 0; floor < 4; floor++)
            {
                List<StairPortal> all = plan.PortalsOnFloor(floor);
                List<StairPortal> real = plan.RealPortalsOnFloor(floor);
                int up = 0;
                int down = 0;
                int boundary = 0;
                for (int i = 0; i < all.Count; i++)
                {
                    StairPortal portal = all[i];
                    Assert.AreSame(portal, plan.FindPortal(portal.Id));
                    if (portal.Direction == StairPortalDirection.Up) up++;
                    else down++;
                    if (!portal.IsBoundary) continue;

                    boundary++;
                    Assert.IsTrue(portal.IsSealed, "boundary stub must be sealed");
                    Assert.AreEqual(string.Empty, portal.LinkId);
                    Assert.AreEqual(floor == 0 ? StairPortalDirection.Down : StairPortalDirection.Up,
                        portal.Direction, "boundary direction");
                }

                if (floor == 0 || floor == 3)
                {
                    Assert.AreEqual(2, real.Count, "real boundary-floor portals");
                    Assert.AreEqual(2, boundary, "sealed boundary stubs");
                    Assert.AreEqual(2, floor == 0 ? up : down);
                    Assert.AreEqual(2, floor == 0 ? down : up);
                }
                else
                {
                    Assert.AreEqual(4, real.Count, "middle-floor real portals");
                    Assert.AreEqual(0, boundary);
                    Assert.AreEqual(2, up, "middle-floor up portals");
                    Assert.AreEqual(2, down, "middle-floor down portals");
                }

                for (int i = 0; i < real.Count; i++)
                {
                    Assert.IsFalse(real[i].IsSealed);
                    StairLink link = plan.FindLink(real[i].LinkId);
                    Assert.IsNotNull(link);
                    RectF expected = real[i].FloorIndex == link.LowerFloor ? link.LowerRect : link.UpperRect;
                    AssertRect(expected, real[i].Rect, "portal/link rect");
                }
            }
        }

        [Test]
        public void SameSeedFingerprintsMatchAndDifferentSeedsMoveStairs()
        {
            StairGraphPlan a = StairGraphPlan.Generate(17, Width, Height);
            StairGraphPlan b = StairGraphPlan.Generate(17, Width, Height);
            StairGraphPlan different = StairGraphPlan.Generate(18, Width, Height);
            Assert.AreEqual(a.Fingerprint(), b.Fingerprint());
            Assert.AreNotEqual(a.Fingerprint(), different.Fingerprint());

            bool moved = false;
            for (int i = 0; i < a.Links.Count; i++)
            {
                Assert.AreEqual(a.Links[i].Id, b.Links[i].Id);
                if (Vector2.Distance(a.Links[i].Center, different.Links[i].Center) > Epsilon) moved = true;
            }
            Assert.IsTrue(moved, "different seed did not change any stair position");
        }

        [Test]
        public void SeedsOneThroughTwentyKeepEveryRectangleInBounds()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                StairGraphPlan plan = StairGraphPlan.Generate(seed, Width, Height);
                AssertInside(plan.Bounds, plan.Elevator, "seed " + seed + " elevator");
                for (int i = 0; i < plan.Links.Count; i++)
                {
                    AssertInside(plan.Bounds, plan.Links[i].LowerRect, "seed " + seed + " lower stair");
                    AssertInside(plan.Bounds, plan.Links[i].UpperRect, "seed " + seed + " upper stair");
                }
                for (int i = 0; i < plan.Portals.Count; i++)
                    AssertInside(plan.Bounds, plan.Portals[i].Rect, "seed " + seed + " portal " + i);
            }
        }

        [Test]
        public void FloorPlanProjectionKeepsCoreStairsAndDoesNotCreateExtractions()
        {
            StairGraphPlan graph = StairGraphPlan.Generate(4, Width, Height);
            for (int floor = 0; floor < 4; floor++)
            {
                FloorPlan plan = new FloorPlan();
                plan.Extractions.Add(new ExtractionPoint());
                plan.ExtractionCandidates.Add(new ExtractionPoint());
                graph.ApplyTo(plan, floor);
                AssertRect(graph.Elevator, plan.Core, "projected core");
                Assert.AreEqual(floor == 0 || floor == 3 ? 2 : 4, plan.Stairs.Count);
                Assert.AreEqual(1, plan.Extractions.Count, "traffic elevator is not an extraction");
                Assert.AreEqual(1, plan.ExtractionCandidates.Count, "traffic elevator is not a candidate");
            }
        }

        static int Quadrant(Vector2 point, Vector2 center)
        {
            if (point.x < center.x && point.y < center.y) return 0;
            if (point.x >= center.x && point.y < center.y) return 1;
            if (point.x >= center.x && point.y >= center.y) return 2;
            return 3;
        }

        static void AssertInside(RectF bounds, RectF rect, string message)
        {
            Assert.GreaterOrEqual(rect.X, bounds.X, message + " X");
            Assert.GreaterOrEqual(rect.Y, bounds.Y, message + " Y");
            Assert.LessOrEqual(rect.XMax, bounds.XMax, message + " XMax");
            Assert.LessOrEqual(rect.YMax, bounds.YMax, message + " YMax");
            Assert.Greater(rect.W, 0f, message + " W");
            Assert.Greater(rect.H, 0f, message + " H");
        }

        static void AssertRect(RectF expected, RectF actual, string message)
        {
            Assert.AreEqual(expected.X, actual.X, Epsilon, message + " X");
            Assert.AreEqual(expected.Y, actual.Y, Epsilon, message + " Y");
            Assert.AreEqual(expected.W, actual.W, Epsilon, message + " W");
            Assert.AreEqual(expected.H, actual.H, Epsilon, message + " H");
        }
    }
}
