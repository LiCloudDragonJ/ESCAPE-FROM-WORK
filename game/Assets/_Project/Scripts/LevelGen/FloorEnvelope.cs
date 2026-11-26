using System.Collections.Generic;
using UnityEngine;

namespace EscapeFromWork.LevelGen
{
    /// <summary>
    /// 楼层外廓：一个正交 + 45° 斜边的简单多边形，外加它的凸块分解。
    /// 房间、地板、外墙都从这里长出来——这是"房间贴着不方外廓生成"的几何真值。
    /// </summary>
    public sealed class EnvelopeShape
    {
        /// <summary>外廓环（CCW，XZ 平面：x = 世界 x，y = 世界 z）。</summary>
        public Vector2[] Outline;
        /// <summary>外廓的凸块分解：并集等于外廓，互不重叠。裁剪房间、铺地板都走这里。</summary>
        public List<Vector2[]> Pieces;
        /// <summary>外廓面积（m²）。</summary>
        public float Area;
        /// <summary>边上挖掉的缺口数量（0–2）。</summary>
        public int NotchCount;

        public RectF Bounds;
    }

    public static class FloorEnvelope
    {
        /// <summary>外廓至少要比包围盒小这么多，否则等于没做。</summary>
        public const float MinCutFraction = 0.06f;

        // ---------------------------------------------------------------- build

        /// <summary>
        /// 生成外廓：矩形板减掉角上的 45° 截角、再在边上挖 0–2 个缺口。
        /// keepOut 里的矩形（核心筒、楼梯、撤离设施）不能被缺口吃进去。
        /// </summary>
        public static EnvelopeShape Build(float width, float height, System.Random rng, IList<RectF> keepOut)
        {
            float minSide = Mathf.Min(width, height);
            float maxChamfer = Mathf.Clamp(minSide * 0.3f, 10f, 46f);
            float maxNotchDepth = Mathf.Clamp(minSide * 0.14f, 6f, 20f);

            // 1) 角：2–4 个角实体地切出 45° 斜边。少于两个角从外面看还是方盒子。
            //    尺寸不再随机乱取：先定"这一层要削掉多少面积"，再反解每个角的斜边长度，
            //    这样每一层都保证是明显的非矩形（≥ MinCutFraction 的一半以上）。
            float plateArea = width * height;
            float cutFraction = Mathf.Lerp(MinCutFraction * 0.5f, MinCutFraction * 0.95f, (float)rng.NextDouble());
            float[] chamfer = new float[4];
            int cutCount = 2 + rng.Next(3);
            int[] order = { 0, 1, 2, 3 };
            for (int i = 3; i > 0; i--) { int j = rng.Next(i + 1); int t = order[i]; order[i] = order[j]; order[j] = t; }
            float perCorner = plateArea * cutFraction / cutCount;
            for (int i = 0; i < cutCount; i++)
            {
                // 45° 斜边的三角形面积 = size² / 2
                float s = Mathf.Sqrt(perCorner * 2f) * Mathf.Lerp(0.85f, 1.15f, (float)rng.NextDouble());
                s = Mathf.Clamp(Mathf.Round(s), 8f, maxChamfer);
                // 斜边不能吃掉整条边
                float sideLimit = (Mathf.Min(width, height) - 24f) * 0.5f;
                chamfer[order[i]] = Mathf.Min(s, Mathf.Max(6f, sideLimit));
            }

            // 2) 缺口：先按矩形板算候选，再按 keepOut 与角截角的关系过滤
            List<RectF> notches = BuildNotches(width, height, maxNotchDepth, chamfer, keepOut, rng);

            // 3) 凸块：矩形板逐个减掉缺口（全是矩形减矩形，不会退化），再用截角半平面裁
            List<Vector2[]> pieces = new List<Vector2[]>();
            List<RectF> rects = new List<RectF>();
            rects.Add(new RectF(0f, 0f, width, height));
            for (int i = 0; i < notches.Count; i++) rects = Subtract(rects, notches[i]);

            for (int i = 0; i < rects.Count; i++)
            {
                Vector2[] piece = RectPoly(rects[i]);
                for (int c = 0; c < 4; c++)
                {
                    if (chamfer[c] <= 0.01f) continue;
                    piece = ClipHalfPlane(piece, ChamferNormal(c), ChamferOffset(c, chamfer[c], width, height));
                    if (piece == null || piece.Length < 3) break;
                }
                if (piece != null && piece.Length >= 3 && Mathf.Abs(SignedArea(piece)) > 0.5f)
                {
                    if (SignedArea(piece) < 0f) System.Array.Reverse(piece);
                    pieces.Add(piece);
                }
            }

            // 4) 外轮廓环：按 CCW 顺序拼出来（角截角 + 边上的缺口）
            Vector2[] outline = BuildOutline(width, height, chamfer, notches);

            EnvelopeShape shape = new EnvelopeShape();
            shape.Outline = outline;
            shape.Pieces = pieces;
            shape.Area = Mathf.Abs(SignedArea(outline));
            shape.NotchCount = notches.Count;
            shape.Bounds = new RectF(0f, 0f, width, height);
            return shape;
        }

