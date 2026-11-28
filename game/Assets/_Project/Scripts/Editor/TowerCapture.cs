using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using EscapeFromWork.LevelGen;

namespace EscapeFromWork.EditorTools
{
    /// <summary>
    /// T3 evidence shots: the four raid floors stacked at 4.2 m spacing, one view per floor plus a
    /// cut-away exterior of the whole tower. Menu: Escape From Work / T3 / Capture Tower Screenshots.
    /// </summary>
    public static class TowerCapture
    {
        const string OutDir = "docs/3d-rebuild/screenshots";
        const int Width = 1920;
        const int Height = 1080;

        [MenuItem("Escape From Work/T3/Capture Tower Screenshots")]
        public static void Capture()
        {
            FloorMaterials mats = M1FloorScene.BuildMaterials();
            PropCatalog catalog = PropCatalog.LoadOrCreateDefault();
            RaidPlan raid = SlicePlanner.Generate(1);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.52f, 0.53f, 0.56f, 1f);
            RenderSettings.skybox = null;

            GameObject tower = new GameObject("Tower");
            GameObject lightGo = new GameObject("CaptureSun");
            Light sun = lightGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 0.9f;
            sun.color = new Color(1f, 0.96f, 0.9f);
            sun.transform.rotation = Quaternion.Euler(52f, -34f, 0f);

            GameObject[] holders = new GameObject[raid.FloorCount];
            GameObject[][] hides = new GameObject[raid.FloorCount][];
            for (int i = 0; i < raid.FloorCount; i++)
            {
                GameObject holder = new GameObject("Tower_F" + i);
                holder.transform.SetParent(tower.transform, false);
                holder.transform.localPosition = new Vector3(0f, i * FloorGeometry.FloorToFloor, 0f);
                FloorGeometry.Build(raid.Floors[i].Plan, mats, holder.transform, catalog);
                holders[i] = holder;

                Transform floorRoot = holder.transform.childCount > 0 ? holder.transform.GetChild(0) : null;
                List<GameObject> hidden = new List<GameObject>();
                if (floorRoot != null)
                {
                    Transform ceiling = floorRoot.Find("Ceiling");
                    Transform panels = floorRoot.Find("LightPanels");
                    if (ceiling != null) hidden.Add(ceiling.gameObject);
                    if (panels != null) hidden.Add(panels.gameObject);
                }
                hides[i] = hidden.ToArray();
            }

            Camera camera = CreateCamera();
            Directory.CreateDirectory(OutDir);

            // Each floor gets a cut-away aerial: only that floor is visible and its ceiling is hidden,
            // so the raid floors can be compared side by side (room mix, facility floors, signage).
            for (int i = 0; i < raid.FloorCount; i++)
            {
                for (int j = 0; j < raid.FloorCount; j++) holders[j].SetActive(j == i);
                for (int k = 0; k < hides[i].Length; k++) hides[i][k].SetActive(false);

                float w = raid.Floors[i].Plan.Bounds.W;
                float h = raid.Floors[i].Plan.Bounds.H;
                float level = i * FloorGeometry.FloorToFloor;
                Vector3 look = new Vector3(w * 0.5f, level, h * 0.5f);
                Vector3 eye = new Vector3(w * 0.5f - w * 0.62f,
                    level + Mathf.Max(36f, w * 0.26f), h * 0.5f - h * 0.62f);
                camera.fieldOfView = 50f;
                Shot(camera, eye, look, Path.Combine(OutDir, "tower-f" + (i + 1) + ".png"));

                for (int k = 0; k < hides[i].Length; k++) hides[i][k].SetActive(true);
            }

            for (int j = 0; j < raid.FloorCount; j++) holders[j].SetActive(true);

            // Exploded stack: pull the four floors apart so the raid reads as four separate plates
            // instead of one pancake (the real tower is 200 x 160 x 16.8 m, i.e. genuinely flat).
            for (int j = 0; j < raid.FloorCount; j++)
                holders[j].transform.localPosition = new Vector3(j * 9f, j * (FloorGeometry.FloorToFloor + 5f), j * 9f);

