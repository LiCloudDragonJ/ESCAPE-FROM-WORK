using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using EscapeFromWork.Gameplay;
using EscapeFromWork.LevelGen;

namespace EscapeFromWork.EditorTools
{
    /// Builds FloorTest.unity from the procedural generator: materials, a baked 3D floor,
    /// a playable player at the spawn zone, and renders in-game screenshots for review.
    public static class M1FloorScene
    {
        const string Root = "Assets/_Project";
        const string ScenePath = Root + "/Scenes/FloorTest.unity";
        const string MatDir = Root + "/Materials/Floor";

        public const int DefaultSeed = 1;

        [MenuItem("Escape From Work/M1/Build 3D Floor Scene")]
        public static void Build()
        {
            EnsureFolders();
            FloorMaterials mats = BuildMaterials();
            FloorSpec spec = ConfigureSpec(FloorSpec.Default(DefaultSeed));
            FloorPlan plan = FloorGenerator.Generate(spec);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.70f, 0.71f, 0.75f);
            RenderSettings.fog = false;

            GameObject root = new GameObject("FloorRuntime");
            FloorRuntime runtime = root.AddComponent<FloorRuntime>();
            runtime.Seed = DefaultSeed;
            runtime.Band = spec.Band;
            runtime.Prototype = spec.Prototype;
            runtime.CorridorLevels = spec.CorridorLevels;
            runtime.Materials = mats;
            runtime.BuildGeometry = false;
            runtime.BuildNavMesh = true;

            GameObject geometry = FloorGeometry.Build(plan, mats, root.transform);
            geometry.name = "FloorGeometry";

            Vector3 spawn = SpawnPoint(plan);
            GameObject player = BuildPlayer(spawn);
            EscapeFromWork.UI.MapHud hud = player.AddComponent<EscapeFromWork.UI.MapHud>();
            hud.Initialize(plan, player.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(Root + "/Scenes/Whitebox.unity", true),
            };
            AssetDatabase.SaveAssets();

            Debug.Log("[M1] FloorTest built seed=" + DefaultSeed
                + " rooms=" + plan.Rooms.Count
                + " corridors=" + plan.Corridors.Count
                + " props=" + plan.Props.Count
                + " extractions=" + plan.Extractions.Count
                + " spawn=" + spawn);
        }

        public static FloorSpec ConfigureSpec(FloorSpec spec)
        {
            spec.Band = FloorBand.Tower;
            spec.Prototype = FloorPrototype.OpenOffice;
            spec.Width = 200f;
            spec.Height = 160f;
            spec.CorridorLevels = 3;
            return spec;
        }

        public static Vector3 SpawnPoint(FloorPlan plan)
        {
            Vector2 c = plan.SpawnZones.Count > 0 ? plan.SpawnZones[0].Center : plan.Core.Center;
            return new Vector3(c.x, 0.2f, c.y);
        }

        static GameObject BuildPlayer(Vector3 spawn)
        {
            GameObject player = new GameObject("Player");
            player.transform.position = spawn;

            CharacterController cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.slopeLimit = 45f;
            cc.stepOffset = 0.35f;
            cc.skinWidth = 0.02f;

            FirstPersonController fps = player.AddComponent<FirstPersonController>();
            player.AddComponent<PerfHud>();

            GameObject camObject = new GameObject("PlayerCamera");
            camObject.transform.SetParent(player.transform, false);
            camObject.transform.localPosition = new Vector3(0f, FloorGeometry.EyeHeight, 0f);
            Camera camera = camObject.AddComponent<Camera>();
            camera.fieldOfView = 78f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 320f;
            camera.tag = "MainCamera";
            camObject.AddComponent<AudioListener>();
            fps.BindCamera(camObject.transform);
            return player;
        }

        [MenuItem("Escape From Work/M1/Capture 3D Floor Screenshots")]
        public static void Capture()
        {
            FloorMaterials mats = BuildMaterials();
            FloorSpec spec = ConfigureSpec(FloorSpec.Default(DefaultSeed));
            FloorPlan plan = FloorGenerator.Generate(spec);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.70f, 0.71f, 0.75f);

