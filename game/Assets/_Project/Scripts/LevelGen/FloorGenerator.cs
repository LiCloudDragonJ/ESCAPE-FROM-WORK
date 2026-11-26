using System;
using System.Collections.Generic;
using UnityEngine;

namespace EscapeFromWork.LevelGen
{
    /// <summary>
    /// 单层生成器 v2：随机 BSP 切分 → 切分线升级成走廊 → 房间沿叶子填充 → 相邻房间开门造环。
    /// 只吃 seed；禁止 UnityEngine.Random。
    /// </summary>
    public static class FloorGenerator
    {
        static readonly string[] NamesByPrototype =
        {
            "高管区", "会议室区", "开放办公", "行政办公", "改造区", "机房"
        };

        public static string NameOf(FloorPrototype p)
        {
            int i = (int)p;
            if (i < 0 || i >= NamesByPrototype.Length) return "办公区";
            return NamesByPrototype[i];
        }

        class Node
        {
            public RectF Rect;
            public Node A;
            public Node B;
            public RectF Corridor;
            public bool HasCorridor;
            /// <summary>与主廊并行的那条服务廊（带宽 ServiceCorridor），只在主廊旁存在。</summary>
            public RectF ServiceCorridor;
            public bool HasService;
            public int Depth;
            public bool IsLeaf { get { return A == null; } }
        }

        public static FloorPlan Generate(FloorSpec spec)
        {
            FloorPlan best = null;
            float bestScore = -1f;

            for (int attempt = 0; attempt < 6; attempt++)
            {
                System.Random rng = new System.Random(unchecked(spec.Seed * 6151 + attempt * 104729 + 17));
                FloorPlan plan = Build(spec, rng);
                float score = Score(plan);
                if (score > bestScore) { bestScore = score; best = plan; }
                if (bestScore >= 0.995f) break;
            }

            // 栅格连通修补只对最终选中的方案做一次（每轮 attempt 都做会把 1000 种子批测拖到十几分钟）
            EnsureGridConnectivity(best, spec);
            RoomDoorPolicy.Apply(best);
            AssignRoomTypes(best, spec);
            // 室内 45° 切角已按用户要求关闭（2026-11-26）：它只是在矩形房间里塞一块斜挡板，
            // 外壳和房间包围盒都还是矩形，视觉上没有真正的形状差异。形状问题改为
            // "外廓 + 少数结构动作传播" 的方向解决（见 tools/plan-proto 原型）。
            // AddCornerCuts(best, spec);
            EnsureExtractionSpread(best, spec);

            System.Random propRng = new System.Random(unchecked(spec.Seed * 31 + 7));
            CoverPlacer.Place(best, spec, propRng);
            CoverPlacer.EnforceClearLane(best, spec);
            CoverPlacer.DropPropsNearDoors(best);
            // 切角三角区在斜墙里面，桌子/隔断不能长在那里（走廊不会命中，房间角会）。
            best.Props.RemoveAll(p => RoomShape.BlockedInFloor(best, p.Rect.Center));

            return best;
        }

        /// <summary>
        /// R3 房间用途：真实面积 ≥56㎡ 一律 Normal；&lt;56㎡ 的才允许是服务间（储藏/卫生间/管井），
        /// 按 55/25/20 比例分配。保险库（Vault）留给 R4 的机制层，这里不生成。
        /// </summary>
        static void AssignRoomTypes(FloorPlan plan, FloorSpec spec)
        {
            System.Random rng = new System.Random(unchecked(spec.Seed * 9173 + 41));
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                Room room = plan.Rooms[i];
                if (RoomShape.AreaOf(room) >= 56f)
                {
                    room.Type = RoomType.Normal;
                }
                else
                {
                    double r = rng.NextDouble();
                    room.Type = r < 0.55 ? RoomType.Storage : (r < 0.80 ? RoomType.Toilet : RoomType.Riser);
                }
                plan.Rooms[i] = room;
            }
        }

        /// <summary>
        /// 把同一叶里被核心筒/楼梯切出来的 &lt;56㎡ 碎块，跟"边完全对齐、能拼成矩形"的邻块并成一块。
        /// 不并的话这些 30–50㎡ 的边角料会被当成独立小房间，把 &lt;56㎡ 占比顶过 10%。
        /// 只并小块的（并进邻块），4 轮足够：碎片本来就来自一次裁剪，最多再叠一次楼梯裁剪。
        /// </summary>
        static void MergeAlignedPieces(List<RectF> pieces, FloorPlan plan)
        {
            const float tiny = 56f;
            for (int pass = 0; pass < 4; pass++)
            {
                bool merged = false;
                for (int i = 0; i < pieces.Count && !merged; i++)
                {
                    if (pieces[i].Area >= tiny) continue;
                    for (int j = 0; j < pieces.Count && !merged; j++)
                    {
                        if (i == j) continue;
                        RectF union;
                        if (!AlignedUnion(pieces[i], pieces[j], out union)) continue;
                        // 并出来的块不能压住核心筒/楼梯，也不能压住别的碎块——否则房间会骑到
                        // 核心筒上或跟邻居重叠（seed 1 / seed 10 的两次回归都是这么来的）。
                        if (union.Overlaps(plan.Core)) continue;
                        bool blocked = false;
                        for (int k = 0; k < plan.Stairs.Count && !blocked; k++)
                            blocked = union.Overlaps(plan.Stairs[k]);
                        for (int k = 0; k < pieces.Count && !blocked; k++)
                        {
                            if (k == i || k == j) continue;
                            blocked = union.Overlaps(pieces[k]);
                        }
                        if (blocked) continue;
                        pieces[j] = union;
                        pieces.RemoveAt(i);
                        merged = true;
                    }
                }
                if (!merged) return;
            }
        }

