using System.Collections.Generic;
using UnityEngine;

namespace EscapeFromWork.LevelGen
{
    /// <summary>Builds wall signs from placement data; generated resources die with their sign.</summary>
    public static class SignageRenderer
    {
        const int MapWidth = 512;
        const int MapHeight = 384;

        public static GameObject Build(FloorPlan plan, IEnumerable<SignPlacement> signs, Transform parent)
        {
            GameObject root = new GameObject("Signage");
            root.transform.SetParent(parent, false);
            if (signs == null) return root;

            int index = 0;
            foreach (SignPlacement sign in signs) BuildSign(plan, sign, root.transform, index++);
            return root;
        }

        static void BuildSign(FloorPlan plan, SignPlacement sign, Transform parent, int index)
        {
            Vector2 size = new Vector2(
                Mathf.Max(0.001f, Mathf.Abs(sign.Size.x)),
                Mathf.Max(0.001f, Mathf.Abs(sign.Size.y)));

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Sign_" + index + "_" + sign.Kind;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(sign.Center.x, sign.Elevation, sign.Center.y);
            go.transform.localRotation = FaceNormal(sign.Normal);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            Collider collider = go.GetComponent<Collider>();
            if (collider != null) DestroyOwned(collider);

            SignageCleanup cleanup = go.AddComponent<SignageCleanup>();
            Texture2D shared = sign.Kind == SignKind.Evacuation ? null : LoadStatic(sign.TexturePath);
            Texture2D generated = null;
            Texture2D surface = null;

            if (sign.Kind == SignKind.Evacuation)
            {
                generated = BuildEvacuationMap(plan, sign);
                surface = generated;
            }
            else if (sign.Kind == SignKind.Floor || sign.Kind == SignKind.Room)
            {
                // The artwork contains placeholder numbers. Copy it, erase those numbers, and
                // draw the real value with TextMesh; the imported/shared texture stays untouched.
                generated = CreateNumberedPlate(shared, sign.Kind);
                surface = generated;
            }
            else
            {
                surface = shared;
                if (surface == null)
                {
                    generated = CreateFallbackPlate(sign.Kind);
                    surface = generated;
                }
            }

            // A packaged player may strip the built-in Unlit/Transparent shader; the editor keeps a
            // Resources material so sign quads still render after a build (see SignageResources).
            Material baseMaterial = Resources.Load<Material>("SignageUnlit");
            Material material;
            if (baseMaterial != null)
            {
                material = new Material(baseMaterial);
            }
            else
            {
                Shader shader = Shader.Find("Unlit/Transparent");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader == null) throw new System.InvalidOperationException("Signage requires the Unlit/Transparent shader.");
                material = new Material(shader);
            }
            material.name = "Signage_" + index + "_" + sign.Kind;
            material.mainTexture = surface;
            cleanup.Initialize(material, generated);

            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            if (sign.Kind == SignKind.Floor)
            {
                string value = plan != null && plan.Spec != null
                    ? plan.Spec.WorldFloor + "F"
                    : (string.IsNullOrEmpty(sign.Text) ? "F?" : sign.Text);
                AddLabel(go.transform, "FloorNumber", value, size,
                    new Vector2(0f, 0.065f), 0.80f, 0.40f, new Color32(25, 29, 33, 255));
            }
            else if (sign.Kind == SignKind.Room)
            {
                string value;
                if (plan != null && plan.Spec != null && sign.RoomIndex >= 0)
                    value = (plan.Spec.WorldFloor * 100 + sign.RoomIndex + 1).ToString("0000");
                else
                    value = string.IsNullOrEmpty(sign.Text) ? "ROOM" : sign.Text;
                AddLabel(go.transform, "RoomNumber", value, size,
                    new Vector2(-0.205f, 0.025f), 0.49f, 0.46f, new Color32(29, 32, 32, 255));
            }
            else if (sign.Kind == SignKind.Evacuation)
            {
                string value = plan != null && plan.Spec != null
                    ? "YOU ARE HERE · " + plan.Spec.WorldFloor + "F"
                    : (string.IsNullOrEmpty(sign.Text) ? "YOU ARE HERE" : sign.Text);
                AddLabel(go.transform, "EvacuationCaption", value, size,
                    new Vector2(0f, -0.425f), 0.88f, 0.105f, new Color32(240, 247, 235, 255));
            }
            else if (shared == null)
            {
                string value = string.IsNullOrEmpty(sign.Text) ? sign.Kind.ToString().ToUpperInvariant() : sign.Text;
                AddLabel(go.transform, "FallbackText", value, size,
                    Vector2.zero, 0.82f, 0.22f, new Color32(238, 241, 231, 255));
            }
        }

