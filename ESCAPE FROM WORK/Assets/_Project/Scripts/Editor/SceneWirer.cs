using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using EscapeFromWork.Player;
using EscapeFromWork.Weapons;
using EscapeFromWork.Data;
using System.Collections.Generic;
using EscapeFromWork.Enemies;
using EscapeFromWork.Level;
using EscapeFromWork.Loot;
using EscapeFromWork.Core;

/// <summary>
/// One-click scene builder. Builds floor, enemies, loot, player, camera,
/// HUD, extraction points, columns, partitions, and high-value zones.
/// Delegates HUD / loot / weapon wiring to partial-class files.
/// </summary>
public static partial class SceneWirer
{
    // ── Materials ──────────────────────────────────────────────────────

    const float FloorY    = 0f;
    const float FloorH    = 0.2f;

    // ── Menu items ─────────────────────────────────────────────────────

    [MenuItem("ESCAPE FROM WORK/Build Scene (30F)")]
    public static void BuildScene() => BuildFloor(30);

    [MenuItem("ESCAPE FROM WORK/Build Floor 50F")]
    public static void Build50() => BuildFloor(50);
    [MenuItem("ESCAPE FROM WORK/Build Floor 27F")]
    public static void Build27() => BuildFloor(27);
    [MenuItem("ESCAPE FROM WORK/Build Floor 15F")]
    public static void Build15() => BuildFloor(15);
    [MenuItem("ESCAPE FROM WORK/Build Floor 5F")]
    public static void Build5()  => BuildFloor(5);
    [MenuItem("ESCAPE FROM WORK/Build Floor 1F")]
    public static void Build1()  => BuildFloor(1);

    // ── Main build method ──────────────────────────────────────────────

    static void BuildFloor(int floorNumber)
    {
        // Clean slate.
        foreach (var t in Object.FindObjectsOfType<Transform>())
        {
            if (t == null || t.gameObject == null) continue;
            if (t.parent != null) continue;
            if (t.gameObject.scene.name != SceneManager.GetActiveScene().name) continue;
            Object.DestroyImmediate(t.gameObject);
        }

        var layout = FloorBuilder.Build(floorNumber);
        float mw = FloorBuilder.MapW;
        float md = FloorBuilder.MapD;

        var mat     = GetLitMaterial();
        var wallMat = new Material(mat) { color = new Color(0.25f, 0.25f, 0.30f) };
        var glassMat = new Material(mat) { color = new Color(0.60f, 0.75f, 0.85f, 0.5f) };
        var floorHolder = new GameObject("--- Floor ---");

        // ── Ground ─────────────────────────────────────────────────────
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.position = new Vector3(mw / 2f, -0.5f, md / 2f);
        ground.transform.localScale = new Vector3(mw + 20f, 0.5f, md + 20f);
        ground.GetComponent<MeshRenderer>().sharedMaterial =
            new Material(mat) { color = new Color(0.12f, 0.12f, 0.14f) };
        Object.DestroyImmediate(ground.GetComponent<Collider>());

        // ── Exterior walls ─────────────────────────────────────────────
        CreateWall("Wall_N", new Vector3(mw / 2f, WallH / 2f, md),
                   new Vector3(mw + 1f, WallH, WallT), wallMat);
        CreateWall("Wall_S", new Vector3(mw / 2f, WallH / 2f, 0f),
                   new Vector3(mw + 1f, WallH, WallT), wallMat);
        CreateWall("Wall_E", new Vector3(mw, WallH / 2f, md / 2f),
                   new Vector3(WallT, WallH, md + 1f), wallMat);
        CreateWall("Wall_W", new Vector3(0f, WallH / 2f, md / 2f),
                   new Vector3(WallT, WallH, md + 1f), wallMat);

        // ── Core筒 exterior walls (with door gaps for ring corridor) ───
        BuildCoreWalls(floorHolder, wallMat, layout);

        // ── Build rooms ────────────────────────────────────────────────
        var roomColorMap = new Dictionary<RoomType, Color>
        {
            { RoomType.Stairwell,      new Color(0.40f, 0.40f, 0.45f) },
            { RoomType.TeaRoom,        new Color(0.45f, 0.55f, 0.45f) },
            { RoomType.ConferenceRoom, new Color(0.55f, 0.45f, 0.55f) },
            { RoomType.ServerRoom,     new Color(0.20f, 0.20f, 0.35f) },
            { RoomType.Office,         new Color(0.50f, 0.48f, 0.42f) },
            { RoomType.Hallway,        new Color(0.35f, 0.35f, 0.40f) },
        };

        foreach (var room in layout.rooms)
        {
            BuildRoom(room, floorHolder, mat, wallMat, glassMat, roomColorMap, layout);
        }

        // ── Desk clusters ──────────────────────────────────────────────
        var lootHolder = new GameObject("--- Loot ---");
        int totalDesks = 0;
        var deskMat = new Material(mat) { color = new Color(0.35f, 0.65f, 0.95f) };
        foreach (var cluster in layout.deskClusters)
        {
            totalDesks += BuildDeskCluster(cluster, lootHolder, deskMat, mat);
        }

        // ── Filing cabinets ────────────────────────────────────────────
        int cabCount = Mathf.RoundToInt(layout.deskClusters.Count * 0.5f);
        var cabMat = new Material(mat) { color = new Color(0.55f, 0.55f, 0.65f) };
        for (int i = 0; i < cabCount; i++)
        {
            var blockedRects = new List<Rect>();
            foreach (var room in layout.rooms)
                blockedRects.Add(new Rect(room.worldPos.x, room.worldPos.y, room.size.x, room.size.y));

            float cx = Random.Range(3f, mw - 3f);
            float cz = Random.Range(3f, md - 3f);
            bool blocked = false;
            foreach (var br in blockedRects)
                if (cx > br.xMin - 1f && cx < br.xMax + 1f && cz > br.yMin - 1f && cz < br.yMax + 1f)
                    { blocked = true; break; }
            if (blocked) continue;

            var cab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cab.name = $"Cab_{i}"; cab.tag = "Loot";
            cab.transform.parent = lootHolder.transform;
            cab.transform.position = new Vector3(cx, 0.75f, cz);
            cab.transform.localScale = new Vector3(1f, 1.5f, 0.6f);
            cab.GetComponent<MeshRenderer>().sharedMaterial = cabMat;
            var clc = cab.AddComponent<LootContainer>();
            var clcSO = new SerializedObject(clc);
            clcSO.FindProperty("containerType").enumValueIndex = (int)ContainerType.FilingCabinet;
            clcSO.ApplyModifiedProperties();
        }

        // ── Columns ────────────────────────────────────────────────────
        var colMat = new Material(mat) { color = new Color(0.7f, 0.7f, 0.7f) };
        foreach (var col in layout.columns)
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            c.name = "Column"; c.transform.parent = floorHolder.transform;
            c.transform.position = new Vector3(col.x, WallH / 2f, col.y);
            c.transform.localScale = new Vector3(0.8f, WallH / 2f, 0.8f);
            c.GetComponent<MeshRenderer>().sharedMaterial = colMat;
            c.isStatic = true;
        }