        struct Notch
        {
            public int Side;      // 0=南 +X，1=东 +Y，2=北 -X，3=西 -Y
            public float A, B;    // 沿边的坐标区间
            public float Depth;
        }

        static List<RectF> BuildNotches(float w, float h, float maxDepth, float[] chamfer,
                                        IList<RectF> keepOut, System.Random rng)
        {
            List<RectF> result = new List<RectF>();
            int count = rng.NextDouble() < 0.55 ? 1 : 2;
            float[] sideLen = { w, h, w, h };
            // 每条边最多一个缺口，且与角截角留出 8m 余量
            List<int> sides = new List<int> { 0, 1, 2, 3 };
            for (int i = 3; i > 0; i--) { int j = rng.Next(i + 1); int t = sides[i]; sides[i] = sides[j]; sides[j] = t; }

            int made = 0;
            for (int si = 0; si < sides.Count && made < count; si++)
            {
                int side = sides[si];
                float len = sideLen[side];
                float margin = 8f + Mathf.Max(chamfer[side], chamfer[(side + 1) % 4]);
                float notchW = Mathf.Round(Mathf.Lerp(9f, Mathf.Min(24f, len * 0.22f), (float)rng.NextDouble()));
                if (len - margin * 2f < notchW + 6f) continue;
                float depth = Mathf.Round(Mathf.Lerp(6f, maxDepth, (float)rng.NextDouble()));

                // 位置被 keepOut 挡住时在同一条边上换个位置重试，而不是直接放弃这个缺口。
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    float a = Mathf.Round(margin + (float)rng.NextDouble() * (len - margin * 2f - notchW));
                    RectF notch = NotchRect(side, a, a + notchW, depth, w, h);
                    bool blocked = false;
                    for (int k = 0; k < keepOut.Count; k++)
                    {
                        if (notch.Overlaps(keepOut[k].Inflate(4f))) { blocked = true; break; }
                    }
                    if (blocked) continue;
                    result.Add(notch);
                    made++;
                    break;
                }
            }
            return result;
        }

        static RectF NotchRect(int side, float a, float b, float depth, float w, float h)
        {
            switch (side)
            {
                case 0: return new RectF(a, 0f, b - a, depth);            // 南：向 +Y 吃进去
                case 1: return new RectF(w - depth, a, depth, b - a);     // 东：向 -X
                case 2: return new RectF(a, h - depth, b - a, depth);     // 北：向 -Y
                default: return new RectF(0f, a, depth, b - a);           // 西：向 +X
            }
        }

        static Vector2 ChamferNormal(int corner)
        {
            // 指向外廓内部的法线（半平面保留 dot(p, n) <= offset 的一侧）
            switch (corner)
            {
                case 0: return new Vector2(1f, 1f).normalized;        // 西南
                case 1: return new Vector2(-1f, 1f).normalized;       // 东南
                case 2: return new Vector2(-1f, -1f).normalized;      // 东北
                default: return new Vector2(1f, -1f).normalized;      // 西北
            }
        }

        /// <summary>保留 dot(p, n) &gt;= offset 的一侧：n 指向内部，offset 是离角点 size/√2 的那条弦。</summary>
        static float ChamferOffset(int corner, float size, float w, float h)
        {
            Vector2 p = CornerPoint(corner, w, h);
            Vector2 n = ChamferNormal(corner);
            return Vector2.Dot(p, n) + size * 0.70710678f;
        }

        static Vector2 CornerPoint(int corner, float w, float h)
        {
            switch (corner)
            {
                case 0: return new Vector2(0f, 0f);
                case 1: return new Vector2(w, 0f);
                case 2: return new Vector2(w, h);
                default: return new Vector2(0f, h);
            }
        }

