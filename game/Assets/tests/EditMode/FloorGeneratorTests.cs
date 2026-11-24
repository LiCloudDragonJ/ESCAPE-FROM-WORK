using System.Collections.Generic;
using EscapeFromWork.LevelGen;
using NUnit.Framework;
using UnityEngine;

namespace EscapeFromWork.Tests.EditMode
{
    public class FloorGeneratorTests
    {
        static FloorSpec Spec(int seed)
        {
            FloorSpec s = new FloorSpec();
            s.Seed = seed;
            s.Width = 200f;
            s.Height = 160f;
            s.Band = FloorBand.Tower;
            s.Prototype = FloorPrototype.OpenOffice;
            return s;
        }

        [Test]
        public void SameSeed_ProducesIdenticalLayout()
        {
            FloorPlan a = FloorGenerator.Generate(Spec(7));
            FloorPlan b = FloorGenerator.Generate(Spec(7));
            Assert.AreEqual(a.Rooms.Count, b.Rooms.Count);
            for (int i = 0; i < a.Rooms.Count; i++)
                Assert.AreEqual(a.Rooms[i].Rect.ToString(), b.Rooms[i].Rect.ToString());
        }

        [Test]
        public void Rooms_NeverOverlapEachOther()
        {
            for (int seed = 1; seed <= 40; seed++)
            {
                FloorPlan p = FloorGenerator.Generate(Spec(seed));
                for (int i = 0; i < p.Rooms.Count; i++)
                    for (int j = i + 1; j < p.Rooms.Count; j++)
                        Assert.IsFalse(p.Rooms[i].Rect.Overlaps(p.Rooms[j].Rect),
                            "seed " + seed + " rooms " + i + " and " + j + " overlap");
            }
        }

        [Test]
        public void Rooms_NeverOverlapCorridorsCoreOrStairs()
        {
            for (int seed = 1; seed <= 40; seed++)
            {
                FloorPlan p = FloorGenerator.Generate(Spec(seed));
                for (int i = 0; i < p.Rooms.Count; i++)
                {
                    RectF r = p.Rooms[i].Rect;
                    Assert.IsFalse(r.Overlaps(p.Core), "seed " + seed + " room " + i + " hits core");
                    for (int c = 0; c < p.Corridors.Count; c++)
                        Assert.IsFalse(r.Overlaps(p.Corridors[c].Rect), "seed " + seed + " room " + i + " hits corridor " + c);
                    for (int s = 0; s < p.Stairs.Count; s++)
                        Assert.IsFalse(r.Overlaps(p.Stairs[s]), "seed " + seed + " room " + i + " hits stair " + s);
                }
            }
        }

        [Test]
        public void Rooms_StayInsideFloorBounds()
        {
            for (int seed = 1; seed <= 40; seed++)
            {
                FloorPlan p = FloorGenerator.Generate(Spec(seed));
                for (int i = 0; i < p.Rooms.Count; i++)
                {
                    RectF r = p.Rooms[i].Rect;
                    Assert.GreaterOrEqual(r.X, 0f);
                    Assert.GreaterOrEqual(r.Y, 0f);
                    Assert.LessOrEqual(r.XMax, p.Spec.Width + 0.01f);
                    Assert.LessOrEqual(r.YMax, p.Spec.Height + 0.01f);
                }
            }
        }

        [Test]
        public void Generator_FillsMostOfTheFloorWithRooms()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                FloorPlan p = FloorGenerator.Generate(Spec(seed));
                float ratio = p.RoomArea / (p.Spec.Width * p.Spec.Height);
                Assert.Greater(ratio, 0.55f, "seed " + seed + " room coverage " + ratio.ToString("P0"));
                Assert.Less(ratio, 0.95f, "seed " + seed + " room coverage " + ratio.ToString("P0"));
            }
        }

        [Test]
        public void Generator_AlwaysProducesExtractionsAndSpawns()
        {
            for (int seed = 1; seed <= 40; seed++)
            {
                FloorPlan p = FloorGenerator.Generate(Spec(seed));
                Assert.GreaterOrEqual(p.Extractions.Count, 4, "seed " + seed);
                Assert.LessOrEqual(p.Extractions.Count, 6, "seed " + seed);
                Assert.AreEqual(4, p.SpawnZones.Count);
                Assert.AreEqual(2, p.Stairs.Count);
            }
        }

        [Test]
        public void Generator_ExtractionsArePickedFromFixedCandidates()
        {
            for (int seed = 1; seed <= 40; seed++)
            {
                FloorPlan p = FloorGenerator.Generate(Spec(seed));
                Assert.GreaterOrEqual(p.ExtractionCandidates.Count, 6, "seed " + seed + " candidates");
                Assert.LessOrEqual(p.ExtractionCandidates.Count, 7, "seed " + seed + " candidates");
                for (int i = 0; i < p.Extractions.Count; i++)
                {
                    bool found = false;
                    for (int c = 0; c < p.ExtractionCandidates.Count; c++)
                    {
                        ExtractionPoint candidate = p.ExtractionCandidates[c];
                        if (candidate.Kind == p.Extractions[i].Kind
                            && candidate.Center == p.Extractions[i].Center) { found = true; break; }
                    }
                    Assert.IsTrue(found, "seed " + seed + " extraction " + i + " is not a physical candidate");
                }
            }
        }

        [Test]
        public void Generator_ExtractionChoiceIsDeterministic()
        {
            for (int seed = 1; seed <= 12; seed++)
            {
                FloorPlan a = FloorGenerator.Generate(Spec(seed));
                FloorPlan b = FloorGenerator.Generate(Spec(seed));
                Assert.AreEqual(a.Extractions.Count, b.Extractions.Count, "seed " + seed);
                for (int i = 0; i < a.Extractions.Count; i++)
                {
                    Assert.AreEqual(a.Extractions[i].Kind, b.Extractions[i].Kind, "seed " + seed + " #" + i);
                    Assert.AreEqual(a.Extractions[i].Center.ToString(), b.Extractions[i].Center.ToString(), "seed " + seed + " #" + i);
                }
            }
        }

        [Test]
        public void Doors_ReachAtLeastNinetyPercentOfRooms()
        {
            int rooms = 0, doorless = 0;
            for (int seed = 1; seed <= 40; seed++)
            {
                FloorPlan p = FloorGenerator.Generate(Spec(seed));
                rooms += p.Rooms.Count;
                for (int i = 0; i < p.Rooms.Count; i++) if (!p.Rooms[i].HasDoor) doorless++;
            }
            float rate = 1f - (float)doorless / Mathf.Max(1, rooms);
            Assert.Greater(rate, 0.9f, "door rate " + rate.ToString("P0") + " (" + doorless + "/" + rooms + " without door)");
        }
    }
}