        /// <summary>
        /// 两个矩形是否共享一整条对齐边（并集仍是矩形）；是则返回并集。
        /// 必须真的贴合（一条边的端点对上另一条边的端点），否则会把核心筒/楼梯两侧的两块并成一块，
        /// 房间就会骑在核心筒上——这正是 seed 1 "room inside stairs" 的成因。
        /// </summary>
        static bool AlignedUnion(RectF a, RectF b, out RectF union)
        {
            union = a;
            const float eps = 0.01f;
            if (Mathf.Abs(a.X - b.X) <= eps && Mathf.Abs(a.XMax - b.XMax) <= eps)
            {
                bool touch = Mathf.Abs(a.YMax - b.Y) <= eps || Mathf.Abs(b.YMax - a.Y) <= eps;
                if (!touch) return false;
                float y0 = Mathf.Min(a.Y, b.Y);
                float y1 = Mathf.Max(a.YMax, b.YMax);
                union = new RectF(a.X, y0, a.W, y1 - y0);
                return true;
            }
            if (Mathf.Abs(a.Y - b.Y) <= eps && Mathf.Abs(a.YMax - b.YMax) <= eps)
            {
                bool touch = Mathf.Abs(a.XMax - b.X) <= eps || Mathf.Abs(b.XMax - a.X) <= eps;
                if (!touch) return false;
                float x0 = Mathf.Min(a.X, b.X);
                float x1 = Mathf.Max(a.XMax, b.XMax);
                union = new RectF(x0, a.Y, x1 - x0, a.H);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 给房间切 45° 角：BSP 出来的房间全是轴对齐矩形，一层看下来像格子间。每个房间
        /// （短边 ≥6m 的）有六成概率切 1–2 个角，边长 1.4–3.2m；切角三角区之后由
        /// RoomShape 统一判定为"不可站"，并在 FloorGeometry 里补一道斜墙封口。
        /// 约束：切完边要够长、门（含套间门/环路门）附近不切，否则门会被切掉。
        /// </summary>
        static void AddCornerCuts(FloorPlan plan, FloorSpec spec)
        {
            System.Random rng = new System.Random(unchecked(spec.Seed * 4099 + 53));
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                Room room = plan.Rooms[i];
                RectF r = room.Rect;
                float shortest = Mathf.Min(r.W, r.H);
                if (shortest < 6f) continue;
                if (rng.NextDouble() > 0.62) continue;

                int count = rng.NextDouble() < 0.3 ? 2 : 1;
                List<CornerCut> cuts = new List<CornerCut>(count);
                for (int k = 0; k < count; k++)
                {
                    int corner = rng.Next(4);
                    if (HasCut(cuts, corner)) continue;
                    float size = RoomShape.MinCut + (float)rng.NextDouble() * (RoomShape.MaxCut - RoomShape.MinCut);
                    size = Mathf.Min(size, shortest * 0.4f);
                    if (size < RoomShape.MinCut) continue;
                    if (shortest < size * 2f + 1.6f) continue;
                    if (DoorNear(plan, room, RoomShape.CornerPoint(r, corner), size + 1.2f)) continue;
                    cuts.Add(new CornerCut(corner, size));
                }
                if (cuts.Count == 0) continue;
                room.Cuts = cuts;
                plan.Rooms[i] = room;
            }
        }

        static bool HasCut(List<CornerCut> cuts, int corner)
        {
            for (int i = 0; i < cuts.Count; i++) if (cuts[i].Corner == corner) return true;
            return false;
        }

        static bool DoorNear(FloorPlan plan, Room room, Vector2 corner, float radius)
        {
            float r2 = radius * radius;
            if (room.HasDoor && (room.Door.Center - corner).sqrMagnitude < r2) return true;
            if (room.ExtraDoors != null)
                for (int i = 0; i < room.ExtraDoors.Count; i++)
                    if ((room.ExtraDoors[i].Center - corner).sqrMagnitude < r2) return true;
            for (int i = 0; i < plan.LoopDoors.Count; i++)
                if ((plan.LoopDoors[i].Center - corner).sqrMagnitude < r2) return true;
            return false;
        }

        /// <summary>
        /// 从固定候选里挑本层撤离点：每个出生区至少 2 个候选满足"步行 ≥60% 层对角线"，
        /// 数量 4–6 个，优先少取而散布不够时再逐个加到 6。撤离点只可能是候选里的物理设施
        /// （消防梯/电梯/货梯/擦窗机/天台下索），不再往走廊里凭空补点。
        /// </summary>
        static void EnsureExtractionSpread(FloorPlan plan, FloorSpec spec)
        {
            plan.Extractions.Clear();
            int count = plan.ExtractionCandidates.Count;
            if (count == 0) return;

            int spawns = plan.SpawnZones.Count;
            FloorValidator.Grid grid = FloorValidator.BuildGrid(plan);
            float required = FloorValidator.RequiredExtractionDistance(plan);

            // far[s * count + e]：从出生区 s 走到候选 e 是否够远
            bool[] far = new bool[Mathf.Max(1, spawns) * count];
            float[] distToCandidate = new float[count];
            for (int s = 0; s < spawns; s++)
            {
                float[] dist = FloorValidator.Flood(grid, plan.SpawnZones[s].Center);
                float farthest = 0f;
                for (int e = 0; e < count; e++)
                {
                    float d = FloorValidator.WalkDistanceAt(grid, dist, plan.ExtractionCandidates[e].Center);
                    distToCandidate[e] = d;
                    if (d > farthest) farthest = d;
                }
                // 与 FloorValidator.AuditSpawns 用同一套阈值：超大层达不到 60% 对角线时按可达最远的 70% 算。
                float threshold = FloorValidator.FarThreshold(required, farthest);
                for (int e = 0; e < count; e++)
                    far[s * count + e] = distToCandidate[e] >= threshold;
            }

            System.Random pick = new System.Random(unchecked(spec.Seed * 7717 + 29));
            int target = Mathf.Clamp(4 + pick.Next(3), 4, count);

            int[] best = null;
            int bestSatisfied = -1;
            int bestSpread = -1;
            for (int k = target; k <= count; k++)
            {
                EvaluateCombinations(count, k, 0, 0, new int[k], far, spawns,
                    ref bestSatisfied, ref bestSpread, ref best);
                if (bestSatisfied >= spawns) break; // 这个数量已能保证散布，不用再多取
            }

            if (best == null) return;
            for (int i = 0; i < best.Length; i++) plan.Extractions.Add(plan.ExtractionCandidates[best[i]]);
        }

        /// <summary>按字典序枚举候选组合，取“满足散布的出生区最多、其次远端点总数最多”的那组。</summary>
        static void EvaluateCombinations(int count, int k, int start, int depth, int[] combo,
            bool[] far, int spawns, ref int bestSatisfied, ref int bestSpread, ref int[] best)
        {
            if (depth == k)
            {
                int satisfied = 0;
                int spread = 0;
                for (int s = 0; s < spawns; s++)
                {
                    int n = 0;
                    for (int i = 0; i < k; i++) if (far[s * count + combo[i]]) n++;
                    if (n >= 2) satisfied++;
                    spread += Mathf.Min(n, 3);
                }
                if (satisfied > bestSatisfied || (satisfied == bestSatisfied && spread > bestSpread))
                {
                    bestSatisfied = satisfied;
                    bestSpread = spread;
                    best = (int[])combo.Clone();
                }
                return;
            }

            for (int i = start; i <= count - (k - depth); i++)
            {
                combo[depth] = i;
                EvaluateCombinations(count, k, i + 1, depth + 1, combo, far, spawns,
                    ref bestSatisfied, ref bestSpread, ref best);
            }
        }

        static FloorPlan Build(FloorSpec spec, System.Random rng)
        {
            FloorPlan plan = new FloorPlan();
            plan.Spec = spec;
            plan.Bounds = new RectF(0f, 0f, spec.Width, spec.Height);

            plan.Core = new RectF(
                spec.Width * 0.5f - spec.CoreSize.x * 0.5f,
                spec.Height - spec.CoreNorthOffset - spec.CoreSize.y,
                spec.CoreSize.x,
                spec.CoreSize.y);

            float stairY = spec.Height * 0.5f - 10f;
            plan.Stairs.Add(new RectF(2f, stairY, 8f, 20f));
            plan.Stairs.Add(new RectF(spec.Width - 10f, stairY, 8f, 20f));

            // 外廓先定：房间、墙、地板、导航栅格都要贴着它裁，不能先铺满矩形再补角。
            List<RectF> envelopeKeepOut = new List<RectF>();
            envelopeKeepOut.Add(plan.Core);
            for (int i = 0; i < plan.Stairs.Count; i++) envelopeKeepOut.Add(plan.Stairs[i]);
            plan.Envelope = FloorEnvelope.Build(spec.Width, spec.Height, rng, envelopeKeepOut);

            AddZones(plan, spec);
            AddColumns(plan, spec);

            Node root = new Node();
            root.Rect = plan.Bounds;
            root.Depth = 0;
            SplitNode(root, spec, rng);

            Emit(root, plan, spec, rng);

            // R2 建筑级断点：长走廊切段 + 门斗 + 并行服务廊。这一步只动走廊矩形与开口登记，
            // 必须排在门分配之前（房间门要按切段后的走廊重新找），也必须排在外墙/内墙生成之前
            // 才能让门洞真的被挖出来。
            CorridorBreaks.Apply(plan, spec);

            AssignCorridorDoors(plan);
            AddLoopDoors(plan, spec, rng);
            AddDeadEndBypass(plan, spec, rng);
            EnsureConnectivity(plan);
            AddExtractions(plan, spec, rng);
            AddSpawnZones(plan, spec);

            return plan;
        }

        static void SplitNode(Node n, FloorSpec spec, System.Random rng)
        {
            RectF r = n.Rect;
            if (n.Depth >= 9) return;
            if (r.W < 7f || r.H < 7f) return;

            float area = r.Area;
            if (area < spec.MinRoomArea) return;

            // R3 分档目标 56–113 / 113–200 / 200–400 / 400+ ≈ 40/35/20/5。
            // 试过把 220–400 档从 .62 抬到 .68、120–220 从 .85 抬到 .88：中位会飘到 134、
            // 而且 3m 级的边角料反而变多（seed 8 的 <56㎡ 从 8.6% 涨到 12.1%），所以保留原表。
            float stopP = area > 900f ? 0.0f : area > 620f ? 0.06f : area > 400f ? 0.30f : area > 220f ? 0.62f : area > 120f ? 0.85f : 0.95f;
            stopP *= PrototypeLeafBias(spec.Prototype);
            // 硬上限：再大的房间也不许留（否则会切出篮球场）。
            // 长边也卡在 30m：R2 要求最长直线视线 ≤45m，而"走廊 + 一整间 42m 的大厅"光这两段就
            // 已经 49m 了（实测 seed 5）。30m 的房间 + 最长的一段走廊 31.8m 也还在 45m 以外有余量。
            if (area > 900f || r.W > 30f || r.H > 30f) stopP = 0f;
            if (rng.NextDouble() < stopP) return;

            bool verticalCut;
            if (r.W > r.H * 1.2f) verticalCut = true;
            else if (r.H > r.W * 1.2f) verticalCut = false;
            else verticalCut = rng.NextDouble() < 0.5f;

            float len = verticalCut ? r.W : r.H;
            if (len < 8f)
            {
                verticalCut = !verticalCut;
                len = verticalCut ? r.W : r.H;
                if (len < 8f) return;
            }

            // 走廊宽度在切的时候就留出来：A 到切线为止，B 从切线 + 走廊总宽开始。
            // 否则以切线为中心的走廊带会压进两侧子区域，房间和走廊必然重叠。
            // 主廊（4.8m）旁边再并一条 2.3m 服务廊：两条共用同一条墙线，不会夹出一条 0.4m 的
            // 空腔（那正是"从缝里看到虚空"的老问题）。
            float band = CorridorWidth(r, spec);
            if (band < 0.01f) band = 0f;
            bool service = band >= spec.MainCorridor - 0.01f && spec.ServiceCorridor >= 1.2f;
            float serviceBand = service ? spec.ServiceCorridor : 0f;
            float total = band + serviceBand;

            float u = 0.27f + (float)rng.NextDouble() * 0.46f;
            float cut = (verticalCut ? r.X : r.Y) + len * u;

            float aLen = cut - (verticalCut ? r.X : r.Y);
            float bLen = (verticalCut ? r.XMax : r.YMax) - cut - total;
            if (aLen < 3.6f || bLen < 3.6f)
            {
                // 让开走廊后不够宽，退回按一半对半切一次
                cut = (verticalCut ? r.X : r.Y) + len * 0.5f;
                aLen = cut - (verticalCut ? r.X : r.Y);
                bLen = (verticalCut ? r.XMax : r.YMax) - cut - total;
                if (aLen < 3.6f || bLen < 3.6f) return;
            }

            // 小到放不下一个房间的子区域不要切出来，否则那份面积变成谁也进不去的黑洞
            float cross = verticalCut ? r.H : r.W;
            if (aLen * cross < spec.MinRoomArea * 1.1f || bLen * cross < spec.MinRoomArea * 1.1f) return;

            n.A = new Node();
            n.B = new Node();
            n.A.Depth = n.Depth + 1;
            n.B.Depth = n.Depth + 1;

            if (verticalCut)
            {
                n.A.Rect = new RectF(r.X, r.Y, aLen, r.H);
                n.B.Rect = new RectF(cut + total, r.Y, bLen, r.H);
                n.Corridor = new RectF(cut, r.Y, band, r.H);
                if (service) n.ServiceCorridor = new RectF(cut + band, r.Y, serviceBand, r.H);
            }
            else
            {
                n.A.Rect = new RectF(r.X, r.Y, r.W, aLen);
                n.B.Rect = new RectF(r.X, cut + total, r.W, bLen);
                n.Corridor = new RectF(r.X, cut, r.W, band);
                if (service) n.ServiceCorridor = new RectF(r.X, cut + band, r.W, serviceBand);
            }
            n.HasCorridor = band > 0f;
            n.HasService = service;

            SplitNode(n.A, spec, rng);
            SplitNode(n.B, spec, rng);
        }

        static float PrototypeLeafBias(FloorPrototype p)
        {
            switch (p)
            {
                case FloorPrototype.OpenOffice: return 0.55f;
                case FloorPrototype.Executive: return 0.6f;
                case FloorPrototype.Renovation: return 0.5f;
                case FloorPrototype.Meeting: return 1.45f;
                case FloorPrototype.SmallOffice: return 1.6f;
                default: return 1.1f;
            }
        }

        /// <summary>走廊按“被切开的父区域有多大”决定，而不是按深度——大分区的切线成主走廊，小隔断只是墙。</summary>
        static float CorridorWidth(RectF parent, FloorSpec spec)
        {
            float area = parent.Area;
            if (area >= 1400f) return spec.MainCorridor;
            if (area >= 640f) return spec.SecondaryCorridor;
            if (area >= Mathf.Max(320f, spec.TertiaryMinArea) && spec.CorridorLevels >= 3) return spec.TertiaryCorridor;
            return 0f;
        }

        static void Emit(Node n, FloorPlan plan, FloorSpec spec, System.Random rng)
        {
            if (!n.IsLeaf)
            {
                if (n.HasCorridor)
                {
                    EmitCorridorBand(plan, spec, n.Corridor, false);
                    if (n.HasService) EmitCorridorBand(plan, spec, n.ServiceCorridor, true);
                }

                Emit(n.A, plan, spec, rng);
                Emit(n.B, plan, spec, rng);
                return;
            }

            RectF leaf = n.Rect;
            if (leaf.W < 4f || leaf.H < 4f) return;

            List<RectF> pieces = new List<RectF>();
            pieces.Add(leaf);

            pieces = Clip(pieces, plan.Core);
            for (int i = 0; i < plan.Stairs.Count; i++) pieces = Clip(pieces, plan.Stairs[i]);

            MergeAlignedPieces(pieces, plan);

            for (int i = 0; i < pieces.Count; i++)
            {
                RectF cell = new RectF(
                    pieces[i].X + spec.WallInset,
                    pieces[i].Y + spec.WallInset,
                    pieces[i].W - spec.WallInset * 2f,
                    pieces[i].H - spec.WallInset * 2f);

                if (cell.W < 2.6f || cell.H < 2.6f) continue;
                if (cell.Area > 18f)
                {
                    // 被核心筒/楼梯切出来的长条（>32m 长、<8m 宽）不是房间，是绕行廊：
                    // 当房间会在一条 4.6m 宽的条带里留下 40m+ 通视（实测 seed 3 的 R4 = 43.7×4.6）。
                    // 交给走廊管线，CorridorBreaks 会按 28m 站距切段并开门。
                    if (Mathf.Max(cell.W, cell.H) > 32f && Mathf.Min(cell.W, cell.H) < 8f)
                        AddCorridor(plan, cell, false, false);
                    else
                        AddRoomClipped(plan, cell);
                }
            }
        }

        /// <summary>
        /// 一条走廊带穿过核心筒/楼梯时不能整段丢掉（丢掉会把整层切成互不相连的两半），
        /// 而是绕开障碍切成几段继续铺。
        /// </summary>
        static void EmitCorridorBand(FloorPlan plan, FloorSpec spec, RectF line, bool service)
        {
            List<RectF> segs = new List<RectF>();
            segs.Add(line);
            segs = Clip(segs, plan.Core);
            for (int i = 0; i < plan.Stairs.Count; i++) segs = Clip(segs, plan.Stairs[i]);
            for (int i = 0; i < segs.Count; i++)
            {
                RectF seg = segs[i];
                if (seg.W < 0.8f || seg.H < 0.8f) continue;
                bool main = !service && Mathf.Min(seg.W, seg.H) >= spec.MainCorridor - 0.01f;
                AddCorridor(plan, seg, main, service);
            }
        }

        /// <summary>把矩形挖掉一个障碍，返回剩下的最多四块。</summary>
        static List<RectF> Clip(List<RectF> input, RectF obstacle)
        {
            List<RectF> output = new List<RectF>();
            for (int i = 0; i < input.Count; i++)
            {
                RectF r = input[i];
                if (!r.Overlaps(obstacle)) { output.Add(r); continue; }

                float ox0 = Mathf.Max(r.X, obstacle.X);
                float ox1 = Mathf.Min(r.XMax, obstacle.XMax);
                float oy0 = Mathf.Max(r.Y, obstacle.Y);
                float oy1 = Mathf.Min(r.YMax, obstacle.YMax);

                if (r.X < ox0) output.Add(new RectF(r.X, r.Y, ox0 - r.X, r.H));
                if (r.XMax > ox1) output.Add(new RectF(ox1, r.Y, r.XMax - ox1, r.H));
                if (r.Y < oy0) output.Add(new RectF(ox0, r.Y, ox1 - ox0, oy0 - r.Y));
                if (r.YMax > oy1) output.Add(new RectF(ox0, oy1, ox1 - ox0, r.YMax - oy1));
            }
            return output;
        }

        /// <summary>
        /// 收房间前先裁到外廓：贴着外廓那一圈的房间会被切成梯形/五边形/L 形，
        /// 而不是先铺满矩形房间、再给边角补个角。Rect 只留作包围盒。
        /// </summary>
        static void AddRoomClipped(FloorPlan plan, RectF cell)
        {
            if (plan.Envelope == null) { AddRoom(plan, cell, null); return; }
            List<Vector2[]> parts = FloorEnvelope.ClipRect(plan.Envelope, cell);
            if (parts.Count == 0) return;
            // 缺口把一个矩形劈成两块时，只留最大的一块；剩下那块在后面封成实心。
            Vector2[] poly = parts[0];
            float area = Mathf.Abs(FloorEnvelope.SignedArea(poly));
            if (area < 18f) return;
            AddRoom(plan, cell, poly);
        }

        static void AddRoom(FloorPlan plan, RectF cell) { AddRoom(plan, cell, null); }

        static void AddRoom(FloorPlan plan, RectF cell, Vector2[] outline)
        {
            Room room;
            room.Rect = cell;
            room.ZoneIndex = ZoneIndexAt(plan, cell.Center);
            room.Type = RoomType.Normal;
            room.Door = default(Door);
            room.Door.Corridor = -1;
            room.Door.OtherRoom = -1;
            room.HasDoor = false;
            room.CorridorIndex = -1;
            room.DoorToCorridor = false;
            room.ExtraDoors = new List<Door>();
            room.Cuts = null;
            room.Outline = outline;
            plan.Rooms.Add(room);
        }

        /// <summary>给每个房间找最宽贴边的那条走廊开门。</summary>
        static void AssignCorridorDoors(FloorPlan plan)
        {
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                Room room = plan.Rooms[i];
                int bestCorridor = -1;
                Vector2 bestCenter = Vector2.zero;
                DoorSide bestSide = DoorSide.South;
                float bestOverlap = 0f;

                for (int c = 0; c < plan.Corridors.Count; c++)
                {
                    Vector2 center;
                    DoorSide side;
                    float len;
                    if (!FindDoorBetween(room.Rect, plan.Corridors[c].Rect, out center, out side, out len)) continue;
                    if (len > bestOverlap)
                    {
                        bestOverlap = len;
                        bestCorridor = c;
                        bestCenter = center;
                        bestSide = side;
                    }
                }

                if (bestCorridor >= 0)
                {
                    room.Door.Center = bestCenter;
                    room.Door.Side = bestSide;
                    room.Door.Width = Mathf.Clamp(bestOverlap * 0.5f, 0.9f, 1.9f);
                    room.Door.Corridor = bestCorridor;
                    room.Door.OtherRoom = -1;
                    room.HasDoor = true;
                    room.CorridorIndex = bestCorridor;
                    room.DoorToCorridor = true;

                    // 大房间再开一道门通向另一条走廊：打通绕行，也避免一个门被堵就没路
                    // 注意：这里沿用包围盒面积（R1 的门数规则就是按 Rect 定的），别改成轮廓面积，
                    // 否则贴外廓的大房间会少一扇门（seed 1 room 26）。
                    if (room.Rect.Area >= 80f)
                    {
                        int second = -1;
                        Vector2 c2 = Vector2.zero;
                        DoorSide s2 = DoorSide.South;
                        float len2 = 0f;
                        for (int c = 0; c < plan.Corridors.Count; c++)
                        {
                            if (c == bestCorridor) continue;
                            Vector2 center;
                            DoorSide side;
                            float len;
                            if (!FindDoorBetween(room.Rect, plan.Corridors[c].Rect, out center, out side, out len)) continue;
                            if (len >= 1.5f && len > len2) { len2 = len; second = c; c2 = center; s2 = side; }
                        }

                        if (second >= 0)
                        {
                            Door extra;
                            extra.Center = c2;
                            extra.Side = s2;
                            extra.Width = Mathf.Clamp(len2 * 0.45f, 0.9f, 1.8f);
                            extra.Corridor = second;
                            extra.OtherRoom = -1;
                            room.ExtraDoors.Add(extra);
                        }
                    }

                    plan.Rooms[i] = room;
                }
            }
        }