        static Quaternion FaceNormal(Vector2 normal)
        {
            if (normal.sqrMagnitude < 0.000001f) normal = Vector2.down;
            normal.Normalize();
            // A Unity Quad faces local -Z, so local +Z must point opposite the plan normal.
            return Quaternion.LookRotation(new Vector3(-normal.x, 0f, -normal.y), Vector3.up);
        }

        static Texture2D LoadStatic(string texturePath)
        {
            if (string.IsNullOrEmpty(texturePath)) return null;
            string path = texturePath.Trim().TrimStart('/');
            if (path.Length == 0) return null;
#if UNITY_EDITOR
            Texture2D editorTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/_Project/Art/Signage/" + path + ".png");
            if (editorTexture != null) return editorTexture;
#endif
            return Resources.Load<Texture2D>("Signage/" + path);
        }

        static Texture2D CreateNumberedPlate(Texture2D source, SignKind kind)
        {
            Texture2D copy = CopyTexture(source);
            if (copy == null) return CreateFallbackPlate(kind);
            copy.name = "Signage_" + kind + "_Dynamic";
            if (kind == SignKind.Floor)
            {
                // Erase 00F while retaining the border and EFW TOWER branding.
                FillPixelRect(copy, 34, 184, 478, 385, new Color32(151, 158, 164, 255));
            }
            else
            {
                // Erase 5001 while retaining the border, heading and ADMIN label.
                FillPixelRect(copy, 28, 88, 282, 188, new Color32(239, 240, 234, 255));
            }
            copy.Apply(false, false);
            return copy;
        }

