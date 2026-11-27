using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EscapeFromWork.LevelGen;

namespace EscapeFromWork.Tests.EditMode
{
    public class LevelAuditTests
    {
        static FloorPlan EmptyPlan(float width = 200f, float height = 160f)
        {
            FloorPlan plan = new FloorPlan { Bounds = new RectF(0f, 0f, width, height), Spec = FloorSpec.Default(1) };
            plan.Rooms.Add(new Room { Rect = new RectF(width - 12f, height - 12f, 6f, 6f), ExtraDoors = new List<Door>() });
            return plan;
        }

        static Corridor Horizontal(float start, float end, float y = 50f, bool vestibule = false)
        {
            return new Corridor
            {
                Rect = new RectF(start, y, end - start, 6f),
                Main = !vestibule,
                Vestibule = vestibule,
                Vertical = false
            };
        }

        static Door Opening(float x, float y, DoorSide side, int corridor = 0, float width = 1.4f)
        {
            return new Door { Center = new Vector2(x, y), Side = side, Corridor = corridor, OtherRoom = -1, Width = width };
        }

        [TestCase(29.999f, 0)]
        [TestCase(30.001f, 1)]
        public void StraightRun_UsesThirtyMeterBoundary(float length, int expected)
        {
            FloorPlan plan = EmptyPlan();
            plan.Corridors.Add(Horizontal(0f, length));
            plan.CorridorOpenings.Add(Opening(length * 0.5f, 56f, DoorSide.North));
            LevelAudit.FloorReport report = LevelAudit.AuditFloor(plan);
            Assert.AreEqual(expected, report.OverlongCorridors);
        }

        [Test]
        public void TouchingCoaxialSegments_AreOneStraightRun()
        {
            FloorPlan plan = EmptyPlan();
            plan.Corridors.Add(Horizontal(0f, 16f));
            plan.Corridors.Add(Horizontal(16f, 32f));
            plan.CorridorOpenings.Add(Opening(16f, 56f, DoorSide.North));
            LevelAudit.FloorReport report = LevelAudit.AuditFloor(plan);
            Assert.AreEqual(32f, report.LongestCorridor, 0.001f);
            Assert.AreEqual(1, report.OverlongCorridors);
        }

        [Test]
        public void ThreeMeterVestibule_SeparatesStraightRuns()
        {
            FloorPlan plan = EmptyPlan();
            plan.Corridors.Add(Horizontal(0f, 16f));
            plan.Corridors.Add(Horizontal(16f, 19f, 50f, true));
            plan.Corridors.Add(Horizontal(19f, 35f));
            Assert.AreEqual(0, LevelAudit.AuditFloor(plan).OverlongCorridors);
        }

        [Test]
        public void ParallelSegments_OnDifferentAcrossPositions_DoNotMerge()
        {
            FloorPlan plan = EmptyPlan();
            plan.Corridors.Add(Horizontal(0f, 16f, 50f));
            plan.Corridors.Add(Horizontal(0f, 16f, 60f));
            Assert.AreEqual(0, LevelAudit.AuditFloor(plan).OverlongCorridors);
        }

        [TestCase(60f, 30f, 0)]
        [TestCase(60.02f, 30.01f, 1)]
        public void LateralExitGap_UsesThirtyMeterBoundary(float length, float exit, int expected)
        {
            FloorPlan plan = EmptyPlan();
            plan.Corridors.Add(Horizontal(0f, length));
            plan.CorridorOpenings.Add(Opening(exit, 56f, DoorSide.North));
            Assert.AreEqual(expected, LevelAudit.AuditFloor(plan).MissingSideExits);
        }

        [Test]
        public void DenseLateralExits_AreLegal()
        {
            FloorPlan plan = EmptyPlan();
            plan.Corridors.Add(Horizontal(0f, 60f));
            for (int x = 10; x <= 50; x += 10) plan.CorridorOpenings.Add(Opening(x, 56f, DoorSide.North));
            Assert.AreEqual(0, LevelAudit.AuditFloor(plan).MissingSideExits);
        }

        [Test]
        public void CrossWallOrDeadEnd_IsNotALateralExit()
        {
            FloorPlan crossDoor = EmptyPlan();
            crossDoor.Corridors.Add(Horizontal(0f, 20f));
            crossDoor.CorridorOpenings.Add(Opening(20f, 56f, DoorSide.East));
            Assert.AreEqual(1, LevelAudit.AuditFloor(crossDoor).MissingSideExits);

            FloorPlan deadWall = EmptyPlan();
            deadWall.Corridors.Add(Horizontal(0f, 20f));
            Assert.AreEqual(1, LevelAudit.AuditFloor(deadWall).MissingSideExits);
        }

