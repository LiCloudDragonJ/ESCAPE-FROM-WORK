using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using EscapeFromWork.Gameplay;

namespace EscapeFromWork.EditorTools
{
    /// Builds the M0 whitebox scene: one enclosed office floor slice with slab,
    /// suspended ceiling, light panels, stairs, ramp, crouch beam and player.
    public static class M0Bootstrap
    {
        const string Root = "Assets/_Project";
        const string ScenePath = Root + "/Scenes/Whitebox.unity";

        // Floor-to-floor 4.2 m: slab 0.25 m on top, suspended ceiling at 3.37 m.
        const float FloorToFloor = 4.2f;
        const float CeilingY = 3.37f;

        [MenuItem("Escape From Work/M0/Build Whitebox Scene")]
        public static void Build()
        {
            EnsureFolders();

            Material floor = MakeMaterial("WB_Floor", new Color(0.52f, 0.53f, 0.55f));
            Material wall = MakeMaterial("WB_Wall", new Color(0.78f, 0.77f, 0.74f));
            Material ceiling = MakeMaterial("WB_Ceiling", new Color(0.86f, 0.86f, 0.84f));
            Material slab = MakeMaterial("WB_Slab", new Color(0.62f, 0.62f, 0.60f));
            Material accent = MakeMaterial("WB_Accent", new Color(0.20f, 0.45f, 0.62f));
            Material crate = MakeMaterial("WB_Crate", new Color(0.62f, 0.42f, 0.24f));
            Material panel = MakeEmissiveMaterial("WB_LightPanel", new Color(0.95f, 0.93f, 0.86f), new Color(2.2f, 2.1f, 1.8f));

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.50f, 0.51f, 0.55f);

            // Floor and outer shell.
            Box("Floor", new Vector3(0f, -0.1f, 0f), new Vector3(50f, 0.2f, 50f), floor);
            const float thickness = 0.4f;
            float wallHeight = FloorToFloor;
            Box("Wall_N", new Vector3(0f, wallHeight * 0.5f, 25f), new Vector3(50f, wallHeight, thickness), wall);
            Box("Wall_S", new Vector3(0f, wallHeight * 0.5f, -25f), new Vector3(50f, wallHeight, thickness), wall);
            Box("Wall_E", new Vector3(25f, wallHeight * 0.5f, 0f), new Vector3(thickness, wallHeight, 50f), wall);
            Box("Wall_W", new Vector3(-25f, wallHeight * 0.5f, 0f), new Vector3(thickness, wallHeight, 50f), wall);

            // Structural slab above this floor.
            Box("Slab", new Vector3(0f, FloorToFloor + 0.125f, 0f), new Vector3(50f, 0.25f, 50f), slab);

            // Suspended ceiling at 3.37 m, with an open stairwell at x 5.5..10.5, z 2.5..10.5.
            Box("Ceiling_W", new Vector3(-9.75f, CeilingY, 0f), new Vector3(30.5f, 0.06f, 50f), ceiling);
            Box("Ceiling_E", new Vector3(17.75f, CeilingY, 0f), new Vector3(14.5f, 0.06f, 50f), ceiling);
            Box("Ceiling_S", new Vector3(8f, CeilingY, -11.25f), new Vector3(5f, 0.06f, 27.5f), ceiling);
            Box("Ceiling_N", new Vector3(8f, CeilingY, 17.75f), new Vector3(5f, 0.06f, 14.5f), ceiling);

            // Recessed 1.2 x 0.6 m light panels on an 8 m grid.
            float[] grid = { -20f, -12f, -4f, 4f, 12f, 20f };
            for (int ix = 0; ix < grid.Length; ix++)
            {
                for (int iz = 0; iz < grid.Length; iz++)
                {
                    Box("LightPanel_" + ix + "_" + iz, new Vector3(grid[ix], CeilingY - 0.05f, grid[iz]), new Vector3(1.2f, 0.04f, 0.6f), panel);
                }
            }

            // Two-room block with a doorway. Walls meet the suspended ceiling.
            Box("Room_W", new Vector3(-15f, 1.7f, 6f), new Vector3(0.3f, 3.4f, 8f), wall);
            Box("Room_E", new Vector3(-5f, 1.7f, 6f), new Vector3(0.3f, 3.4f, 8f), wall);
            Box("Room_N", new Vector3(-10f, 1.7f, 10f), new Vector3(10f, 3.4f, 0.3f), wall);
            Box("Room_S_A", new Vector3(-13.5f, 1.7f, 2f), new Vector3(3f, 3.4f, 0.3f), wall);
            Box("Room_S_B", new Vector3(-6.5f, 1.7f, 2f), new Vector3(3f, 3.4f, 0.3f), wall);