        /// <summary>没走廊门的房间，靠相邻房间的套间门接进图里；再随机加环路边。</summary>
        static void AddLoopDoors(FloorPlan plan, FloorSpec spec, System.Random rng)
        {
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                Room room = plan.Rooms[i];
                if (room.HasDoor) continue;

                int best = -1;
                Vector2 bestCenter = Vector2.zero;
                DoorSide bestSide = DoorSide.South;
                float bestOverlap = 0f;

                for (int j = 0; j < plan.Rooms.Count; j++)
                {
                    if (j == i) continue;
                    Vector2 center;
                    DoorSide side;
                    float len;
                    if (!FindDoorBetween(room.Rect, plan.Rooms[j].Rect, out center, out side, out len)) continue;
                    if (len > bestOverlap) { bestOverlap = len; best = j; bestCenter = center; bestSide = side; }
                }

                if (best >= 0 && bestOverlap >= 1.0f)
                {
                    room.Door.Center = bestCenter;
                    room.Door.Side = bestSide;
                    room.Door.Width = Mathf.Clamp(bestOverlap * 0.45f, 0.9f, 1.6f);
                    room.Door.Corridor = -1;
                    room.Door.OtherRoom = best;
                    room.HasDoor = true;
                    room.CorridorIndex = -1;
                    room.DoorToCorridor = false;
                    plan.Rooms[i] = room;

                    Door d;
                    d.Center = bestCenter;
                    d.Side = bestSide;
                    d.Width = room.Door.Width;
                    d.Corridor = -1;
                    d.OtherRoom = best;
                    plan.LoopDoors.Add(d);
                }
            }