        static Vector2[] BuildOutline(float w, float h, float[] chamfer, List<RectF> notches)
        {
            // 把缺口换算成"边 + 区间 + 深度"
            List<Notch> list = new List<Notch>();
            for (int i = 0; i < notches.Count; i++)
            {
                RectF r = notches[i];
                // 北边和西边在 CCW 走法里是反着走的（-X / -Z），区间要换算出沿行进方向的坐标，
                // 否则缺口会镜像到对面那条边上去。
                if (Mathf.Abs(r.Y) < 0.01f) list.Add(MakeNotch(0, r.X, r.XMax, r.H));
                else if (Mathf.Abs(r.XMax - w) < 0.01f) list.Add(MakeNotch(1, r.Y, r.YMax, r.W));
                else if (Mathf.Abs(r.YMax - h) < 0.01f) list.Add(MakeNotch(2, w - r.XMax, w - r.X, r.H));
                else list.Add(MakeNotch(3, h - r.YMax, h - r.Y, r.W));
            }
            list.Sort(delegate (Notch x, Notch y) { return x.Side != y.Side ? x.Side - y.Side : x.A.CompareTo(y.A); });

            Vector2[] corner = { new Vector2(0f, 0f), new Vector2(w, 0f), new Vector2(w, h), new Vector2(0f, h) };
            // 每条边的行进方向（CCW）
            Vector2[] dir = { new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(-1f, 0f), new Vector2(0f, -1f) };
            // 向内（指向内部）的法线
            Vector2[] inward = { new Vector2(0f, 1f), new Vector2(-1f, 0f), new Vector2(0f, -1f), new Vector2(1f, 0f) };
            float[] sideLen = { w, h, w, h };

            List<Vector2> pts = new List<Vector2>();
            for (int side = 0; side < 4; side++)
            {
                int c0 = side;              // 边的起点角
                int c1 = (side + 1) % 4;    // 边的终点角
                float s0 = chamfer[c0];
                float s1 = chamfer[c1];
                float start = s0;
                float end = sideLen[side] - s1;
                if (end - start < 2f) continue;

                AddPoint(pts, corner[c0] + dir[side] * start);

                // 这条边上的缺口
                for (int i = 0; i < list.Count; i++)
                {
                    Notch n = list[i];
                    if (n.Side != side) continue;
                    if (n.A < start + 0.01f || n.B > end - 0.01f) continue;
                    Vector2 pa = corner[c0] + dir[side] * n.A;
                    Vector2 pb = corner[c0] + dir[side] * n.B;
                    AddPoint(pts, pa);
                    AddPoint(pts, pa + inward[side] * n.Depth);
                    AddPoint(pts, pb + inward[side] * n.Depth);
                    AddPoint(pts, pb);
                }

                Vector2 atEnd = corner[c0] + dir[side] * end;
                AddPoint(pts, atEnd);
                if (s1 > 0.01f) AddPoint(pts, corner[c1] + dir[(side + 1) % 4] * s1);
            }
            return CleanOutline(pts);
        }

        /// <summary>
        /// Drop the duplicated closing point and merge collinear vertices. Ear clipping (and any
        /// 0.25m sampling downstream) breaks on both, so the ring is normalised once here.
        /// </summary>
        static Vector2[] CleanOutline(List<Vector2> pts)
        {
            List<Vector2> ring = new List<Vector2>(pts.Count);
            for (int i = 0; i < pts.Count; i++)
            {
                Vector2 p = pts[i];
                if (ring.Count > 0 && (ring[ring.Count - 1] - p).sqrMagnitude < 1e-6f) continue;
                ring.Add(p);
            }
            while (ring.Count > 1 && (ring[0] - ring[ring.Count - 1]).sqrMagnitude < 1e-6f)
                ring.RemoveAt(ring.Count - 1);

            bool merged = true;
            while (merged && ring.Count > 3)
            {
                merged = false;
                for (int i = 0; i < ring.Count; i++)
                {
                    Vector2 prev = ring[(i + ring.Count - 1) % ring.Count];
                    Vector2 cur = ring[i];
                    Vector2 next = ring[(i + 1) % ring.Count];
                    Vector2 d0 = cur - prev;
                    Vector2 d1 = next - cur;
                    float cross = d0.x * d1.y - d0.y * d1.x;
                    float len = d0.magnitude * d1.magnitude;
                    if (Mathf.Abs(cross) <= 1e-4f * Mathf.Max(1f, len) && Vector2.Dot(d0, d1) > 0f)
                    {
                        ring.RemoveAt(i);
                        merged = true;
                        break;
                    }
                }
            }
            return ring.ToArray();
        }

        static Notch MakeNotch(int side, float a, float b, float depth)
        {
            Notch n;
            n.Side = side; n.A = a; n.B = b; n.Depth = depth;
            return n;
        }