            FloorGeometry.Build(plan, mats, null);

            GameObject cameraObject = new GameObject("CaptureCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 78f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 320f;

            string dir = System.IO.Path.GetDirectoryName(Application.dataPath) + "/../docs/3d-rebuild/screenshots/";
            System.IO.Directory.CreateDirectory(dir);

            Vector3 spawn = SpawnPoint(plan);
            Vector3 spawnEye = spawn + new Vector3(0f, FloorGeometry.EyeHeight, 0f);
            Shot(camera, spawnEye, PlanTarget(plan, spawn), dir + "m1-floor3d-spawn.png");

            RectF corridor = LongestMainCorridor(plan);
            Vector3 corridorEye = new Vector3(corridor.X + corridor.W * 0.5f, FloorGeometry.EyeHeight, corridor.Y + 1.2f);
            Vector3 corridorTarget = new Vector3(corridor.X + corridor.W * 0.5f, FloorGeometry.EyeHeight - 0.15f, corridor.YMax - 1.2f);
            if (corridor.W > corridor.H) corridorTarget = new Vector3(corridor.XMax - 1.2f, FloorGeometry.EyeHeight - 0.15f, corridor.Y + corridor.H * 0.5f);
            Shot(camera, corridorEye, corridorTarget, dir + "m1-floor3d-corridor.png");

            RectF core = plan.Core;
            Vector3 coreEye = new Vector3(core.X + core.W * 0.5f, FloorGeometry.EyeHeight, core.Y - 8f);
            Shot(camera, coreEye, new Vector3(core.X + core.W * 0.5f, 1.6f, core.YMax), dir + "m1-floor3d-core.png");

            Debug.Log("[M1] screenshots written: spawn / corridor / core");
        }

        [MenuItem("Escape From Work/M1/Capture 3D Floor Geometry Debug")]
        public static void CaptureGeometryDebug()
        {
            EnsureFolders();
            FloorMaterials flat = BuildDebugMaterial();
            FloorSpec spec = ConfigureSpec(FloorSpec.Default(DefaultSeed));
            FloorPlan plan = FloorGenerator.Generate(spec);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.92f, 0.92f, 0.92f);

            GameObject root = FloorGeometry.Build(plan, flat, null);
            int verts = 0;
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>();
            for (int i = 0; i < filters.Length; i++)
            {
                if (filters[i].sharedMesh == null) continue;
                verts += filters[i].sharedMesh.vertexCount;
                Bounds mb = filters[i].sharedMesh.bounds;
                string shader = filters[i].GetComponent<MeshRenderer>().sharedMaterial == null
                    ? "null"
                    : filters[i].GetComponent<MeshRenderer>().sharedMaterial.shader.name;
                Debug.Log("[M1] mesh " + filters[i].name + " verts=" + filters[i].sharedMesh.vertexCount
                    + " tris=" + filters[i].sharedMesh.triangles.Length / 3
                    + " bounds=" + mb.min.ToString("0.0") + ".." + mb.max.ToString("0.0")
                    + " shader=" + shader);
            }
            Debug.Log("[M1] geometry verts total=" + verts + " renderers=" + filters.Length);

            GameObject camObject = new GameObject("DebugCamera");
            Camera camera = camObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 500f;
            camera.fieldOfView = 78f;

            string dir = System.IO.Path.GetDirectoryName(Application.dataPath) + "/../docs/3d-rebuild/screenshots/";
            System.IO.Directory.CreateDirectory(dir);

            Vector3 spawn = SpawnPoint(plan);
            Shot(camera, spawn + new Vector3(0f, FloorGeometry.EyeHeight, 0f), PlanTarget(plan, spawn), dir + "m1-geo-spawn.png");

