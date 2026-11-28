using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using EscapeFromWork.Gameplay;
using EscapeFromWork.LevelGen;

namespace EscapeFromWork.EditorTools
{
    /// <summary>
    /// Builds the T3 playable test scene: one player, one TowerRuntime with the real material set.
    /// Menu: Escape From Work / T3 / Build Tower Test Scene.
    /// </summary>
    public static class TowerSetup
    {
        public const string ScenePath = "Assets/_Project/Scenes/TowerTest.unity";

        [MenuItem("Escape From Work/T3/Build Tower Test Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            UnityEngine.SceneManagement.Scene scene =
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.52f, 0.53f, 0.56f, 1f);
            RenderSettings.skybox = null;

            GameObject player = new GameObject("Player");
            CharacterController body = player.AddComponent<CharacterController>();
            body.height = 1.8f;
            body.radius = 0.32f;
            body.center = new Vector3(0f, 0.9f, 0f);
            body.slopeLimit = 50f;
            body.stepOffset = 0.42f;
            FirstPersonController fps = player.AddComponent<FirstPersonController>();

            GameObject rig = new GameObject("PlayerCamera");
            rig.transform.SetParent(player.transform, false);
            rig.transform.localPosition = new Vector3(0f, fps.standEye, 0f);
            Camera camera = rig.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 320f;
            camera.fieldOfView = 78f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.05f, 0.06f, 1f);
            rig.AddComponent<AudioListener>();
            fps.cameraRig = rig.transform;
            fps.BindCamera(rig.transform);

            GameObject towerObject = new GameObject("Tower");
            TowerRuntime tower = towerObject.AddComponent<TowerRuntime>();
            tower.Seed = 1;
            tower.Materials = M1FloorScene.BuildMaterials();
            tower.Catalog = PropCatalog.LoadOrCreateDefault();
            tower.Player = player.transform;
            tower.NeighbourRadius = 0;

            Directory.CreateDirectory("Assets/_Project/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Debug.Log("[Tower] scene written -> " + ScenePath);
        }

        [MenuItem("Escape From Work/T3/Build Windows Player")]
        public static void BuildPlayer()
        {
            string[] scenes = { ScenePath };
            string output = "Builds/Windows/EFW_Tower.exe";
            Directory.CreateDirectory("Builds/Windows");
            BuildPipeline.BuildPlayer(scenes, output, BuildTarget.StandaloneWindows64, BuildOptions.None);
            Debug.Log("[Tower] player built -> " + output);
        }
    }
}
