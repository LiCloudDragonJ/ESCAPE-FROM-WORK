using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using EscapeFromWork.Player;
using EscapeFromWork.Weapons;
using EscapeFromWork.Data;
using EscapeFromWork.Enemies;

/// <summary>
/// One-click scene builder. Builds floor, enemies, loot, player, camera,
/// HUD, and extraction points. Delegate to partial-class files for
/// HUD / loot / weapon wiring to keep file sizes manageable.
/// </summary>
public static partial class SceneWirer
{
    // ---- Constants ----
    const int GridSize = 5;
    const float TileSize = 20f;
    const float FloorY = 0f;
    const float FloorHeight = 0.2f;

    static Color[] RoomColors = {
        new Color(0.7f, 0.65f, 0.55f), // Office
        new Color(0.4f, 0.7f, 0.7f),   // Hallway
        new Color(0.3f, 0.7f, 0.3f),   // TeaRoom
        new Color(0.5f, 0.5f, 0.5f),   // Stairwell
        new Color(0.7f, 0.3f, 0.7f),   // Conference
    };

    [MenuItem("ESCAPE FROM WORK/Build Scene")]
    public static void BuildScene()
    {
        // ---- 1. Clean slate ----
        foreach (var t in Object.FindObjectsOfType<Transform>())
        {
            if (t == null || t.gameObject == null) continue;
            if (t.parent != null) continue;
            if (t.gameObject.scene.name != SceneManager.GetActiveScene().name) continue;
            UnityEngine.Object.DestroyImmediate(t.gameObject);
        }

        // ---- 2. Create floor ----
        var floorHolder = new GameObject("--- Floor ---");
        var mat = GetLitMaterial();
        for (int x = 0; x < GridSize; x++)
        {
            for (int z = 0; z < GridSize; z++)
            {
                int colorIdx = 0;
                if (x == 2 && z == 0) colorIdx = 2;
                else if (x == 0 && z == 0) colorIdx = 3;
                else if (x == 4 && z == 4) colorIdx = 3;
                else if (x == 1 || z == 1) colorIdx = 1;
                else if (x == 3 || z == 3) colorIdx = 4;

                var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.name = $"Tile_{x}_{z}";
                tile.transform.parent = floorHolder.transform;
                tile.transform.position = new Vector3(
                    x * TileSize + TileSize / 2f,
                    FloorY,
                    z * TileSize + TileSize / 2f
                );
                tile.transform.localScale = new Vector3(TileSize, FloorHeight, TileSize);
                tile.GetComponent<MeshRenderer>().sharedMaterial = new Material(mat) { color = RoomColors[colorIdx] };
                UnityEngine.Object.DestroyImmediate(tile.GetComponent<Collider>());
                tile.isStatic = true;
            }
        }
        Debug.Log($"Floor: {GridSize}x{GridSize} grid, {TileSize}x{TileSize} tiles, zero gaps");

        // ---- 2b. Single flat floor collider ----
        var floorCollider = new GameObject("FloorCollider");
        var fc = floorCollider.AddComponent<BoxCollider>();
        fc.size = new Vector3(GridSize * TileSize, 0.1f, GridSize * TileSize);
        fc.center = new Vector3(GridSize * TileSize / 2f, FloorY, GridSize * TileSize / 2f);
        floorCollider.isStatic = true;

        // ---- 2c. Ground plane ----
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.position = new Vector3(50f, -0.5f, 50f);
        ground.transform.localScale = new Vector3(200f, 0.5f, 200f);
        ground.GetComponent<MeshRenderer>().sharedMaterial = new Material(mat) { color = new Color(0.1f, 0.1f, 0.12f) };
        UnityEngine.Object.DestroyImmediate(ground.GetComponent<Collider>());
        ground.isStatic = true;

        // ---- 2d. Boundary walls ----
        var wallMat = new Material(mat) { color = new Color(0.2f, 0.2f, 0.25f) };
        float mapSize = GridSize * TileSize;
        float wallH = 3f;
        float wallT = 0.5f;
        CreateWall($"Wall_North", new Vector3(mapSize / 2f, wallH / 2f, mapSize), new Vector3(mapSize + wallT * 2, wallH, wallT), wallMat);
        CreateWall($"Wall_South", new Vector3(mapSize / 2f, wallH / 2f, 0f), new Vector3(mapSize + wallT * 2, wallH, wallT), wallMat);
        CreateWall($"Wall_East",  new Vector3(mapSize, wallH / 2f, mapSize / 2f), new Vector3(wallT, wallH, mapSize + wallT * 2), wallMat);
        CreateWall($"Wall_West",  new Vector3(0f, wallH / 2f, mapSize / 2f), new Vector3(wallT, wallH, mapSize + wallT * 2), wallMat);
        Debug.Log("Walls + dark ground");

        // ---- 3. Spawn enemies ----
        var enemyHolder = new GameObject("--- Enemies ---");
        var enemyMat = new Material(mat) { color = Color.red };
        for (int i = 0; i < 5; i++)
        {
            var e = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            e.name = $"Enemy_{i}";
            e.tag = "Enemy";
            e.transform.parent = enemyHolder.transform;
            e.transform.position = new Vector3(
                Random.Range(10f, 90f),
                1f,
                Random.Range(10f, 90f)
            );
            e.transform.localScale = new Vector3(1f, 1.2f, 1f);
            e.GetComponent<MeshRenderer>().sharedMaterial = enemyMat;
            e.AddComponent<EscapeFromWork.Enemies.KPIZombie>();
        }
        Debug.Log("Enemies: 5 KPI zombies (red)");

        // ---- 3b. Loot containers ----
        var lootHolder = new GameObject("--- Loot ---");
        foreach (var (ctype, color, count) in new (ContainerType, Color, int)[] {
            (ContainerType.Desk,          new Color(0.3f, 0.6f, 0.9f), 5),
            (ContainerType.FilingCabinet, new Color(0.5f, 0.5f, 0.6f), 3),
            (ContainerType.SupplyCloset,  new Color(0.2f, 0.7f, 0.4f), 2),
            (ContainerType.Safe,          new Color(0.9f, 0.7f, 0.2f), 1),
            (ContainerType.ServerRack,    new Color(0.1f, 0.1f, 0.3f), 1),
        })
        for (int i = 0; i < count; i++)
        {
            var container = GameObject.CreatePrimitive(PrimitiveType.Cube);
            container.name = $"Loot_{ctype}_{i}";
            container.tag = "Loot";
            container.transform.parent = lootHolder.transform;
            container.transform.position = new Vector3(Random.Range(5f, 95f), 0.3f, Random.Range(5f, 95f));
            container.transform.localScale = new Vector3(1f, 0.6f, 1f);
            container.GetComponent<MeshRenderer>().sharedMaterial = new Material(mat) { color = color };
            var lc = container.AddComponent<EscapeFromWork.Loot.LootContainer>();
            var lcSO = new SerializedObject(lc);
            lcSO.FindProperty("containerType").enumValueIndex = (int)ctype;
            lcSO.ApplyModifiedProperties();
        }
        Debug.Log("Loot: 12 containers (5 Desk, 3 Cabinet, 2 Supply, 1 Safe, 1 Server)");

        // ---- 3c. Loose loot ----
        BuildLooseLoot(mat);

        // ---- 3d. Extraction triggers ----
        var extractMat = new Material(mat) { color = new Color(0.2f, 0.9f, 0.3f) };
        AddExtractionPoint("Extract_Stairs", new Vector3(TileSize/2f, 0.1f, TileSize/2f), false, extractMat);
        AddExtractionPoint("Extract_FireEscape", new Vector3(TileSize * (GridSize-1) + TileSize/2f, 0.1f, TileSize * (GridSize-1) + TileSize/2f), true, extractMat);
        Debug.Log("Extraction: stairs (SW) + fire escape (NE)");

        // ---- 4. Player ----
        var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.tag = "Player";
        player.transform.position = new Vector3(TileSize * 2 + TileSize / 2f, 1f, 2f);
        player.transform.localScale = new Vector3(1f, 1.5f, 1f);
        var rb = player.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        player.AddComponent<PlayerController>();
        player.AddComponent<PlayerAim>();
        player.AddComponent<PlayerCombat>();
        player.AddComponent<PlayerInventory>();
        player.AddComponent<PlayerInteraction>();
        player.AddComponent<PlayerHealth>();
        player.GetComponent<MeshRenderer>().sharedMaterial = new Material(mat) { color = Color.yellow };
        Debug.Log("Player: yellow capsule at tea room entrance");

        // ---- 5. Camera ----
        var camObj = new GameObject("Main Camera");
        camObj.tag = "MainCamera";
        var cam = camObj.AddComponent<Camera>();
        camObj.AddComponent<AudioListener>();
        var cf = camObj.AddComponent<EscapeFromWork.Core.SimpleCameraFollow>();
        var cfo = new SerializedObject(cf);
        cfo.FindProperty("target").objectReferenceValue = player.transform;
        cfo.FindProperty("boundsMin").vector2Value = new Vector2(-20f, -20f);
        cfo.FindProperty("boundsMax").vector2Value = new Vector2(GridSize * TileSize + 20f, GridSize * TileSize + 20f);
        cfo.ApplyModifiedProperties();
        Debug.Log("Camera: 2.5D perspective (SimpleCameraFollow)");

        // ---- 6. Light ----
        var light = new GameObject("Directional Light");
        var dl = light.AddComponent<Light>();
        dl.type = LightType.Directional;
        dl.intensity = 1.5f;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // ---- 7. HUD Canvas ----
        BuildHUD();

        // ---- 8. FloorManager + GameManager ----
        var fmObj = new GameObject("FloorManager");
        var fm = fmObj.AddComponent<EscapeFromWork.Level.FloorManager>();
        fm.floorNumber = 50;

        var gmObj = new GameObject("GameManager");
        gmObj.AddComponent<EscapeFromWork.Core.GameManager>();

        // ---- 9. Wire & save ----
        WireWeapons();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("Scene built + weapons wired. Ready to Play!");
    }

    static Material GetLitMaterial()
    {
        var s = Shader.Find("Universal Render Pipeline/Lit")
             ?? Shader.Find("URP/Lit")
             ?? Shader.Find("Standard");
        return new Material(s);
    }

    static void CreateWall(string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
        w.name = name;
        w.transform.position = pos;
        w.transform.localScale = scale;
        w.GetComponent<MeshRenderer>().sharedMaterial = mat;
        w.isStatic = true;
    }

    static T GetOrCreateSO<T>(string path) where T : ScriptableObject
    {
        var e = AssetDatabase.LoadAssetAtPath<T>(path);
        if (e != null) return e;
        var so = ScriptableObject.CreateInstance<T>();
        so.name = System.IO.Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(so, path);
        return so;
    }
}
