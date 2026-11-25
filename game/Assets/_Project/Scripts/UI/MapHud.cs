using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using EscapeFromWork.LevelGen;

namespace EscapeFromWork.UI
{
    /// <summary>
    /// Region-level exploration fog. Pure data so it can be tested without a scene; the HUD
    /// only turns it into pixels. Sight works per 2 m cell: within range and inside the
    /// horizontal view cone. Walls are not checked yet (M1 simplification).
    /// </summary>
    public class MapFog
    {
        public const float CellSize = 2f;

        public readonly int CellW;
        public readonly int CellH;
        public readonly bool[] Explored;
        public readonly RectF Bounds;

        int _count;

        public MapFog(FloorPlan plan)
        {
            Bounds = plan.Bounds;
            CellW = Mathf.Max(1, Mathf.CeilToInt(Bounds.W / CellSize));
            CellH = Mathf.Max(1, Mathf.CeilToInt(Bounds.H / CellSize));
            Explored = new bool[CellW * CellH];
        }

        public float ExploredRatio
        {
            get { return Explored.Length == 0 ? 0f : _count / (float)Explored.Length; }
        }

        public Vector2 CellCenter(int x, int y)
        {
            return new Vector2(Bounds.X + (x + 0.5f) * CellSize, Bounds.Y + (y + 0.5f) * CellSize);
        }

