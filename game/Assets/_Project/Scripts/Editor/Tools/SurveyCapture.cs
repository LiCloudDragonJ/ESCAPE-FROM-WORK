using System.Collections.Generic;
using EscapeFromWork.LevelGen;
using UnityEditor;
using UnityEngine;

namespace EscapeFromWork.EditorTools
{
    /// <summary>
    /// 出"效果图"用：当前生成器的平面图（全层 + 走廊断点特写 + 小地图外观），
    /// 加上 3D 场景截图（由 M1FloorScene 负责）。这里只画图，不改生成器。
    ///
    /// 颜色约定（对图纸说话的图例，别随便改）：
    ///   房间 Normal  #35505F   服务间 Storage #6B5A3A / Toilet #3F6B5A / Riser #5A4A6B
    ///   走廊 主廊   #5C6674   服务廊 #2E7A6A   门斗 #D9A441
    ///   核心筒      #262C34   楼梯 #1B2026   门（含门斗开口） #F2F5F8
    ///   撤离点      #6BE08A   出生区 #6BA8E0
    /// </summary>
    public static class SurveyCapture
    {
        const string Dir = "docs/3d-rebuild/screenshots/";

        static readonly Color32 Bg = new Color32(15, 18, 23, 255);
        static readonly Color32 Floor = new Color32(28, 33, 40, 255);
        static readonly Color32 Wall = new Color32(214, 222, 232, 255);
        static readonly Color32 RoomNormal = new Color32(0x24, 0x37, 0x42, 255);
        static readonly Color32 RoomStorage = new Color32(0x6B, 0x5A, 0x3A, 255);
        static readonly Color32 RoomToilet = new Color32(0x3F, 0x6B, 0x5A, 255);
        static readonly Color32 RoomRiser = new Color32(0x5A, 0x4A, 0x6B, 255);
        static readonly Color32 CorrMain = new Color32(0x6E, 0x79, 0x8A, 255);
        static readonly Color32 CorrService = new Color32(0x2E, 0x8B, 0x77, 255);
        static readonly Color32 CorrVest = new Color32(0xE8, 0xA9, 0x3F, 255);
        static readonly Color32 CoreCol = new Color32(0x26, 0x2C, 0x34, 255);
        static readonly Color32 StairCol = new Color32(0x1B, 0x20, 0x26, 255);
        static readonly Color32 DoorCol = new Color32(0xF2, 0xF5, 0xF8, 255);
        static readonly Color32 ExtractionCol = new Color32(0x6B, 0xE0, 0x8A, 255);
        static readonly Color32 SpawnCol = new Color32(0x6B, 0xA8, 0xE0, 255);

        [MenuItem("Escape From Work/Survey/Capture Plans And Maps")]
        public static void CapturePlans()
        {
            System.IO.Directory.CreateDirectory(Path());
            int[] seeds = { 1, 7 };
            for (int i = 0; i < seeds.Length; i++)
            {
                FloorPlan plan = FloorGenerator.Generate(FloorSpec.Default(seeds[i]));
                Save(Path() + "plan-" + seeds[i] + ".png", RenderPlan(plan, 2400, 1920));
                Save(Path() + "plan-" + seeds[i] + "-map.png", RenderMapMock(plan, 900, 720));
                Save(Path() + "plan-" + seeds[i] + "-corridor.png", RenderCorridorCloseUp(plan, 1200, 700));
                Debug.Log(string.Format("[Survey] seed={0} rooms={1} corridors={2} openings={3} extractions={4}",
                    seeds[i], plan.Rooms.Count, plan.Corridors.Count, plan.CorridorOpenings.Count, plan.Extractions.Count));
            }
            Debug.Log("[Survey] plans written to " + Path());
        }

        static string Path()
        {
            return System.IO.Path.GetFullPath(
                System.IO.Path.Combine(Application.dataPath, "../../" + Dir));
        }