        static FloorPlan DeadBranchPlan()
        {
            FloorPlan plan = EmptyPlan(40f, 40f);
            plan.Corridors.Add(Horizontal(4f, 36f, 18f));
            return plan;
        }

        [Test]
        public void LongDeadCorridorBranch_IsReported()
        {
            Assert.Greater(LevelAudit.AuditFloor(DeadBranchPlan()).DeadEndCorridors, 0);
        }

        [Test]
        public void TerminalOpenings_ClearDeadCorridorBranches()
        {
            FloorPlan plan = DeadBranchPlan();
            plan.CorridorOpenings.Add(Opening(4f, 21f, DoorSide.West));
            plan.CorridorOpenings.Add(Opening(36f, 21f, DoorSide.East));
            Assert.AreEqual(0, LevelAudit.AuditFloor(plan).DeadEndCorridors);
        }

        [Test]
        public void TouchingRoomWithoutDoor_DoesNotClearDeadBranch()
        {
            FloorPlan plan = DeadBranchPlan();
            plan.Rooms.Add(new Room { Rect = new RectF(36f, 18f, 4f, 6f), ExtraDoors = new List<Door>() });
            Assert.Greater(LevelAudit.AuditFloor(plan).DeadEndCorridors, 0);
        }

        static FloorPlan AttachedRoomPlan()
        {
            FloorPlan plan = EmptyPlan(30f, 20f);
            plan.Rooms.Clear();
            plan.Corridors.Add(Horizontal(0f, 20f, 0f));
            Room room = new Room { Rect = new RectF(4f, 4f, 6f, 6f), ExtraDoors = new List<Door>() };
            room.Door = Opening(7f, 4f, DoorSide.South);
            room.HasDoor = true;
            plan.Rooms.Add(room);
            return plan;
        }

        [Test]
        public void DeclaredConnectedDoor_AttachesRoom()
        {
            LevelAudit.FloorReport report = LevelAudit.AuditFloor(AttachedRoomPlan());
            Assert.AreEqual(0, report.UnattachedRooms);
            Assert.AreEqual(1f, report.RoomAttachmentRate, 0.001f);
            Assert.AreEqual(1, report.DeadEndRooms);
        }

        [Test]
        public void TouchingRoomWithoutDeclaredTarget_IsUnattached()
        {
            FloorPlan plan = AttachedRoomPlan();
            Room room = plan.Rooms[0];
            room.Door.Corridor = -1;
            room.Door.OtherRoom = -1;
            plan.Rooms[0] = room;
            Assert.AreEqual(1, LevelAudit.AuditFloor(plan).UnattachedRooms);
        }

        [Test]
        public void DuplicateFragmentsOfSameCorridor_DoNotCreateSecondDegree()
        {
            FloorPlan plan = AttachedRoomPlan();
            plan.Corridors.Clear();
            plan.Corridors.Add(Horizontal(0f, 10f, 0f));
            plan.Corridors.Add(Horizontal(10f, 20f, 0f));
            Room room = plan.Rooms[0];
            room.Rect = new RectF(4f, 4f, 10f, 6f);
            room.Door = Opening(7f, 4f, DoorSide.South, 0);
            room.ExtraDoors.Add(Opening(12f, 4f, DoorSide.South, 1));
            plan.Rooms[0] = room;
            LevelAudit.FloorReport report = LevelAudit.AuditFloor(plan);
            Assert.AreEqual(0, report.UnattachedRooms);
            Assert.AreEqual(1, report.DeadEndRooms);
        }

        static FloorPlan DeadRoomRatioPlan(int count, int dead)
        {
            FloorPlan plan = EmptyPlan(count * 10f, 20f);
            plan.Rooms.Clear();
            for (int i = 0; i < count; i++)
            {
                float x = i * 10f;
                plan.Corridors.Add(Horizontal(x, x + 4f, 0f));
                plan.Corridors.Add(Horizontal(x, x + 4f, 10f));
                Room room = new Room { Rect = new RectF(x, 4f, 4f, 6f), ExtraDoors = new List<Door>() };
                room.Door = Opening(x + 2f, 4f, DoorSide.South, i * 2);
                room.HasDoor = true;
                if (i >= dead) room.ExtraDoors.Add(Opening(x + 2f, 10f, DoorSide.North, i * 2 + 1));
                plan.Rooms.Add(room);
            }
            return plan;
        }

