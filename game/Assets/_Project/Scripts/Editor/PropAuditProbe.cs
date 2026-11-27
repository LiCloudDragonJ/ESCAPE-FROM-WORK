using System.Collections.Generic;
using System.IO;
using System.Text;
using EscapeFromWork.LevelGen;
using UnityEditor;
using UnityEngine;

namespace EscapeFromWork.EditorTools
{
    /// <summary>
    /// 临时探针：把 PropCatalog 里每个预制体在原点、无旋转实例化，量渲染包围盒，
    /// 和 Entry.Size 对比，找出"躺平"的件（Y 向高度远小于目录高度）。
    /// </summary>
    public static class PropAuditProbe
    {
        const string CatalogPath = "Assets/_Project/Data/PropCatalog.asset";

        static string DocsDir
        {
            get { return Path.GetDirectoryName(Application.dataPath) + "/../docs/3d-rebuild/"; }
        }

        [MenuItem("Escape From Work/Debug/Prop Orientation Audit")]
        public static void Audit()
        {
            PropCatalog catalog = AssetDatabase.LoadAssetAtPath<PropCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError("[PropAudit] catalog missing: " + CatalogPath);
                return;
            }

            StringBuilder report = new StringBuilder();
            report.AppendLine("name\tprefab\tcatalogSize\tboundsSize\tmodelSize\trootRot\tminY\tchildRotations\tverdict");
            int lying = 0, total = 0, floating = 0;
            foreach (PropCatalog.Entry entry in catalog.Entries)
            {
                if (entry == null || entry.Prefab == null) continue;
                total++;
                GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(entry.Prefab);
                go.transform.position = Vector3.zero;
                go.transform.rotation = Quaternion.identity;

                Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
                bool has = false;
                Bounds b = new Bounds();
                foreach (Renderer r in renderers)
                {
                    if (!has) { b = r.bounds; has = true; }
                    else b.Encapsulate(r.bounds);
                }

                string rots = string.Empty;
                foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                {
                    Vector3 e = t.localEulerAngles;
                    if (Mathf.Abs(Mathf.DeltaAngle(0f, e.x)) > 5f || Mathf.Abs(Mathf.DeltaAngle(0f, e.z)) > 5f)
                        rots += t.name + "(" + e.x.ToString("0") + "," + e.y.ToString("0") + "," + e.z.ToString("0") + ") ";
                }

                Vector3 size = has ? b.size : Vector3.zero;

                // 同一个模型的原始 FBX：用来区分"预制作歪了"还是"源模型就躺着"。
                Vector3 modelSize = Vector3.zero;
                Vector3 modelRootRot = Vector3.zero;
                string fbxPath = "Assets/_Project/Art/Props/" + entry.Prefab.name + ".fbx";
                GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                if (modelAsset != null)
                {
                    GameObject raw = Object.Instantiate(modelAsset);
                    raw.transform.position = Vector3.zero;
                    raw.transform.rotation = Quaternion.identity;
                    modelRootRot = modelAsset.transform.localEulerAngles;
                    Bounds rb = new Bounds();
                    bool rawHas = false;
                    foreach (Renderer r in raw.GetComponentsInChildren<Renderer>(true))
                    {
                        if (!rawHas) { rb = r.bounds; rawHas = true; }
                        else rb.Encapsulate(r.bounds);
                    }
                    if (rawHas) modelSize = rb.size;
                    Object.DestroyImmediate(raw);
                }

                bool flat = entry.Size.y > 0.45f && size.y < entry.Size.y * 0.62f && size.y < 0.5f;
                if (flat) lying++;
                float minY = has ? b.min.y : 0f;
                bool floats = Mathf.Abs(minY) > 0.05f;
                if (floats) floating++;
                report.AppendLine(string.Format("{0}\t{1}\t({2:0.00},{3:0.00},{4:0.00})\t({5:0.00},{6:0.00},{7:0.00})\t({8:0.00},{9:0.00},{10:0.00})\trootRot=({11:0},{12:0},{13:0})\tminY={14:0.00}\t{15}\t{16}",
                    entry.Name, entry.Prefab.name, entry.Size.x, entry.Size.y, entry.Size.z,
                    size.x, size.y, size.z, modelSize.x, modelSize.y, modelSize.z,
                    modelRootRot.x, modelRootRot.y, modelRootRot.z, minY,
                    string.IsNullOrEmpty(rots) ? "-" : rots.Trim(),
                    flat ? "LIES_FLAT" : (floats ? "FLOATS" : "ok")));
                Object.DestroyImmediate(go);
            }