            RectF core = plan.Core;
            camera.orthographic = true;
            camera.orthographicSize = 45f;
            camera.transform.position = new Vector3(core.Center.x, 90f, core.Y);
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            camera.fieldOfView = 60f;
            Shot(camera, camera.transform.position, camera.transform.position + Vector3.down, dir + "m1-geo-top.png");
            Debug.Log("[M1] geometry debug shots written");
        }

        static FloorMaterials BuildDebugMaterial()
        {
            string path = MatDir + "/DebugGray.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            Color gray = new Color(0.72f, 0.72f, 0.74f);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", gray);
            if (m.HasProperty("_Color")) m.SetColor("_Color", gray);
            EditorUtility.SetDirty(m);

            FloorMaterials mats = new FloorMaterials();
            mats.Wall = m; mats.Floor = m; mats.Ceiling = m; mats.Column = m; mats.Core = m;
            mats.Stair = m; mats.PropLarge = m; mats.PropMedium = m; mats.PropSmall = m;
            mats.SightBreak = m; mats.LightPanel = m; mats.Extraction = m; mats.Spawn = m;
            return mats;
        }

        static Vector3 PlanTarget(FloorPlan plan, Vector3 from)
        {
            Vector2 best = plan.Core.Center;
            float bestDist = float.MaxValue;
            for (int i = 0; i < plan.Corridors.Count; i++)
            {
                RectF r = plan.Corridors[i].Rect;
                if (Mathf.Max(r.W, r.H) < 12f) continue;
                float d = Vector2.Distance(new Vector2(from.x, from.z), r.Center);
                if (d < bestDist) { bestDist = d; best = r.Center; }
            }
            return new Vector3(best.x, FloorGeometry.EyeHeight - 0.2f, best.y);
        }

        static RectF LongestMainCorridor(FloorPlan plan)
        {
            RectF best = plan.Core;
            float bestLen = 0f;
            for (int i = 0; i < plan.Corridors.Count; i++)
            {
                Corridor c = plan.Corridors[i];
                if (Mathf.Min(c.Rect.W, c.Rect.H) < plan.Spec.MainCorridor - 0.01f) continue;
                float len = Mathf.Max(c.Rect.W, c.Rect.H);
                if (len > bestLen) { bestLen = len; best = c.Rect; }
            }
            return best;
        }

        static void Shot(Camera camera, Vector3 eye, Vector3 target, string path)
        {
            camera.transform.position = eye;
            camera.transform.LookAt(target);
            RenderTexture rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Debug.Log("[M1] shot " + path);
        }

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets", "_Project");
            if (!AssetDatabase.IsValidFolder(Root + "/Scenes")) AssetDatabase.CreateFolder(Root, "Scenes");
            if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder(Root + "/Materials", "Floor");
        }

        static FloorMaterials BuildMaterials()
        {
            FloorMaterials mats = new FloorMaterials();
            mats.Wall = Mat("Wall", new Color(0.78f, 0.77f, 0.74f));
            mats.Floor = Mat("Floor", new Color(0.46f, 0.47f, 0.50f));
            mats.Ceiling = Mat("Ceiling", new Color(0.86f, 0.86f, 0.84f));
            mats.Column = Mat("Column", new Color(0.70f, 0.70f, 0.68f));
            mats.Core = Mat("Core", new Color(0.55f, 0.57f, 0.60f));
            mats.Stair = Mat("Stair", new Color(0.38f, 0.40f, 0.43f));
            mats.PropLarge = Mat("PropLarge", new Color(0.55f, 0.42f, 0.30f));
            mats.PropMedium = Mat("PropMedium", new Color(0.62f, 0.55f, 0.46f));
            mats.PropSmall = Mat("PropSmall", new Color(0.45f, 0.55f, 0.52f));
            mats.SightBreak = Mat("SightBreak", new Color(0.72f, 0.50f, 0.38f));
            mats.LightPanel = Emissive("LightPanel", new Color(0.95f, 0.93f, 0.86f), new Color(2.2f, 2.1f, 1.8f));
            mats.Extraction = Emissive("Extraction", new Color(0.95f, 0.65f, 0.25f), new Color(1.1f, 0.55f, 0.15f));
            mats.Spawn = Emissive("Spawn", new Color(0.35f, 0.65f, 0.45f), new Color(0.25f, 0.6f, 0.35f));
            return mats;
        }

        static Material Mat(string name, Color color)
        {
            string path = MatDir + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Emissive(string name, Color color, Color emission)
        {
            Material m = Mat(name, color);
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
