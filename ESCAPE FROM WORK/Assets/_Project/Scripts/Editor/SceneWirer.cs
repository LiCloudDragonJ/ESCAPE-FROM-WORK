using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class SceneWirer
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
            if (t.parent != null) continue; // only root objects
            if (t.gameObject.scene.name != SceneManager.GetActiveScene().name) continue;
            Object.DestroyImmediate(t.gameObject);
        }

        // ---- 2. Create floor ----
        var floorHolder = new GameObject("--- Floor ---");
        var mat = GetLitMaterial();
        for (int x = 0; x < GridSize; x++)
        {
            for (int z = 0; z < GridSize; z++)
            {
                // Fixed positions: TeaRoom at (2,0), Stairwells at corners
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
                tile.isStatic = true;
            }
        }
        Debug.Log($"✅ Floor: {GridSize}x{GridSize} grid, {TileSize}x{TileSize} tiles, zero gaps");

        // ---- 2b. Ground plane (dark fill beyond edges) ----
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.position = new Vector3(50f, -0.5f, 50f);
        ground.transform.localScale = new Vector3(200f, 0.5f, 200f);
        ground.GetComponent<MeshRenderer>().sharedMaterial = new Material(mat) { color = new Color(0.1f, 0.1f, 0.12f) };
        ground.isStatic = true;

        // ---- 2c. Boundary walls ----
        var wallMat = new Material(mat) { color = new Color(0.2f, 0.2f, 0.25f) };
        float mapSize = GridSize * TileSize; // 100
        float wallH = 3f;
        float wallT = 0.5f;
        CreateWall($"Wall_North", new Vector3(mapSize / 2f, wallH / 2f, mapSize), new Vector3(mapSize + wallT * 2, wallH, wallT), wallMat);
        CreateWall($"Wall_South", new Vector3(mapSize / 2f, wallH / 2f, 0f), new Vector3(mapSize + wallT * 2, wallH, wallT), wallMat);
        CreateWall($"Wall_East",  new Vector3(mapSize, wallH / 2f, mapSize / 2f), new Vector3(wallT, wallH, mapSize + wallT * 2), wallMat);
        CreateWall($"Wall_West",  new Vector3(0f, wallH / 2f, mapSize / 2f), new Vector3(wallT, wallH, mapSize + wallT * 2), wallMat);
        Debug.Log("✅ Walls + dark ground");

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
            // Add enemy components
            e.AddComponent<EscapeFromWork.Enemies.KPIZombie>();
        }
        Debug.Log("✅ Enemies: 5 KPI zombies (red)");

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
        player.AddComponent<EscapeFromWork.Player.PlayerController>();
        player.AddComponent<EscapeFromWork.Player.PlayerAim>();
        player.AddComponent<EscapeFromWork.Player.PlayerCombat>();
        player.AddComponent<EscapeFromWork.Player.PlayerInventory>();
        player.AddComponent<EscapeFromWork.Player.PlayerInteraction>();
        player.AddComponent<EscapeFromWork.Player.PlayerHealth>();
        player.GetComponent<MeshRenderer>().sharedMaterial = new Material(mat) { color = Color.yellow };
        Debug.Log("✅ Player: yellow capsule at tea room entrance");

        // ---- 5. Camera ----
        var camObj = new GameObject("Main Camera");
        camObj.tag = "MainCamera";
        var cam = camObj.AddComponent<Camera>();
        camObj.AddComponent<AudioListener>();
        cam.orthographic = true;
        cam.orthographicSize = 16f; // 57 units visible @ 16:9, good detail for 100x100 map
        cam.transform.position = new Vector3(50f, 40f, 50f);
        // Camera follows player XZ between edges
        var cf = camObj.AddComponent<EscapeFromWork.Core.SimpleCameraFollow>();
        var cfo = new SerializedObject(cf);
        cfo.FindProperty("target").objectReferenceValue = player.transform;
        cfo.FindProperty("followSpeed").floatValue = 12f;
        cfo.FindProperty("boundsMin").vector2Value = new Vector2(0f, 0f);
        cfo.FindProperty("boundsMax").vector2Value = new Vector2(GridSize * TileSize, GridSize * TileSize);
        cfo.ApplyModifiedProperties();
        cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 300f;
        Debug.Log("✅ Camera: orthoSize=16, follows Player, clamped to map edges");

        // ---- 6. Light ----
        var light = new GameObject("Directional Light");
        var dl = light.AddComponent<Light>();
        dl.type = LightType.Directional;
        dl.intensity = 1.5f;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // ---- 7. GameManager ----
        var gmObj = new GameObject("GameManager");
        gmObj.AddComponent<EscapeFromWork.Core.GameManager>();

        // ---- 8. Save ----
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("🎉 Scene built! Visible in Editor, ready to Play.");
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

}