            string outPath = DocsDir + "prop-audit.tsv";
            File.WriteAllText(outPath, report.ToString(), new UTF8Encoding(false));
            Debug.Log("[PropAudit] " + total + " prefabs, " + lying + " look flat, " + floating + " float -> " + outPath);
        }

        /// <summary>把全部预制体按 3/4 视角摆成网格出图，肉眼看朝向/比例/悬浮。</summary>
        [MenuItem("Escape From Work/Debug/Prop Grid Render")]
        public static void GridRender()
        {
            PropCatalog catalog = AssetDatabase.LoadAssetAtPath<PropCatalog>(CatalogPath);
            if (catalog == null) return;

            const int perRow = 10;
            const float spacing = 3.4f;
            var root = new GameObject("PropGrid");
            int index = 0;
            var seen = new HashSet<GameObject>();
            foreach (PropCatalog.Entry entry in catalog.Entries)
            {
                if (entry == null || entry.Prefab == null) continue;
                if (!seen.Add(entry.Prefab)) continue; // 同一模型只摆一次
                float x = (index % perRow) * spacing;
                float z = (index / perRow) * spacing;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(entry.Prefab, root.transform);
                go.transform.position = new Vector3(x, 0f, z);
                go.transform.rotation = Quaternion.identity;
                index++;
            }

            int rows = (index + perRow - 1) / perRow;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.SetParent(root.transform, false);
            ground.transform.position = new Vector3((perRow - 1) * spacing * 0.5f, -0.001f, (rows - 1) * spacing * 0.5f);
            ground.transform.localScale = new Vector3(perRow * spacing * 0.16f, 1f, rows * spacing * 0.16f);
            ground.GetComponent<Renderer>().sharedMaterial = PropMaterialFallback();

            var camGo = new GameObject("Cam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = rows * spacing * 0.62f;
            cam.transform.position = new Vector3((perRow - 1) * spacing * 0.5f + 9f, 14f, (rows - 1) * spacing * 0.5f + 9f);
            cam.transform.LookAt(new Vector3((perRow - 1) * spacing * 0.5f, 0.8f, (rows - 1) * spacing * 0.5f));
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.18f, 0.18f, 0.19f);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(48f, -34f, 0f);
            sun.intensity = 1.3f;

            var rt = new RenderTexture(3840, 2160, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(3840, 2160, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 3840, 2160), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            string outPath = DocsDir + "screenshots/props-grid-34.png";
            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            Debug.Log("[PropAudit] grid with " + index + " props -> " + outPath);

            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(sun.gameObject);
        }

        static Material PropMaterialFallback()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var m = new Material(shader);
            m.color = new Color(0.55f, 0.53f, 0.50f);
            return m;
        }

        /// <summary>重建 M1 场景，把走廊镜头视锥内的道具/灰盒逐个打出来，定位"奇怪形状"是谁。</summary>
        [MenuItem("Escape From Work/Debug/Dump Corridor Props")]
        public static void DumpCorridorProps()
        {
            System.Reflection.MethodInfo buildMats = typeof(M1FloorScene).GetMethod("BuildMaterials",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            System.Reflection.MethodInfo corridorOf = typeof(M1FloorScene).GetMethod("LongestMainCorridor",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            var mats = (FloorMaterials)buildMats.Invoke(null, null);
            FloorSpec spec = M1FloorScene.ConfigureSpec(FloorSpec.Default(M1FloorScene.DefaultSeed));
            FloorPlan plan = FloorGenerator.Generate(spec);

            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.52f, 0.53f, 0.56f);
            FloorGeometry.Build(plan, mats, null, PropCatalog.LoadOrCreateDefault());
            Physics.SyncTransforms();

            RectF corridor = (RectF)corridorOf.Invoke(null, new object[] { plan });
            Vector3 eye = new Vector3(corridor.X + corridor.W * 0.5f, FloorGeometry.EyeHeight, corridor.Y + 1.2f);
            Debug.Log("[Dump] corridor rect=" + corridor.X + "," + corridor.Y + " " + corridor.W + "x" + corridor.H + " eye=" + eye);

            Renderer[] all = Object.FindObjectsOfType<Renderer>();
            foreach (Renderer r in all)
            {
                Vector3 p = r.bounds.center;
                if (Mathf.Abs(p.x - eye.x) > 9f) continue;
                if (p.z < eye.z - 2f || p.z > eye.z + 34f) continue;
                if (r.bounds.size.y < 0.05f) continue;
                Debug.Log("[Dump] " + r.name + " parent=" + r.transform.parent.name +
                          " pos=" + r.bounds.center.ToString("0.00") +
                          " size=" + r.bounds.size.ToString("0.00"));
            }
        }
    }
}