            int budget = Mathf.RoundToInt(plan.Rooms.Count * spec.LoopDoorChance);
            int added = 0;
            for (int i = 0; i < plan.Rooms.Count && added < budget; i++)
            {
                if (rng.NextDouble() > spec.LoopDoorChance) continue;
                for (int j = 0; j < plan.Rooms.Count; j++)
                {
                    if (j == i) continue;
                    Vector2 center;
                    DoorSide side;
                    float len;
                    if (!FindDoorBetween(plan.Rooms[i].Rect, plan.Rooms[j].Rect, out center, out side, out len)) continue;
                    if (len < 2.0f) continue;
                    Door d;
                    d.Center = center;
                    d.Side = side;
                    d.Width = Mathf.Clamp(len * 0.4f, 1.0f, 1.8f);
                    d.Corridor = -1;
                    d.OtherRoom = j;
                    Room src = plan.Rooms[i];
                    if (src.ExtraDoors == null) src.ExtraDoors = new List<Door>();
                    src.ExtraDoors.Add(d);
                    plan.Rooms[i] = src;
                    plan.LoopDoors.Add(d);
                    added++;
                    break;
                }
            }
        }

        /// <summary>
        /// 死胡同消除：只有一道门的房间再补一道，优先串到相邻房间（套间门），其次通向走廊。
        /// 迷宫感主要来自大量单出入口的小房间；补门之后一条单线变成一条环路，追击和绕行都能成立。
        /// </summary>
        static void AddDeadEndBypass(FloorPlan plan, FloorSpec spec, System.Random rng)
        {
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                Room room = plan.Rooms[i];
                if (DoorCount(room) >= 2) continue;
                if (room.Rect.Area < 8f) continue;

                // 1) 通向相邻房间
                int bestRoom = -1;
                Vector2 roomCenter = Vector2.zero;
                DoorSide roomSide = DoorSide.South;
                float roomLen = 0f;
                for (int j = 0; j < plan.Rooms.Count; j++)
                {
                    if (j == i) continue;
                    Vector2 c;
                    DoorSide s;
                    float len;
                    if (!FindDoorBetween(room.Rect, plan.Rooms[j].Rect, out c, out s, out len)) continue;
                    if (len < 1.2f || len <= roomLen) continue;
                    if (DoorNear(room, c, 1.4f)) continue;
                    roomLen = len;
                    bestRoom = j;
                    roomCenter = c;
                    roomSide = s;
                }

                if (bestRoom >= 0)
                {
                    Door d;
                    d.Center = roomCenter;
                    d.Side = roomSide;
                    d.Width = Mathf.Clamp(roomLen * 0.45f, 0.9f, 1.6f);
                    d.Corridor = -1;
                    d.OtherRoom = bestRoom;
                    room.ExtraDoors.Add(d);
                    plan.LoopDoors.Add(d);
                    plan.Rooms[i] = room;
                    continue;
                }

                // 2) 通向走廊
                int bestCorridor = -1;
                Vector2 corrCenter = Vector2.zero;
                DoorSide corrSide = DoorSide.South;
                float corrLen = 0f;
                for (int c = 0; c < plan.Corridors.Count; c++)
                {
                    Vector2 center;
                    DoorSide side;
                    float len;
                    if (!FindDoorBetween(room.Rect, plan.Corridors[c].Rect, out center, out side, out len)) continue;
                    if (len < 1.2f || len <= corrLen) continue;
                    if (DoorNear(room, center, 1.4f)) continue;
                    corrLen = len;
                    bestCorridor = c;
                    corrCenter = center;
                    corrSide = side;
                }

                if (bestCorridor < 0) continue;
                Door extra;
                extra.Center = corrCenter;
                extra.Side = corrSide;
                extra.Width = Mathf.Clamp(corrLen * 0.45f, 0.9f, 1.6f);
                extra.Corridor = bestCorridor;
                extra.OtherRoom = -1;
                room.ExtraDoors.Add(extra);
                plan.Rooms[i] = room;
            }
        }

        static int DoorCount(Room room)
        {
            int count = room.HasDoor ? 1 : 0;
            if (room.ExtraDoors != null) count += room.ExtraDoors.Count;
            return count;
        }

        static bool DoorNear(Room room, Vector2 point, float radius)
        {
            float sq = radius * radius;
            if (room.HasDoor && (room.Door.Center - point).sqrMagnitude < sq) return true;
            if (room.ExtraDoors == null) return false;
            for (int i = 0; i < room.ExtraDoors.Count; i++)
                if ((room.ExtraDoors[i].Center - point).sqrMagnitude < sq) return true;
            return false;
        }

        /// <summary>从走廊出发做图遍历，把没接上的房间连到最近的可达房间，连不上就丢掉。</summary>
        static void EnsureConnectivity(FloorPlan plan)
        {
            int roomCount = plan.Rooms.Count;
            if (roomCount == 0) return;

            List<int>[] adj = BuildGraph(plan);

            bool[] reachable = new bool[roomCount + plan.Corridors.Count];
            Queue<int> queue = new Queue<int>();
            for (int c = 0; c < plan.Corridors.Count; c++)
            {
                int node = roomCount + c;
                reachable[node] = true;
                queue.Enqueue(node);
            }
            Bfs(adj, reachable, queue);

            if (plan.Corridors.Count == 0)
            {
                for (int i = 0; i < roomCount; i++) { reachable[i] = true; }
            }

            for (int pass = 0; pass < 4; pass++)
            {
                bool changed = false;
                for (int i = 0; i < plan.Rooms.Count; i++)
                {
                    if (reachable[i]) continue;

                    // 先试接可达走廊：走廊自身是孤岛时，房间周围可能没有可达房间
                    int bestCorr = -1;
                    Vector2 cc = Vector2.zero;
                    DoorSide cs = DoorSide.South;
                    float cl = 0f;
                    for (int c = 0; c < plan.Corridors.Count; c++)
                    {
                        if (!reachable[roomCount + c]) continue;
                        Vector2 c2;
                        DoorSide s2;
                        float len;
                        if (!FindDoorBetween(plan.Rooms[i].Rect, plan.Corridors[c].Rect, out c2, out s2, out len)) continue;
                        if (len >= 1.0f && len > cl) { cl = len; bestCorr = c; cc = c2; cs = s2; }
                    }

                    if (bestCorr >= 0)
                    {
                        Room rr = plan.Rooms[i];
                        Door nd;
                        nd.Center = cc;
                        nd.Side = cs;
                        nd.Width = Mathf.Clamp(cl * 0.45f, 0.9f, 1.8f);
                        nd.Corridor = bestCorr;
                        nd.OtherRoom = -1;

                        if (!rr.HasDoor)
                        {
                            rr.Door = nd;
                            rr.HasDoor = true;
                            rr.DoorToCorridor = true;
                            rr.CorridorIndex = bestCorr;
                        }
                        else
                        {
                            if (rr.ExtraDoors == null) rr.ExtraDoors = new List<Door>();
                            rr.ExtraDoors.Add(nd);
                        }

                        plan.Rooms[i] = rr;
                        reachable[i] = true;
                        changed = true;
                        continue;
                    }

                    int best = -1;
                    Vector2 center = Vector2.zero;
                    DoorSide side = DoorSide.South;
                    float bestLen = 0f;
                    for (int j = 0; j < plan.Rooms.Count; j++)
                    {
                        if (j == i || !reachable[j]) continue;
                        Vector2 c2;
                        DoorSide s2;
                        float len;
                        if (!FindDoorBetween(plan.Rooms[i].Rect, plan.Rooms[j].Rect, out c2, out s2, out len)) continue;
                        if (len > bestLen) { bestLen = len; best = j; center = c2; side = s2; }
                    }

                    if (best >= 0 && bestLen >= 1.0f)
                    {
                        Room room = plan.Rooms[i];
                        if (!room.HasDoor)
                        {
                            room.Door.Center = center;
                            room.Door.Side = side;
                            room.Door.Width = Mathf.Clamp(bestLen * 0.45f, 0.9f, 1.6f);
                            room.Door.Corridor = -1;
                            room.Door.OtherRoom = best;
                            room.HasDoor = true;
                            room.DoorToCorridor = false;
                            room.CorridorIndex = -1;
                            plan.Rooms[i] = room;
                        }
                        Door d;
                        d.Center = center;
                        d.Side = side;
                        d.Width = room.Door.Width;
                        d.Corridor = -1;
                        d.OtherRoom = best;
                        Room bridged = plan.Rooms[i];
                        if (bridged.ExtraDoors == null) bridged.ExtraDoors = new List<Door>();
                        bridged.ExtraDoors.Add(d);
                        plan.Rooms[i] = bridged;
                        plan.LoopDoors.Add(d);
                        reachable[i] = true;
                        changed = true;
                    }
                }

                if (!changed) break;
                adj = BuildGraph(plan);
                reachable = new bool[roomCount + plan.Corridors.Count];
                Queue<int> q2 = new Queue<int>();
                for (int c = 0; c < plan.Corridors.Count; c++)
                {
                    int node = roomCount + c;
                    reachable[node] = true;
                    q2.Enqueue(node);
                }
                Bfs(adj, reachable, q2);
                if (plan.Corridors.Count == 0) for (int i = 0; i < roomCount; i++) reachable[i] = true;
            }

            // 丢弃彻底接不上的房间
            for (int i = plan.Rooms.Count - 1; i >= 0; i--)
            {
                if (i < reachable.Length && !reachable[i]) plan.Rooms.RemoveAt(i);
            }
        }

        /// <summary>图连通不等于几何连通：用 1m 栅格泛洪复核，把走不到的房间就地补一道门。</summary>
        static void EnsureGridConnectivity(FloorPlan plan, FloorSpec spec)
        {
            const float cell = 1f;
            int w = Mathf.CeilToInt(spec.Width / cell);
            int h = Mathf.CeilToInt(spec.Height / cell);
            if (w <= 0 || h <= 0) return;

            Vector2 spawn = plan.SpawnZones.Count > 0
                ? plan.SpawnZones[0].Center
                : new Vector2(spec.Width * 0.5f, spec.Height * 0.5f);

            for (int iter = 0; iter < 6; iter++)
            {
                bool[] walk = BuildWalkGrid(plan, w, h, cell);
                bool[] reach = FloodGrid(walk, w, h, spawn, cell);
                int total = 0, got = 0;
                for (int i = 0; i < walk.Length; i++) { if (walk[i]) total++; if (reach[i]) got++; }
                if (total == 0 || got >= total * 0.995f) return;

                bool changed = false;
                for (int i = 0; i < plan.Rooms.Count; i++)
                {
                    if (RectReached(plan.Rooms[i].Rect, reach, w, h, cell)) continue;

                    int bestKind = -1;
                    int bestIdx = -1;
                    Vector2 bc = Vector2.zero;
                    DoorSide bs = DoorSide.South;
                    float bl = 0f;

                    for (int j = 0; j < plan.Rooms.Count; j++)
                    {
                        if (j == i || !RectReached(plan.Rooms[j].Rect, reach, w, h, cell)) continue;
                        Vector2 c;
                        DoorSide s;
                        float len;
                        if (!FindDoorBetween(plan.Rooms[i].Rect, plan.Rooms[j].Rect, out c, out s, out len)) continue;
                        if (len > bl) { bl = len; bestKind = 0; bestIdx = j; bc = c; bs = s; }
                    }

                    for (int c2 = 0; c2 < plan.Corridors.Count; c2++)
                    {
                        if (!RectReached(plan.Corridors[c2].Rect, reach, w, h, cell)) continue;
                        Vector2 c;
                        DoorSide s;
                        float len;
                        if (!FindDoorBetween(plan.Rooms[i].Rect, plan.Corridors[c2].Rect, out c, out s, out len)) continue;
                        if (len > bl + 0.2f) { bl = len; bestKind = 1; bestIdx = c2; bc = c; bs = s; }
                    }

                    if (bestKind < 0) continue;

                    Room room = plan.Rooms[i];
                    Door nd;
                    nd.Center = bc;
                    nd.Side = bs;
                    nd.Width = Mathf.Clamp(bl * 0.45f, 0.9f, 1.8f);
                    nd.Corridor = bestKind == 1 ? bestIdx : -1;
                    nd.OtherRoom = bestKind == 0 ? bestIdx : -1;

                    if (!room.HasDoor)
                    {
                        room.Door = nd;
                        room.HasDoor = true;
                        room.DoorToCorridor = bestKind == 1;
                        room.CorridorIndex = bestKind == 1 ? bestIdx : -1;
                    }
                    else
                    {
                        if (room.ExtraDoors == null) room.ExtraDoors = new List<Door>();
                        room.ExtraDoors.Add(nd);
                    }

                    plan.Rooms[i] = room;
                    if (bestKind == 0) plan.LoopDoors.Add(nd);
                    changed = true;
                }

                if (!changed) return;
            }
        }

        static bool[] BuildWalkGrid(FloorPlan plan, int w, int h, float cell)
        {
            bool[] walk = new bool[w * h];
            for (int z = 0; z < plan.Corridors.Count; z++) MarkGrid(walk, w, h, cell, plan.Corridors[z].Rect.Inflate(0.05f));
            for (int r = 0; r < plan.Rooms.Count; r++) MarkGrid(walk, w, h, cell, plan.Rooms[r].Rect.Inflate(0.05f));
            for (int s = 0; s < plan.Stairs.Count; s++) MarkGrid(walk, w, h, cell, plan.Stairs[s].Inflate(0.05f));
            MarkGrid(walk, w, h, cell, plan.Core.Inflate(0.05f));

            for (int r = 0; r < plan.Rooms.Count; r++)
            {
                Room room = plan.Rooms[r];
                if (room.HasDoor)
                {
                    Vector2 c = room.Door.Center;
                    MarkGrid(walk, w, h, cell, new RectF(c.x - 0.7f, c.y - 0.7f, 1.4f, 1.4f));
                }
                if (room.ExtraDoors == null) continue;
                for (int d = 0; d < room.ExtraDoors.Count; d++)
                {
                    Vector2 c = room.ExtraDoors[d].Center;
                    MarkGrid(walk, w, h, cell, new RectF(c.x - 0.6f, c.y - 0.6f, 1.2f, 1.2f));
                }
            }

            for (int d = 0; d < plan.LoopDoors.Count; d++)
            {
                Vector2 c = plan.LoopDoors[d].Center;
                MarkGrid(walk, w, h, cell, new RectF(c.x - 0.6f, c.y - 0.6f, 1.2f, 1.2f));
            }

            return walk;
        }

        static void MarkGrid(bool[] walk, int w, int h, float cell, RectF r)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(r.X / cell));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(r.Y / cell));
            int x1 = Mathf.Min(w - 1, Mathf.FloorToInt(r.XMax / cell));
            int y1 = Mathf.Min(h - 1, Mathf.FloorToInt(r.YMax / cell));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++) walk[y * w + x] = true;
        }

        static bool[] FloodGrid(bool[] walk, int w, int h, Vector2 start, float cell)
        {
            bool[] seen = new bool[walk.Length];
            int sx = Mathf.Clamp(Mathf.FloorToInt(start.x / cell), 0, w - 1);
            int sy = Mathf.Clamp(Mathf.FloorToInt(start.y / cell), 0, h - 1);
            int s = sy * w + sx;
            if (!walk[s])
            {
                // 起点落在空处时向最近的可走格靠拢，避免整层被判成不可达
                for (int step = 1; step <= 12 && !walk[s]; step++)
                {
                    bool found = false;
                    for (int dy = -step; dy <= step && !found; dy++)
                        for (int dx = -step; dx <= step && !found; dx++)
                        {
                            int nx = sx + dx, ny = sy + dy;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                            int ni = ny * w + nx;
                            if (!walk[ni]) continue;
                            s = ni;
                            found = true;
                        }
                }
            }
            if (!walk[s]) return seen;

            Stack<int> stack = new Stack<int>();
            seen[s] = true;
            stack.Push(s);
            while (stack.Count > 0)
            {
                int idx = stack.Pop();
                int x = idx % w;
                int y = idx / w;
                GridPush(walk, seen, stack, w, h, x + 1, y);
                GridPush(walk, seen, stack, w, h, x - 1, y);
                GridPush(walk, seen, stack, w, h, x, y + 1);
                GridPush(walk, seen, stack, w, h, x, y - 1);
            }
            return seen;
        }

        static void GridPush(bool[] walk, bool[] seen, Stack<int> stack, int w, int h, int x, int y)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            int idx = y * w + x;
            if (!walk[idx] || seen[idx]) return;
            seen[idx] = true;
            stack.Push(idx);
        }

        static bool RectReached(RectF r, bool[] reach, int w, int h, float cell)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(r.X / cell));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(r.Y / cell));
            int x1 = Mathf.Min(w - 1, Mathf.FloorToInt(r.XMax / cell));
            int y1 = Mathf.Min(h - 1, Mathf.FloorToInt(r.YMax / cell));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if (reach[y * w + x]) return true;
            return false;
        }

        static List<int>[] BuildGraph(FloorPlan plan)
        {
            int roomCount = plan.Rooms.Count;
            List<int>[] adj = new List<int>[roomCount + plan.Corridors.Count];
            for (int i = 0; i < adj.Length; i++) adj[i] = new List<int>();

            for (int i = 0; i < roomCount; i++)
            {
                Room room = plan.Rooms[i];
                if (!room.HasDoor) continue;
                if (room.DoorToCorridor && room.CorridorIndex >= 0 && room.CorridorIndex < plan.Corridors.Count)
                {
                    int c = roomCount + room.CorridorIndex;
                    adj[i].Add(c);
                    adj[c].Add(i);
                }

                // 只认实际存在的门：额外门可能通向走廊，也可能通向相邻房间
                if (room.ExtraDoors == null) continue;
                for (int k = 0; k < room.ExtraDoors.Count; k++)
                {
                    Door d = room.ExtraDoors[k];
                    if (d.Corridor >= 0 && d.Corridor < plan.Corridors.Count)
                    {
                        int c = roomCount + d.Corridor;
                        adj[i].Add(c);
                        adj[c].Add(i);
                    }
                    else if (d.OtherRoom >= 0 && d.OtherRoom < roomCount)
                    {
                        adj[i].Add(d.OtherRoom);
                        adj[d.OtherRoom].Add(i);
                    }
                }
            }

            // 走廊之间也要连上：BSP 切出来的走廊会在交叉口/相邻处接续，
            // 只连房间-走廊会让整片区域在图上是孤岛（网格泛洪却能走通）。
            for (int a = 0; a < plan.Corridors.Count; a++)
            {
                for (int b = a + 1; b < plan.Corridors.Count; b++)
                {
                    if (!plan.Corridors[a].Rect.Inflate(0.3f).Overlaps(plan.Corridors[b].Rect)) continue;
                    int na = roomCount + a;
                    int nb = roomCount + b;
                    adj[na].Add(nb);
                    adj[nb].Add(na);
                }
            }

            return adj;
        }

        static void Bfs(List<int>[] adj, bool[] seen, Queue<int> queue)
        {
            while (queue.Count > 0)
            {
                int node = queue.Dequeue();
                List<int> list = adj[node];
                for (int i = 0; i < list.Count; i++)
                {
                    int next = list[i];
                    if (seen[next]) continue;
                    seen[next] = true;
                    queue.Enqueue(next);
                }
            }
        }

        static float Score(FloorPlan plan)
        {
            if (plan.Rooms.Count == 0) return 0f;

            int withDoor = 0;
            int corridorDoors = 0;
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                if (plan.Rooms[i].HasDoor) withDoor++;
                if (plan.Rooms[i].DoorToCorridor) corridorDoors++;
            }

            float doorRatio = withDoor / (float)plan.Rooms.Count;
            float corridorRatio = corridorDoors / (float)plan.Rooms.Count;
            float loopRatio = plan.LoopDoors.Count / (float)plan.Rooms.Count;

            float score = doorRatio * 0.5f + corridorRatio * 0.35f + Mathf.Clamp01(loopRatio * 2f) * 0.15f;
            return score;
        }

        static bool FindDoorBetween(RectF a, RectF b, out Vector2 center, out DoorSide side, out float overlapLen)
        {
            const float gap = 0.8f;
            center = Vector2.zero;
            side = DoorSide.South;
            overlapLen = 0f;

            float yLo = Mathf.Max(a.Y, b.Y);
            float yHi = Mathf.Min(a.YMax, b.YMax);
            if (yHi - yLo >= 1.0f)
            {
                if (Mathf.Abs(a.XMax - b.X) <= gap) { center = new Vector2((a.XMax + b.X) * 0.5f, (yLo + yHi) * 0.5f); side = DoorSide.East; overlapLen = yHi - yLo; return true; }
                if (Mathf.Abs(b.XMax - a.X) <= gap) { center = new Vector2((b.XMax + a.X) * 0.5f, (yLo + yHi) * 0.5f); side = DoorSide.West; overlapLen = yHi - yLo; return true; }
            }

            float xLo = Mathf.Max(a.X, b.X);
            float xHi = Mathf.Min(a.XMax, b.XMax);
            if (xHi - xLo >= 1.0f)
            {
                if (Mathf.Abs(a.YMax - b.Y) <= gap) { center = new Vector2((xLo + xHi) * 0.5f, (a.YMax + b.Y) * 0.5f); side = DoorSide.North; overlapLen = xHi - xLo; return true; }
                if (Mathf.Abs(b.YMax - a.Y) <= gap) { center = new Vector2((xLo + xHi) * 0.5f, (b.YMax + a.Y) * 0.5f); side = DoorSide.South; overlapLen = xHi - xLo; return true; }
            }

            return false;
        }

        static bool AnyOverlap(List<RectF> list, RectF r)
        {
            for (int i = 0; i < list.Count; i++) if (list[i].Overlaps(r)) return true;
            return false;
        }

        static void AddCorridor(FloorPlan plan, RectF r, bool main, bool service)
        {
            Corridor c;
            c.Rect = r;
            c.Main = main;
            c.Service = service;
            c.Vestibule = false;
            c.Vertical = r.H >= r.W;
            plan.Corridors.Add(c);
        }

        static void AddColumns(FloorPlan plan, FloorSpec spec)
        {
            for (float x = spec.ColumnGrid; x < spec.Width - 0.01f; x += spec.ColumnGrid)
            {
                for (float y = spec.ColumnGrid; y < spec.Height - 0.01f; y += spec.ColumnGrid)
                {
                    Vector2 p = new Vector2(x, y);
                    if (plan.Core.Inflate(0.3f).Contains(p)) continue;
                    if (AnyRectContains(plan.Stairs, p, 0.3f)) continue;
                    plan.Columns.Add(p);
                }
            }
        }

        static bool AnyRectContains(List<RectF> rects, Vector2 p, float pad)
        {
            for (int i = 0; i < rects.Count; i++) if (rects[i].Inflate(pad).Contains(p)) return true;
            return false;
        }

        static void AddZones(FloorPlan plan, FloorSpec spec)
        {
            float hw = spec.Width * 0.5f;
            float hh = spec.Height * 0.5f;
            FloorPrototype[] order = ZoneOrder(spec.Prototype);
            RectF[] rects =
            {
                new RectF(0f, 0f, hw, hh),
                new RectF(hw, 0f, hw, hh),
                new RectF(0f, hh, hw, hh),
                new RectF(hw, hh, hw, hh)
            };

            for (int i = 0; i < rects.Length; i++)
            {
                Zone z;
                z.Rect = rects[i];
                z.Prototype = order[i];
                z.Name = NameOf(order[i]);
                plan.Zones.Add(z);
            }
        }

        static FloorPrototype[] ZoneOrder(FloorPrototype p)
        {
            switch (p)
            {
                case FloorPrototype.Executive:
                    return new FloorPrototype[] { FloorPrototype.Executive, FloorPrototype.Meeting, FloorPrototype.Executive, FloorPrototype.SmallOffice };
                case FloorPrototype.Meeting:
                    return new FloorPrototype[] { FloorPrototype.Meeting, FloorPrototype.Meeting, FloorPrototype.SmallOffice, FloorPrototype.Meeting };
                case FloorPrototype.SmallOffice:
                    return new FloorPrototype[] { FloorPrototype.SmallOffice, FloorPrototype.SmallOffice, FloorPrototype.Meeting, FloorPrototype.SmallOffice };
                case FloorPrototype.Renovation:
                    return new FloorPrototype[] { FloorPrototype.Renovation, FloorPrototype.Renovation, FloorPrototype.OpenOffice, FloorPrototype.Utility };
                case FloorPrototype.Utility:
                    return new FloorPrototype[] { FloorPrototype.Utility, FloorPrototype.Utility, FloorPrototype.Renovation, FloorPrototype.Renovation };
                default:
                    return new FloorPrototype[] { FloorPrototype.OpenOffice, FloorPrototype.OpenOffice, FloorPrototype.Meeting, FloorPrototype.SmallOffice };
            }
        }

        static int ZoneIndexAt(FloorPlan plan, Vector2 p)
        {
            for (int i = 0; i < plan.Zones.Count; i++) if (plan.Zones[i].Rect.Contains(p)) return i;
            return 0;
        }

        /// <summary>
        /// 本层固定撤离设施：两座消防梯、核心筒电梯/货梯、东西两侧擦窗机，顶部再加天台下索。
        /// 这里只放候选，取哪 4–6 个由 EnsureExtractionSpread 按出生区散布挑选。
        /// </summary>
        static void AddExtractions(FloorPlan plan, FloorSpec spec, System.Random rng)
        {
            List<ExtractionPoint> pool = new List<ExtractionPoint>();

            pool.Add(MakeExtraction(plan.Stairs[0], ExtractionKind.FireStair, 20f, false, false, "撤离 · 消防梯"));
            pool.Add(MakeExtraction(plan.Stairs[1], ExtractionKind.FireStair, 20f, false, false, "撤离 · 消防梯"));

            RectF elevator = new RectF(plan.Core.X + plan.Core.W * 0.5f - 2.6f, plan.Core.Y - 0.2f, 5.2f, 2.6f);
            pool.Add(MakeExtraction(elevator, ExtractionKind.Elevator, 10f, true, false, "撤离 · 电梯"));

            RectF freight = new RectF(plan.Core.X + plan.Core.W * 0.5f - 2.2f, plan.Core.YMax - 2.4f, 4.4f, 2.4f);
            pool.Add(MakeExtraction(freight, ExtractionKind.Freight, 15f, false, true, "撤离 · 货梯"));

            // 擦窗机轨道沿四条边各有一个停靠位：只放东西两个的话，贴着同一侧角的出生区
            // 走到哪儿都不到"对角线 60%"，凑不出两个远端撤离（实测 seed 88 Tower 只有 1 个）。
            RectF basketSW = new RectF(0.2f, spec.Height * 0.25f - 3f, 2.4f, 6f);
            RectF basketNW = new RectF(0.2f, spec.Height * 0.75f - 3f, 2.4f, 6f);
            RectF basketSE = new RectF(spec.Width - 2.6f, spec.Height * 0.25f - 3f, 2.4f, 6f);
            RectF basketNE = new RectF(spec.Width - 2.6f, spec.Height * 0.75f - 3f, 2.4f, 6f);
            pool.Add(MakeExtraction(basketSW, ExtractionKind.WindowBasket, 25f, false, false, "撤离 · 擦窗机"));
            pool.Add(MakeExtraction(basketNW, ExtractionKind.WindowBasket, 25f, false, false, "撤离 · 擦窗机"));
            pool.Add(MakeExtraction(basketSE, ExtractionKind.WindowBasket, 25f, false, false, "撤离 · 擦窗机"));
            pool.Add(MakeExtraction(basketNE, ExtractionKind.WindowBasket, 25f, false, false, "撤离 · 擦窗机"));

            if (spec.Band == FloorBand.Top)
            {
                RectF rappel = new RectF(spec.Width - 22f, spec.Height - 22f, 10f, 10f);
                pool.Add(MakeExtraction(rappel, ExtractionKind.RoofRappel, 30f, false, false, "撤离 · 天台下索"));
            }

            // 贴边的设施（擦窗机/天台下索）可能落在截角或缺口外面：沿指向层心的方向拉回外廓内。
            for (int i = 0; i < pool.Count; i++)
            {
                ExtractionPoint p = pool[i];
                if (plan.Envelope == null) break;
                if (FloorEnvelope.Contains(plan.Envelope.Outline, p.Center)) continue;
                Vector2 q = PullInsideFloor(plan, p.Center);
                p.Rect = new RectF(q.x - p.Rect.W * 0.5f, q.y - p.Rect.H * 0.5f, p.Rect.W, p.Rect.H);
                p.Center = p.Rect.Center;
                pool[i] = p;
            }

            // 打乱候选顺序：选点组合按此顺序做确定性 tie-break，种子不同则候选优先序也不同
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                ExtractionPoint tmp = pool[i];
                pool[i] = pool[j];
                pool[j] = tmp;
            }

            plan.ExtractionCandidates.AddRange(pool);
        }

        static ExtractionPoint MakeExtraction(RectF rect, ExtractionKind kind, float seconds, bool power, bool card, string label)
        {
            ExtractionPoint p;
            p.Rect = rect;
            p.Center = rect.Center;
            p.Kind = kind;
            p.NeedsPower = power;
            p.NeedsCard = card;
            p.ChannelSeconds = seconds;
            p.Label = label;
            return p;
        }

        /// <summary>出生区候选：中心必须在外廓内部、且离外廓边缘有半个出生区的余量。</summary>
        static bool SpawnCandidateInside(FloorPlan plan, RectF r)
        {
            if (plan.Envelope == null) return true;
            float margin = Mathf.Min(8f, Mathf.Min(r.W, r.H) * 0.5f);
            return FloorEnvelope.Contains(plan.Envelope.Outline, r.Center)
                && FloorEnvelope.DistanceToEdge(plan.Envelope.Outline, r.Center) >= margin;
        }

        /// <summary>把一个可能落在外廓外面的点，沿指向层心的方向拉回外廓内部。</summary>
        static Vector2 PullInsideFloor(FloorPlan plan, Vector2 p)
        {
            if (plan.Envelope == null) return p;
            Vector2 c = plan.Bounds.Center;
            for (int step = 0; step <= 20; step++)
            {
                Vector2 q = Vector2.Lerp(p, c, step * 0.05f);
                if (FloorEnvelope.Contains(plan.Envelope.Outline, q)) return q;
            }
            return c;
        }

        /// <summary>出生区必须落在真实可走的地面上：四个角各取最近的房间/走廊，而不是写死角落坐标。</summary>
        static void AddSpawnZones(FloorPlan plan, FloorSpec spec)
        {
            Vector2[] corners =
            {
                new Vector2(14f, 14f),
                new Vector2(spec.Width - 14f, 14f),
                new Vector2(14f, spec.Height - 14f),
                new Vector2(spec.Width - 14f, spec.Height - 14f),
            };

            List<RectF> candidates = new List<RectF>();
            List<RectF> loose = new List<RectF>();
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                RectF r = plan.Rooms[i].Rect;
                if (r.W < 4f || r.H < 4f) continue;
                loose.Add(r);
                if (SpawnCandidateInside(plan, r)) candidates.Add(r);
            }
            for (int i = 0; i < plan.Corridors.Count; i++)
            {
                RectF r = plan.Corridors[i].Rect;
                if (r.W < 4f || r.H < 4f) continue;
                loose.Add(r);
                if (SpawnCandidateInside(plan, r)) candidates.Add(r);
            }
            // 外廓截角/缺口会吃掉角上的房间；万一没有足够贴着四角的候选，就退回全部房间。
            if (candidates.Count < corners.Length) candidates = loose;

            List<int> used = new List<int>();
            for (int c = 0; c < corners.Length; c++)
            {
                int best = -1;
                float bestDist = float.MaxValue;
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (used.Contains(i)) continue;
                    float d = Vector2.Distance(candidates[i].Center, corners[c]);
                    if (d < bestDist) { bestDist = d; best = i; }
                }

                if (best < 0)
                {
                    // 没有可用房间时不写死角落：角落可能正好在外廓截角/缺口外面。
                    Vector2 p = PullInsideFloor(plan, corners[c]);
                    plan.SpawnZones.Add(new RectF(p.x - 6f, p.y - 6f, 12f, 12f));
                    continue;
                }

                used.Add(best);
                RectF pick = candidates[best];
                float size = Mathf.Min(10f, Mathf.Min(pick.W - 0.4f, pick.H - 0.4f));
                if (size < 2f) size = 2f;
                Vector2 center = pick.Center;
                plan.SpawnZones.Add(new RectF(center.x - size * 0.5f, center.y - size * 0.5f, size, size));
            }
        }
    }
}
