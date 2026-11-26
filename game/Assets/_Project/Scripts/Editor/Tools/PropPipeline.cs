using System.Collections.Generic;
using System.IO;
using System.Linq;
using EscapeFromWork.LevelGen;
using UnityEditor;
using UnityEngine;

namespace EscapeFromWork.EditorTools
{
    /// <summary>
    /// Blender 产出（tools/blender/gen_props.py → Art/Props/*.fbx + prop_manifest.json）到 Unity 的落地：
    /// 设 FBX 导入参数 → 生成 prefab（pivot 底心、单 BoxCollider、静态合批）→ 写进 PropCatalog 资产。
    /// 生成器只按 PropSpec.Name 查目录，所以这一步不改任何 LevelGen 代码。
    /// </summary>
    public static class PropPipeline
    {
        const string ArtDir = "Assets/_Project/Art/Props";
        const string PrefabDir = "Assets/_Project/Prefabs/Props";
        const string CatalogPath = "Assets/_Project/Data/PropCatalog.asset";

        /// <summary>生成器里出现、但 Blender 侧没有独立模型的名字 → 借用最近的模型。</summary>
        static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>
        {
            { "列头柜", "power_cabinet" },
            { "配线架", "pipe_rack" },
            { "管井架", "pipe_rack" },
            { "阀门组", "pipe_rack" },
            // 有了真实模型（R4 第二批 20 个道具）之后，这几个名字直接命中，不用再借：
            // 工具柜 → tool_cart、隔离床 → hospital_bed、货架 → shelf_unit、屏幕墙 / 泵组 也不再借。
            { "工具柜", "tool_cart" },
            { "隔离床", "hospital_bed" },
            { "货堆", "crate_stack" },
            { "洁净工作台", "workbench" },
            { "传递柜", "locker" },
            { "隔间", "partition" },
            { "货架", "shelf_unit" },
            { "办公桌组", "desk" },
            { "文件柜", "locker" },
            { "小纸箱", "crate_stack" },
        };

        class PropMeta
        {
            public string Id;
            public string Name;
            public float Width, Depth, Height;
            public string SizeClass;
            public string[] MaterialSlots;
        }