            float cx = raid.Floors[0].Plan.Bounds.W * 0.5f;
            float cz = raid.Floors[0].Plan.Bounds.H * 0.5f;
            float bw = raid.Floors[0].Plan.Bounds.W;
            float bh = raid.Floors[0].Plan.Bounds.H;
            camera.fieldOfView = 44f;
            Shot(camera, new Vector3(cx - bw * 0.72f, 46f, cz - bh * 0.72f),
                new Vector3(cx + 9f, 18f, cz + 9f), Path.Combine(OutDir, "tower-stack.png"));

            for (int j = 0; j < raid.FloorCount; j++)
                holders[j].transform.localPosition = new Vector3(0f, j * FloorGeometry.FloorToFloor, 0f);

            Debug.Log("[Tower] screenshots written: f1..f4 + stack -> " + OutDir);
        }

        /// <summary>
        /// Top-down orthographic plan of one floor with only the wall meshes visible. Used to audit
        /// wall sharing: every wall body is 0.4 m thick, so a slot wider than that between two
        /// parallel walls is a real gap, not two faces of the same wall.
        /// Menu: Escape From Work / T3 / Capture Wall Plan.
        /// </summary>
        [MenuItem("Escape From Work/T3/Capture Wall Plan")]
        public static void CapturePlan()
        {
            FloorMaterials mats = M1FloorScene.BuildMaterials();
            PropCatalog catalog = PropCatalog.LoadOrCreateDefault();
            RaidPlan raid = SlicePlanner.Generate(1);
            int floor = 2;
            FloorPlan plan = raid.Floors[floor].Plan;

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.78f, 0.78f, 0.80f, 1f);
            RenderSettings.skybox = null;

            GameObject root = new GameObject("Plan");
            FloorGeometry.Build(plan, mats, root.transform, catalog);

            Transform floorRoot = root.transform.childCount > 0 ? root.transform.GetChild(0) : null;
            if (floorRoot != null)
            {
                Material solidMat = new Material(Shader.Find("Unlit/Color"));
                solidMat.color = new Color(0.85f, 0.25f, 0.2f, 1f);
                Material floorMat = new Material(Shader.Find("Unlit/Color"));
                floorMat.color = new Color(0.2f, 0.35f, 0.65f, 1f);
                Material wallMat = new Material(Shader.Find("Unlit/Color"));
                wallMat.color = new Color(0.95f, 0.95f, 0.93f, 1f);
                for (int i = 0; i < floorRoot.childCount; i++)
                {
                    Transform child = floorRoot.GetChild(i);
                    string n = child.name;
                    Renderer[] renderers = child.GetComponentsInChildren<Renderer>(true);
                    if (n == "Walls")
                    {
                        for (int r = 0; r < renderers.Length; r++) renderers[r].sharedMaterial = wallMat;
                    }
                    else if (n == "SolidMass")
                    {
                        for (int r = 0; r < renderers.Length; r++) renderers[r].sharedMaterial = solidMat;
                    }
                    else if (n == "Floor")
                    {
                        for (int r = 0; r < renderers.Length; r++) renderers[r].sharedMaterial = floorMat;
                    }
                    else
                    {
                        child.gameObject.SetActive(false);
                    }
                }
            }

            GameObject lightGo = new GameObject("CaptureSun");
            Light sun = lightGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.color = Color.white;
            sun.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            GameObject cameraObject = new GameObject("PlanCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(plan.Bounds.W, plan.Bounds.H) * 0.52f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 200f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.08f, 0.09f, 1f);
            camera.aspect = 1f;

            Vector3 eye = new Vector3(plan.Bounds.W * 0.5f, 60f, plan.Bounds.H * 0.5f);
            camera.transform.position = eye;
            camera.transform.LookAt(new Vector3(plan.Bounds.W * 0.5f, 0f, plan.Bounds.H * 0.5f));

            const int size = 2400;
            RenderTexture rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            tex.Apply();
            Directory.CreateDirectory(OutDir);
            string path = Path.Combine(OutDir, "tower-plan-f" + (floor + 1) + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Debug.Log("[Tower] plan shot " + path);
        }

        static Camera CreateCamera()
        {
            GameObject cameraObject = new GameObject("CaptureCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 70f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 420f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.05f, 0.06f, 1f);
            return camera;
        }

        static void Shot(Camera camera, Vector3 eye, Vector3 target, string path)
        {
            camera.transform.position = eye;
            camera.transform.LookAt(target);
            camera.aspect = Width / (float)Height;

            RenderTexture rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Debug.Log("[Tower] shot " + path);
        }
    }
}