            // Stairs (10 x 0.18 m) with a landing.
            for (int i = 0; i < 10; i++)
            {
                Box("Step_" + i, new Vector3(8f, 0.09f + i * 0.18f, 4f + i * 0.32f), new Vector3(3f, 0.18f, 0.32f), accent);
            }
            Box("Landing", new Vector3(8f, 1.88f, 8.4f), new Vector3(3f, 0.2f, 3f), accent);

            // Ramp.
            GameObject ramp = Box("Ramp", new Vector3(-14f, 1.1f, -8f), new Vector3(4f, 0.3f, 10f), accent);
            ramp.transform.rotation = Quaternion.Euler(-15f, 0f, 0f);

            // Crouch beam: standing height 1.8 m will not fit under a 1.15 m gap.
            Box("CrouchBeam", new Vector3(0f, 1.30f, -2f), new Vector3(4f, 0.3f, 0.6f), wall);

            // Pillars.
            Box("Pillar_A", new Vector3(5f, 1.7f, -5f), new Vector3(0.6f, 3.4f, 0.6f), wall);
            Box("Pillar_B", new Vector3(5f, 1.7f, 5f), new Vector3(0.6f, 3.4f, 0.6f), wall);
            Box("Pillar_C", new Vector3(15f, 1.7f, -5f), new Vector3(0.6f, 3.4f, 0.6f), wall);
            Box("Pillar_D", new Vector3(15f, 1.7f, 5f), new Vector3(0.6f, 3.4f, 0.6f), wall);

            // Crates.
            Box("Crate_1", new Vector3(18f, 0.5f, 12f), Vector3.one, crate);
            Box("Crate_2", new Vector3(19.1f, 0.5f, 12f), Vector3.one, crate);
            Box("Crate_3", new Vector3(18.55f, 1.5f, 12f), Vector3.one, crate);

            // Player.
            GameObject player = new GameObject("Player");
            player.transform.position = new Vector3(0f, 0.2f, -18f);
            CharacterController cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.slopeLimit = 45f;
            cc.stepOffset = 0.35f;
            cc.skinWidth = 0.02f;

            FirstPersonController fps = player.AddComponent<FirstPersonController>();
            player.AddComponent<PerfHud>();

            GameObject cameraObject = new GameObject("PlayerCamera");
            cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.62f, 0f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 78f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 300f;
            camera.tag = "MainCamera";
            cameraObject.AddComponent<AudioListener>();
            fps.BindCamera(cameraObject.transform);

            // Sun only reaches the stairwell opening; interior is lit by the panels below.
            GameObject sun = new GameObject("Sun");
            Light sunLight = sun.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.intensity = 0.9f;
            sunLight.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            AddCeilingLight("Light_Quad_NE", new Vector3(12f, 3.05f, 12f));
            AddCeilingLight("Light_Quad_NW", new Vector3(-12f, 3.05f, 12f));
            AddCeilingLight("Light_Quad_SE", new Vector3(12f, 3.05f, -12f));
            AddCeilingLight("Light_Quad_SW", new Vector3(-12f, 3.05f, -12f));
            AddCeilingLight("Light_Room", new Vector3(-10f, 3.0f, 6f));
            AddCeilingLight("Light_Stair", new Vector3(8f, 3.7f, 8f));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[M0] Whitebox scene built: " + ScenePath);
        }

        static void AddCeilingLight(string name, Vector3 position)
        {
            GameObject go = new GameObject(name);
            go.transform.position = position;
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 28f;
            light.intensity = 10f;
            light.color = new Color(1f, 0.95f, 0.86f);
            light.shadows = LightShadows.None;
        }

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets", "_Project");
            if (!AssetDatabase.IsValidFolder(Root + "/Scenes")) AssetDatabase.CreateFolder(Root, "Scenes");
            if (!AssetDatabase.IsValidFolder(Root + "/Materials")) AssetDatabase.CreateFolder(Root, "Materials");
        }

        static Material MakeMaterial(string name, Color color)
        {
            string path = Root + "/Materials/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.12f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material MakeEmissiveMaterial(string name, Color baseColor, Color emission)
        {
            Material material = MakeMaterial(name, baseColor);
            material.EnableKeyword("_EMISSION");
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", emission);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(material);
            return material;
        }

        static GameObject Box(string name, Vector3 position, Vector3 size, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }
    }
}