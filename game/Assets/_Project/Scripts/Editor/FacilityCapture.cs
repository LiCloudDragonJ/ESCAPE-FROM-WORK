using System.Collections.Generic;
using System.IO;
using System.Reflection;
using EscapeFromWork.LevelGen;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace EscapeFromWork.EditorTools
{
    /// <summary>
    /// 按设施类型各拍一张房间内景，用来检查"功能分区"是否真的在视觉上分开了：
    /// 每种 FacilityKind 挑面积最大的一间房，从房间靠近核心筒的角上看进去。
    /// </summary>
    public static class FacilityCapture
    {
        const int Width = 1920;
        const int Height = 1080;

        static string OutDir
        {
            get
            {
                return Path.GetDirectoryName(Application.dataPath) + "/../docs/3d-rebuild/screenshots/";
            }
        }

        [MenuItem("Escape From Work/M1/Capture Facility Rooms")]
        public static void Capture()
        {
            FloorMaterials mats = BuildMaterials();
            FloorSpec spec = M1FloorScene.ConfigureSpec(FloorSpec.Default(M1FloorScene.DefaultSeed));
            FloorPlan plan = FloorGenerator.Generate(spec);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.52f, 0.53f, 0.56f);

            FloorGeometry.Build(plan, mats, null, PropCatalog.LoadOrCreateDefault());

            GameObject cameraObject = new GameObject("FacilityCaptureCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 78f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 320f;

            Directory.CreateDirectory(OutDir);
            Physics.SyncTransforms();

            // 每种设施挑一间最大的（太小的房间视角摆不开，跳过）
            Dictionary<FacilityKind, Room> picks = new Dictionary<FacilityKind, Room>();
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                Room room = plan.Rooms[i];
                if (room.Rect.Area < 36f) continue;
                if (!picks.ContainsKey(room.Facility) || picks[room.Facility].Rect.Area < room.Rect.Area)
                    picks[room.Facility] = room;
            }

            List<FacilityKind> kinds = new List<FacilityKind>(picks.Keys);
            kinds.Sort((a, b) => string.CompareOrdinal(a.ToString(), b.ToString()));

            int shots = 0;
            for (int i = 0; i < kinds.Count; i++)
            {
                FacilityKind kind = kinds[i];
                RectF rect = picks[kind].Rect;
                Vector2 center = rect.Center;

                // 从房间离核心筒最近的那一侧看进去，尽量把房间进深拍全
                Vector2 toCore = center - plan.Core.Center;
                if (toCore.sqrMagnitude < 0.01f) toCore = Vector2.right;
                toCore.Normalize();

                float halfX = Mathf.Max(0.5f, rect.W * 0.5f - 1.4f);
                float halfZ = Mathf.Max(0.5f, rect.H * 0.5f - 1.4f);
                Vector3 eye = new Vector3(center.x + toCore.x * halfX * 0.92f, FloorGeometry.EyeHeight,
                    center.y + toCore.y * halfZ * 0.92f);
                Vector3 target = new Vector3(center.x - toCore.x * rect.W * 0.25f, 1.1f,
                    center.y - toCore.y * rect.H * 0.25f);

                string path = OutDir + "facility-" + kind.ToString().ToLowerInvariant() + ".png";
                Shoot(camera, eye, target, path);
                shots++;
                Debug.Log("[Facility] " + kind + " area=" + rect.Area.ToString("0") + " rect=" + rect
                    + " floor=" + FacilityGrammar.Get(kind).FloorMaterial
                    + " wall=" + FacilityGrammar.Get(kind).WallMaterial
                    + " -> " + path);
            }

            Debug.Log("[Facility] shots written: " + shots + " / " + kinds.Count);
        }

        static FloorMaterials BuildMaterials()
        {
            MethodInfo method = typeof(M1FloorScene).GetMethod("BuildMaterials",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            return (FloorMaterials)method.Invoke(null, null);
        }

        static void Shoot(Camera camera, Vector3 eye, Vector3 target, string path)
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
        }
    }
}
