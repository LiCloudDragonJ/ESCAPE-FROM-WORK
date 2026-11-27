using NUnit.Framework;
using UnityEngine;
using EscapeFromWork.LevelGen;

namespace EscapeFromWork.Tests.EditMode
{
    /// <summary>
    /// R7：出生区不能挤在一角。验收口径是**步行**距离，不是直线距离——
    /// 两个出生区可能只隔 20 m 直线，但中间隔着核心筒和整排办公室，走起来要绕 90 m。
    ///
    /// 这里刻意用 2 m 粗栅格（FloorValidator.CoarseCell）：1 m 栅格上逐候选 flood 会
    /// 把 1000 种子批测从约 2 分钟拖到 10 分钟（实测 596s），所以生成器内部与测试用同一套粗栅格口径。
    /// </summary>
    public class SpawnSpacingTests
    {
        static FloorPlan Plan(int seed)
        {
            return FloorGenerator.Generate(FloorSpec.Default(seed));
        }

        [Test]
        public void SpawnZones_AreAtLeast35PercentOfDiameterApart()
        {
            for (int seed = 1; seed <= 8; seed++)
            {
                FloorPlan plan = Plan(seed);
                FloorValidator.Grid g = FloorValidator.BuildGrid(plan, FloorValidator.CoarseCell);
                float diameter = FloorValidator.CoarseDiameter(plan, g);
                Assert.That(diameter, Is.GreaterThan(1f), $"seed {seed} 锚点直径异常");

                FloorValidator.SpawnSpacingAudit audit =
                    FloorValidator.AuditSpawnSpacing(plan, g, diameter);

                Assert.That(audit.MinDistance, Is.GreaterThanOrEqualTo(audit.Required - 0.01f),
                    $"seed {seed} 出生区最近步行间距 {audit.MinDistance:F1}m，要求 ≥{audit.Required:F1}m（直径 {diameter:F1}m）");
            }
        }

        [Test]
        public void EverySpawnZoneCanReachAnExtraction()
        {
            for (int seed = 1; seed <= 6; seed++)
            {
                FloorPlan plan = Plan(seed);
                FloorValidator.Grid g = FloorValidator.BuildGrid(plan, FloorValidator.CoarseCell);
                Assert.That(plan.Extractions.Count, Is.GreaterThan(0), $"seed {seed} 没有撤离点");
                for (int s = 0; s < plan.SpawnZones.Count; s++)
                {
                    float[] dist = FloorValidator.Flood(g, plan.SpawnZones[s].Center);
                    bool reached = false;
                    for (int e = 0; e < plan.Extractions.Count && !reached; e++)
                        reached = FloorValidator.WalkDistanceAt(g, dist, plan.Extractions[e].Center) >= 0f;
                    Assert.That(reached, Is.True, $"seed {seed} 出生区 {s} 走不到任何撤离点");
                }
            }
        }

        [Test]
        public void SpawnZones_AreDeterministicForSameSeed()
        {
            for (int seed = 1; seed <= 4; seed++)
            {
                FloorPlan a = Plan(seed);
                FloorPlan b = Plan(seed);
                Assert.That(a.SpawnZones.Count, Is.EqualTo(b.SpawnZones.Count), $"seed {seed}");
                for (int i = 0; i < a.SpawnZones.Count; i++)
                {
                    Assert.That(a.SpawnZones[i].X, Is.EqualTo(b.SpawnZones[i].X).Within(0.001f), $"seed {seed} 出生区 {i} x");
                    Assert.That(a.SpawnZones[i].Y, Is.EqualTo(b.SpawnZones[i].Y).Within(0.001f), $"seed {seed} 出生区 {i} y");
                }
            }
        }
    }
}
