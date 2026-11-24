using System.Collections.Generic;
using EscapeFromWork.LevelGen;
using UnityEditor;
using UnityEngine;

namespace EscapeFromWork.Editor
{
    /// <summary>
    /// Floor Lab：调参数、生成单层平面、俯视图人工审。
    /// 菜单 Tools/EFW/Floor Lab。
    /// </summary>
    public class FloorLabWindow : EditorWindow
    {
        int _seed = 1;
        float _width = 200f;
        float _height = 160f;
        FloorBand _band = FloorBand.Tower;
        FloorPrototype _prototype = FloorPrototype.OpenOffice;
        float _mainCorridor = 4.8f;
        float _secondaryCorridor = 3.6f;

        FloorPlan _plan;
        Texture2D _tex;
        Vector2 _scroll;

        const int TexW = 900;
        const int TexH = 720;

        [MenuItem("Tools/EFW/Floor Lab")]
        public static void Open()
        {
            FloorLabWindow w = GetWindow<FloorLabWindow>("Floor Lab");
            w.minSize = new Vector2(720f, 560f);
            w.Generate();
        }

        /// <summary>批量出图：几个典型种子直接落 PNG，免得人工点菜单。</summary>
        [MenuItem("Tools/EFW/Floor Lab — 批量出图")]
        public static void CaptureBatch()
        {
            string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../docs/3d-rebuild/screenshots"));
            System.IO.Directory.CreateDirectory(dir);

            // 三种走廊分级方案，同一层同一装修，供人工对比
            string[] tags = { "A", "B", "C" };
            int[] levelSets = { 2, 3, 3 };
            float[] tertiaryAreas = { 190f, 190f, 260f };
            for (int l = 0; l < levelSets.Length; l++)
            {
                {
                    FloorSpec spec = new FloorSpec();
                    spec.Seed = 1;
                    spec.Prototype = FloorPrototype.OpenOffice;
                    spec.CorridorLevels = levelSets[l];
                    spec.TertiaryMinArea = tertiaryAreas[l];

                    FloorPlan plan = FloorGenerator.Generate(spec);
                    Texture2D tex = Render(plan);
                    string path = System.IO.Path.Combine(dir, "m1-corridor-" + tags[l] + ".png");
                    System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
                    Object.DestroyImmediate(tex);

                    float cov = plan.RoomArea / (spec.Width * spec.Height);
                    float maxArea = 0f;
                    int bigRooms = 0;
                    for (int i = 0; i < plan.Rooms.Count; i++)
                    {
                        float ra = plan.Rooms[i].Rect.Area;
                        if (ra > maxArea) maxArea = ra;
                        if (ra > 620f) bigRooms++;
                    }
                    int nearSide = 0, farSide = 0;
                    int[] bySize = new int[3];
                    for (int i = 0; i < plan.Props.Count; i++)
                    {
                        if (plan.Props[i].FarSide) farSide++; else nearSide++;
                        bySize[(int)plan.Props[i].Spec.Size]++;
                    }
                    int hitRoom = -1, hitCorr = -1;
                    for (int i = 0; i < plan.Rooms.Count && hitRoom < 0; i++)
                        for (int c = 0; c < plan.Corridors.Count; c++)
                            if (plan.Rooms[i].Rect.Overlaps(plan.Corridors[c].Rect)) { hitRoom = i; hitCorr = c; break; }
                    if (hitRoom >= 0)
                        Debug.Log(string.Format("[FloorLab] overlap room#{0} {1} vs corr#{2} {3}", hitRoom,
                            plan.Rooms[hitRoom].Rect, hitCorr, plan.Corridors[hitCorr].Rect));

                    float corrArea = 0f;
                    for (int i = 0; i < plan.Corridors.Count; i++) corrArea += plan.Corridors[i].Rect.Area;
                    float hole = 1f - (plan.RoomArea + corrArea) / (spec.Width * spec.Height);
                    Debug.Log(string.Format("[FloorLab] seed={0} levels={1} rooms={2} coverage={3:P0} corr={17:P0} hole={18:P0} doorless={4} loops={5} corridors={6} len={7:0}m maxRoom={8:0}㎡ bigRooms={9} extractions={10} props={11}(近{12}/远{13}) 大{14}/中{15}/小{16}",
                        spec.Seed, spec.CorridorLevels, plan.Rooms.Count, cov, CountDoorless(plan), plan.LoopDoors.Count,
                        plan.Corridors.Count, plan.CorridorLength, maxArea, bigRooms, plan.Extractions.Count,
                        plan.Props.Count, nearSide, farSide, bySize[0], bySize[1], bySize[2],
                        corrArea / (spec.Width * spec.Height), hole));

                    if (l == 1)
                    {
                        Texture2D hires = Render(plan, 2400, 1920);
                        System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "m1-floor-hires.png"), hires.EncodeToPNG());
                        Object.DestroyImmediate(hires);
                    }
                }
            }
        }

        void OnDisable()
        {
            if (_tex != null) DestroyImmediate(_tex);
            _tex = null;
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("楼层参数", EditorStyles.boldLabel);
            _seed = EditorGUILayout.IntField("Seed", _seed);
            _width = EditorGUILayout.FloatField("宽度 (m)", _width);
            _height = EditorGUILayout.FloatField("深度 (m)", _height);
            _band = (FloorBand)EditorGUILayout.EnumPopup("区段", _band);
            _prototype = (FloorPrototype)EditorGUILayout.EnumPopup("装修原型", _prototype);
            _mainCorridor = EditorGUILayout.FloatField("主走廊宽 (m)", _mainCorridor);
            _secondaryCorridor = EditorGUILayout.FloatField("次走廊宽 (m)", _secondaryCorridor);

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("生成", GUILayout.Height(28f))) Generate();
            if (GUILayout.Button("随机种子", GUILayout.Height(28f))) { _seed = Random.Range(1, 999999); Generate(); }
            if (GUILayout.Button("种子 +1", GUILayout.Height(28f))) { _seed++; Generate(); }
            if (GUILayout.Button("导出 PNG", GUILayout.Height(28f))) ExportPng();
            EditorGUILayout.EndHorizontal();

            if (_plan == null)
            {
                EditorGUILayout.HelpBox("点\"生成\"出一张平面图。", MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("统计", EditorStyles.boldLabel);
            float total = _width * _height;
            EditorGUILayout.LabelField(string.Format(
                "房间 {0} 间 · 房间面积 {1:0}㎡ ({2:P0}) · 平均 {3:0}㎡ · 走廊总长 {4:0}m · 撤离点 {5} 个 · 无门房间 {6} 间",
                _plan.Rooms.Count,
                _plan.RoomArea,
                _plan.RoomArea / Mathf.Max(1f, total),
                _plan.RoomArea / Mathf.Max(1f, _plan.Rooms.Count),
                _plan.CorridorLength,
                _plan.Extractions.Count,
                CountDoorless(_plan)));

            EditorGUILayout.Space();
            if (_tex != null)
            {
                float avail = Mathf.Min(position.width - 40f, TexW);
                float h = avail * TexH / TexW;
                Rect r = GUILayoutUtility.GetRect(avail, h);
                GUI.DrawTexture(r, _tex, ScaleMode.ScaleToFit, false);
                DrawZoneLabels(r);
            }

            EditorGUILayout.EndScrollView();
        }

        void DrawZoneLabels(Rect r)
        {
            if (_plan == null) return;
            GUIStyle st = new GUIStyle(EditorStyles.boldLabel);
            st.normal.textColor = new Color(0.75f, 0.79f, 0.84f);
            st.alignment = TextAnchor.MiddleCenter;
            for (int i = 0; i < _plan.Zones.Count; i++)
            {
                Zone z = _plan.Zones[i];
                Vector2 c = z.Rect.Center;
                float px = r.x + c.x / _width * r.width;
                float py = r.y + (1f - c.y / _height) * r.height;
                Vector2 size = st.CalcSize(new GUIContent(z.Name));
                GUI.Label(new Rect(px - size.x * 0.5f, py - size.y * 0.5f, size.x, size.y), z.Name, st);
            }
        }

        static int CountDoorless(FloorPlan p)
        {
            int n = 0;
            for (int i = 0; i < p.Rooms.Count; i++) if (!p.Rooms[i].HasDoor) n++;
            return n;
        }

        void Generate()
        {
            FloorSpec spec = new FloorSpec();
            spec.Seed = _seed;
            spec.Width = Mathf.Max(40f, _width);
            spec.Height = Mathf.Max(40f, _height);
            spec.Band = _band;
            spec.Prototype = _prototype;
            spec.MainCorridor = Mathf.Max(1.2f, _mainCorridor);
            spec.SecondaryCorridor = Mathf.Max(1.2f, _secondaryCorridor);

            _plan = FloorGenerator.Generate(spec);
            if (_tex != null) DestroyImmediate(_tex);
            _tex = Render(_plan);
            Repaint();
        }

        void ExportPng()
        {
            if (_tex == null) return;
            string dir = System.IO.Path.Combine(Application.dataPath, "../docs/3d-rebuild/screenshots");
            dir = System.IO.Path.GetFullPath(dir);
            System.IO.Directory.CreateDirectory(dir);
            string path = System.IO.Path.Combine(dir, "floor-lab-" + _seed + ".png");
            System.IO.File.WriteAllBytes(path, _tex.EncodeToPNG());
            Debug.Log("[FloorLab] exported " + path);
        }

        static Color32 ZoneColor(FloorPrototype p)
        {
            switch (p)
            {
                case FloorPrototype.Executive: return new Color32(122, 100, 142, 255);
                case FloorPrototype.Meeting: return new Color32(146, 156, 96, 255);
                case FloorPrototype.SmallOffice: return new Color32(128, 138, 150, 255);
                case FloorPrototype.Renovation: return new Color32(120, 110, 100, 255);
                case FloorPrototype.Utility: return new Color32(92, 92, 98, 255);
                default: return new Color32(104, 148, 176, 255);
            }
        }

        static Texture2D Render(FloorPlan plan, int W = TexW, int H = TexH)
        {
            Color32[] px = new Color32[W * H];
            Color32 bg = new Color32(17, 21, 27, 255);
            for (int i = 0; i < px.Length; i++) px[i] = bg;

            float sx = W / plan.Spec.Width;
            float sy = H / plan.Spec.Height;

            System.Action<RectF, Color32> fill = delegate(RectF r, Color32 c)
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
            };

            System.Action<RectF, Color32> outline = delegate(RectF r, Color32 c)
            {
                int x0 = Mathf.Clamp(Mathf.RoundToInt(r.X * sx), 0, W - 1);
                int x1 = Mathf.Clamp(Mathf.RoundToInt(r.XMax * sx), 0, W - 1);
                int y0 = Mathf.Clamp(Mathf.RoundToInt(r.Y * sy), 0, H - 1);
                int y1 = Mathf.Clamp(Mathf.RoundToInt(r.YMax * sy), 0, H - 1);
                for (int x = x0; x <= x1; x++) { px[y0 * W + x] = c; px[y1 * W + x] = c; }
                for (int y = y0; y <= y1; y++) { px[y * W + x0] = c; px[y * W + x1] = c; }
            };

            for (int i = 0; i < plan.Corridors.Count; i++)
            {
                Corridor c = plan.Corridors[i];
                float thickness = Mathf.Min(c.Rect.W, c.Rect.H);
                Color32 cc = thickness >= 3.0f
                    ? new Color32(92, 102, 116, 255)
                    : thickness >= 2.1f ? new Color32(70, 78, 90, 255) : new Color32(56, 62, 72, 255);
                fill(c.Rect, cc);
            }

            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                Room room = plan.Rooms[i];
                FloorPrototype proto = plan.Zones[room.ZoneIndex].Prototype;
                Color32 zc = ZoneColor(proto);
                Color32 body = new Color32((byte)(zc.r * 0.42f), (byte)(zc.g * 0.42f), (byte)(zc.b * 0.42f), 255);
                fill(room.Rect, body);
                outline(room.Rect, new Color32((byte)Mathf.Min(255f, zc.r * 1.1f), (byte)Mathf.Min(255f, zc.g * 1.1f), (byte)Mathf.Min(255f, zc.b * 1.1f), 255));
            }

            fill(plan.Core, new Color32(44, 50, 59, 255));
            outline(plan.Core, new Color32(214, 222, 232, 255));

            for (int i = 0; i < plan.Stairs.Count; i++)
            {
                fill(plan.Stairs[i], new Color32(30, 35, 42, 255));
                outline(plan.Stairs[i], new Color32(190, 198, 208, 255));
            }

            // doors
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                Room room = plan.Rooms[i];
                if (!room.HasDoor) continue;
                int dx = Mathf.Clamp(Mathf.RoundToInt(room.Door.Center.x * sx), 0, W - 1);
                int dy = Mathf.Clamp(Mathf.RoundToInt(room.Door.Center.y * sy), 0, H - 1);
                for (int oy = -2; oy <= 2; oy++)
                    for (int ox = -2; ox <= 2; ox++)
                    {
                        int xx = dx + ox, yy = dy + oy;
                        if (xx >= 0 && xx < W && yy >= 0 && yy < H) px[yy * W + xx] = new Color32(250, 214, 120, 255);
                    }
            }

            // 房间的额外门（通向走廊的那部分；通向房间的已在 LoopDoors 里）
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                List<Door> extra = plan.Rooms[i].ExtraDoors;
                if (extra == null) continue;
                for (int k = 0; k < extra.Count; k++)
                {
                    if (extra[k].Corridor < 0) continue;
                    int dx = Mathf.Clamp(Mathf.RoundToInt(extra[k].Center.x * sx), 0, W - 1);
                    int dy = Mathf.Clamp(Mathf.RoundToInt(extra[k].Center.y * sy), 0, H - 1);
                    for (int oy = -2; oy <= 2; oy++)
                        for (int ox = -2; ox <= 2; ox++)
                        {
                            int xx = dx + ox, yy = dy + oy;
                            if (xx >= 0 && xx < W && yy >= 0 && yy < H) px[yy * W + xx] = new Color32(250, 180, 96, 255);
                        }
                }
            }

            // loop doors (房间之间的大环)
            for (int i = 0; i < plan.LoopDoors.Count; i++)
            {
                int dx = Mathf.Clamp(Mathf.RoundToInt(plan.LoopDoors[i].Center.x * sx), 0, W - 1);
                int dy = Mathf.Clamp(Mathf.RoundToInt(plan.LoopDoors[i].Center.y * sy), 0, H - 1);
                for (int oy = -2; oy <= 2; oy++)
                    for (int ox = -2; ox <= 2; ox++)
                    {
                        if (Mathf.Abs(ox) + Mathf.Abs(oy) > 2) continue;
                        int xx = dx + ox, yy = dy + oy;
                        if (xx >= 0 && xx < W && yy >= 0 && yy < H) px[yy * W + xx] = new Color32(70, 214, 196, 255);
                    }
            }

            // 走廊掩体：大=琥珀，中=浅蓝灰，小=薄荷绿
            for (int i = 0; i < plan.Props.Count; i++)
            {
                PropPlacement pp = plan.Props[i];
                Color32 pc;
                if (pp.Spec.Name == "隔断墙") pc = new Color32(232, 120, 74, 255);       // 隔断墙：砖红
                else if (pp.Spec.Size == PropSize.Large) pc = new Color32(246, 206, 140, 255);
                else if (pp.Spec.Size == PropSize.Medium) pc = new Color32(170, 198, 224, 255);
                else pc = new Color32(140, 216, 190, 255);
                fill(pp.Rect, pc);
            }

            // extraction markers
            for (int i = 0; i < plan.Extractions.Count; i++)
            {
                Vector2 c = plan.Extractions[i].Center;
                int ex = Mathf.Clamp(Mathf.RoundToInt(c.x * sx), 0, W - 1);
                int ey = Mathf.Clamp(Mathf.RoundToInt(c.y * sy), 0, H - 1);
                for (int oy = -5; oy <= 5; oy++)
                    for (int ox = -5; ox <= 5; ox++)
                    {
                        if (Mathf.Abs(ox) + Mathf.Abs(oy) > 5) continue;
                        int xx = ex + ox, yy = ey + oy;
                        if (xx >= 0 && xx < W && yy >= 0 && yy < H) px[yy * W + xx] = new Color32(232, 168, 74, 255);
                    }
            }

            // spawn markers
            for (int i = 0; i < plan.SpawnZones.Count; i++)
                outline(plan.SpawnZones[i], new Color32(74, 190, 130, 255));

            outline(plan.Bounds, new Color32(226, 234, 244, 255));

            Texture2D tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
