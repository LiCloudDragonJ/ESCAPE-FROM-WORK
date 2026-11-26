using System.Collections.Generic;
using EscapeFromWork.LevelGen;
using NUnit.Framework;
using UnityEngine;

namespace EscapeFromWork.Tests.EditMode
{
    public class SingleDoorTests
    {
        const int SeedCount = 100;

        static FloorSpec Spec(int seed)
        {
            FloorSpec spec = FloorSpec.Default(seed);
            spec.Width = 200f;
            spec.Height = 160f;
            spec.Band = FloorBand.Tower;
            spec.Prototype = FloorPrototype.OpenOffice;
            return spec;
        }

        static List<Door> Doors(Room room)
        {
            List<Door> result = new List<Door>();
            if (room.HasDoor) result.Add(room.Door);
            if (room.ExtraDoors != null) result.AddRange(room.ExtraDoors);
            return result;
        }

        [Test]
        public void OrdinaryRooms_HaveTwoDoorsOnDifferentWalls()
        {
            for (int seed = 1; seed <= SeedCount; seed++)
            {
                FloorPlan plan = FloorGenerator.Generate(Spec(seed));
                for (int i = 0; i < plan.Rooms.Count; i++)
                {
                    List<Door> doors = Doors(plan.Rooms[i]);
                    Assert.GreaterOrEqual(doors.Count, 2, "seed " + seed + " room " + i + " has fewer than two doors");
                    int mask = 0;
                    for (int d = 0; d < doors.Count; d++) mask |= 1 << (int)doors[d].Side;
                    int sides = 0;
                    for (int s = 0; s < 4; s++) if ((mask & (1 << s)) != 0) sides++;
                    Assert.GreaterOrEqual(sides, 2, "seed " + seed + " room " + i + " doors share one wall");
                }
            }
        }

        [Test]
        public void LargeRooms_AndHalls_HaveThreeDoorsAndHallEndEgress()
        {
            for (int seed = 1; seed <= SeedCount; seed++)
            {
                FloorPlan plan = FloorGenerator.Generate(Spec(seed));
                for (int i = 0; i < plan.Rooms.Count; i++)
                {
                    Room room = plan.Rooms[i];
                    List<Door> doors = Doors(room);
                    if (room.Rect.Area >= 100f)
                        Assert.GreaterOrEqual(doors.Count, 3, "seed " + seed + " large room " + i);
                    if (room.Rect.Area <= 400f) continue;

                    bool horizontal = room.Rect.W >= room.Rect.H;
                    DoorSide a = horizontal ? DoorSide.South : DoorSide.West;
                    DoorSide b = horizontal ? DoorSide.North : DoorSide.East;
                    float min = horizontal ? room.Rect.X : room.Rect.Y;
                    float max = horizontal ? room.Rect.XMax : room.Rect.YMax;
                    bool nearMin = false, nearMax = false;
                    for (int d = 0; d < doors.Count; d++)
                    {
                        Door door = doors[d];
                        if (door.Side != a && door.Side != b) continue;
                        float tangent = RoomDoorPolicy.Tangent(door);
                        nearMin |= Mathf.Abs(tangent - min) <= 1.2f + door.Width * 0.5f + 0.04f;
                        nearMax |= Mathf.Abs(tangent - max) <= 1.2f + door.Width * 0.5f + 0.04f;
                    }
                    Assert.IsTrue(nearMin && nearMax, "seed " + seed + " hall " + i + " lacks long-side end egress");
                }
            }
        }

        [Test]
        public void DoorEdges_StayInsideCornerClearanceBands()
        {
            for (int seed = 1; seed <= SeedCount; seed++)
            {
                FloorPlan plan = FloorGenerator.Generate(Spec(seed));
                for (int i = 0; i < plan.Rooms.Count; i++)
                {
                    Room room = plan.Rooms[i];
                    List<Door> doors = Doors(room);
                    for (int d = 0; d < doors.Count; d++)
                    {
                        Door door = doors[d];
                        bool vertical = door.Side == DoorSide.East || door.Side == DoorSide.West;
                        float min = vertical ? room.Rect.Y : room.Rect.X;
                        float max = vertical ? room.Rect.YMax : room.Rect.XMax;
                        float tangent = RoomDoorPolicy.Tangent(door);
                        float half = door.Width * 0.5f;
                        float offset = Mathf.Min(Mathf.Abs(tangent - half - min), Mathf.Abs(max - tangent - half));
                        bool band = (offset >= 0.12f && offset <= 0.53f) || (offset >= 0.87f && offset <= 1.23f);
                        Assert.IsTrue(band, "seed " + seed + " room " + i + " door " + d + " corner offset " + offset.ToString("F3"));
                    }
                }
            }
        }

        [Test]
        public void OpposingDoors_AreOffsetByAtLeastOnePointTenMetres()
        {
            for (int seed = 1; seed <= SeedCount; seed++)
            {
                FloorPlan plan = FloorGenerator.Generate(Spec(seed));
                for (int i = 0; i < plan.Rooms.Count; i++)
                {
                    List<Door> doors = Doors(plan.Rooms[i]);
                    for (int a = 0; a < doors.Count; a++)
                    {
                        for (int b = a + 1; b < doors.Count; b++)
                        {
                            if (!RoomDoorPolicy.AreOpposite(doors[a].Side, doors[b].Side)) continue;
                            float stagger = Mathf.Abs(RoomDoorPolicy.Tangent(doors[a]) - RoomDoorPolicy.Tangent(doors[b]));
                            Assert.GreaterOrEqual(stagger, RoomDoorPolicy.MinOppositeStagger - 0.02f,
                                "seed " + seed + " room " + i + " opposing doors " + a + "/" + b);
                        }
                    }
                }
            }
        }
    }
}
