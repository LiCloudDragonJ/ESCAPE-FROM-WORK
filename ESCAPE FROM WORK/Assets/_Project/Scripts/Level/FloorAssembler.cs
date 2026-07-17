using System.Collections.Generic;
using UnityEngine;
using EscapeFromWork.Core;
using EscapeFromWork.Data;
using EscapeFromWork.Loot;

namespace EscapeFromWork.Level
{
    /// <summary>
    /// Runtime-safe floor assembler. Takes a <see cref="FloorLayoutData"/> produced by
    /// <see cref="FloorBuilder"/> and creates the corresponding GameObjects in the scene.
    ///
    /// <para>Works both at runtime (called by <see cref="FloorManager"/>) and in the
    /// editor (called by <see cref="SceneWirer"/>).</para>
    /// </summary>
    public static class FloorAssembler
    {
        // ── Constants ──────────────────────────────────────────────────────

        const float WallH = 3.5f;
        const float WallT = 0.2f;

        // ── Helpers ────────────────────────────────────────────────────────

        /// <summary>
        /// Destroy a component safely in both edit mode and play mode.
        /// </summary>
        static void SafeDestroy(Object obj)
        {
            if (obj == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                Object.DestroyImmediate(obj);
            else
                SafeDestroy(obj);
#else
            SafeDestroy(obj);
#endif
        }

        // ── Public entry ───────────────────────────────────────────────────

        /// <summary>
        /// Assemble the full floor from layout data. Returns the root GameObject
        /// that parents all floor objects.
        /// </summary>
        public static GameObject Assemble(FloorLayoutData layout)
        {
            var root = new GameObject("--- Floor ---");
            float mw = FloorBuilder.MapW, md = FloorBuilder.MapD;
            var mat = GetLitMaterial();

            var wallMat  = new Material(mat) { color = new Color(0.25f, 0.25f, 0.30f) };
            var glassMat = new Material(mat) { color = new Color(0.60f, 0.75f, 0.85f, 0.5f) };
            var roomColorMap = new Dictionary<RoomType, Color>
            {
                // Public
                { RoomType.Reception,      new Color(0.55f, 0.50f, 0.40f) },
                { RoomType.WaitingArea,     new Color(0.50f, 0.48f, 0.42f) },
                // Semi-public
                { RoomType.ConferenceRoom,  new Color(0.55f, 0.45f, 0.55f) },
                { RoomType.TeaRoom,         new Color(0.45f, 0.55f, 0.45f) },
                // Workspace
                { RoomType.OpenOffice,      new Color(0.50f, 0.48f, 0.42f) },
                { RoomType.PrivateOffice,   new Color(0.48f, 0.45f, 0.40f) },
                { RoomType.ExecutiveOffice, new Color(0.40f, 0.35f, 0.25f) },
                // Secure
                { RoomType.ServerRoom,      new Color(0.20f, 0.20f, 0.35f) },
                { RoomType.ArchiveRoom,     new Color(0.35f, 0.32f, 0.28f) },
                { RoomType.StorageRoom,     new Color(0.40f, 0.40f, 0.38f) },
                { RoomType.VaultRoom,       new Color(0.25f, 0.22f, 0.18f) },
                // Facility
                { RoomType.Stairwell,       new Color(0.40f, 0.40f, 0.45f) },
                { RoomType.ElevatorLobby,   new Color(0.42f, 0.42f, 0.48f) },
                { RoomType.Hallway,         new Color(0.35f, 0.35f, 0.40f) },
                { RoomType.Restroom,        new Color(0.45f, 0.50f, 0.55f) },
                // Core
                { RoomType.CoreStairs,      new Color(0.40f, 0.40f, 0.45f) },
                { RoomType.CoreElevator,    new Color(0.38f, 0.38f, 0.42f) },
                { RoomType.CoreRestroom,    new Color(0.45f, 0.50f, 0.55f) },
                { RoomType.CoreMechanical,  new Color(0.30f, 0.30f, 0.32f) },
            };

            // Ground.
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.position = new Vector3(mw / 2f, -0.5f, md / 2f);
            ground.transform.localScale = new Vector3(mw + 20f, 0.5f, md + 20f);
            ground.GetComponent<MeshRenderer>().sharedMaterial =
                new Material(mat) { color = new Color(0.12f, 0.12f, 0.14f) };
            SafeDestroy(ground.GetComponent<Collider>());

            // Exterior walls.
            CreateWall("Wall_N", new Vector3(mw / 2f, WallH / 2f, md),
                       new Vector3(mw + 1f, WallH, WallT), wallMat);
            CreateWall("Wall_S", new Vector3(mw / 2f, WallH / 2f, 0f),
                       new Vector3(mw + 1f, WallH, WallT), wallMat);
            CreateWall("Wall_E", new Vector3(mw, WallH / 2f, md / 2f),
                       new Vector3(WallT, WallH, md + 1f), wallMat);
            CreateWall("Wall_W", new Vector3(0f, WallH / 2f, md / 2f),
                       new Vector3(WallT, WallH, md + 1f), wallMat);

            // Core筒 exterior walls.
            BuildCoreWalls(root, wallMat, layout);

            // Rooms.
            foreach (var room in layout.rooms)
                BuildRoom(room, root, mat, wallMat, glassMat, roomColorMap, layout);

            // Desk clusters.
            var lootHolder = new GameObject("--- Loot ---");
            int totalDesks = 0;
            var deskMat = new Material(mat) { color = new Color(0.35f, 0.65f, 0.95f) };
            foreach (var cluster in layout.deskClusters)
                totalDesks += BuildDeskCluster(cluster, lootHolder, deskMat, mat);

            // Filing cabinets.
            int cabCount = Mathf.RoundToInt(layout.deskClusters.Count * 0.5f);
            var cabMat = new Material(mat) { color = new Color(0.55f, 0.55f, 0.65f) };
            var blockedRects = new List<Rect>();
            foreach (var room in layout.rooms)
                blockedRects.Add(new Rect(room.worldPos.x, room.worldPos.y, room.size.x, room.size.y));

            for (int i = 0; i < cabCount; i++)
            {
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
                clc.ContainerType = ContainerType.FilingCabinet;
            }

            // Columns.
            var colMat = new Material(mat) { color = new Color(0.7f, 0.7f, 0.7f) };
            foreach (var col in layout.columns)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                c.name = "Column"; c.tag = "Furniture";
                c.transform.parent = root.transform;
                c.transform.position = new Vector3(col.x, WallH / 2f, col.y);
                c.transform.localScale = new Vector3(0.8f, WallH / 2f, 0.8f);
                c.GetComponent<MeshRenderer>().sharedMaterial = colMat;
                c.isStatic = true;
            }

            // Partitions.
            foreach (var part in layout.partitions)
                BuildPartition(part, root, mat);

            // High-value zone marker.
            if (layout.highValueZonePos != Vector2.zero)
            {
                var hvMat = new Material(mat) { color = new Color(0.9f, 0.7f, 0.1f, 0.6f) };
                var hvMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                hvMarker.name = $"HighValue_{layout.highValueZoneType}";
                hvMarker.transform.position = new Vector3(layout.highValueZonePos.x, 0.1f, layout.highValueZonePos.y);
                hvMarker.transform.localScale = new Vector3(2f, 0.05f, 2f);
                hvMarker.GetComponent<MeshRenderer>().sharedMaterial = hvMat;
                SafeDestroy(hvMarker.GetComponent<Collider>());
            }

            // Luxury tea bar (every 5 floors).
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
                tlc.ContainerType = ContainerType.Desk;
            }

