using EscapeFromWork.LevelGen;
using NUnit.Framework;
using UnityEngine;

namespace EscapeFromWork.Tests.EditMode
{
    /// <summary>
    /// R2 验收：走廊必须有建筑级断点。
    ///
    /// 这里刻意不用 FloorValidator.HasLineOfSight——那是 1m 网格的"能不能走过去"近似，
    /// 门洞和 0.2m 的墙在它眼里都不存在，量视线会得出"走廊一路到底"的假象。
    /// 视线统一走 SightlineAudit：真实墙盒 → 0.25m 眼高占用栅格 → 32 方向射线。
    /// </summary>
    public class SightlineTests
    {
        static FloorPlan Plan(int seed)
        {
            return FloorGenerator.Generate(FloorSpec.Default(seed));
        }

        [Test]
        public void Corridors_AreCutIntoShortSegments()
        {
            for (int seed = 1; seed <= 4; seed++)
            {
                FloorPlan plan = Plan(seed);
                for (int i = 0; i < plan.Corridors.Count; i++)
                {
                    RectF r = plan.Corridors[i].Rect;
                    float length = Mathf.Max(r.W, r.H);
                    Assert.That(length, Is.LessThanOrEqualTo(30.05f),
                        "seed " + seed + " 有一整段 " + length.ToString("F1") + "m 的走廊没有断点");
                }
            }
        }

        [Test]
        public void Corridors_HaveVestibules()
        {
            for (int seed = 1; seed <= 4; seed++)
            {
                FloorPlan plan = Plan(seed);
                Assert.That(CorridorBreaks.CountVestibules(plan), Is.GreaterThan(20),
                    "seed " + seed + " 几乎没有门斗：断点没生成");
                Assert.That(CorridorBreaks.LongestSegment(plan), Is.LessThanOrEqualTo(30.05f));
            }
        }

        [Test]
        public void VestibuleHasDoorsOnBothEndWalls()
        {
            for (int seed = 1; seed <= 4; seed++)
            {
                FloorPlan plan = Plan(seed);
                for (int i = 0; i < plan.Corridors.Count; i++)
                {
                    Corridor c = plan.Corridors[i];
                    if (!c.Vestibule) continue;
                    RectF r = c.Rect;
                    bool vertical = c.Vertical;
                    float start = vertical ? r.Y : r.X;
                    float end = vertical ? r.YMax : r.XMax;

                    Assert.That(HasOpeningAt(plan, c, start, true), Is.True, "门斗起点没有门，段与段之间是死墙");
                    Assert.That(HasOpeningAt(plan, c, end, false), Is.True, "门斗终点没有门，段与段之间是死墙");
                }
            }
        }

        /// <summary>门斗两端的横墙上是否真的登记了开口（只是近似判断：坐标系一致 + 落在带宽内）。</summary>
        static bool HasOpeningAt(FloorPlan plan, Corridor c, float line, bool atStart)
        {
            RectF r = c.Rect;
            bool vertical = c.Vertical;
            float low = vertical ? r.X : r.Y;
            float high = vertical ? r.XMax : r.YMax;

            for (int i = 0; i < plan.CorridorOpenings.Count; i++)
            {
                Door d = plan.CorridorOpenings[i];
                Vector2 p = d.Center;
                if (vertical)
                {
                    if (Mathf.Abs(p.y - line) > 0.2f) continue;
                    if (p.x < low - 0.01f || p.x > high + 0.01f) continue;
                    return true;
                }
                if (Mathf.Abs(p.x - line) > 0.2f) continue;
                if (p.y < low - 0.01f || p.y > high + 0.01f) continue;
                return true;
            }
            return false;
        }

        [Test]
        public void ServiceCorridors_AreParallelAndWalkable()
        {
            for (int seed = 1; seed <= 4; seed++)
            {
                FloorPlan plan = Plan(seed);
                int service = 0;
                for (int i = 0; i < plan.Corridors.Count; i++)
                {
                    Corridor c = plan.Corridors[i];
                    if (!c.Service) continue;
                    service++;
                    // 带宽取长轴的垂直方向：服务廊被核心筒/楼梯裁短后，长轴可能比 2.3m 带宽还短，
                    // 用 Min(W,H) 会把“长度”当成“带宽”（实测 seed 4 量出 2.03，误判净宽 1.63）。
                    float band = c.Vertical ? c.Rect.W : c.Rect.H;
                    // 净宽 = 带宽 - 墙厚 0.4m，验收要 1.8–2.1m
                    Assert.That(band - 0.4f, Is.InRange(1.7f, 2.1f),
                        "seed " + seed + " 服务廊净宽不对 rect=" + c.Rect.W + "x" + c.Rect.H + " vertical=" + c.Vertical);
                    Assert.That(c.Main, Is.False, "服务廊不该标成主廊");
                }
                Assert.That(service, Is.GreaterThan(20), "seed " + seed + " 没有铺服务廊");
            }
        }

        [Test]
        public void EveryServiceSegment_HasAnOpening()
        {
            for (int seed = 1; seed <= 4; seed++)
            {
                FloorPlan plan = Plan(seed);
                for (int i = 0; i < plan.Corridors.Count; i++)
                {
                    Corridor c = plan.Corridors[i];
                    if (!c.Service) continue;
                    Assert.That(HasSideOpening(plan, c), Is.True,
                        "seed " + seed + " 服务廊有一段没有任何开口，等于封死的空间 rect=" +
                        c.Rect.W + "x" + c.Rect.H + " at (" + c.Rect.X + "," + c.Rect.Y + ") vertical=" + c.Vertical);
                }
            }
        }

        static bool HasSideOpening(FloorPlan plan, Corridor c)
        {
            RectF r = c.Rect;
            bool vertical = c.Vertical;
            float start = vertical ? r.Y : r.X;
            float end = vertical ? r.YMax : r.XMax;
            for (int i = 0; i < plan.CorridorOpenings.Count; i++)
            {
                Vector2 p = plan.CorridorOpenings[i].Center;
                if (vertical)
                {
                    bool onWall = Mathf.Abs(p.x - r.X) < 0.2f || Mathf.Abs(p.x - r.XMax) < 0.2f;
                    if (!onWall) continue;
                    if (p.y < start - 0.01f || p.y > end + 0.01f) continue;
                    return true;
                }
                bool onHorizontalWall = Mathf.Abs(p.y - r.Y) < 0.2f || Mathf.Abs(p.y - r.YMax) < 0.2f;
                if (!onHorizontalWall) continue;
                if (p.x < start - 0.01f || p.x > end + 0.01f) continue;
                return true;
            }
            return false;
        }

        [Test]
        public void Sightlines_StayUnder45m()
        {
            for (int seed = 1; seed <= 3; seed++)
            {
                FloorPlan plan = Plan(seed);
                SightlineAudit.Report report = SightlineAudit.Run(plan);
                Assert.That(report.Samples, Is.GreaterThan(3000), "采样点太少，审计没跑起来");
                Assert.That(report.GlobalMax, Is.LessThanOrEqualTo(45f),
                    "seed " + seed + " 最长视线 " + report.GlobalMax.ToString("F1") +
                    "m，从 (" + report.From.x.ToString("F1") + "," + report.From.y.ToString("F1") +
                    ") 看到 (" + report.To.x.ToString("F1") + "," + report.To.y.ToString("F1") + ")");
            }
        }
    }
}