        /// <summary>Marks every cell inside range and inside the view cone. Returns cells changed.</summary>
        public int Mark(Vector2 eye, Vector2 forward, float range, float halfFovDegrees)
        {
            int changed = 0;
            float cosHalf = Mathf.Cos(halfFovDegrees * Mathf.Deg2Rad);
            Vector2 fwd = forward.sqrMagnitude < 0.0001f ? Vector2.up : forward.normalized;

            int x0 = Mathf.Clamp(Mathf.FloorToInt((eye.x - range - Bounds.X) / CellSize), 0, CellW - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((eye.x + range - Bounds.X) / CellSize), 0, CellW - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt((eye.y - range - Bounds.Y) / CellSize), 0, CellH - 1);
            int y1 = Mathf.Clamp(Mathf.CeilToInt((eye.y + range - Bounds.Y) / CellSize), 0, CellH - 1);

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    int i = y * CellW + x;
                    if (Explored[i]) continue;
                    Vector2 c = CellCenter(x, y);
                    Vector2 d = c - eye;
                    float dist = d.magnitude;
                    if (dist > range) continue;
                    // Standing on a cell reveals it; beyond 2 m the view cone applies.
                    if (dist > CellSize && Vector2.Dot(fwd, d / Mathf.Max(0.0001f, dist)) < cosHalf) continue;
                    Explored[i] = true;
                    _count++;
                    changed++;
                }
            }
            return changed;
        }

        /// <summary>Fog texture: unexplored cells opaque, explored cells clear.</summary>
        public Texture2D BuildTexture()
        {
            Texture2D tex = new Texture2D(CellW, CellH, TextureFormat.RGBA32, false);
            tex.name = "MapFog";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            Color32[] px = new Color32[CellW * CellH];
            for (int i = 0; i < px.Length; i++) px[i] = Explored[i] ? new Color32(4, 6, 8, 0) : new Color32(4, 6, 8, 214);
            tex.SetPixels32(px);
            tex.Apply(false);
            return tex;
        }

        public void ApplyTexture(Texture2D tex)
        {
            if (tex == null) return;
            Color32[] px = new Color32[CellW * CellH];
            for (int i = 0; i < px.Length; i++) px[i] = Explored[i] ? new Color32(4, 6, 8, 0) : new Color32(4, 6, 8, 214);
            tex.SetPixels32(px);
            tex.Apply(false);
        }
    }

    /// <summary>Bakes the plan into a 2D top-down texture: walls, rooms, corridors, doors.</summary>
    public static class MapRaster
    {
        public const float PixelsPerMeter = 4f;

        static readonly Color32 Bg = new Color32(10, 12, 15, 255);
        static readonly Color32 CorridorFill = new Color32(30, 34, 40, 255);
        static readonly Color32 SecondaryFill = new Color32(26, 30, 35, 255);
        static readonly Color32 Wall = new Color32(122, 130, 142, 255);
        static readonly Color32 CoreFill = new Color32(20, 22, 26, 255);
        static readonly Color32 StairFill = new Color32(16, 18, 21, 255);
        static readonly Color32 DoorDot = new Color32(214, 176, 92, 255);
        static readonly Color32 SpawnLine = new Color32(74, 196, 138, 255);
        static readonly Color32 ColumnDot = new Color32(58, 64, 74, 255);

        public static Texture2D Build(FloorPlan plan)
        {
            int w = Mathf.CeilToInt(plan.Bounds.W * PixelsPerMeter);
            int h = Mathf.CeilToInt(plan.Bounds.H * PixelsPerMeter);
            Color32[] px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = Bg;

            for (int i = 0; i < plan.Corridors.Count; i++)
            {
                Corridor c = plan.Corridors[i];
                Fill(px, w, h, c.Rect, c.Main ? CorridorFill : SecondaryFill);
            }

            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                Room room = plan.Rooms[i];
                Color32 fill = ZoneColor(plan, room.ZoneIndex);
                Fill(px, w, h, room.Rect, fill);
                Outline(px, w, h, room.Rect, Wall);
            }

            for (int i = 0; i < plan.Stairs.Count; i++)
            {
                Fill(px, w, h, plan.Stairs[i], StairFill);
                Outline(px, w, h, plan.Stairs[i], Wall);
                Hatch(px, w, h, plan.Stairs[i], new Color32(52, 58, 68, 255));
            }

            Fill(px, w, h, plan.Core, CoreFill);
            Outline(px, w, h, plan.Core, Wall);

            for (int i = 0; i < plan.Columns.Count; i++)
            {
                Vector2 c = plan.Columns[i];
                Dot(px, w, h, c, ColumnDot, 2);
            }

            List<Door> doors = CollectDoors(plan);
            for (int i = 0; i < doors.Count; i++) Dot(px, w, h, doors[i].Center, DoorDot, 2);

            for (int i = 0; i < plan.SpawnZones.Count; i++)
            {
                Outline(px, w, h, plan.SpawnZones[i], SpawnLine);
                Outline(px, w, h, plan.SpawnZones[i].Inflate(-0.8f), SpawnLine);
            }

            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.name = "MapRaster";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(px);
            tex.Apply(false);
            return tex;
        }

        public static List<Door> CollectDoors(FloorPlan plan)
        {
            List<Door> doors = new List<Door>();
            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                Room r = plan.Rooms[i];
                AddDoor(doors, seen, r.Door);
                if (r.ExtraDoors == null) continue;
                for (int k = 0; k < r.ExtraDoors.Count; k++) AddDoor(doors, seen, r.ExtraDoors[k]);
            }
            for (int i = 0; i < plan.LoopDoors.Count; i++) AddDoor(doors, seen, plan.LoopDoors[i]);
            return doors;
        }

        static void AddDoor(List<Door> doors, HashSet<string> seen, Door d)
        {
            if (d.Width <= 0f) return;
            string key = Mathf.RoundToInt(d.Center.x * 10f) + ":" + Mathf.RoundToInt(d.Center.y * 10f) + ":" + d.Side;
            if (!seen.Add(key)) return;
            doors.Add(d);
        }

        public static Color32 ZoneColor(FloorPlan plan, int zoneIndex)
        {
            FloorPrototype p = FloorPrototype.OpenOffice;
            if (zoneIndex >= 0 && zoneIndex < plan.Zones.Count) p = plan.Zones[zoneIndex].Prototype;
            switch (p)
            {
                case FloorPrototype.Executive: return new Color32(92, 78, 58, 255);
                case FloorPrototype.Meeting: return new Color32(78, 88, 66, 255);
                case FloorPrototype.SmallOffice: return new Color32(82, 74, 92, 255);
                case FloorPrototype.Renovation: return new Color32(76, 72, 68, 255);
                case FloorPrototype.Utility: return new Color32(60, 62, 68, 255);
                default: return new Color32(66, 80, 98, 255);
            }
        }

        static void Fill(Color32[] px, int w, int h, RectF r, Color32 c)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt((r.X - 0f) * PixelsPerMeter), 0, w);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(r.XMax * PixelsPerMeter), 0, w);
            int y0 = Mathf.Clamp(Mathf.FloorToInt((r.Y - 0f) * PixelsPerMeter), 0, h);
            int y1 = Mathf.Clamp(Mathf.CeilToInt(r.YMax * PixelsPerMeter), 0, h);
            for (int y = y0; y < y1; y++)
            {
                int row = y * w;
                for (int x = x0; x < x1; x++) px[row + x] = c;
            }
        }

        static void Outline(Color32[] px, int w, int h, RectF r, Color32 c)
        {
            FillRaw(px, w, h, r.X, r.Y, r.XMax, r.Y + 0.25f, c);
            FillRaw(px, w, h, r.X, r.YMax - 0.25f, r.XMax, r.YMax, c);
            FillRaw(px, w, h, r.X, r.Y, r.X + 0.25f, r.YMax, c);
            FillRaw(px, w, h, r.XMax - 0.25f, r.Y, r.XMax, r.YMax, c);
        }

        static void FillRaw(Color32[] px, int w, int h, float xa, float ya, float xb, float yb, Color32 c)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt(xa * PixelsPerMeter), 0, w);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(xb * PixelsPerMeter), 0, w);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(ya * PixelsPerMeter), 0, h);
            int y1 = Mathf.Clamp(Mathf.CeilToInt(yb * PixelsPerMeter), 0, h);
            for (int y = y0; y < y1; y++)
            {
                int row = y * w;
                for (int x = x0; x < x1; x++) px[row + x] = c;
            }
        }

        static void Hatch(Color32[] px, int w, int h, RectF r, Color32 c)
        {
            for (float y = r.Y + 0.6f; y < r.YMax - 0.6f; y += 1.6f) FillRaw(px, w, h, r.X + 0.4f, y, r.XMax - 0.4f, y + 0.2f, c);
        }

        static void Dot(Color32[] px, int w, int h, Vector2 p, Color32 c, int radius)
        {
            int cx = Mathf.RoundToInt(p.x * PixelsPerMeter);
            int cy = Mathf.RoundToInt(p.y * PixelsPerMeter);
            for (int y = cy - radius; y <= cy + radius; y++)
            {
                if (y < 0 || y >= h) continue;
                for (int x = cx - radius; x <= cx + radius; x++)
                {
                    if (x < 0 || x >= w) continue;
                    px[y * w + x] = c;
                }
            }
        }
    }

    /// <summary>
    /// Hold M to read the floor map. Never pauses the game (this is an online extraction
    /// shooter): extractions are always visible, everything else appears as you explore.
    /// </summary>
    public class MapHud : MonoBehaviour
    {
        public FloorPlan Plan;
        public Transform Viewer;
        public float ViewRange = 30f;
        public float HudHeightFraction = 0.72f;

        MapFog _fog;
        Texture2D _mapTex;
        Texture2D _fogTex;
        Texture2D _diamond;
        Camera _camera;
        float _tick;
        bool _open;
        int _screens;

        public MapFog Fog { get { return _fog; } }
        public bool IsOpen { get { return _open; } }

        public void Initialize(FloorPlan plan, Transform viewer)
        {
            Plan = plan;
            Viewer = viewer;
            _fog = new MapFog(plan);
            _mapTex = MapRaster.Build(plan);
            _fogTex = _fog.BuildTexture();
            _diamond = DiamondTexture();
            _camera = viewer != null ? viewer.GetComponentInChildren<Camera>() : Camera.main;
        }

        /// Rebuilds map + fog for a new floor plan (runtime floor regeneration).
        public void Rebuild(FloorPlan plan)
        {
            if (_mapTex != null) Destroy(_mapTex);
            if (_fogTex != null) Destroy(_fogTex);
            if (_diamond != null) Destroy(_diamond);
            _open = false;
            Initialize(plan, Viewer);
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.mKey.wasPressedThisFrame) _open = !_open;
                if (keyboard.escapeKey.wasPressedThisFrame) _open = false;
            }
            if (Viewer == null || _fog == null) return;

            _tick += Time.unscaledDeltaTime;
            if (_tick < 0.15f) return;
            _tick = 0f;

            Vector3 fwd3 = Viewer.forward;
            Vector2 forward = new Vector2(fwd3.x, fwd3.z);
            if (forward.sqrMagnitude < 0.0001f) forward = Vector2.up;
            int changed = _fog.Mark(new Vector2(Viewer.position.x, Viewer.position.z), forward, ViewRange, HalfFov());
            if (changed > 0) _fog.ApplyTexture(_fogTex);
        }

        float HalfFov()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return 45f;
            float v = _camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float h = Mathf.Atan(Mathf.Tan(v) * Mathf.Max(1f, _camera.aspect)) * Mathf.Rad2Deg;
            return Mathf.Clamp(h, 30f, 70f);
        }

        void OnGUI()
        {
            if (!_open || Plan == null || _mapTex == null) return;
            _screens++;

            float panelH = Screen.height * HudHeightFraction;
            float panelW = panelH * (Plan.Bounds.W / Plan.Bounds.H);
            if (panelW > Screen.width * 0.94f)
            {
                panelW = Screen.width * 0.94f;
                panelH = panelW * (Plan.Bounds.H / Plan.Bounds.W);
            }
            float px = (Screen.width - panelW) * 0.5f;
            float py = (Screen.height - panelH) * 0.5f;
            Rect area = new Rect(px, py, panelW, panelH);

            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.DrawTexture(area, _mapTex, ScaleMode.StretchToFill, false);
            GUI.DrawTexture(area, _fogTex, ScaleMode.StretchToFill, true);

            DrawExtractions(area);
            DrawUnvisitedZoneNames(area);
            DrawChrome(area);
        }

        Vector2 ToScreen(Rect area, Vector2 planPoint)
        {
            float u = Mathf.InverseLerp(Plan.Bounds.X, Plan.Bounds.XMax, planPoint.x);
            float v = Mathf.InverseLerp(Plan.Bounds.Y, Plan.Bounds.YMax, planPoint.y);
            return new Vector2(area.x + u * area.width, area.y + (1f - v) * area.height);
        }

        void DrawExtractions(Rect area)
        {
            GUIStyle label = new GUIStyle(GUI.skin.label);
            label.fontSize = Mathf.RoundToInt(Mathf.Clamp(area.height * 0.024f, 10f, 18f));
            label.alignment = TextAnchor.MiddleLeft;
            label.normal.textColor = new Color(0.96f, 0.73f, 0.32f);

            for (int i = 0; i < Plan.Extractions.Count; i++)
            {
                ExtractionPoint e = Plan.Extractions[i];
                Vector2 p = ToScreen(area, e.Center);
                float size = Mathf.Clamp(area.height * 0.022f, 9f, 20f);
                GUI.color = new Color(0.98f, 0.72f, 0.28f, 1f);
                GUI.DrawTexture(new Rect(p.x - size * 0.5f, p.y - size * 0.5f, size, size), _diamond, ScaleMode.StretchToFill, true);
                GUI.color = Color.white;

                string text = e.Label + (e.NeedsPower ? " · 需供电" : e.NeedsCard ? " · 需门禁卡" : "")
                    + " " + Mathf.RoundToInt(e.ChannelSeconds) + "s";
                label.normal.textColor = new Color(0.96f, 0.73f, 0.32f);
                GUI.Label(new Rect(p.x + size * 0.8f, p.y - size * 0.8f, area.width * 0.22f, size * 1.6f), text, label);
            }
        }

        void DrawUnvisitedZoneNames(Rect area)
        {
            if (_fog == null) return;
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = Mathf.RoundToInt(Mathf.Clamp(area.height * 0.026f, 11f, 20f));
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.textColor = new Color(0.62f, 0.66f, 0.72f, 0.9f);

            for (int i = 0; i < Plan.Zones.Count; i++)
            {
                Zone z = Plan.Zones[i];
                float seen = 0f;
                int total = 0;
                for (int y = 0; y < _fog.CellH; y++)
                {
                    for (int x = 0; x < _fog.CellW; x++)
                    {
                        Vector2 c = _fog.CellCenter(x, y);
                        if (!z.Rect.Contains(c)) continue;
                        total++;
                        if (_fog.Explored[y * _fog.CellW + x]) seen++;
                    }
                }
                if (total == 0 || seen / total > 0.35f) continue;
                Vector2 p = ToScreen(area, z.Rect.Center);
                GUI.Label(new Rect(p.x - area.width * 0.12f, p.y - 12f, area.width * 0.24f, 26f), z.Name, style);
            }
        }

        void DrawChrome(Rect area)
        {
            GUIStyle title = new GUIStyle(GUI.skin.label);
            title.fontSize = Mathf.RoundToInt(Mathf.Clamp(area.height * 0.036f, 14f, 26f));
            title.fontStyle = FontStyle.Bold;
            title.normal.textColor = new Color(0.93f, 0.95f, 0.98f);

            string head = "本层 " + Mathf.RoundToInt(Plan.Bounds.Area) + "㎡ · 已探索 "
                + Mathf.RoundToInt(_fog.ExploredRatio * 100f) + "%";
            GUI.Label(new Rect(area.x + 8f, area.y - title.fontSize - 10f, area.width * 0.7f, title.fontSize + 6f), head, title);

            GUIStyle small = new GUIStyle(GUI.skin.label);
            small.fontSize = Mathf.RoundToInt(Mathf.Clamp(area.height * 0.021f, 10f, 15f));
            small.normal.textColor = new Color(0.62f, 0.66f, 0.72f);
            GUI.Label(new Rect(area.x + 8f, area.y + area.height + 4f, area.width, small.fontSize + 6f),
                "M 关闭 · 不暂停 · 撤离点常显 · 未探索区只有区域名", small);

            GUI.Label(new Rect(area.x + 8f, area.y - title.fontSize - 10f, area.width - 16f, title.fontSize + 6f),
                "", title);
        }

        static Texture2D DiamondTexture()
        {
            const int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "MapDiamond";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            Color32[] px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Abs(x + 0.5f - half) + Mathf.Abs(y + 0.5f - half);
                    bool inside = d <= half - 2f;
                    bool ring = d > half - 5f && d <= half - 1f;
                    px[y * size + x] = inside ? new Color32(255, 255, 255, 255)
                        : ring ? new Color32(20, 16, 8, 220) : new Color32(0, 0, 0, 0);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false);
            return tex;
        }

        void OnDestroy()
        {
            if (_mapTex != null) Destroy(_mapTex);
            if (_fogTex != null) Destroy(_fogTex);
            if (_diamond != null) Destroy(_diamond);
        }
    }
}