            // Extraction points.
            var extractMat = new Material(mat) { color = new Color(0.2f, 0.9f, 0.3f) };
            AddMarker("EntryPoint", layout.entryPos, new Material(mat) { color = Color.cyan });
            AddExtractionPoint("ExtractPoint", layout.extractPos, extractMat);

            return root;
        }

        // ════════════════════════════════════════════════════════════════════
        //  Core筒 exterior walls
        // ════════════════════════════════════════════════════════════════════

        public static void BuildCoreWalls(GameObject parent, Material wallMat, FloorLayoutData layout)
        {
            float cx = FloorBuilder.CoreX, cz = FloorBuilder.CoreZ;
            float cw = FloorBuilder.CoreW, cd = FloorBuilder.CoreD;
            float wh = WallH, wt = WallT;

            float teaCenterX = cx + cw / 2f;
            float doorW = 3f;
            float nLeft = cx, nRight = teaCenterX - doorW / 2f;
            if (nRight > nLeft)
                CreateWall("Core_Wall_NL", new Vector3((nLeft + nRight) / 2f, wh / 2f, cz + cd),
                           new Vector3(nRight - nLeft, wh, wt), wallMat);
            float nLeft2 = teaCenterX + doorW / 2f, nRight2 = cx + cw;
            if (nRight2 > nLeft2)
                CreateWall("Core_Wall_NR", new Vector3((nLeft2 + nRight2) / 2f, wh / 2f, cz + cd),
                           new Vector3(nRight2 - nLeft2, wh, wt), wallMat);

            float elevCenterX = cx + cw / 2f;
            float sLeft = cx, sRight = elevCenterX - doorW / 2f;
            if (sRight > sLeft)
                CreateWall("Core_Wall_SL", new Vector3((sLeft + sRight) / 2f, wh / 2f, cz),
                           new Vector3(sRight - sLeft, wh, wt), wallMat);
            float sLeft2 = elevCenterX + doorW / 2f, sRight2 = cx + cw;
            if (sRight2 > sLeft2)
                CreateWall("Core_Wall_SR", new Vector3((sLeft2 + sRight2) / 2f, wh / 2f, cz),
                           new Vector3(sRight2 - sLeft2, wh, wt), wallMat);

            float eMidZ = cz + cd / 2f;
            float eBot = cz, eTop = eMidZ - doorW / 2f;
            if (eTop > eBot)
                CreateWall("Core_Wall_EB", new Vector3(cx + cw, wh / 2f, (eBot + eTop) / 2f),
                           new Vector3(wt, wh, eTop - eBot), wallMat);
            float eBot2 = eMidZ + doorW / 2f, eTop2 = cz + cd;
            if (eTop2 > eBot2)
                CreateWall("Core_Wall_ET", new Vector3(cx + cw, wh / 2f, (eBot2 + eTop2) / 2f),
                           new Vector3(wt, wh, eTop2 - eBot2), wallMat);

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

        public static void BuildRoom(RoomDef room, GameObject parent, Material mat,
                                      Material wallMat, Material glassMat,
                                      Dictionary<RoomType, Color> colorMap,
                                      FloorLayoutData layout)
        {
            float x = room.worldPos.x, z = room.worldPos.y;
            float w = room.size.x, d = room.size.y;
            float wh = WallH;
            float wt = WallT;

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
            if (isRingCorridor)
                rc = new Color(0.3f, 0.32f, 0.35f);
            rf.GetComponent<MeshRenderer>().sharedMaterial =
                new Material(wallMat) { color = rc };
            SafeDestroy(rf.GetComponent<Collider>());

            if (isRingCorridor) return;

            // Core interior rooms: skip walls.
            bool isCoreInterior = x >= FloorBuilder.CoreX - 0.1f &&
                                  x + w <= FloorBuilder.CoreX + FloorBuilder.CoreW + 0.1f &&
                                  z >= FloorBuilder.CoreZ - 0.1f &&
                                  z + d <= FloorBuilder.CoreZ + FloorBuilder.CoreD + 0.1f;
            if (isCoreInterior) return;

            // Wall material.
            Material roomWallMat = room.roomType switch
            {
                RoomType.ConferenceRoom => glassMat,
                _ => wallMat
            };

            bool isFireEscape = room.roomType == RoomType.Stairwell &&
                                (x < 5f || x > FloorBuilder.MapW - 8f || z < 5f || z > FloorBuilder.MapD - 8f);

            float tw = room.roomType == RoomType.ConferenceRoom ? FloorBuilder.GlassT : wt;

            // Door side logic.
            int doorSide;
            float doorWidth = 2.5f;
            if (isFireEscape)
            {
                if (x < 5f) doorSide = 2;
                else if (x > FloorBuilder.MapW - 8f) doorSide = 3;
                else if (z < 5f) doorSide = 0;
                else doorSide = 1;
            }
            else if (room.roomType == RoomType.ConferenceRoom || room.roomType == RoomType.TeaRoom)
            {
                float rx = x + w / 2f, rz = z + d / 2f;
                float coreCX = FloorBuilder.CoreX + FloorBuilder.CoreW / 2f;
                float coreCZ = FloorBuilder.CoreZ + FloorBuilder.CoreD / 2f;
                float dx = rx - coreCX, dz = rz - coreCZ;
                doorSide = Mathf.Abs(dz) > Mathf.Abs(dx) ? (dz > 0 ? 1 : 0) : (dx > 0 ? 3 : 2);
            }
            else
            {
                float rx = x + w / 2f, rz = z + d / 2f;
                float mapCX = FloorBuilder.MapW / 2f, mapCZ = FloorBuilder.MapD / 2f;
                float dx = rx - mapCX, dz = rz - mapCZ;
                doorSide = Mathf.Abs(dz) > Mathf.Abs(dx) ? (dz > 0 ? 1 : 0) : (dx > 0 ? 3 : 2);
            }

            bool atNorthEdge = z + d >= FloorBuilder.MapD - 0.1f;
            bool atSouthEdge = z <= 0.1f;
            bool atEastEdge  = x + w >= FloorBuilder.MapW - 0.1f;
            bool atWestEdge  = x <= 0.1f;

            // Skip walls that face another room (adjacent rooms share walls).
            if (atNorthEdge || WallFacesRoom(x, z + d, w, true, 0.5f)) { /* skip */ }
            else if (doorSide != 0)
                CreateWallSeg($"R_N", x, z + d, w, wh, tw, true, roomWallMat);
            else
                CreateWallWithGap($"R_N", x, z + d, w, wh, tw, x + w / 2f, doorWidth, true, roomWallMat);

            if (atSouthEdge || WallFacesRoom(x, z, w, true, 0.5f)) { /* skip */ }
            else if (doorSide != 1)
                CreateWallSeg($"R_S", x, z, w, wh, tw, true, roomWallMat);
            else
                CreateWallWithGap($"R_S", x, z, w, wh, tw, x + w / 2f, doorWidth, true, roomWallMat);

            if (atEastEdge || WallFacesRoom(x + w, z, d, false, 0.5f)) { /* skip */ }
            else if (doorSide != 2)
                CreateWallSeg($"R_E", x + w, z, d, wh, tw, false, roomWallMat);
            else
                CreateWallWithGap($"R_E", x + w, z, d, wh, tw, z + d / 2f, doorWidth, false, roomWallMat);

            if (atWestEdge || WallFacesRoom(x, z, d, false, 0.5f)) { /* skip */ }
            else if (doorSide != 3)
                CreateWallSeg($"R_W", x, z, d, wh, tw, false, roomWallMat);
            else
                CreateWallWithGap($"R_W", x, z, d, wh, tw, z + d / 2f, doorWidth, false, roomWallMat);

            // ── Furniture ──────────────────────────────────────────────────
            if (room.roomType == RoomType.Reception)
            {
                // Long front desk.
                var desk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                desk.name = "ReceptionDesk"; desk.transform.SetParent(parent.transform);
                desk.transform.position = new Vector3(x + w / 2f, 0.5f, z + d * 0.3f);
                desk.transform.localScale = new Vector3(w * 0.8f, 1f, 0.8f);
                desk.GetComponent<MeshRenderer>().sharedMaterial =
                    new Material(mat) { color = new Color(0.5f, 0.4f, 0.3f) };
                SafeDestroy(desk.GetComponent<Collider>());
            }
            if (room.roomType == RoomType.ConferenceRoom)
            {
                var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
                table.name = "ConfTable"; table.transform.SetParent(parent.transform);
                table.transform.position = new Vector3(x + w / 2f, 0.4f, z + d / 2f);
                table.transform.localScale = new Vector3(w * 0.6f, 0.15f, d * 0.5f);
                table.GetComponent<MeshRenderer>().sharedMaterial =
                    new Material(mat) { color = new Color(0.4f, 0.3f, 0.2f) };
                SafeDestroy(table.GetComponent<Collider>());
            }
            if (room.roomType == RoomType.TeaRoom)
            {
                var counter = GameObject.CreatePrimitive(PrimitiveType.Cube);
                counter.name = "TeaCounter"; counter.transform.SetParent(parent.transform);
                counter.transform.position = new Vector3(x + w / 2f, 0.5f, z + d * 0.7f);
                counter.transform.localScale = new Vector3(w * 0.7f, 1f, 0.6f);
                counter.GetComponent<MeshRenderer>().sharedMaterial =
                    new Material(mat) { color = new Color(0.6f, 0.55f, 0.45f) };
                SafeDestroy(counter.GetComponent<Collider>());
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
                    SafeDestroy(rack.GetComponent<Collider>());
                }
            }
            if (room.roomType == RoomType.ExecutiveOffice)
            {
                // CEO desk + wine cabinet.
                var eDesk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                eDesk.name = "CEODeck"; eDesk.transform.SetParent(parent.transform);
                eDesk.transform.position = new Vector3(x + w * 0.6f, 0.5f, z + d / 2f);
                eDesk.transform.localScale = new Vector3(2.5f, 0.8f, 1.2f);
                eDesk.GetComponent<MeshRenderer>().sharedMaterial =
                    new Material(mat) { color = new Color(0.25f, 0.15f, 0.05f) };
                SafeDestroy(eDesk.GetComponent<Collider>());
            }
            if (room.roomType == RoomType.VaultRoom)
            {
                // Large safe in centre.
                var safe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                safe.name = "VaultSafe"; safe.transform.SetParent(parent.transform);
                safe.transform.position = new Vector3(x + w / 2f, 1f, z + d / 2f);
                safe.transform.localScale = new Vector3(2f, 2f, 2f);
                safe.GetComponent<MeshRenderer>().sharedMaterial =
                    new Material(mat) { color = new Color(0.2f, 0.2f, 0.2f) };
                safe.tag = "Loot";
                var slc = safe.AddComponent<LootContainer>();
                slc.ContainerType = ContainerType.Safe;
            }
            if (room.roomType == RoomType.ArchiveRoom)
            {
                // Filing cabinet maze — two rows.
                for (int r = 0; r < 4; r++)
                {
                    var cab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cab.name = $"ArchiveCab_{r}"; cab.transform.SetParent(parent.transform);
                    cab.transform.position = new Vector3(x + 1.5f + r * 3f, 0.9f, z + d / 2f);
                    cab.transform.localScale = new Vector3(0.6f, 1.8f, d * 0.5f);
                    cab.GetComponent<MeshRenderer>().sharedMaterial =
                        new Material(mat) { color = new Color(0.35f, 0.3f, 0.25f) };
                    cab.tag = "Loot";
                    var alc = cab.AddComponent<LootContainer>();
                    alc.ContainerType = ContainerType.FilingCabinet;
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  Desk clusters
        // ════════════════════════════════════════════════════════════════════

        public static int BuildDeskCluster(DeskClusterDef cluster, GameObject parent,
                                            Material deskMat, Material mat)
        {
            int count = 0;
            float cx = cluster.center.x, cz = cluster.center.y;
            float halfW = cluster.size.x / 2f, halfD = cluster.size.y / 2f;
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
                    dlc.ContainerType = ContainerType.Desk;
                    count++;
                }
            }
            return count;
        }

        // ════════════════════════════════════════════════════════════════════
        //  Partitions
        // ════════════════════════════════════════════════════════════════════

        public static void BuildPartition(PartitionDef part, GameObject parent, Material mat)
        {
            Vector2 dir = part.end - part.start;
            float len = dir.magnitude;
            if (len < 0.1f) return;
            Vector2 mid = (part.start + part.end) / 2f;

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
        //  Extraction / markers
        // ════════════════════════════════════════════════════════════════════

        public static void AddExtractionPoint(string name, Vector2 pos, Material mat)
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

        public static void AddMarker(string name, Vector2 pos, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = name;
            go.transform.position = new Vector3(pos.x, 0.05f, pos.y);
            go.transform.localScale = new Vector3(0.6f, 1f, 0.6f);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            SafeDestroy(go.GetComponent<Collider>());
        }

        // ════════════════════════════════════════════════════════════════════
        //  Wall helpers
        // ════════════════════════════════════════════════════════════════════

        public static Material GetLitMaterial()
        {
            var s = Shader.Find("Universal Render Pipeline/Lit")
                 ?? Shader.Find("URP/Lit")
                 ?? Shader.Find("Standard");
            return new Material(s);
        }

        public static void CreateWall(string name, Vector3 pos, Vector3 scale, Material mat)
        {
            float mw = FloorBuilder.MapW, md = FloorBuilder.MapD;
            float halfX = scale.x / 2f, halfZ = scale.z / 2f;
            pos.x = Mathf.Clamp(pos.x, halfX, mw - halfX);
            pos.z = Mathf.Clamp(pos.z, halfZ, md - halfZ);
            float maxExtentX = Mathf.Min(pos.x + halfX, mw) - Mathf.Max(pos.x - halfX, 0);
            float maxExtentZ = Mathf.Min(pos.z + halfZ, md) - Mathf.Max(pos.z - halfZ, 0);
            scale.x = Mathf.Min(scale.x, maxExtentX);
            scale.z = Mathf.Min(scale.z, maxExtentZ);
            if (scale.x <= 0.01f || scale.z <= 0.01f) return;

            var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
            w.name = name;
            w.transform.position = pos;
            w.transform.localScale = scale;
            w.GetComponent<MeshRenderer>().sharedMaterial = mat;
            w.isStatic = true;
        }

        public static void CreateWallSeg(string name, float alongStart, float acrossPos,
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

        public static void CreateWallWithGap(string name, float alongStart, float acrossPos,
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
    }
}