        [MenuItem("Escape From Work/Props/Import Blender Props")]
        public static void ImportAll()
        {
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { ArtDir });
            var metas = new List<PropMeta>();
            int imported = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".fbx")) continue;

                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer != null)
                {
                    importer.globalScale = 1f;
                    importer.useFileScale = false;
                    importer.materialImportMode = ModelImporterMaterialImportMode.None;
                    importer.importAnimation = false;
                    importer.addCollider = false;
                    importer.importBlendShapes = false;
                    importer.meshCompression = ModelImporterMeshCompression.Off;
                    importer.optimizeMeshPolygons = true;
                    importer.optimizeMeshVertices = true;
                    importer.weldVertices = true;
                    importer.SaveAndReimport();
                }

                string baseName = Path.GetFileNameWithoutExtension(path);
                string id = baseName.StartsWith("P_") ? baseName.Substring(2) : baseName;
                PropMeta meta = ReadMeta(id);
                if (meta == null) continue;
                meta.Id = id;
                metas.Add(meta);

                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model != null)
                {
                    BuildPrefab(model, meta);
                    imported++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            FillCatalog(metas);

            Debug.Log("[Prop] prefabs built: " + imported + " / " + metas.Count);
            foreach (PropMeta m in metas)
                Debug.Log("[Prop] " + m.Id + " '" + m.Name + "' " + m.Width.ToString("0.00") + " x " +
                          m.Depth.ToString("0.00") + " x " + m.Height.ToString("0.00") + " (" + m.SizeClass + ")");
        }

        static PropMeta ReadMeta(string id)
        {
            string json = Path.Combine(ArtDir, "P_" + id + ".json");
            if (!File.Exists(json)) return null;

            string text = File.ReadAllText(json); // JSON 是 UTF-8 写入的，别用默认 ANSI 编码读
            var meta = new PropMeta();
            meta.Name = JsonField(text, "name") ?? id;
            meta.Width = JsonFloat(text, "width");
            meta.Depth = JsonFloat(text, "depth");
            meta.Height = JsonFloat(text, "height");
            meta.SizeClass = JsonField(text, "size_class") ?? "medium";
            meta.MaterialSlots = JsonArray(text, "material_slots");
            return meta;
        }

        static string JsonField(string text, string key)
        {
            int i = text.IndexOf("\"" + key + "\"", System.StringComparison.Ordinal);
            if (i < 0) return null;
            int colon = text.IndexOf(':', i);
            int start = text.IndexOf('"', colon + 1);
            int end = text.IndexOf('"', start + 1);
            if (start < 0 || end < 0) return null;
            return text.Substring(start + 1, end - start - 1);
        }

        static float JsonFloat(string text, string key)
        {
            int i = text.IndexOf("\"" + key + "\"", System.StringComparison.Ordinal);
            if (i < 0) return 0f;
            int colon = text.IndexOf(':', i);
            int end = text.IndexOfAny(new[] { ',', '\n', '}' }, colon + 1);
            string raw = text.Substring(colon + 1, end - colon - 1).Trim();
            float v;
            return float.TryParse(raw, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out v) ? v : 0f;
        }

        static string[] JsonArray(string text, string key)
        {
            int i = text.IndexOf("\"" + key + "\"", System.StringComparison.Ordinal);
            if (i < 0) return new string[0];
            int open = text.IndexOf('[', i);
            int close = text.IndexOf(']', open);
            if (open < 0 || close < 0) return new string[0];
            return text.Substring(open + 1, close - open - 1)
                .Split(',')
                .Select(s => s.Trim().Trim('"'))
                .Where(s => s.Length > 0)
                .ToArray();
        }

        static void BuildPrefab(GameObject model, PropMeta meta)
        {
            EnsureFolder(PrefabDir);
            string prefabPath = PrefabDir + "/P_" + meta.Id + ".prefab";

            GameObject go = Object.Instantiate(model);
            go.name = "P_" + meta.Id;
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            Material body = PropMaterial(SlotFor(meta));
            if (body != null)
            {
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
                {
                    var mats = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++) mats[i] = body;
                    r.sharedMaterials = mats;
                }
            }

            if (go.GetComponentInChildren<Collider>() == null)
            {
                var box = go.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, meta.Height * 0.5f, 0f);
                box.size = new Vector3(meta.Width, meta.Height, meta.Depth);
            }

            // 静态合批：道具不动，标记后由场景统一合批（04-3D 资产规范要求）
            foreach (Transform t in go.GetComponentsInChildren<Transform>())
            {
                GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            }

            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            Debug.Log("[Prop] prefab " + prefabPath + " (collider " + meta.Width.ToString("0.00") + " x " +
                      meta.Height.ToString("0.00") + " x " + meta.Depth.ToString("0.00") + ")");
        }

        static string SlotFor(PropMeta meta)
        {
            if (meta.MaterialSlots != null && meta.MaterialSlots.Length > 0)
            {
                string slot = meta.MaterialSlots[0];
                if (MaterialArtLibrary.TryLoad(slot) != null) return slot;
            }
            if (meta.SizeClass == "large") return "PropLarge";
            if (meta.SizeClass == "small") return "PropSmall";
            return "PropMedium";
        }

        /// <summary>
        /// 道具用"扁平材质"：只保留 albedo + 低光滑度，去掉法线/金属贴图。
        /// 原因：Blender 导出的盒子每个面都是 0–1 UV，法线贴图在平面上一铺就像瓦楞纸，
        /// 近看全是条棱（2026-11-27 道具总览图实测）。道具的形体靠几何与倒角，不靠法线。
        /// </summary>
        static readonly Dictionary<string, Material> FlatMaterials = new Dictionary<string, Material>();

        static Material PropMaterial(string slot)
        {
            Material cached;
            if (FlatMaterials.TryGetValue(slot, out cached)) return cached;

            const string dir = "Assets/_Project/Materials/Props";
            EnsureFolder(dir);
            string path = dir + "/M_Prop_" + slot + ".mat";

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Material source = MaterialArtLibrary.TryLoad(slot);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                mat = new Material(shader);
                if (source != null)
                {
                    Texture albedo = source.GetTexture("_BaseMap");
                    if (albedo != null) mat.SetTexture("_BaseMap", albedo);
                    mat.SetColor("_BaseColor", source.GetColor("_BaseColor"));
                }
                mat.SetFloat("_Smoothness", 0.18f);
                mat.SetFloat("_Metallic", 0f);
                mat.SetFloat("_EnvironmentReflections", 0f);
                mat.enableInstancing = true;
                AssetDatabase.CreateAsset(mat, path);
            }

            FlatMaterials[slot] = mat;
            return mat;
        }

        static void FillCatalog(List<PropMeta> metas)
        {
            EnsureFolder("Assets/_Project/Data");
            var catalog = AssetDatabase.LoadAssetAtPath<PropCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<PropCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.Entries.Clear();
            foreach (PropMeta m in metas)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/P_" + m.Id + ".prefab");
                if (prefab == null) continue;
                catalog.Entries.Add(new PropCatalog.Entry
                {
                    Name = m.Name,
                    Prefab = prefab,
                    Size = new Vector3(m.Width, m.Height, m.Depth),
                    AddBoxCollider = false, // prefab 自带刚体碰撞
                });
            }

            // 别名：生成器里同功能但没单独建模的名字，指向最近的模型
            foreach (var pair in Aliases)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/P_" + pair.Value + ".prefab");
                if (prefab == null) continue;
                PropMeta m = metas.FirstOrDefault(x => x.Id == pair.Value);
                if (m == null) continue;
                catalog.Entries.Add(new PropCatalog.Entry
                {
                    Name = pair.Key,
                    Prefab = prefab,
                    Size = new Vector3(m.Width, m.Height, m.Depth),
                    AddBoxCollider = false,
                });
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("[Prop] catalog entries: " + catalog.Entries.Count + " -> " + CatalogPath);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        /// <summary>把目录里所有道具排成一排拍一张图，用来肉眼核对模型/比例/朝向。</summary>
        [MenuItem("Escape From Work/Props/Capture Prop Showcase")]
        public static void CaptureShowcase()
        {
            var prefabs = new List<GameObject>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null) prefabs.Add(go);
            }
            prefabs.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));

            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "ShowcaseFloor";
            floor.transform.localScale = new Vector3(6f, 1f, 3f);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.6f;
            sun.color = new Color(1f, 0.97f, 0.92f);
            sun.transform.rotation = Quaternion.Euler(48f, -34f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.44f, 0.48f);

            const int perRow = 7;
            const float spacing = 2.6f;
            for (int i = 0; i < prefabs.Count; i++)
            {
                int row = i / perRow, col = i % perRow;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i]);
                go.transform.position = new Vector3((col - (perRow - 1) * 0.5f) * spacing, 0f, row * spacing * -1f);
                go.transform.rotation = Quaternion.identity;
            }

            var camGo = new GameObject("ShowcaseCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.08f, 0.10f);
            cam.fieldOfView = 42f;
            int rows = Mathf.CeilToInt(prefabs.Count / (float)perRow);
            float width = perRow * spacing;
            float height = Mathf.Max(4f, rows * spacing);
            cam.transform.position = new Vector3(0f, width * 0.62f, -height * 0.55f - 3f);
            cam.transform.LookAt(new Vector3(0f, 0.9f, -height * 0.4f));

            // Unity 批处理的当前目录是工程目录（game/），出图路径要显式指到仓库 docs/ 下
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../docs/3d-rebuild/screenshots"));
            Directory.CreateDirectory(outDir);
            const int w = 1920, h = 1080;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            File.WriteAllBytes(outDir + "/props-showcase.png", tex.EncodeToPNG());
            Debug.Log("[Prop] showcase written to " + outDir + "/props-showcase.png (" + prefabs.Count + " prefabs)");
        }
    }
}