        static void Save(string file, Texture2D tex)
        {
            System.IO.File.WriteAllBytes(file, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        // ---------- 全层平面图 ----------

        static Texture2D RenderPlan(FloorPlan plan, int W, int H)
        {
            Color32[] px = new Color32[W * H];
            for (int i = 0; i < px.Length; i++) px[i] = Bg;

            float sx = W / plan.Spec.Width;
            float sy = H / plan.Spec.Height;

            // 楼板：外廓多边形
            FillEnvelope(px, W, H, plan, sx, sy, Floor);

            // 走廊先画（房间压在上面边界更干净）
            for (int i = 0; i < plan.Corridors.Count; i++)
            {
                Corridor c = plan.Corridors[i];
                Color32 col = c.Vestibule ? CorrVest : (c.Service ? CorrService : CorrMain);
                FillRect(px, W, H, c.Rect, sx, sy, col);
            }

            // 核心筒 / 楼梯
            FillRect(px, W, H, plan.Core, sx, sy, CoreCol);
            for (int i = 0; i < plan.Stairs.Count; i++) FillRect(px, W, H, plan.Stairs[i], sx, sy, StairCol);

            // 房间按用途上色 + 描边
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                Room room = plan.Rooms[i];
                FillRect(px, W, H, room.Rect, sx, sy, RoomColor(room.Type));
                OutlineRect(px, W, H, room.Rect, sx, sy, Shade(RoomColor(room.Type), 1.5f), 1);
            }

            // 门（房间门 + 环路门 + 走廊内部开口）
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                Room room = plan.Rooms[i];
                if (room.HasDoor) Dot(px, W, H, room.Door.Center, sx, sy, DoorCol, 2);
                if (room.ExtraDoors != null)
                    for (int k = 0; k < room.ExtraDoors.Count; k++)
                        Dot(px, W, H, room.ExtraDoors[k].Center, sx, sy, DoorCol, 2);
            }
            for (int i = 0; i < plan.LoopDoors.Count; i++) Dot(px, W, H, plan.LoopDoors[i].Center, sx, sy, DoorCol, 2);
            for (int i = 0; i < plan.CorridorOpenings.Count; i++) Dot(px, W, H, plan.CorridorOpenings[i].Center, sx, sy, DoorCol, 2);

            // 出生区 / 撤离点
            for (int i = 0; i < plan.SpawnZones.Count; i++)
            {
                RectF z = plan.SpawnZones[i];
                OutlineRect(px, W, H, z, sx, sy, SpawnCol, 2);
                FillRect(px, W, H, new RectF(z.Center.x - 0.6f, z.Center.y - 0.6f, 1.2f, 1.2f), sx, sy, SpawnCol);
            }
            for (int i = 0; i < plan.Extractions.Count; i++)
            {
                ExtractionPoint e = plan.Extractions[i];
                FillRect(px, W, H, e.Rect, sx, sy, ExtractionCol);
                OutlineRect(px, W, H, e.Rect, sx, sy, new Color32(12, 40, 22, 255), 1);
            }

            // 外廓墙线最后画，压在一切上面
            DrawEnvelopeOutline(px, W, H, plan, sx, sy, Wall);

            Texture2D tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.name = "SurveyPlan";
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        static Color32 RoomColor(RoomType t)
        {
            switch (t)
            {
                case RoomType.Storage: return RoomStorage;
                case RoomType.Toilet: return RoomToilet;
                case RoomType.Riser: return RoomRiser;
            }
            return RoomNormal;
        }

        static Color32 Shade(Color32 c, float k)
        {
            return new Color32(
                (byte)Mathf.Min(255f, c.r * k),
                (byte)Mathf.Min(255f, c.g * k),
                (byte)Mathf.Min(255f, c.b * k), 255);
        }

        // ---------- 走廊断点特写 ----------

        /// <summary>
        /// 找一条带门斗、且旁边有服务廊的主廊，画它 ±18m 的范围，8px/m。
        /// 这是 R2 那条规则"任何直线都穿不过两道错位门"的直观证据。
        /// </summary>
        static Texture2D RenderCorridorCloseUp(FloorPlan plan, int W, int H)
        {
            Corridor host = default(Corridor);
            bool found = false;
            for (int i = 0; i < plan.Corridors.Count && !found; i++)
            {
                Corridor c = plan.Corridors[i];
                if (!c.Vestibule || c.Service) continue;
                bool near = false;
                for (int j = 0; j < plan.Corridors.Count && !near; j++)
                {
                    Corridor s = plan.Corridors[j];
                    if (!s.Service) continue;
                    if (s.Rect.Inflate(1.0f).Overlaps(c.Rect)) near = true;
                }
                if (near) { host = c; found = true; }
            }
            if (!found) return RenderPlan(plan, W, H);

            float win = 44f;
            float sx = W / win;
            float sy = H / win;
            float ox = host.Rect.Center.x - win * 0.5f;
            float oy = host.Rect.Center.y - win * 0.5f;

            Color32[] px = new Color32[W * H];
            for (int i = 0; i < px.Length; i++) px[i] = Bg;

            System.Action<RectF, Color32, int> rect = delegate(RectF r, Color32 col, int grow)
            {
                int x0 = Mathf.RoundToInt((r.X - ox) * sx) - grow;
                int x1 = Mathf.RoundToInt((r.XMax - ox) * sx) + grow;
                int y0 = Mathf.RoundToInt((r.Y - oy) * sy) - grow;
                int y1 = Mathf.RoundToInt((r.YMax - oy) * sy) + grow;
                for (int y = Mathf.Max(0, y0); y <= Mathf.Min(H - 1, y1); y++)
                    for (int x = Mathf.Max(0, x0); x <= Mathf.Min(W - 1, x1); x++)
                        px[y * W + x] = col;
            };

            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                Room room = plan.Rooms[i];
                if (!room.Rect.Inflate(0.1f).Overlaps(new RectF(ox, oy, win, win))) continue;
                rect(room.Rect, Shade(RoomColor(room.Type), 0.85f), 0);
            }
            for (int i = 0; i < plan.Corridors.Count; i++)
            {
                Corridor c = plan.Corridors[i];
                if (!c.Rect.Overlaps(new RectF(ox, oy, win, win))) continue;
                rect(c.Rect, c.Vestibule ? CorrVest : (c.Service ? CorrService : CorrMain), 0);
            }
            for (int i = 0; i < plan.CorridorOpenings.Count; i++)
            {
                Vector2 p = plan.CorridorOpenings[i].Center;
                if (p.x < ox || p.y < oy || p.x > ox + win || p.y > oy + win) continue;
                int cx = Mathf.RoundToInt((p.x - ox) * sx);
                int cy = Mathf.RoundToInt((p.y - oy) * sy);
                for (int dy = -5; dy <= 5; dy++)
                    for (int dx = -5; dx <= 5; dx++)
                    {
                        int x = cx + dx, y = cy + dy;
                        if (x < 0 || y < 0 || x >= W || y >= H) continue;
                        px[y * W + x] = DoorCol;
                    }
            }

            Texture2D tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.name = "SurveyCorridor";
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        // ---------- 小地图外观（游戏内 MapHud 的 MapRaster 输出） ----------

        static Texture2D RenderMapMock(FloorPlan plan, int W, int H)
        {
            Texture2D raster = EscapeFromWork.UI.MapRaster.Build(plan);
            Color32[] src = raster.GetPixels32();
            int sw = raster.width;
            int sh = raster.height;
            Color32[] px = new Color32[W * H];
            for (int y = 0; y < H; y++)
            {
                int syi = Mathf.Clamp(y * sh / H, 0, sh - 1);
                for (int x = 0; x < W; x++)
                {
                    int sxi = Mathf.Clamp(x * sw / W, 0, sw - 1);
                    px[y * W + x] = src[syi * sw + sxi];
                }
            }
            Object.DestroyImmediate(raster);
            Texture2D tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.name = "SurveyMap";
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        // ---------- 低层绘图 ----------

        static void FillRect(Color32[] px, int W, int H, RectF r, float sx, float sy, Color32 c)
        {
            int x0 = Mathf.Clamp(Mathf.RoundToInt(r.X * sx), 0, W - 1);
            int x1 = Mathf.Clamp(Mathf.RoundToInt(r.XMax * sx), 0, W - 1);
            int y0 = Mathf.Clamp(Mathf.RoundToInt(r.Y * sy), 0, H - 1);
            int y1 = Mathf.Clamp(Mathf.RoundToInt(r.YMax * sy), 0, H - 1);
            for (int y = y0; y <= y1; y++)
            {
                int row = y * W;
                for (int x = x0; x <= x1; x++) px[row + x] = c;
            }
        }

        static void OutlineRect(Color32[] px, int W, int H, RectF r, float sx, float sy, Color32 c, int grow)
        {
            int x0 = Mathf.Clamp(Mathf.RoundToInt(r.X * sx) - grow, 0, W - 1);
            int x1 = Mathf.Clamp(Mathf.RoundToInt(r.XMax * sx) + grow, 0, W - 1);
            int y0 = Mathf.Clamp(Mathf.RoundToInt(r.Y * sy) - grow, 0, H - 1);
            int y1 = Mathf.Clamp(Mathf.RoundToInt(r.YMax * sy) + grow, 0, H - 1);
            for (int x = x0; x <= x1; x++) { px[y0 * W + x] = c; px[y1 * W + x] = c; }
            for (int y = y0; y <= y1; y++) { px[y * W + x0] = c; px[y * W + x1] = c; }
        }

        static void Dot(Color32[] px, int W, int H, Vector2 p, float sx, float sy, Color32 c, int r)
        {
            int cx = Mathf.RoundToInt(p.x * sx);
            int cy = Mathf.RoundToInt(p.y * sy);
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int x = cx + dx, y = cy + dy;
                    if (x < 0 || y < 0 || x >= W || y >= H) continue;
                    px[y * W + x] = c;
                }
        }

        static void FillEnvelope(Color32[] px, int W, int H, FloorPlan plan, float sx, float sy, Color32 c)
        {
            if (plan.Envelope == null || plan.Envelope.Outline == null) return;
            Vector2[] poly = plan.Envelope.Outline;
            for (int y = 0; y < H; y++)
            {
                float wy = (y + 0.5f) / sy;
                for (int x = 0; x < W; x++)
                {
                    float wx = (x + 0.5f) / sx;
                    if (PointInPoly(poly, wx, wy)) px[y * W + x] = c;
                }
            }
        }

        static void DrawEnvelopeOutline(Color32[] px, int W, int H, FloorPlan plan, float sx, float sy, Color32 c)
        {
            if (plan.Envelope == null || plan.Envelope.Outline == null) return;
            Vector2[] poly = plan.Envelope.Outline;
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i];
                Vector2 b = poly[(i + 1) % poly.Length];
                int steps = Mathf.CeilToInt(Vector2.Distance(a, b) * Mathf.Max(sx, sy));
                for (int s = 0; s <= steps; s++)
                {
                    Vector2 p = Vector2.Lerp(a, b, steps == 0 ? 0f : (float)s / steps);
                    int x = Mathf.Clamp(Mathf.RoundToInt(p.x * sx), 0, W - 1);
                    int y = Mathf.Clamp(Mathf.RoundToInt(p.y * sy), 0, H - 1);
                    for (int dy = 0; dy <= 1; dy++)
                        for (int dx = 0; dx <= 1; dx++)
                        {
                            int xx = Mathf.Clamp(x + dx, 0, W - 1);
                            int yy = Mathf.Clamp(y + dy, 0, H - 1);
                            px[yy * W + xx] = c;
                        }
                }
            }
        }

        static bool PointInPoly(Vector2[] poly, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if ((poly[i].y > y) != (poly[j].y > y))
                {
                    float t = (y - poly[i].y) / (poly[j].y - poly[i].y);
                    if (x < poly[i].x + t * (poly[j].x - poly[i].x)) inside = !inside;
                }
            }
            return inside;
        }
    }
}