        // ── Partition walls ────────────────────────────────────────────
        foreach (var part in layout.partitions)
        {
            BuildPartition(part, floorHolder, mat);
        }

        // ── High-value zone marker ─────────────────────────────────────
        if (layout.highValueZonePos != Vector2.zero)
        {
            var hvMat = new Material(mat) { color = new Color(0.9f, 0.7f, 0.1f, 0.6f) };
            var hvMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hvMarker.name = $"HighValue_{layout.highValueZoneType}";
            hvMarker.transform.position = new Vector3(layout.highValueZonePos.x, 0.1f, layout.highValueZonePos.y);
            hvMarker.transform.localScale = new Vector3(2f, 0.05f, 2f);
            hvMarker.GetComponent<MeshRenderer>().sharedMaterial = hvMat;
            Object.DestroyImmediate(hvMarker.GetComponent<Collider>());
        }

        // ── Luxury tea bar (every 5 floors) ────────────────────────────
        if (layout.hasLuxuryTeaBar)
        {
            float ltx = Random.Range(10f, mw - 10f);
            float ltz = Random.Range(10f, md - 10f);
            var teaBar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            teaBar.name = "LuxuryTeaBar";
            teaBar.transform.position = new Vector3(ltx, 0.5f, ltz);
            teaBar.transform.localScale = new Vector3(5f, 1f, 4f);
            teaBar.GetComponent<MeshRenderer>().sharedMaterial =
                new Material(mat) { color = new Color(0.6f, 0.5f, 0.35f) };
            teaBar.tag = "Loot";
            var tlc = teaBar.AddComponent<LootContainer>();
            var tlcSO = new SerializedObject(tlc);
            tlcSO.FindProperty("containerType").enumValueIndex = (int)ContainerType.Desk;
            tlcSO.ApplyModifiedProperties();
        }

        // ── Extraction points ──────────────────────────────────────────
        var extractMat = new Material(mat) { color = new Color(0.2f, 0.9f, 0.3f) };
        // Entry stairs (player starts here, no extraction trigger).
        AddMarker("EntryPoint", layout.entryPos, new Material(mat) { color = Color.cyan });
        // Extraction stairs (trigger to leave).
        AddExtractionPoint("ExtractPoint", layout.extractPos, extractMat);

        // ── Player ─────────────────────────────────────────────────────
        var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player"; player.tag = "Player";
        player.transform.position = new Vector3(layout.entryPos.x, 1f, layout.entryPos.y + 3f);
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
        player.GetComponent<MeshRenderer>().sharedMaterial =
            new Material(mat) { color = Color.yellow };

        // ── Camera ─────────────────────────────────────────────────────
        var camObj = new GameObject("Main Camera");
        camObj.tag = "MainCamera";
        var cam = camObj.AddComponent<Camera>();
        camObj.AddComponent<AudioListener>();
        var cf = camObj.AddComponent<SimpleCameraFollow>();
        var cfo = new SerializedObject(cf);
        cfo.FindProperty("target").objectReferenceValue = player.transform;
        cfo.FindProperty("boundsMin").vector2Value = new Vector2(-10f, -10f);
        cfo.FindProperty("boundsMax").vector2Value = new Vector2(mw + 10f, md + 10f);
        cfo.ApplyModifiedProperties();