        static Texture2D CopyTexture(Texture2D source)
        {
            if (source == null) return null;
            Texture2D copy = null;
            RenderTexture temporary = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                copy = NewTexture(source.width, source.height);
                if (source.isReadable)
                {
                    copy.SetPixels(source.GetPixels());
                }
                else
                {
                    temporary = RenderTexture.GetTemporary(source.width, source.height, 0,
                        RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                    Graphics.Blit(source, temporary);
                    RenderTexture.active = temporary;
                    copy.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0);
                }
                copy.Apply(false, false);
                return copy;
            }
            catch (System.Exception)
            {
                if (copy != null) DestroyOwned(copy);
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                if (temporary != null) RenderTexture.ReleaseTemporary(temporary);
            }
        }

        static Texture2D CreateFallbackPlate(SignKind kind)
        {
            int width = kind == SignKind.Room ? 256 : 256;
            int height = kind == SignKind.Room ? 128 : 256;
            Texture2D texture = NewTexture(width, height);
            Color32 background = kind == SignKind.Floor
                ? new Color32(151, 158, 164, 255)
                : kind == SignKind.Room
                    ? new Color32(239, 240, 234, 255)
                    : new Color32(44, 53, 57, 255);
            Color32 border = kind == SignKind.Floor || kind == SignKind.Room
                ? new Color32(218, 226, 218, 255)
                : new Color32(229, 235, 224, 255);
            Clear(texture, background);
            DrawPixelBorder(texture, 5, 5, width - 10, height - 10, 2, border);
            texture.name = "Signage_" + kind + "_Fallback";
            texture.Apply(false, false);
            return texture;
        }

        static Texture2D BuildEvacuationMap(FloorPlan plan, SignPlacement sign)
        {
            Texture2D texture = NewTexture(MapWidth, MapHeight);
            texture.name = "Signage_Evacuation_RuntimeMap";
            Clear(texture, new Color32(24, 55, 47, 255));
            DrawPixelBorder(texture, 5, 5, MapWidth - 10, MapHeight - 10, 3,
                new Color32(211, 232, 207, 255));
            DrawPixelRect(texture, 12, 12, MapWidth - 24, 39,
                new Color32(35, 82, 65, 255));

            RectF pixelArea = new RectF(22f, 58f, MapWidth - 44f, MapHeight - 80f);
            DrawPixelBorder(texture, Mathf.RoundToInt(pixelArea.X), Mathf.RoundToInt(pixelArea.Y),
                Mathf.RoundToInt(pixelArea.W), Mathf.RoundToInt(pixelArea.H), 1,
                new Color32(83, 126, 103, 255));

            if (plan != null)
            {
                RectF world = plan.Bounds;
                world = new RectF(world.X, world.Y, Mathf.Max(0.001f, world.W), Mathf.Max(0.001f, world.H));

                if (plan.Corridors != null)
                {
                    for (int i = 0; i < plan.Corridors.Count; i++)
                    {
                        Corridor corridor = plan.Corridors[i];
                        Color32 color = corridor.Main
                            ? new Color32(116, 184, 145, 255)
                            : new Color32(143, 195, 163, 255);
                        FillWorldRect(texture, corridor.Rect, world, pixelArea, color);
                        DrawWorldRect(texture, corridor.Rect, world, pixelArea,
                            new Color32(45, 92, 69, 255), 1);
                    }
                }

                if (plan.Rooms != null)
                {
                    for (int i = 0; i < plan.Rooms.Count; i++)
                    {
                        Room room = plan.Rooms[i];
                        if (room.Outline != null && room.Outline.Length >= 3)
                        {
                            Vector2[] polygon = new Vector2[room.Outline.Length];
                            for (int p = 0; p < polygon.Length; p++)
                                polygon[p] = MapPoint(room.Outline[p], world, pixelArea);
                            FillPolygon(texture, polygon, new Color32(218, 229, 211, 255));
                            for (int p = 0; p < polygon.Length; p++)
                                DrawLine(texture, polygon[p], polygon[(p + 1) % polygon.Length],
                                    new Color32(48, 81, 65, 255), 1);
                        }
                        else
                        {
                            FillWorldRect(texture, room.Rect, world, pixelArea,
                                new Color32(218, 229, 211, 255));
                            DrawWorldRect(texture, room.Rect, world, pixelArea,
                                new Color32(48, 81, 65, 255), 1);
                        }
                    }
                }

                Vector2 anchor = MapPoint(sign.MapAnchor, world, pixelArea);
                FillCircle(texture, Mathf.RoundToInt(anchor.x), Mathf.RoundToInt(anchor.y), 9,
                    new Color32(247, 247, 230, 255));
                FillCircle(texture, Mathf.RoundToInt(anchor.x), Mathf.RoundToInt(anchor.y), 6,
                    new Color32(220, 45, 48, 255));
            }

            texture.Apply(false, false);
            return texture;
        }

        static Texture2D NewTexture(int width, int height)
        {
            Texture2D texture = new Texture2D(Mathf.Max(1, width), Mathf.Max(1, height),
                TextureFormat.RGBA32, false, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            return texture;
        }

        static void AddLabel(Transform sign, string name, string value, Vector2 signSize,
            Vector2 normalizedPosition, float widthFraction, float heightFraction, Color32 color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(sign, false);
            go.transform.localPosition = new Vector3(normalizedPosition.x, normalizedPosition.y, -0.008f);
            // Cancel the Quad's non-uniform scale so text is sized in metres, not stretched.
            go.transform.localScale = new Vector3(1f / signSize.x, 1f / signSize.y, 1f);

            TextMesh text = go.AddComponent<TextMesh>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 72;
            text.characterSize = 1f;
            text.fontStyle = FontStyle.Bold;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.richText = false;
            text.color = color;
            text.text = value;

            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }

            float maxWidth = signSize.x * widthFraction;
            float maxHeight = signSize.y * heightFraction;
            MeshFilter filter = go.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
            {
                Vector3 raw = filter.sharedMesh.bounds.size;
                if (raw.x > 0.00001f && raw.y > 0.00001f)
                    text.characterSize = Mathf.Min(maxWidth / raw.x, maxHeight / raw.y);
                else
                    text.characterSize = maxHeight / text.fontSize;
            }
            else
            {
                text.characterSize = maxHeight / text.fontSize;
            }
        }

        static Vector2 MapPoint(Vector2 point, RectF world, RectF pixels)
        {
            float x = Mathf.Clamp01((point.x - world.X) / world.W);
            float y = Mathf.Clamp01((point.y - world.Y) / world.H);
            return new Vector2(pixels.X + x * pixels.W, pixels.Y + y * pixels.H);
        }

        static void FillWorldRect(Texture2D texture, RectF rect, RectF world, RectF area, Color32 color)
        {
            Vector2 a = MapPoint(new Vector2(rect.X, rect.Y), world, area);
            Vector2 b = MapPoint(new Vector2(rect.XMax, rect.YMax), world, area);
            FillPixelRect(texture, Mathf.FloorToInt(Mathf.Min(a.x, b.x)), Mathf.FloorToInt(Mathf.Min(a.y, b.y)),
                Mathf.CeilToInt(Mathf.Max(a.x, b.x)), Mathf.CeilToInt(Mathf.Max(a.y, b.y)), color);
        }

        static void DrawWorldRect(Texture2D texture, RectF rect, RectF world, RectF area, Color32 color, int thickness)
        {
            Vector2 a = MapPoint(new Vector2(rect.X, rect.Y), world, area);
            Vector2 b = MapPoint(new Vector2(rect.XMax, rect.Y), world, area);
            Vector2 c = MapPoint(new Vector2(rect.XMax, rect.YMax), world, area);
            Vector2 d = MapPoint(new Vector2(rect.X, rect.YMax), world, area);
            DrawLine(texture, a, b, color, thickness);
            DrawLine(texture, b, c, color, thickness);
            DrawLine(texture, c, d, color, thickness);
            DrawLine(texture, d, a, color, thickness);
        }

        static void Clear(Texture2D texture, Color32 color)
        {
            Color32[] pixels = new Color32[texture.width * texture.height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            texture.SetPixels32(pixels);
        }

        static void FillPixelRect(Texture2D texture, int x0, int y0, int x1, int y1, Color32 color)
        {
            x0 = Mathf.Clamp(Mathf.Min(x0, x1), 0, texture.width);
            x1 = Mathf.Clamp(Mathf.Max(x0, x1), 0, texture.width);
            y0 = Mathf.Clamp(Mathf.Min(y0, y1), 0, texture.height);
            y1 = Mathf.Clamp(Mathf.Max(y0, y1), 0, texture.height);
            for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++) texture.SetPixel(x, y, color);
        }

        static void DrawPixelRect(Texture2D texture, int x, int y, int width, int height, Color32 color)
        {
            FillPixelRect(texture, x, y, x + width, y + height, color);
        }

        static void DrawPixelBorder(Texture2D texture, int x, int y, int width, int height, int thickness, Color32 color)
        {
            FillPixelRect(texture, x, y, x + width, y + thickness, color);
            FillPixelRect(texture, x, y + height - thickness, x + width, y + height, color);
            FillPixelRect(texture, x, y, x + thickness, y + height, color);
            FillPixelRect(texture, x + width - thickness, y, x + width, y + height, color);
        }

        static void FillPolygon(Texture2D texture, Vector2[] points, Color32 color)
        {
            float min = points[0].y, max = points[0].y;
            for (int i = 1; i < points.Length; i++)
            {
                min = Mathf.Min(min, points[i].y);
                max = Mathf.Max(max, points[i].y);
            }
            int y0 = Mathf.Clamp(Mathf.FloorToInt(min), 0, texture.height - 1);
            int y1 = Mathf.Clamp(Mathf.CeilToInt(max), 0, texture.height - 1);
            List<float> intersections = new List<float>();
            for (int y = y0; y <= y1; y++)
            {
                float scanY = y + 0.5f;
                intersections.Clear();
                for (int i = 0; i < points.Length; i++)
                {
                    Vector2 a = points[i], b = points[(i + 1) % points.Length];
                    if ((a.y <= scanY && b.y > scanY) || (b.y <= scanY && a.y > scanY))
                        intersections.Add(a.x + (scanY - a.y) / (b.y - a.y) * (b.x - a.x));
                }
                intersections.Sort();
                for (int i = 0; i + 1 < intersections.Count; i += 2)
                    FillPixelRect(texture, Mathf.CeilToInt(intersections[i]), y,
                        Mathf.FloorToInt(intersections[i + 1]) + 1, y + 1, color);
            }
        }

        static void DrawLine(Texture2D texture, Vector2 a, Vector2 b, Color32 color, int thickness)
        {
            int x0 = Mathf.RoundToInt(a.x), y0 = Mathf.RoundToInt(a.y);
            int x1 = Mathf.RoundToInt(b.x), y1 = Mathf.RoundToInt(b.y);
            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int error = dx + dy;
            while (true)
            {
                FillCircle(texture, x0, y0, Mathf.Max(0, thickness - 1), color);
                if (x0 == x1 && y0 == y1) break;
                int twice = 2 * error;
                if (twice >= dy) { error += dy; x0 += sx; }
                if (twice <= dx) { error += dx; y0 += sy; }
            }
        }

        static void FillCircle(Texture2D texture, int cx, int cy, int radius, Color32 color)
        {
            if (radius <= 0)
            {
                if (cx >= 0 && cx < texture.width && cy >= 0 && cy < texture.height) texture.SetPixel(cx, cy, color);
                return;
            }
            int rr = radius * radius;
            for (int y = -radius; y <= radius; y++)
            for (int x = -radius; x <= radius; x++)
            {
                if (x * x + y * y > rr) continue;
                int px = cx + x, py = cy + y;
                if (px >= 0 && px < texture.width && py >= 0 && py < texture.height) texture.SetPixel(px, py, color);
            }
        }

        internal static void DestroyOwned(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Object.Destroy(obj);
            else Object.DestroyImmediate(obj);
        }
    }

    /// <summary>Owns only per-sign resources; imported and Resources textures are never stored here.</summary>
    internal sealed class SignageCleanup : MonoBehaviour
    {
        Material _material;
        Texture2D _generatedTexture;

        internal void Initialize(Material material, Texture2D generatedTexture)
        {
            _material = material;
            _generatedTexture = generatedTexture;
        }

        void OnDestroy()
        {
            SignageRenderer.DestroyOwned(_material);
            SignageRenderer.DestroyOwned(_generatedTexture);
            _material = null;
            _generatedTexture = null;
        }
    }
}
