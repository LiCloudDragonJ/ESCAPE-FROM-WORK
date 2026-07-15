using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using EscapeFromWork.Player;
using EscapeFromWork.Weapons;
using EscapeFromWork.Data;
using EscapeFromWork.Enemies;

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
                UnityEngine.Object.DestroyImmediate(tile.GetComponent<Collider>()); // visual only, no blocking
                tile.isStatic = true;
            }
        }
        Debug.Log($"✅ Floor: {GridSize}x{GridSize} grid, {TileSize}x{TileSize} tiles, zero gaps");

        // ---- 2b. Single flat floor collider (player walks on this) ----
        var floorCollider = new GameObject("FloorCollider");
        var fc = floorCollider.AddComponent<BoxCollider>();
        fc.size = new Vector3(GridSize * TileSize, 0.1f, GridSize * TileSize);
        fc.center = new Vector3(GridSize * TileSize / 2f, FloorY, GridSize * TileSize / 2f);
        floorCollider.isStatic = true;

        // ---- 2c. Ground plane (dark fill beyond edges) ----
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.position = new Vector3(50f, -0.5f, 50f);
        ground.transform.localScale = new Vector3(200f, 0.5f, 200f);
        ground.GetComponent<MeshRenderer>().sharedMaterial = new Material(mat) { color = new Color(0.1f, 0.1f, 0.12f) };
        UnityEngine.Object.DestroyImmediate(ground.GetComponent<Collider>());
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

        // ---- 3b. Loot containers (different types per color) ----
        var lootHolder = new GameObject("--- Loot ---");
        foreach (var (ctype, color, count) in new (EscapeFromWork.Data.ContainerType, Color, int)[] {
            (EscapeFromWork.Data.ContainerType.Desk,          new Color(0.3f, 0.6f, 0.9f), 5),
            (EscapeFromWork.Data.ContainerType.FilingCabinet, new Color(0.5f, 0.5f, 0.6f), 3),
            (EscapeFromWork.Data.ContainerType.SupplyCloset,  new Color(0.2f, 0.7f, 0.4f), 2),
            (EscapeFromWork.Data.ContainerType.Safe,          new Color(0.9f, 0.7f, 0.2f), 1),
            (EscapeFromWork.Data.ContainerType.ServerRack,    new Color(0.1f, 0.1f, 0.3f), 1),
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
        Debug.Log("✅ Loot: 12 containers (5 Desk, 3 Cabinet, 2 Supply, 1 Safe, 1 Server)");

        // ---- 3d. Loose loot (large valuables placed near furniture) ----
        var looseHolder = new GameObject("--- Loose Loot ---");
        // Only big/valuable items spawn loose — the exciting "come look!" moments
        string[] looseItems = {
            "Assets/_Project/ScriptableObjects/Items/SO_Item_GPU.asset",
            "Assets/_Project/ScriptableObjects/Items/SO_Item_Figurine.asset",
            "Assets/_Project/ScriptableObjects/Items/SO_Item_Whiskey.asset",
            "Assets/_Project/ScriptableObjects/Items/SO_Item_PaidLeave.asset",
            "Assets/_Project/ScriptableObjects/Items/SO_Item_Trophy.asset",
            "Assets/_Project/ScriptableObjects/Items/SO_Item_StockOption.asset",
            "Assets/_Project/ScriptableObjects/Items/SO_Item_BonusCheck.asset",
        };
        // Pre-defined furniture positions (near walls, corners, desk clusters)
        Vector3[] furnitureSpots = {
            new(15,0.4f,15), new(35,0.4f,15), new(55,0.4f,15), new(75,0.4f,15),
            new(15,0.4f,45), new(35,0.4f,45), new(85,0.4f,35), new(60,0.4f,65),
            new(25,0.4f,75), new(45,0.4f,85), new(80,0.4f,80), new(20,0.4f,85),
        };
        for (int i = 0; i < 8; i++)
        {
            var itemSO = AssetDatabase.LoadAssetAtPath<EscapeFromWork.Data.ItemData>(looseItems[Random.Range(0, looseItems.Length)]);
            if (itemSO == null) continue;
            var pos = furnitureSpots[i] + new Vector3(Random.Range(-3f,3f), 0, Random.Range(-3f,3f));
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Loose_{itemSO.ItemName}";
            go.tag = "Loot";
            go.transform.parent = looseHolder.transform;
            go.transform.position = pos;
            go.transform.localScale = new Vector3(itemSO.Width * 0.5f, 0.3f, (itemSO.Height > 1 ? itemSO.Height : 1) * 0.5f);
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            var trigger = new GameObject("Trigger").AddComponent<SphereCollider>();
            trigger.isTrigger = true; trigger.radius = 2f;
            trigger.transform.SetParent(go.transform, false);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(mat) { color = itemSO.Rarity switch {
                Rarity.Legendary => new Color(1f, 0.6f, 0f), Rarity.Epic => new Color(0.7f, 0.3f, 1f),
                Rarity.Rare => new Color(0.2f, 0.6f, 1f), _ => new Color(0.3f, 0.8f, 0.3f)
            }};
            var pickup = go.AddComponent<EscapeFromWork.Loot.PickupItem>();
            pickup.Initialize(itemSO, 1);
            go.AddComponent<EscapeFromWork.Loot.LooseLootBob>();
        }
        Debug.Log("✅ Loose loot: 8 big valuables near furniture");

        // ---- 3c. Extraction triggers ----
        var extractMat = new Material(mat) { color = new Color(0.2f, 0.9f, 0.3f) }; // green
        AddExtractionPoint("Extract_Stairs", new Vector3(TileSize/2f, 0.1f, TileSize/2f), false, extractMat);
        AddExtractionPoint("Extract_FireEscape", new Vector3(TileSize * (GridSize-1) + TileSize/2f, 0.1f, TileSize * (GridSize-1) + TileSize/2f), true, extractMat);
        Debug.Log("✅ Extraction: stairs (SW) + fire escape (NE)");

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
        player.AddComponent<EscapeFromWork.Player.PlayerController>();
        player.AddComponent<EscapeFromWork.Player.PlayerAim>();
        player.AddComponent<EscapeFromWork.Player.PlayerCombat>();
        player.AddComponent<EscapeFromWork.Player.PlayerInventory>();
        player.AddComponent<EscapeFromWork.Player.PlayerInteraction>();
        player.AddComponent<EscapeFromWork.Player.PlayerHealth>();
        player.GetComponent<MeshRenderer>().sharedMaterial = new Material(mat) { color = Color.yellow };
        Debug.Log("✅ Player: yellow capsule at tea room entrance");

        // ---- 5. Camera (2.5D perspective — SimpleCameraFollow handles everything) ----
        var camObj = new GameObject("Main Camera");
        camObj.tag = "MainCamera";
        var cam = camObj.AddComponent<Camera>();
        camObj.AddComponent<AudioListener>();
        // SimpleCameraFollow.Awake() sets perspective, FOV, pitch, and handles follow+clamp.
        var cf = camObj.AddComponent<EscapeFromWork.Core.SimpleCameraFollow>();
        var cfo = new SerializedObject(cf);
        cfo.FindProperty("target").objectReferenceValue = player.transform;
        cfo.FindProperty("boundsMin").vector2Value = new Vector2(-20f, -20f);
        cfo.FindProperty("boundsMax").vector2Value = new Vector2(GridSize * TileSize + 20f, GridSize * TileSize + 20f);
        cfo.ApplyModifiedProperties();
        Debug.Log("✅ Camera: 2.5D perspective (SimpleCameraFollow), follows Player");

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

        // ---- 9. Save ----
        // Auto-wire weapons after scene build
        WireWeapons();

        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("🎉 Scene built + weapons wired. Ready to Play!");
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

    static void BuildHUD()
    {
        // Don't overwrite manually-adjusted layout once created
        if (GameObject.Find("HUDCanvas") != null) { Debug.Log("HUD exists — keeping manual layout"); return; }

        // Ensure EventSystem exists (required for UI button clicks)
        if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        var canvasGo = new GameObject("HUDCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        var hud = canvasGo.AddComponent<EscapeFromWork.UI.HUDManager>();

        // Panel helper: creates a positioned, sized, background-filled panel
        RectTransform MakePanel(Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color bgColor)
        {
            var go = new GameObject("Panel", typeof(RectTransform));
            go.transform.SetParent(canvasGo.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor; rt.anchorMax = anchor;
            rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
            var bg = go.AddComponent<Image>();
            bg.color = bgColor;
            bg.raycastTarget = false;
            return rt;
        }

        // Text helper: fills parent, uses textAnchor for alignment
        Text MakeText(string name, RectTransform parent, int fontSize, TextAnchor align, Color? c = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
            t.fontSize = fontSize;
            t.color = c ?? Color.white;
            t.alignment = align;
            t.raycastTarget = false;
            var tr = go.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.sizeDelta = Vector2.zero;
            tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
            tr.pivot = new Vector2(0.5f, 0.5f);
            return t;
        }

        // ---- Top-Left: Health (480×90) ----
        var hpRt = MakePanel(new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -40), new Vector2(480, 90), new Color(0, 0, 0, 0.55f));
        var sliderGo = new GameObject("Slider", typeof(RectTransform)); sliderGo.transform.SetParent(hpRt, false);
        var sliderBg = sliderGo.AddComponent<Image>(); sliderBg.color = new Color(0.1f, 0.1f, 0.1f, 0.7f);
        var fillGo = new GameObject("Fill", typeof(RectTransform)); fillGo.transform.SetParent(sliderGo.transform, false);
        var fillImg = fillGo.AddComponent<Image>(); fillImg.color = new Color(0.9f, 0.2f, 0.2f);
        var fRt = fillGo.GetComponent<RectTransform>(); fRt.anchorMin = Vector2.zero; fRt.anchorMax = Vector2.one; fRt.sizeDelta = Vector2.zero;
        var slider = sliderGo.AddComponent<Slider>(); slider.fillRect = fRt; slider.targetGraphic = fillImg;
        var slRt = sliderGo.GetComponent<RectTransform>();
        slRt.anchorMin = new Vector2(0, 0.3f); slRt.anchorMax = new Vector2(1, 0.7f);
        slRt.offsetMin = new Vector2(12, 0); slRt.offsetMax = new Vector2(-12, 0);
        var healthTxt = MakeText("HealthText", hpRt, 24, TextAnchor.MiddleCenter);
        var htRt = healthTxt.GetComponent<RectTransform>();
        htRt.anchorMin = new Vector2(0, 0.3f); htRt.anchorMax = new Vector2(1, 0.7f);

        // ---- Top-Right: Floor (330×100) ----
        var fpRt = MakePanel(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -40), new Vector2(330, 100), new Color(0, 0, 0, 0.55f));
        var floorTxt = MakeText("FloorText", fpRt, 40, TextAnchor.MiddleCenter);
        var ftRt = floorTxt.GetComponent<RectTransform>();
        ftRt.anchorMin = new Vector2(0, 0.5f); ftRt.anchorMax = new Vector2(1, 1);
        ftRt.sizeDelta = Vector2.zero; ftRt.pivot = new Vector2(0.5f, 0.5f);
        var statusTxt = MakeText("StatusText", fpRt, 20, TextAnchor.MiddleCenter);
        var stRt = statusTxt.GetComponent<RectTransform>();
        stRt.anchorMin = new Vector2(0, 0); stRt.anchorMax = new Vector2(1, 0.5f);
        stRt.sizeDelta = Vector2.zero; stRt.pivot = new Vector2(0.5f, 0.5f);

        // ---- Top-Left below Health: Ammo (330×80) ----
        var apRt = MakePanel(new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -140), new Vector2(330, 80), new Color(0, 0, 0, 0.55f));
        var ammoTxt = MakeText("AmmoText", apRt, 44, TextAnchor.MiddleCenter);
        ammoTxt.GetComponent<RectTransform>().anchorMin = new Vector2(0, 0.35f);
        ammoTxt.GetComponent<RectTransform>().anchorMax = new Vector2(1, 1);
        ammoTxt.text = "15 / 15";
        var ammoTypeTxt = MakeText("AmmoTypeText", apRt, 22, TextAnchor.MiddleCenter);
        ammoTypeTxt.GetComponent<RectTransform>().anchorMin = new Vector2(0, 0);
        ammoTypeTxt.GetComponent<RectTransform>().anchorMax = new Vector2(1, 0.35f);
        ammoTypeTxt.text = "Staple";

        // --- Center: Extraction warning ---
        var extPanelRt = MakePanel(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, 60), new Color(0, 0, 0, 0));
        extPanelRt.gameObject.SetActive(false);
        var extTxt = MakeText("ExtractionText", extPanelRt, 36, TextAnchor.MiddleCenter, Color.red);
        extTxt.text = "撤离!";

        // --- Prompt ---
        var promptRt = MakePanel(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 85), new Vector2(400, 30), new Color(0, 0, 0, 0));
        var promptTxt = MakeText("PromptText", promptRt, 18, TextAnchor.MiddleCenter);
        promptTxt.text = "";

        // --- Loot Container UI (3 equal columns: equip | backpack | container) ---
        float panelW = 1450, panelH = 820;
        var lcPanelRt = MakePanel(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(panelW, panelH), new Color(0.05f, 0.05f, 0.08f, 0.95f));
        lcPanelRt.gameObject.SetActive(false);

        // Title bar at top
        var lcTitle = MakeText("LCTitle", lcPanelRt, 22, TextAnchor.UpperCenter);
        lcTitle.text = "搜刮中...";
        lcTitle.GetComponent<RectTransform>().anchorMin = new Vector2(0, 0.95f); lcTitle.GetComponent<RectTransform>().anchorMax = new Vector2(1, 1);

        // Labels row (each aligned above its column)
        var eqLabel = MakeText("EQLabel", lcPanelRt, 22, TextAnchor.UpperCenter); eqLabel.text = "装备";
        eqLabel.GetComponent<RectTransform>().anchorMin = new Vector2(0.02f, 0.90f); eqLabel.GetComponent<RectTransform>().anchorMax = new Vector2(0.18f, 0.95f);
        var bpLabel = MakeText("BPLabel", lcPanelRt, 22, TextAnchor.UpperCenter); bpLabel.text = "背包";
        bpLabel.GetComponent<RectTransform>().anchorMin = new Vector2(0.19f, 0.90f); bpLabel.GetComponent<RectTransform>().anchorMax = new Vector2(0.60f, 0.95f);
        var cLabel = MakeText("CLabel", lcPanelRt, 22, TextAnchor.UpperCenter); cLabel.text = "容器";
        cLabel.GetComponent<RectTransform>().anchorMin = new Vector2(0.61f, 0.90f); cLabel.GetComponent<RectTransform>().anchorMax = new Vector2(0.98f, 0.95f);

        // Columns: equip (narrow) | backpack (wide) | container (wide)
        var eqPanel = new GameObject("EquipPanel", typeof(RectTransform), typeof(Image));
        eqPanel.transform.SetParent(lcPanelRt, false);
        eqPanel.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.10f, 0.8f);
        eqPanel.GetComponent<RectTransform>().anchorMin = new Vector2(0.02f, 0.06f);
        eqPanel.GetComponent<RectTransform>().anchorMax = new Vector2(0.18f, 0.89f);

        var bpGrid = new GameObject("BackpackGrid", typeof(RectTransform));
        bpGrid.transform.SetParent(lcPanelRt, false);
        bpGrid.GetComponent<RectTransform>().anchorMin = new Vector2(0.19f, 0.06f);
        bpGrid.GetComponent<RectTransform>().anchorMax = new Vector2(0.60f, 0.89f);

        var cGrid = new GameObject("ContainerGrid", typeof(RectTransform));
        cGrid.transform.SetParent(lcPanelRt, false);
        cGrid.GetComponent<RectTransform>().anchorMin = new Vector2(0.61f, 0.06f);
        cGrid.GetComponent<RectTransform>().anchorMax = new Vector2(0.98f, 0.89f);

        // Drop zone (red bar at bottom of backpack panel)
        var dropZone = new GameObject("DropZone", typeof(RectTransform), typeof(Image));
        dropZone.transform.SetParent(lcPanelRt, false);
        dropZone.GetComponent<Image>().color = new Color(0.8f, 0.1f, 0.1f, 0.7f);
        dropZone.GetComponent<RectTransform>().anchorMin = new Vector2(0.75f, 0.01f);
        dropZone.GetComponent<RectTransform>().anchorMax = new Vector2(0.98f, 0.05f);
        dropZone.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
        var dzLabel = MakeText("DZLabel", dropZone.GetComponent<RectTransform>(), 14, TextAnchor.MiddleCenter, Color.white);
        dzLabel.text = "拖拽至此丢弃";
        dzLabel.raycastTarget = false;

        var lcUI = canvasGo.AddComponent<EscapeFromWork.UI.LootContainerUI>();
        var lcUISO = new SerializedObject(lcUI);
        lcUISO.FindProperty("panel").objectReferenceValue = lcPanelRt.gameObject;
        lcUISO.FindProperty("titleText").objectReferenceValue = lcTitle;
        lcUISO.FindProperty("equipParent").objectReferenceValue = eqPanel.transform;
        lcUISO.FindProperty("bpGridParent").objectReferenceValue = bpGrid.transform;
        lcUISO.FindProperty("bpLabel").objectReferenceValue = bpLabel;
        lcUISO.FindProperty("contGridParent").objectReferenceValue = cGrid.transform;
        lcUISO.FindProperty("contLabel").objectReferenceValue = cLabel;
        // Item info text (bottom of equipment panel)
        var infoTxt = MakeText("InfoText", lcPanelRt, 14, TextAnchor.UpperLeft);
        infoTxt.text = "";
        infoTxt.GetComponent<RectTransform>().anchorMin = new Vector2(0.02f, 0.01f);
        infoTxt.GetComponent<RectTransform>().anchorMax = new Vector2(0.25f, 0.05f);

        lcUISO.FindProperty("infoText").objectReferenceValue = infoTxt;
        lcUISO.FindProperty("cellSize").floatValue = 60f;
        lcUISO.ApplyModifiedProperties();

        // Wire HUDManager
        var hudSO = new SerializedObject(hud);
        hudSO.FindProperty("healthBar").objectReferenceValue = slider;
        hudSO.FindProperty("healthText").objectReferenceValue = healthTxt;
        hudSO.FindProperty("ammoText").objectReferenceValue = ammoTxt;
        hudSO.FindProperty("ammoTypeText").objectReferenceValue = ammoTypeTxt;
        hudSO.FindProperty("floorNumberText").objectReferenceValue = floorTxt;
        hudSO.FindProperty("floorStatusText").objectReferenceValue = statusTxt;
        hudSO.FindProperty("extractionTimerText").objectReferenceValue = extTxt;
        hudSO.FindProperty("extractionWarning").objectReferenceValue = extPanelRt.gameObject;
        hudSO.FindProperty("interactionPrompt").objectReferenceValue = promptTxt;
        hudSO.ApplyModifiedProperties();

        Debug.Log("✅ HUD Canvas created");
    }

    static void WireWeapons()
    {
        var player = GameObject.Find("Player");
        if (player == null) { Debug.LogWarning("Player not found — skipping weapon wiring"); return; }

        var projPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Weapons/Projectile.prefab");
        if (projPrefab == null)
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tmp.name = "Projectile"; tmp.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            tmp.AddComponent<Projectile>();
            tmp.GetComponent<Collider>().isTrigger = true;
            var rb = tmp.AddComponent<Rigidbody>(); rb.useGravity = false; rb.interpolation = RigidbodyInterpolation.Interpolate;
            projPrefab = PrefabUtility.SaveAsPrefabAsset(tmp, "Assets/_Project/Prefabs/Weapons/Projectile.prefab");
            UnityEngine.Object.DestroyImmediate(tmp);
        }

        var pistol = player.transform.Find("Weapon_StaplerPistol")?.GetComponent<RangedWeapon>();
        if (pistol == null)
        {
            var go = new GameObject("Weapon_StaplerPistol"); go.transform.SetParent(player.transform, false);
            var muzzle = new GameObject("Muzzle"); muzzle.transform.SetParent(go.transform, false);
            muzzle.transform.localPosition = new Vector3(0, 0, 0.5f);
            pistol = go.AddComponent<RangedWeapon>();
            var so = new SerializedObject(pistol);
            so.FindProperty("data").objectReferenceValue = GetOrCreateSO<WeaponData>("Assets/_Project/ScriptableObjects/Weapons/SO_Weapon_StaplerPistol.asset");
            so.FindProperty("projectilePrefab").objectReferenceValue = projPrefab;
            so.FindProperty("muzzlePoint").objectReferenceValue = muzzle.transform;
            so.ApplyModifiedProperties();
        }

        var melee = player.transform.Find("Weapon_KeyboardMelee")?.GetComponent<MeleeWeapon>();
        if (melee == null)
        {
            var go = new GameObject("Weapon_KeyboardMelee"); go.transform.SetParent(player.transform, false);
            melee = go.AddComponent<MeleeWeapon>();
            var so = new SerializedObject(melee);
            so.FindProperty("data").objectReferenceValue = GetOrCreateSO<WeaponData>("Assets/_Project/ScriptableObjects/Weapons/SO_Weapon_KeyboardMelee.asset");
            so.ApplyModifiedProperties();
        }

        var inv = player.GetComponent<PlayerInventory>();
        if (inv != null)
        {
            var so = new SerializedObject(inv);
            var eq = so.FindProperty("equippedWeapons");
            eq.ClearArray();
            eq.InsertArrayElementAtIndex(0); eq.GetArrayElementAtIndex(0).objectReferenceValue = pistol;
            eq.InsertArrayElementAtIndex(1); eq.GetArrayElementAtIndex(1).objectReferenceValue = null;
            eq.InsertArrayElementAtIndex(2); eq.GetArrayElementAtIndex(2).objectReferenceValue = melee;
            so.ApplyModifiedProperties();
        }

        var enemyData = GetOrCreateSO<EnemyData>("Assets/_Project/ScriptableObjects/Enemies/SO_Enemy_KPIZombie.asset");
        var enemyHolder = GameObject.Find("--- Enemies ---");
        if (enemyHolder != null)
            foreach (var e in enemyHolder.GetComponentsInChildren<KPIZombie>())
                new SerializedObject(e).FindProperty("data").objectReferenceValue = enemyData;

        // Wire loot types and tables
        WireLootTables();

        // Give player initial small backpack
        var initBp = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/_Project/ScriptableObjects/Items/SO_Item_BP_Small.asset");
        var playerCombat = Object.FindObjectOfType<EscapeFromWork.Player.PlayerCombat>();
        if (playerCombat != null && initBp != null && playerCombat.SlotBackpack == null)
            playerCombat.SetSlotItem(GearSlot.Backpack, initBp);

        Debug.Log("✅ Weapons + LootTable wired");
    }

    static void WireLootTables()
    {
        // ==== 办公消耗品 (Common) ====
        var pp  = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_Paperclip.asset", "回形针", ItemType.Currency, Rarity.Common, 1, 999, 1);
        var pr  = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_PrinterPaper.asset", "打印纸", ItemType.OfficeSupply, Rarity.Common, 1, 200, 2);
        var ink = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_InkCartridge.asset", "墨盒", ItemType.OfficeSupply, Rarity.Common, 1, 30, 8);
        var cf  = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_CoffeeBean.asset", "咖啡豆", ItemType.Consumable, Rarity.Common, 1, 20, 5);
        var ban = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_EnergyBar.asset", "能量棒", ItemType.Consumable, Rarity.Common, 1, 10, 10);
        var tap = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_StickyNotes.asset", "便利贴", ItemType.OfficeSupply, Rarity.Common, 1, 50, 2);

        // ==== 建材 (Common/Uncommon) ====
        var scr = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_Screws.asset", "螺丝钉", ItemType.Construction, Rarity.Common, 1, 50, 3);
        var wr  = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_Wire.asset", "铜芯电线", ItemType.Construction, Rarity.Common, 1, 30, 5);
        var mtl = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_MetalSheet.asset", "金属薄板", ItemType.Construction, Rarity.Uncommon, 2, 15, 15);
        var brd = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_CircuitBoard.asset", "电路板", ItemType.Construction, Rarity.Uncommon, 1, 10, 20);

        // ==== 电子产品 (Uncommon/Rare) ====
        var usb = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_USB.asset", "加密U盘", ItemType.Electronics, Rarity.Uncommon, 1, 5, 500);
        var hdd = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_HardDrive.asset", "SSD硬盘", ItemType.Electronics, Rarity.Uncommon, 2, 3, 800);
        var cpu = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_CPU.asset", "CPU处理器", ItemType.Electronics, Rarity.Rare, 1, 2, 2000);
        var gpu = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_GPU.asset", "未拆封显卡", ItemType.Electronics, Rarity.Epic, 2, 1, 8000);

        // ==== 奢侈品 (Epic/Legendary) ====
        var pen = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_CEOPen.asset", "CEO签名钢笔", ItemType.Luxury, Rarity.Epic, 1, 1, 12000);
        var fig = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_Figurine.asset", "绝版手办(未拆)", ItemType.Luxury, Rarity.Legendary, 2, 1, 25000);
        var whi = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_Whiskey.asset", "珍藏威士忌", ItemType.Luxury, Rarity.Epic, 1, 1, 15000);

        // ==== 情报 (Uncommon/Rare) ====
        var fil = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_PersonnelFile.asset", "人事档案", ItemType.Intel, Rarity.Uncommon, 1, 1, 300);
        var fin = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_FinReport.asset", "财务报告(机密)", ItemType.Intel, Rarity.Rare, 1, 1, 1200);
        var rd  = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_RnDNotes.asset", "研发笔记", ItemType.Intel, Rarity.Rare, 1, 1, 1500);

        // ==== 收藏品 — "大货" (Epic/Legendary) ====
        var pto = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_PaidLeave.asset", "带薪年假批准函", ItemType.Collectible, Rarity.Legendary, 2, 1, 50000);
        var bns = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_BonusCheck.asset", "年终奖确认函", ItemType.Collectible, Rarity.Legendary, 2, 1, 45000);
        var pro = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_Promotion.asset", "升职推荐信", ItemType.Collectible, Rarity.Epic, 1, 1, 20000);
        var trp = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_Trophy.asset", "年度优秀员工奖杯", ItemType.Collectible, Rarity.Epic, 2, 1, 18000);
        var key = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_MasterKey.asset", "万能门禁卡", ItemType.KeyItem, Rarity.Epic, 1, 1, 0);
        var stk = MkItem("Assets/_Project/ScriptableObjects/Items/SO_Item_StockOption.asset", "股权期权证书", ItemType.Collectible, Rarity.Legendary, 1, 1, 60000);

        // ==== 装备：背包 (GearSlot.Backpack) ====
        MkGear("Assets/_Project/ScriptableObjects/Items/SO_Item_BP_Small.asset", "帆布背包", ItemType.OfficeSupply, Rarity.Common, 2, 1, GearSlot.Backpack, 4, 3, 50);
        MkGear("Assets/_Project/ScriptableObjects/Items/SO_Item_BP_Medium.asset", "电脑双肩包", ItemType.OfficeSupply, Rarity.Uncommon, 3, 1, GearSlot.Backpack, 6, 4, 200);
        MkGear("Assets/_Project/ScriptableObjects/Items/SO_Item_BP_Large.asset", "商务旅行箱", ItemType.OfficeSupply, Rarity.Rare, 3, 2, GearSlot.Backpack, 8, 6, 800);

        // ==== 装备：护甲 (GearSlot.Armor) ====
        MkGear("Assets/_Project/ScriptableObjects/Items/SO_Item_Armor_Vest.asset", "工装马甲", ItemType.OfficeSupply, Rarity.Common, 2, 1, GearSlot.Armor, 0, 0, 100);
        MkGear("Assets/_Project/ScriptableObjects/Items/SO_Item_Armor_Jacket.asset", "防刺夹克", ItemType.OfficeSupply, Rarity.Uncommon, 3, 1, GearSlot.Armor, 0, 0, 400);
        MkGear("Assets/_Project/ScriptableObjects/Items/SO_Item_Armor_Suit.asset", "CEO防弹西装", ItemType.OfficeSupply, Rarity.Rare, 3, 2, GearSlot.Armor, 0, 0, 1500);

        // Build loot tables per container type
        BuildLootTable("Assets/_Project/ScriptableObjects/SO_Loot_Desk.asset", new (string, float, int, int)[] {
            (pp, 0.35f, 3, 15), (pr, 0.2f, 2, 8), (tap, 0.15f, 1, 5), (cf, 0.1f, 1, 3), (usb, 0.08f, 1, 2), (scr, 0.05f, 1, 3), (ban, 0.07f, 1, 2)
        });
        BuildLootTable("Assets/_Project/ScriptableObjects/SO_Loot_Cabinet.asset", new (string, float, int, int)[] {
            (pr, 0.3f, 5, 20), (fil, 0.2f, 1, 2), (ink, 0.15f, 2, 5), (wr, 0.1f, 1, 3), (fin, 0.08f, 1, 1), (rd, 0.07f, 1, 1), (usb, 0.1f, 1, 2)
        });
        BuildLootTable("Assets/_Project/ScriptableObjects/SO_Loot_Supply.asset", new (string, float, int, int)[] {
            (scr, 0.25f, 3, 10), (wr, 0.2f, 2, 6), (mtl, 0.15f, 1, 3), (brd, 0.1f, 1, 2), (cf, 0.15f, 2, 5), (ban, 0.15f, 2, 4)
        });
        BuildLootTable("Assets/_Project/ScriptableObjects/SO_Loot_Safe.asset", new (string, float, int, int)[] {
            (pp, 0.25f, 50, 200), (pto, 0.01f, 1, 1), (bns, 0.01f, 1, 1), (stk, 0.005f, 1, 1), (pen, 0.1f, 1, 1), (pro, 0.08f, 1, 1), (fig, 0.04f, 1, 1), (whi, 0.1f, 1, 1), (cpu, 0.15f, 1, 1), (usb, 0.25f, 1, 3)
        });
        BuildLootTable("Assets/_Project/ScriptableObjects/SO_Loot_Server.asset", new (string, float, int, int)[] {
            (usb, 0.3f, 1, 3), (hdd, 0.2f, 1, 2), (cpu, 0.15f, 1, 1), (gpu, 0.1f, 1, 1), (brd, 0.15f, 1, 3), (wr, 0.1f, 1, 3)
        });

        // Assign loot tables to containers by type
        var containerMap = new System.Collections.Generic.Dictionary<ContainerType, string> {
            {ContainerType.Desk, "Assets/_Project/ScriptableObjects/SO_Loot_Desk.asset"},
            {ContainerType.FilingCabinet, "Assets/_Project/ScriptableObjects/SO_Loot_Cabinet.asset"},
            {ContainerType.SupplyCloset, "Assets/_Project/ScriptableObjects/SO_Loot_Supply.asset"},
            {ContainerType.Safe, "Assets/_Project/ScriptableObjects/SO_Loot_Safe.asset"},
            {ContainerType.ServerRack, "Assets/_Project/ScriptableObjects/SO_Loot_Server.asset"},
        };
        foreach (var lc in Object.FindObjectsOfType<EscapeFromWork.Loot.LootContainer>())
        {
            var so = new SerializedObject(lc);
            var cType = (ContainerType)so.FindProperty("containerType").enumValueIndex;
            containerMap.TryGetValue(cType, out string path);
            if (path != null)
                so.FindProperty("lootTable").objectReferenceValue = AssetDatabase.LoadAssetAtPath<EscapeFromWork.Loot.LootTable>(path);
            so.ApplyModifiedProperties();
        }
        Debug.Log("✅ Loot tables wired per container type");
    }

    static string MkGear(string path, string name, ItemType type, Rarity rarity, int w, int h, GearSlot slot, int bpW, int bpH, int value)
    {
        var p = MkItem(path, name, type, rarity, w, 1, value);
        var so = AssetDatabase.LoadAssetAtPath<ItemData>(p);
        var sso = new SerializedObject(so);
        sso.FindProperty("height").intValue = h;
        sso.FindProperty("gearSlot").enumValueIndex = (int)slot;
        sso.FindProperty("backpackWidth").intValue = bpW;
        sso.FindProperty("backpackHeight").intValue = bpH;
        sso.ApplyModifiedProperties(); EditorUtility.SetDirty(so);
        return p;
    }

    static string MkItem(string path, string name, ItemType type, Rarity rarity, int w, int maxStack, int value)
    {
        var so = AssetDatabase.LoadAssetAtPath<ItemData>(path) ?? ScriptableObject.CreateInstance<ItemData>();
        if (AssetDatabase.LoadAssetAtPath<ItemData>(path) == null) { so.name = System.IO.Path.GetFileNameWithoutExtension(path); AssetDatabase.CreateAsset(so, path); }
        var sso = new SerializedObject(so);
        sso.FindProperty("itemName").stringValue = name;
        sso.FindProperty("itemType").enumValueIndex = (int)type;
        sso.FindProperty("rarity").enumValueIndex = (int)rarity;
        sso.FindProperty("width").intValue = w;
        sso.FindProperty("height").intValue = 1;
        if (w >= 2 && (rarity >= Rarity.Epic || type == ItemType.Collectible)) sso.FindProperty("height").intValue = 2;
        sso.FindProperty("maxStackSize").intValue = maxStack;
        sso.FindProperty("baseValue").intValue = value;
        sso.ApplyModifiedProperties();
        EditorUtility.SetDirty(so);
        return path;
    }

    static void BuildLootTable(string path, (string itemPath, float weight, int min, int max)[] entries)
    {
        var lt = AssetDatabase.LoadAssetAtPath<EscapeFromWork.Loot.LootTable>(path);
        if (lt == null)
        {
            lt = ScriptableObject.CreateInstance<EscapeFromWork.Loot.LootTable>();
            lt.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(lt, path);
        }
        var so = new SerializedObject(lt);
        so.FindProperty("minRolls").intValue = 2;
        so.FindProperty("maxRolls").intValue = 5;
        var e = so.FindProperty("entries");
        e.ClearArray();
        foreach (var (ip, w, min, max) in entries)
            AddLootEntry(e, ip, w, min, max);
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(lt);
    }

    static void AddExtractionPoint(string name, Vector3 pos, bool fireEscape, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = new Vector3(3f, 0.05f, 3f);
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        go.tag = "Respawn"; // reuse existing tag
        var col = go.GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
        var et = go.AddComponent<EscapeFromWork.Core.ExtractionTrigger>();
        var etSO = new SerializedObject(et);
        etSO.FindProperty("useFireEscape").boolValue = fireEscape;
        etSO.ApplyModifiedProperties();
    }

    static void AddLootEntry(SerializedProperty entriesProp, string itemPath, float weight, int minCount, int maxCount)
    {
        int idx = entriesProp.arraySize;
        entriesProp.InsertArrayElementAtIndex(idx);
        var entry = entriesProp.GetArrayElementAtIndex(idx);
        entry.FindPropertyRelative("item").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ItemData>(itemPath);
        entry.FindPropertyRelative("weight").floatValue = weight;
        entry.FindPropertyRelative("minCount").intValue = minCount;
        entry.FindPropertyRelative("maxCount").intValue = maxCount;
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