        // ── Lighting ───────────────────────────────────────────────────
        var lightObj = new GameObject("Directional Light");
        var dl = lightObj.AddComponent<Light>();
        dl.type = LightType.Directional;
        dl.intensity = 1.5f;
        dl.shadows = LightShadows.Soft;
        lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // Ambient light.
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.2f, 0.2f, 0.25f);

        // ── Enemies ────────────────────────────────────────────────────
        var enemyHolder = new GameObject("--- Enemies ---");
        var enemyMat = new Material(mat) { color = Color.red };
        for (int i = 0; i < layout.enemyCount; i++)
        {
            var e = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            e.name = $"Enemy_{i}"; e.tag = "Enemy";
            e.transform.parent = enemyHolder.transform;
            e.transform.position = new Vector3(
                Random.Range(3f, mw - 3f), 1f, Random.Range(3f, md - 3f));
            e.transform.localScale = new Vector3(1f, 1.2f, 1f);
            e.GetComponent<MeshRenderer>().sharedMaterial = enemyMat;
            e.AddComponent<KPIZombie>();
        }

        // ── HUD + Managers + Weapons ───────────────────────────────────
        BuildHUD();
        var fmObj = new GameObject("FloorManager");
        fmObj.AddComponent<FloorManager>().floorNumber = floorNumber;
        var gmObj = new GameObject("GameManager");
        gmObj.AddComponent<GameManager>();
        WireWeapons();

        // ── Navigation verification ─────────────────────────────────────
        bool navOk = VerifyNavigation(layout);

        // ── Save scene ─────────────────────────────────────────────────
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log($"[SceneWirer] Floor {floorNumber}: {layout.rooms.Count} rooms, " +
                  $"{totalDesks} desks, {layout.columns.Count} columns, " +
                  $"{layout.enemyCount} enemies, {layout.partitions.Count} partitions, " +
                  $"high-value: {layout.highValueZoneType}, navCheck: {(navOk ? "PASS" : "FAIL")}");
    }

    // ════════════════════════════════════════════════════════════════════
    //  Core筒 exterior walls
    // ════════════════════════════════════════════════════════════════════

    static void BuildCoreWalls(GameObject parent, Material wallMat, FloorLayoutData layout)
    {
        float cx = FloorBuilder.CoreX, cz = FloorBuilder.CoreZ;
        float cw = FloorBuilder.CoreW, cd = FloorBuilder.CoreD;
        float wh = FloorBuilder.WallH, wt = FloorBuilder.WallT;

        // N wall with door to tea room.
        float teaCenterX = cx + cw / 2f;
        float doorW = 3f; // wide enough for player capsule (1m diam)
        float nLeft = cx, nRight = teaCenterX - doorW / 2f;
        if (nRight > nLeft)
            CreateWall("Core_Wall_NL", new Vector3((nLeft + nRight) / 2f, wh / 2f, cz + cd),
                       new Vector3(nRight - nLeft, wh, wt), wallMat);
        float nLeft2 = teaCenterX + doorW / 2f, nRight2 = cx + cw;
        if (nRight2 > nLeft2)
            CreateWall("Core_Wall_NR", new Vector3((nLeft2 + nRight2) / 2f, wh / 2f, cz + cd),
                       new Vector3(nRight2 - nLeft2, wh, wt), wallMat);

        // S wall with door to elevator lobby.
        float elevCenterX = cx + cw / 2f;
        float sLeft = cx, sRight = elevCenterX - doorW / 2f;
        if (sRight > sLeft)
            CreateWall("Core_Wall_SL", new Vector3((sLeft + sRight) / 2f, wh / 2f, cz),
                       new Vector3(sRight - sLeft, wh, wt), wallMat);
        float sLeft2 = elevCenterX + doorW / 2f, sRight2 = cx + cw;
        if (sRight2 > sLeft2)
            CreateWall("Core_Wall_SR", new Vector3((sLeft2 + sRight2) / 2f, wh / 2f, cz),
                       new Vector3(sRight2 - sLeft2, wh, wt), wallMat);

        // E wall with mid-door to ring corridor.
        float eMidZ = cz + cd / 2f;
        float eBot = cz, eTop = eMidZ - doorW / 2f;
        if (eTop > eBot)
            CreateWall("Core_Wall_EB", new Vector3(cx + cw, wh / 2f, (eBot + eTop) / 2f),
                       new Vector3(wt, wh, eTop - eBot), wallMat);
        float eBot2 = eMidZ + doorW / 2f, eTop2 = cz + cd;
        if (eTop2 > eBot2)
            CreateWall("Core_Wall_ET", new Vector3(cx + cw, wh / 2f, (eBot2 + eTop2) / 2f),
                       new Vector3(wt, wh, eTop2 - eBot2), wallMat);

        // W wall with mid-door to ring corridor.
        float wMidZ = cz + cd / 2f;
        float wBot = cz, wTop = wMidZ - doorW / 2f;
        if (wTop > wBot)
            CreateWall("Core_Wall_WB", new Vector3(cx, wh / 2f, (wBot + wTop) / 2f),
                       new Vector3(wt, wh, wTop - wBot), wallMat);
        float wBot2 = wMidZ + doorW / 2f, wTop2 = cz + cd;
        if (wTop2 > wBot2)
            CreateWall("Core_Wall_WT", new Vector3(cx, wh / 2f, (wBot2 + wTop2) / 2f),
                       new Vector3(wt, wh, wTop2 - wBot2), wallMat);
    }

    // ════════════════════════════════════════════════════════════════════
    //  Room builder
    // ════════════════════════════════════════════════════════════════════

    static void BuildRoom(RoomDef room, GameObject parent, Material mat,
                          Material wallMat, Material glassMat,
                          Dictionary<RoomType, Color> colorMap,
                          FloorLayoutData layout)
    {
        float x = room.worldPos.x, z = room.worldPos.y;
        float w = room.size.x, d = room.size.y;
        float wh = FloorBuilder.WallH;
        float wt = FloorBuilder.WallT;

        bool isRingCorridor = room.roomType == RoomType.Hallway &&
                              x >= FloorBuilder.CoreOuterX1 - 0.1f &&
                              x + w <= FloorBuilder.CoreOuterX2 + 0.1f &&
                              z >= FloorBuilder.CoreOuterZ1 - 0.1f &&
                              z + d <= FloorBuilder.CoreOuterZ2 + 0.1f;

        // Floor tile.
        var rf = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rf.name = $"F_{room.roomType}";
        rf.transform.SetParent(parent.transform);
        rf.transform.position = new Vector3(x + w / 2f, 0f, z + d / 2f);
        rf.transform.localScale = new Vector3(w, 0.1f, d);
        colorMap.TryGetValue(room.roomType, out var rc);

        // Ring corridor gets a distinct floor color.
        if (isRingCorridor)
            rc = new Color(0.3f, 0.32f, 0.35f);

        rf.GetComponent<MeshRenderer>().sharedMaterial =
            new Material(wallMat) { color = rc };
        Object.DestroyImmediate(rf.GetComponent<Collider>());

        if (isRingCorridor) return; // ring corridor = no walls, just floor marking

        // Core interior rooms: skip walls — the core筒 exterior walls are enough.
        // Only floor tiles (already built above) for visual distinction.
        bool isCoreInterior = x >= FloorBuilder.CoreX - 0.1f &&
                              x + w <= FloorBuilder.CoreX + FloorBuilder.CoreW + 0.1f &&
                              z >= FloorBuilder.CoreZ - 0.1f &&
                              z + d <= FloorBuilder.CoreZ + FloorBuilder.CoreD + 0.1f;
        if (isCoreInterior) return;

        // ── Walls: choose material based on room type ──────────────────
        Material roomWallMat = room.roomType switch
        {
            RoomType.ConferenceRoom => glassMat,  // glass walls for meeting rooms
            _ => wallMat  // solid walls for everything else
        };

        bool isFireEscape = room.roomType == RoomType.Stairwell &&
                            (x < 5f || x > FloorBuilder.MapW - 8f || z < 5f || z > FloorBuilder.MapD - 8f);

        // Wall thickness depends on whether it's glass.
        float tw = room.roomType == RoomType.ConferenceRoom ? FloorBuilder.GlassT : wt;

        // For fire escape stairwells: door facing inward (toward building center).
        // For other rooms: door on a random side.
        int doorSide;
        float doorWidth = 2.5f;
        if (isFireEscape)
        {
            // Door faces toward the center of the map.
            float cx = x + w / 2f, cz = z + d / 2f;
            if (x < 5f) doorSide = 2;      // West wall → door on east side
            else if (x > FloorBuilder.MapW - 8f) doorSide = 3; // East → west
            else if (z < 5f) doorSide = 0; // South → north
            else doorSide = 1;              // North → south
        }
        else if (room.roomType == RoomType.ConferenceRoom || room.roomType == RoomType.TeaRoom)
        {
            // Meeting rooms and tea rooms face the core筒 (ring corridor).
            float rx = x + w / 2f, rz = z + d / 2f;
            float coreCX = FloorBuilder.CoreX + FloorBuilder.CoreW / 2f;
            float coreCZ = FloorBuilder.CoreZ + FloorBuilder.CoreD / 2f;
            float dx = rx - coreCX, dz = rz - coreCZ;
            if (Mathf.Abs(dz) > Mathf.Abs(dx))
                doorSide = dz > 0 ? 1 : 0; // N of core → door south, S of core → door north
            else
                doorSide = dx > 0 ? 3 : 2; // E of core → door west, W of core → door east
        }
        else
        {
            // All other rooms: door faces toward the map center.
            float rx = x + w / 2f, rz = z + d / 2f;
            float mapCX = FloorBuilder.MapW / 2f, mapCZ = FloorBuilder.MapD / 2f;
            float dx = rx - mapCX, dz = rz - mapCZ;
            if (Mathf.Abs(dz) > Mathf.Abs(dx))
                doorSide = dz > 0 ? 1 : 0; // S of center → door north, N of center → door south
            else
                doorSide = dx > 0 ? 3 : 2; // E of center → door west, W of center → door east
        }

        // Skip exterior-facing walls for rooms on the map boundary
        // (the exterior boundary wall already covers that side).
        bool atNorthEdge = z + d >= FloorBuilder.MapD - 0.1f;
        bool atSouthEdge = z <= 0.1f;
        bool atEastEdge  = x + w >= FloorBuilder.MapW - 0.1f;
        bool atWestEdge  = x <= 0.1f;

        // N wall (z + d).
        if (atNorthEdge) { /* skip — exterior wall */ }
        else if (doorSide != 0)
            CreateWallSeg($"R_N", x, z + d, w, wh, tw, true, mat);
        else
            CreateWallWithGap($"R_N", x, z + d, w, wh, tw, x + w / 2f, doorWidth, true, mat);

        // S wall (z).
        if (atSouthEdge) { /* skip */ }
        else if (doorSide != 1)
            CreateWallSeg($"R_S", x, z, w, wh, tw, true, mat);
        else
            CreateWallWithGap($"R_S", x, z, w, wh, tw, x + w / 2f, doorWidth, true, mat);

        // E wall (x + w).
        if (atEastEdge) { /* skip */ }
        else if (doorSide != 2)
            CreateWallSeg($"R_E", x + w, z, d, wh, tw, false, mat);
        else
            CreateWallWithGap($"R_E", x + w, z, d, wh, tw, z + d / 2f, doorWidth, false, mat);

        // W wall (x).
        if (atWestEdge) { /* skip */ }
        else if (doorSide != 3)
            CreateWallSeg($"R_W", x, z, d, wh, tw, false, mat);
        else
            CreateWallWithGap($"R_W", x, z, d, wh, tw, z + d / 2f, doorWidth, false, mat);

        // ── Furniture ──────────────────────────────────────────────────
        if (room.roomType == RoomType.ConferenceRoom)
        {
            var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "ConfTable"; table.transform.SetParent(parent.transform);
            table.transform.position = new Vector3(x + w / 2f, 0.4f, z + d / 2f);
            table.transform.localScale = new Vector3(w * 0.6f, 0.15f, d * 0.5f);
            table.GetComponent<MeshRenderer>().sharedMaterial =
                new Material(mat) { color = new Color(0.4f, 0.3f, 0.2f) };
            Object.DestroyImmediate(table.GetComponent<Collider>());
        }
        if (room.roomType == RoomType.TeaRoom)
        {
            var counter = GameObject.CreatePrimitive(PrimitiveType.Cube);
            counter.name = "TeaCounter"; counter.transform.SetParent(parent.transform);
            counter.transform.position = new Vector3(x + w / 2f, 0.5f, z + d * 0.7f);
            counter.transform.localScale = new Vector3(w * 0.7f, 1f, 0.6f);
            counter.GetComponent<MeshRenderer>().sharedMaterial =
                new Material(mat) { color = new Color(0.6f, 0.55f, 0.45f) };
            Object.DestroyImmediate(counter.GetComponent<Collider>());
        }
        if (room.roomType == RoomType.ServerRoom)
        {
            for (int r = 0; r < 3; r++)
            {
                var rack = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rack.name = $"Rack_{r}"; rack.transform.SetParent(parent.transform);
                rack.transform.position = new Vector3(x + 1.5f + r * 2.5f, 0.9f, z + d / 2f);
                rack.transform.localScale = new Vector3(0.6f, 1.8f, d * 0.7f);
                rack.GetComponent<MeshRenderer>().sharedMaterial =
                    new Material(mat) { color = new Color(0.15f, 0.15f, 0.2f) };
                Object.DestroyImmediate(rack.GetComponent<Collider>());
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //  Desk clusters
    // ════════════════════════════════════════════════════════════════════

    static int BuildDeskCluster(DeskClusterDef cluster, GameObject parent,
                                Material deskMat, Material mat)
    {
        int count = 0;
        float cx = cluster.center.x, cz = cluster.center.y;
        float halfW = cluster.size.x / 2f, halfD = cluster.size.y / 2f;

        // Place desks in a grid within the cluster bounds.
        float deskW = 1.8f, deskD = 0.8f, deskH = 0.75f;

        int cols = Mathf.Max(1, Mathf.FloorToInt(cluster.size.x / 2.2f));
        int rows = Mathf.Max(1, Mathf.FloorToInt(cluster.size.y / 2.0f));

        for (int r = 0; r < rows && count < cluster.deskCount; r++)
        {
            for (int c = 0; c < cols && count < cluster.deskCount; c++)
            {
                float dx = -halfW + 1.1f + c * 2.2f;
                float dz = -halfD + 1.0f + r * 2.0f;

                var desk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                desk.name = $"Desk_{parent.transform.childCount}"; desk.tag = "Loot";
                desk.transform.parent = parent.transform;
                desk.transform.position = new Vector3(cx + dx, deskH / 2f, cz + dz);
                desk.transform.localScale = new Vector3(deskW, deskH, deskD);
                desk.GetComponent<MeshRenderer>().sharedMaterial = deskMat;

                var dlc = desk.AddComponent<LootContainer>();
                var dlcSO = new SerializedObject(dlc);
                dlcSO.FindProperty("containerType").enumValueIndex = (int)ContainerType.Desk;
                dlcSO.ApplyModifiedProperties();
                count++;
            }
        }
        return count;
    }

    // ════════════════════════════════════════════════════════════════════
    //  Partitions
    // ════════════════════════════════════════════════════════════════════

    static void BuildPartition(PartitionDef part, GameObject parent, Material mat)
    {
        Vector2 dir = part.end - part.start;
        float len = dir.magnitude;
        if (len < 0.1f) return;
        Vector2 mid = (part.start + part.end) / 2f;
        Vector2 norm = new Vector2(-dir.y, dir.x).normalized;

        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Partition"; wall.transform.parent = parent.transform;

        bool isVertical = Mathf.Abs(dir.x) < Mathf.Abs(dir.y);
        float thick = 0.1f;

        wall.transform.position = new Vector3(mid.x, part.height / 2f, mid.y);
        wall.transform.localScale = isVertical
            ? new Vector3(thick, part.height, len)
            : new Vector3(len, part.height, thick);

        Color partColor = part.height > 1.5f
            ? new Color(0.45f, 0.45f, 0.50f)
            : new Color(0.55f, 0.60f, 0.65f);
        wall.GetComponent<MeshRenderer>().sharedMaterial =
            new Material(mat) { color = partColor };
        wall.isStatic = true;
    }

    // ════════════════════════════════════════════════════════════════════
    //  Extraction point
    // ════════════════════════════════════════════════════════════════════

    static void AddExtractionPoint(string name, Vector2 pos, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = new Vector3(pos.x, 0.15f, pos.y);
        go.transform.localScale = new Vector3(6f, 0.3f, 6f);
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        go.isStatic = true;
        var trigger = go.AddComponent<ExtractionTrigger>();
        trigger.name = name;
    }

    static void AddMarker(string name, Vector2 pos, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
        go.name = name;
        go.transform.position = new Vector3(pos.x, 0.05f, pos.y);
        go.transform.localScale = new Vector3(0.6f, 1f, 0.6f);
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        Object.DestroyImmediate(go.GetComponent<Collider>());
    }

    // ════════════════════════════════════════════════════════════════════
    //  Helpers
    // ════════════════════════════════════════════════════════════════════

    const float WallH = 3.5f;
    const float WallT = 0.2f;

    static Material GetLitMaterial()
    {
        var s = Shader.Find("Universal Render Pipeline/Lit")
             ?? Shader.Find("URP/Lit")
             ?? Shader.Find("Standard");
        return new Material(s);
    }

    static void CreateWall(string name, Vector3 pos, Vector3 scale, Material mat)
    {
        // Clamp to map bounds — prevent walls from extending beyond the floor plate.
        float mw = FloorBuilder.MapW, md = FloorBuilder.MapD;
        float halfX = scale.x / 2f, halfZ = scale.z / 2f;

        // Clamp position so wall stays within [0, mw] × [0, md].
        pos.x = Mathf.Clamp(pos.x, halfX, mw - halfX);
        pos.z = Mathf.Clamp(pos.z, halfZ, md - halfZ);

        // Clamp scale if wall would still extend beyond.
        float maxExtentX = Mathf.Min(pos.x + halfX, mw) - Mathf.Max(pos.x - halfX, 0);
        float maxExtentZ = Mathf.Min(pos.z + halfZ, md) - Mathf.Max(pos.z - halfZ, 0);
        scale.x = Mathf.Min(scale.x, maxExtentX);
        scale.z = Mathf.Min(scale.z, maxExtentZ);

        if (scale.x <= 0.01f || scale.z <= 0.01f) return; // too small to matter

        var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
        w.name = name;
        w.transform.position = pos;
        w.transform.localScale = scale;
        w.GetComponent<MeshRenderer>().sharedMaterial = mat;
        w.isStatic = true;
    }

    static void CreateWallSeg(string name, float alongStart, float acrossPos,
                              float length, float height, float thickness,
                              bool isZWall, Material mat)
    {
        float cx = isZWall ? alongStart + length / 2f : acrossPos;
        float cz = isZWall ? acrossPos : alongStart + length / 2f;
        var ws = isZWall
            ? new Vector3(length, height, thickness)
            : new Vector3(thickness, height, length);
        CreateWall(name, new Vector3(cx, height / 2f, cz), ws, mat);
    }

    static void CreateWallWithGap(string name, float alongStart, float acrossPos,
                                  float length, float height, float thickness,
                                  float gapCenter, float gapWidth, bool isZWall,
                                  Material mat)
    {
        float gs = gapCenter - gapWidth / 2f, ge = gapCenter + gapWidth / 2f;
        if (gs > alongStart + 0.01f)
        {
            float sl = gs - alongStart;
            float cx = isZWall ? alongStart + sl / 2f : acrossPos;
            float cz = isZWall ? acrossPos : alongStart + sl / 2f;
            var ws = isZWall
                ? new Vector3(sl, height, thickness)
                : new Vector3(thickness, height, sl);
            CreateWall($"{name}_A", new Vector3(cx, height / 2f, cz), ws, mat);
        }
        if (ge < alongStart + length - 0.01f)
        {
            float sl = (alongStart + length) - ge;
            float sx = isZWall ? ge : acrossPos, sz = isZWall ? acrossPos : ge;
            float cx = isZWall ? sx + sl / 2f : acrossPos;
            float cz = isZWall ? acrossPos : sz + sl / 2f;
            var ws = isZWall
                ? new Vector3(sl, height, thickness)
                : new Vector3(thickness, height, sl);
            CreateWall($"{name}_B", new Vector3(cx, height / 2f, cz), ws, mat);
        }
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

    // ════════════════════════════════════════════════════════════════════
    //  Navigation Verification (BFS grid — checks actual wall colliders)
    // ════════════════════════════════════════════════════════════════════

    static bool VerifyNavigation(FloorLayoutData layout)
    {
        const float res = 0.5f;
        float mw = FloorBuilder.MapW, md = FloorBuilder.MapD;
        int gw = Mathf.CeilToInt(mw / res), gd = Mathf.CeilToInt(md / res);
        bool[,] blocked = new bool[gw, gd];

        float wt = FloorBuilder.WallT;
        float cx = FloorBuilder.CoreX, cz = FloorBuilder.CoreZ;
        float coW = FloorBuilder.CoreW, coD = FloorBuilder.CoreD;

        // Local: mark a rect as blocked on the grid.
        void Block(float x, float z, float w, float d)
        {
            if (w <= 0.001f || d <= 0.001f) return;
            int x0 = Mathf.Clamp(Mathf.FloorToInt(x / res), 0, gw - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((x + w) / res), 0, gw - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt(z / res), 0, gd - 1);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((z + d) / res), 0, gd - 1);
            for (int ix = x0; ix <= x1; ix++)
                for (int iz = z0; iz <= z1; iz++)
                    blocked[ix, iz] = true;
        }

        bool InBounds(int x, int z) => x >= 0 && x < gw && z >= 0 && z < gd;
        int CG(int v, int max) => Mathf.Clamp(v, 0, max - 1);

        // ── Block exterior walls ─────────────────────────────────────
        Block(0f - wt, -wt, mw + wt * 2, wt);          // S
        Block(0f - wt, md, mw + wt * 2, wt);           // N
        Block(0f - wt, -wt, wt, md + wt * 2);          // W
        Block(mw, -wt, wt, md + wt * 2);               // E

        // ── Block core筒 exterior walls (with 3m door gaps) ─────────
        float doorW = 3f, halfDW = doorW / 2f;
        float cMidX = cx + coW / 2f, cMidZ = cz + coD / 2f;

        // N wall
        Block(cx, cz + coD, cMidX - halfDW - cx, wt);
        Block(cMidX + halfDW, cz + coD, (cx + coW) - (cMidX + halfDW), wt);
        // S wall
        Block(cx, cz - wt, cMidX - halfDW - cx, wt);
        Block(cMidX + halfDW, cz - wt, (cx + coW) - (cMidX + halfDW), wt);
        // E wall
        Block(cx + coW, cz, wt, cMidZ - halfDW - cz);
        Block(cx + coW, cMidZ + halfDW, wt, (cz + coD) - (cMidZ + halfDW));
        // W wall
        Block(cx - wt, cz, wt, cMidZ - halfDW - cz);
        Block(cx - wt, cMidZ + halfDW, wt, (cz + coD) - (cMidZ + halfDW));

        // ── Block room walls ────────────────────────────────────────
        foreach (var room in layout.rooms)
        {
            float rx = room.worldPos.x, rz = room.worldPos.y;
            float rw = room.size.x, rd = room.size.y;

            // Skip ring corridor (no walls).
            bool isRingCorridor = room.roomType == RoomType.Hallway &&
                rx >= FloorBuilder.CoreOuterX1 - 0.1f && rx + rw <= FloorBuilder.CoreOuterX2 + 0.1f &&
                rz >= FloorBuilder.CoreOuterZ1 - 0.1f && rz + rd <= FloorBuilder.CoreOuterZ2 + 0.1f;
            if (isRingCorridor) continue;

            // Skip core interior rooms (no walls).
            bool isCoreInterior = rx >= cx - 0.1f && rx + rw <= cx + coW + 0.1f &&
                                  rz >= cz - 0.1f && rz + rd <= cz + coD + 0.1f;
            if (isCoreInterior) continue;

            // Determine which side has the door.
            int doorSide;
            bool isFireEscape = room.roomType == RoomType.Stairwell &&
                (rx < 5f || rx > mw - 8f || rz < 5f || rz > md - 8f);

            if (isFireEscape)
            {
                if (rx < 5f) doorSide = 2;
                else if (rx > mw - 8f) doorSide = 3;
                else if (rz < 5f) doorSide = 0;
                else doorSide = 1;
            }
            else if (room.roomType == RoomType.ConferenceRoom || room.roomType == RoomType.TeaRoom)
            {
                float rcx = rx + rw / 2f, rcz = rz + rd / 2f;
                float dx_ = rcx - cMidX, dz_ = rcz - cMidZ;
                if (Mathf.Abs(dz_) > Mathf.Abs(dx_))
                    doorSide = dz_ > 0 ? 1 : 0;
                else
                    doorSide = dx_ > 0 ? 3 : 2;
            }
            else
            {
                float rcx = rx + rw / 2f, rcz = rz + rd / 2f;
                float dx_ = rcx - mw / 2f, dz_ = rcz - md / 2f;
                if (Mathf.Abs(dz_) > Mathf.Abs(dx_))
                    doorSide = dz_ > 0 ? 1 : 0;
                else
                    doorSide = dx_ > 0 ? 3 : 2;
            }

            float rDoorW = 2.5f, rHalfDW = rDoorW / 2f;
            float rMidX = rx + rw / 2f, rMidZ = rz + rd / 2f;

            // N wall (rz + rd)
            if (doorSide != 0) Block(rx, rz + rd, rw, wt);
            else { Block(rx, rz + rd, rMidX - rHalfDW - rx, wt); Block(rMidX + rHalfDW, rz + rd, (rx + rw) - (rMidX + rHalfDW), wt); }
            // S wall (rz)
            if (doorSide != 1) Block(rx, rz - wt, rw, wt);
            else { Block(rx, rz - wt, rMidX - rHalfDW - rx, wt); Block(rMidX + rHalfDW, rz - wt, (rx + rw) - (rMidX + rHalfDW), wt); }
            // E wall (rx + rw)
            if (doorSide != 2) Block(rx + rw, rz, wt, rd);
            else { Block(rx + rw, rz, wt, rMidZ - rHalfDW - rz); Block(rx + rw, rMidZ + rHalfDW, wt, (rz + rd) - (rMidZ + rHalfDW)); }
            // W wall (rx)
            if (doorSide != 3) Block(rx - wt, rz, wt, rd);
            else { Block(rx - wt, rz, wt, rMidZ - rHalfDW - rz); Block(rx - wt, rMidZ + rHalfDW, wt, (rz + rd) - (rMidZ + rHalfDW)); }
        }

        // ── Block partitions ─────────────────────────────────────────
        foreach (var part in layout.partitions)
        {
            float thick = 0.1f;
            Vector2 dir = part.end - part.start;
            float len = dir.magnitude;
            if (len < 0.1f) continue;
            Vector2 mid = (part.start + part.end) / 2f;
            bool vert = Mathf.Abs(dir.x) < Mathf.Abs(dir.y);
            float bw = vert ? thick : len, bd = vert ? len : thick;
            Block(mid.x - bw / 2f, mid.y - bd / 2f, bw, bd);
        }

        // ── BFS ──────────────────────────────────────────────────────
        var start = new Vector2Int(CG(Mathf.RoundToInt(layout.entryPos.x / res), gw),
                                    CG(Mathf.RoundToInt(layout.entryPos.y / res), gd));
        var end   = new Vector2Int(CG(Mathf.RoundToInt(layout.extractPos.x / res), gw),
                                    CG(Mathf.RoundToInt(layout.extractPos.y / res), gd));
        var tea   = new Vector2Int(CG(Mathf.RoundToInt(layout.teaRoomPos.x / res), gw),
                                    CG(Mathf.RoundToInt(layout.teaRoomPos.y / res), gd));

        var visited = new bool[gw, gd];
        var queue = new System.Collections.Generic.Queue<Vector2Int>();
        int[] dirs = { 0, 1, 0, -1, 0 };

        // Seed from entry neighbors (entry is inside a stairwell — find the door gap).
        bool seeded = false;
        for (int d = 0; d < 4; d++)
        {
            int sx = start.x + dirs[d], sz = start.y + dirs[d + 1];
            if (InBounds(sx, sz) && !blocked[sx, sz])
            { queue.Enqueue(new Vector2Int(sx, sz)); visited[sx, sz] = true; seeded = true; }
        }
        if (!seeded && InBounds(start.x, start.y) && !blocked[start.x, start.y])
        { queue.Enqueue(start); visited[start.x, start.y] = true; seeded = true; }
        if (!seeded)
        {
            Debug.LogError("[NavCheck] Entry completely walled off!");
            return false;
        }

        bool reachedExit = false, reachedTea = false;
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            if (cell == end) reachedExit = true;
            if (cell == tea) reachedTea = true;
            for (int d = 0; d < 4; d++)
            {
                int nx = cell.x + dirs[d], nz = cell.y + dirs[d + 1];
                if (!InBounds(nx, nz) || visited[nx, nz] || blocked[nx, nz]) continue;
                visited[nx, nz] = true;
                queue.Enqueue(new Vector2Int(nx, nz));
            }
        }

        int reachable = 0, total = 0;
        for (int x = 0; x < gw; x++)
            for (int z = 0; z < gd; z++)
            {
                if (!blocked[x, z]) total++;
                if (visited[x, z]) reachable++;
            }
        float cov = total > 0 ? (float)reachable / total * 100f : 0f;

        if (reachedExit)
        {
            Debug.Log($"[NavCheck] PASS — entry→exit reachable, teaRoom:{(reachedTea ? "OK" : "BLOCKED")}, " +
                      $"coverage:{cov:F0}% ({reachable}/{total})");
            return true;
        }
        else
        {
            Debug.LogError($"[NavCheck] FAIL — extraction unreachable! teaRoom:{(reachedTea ? "OK" : "BLOCKED")}, " +
                          $"coverage:{cov:F0}% ({reachable}/{total})");
            return false;
        }
    }

    static int ClampGrid(int v, int max) => Mathf.Clamp(v, 0, max - 1);
}