        static void AddPoint(List<Vector2> pts, Vector2 p)
        {
            if (pts.Count > 0 && (pts[pts.Count - 1] - p).sqrMagnitude < 0.0004f) return;
            pts.Add(p);
        }

        // ------------------------------------------------------------- clipping

        /// <summary>把矩形裁到外廓里，返回若干凸多边形（按面积从大到小）。</summary>
        public static List<Vector2[]> ClipRect(EnvelopeShape shape, RectF rect)
        {
            List<Vector2[]> outList = new List<Vector2[]>();
            if (shape == null) { outList.Add(RectPoly(rect)); return outList; }
            Vector2[] subject = RectPoly(rect);
            for (int i = 0; i < shape.Pieces.Count; i++)
            {
                Vector2[] clipped = ClipConvex(subject, shape.Pieces[i]);
                if (clipped == null || clipped.Length < 3) continue;
                if (Mathf.Abs(SignedArea(clipped)) < 0.5f) continue;
                outList.Add(clipped);
            }
            outList.Sort(delegate (Vector2[] a, Vector2[] b) { return Mathf.Abs(SignedArea(b)).CompareTo(Mathf.Abs(SignedArea(a))); });
            return outList;
        }

        public static Vector2[] RectPoly(RectF r)
        {
            return new[]
            {
                new Vector2(r.X, r.Y),
                new Vector2(r.XMax, r.Y),
                new Vector2(r.XMax, r.YMax),
                new Vector2(r.X, r.YMax),
            };
        }

        /// <summary>凸裁剪：clip 必须是 CCW 凸多边形（外廓的凸块都是）。</summary>
        static Vector2[] ClipConvex(Vector2[] subject, Vector2[] clip)
        {
            List<Vector2> output = new List<Vector2>(subject);
            for (int e = 0; e < clip.Length && output.Count > 0; e++)
            {
                Vector2 a = clip[e];
                Vector2 b = clip[(e + 1) % clip.Length];
                // 内部在边的左侧（CCW）
                List<Vector2> input = output;
                output = new List<Vector2>(input.Count + 2);
                for (int i = 0; i < input.Count; i++)
                {
                    Vector2 cur = input[i];
                    Vector2 prev = input[(i + input.Count - 1) % input.Count];
                    bool curIn = Side(a, b, cur) >= 0f;
                    bool prevIn = Side(a, b, prev) >= 0f;
                    if (curIn)
                    {
                        if (!prevIn) output.Add(Intersect(prev, cur, a, b));
                        output.Add(cur);
                    }
                    else if (prevIn)
                    {
                        output.Add(Intersect(prev, cur, a, b));
                    }
                }
            }
            return output.Count >= 3 ? output.ToArray() : null;
        }

        static float Side(Vector2 a, Vector2 b, Vector2 p)
        {
            return (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
        }

        static Vector2 Intersect(Vector2 p0, Vector2 p1, Vector2 a, Vector2 b)
        {
            Vector2 d0 = p1 - p0;
            Vector2 d1 = b - a;
            float den = d0.x * d1.y - d0.y * d1.x;
            if (Mathf.Abs(den) < 1e-9f) return p1;
            float t = ((a.x - p0.x) * d1.y - (a.y - p0.y) * d1.x) / den;
            return p0 + d0 * t;
        }

        /// <summary>半平面裁剪：保留 dot(p, n) &gt;= offset 的一侧（n 指向外廓内部）。</summary>
        static Vector2[] ClipHalfPlane(Vector2[] poly, Vector2 n, float offset)
        {
            List<Vector2> output = new List<Vector2>(poly.Length + 2);
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 cur = poly[i];
                Vector2 prev = poly[(i + poly.Length - 1) % poly.Length];
                float dc = Vector2.Dot(cur, n) - offset;
                float dp = Vector2.Dot(prev, n) - offset;
                bool curIn = dc >= 0f;
                bool prevIn = dp >= 0f;
                if (curIn)
                {
                    if (!prevIn) output.Add(LerpCut(prev, cur, dp, dc));
                    output.Add(cur);
                }
                else if (prevIn)
                {
                    output.Add(LerpCut(prev, cur, dp, dc));
                }
            }
            return output.Count >= 3 ? output.ToArray() : null;
        }

        static Vector2 LerpCut(Vector2 prev, Vector2 cur, float dp, float dc)
        {
            float t = dp / (dp - dc);
            return prev + (cur - prev) * Mathf.Clamp01(t);
        }

