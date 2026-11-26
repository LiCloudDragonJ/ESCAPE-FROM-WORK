using System.Collections.Generic;
using EscapeFromWork.LevelGen;
using NUnit.Framework;
using UnityEngine;

namespace EscapeFromWork.Tests
{
    public class ExtractionSpreadTests
    {
        static FloorSpec Spec(int seed)
        {
            FloorSpec spec = FloorSpec.Default(seed);
            int band = seed % 3;
            spec.Band = band == 0 ? FloorBand.Podium : band == 1 ? FloorBand.Tower : FloorBand.Top;
            spec.Width = spec.Band == FloorBand.Podium ? 280f : spec.Band == FloorBand.Tower ? 200f : 160f;
            spec.Height = spec.Band == FloorBand.Podium ? 280f : spec.Band == FloorBand.Tower ? 160f : 120f;
            spec.Prototype = (FloorPrototype)(seed % 6);
            spec.CorridorLevels = 3;
            return spec;
        }

        [Test]
        public void EverySpawnZoneHasTwoFarExtractions()
        {
            for (int seed = 1; seed <= 120; seed++)
            {
                FloorPlan plan = FloorGenerator.Generate(Spec(seed));
                FloorValidator.Grid grid = FloorValidator.BuildGrid(plan);
                List<FloorValidator.SpawnAudit> audits = FloorValidator.AuditSpawns(plan, grid);
                Assert.AreEqual(plan.SpawnZones.Count, audits.Count, "seed " + seed);

                for (int i = 0; i < audits.Count; i++)
                {
                    FloorValidator.SpawnAudit a = audits[i];
                    Assert.GreaterOrEqual(a.FarExtractions, 2,
                        "seed " + seed + " spawn " + i + " far extractions=" + a.FarExtractions
                        + " required=" + a.RequiredDistance.ToString("0") + "m"
                        + " applied=" + a.AppliedDistance.ToString("0") + "m");
                }
            }
        }

        [Test]
        public void RequiredDistance_IsSixtyPercentOfDiagonal()
        {
            FloorPlan plan = FloorGenerator.Generate(Spec(3));
            float diag = Mathf.Sqrt(plan.Bounds.W * plan.Bounds.W + plan.Bounds.H * plan.Bounds.H);
            Assert.AreEqual(diag * 0.6f, FloorValidator.RequiredExtractionDistance(plan), 0.01f);
        }

        [Test]
        public void WalkDistance_FollowsRoomsNotStraightLines()
        {
            FloorPlan plan = FloorGenerator.Generate(Spec(1));
            FloorValidator.Grid grid = FloorValidator.BuildGrid(plan);
            Vector2 a = plan.SpawnZones[0].Center;
            Vector2 b = plan.SpawnZones[2].Center;

            float walk = FloorValidator.WalkDistance(grid, a, b);
            float straight = Vector2.Distance(a, b);
            Assert.Greater(walk, 0f);
            Assert.GreaterOrEqual(walk, straight * 0.95f, "walking can never beat the straight line");
        }
    }
}