        [TestCase(12, 1, false)]
        [TestCase(13, 1, true)]
        [TestCase(25, 2, true)]
        public void DeadEndRoomRatio_RespectsEightPercentBoundary(int count, int dead, bool passes)
        {
            LevelAudit.FloorReport report = LevelAudit.AuditFloor(DeadRoomRatioPlan(count, dead));
            Assert.AreEqual(0, report.UnattachedRooms);
            Assert.AreEqual(dead, report.DeadEndRooms);
            Assert.AreEqual(dead / (float)count, report.DeadEndRoomRatio, 0.0001f);
            Assert.AreEqual(passes, report.DeadEndRoomRatio <= LevelAudit.MaxDeadEndRoomRatio + 0.0001f);
        }

        [Test]
        public void EmptyFloor_IsInvalidNotASuccess()
        {
            FloorPlan plan = EmptyPlan();
            plan.Rooms.Clear();
            LevelAudit.FloorReport report = LevelAudit.AuditFloor(plan);
            Assert.AreEqual(1, report.InvalidInputs);
            Assert.Greater(report.StructuralViolationCount, 0);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void GeneratedFloors_HaveNoOverlongStraightRuns(int seed)
        {
            Assert.AreEqual(0, LevelAudit.AuditFloor(FloorGenerator.Generate(FloorSpec.Default(seed))).OverlongCorridors);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void GeneratedFloors_HaveNoDeadCorridors(int seed)
        {
            Assert.AreEqual(0, LevelAudit.AuditFloor(FloorGenerator.Generate(FloorSpec.Default(seed))).DeadEndCorridors);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void GeneratedFloors_AttachEveryRoom(int seed)
        {
            Assert.AreEqual(0, LevelAudit.AuditFloor(FloorGenerator.Generate(FloorSpec.Default(seed))).UnattachedRooms);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void GeneratedFloors_RespectLateralExitSpacing(int seed)
        {
            LevelAudit.FloorReport report = LevelAudit.AuditFloor(FloorGenerator.Generate(FloorSpec.Default(seed)));
            Assert.AreEqual(0, report.MissingSideExits, string.Join("\n", report.Violations));
        }

        [Test]
        public void SliceSightlines_DoNotSampleSealedAreas()
        {
            FloorPlan plan = EmptyPlan();
            plan.Corridors.Add(Horizontal(0f, 60f));
            SightlineAudit.Report full = SightlineAudit.Run(plan);
            Assert.Greater(full.Samples, 0);
            RaidPlan raid = new RaidPlan();
            raid.Floors.Add(new SliceFloor
            {
                FloorIndex = 0,
                Plan = plan,
                Open = new List<RectF> { new RectF(150f, 150f, 5f, 5f) },
                Sealed = new List<SealedArea> { new SealedArea { Rect = plan.Bounds, Reason = SealReason.Cordon } }
            });
            Assert.AreEqual(0, LevelAudit.AuditSlice(raid, true).SightlineSamples);
        }

        [Test]
        public void GeneratedRaid_OpenAreaSightlinesStayWithinFortyFiveMeters()
        {
            LevelAudit.SliceReport report = LevelAudit.AuditSlice(SlicePlanner.Generate(1), true);
            Assert.Greater(report.SightlineSamples, 0);
            string detail = "";
            for (int i = 0; i < report.Floors.Count; i++)
            {
                LevelAudit.FloorReport f = report.Floors[i];
                if (f.Sightlines.GlobalMax <= 45f) continue;
                detail += " F" + f.FloorIndex + " " + f.Sightlines.GlobalMax.ToString("F1") +
                    "m from (" + f.Sightlines.From.x.ToString("F1") + "," + f.Sightlines.From.y.ToString("F1") +
                    ") to (" + f.Sightlines.To.x.ToString("F1") + "," + f.Sightlines.To.y.ToString("F1") + ")";
                for (int v = 0; v < f.Violations.Count; v++)
                    if (f.Violations[v].StartsWith("slice open-area sightline")) detail += " [" + f.Violations[v] + "]";
            }
            Assert.That(report.WorstSightline, Is.LessThanOrEqualTo(45f),
                "slice open-area sightline must use the same 45m limit as a single floor" + detail);
        }
    }
}