        /// <summary>矩形挖掉一个矩形，返回最多 4 块（都是矩形，便于后续裁剪）。</summary>
        static List<RectF> Subtract(List<RectF> input, RectF hole)
        {
            List<RectF> output = new List<RectF>();
            for (int i = 0; i < input.Count; i++)
            {
                RectF r = input[i];
                if (!r.Overlaps(hole)) { output.Add(r); continue; }
                float ox0 = Mathf.Max(r.X, hole.X);
                float ox1 = Mathf.Min(r.XMax, hole.XMax);
                float oy0 = Mathf.Max(r.Y, hole.Y);
                float oy1 = Mathf.Min(r.YMax, hole.YMax);
                if (r.X < ox0) output.Add(new RectF(r.X, r.Y, ox0 - r.X, r.H));
                if (r.XMax > ox1) output.Add(new RectF(ox1, r.Y, r.XMax - ox1, r.H));
                if (r.Y < oy0) output.Add(new RectF(ox0, r.Y, ox1 - ox0, oy0 - r.Y));
                if (r.YMax > oy1) output.Add(new RectF(ox0, oy1, ox1 - ox0, r.YMax - oy1));
            }
            return output;
        }

        // ----------------------------------------------------------- queries

        public static float SignedArea(IList<Vector2> p)
        {
            float a = 0f;
            for (int i = 0; i < p.Count; i++)
            {
                Vector2 c = p[i];
                Vector2 n = p[(i + 1) % p.Count];
                a += c.x * n.y - n.x * c.y;
            }
            return a * 0.5f;
        }

        public static bool Contains(Vector2[] poly, Vector2 p)
        {
            if (poly == null || poly.Length < 3) return false;
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                Vector2 a = poly[i];
                Vector2 b = poly[j];
                if (((a.y > p.y) != (b.y > p.y)) &&
                    (p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x))
                    inside = !inside;
            }
            return inside;
        }

        /// <summary>点到多边形边界的最短距离（点在内部时也返回正数）。</summary>
        public static float DistanceToEdge(Vector2[] poly, Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i];
                Vector2 b = poly[(i + 1) % poly.Length];
                Vector2 ab = b - a;
                float len2 = ab.sqrMagnitude;
                float t = len2 < 1e-8f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                float d = (p - (a + ab * t)).magnitude;
                if (d < best) best = d;
            }
            return best;
        }

        public static RectF BoundsOf(Vector2[] poly)
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            for (int i = 0; i < poly.Length; i++)
            {
                x0 = Mathf.Min(x0, poly[i].x); x1 = Mathf.Max(x1, poly[i].x);
                y0 = Mathf.Min(y0, poly[i].y); y1 = Mathf.Max(y1, poly[i].y);
            }
            return new RectF(x0, y0, x1 - x0, y1 - y0);
        }

        /// <summary>耳切三角化：返回三角形顶点下标（每 3 个一组）。外廓是简单多边形，没有洞。</summary>
        public static List<int> Triangulate(Vector2[] poly)
        {
            List<int> tris = new List<int>(Mathf.Max(0, poly.Length - 2) * 3);
            if (poly == null || poly.Length < 3) return tris;
            List<int> idx = new List<int>(poly.Length);
            for (int i = 0; i < poly.Length; i++) idx.Add(i);
            if (SignedArea(poly) < 0f) idx.Reverse();

            int guard = 0;
            while (idx.Count > 3 && guard++ < 4096)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int i0 = idx[(i + idx.Count - 1) % idx.Count];
                    int i1 = idx[i];
                    int i2 = idx[(i + 1) % idx.Count];
                    Vector2 a = poly[i0], b = poly[i1], c = poly[i2];
                    if (Side(a, b, c) <= 1e-6f) continue;   // 反折或共线，不是耳
                    bool hasInside = false;
                    for (int k = 0; k < idx.Count; k++)
                    {
                        int ik = idx[k];
                        if (ik == i0 || ik == i1 || ik == i2) continue;
                        if (PointInTriangle(poly[ik], a, b, c)) { hasInside = true; break; }
                    }
                    if (hasInside) continue;
                    tris.Add(i0); tris.Add(i1); tris.Add(i2);
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break;
            }
            if (idx.Count == 3) { tris.Add(idx[0]); tris.Add(idx[1]); tris.Add(idx[2]); }
            return tris;
        }

        static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Side(a, b, p);
            float d2 = Side(b, c, p);
            float d3 = Side(c, a, p);
            bool neg = (d1 < 0f) || (d2 < 0f) || (d3 < 0f);
            bool pos = (d1 > 0f) || (d2 > 0f) || (d3 > 0f);
            return !(neg && pos);
        }
    }
}
