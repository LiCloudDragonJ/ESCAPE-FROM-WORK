using System.Collections.Generic;
using UnityEngine;

namespace EscapeFromWork.Level
{
    /// <summary>
    /// Realistic Chinese tech-office floor builder for a stealth extraction shooter.
    ///
    /// Design principles:
    /// - Core筒 (16×12m) is fixed — the structural skeleton of all 50 floors.
    /// - 8 perimeter fire escape stairwells serve as entry/extraction anchors.
    /// - Entry random, extraction = farthest anchor by walking distance.
    /// - 6 layout ARCHETYPES provide fundamentally different spatial skeletons.
    ///   Each archetype has continuous internal parameter variation → no two floors identical.
    /// - Every floor has ONE high-value zone (type varies by floor segment).
    /// </summary>
    public static class FloorBuilder
    {
        // ── Map ──────────────────────────────────────────────────────────
        public const float MapW = 100f;
        public const float MapD = 80f;

        // ── Core筒 (centered, "日" shape, 50 floors UNCHANGED) ───────────
        public const float CoreW = 16f;
        public const float CoreD = 12f;
        public const float CoreX = (MapW - CoreW) / 2f;  // 42
        public const float CoreZ = (MapD - CoreD) / 2f;  // 34
        public static Vector2 CoreCenter => new(CoreX + CoreW / 2f, CoreZ + CoreD / 2f);

        // ── Ring corridor ───────────────────────────────────────────────
        // 2.5m minimum — player dodge is ~2m; corridors narrower than dodge = death trap
        public const float CorridorW = 2.5f;
        public const float BranchCorridorW = 1.8f;
        public const float DeadEndCorridorW = 1.5f;

        public static float CoreOuterX1 => CoreX - CorridorW;
        public static float CoreOuterX2 => CoreX + CoreW + CorridorW;
        public static float CoreOuterZ1 => CoreZ - CorridorW;
        public static float CoreOuterZ2 => CoreZ + CoreD + CorridorW;

        // ── Walls ───────────────────────────────────────────────────────
        public const float WallH  = 3.5f;
        public const float WallT  = 0.2f;
        public const float GlassT = 0.1f;

        // ── Structural ──────────────────────────────────────────────────
        public const float ColSpacing = 12f;

        // ── Fire escape stairwells ──────────────────────────────────────
        const float StairSize = 6f;
        const float StairHalf = StairSize / 2f;

        static readonly Vector2[] AnchorPositions = new Vector2[]
        {
            new Vector2(20f, StairHalf),                // S1 — south
            new Vector2(65f, StairHalf),                // S2
            new Vector2(35f, MapD - StairHalf),         // N1 — north
            new Vector2(75f, MapD - StairHalf),         // N2
            new Vector2(StairHalf, 20f),                // W1 — west
            new Vector2(StairHalf, 55f),                // W2
            new Vector2(MapW - StairHalf, 25f),         // E1 — east
            new Vector2(MapW - StairHalf, 65f),         // E2
        };

        enum AnchorSide { North, South, East, West }

        // ── Layout Archetypes ───────────────────────────────────────────

        public enum FloorArchetype
        {
            RingStandard,  // A: core centered, full ring corridor, meeting belt + exterior offices
            OpenPlan,      // B: core centered, minimal walls, desk sea + partitions
            Cellular,      // C: many small rooms, narrow corridors, core offset
        }

        static FloorArchetype SelectArchetype(int floor)
        {
            int r = (floor * 173 + 97) % 40;
            if (r < 18) return FloorArchetype.RingStandard;  // 45%
            if (r < 30) return FloorArchetype.OpenPlan;      // 30%
            return FloorArchetype.Cellular;                   // 25%
        }

        // ── Core筒 interior sub-rects ───────────────────────────────────

        static Rect StairsA   => new(CoreX + 1f, CoreZ + 1f, 4f, 4f);
        static Rect StairsB   => new(CoreX + CoreW - 5f, CoreZ + CoreD - 5f, 4f, 4f);
        static Rect ElevLobby => new(CoreX + CoreW / 2f - 2.5f, CoreZ + CoreD - 5f, 5f, 4f);
        static Rect RestroomM => new(CoreX + 1f, CoreZ + CoreD - 5f, 3f, 4f);
        static Rect RestroomF => new(CoreX + CoreW - 4f, CoreZ + 1f, 3f, 4f);
        static Rect TeaCore   => new(CoreX + CoreW / 2f - 2f, CoreZ + 1f, 4f, 3f);

        // ════════════════════════════════════════════════════════════════
        //  Public entry
        // ════════════════════════════════════════════════════════════════

        public static FloorLayoutData Build(int floorNumber)
        {
            Random.InitState(floorNumber * 137 + 42);
            int seed = floorNumber * 137 + 42;
            var archetype = SelectArchetype(floorNumber);

            var layout = new FloorLayoutData
            {
                floorNumber     = floorNumber,
                seed            = seed,
                hasLuxuryTeaBar = (floorNumber % 5 == 0),
                archetype       = archetype,
            };

            var rooms   = new List<RoomDef>();
            var blocked = new List<Rect>();

            // ── 0. Archetype-specific blocking zone ─────────────────────
            // Core筒 is always blocked. Ring corridor blocked for A and B, not C.
            if (archetype != FloorArchetype.Cellular)
            {
                var coreOuter = new Rect(CoreOuterX1, CoreOuterZ1,
                                         CoreOuterX2 - CoreOuterX1, CoreOuterZ2 - CoreOuterZ1);
                blocked.Add(coreOuter);
                layout.ringCorridorRect = coreOuter;

                // Ring corridor floor marking (visual only, no walls).
                rooms.Add(new RoomDef
                {
                    roomType = RoomType.Hallway,
                    worldPos = new Vector2(coreOuter.x, coreOuter.y),
                    size     = new Vector2(coreOuter.width, coreOuter.height),
                    doorPos  = Vector2.zero,
                });
            }
            else
            {
                // Cellular: only core筒 is blocked (ring corridor becomes part of the corridor grid).
                blocked.Add(new Rect(CoreX, CoreZ, CoreW, CoreD));
                layout.ringCorridorRect = new Rect(0, 0, 0, 0); // no ring corridor
            }

            // ── 2. Core筒 interior (always fixed) ───────────────────────
            AddCoreInterior(rooms);

            // ── 3. Entry point (fixed north side, near public zone) ────
            // Pick the north anchor closest to the map centre.
            int entryIdx = 2; // N1 (35, 77) — closest to centre
            layout.entryPos = AnchorPositions[entryIdx];

            // ── 4. Fire escape stairwells at ALL 8 anchors ──────────────
            // Extraction: random subset available (count by floor segment).
            int extractCount = floorNumber >= 36 ? SeededRange(floorNumber, 997, 53, 3, 4)
                            : floorNumber >= 16 ? SeededRange(floorNumber, 997, 53, 2, 3)
                            : SeededRange(floorNumber, 997, 53, 1, 2);

            var allAnchors = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7 };
            // Shuffle deterministically.
            for (int i = allAnchors.Count - 1; i > 0; i--)
            {
                int j = Mathf.FloorToInt(SeededFloat(floorNumber, 911 + i, 29 + i, 0f, i + 0.999f));
                (allAnchors[i], allAnchors[j]) = (allAnchors[j], allAnchors[i]);
            }

            int extractIdx = -1;
            var availableExtracts = new List<int>();
            for (int i = 0; i < allAnchors.Count; i++)
            {
                int ai = allAnchors[i];
                if (ai == entryIdx) continue; // entry stairwell is not extraction
                var center = AnchorPositions[ai];
                var stairRect = new Rect(
                    Mathf.Clamp(center.x - StairHalf, 0f, MapW - StairSize),
                    Mathf.Clamp(center.y - StairHalf, 0f, MapD - StairSize),
                    StairSize, StairSize);

                string label;
                if (availableExtracts.Count < extractCount)
                {
                    availableExtracts.Add(ai);
                    if (extractIdx < 0 || Vector2.Distance(AnchorPositions[ai], layout.entryPos) >
                        Vector2.Distance(AnchorPositions[extractIdx], layout.entryPos))
                        extractIdx = ai; // farthest available = primary extraction
                    label = availableExtracts.Count == 1 ? "Extract_Primary" : $"Extract_{availableExtracts.Count}";
                }
                else
                {
                    label = $"Stairs_Locked_{i}";
                }

                rooms.Add(MakeRoom(RoomType.Stairwell, stairRect, label));
                blocked.Add(stairRect);
            }

            if (extractIdx < 0) extractIdx = allAnchors.Find(a => a != entryIdx);
            layout.extractPos = AnchorPositions[extractIdx];

            // ── 4.5 Reception near entry (north side, public zone) ──────
            // Place at the north-centre, just inside the public belt.
            float recX = MapW / 2f - 6f;
            float recZ = MapD - 9f; // inside north public belt
            var receptionRect = new Rect(recX, recZ, 12f, 6f);
            if (!OverlapsAny(receptionRect, blocked))
            {
                rooms.Add(MakeRoom(RoomType.Reception, receptionRect, "Reception"));
                BlockWithMargin(blocked, receptionRect);
            }

            // ── 4.6 Tea room in semi-public zone (Y=60-72, near core) ──
            // Place it adjacent to the core筒 north side.
            float teaX = CoreX + 2f;
            float teaZ = CoreZ + CoreD + CorridorW + 1f;
            var teaRect = new Rect(teaX, teaZ, 6f, 5f);
            if (!OverlapsAny(teaRect, blocked))
            {
                rooms.Add(MakeRoom(RoomType.TeaRoom, teaRect, "TeaRoom"));
                layout.teaRoomPos = new Vector2(teaRect.x + teaRect.width / 2f, teaRect.y + teaRect.height / 2f);
                BlockWithMargin(blocked, teaRect);
            }

            // ── 5. Archetype-specific room generation ───────────────────
            var quadDensity = AssignQuadrants(floorNumber);

            switch (archetype)
            {
                case FloorArchetype.RingStandard:
                    BuildRingStandard(rooms, blocked, floorNumber, quadDensity);
                    break;
                case FloorArchetype.OpenPlan:
                    BuildOpenPlan(rooms, blocked, floorNumber, quadDensity);
                    break;
                case FloorArchetype.Cellular:
                    BuildCellular(rooms, blocked, floorNumber);
                    break;
            }

            // ── 6. High-value zone ──────────────────────────────────────
            AssignHighValueZone(layout, rooms, floorNumber);

            // ── 7. Desk clusters ────────────────────────────────────────
            int deskClusterCount = archetype switch
            {
                FloorArchetype.RingStandard => SeededRange(floorNumber, 359, 67, 10, 24),
                FloorArchetype.OpenPlan     => SeededRange(floorNumber, 359, 67, 18, 32),
                FloorArchetype.Cellular     => SeededRange(floorNumber, 359, 67, 5, 14),
                _ => 12
            };
            layout.deskClusters = GenerateDeskClusters(blocked, deskClusterCount, floorNumber, quadDensity, archetype);

            // ── 8. Columns ──────────────────────────────────────────────
            layout.columns = GenerateColumns(blocked);

            // ── 9. Partitions ───────────────────────────────────────────
            float floorLerp = Mathf.InverseLerp(50f, 2f, floorNumber);
            layout.partitionDensity = archetype switch
            {
                FloorArchetype.OpenPlan => Mathf.Lerp(0.55f, 0.2f, floorLerp),
                FloorArchetype.Cellular => Mathf.Lerp(0.2f, 0.05f, floorLerp),
                _ => Mathf.Lerp(0.7f, 0.15f, floorLerp),
            };
            layout.partitions = GeneratePartitions(layout.deskClusters, layout.partitionDensity, floorNumber);

            // ── 10. Enemy count ─────────────────────────────────────────
            // floorLerp: 50F=0 → 1F≈1. Higher floors = fewer enemies.
            layout.enemyCount = archetype switch
            {
                FloorArchetype.Cellular => Mathf.RoundToInt(Mathf.Lerp(8, 24, floorLerp)),
                _ => Mathf.RoundToInt(Mathf.Lerp(5, 20, floorLerp)),
            };

            // ── 11. Zone-level assignment ──────────────────────────────
            ConfigureZoneLevels(rooms);

            layout.rooms = rooms;
            Debug.Log($"[FloorBuilder] Floor {floorNumber} [{archetype}]: entry={AnchorPositions[entryIdx]} extract={AnchorPositions[extractIdx]}, " +
                      $"{rooms.Count} rooms, {layout.deskClusters.Count} desk clusters, {layout.enemyCount} enemies, " +
                      $"highValue={layout.highValueZoneType}");
            return layout;
        }

        // ════════════════════════════════════════════════════════════════
        //  Archetype A: Ring Standard
        //  Core centered, full ring corridor, meeting belt + exterior offices.
        // ════════════════════════════════════════════════════════════════

        static void BuildRingStandard(List<RoomDef> rooms, List<Rect> blocked,
                                      int floor, QuadDensity[] quadDensity)
        {
            // Meeting rooms in the belt around ring corridor.
            int meetingCount = SeededRange(floor, 199, 89, 4, 10);
            AddMeetingRooms(rooms, blocked, floor, meetingCount, quadDensity);

            // Private offices along exterior walls.
            int officeCount = SeededRange(floor, 307, 91, 4, 12);
            AddExteriorOffices(rooms, blocked, floor, officeCount, quadDensity);

            // Server rooms on IT floors.
            if (floor >= 22 && floor <= 28)
                AddServerRooms(rooms, blocked, floor, SeededRange(floor, 419, 73, 2, 5));
        }

        // ════════════════════════════════════════════════════════════════
        //  Archetype B: Open Plan
        //  Core centered, almost no walled rooms, desk sea + partitions.
        // ════════════════════════════════════════════════════════════════

        static void BuildOpenPlan(List<RoomDef> rooms, List<Rect> blocked,
                                  int floor, QuadDensity[] quadDensity)
        {
            // Very few meeting rooms — 1 to 3, placed near core.
            int meetingCount = SeededRange(floor, 199, 89, 1, 3);
            AddMeetingRooms(rooms, blocked, floor, meetingCount, quadDensity);

            // No private offices — the floor IS the office.

            // Scatter 2-5 phone booth rooms (tiny, 2×2m) for variety.
            int boothCount = SeededRange(floor, 523, 107, 2, 5);
            for (int i = 0; i < boothCount; i++)
            {
                TryPlaceRoom(rooms, blocked, 2f, 2f, RoomType.Office, $"Booth_{i}", 30);
            }

            // 1-3 lounge / tea corners (larger open rooms, good loot).
            int loungeCount = SeededRange(floor, 617, 131, 1, 3);
            for (int i = 0; i < loungeCount; i++)
            {
                float lw = Random.Range(4f, 7f);
                float ld = Random.Range(3f, 5f);
                TryPlaceRoom(rooms, blocked, lw, ld, RoomType.TeaRoom, $"Lounge_{i}", 30);
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  Archetype C: Cellular
        //  Many small rooms, narrow corridors, core offset.
        // ════════════════════════════════════════════════════════════════

        static void BuildCellular(List<RoomDef> rooms, List<Rect> blocked, int floor)
        {
            // Cellular: blocked already contains just the core筒. Fire escape
            // stairwells and core interior rooms are already in rooms + blocked.
            // We just need to fill remaining space with small cell rooms.

            // Narrow corridor width (1.2–1.5m between rooms).
            float corridorW = SeededFloat(floor, 257, 73, 1.2f, 1.5f);

            // Cell count varies by floor.
            int cellCount = SeededRange(floor, 251, 71, 16, 32);

            // Bias direction for denser placement (creates "offset core" feel).
            float biasX = SeededFloat(floor, 701, 43, -1f, 1f);
            float biasZ = SeededFloat(floor, 709, 47, -1f, 1f);
            Vector2 bias = new Vector2(biasX, biasZ).normalized * 10f;

            for (int i = 0; i < cellCount; i++)
            {
                float cw = SeededFloat(floor, 311 + i * 27, 101 + i, 2.5f, 5f);
                float cd = SeededFloat(floor, 317 + i * 29, 103 + i, 2.5f, 5f);

                int attempts = 0;
                bool placed = false;
                while (attempts < 50 && !placed)
                {
                    // Bias toward shifted center for that "offset core" effect.
                    float cx = Random.Range(1f, MapW - cw - 1f);
                    float cz = Random.Range(1f, MapD - cd - 1f);

                    // Gentle bias — prefer the dense side.
                    Vector2 cellCenter = new(cx + cw / 2f, cz + cd / 2f);
                    float distToBias = Vector2.Distance(cellCenter, CoreCenter + bias);
                    if (distToBias > 30f && Random.value < 0.4f) { attempts++; continue; }

                    // Expand by corridor width for spacing check.
                    Rect candidate = new(cx - corridorW, cz - corridorW,
                                         cw + corridorW * 2, cd + corridorW * 2);
                    if (OverlapsAny(candidate, blocked)) { attempts++; continue; }

                    GridSnap(ref cx); GridSnap(ref cz);
                    Rect final = new(cx, cz, cw, cd);
                    if (final.xMin < 0.5f || final.yMin < 0.5f ||
                        final.xMax > MapW - 0.5f || final.yMax > MapD - 0.5f)
                        { attempts++; continue; }

                    BlockWithMargin(blocked, final);

                    // Mix room types: mostly small offices, some meeting rooms.
                    RoomType rt = Random.value < 0.55f ? RoomType.Office
                                : Random.value < 0.75f ? RoomType.ConferenceRoom
                                : RoomType.ServerRoom;
                    rooms.Add(MakeRoom(rt, final, $"Cell_{i}"));
                    placed = true;
                }
                if (!placed && i < cellCount - 3)
                {
                    // If we can't place more rooms, stop early — floor is full.
                    // Only skip a few; if we're close to target, it's fine.
                }
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  Anchor Selection
        // ════════════════════════════════════════════════════════════════

        static (int entry, int extract) SelectAnchors(int maxAttempts = 50)
        {
            int bestEntry = 0, bestExit = 0;
            float bestDist = 0f;
            for (int a = 0; a < maxAttempts; a++)
            {
                int entry = Random.Range(0, AnchorPositions.Length);
                int farthest = -1;
                float farthestDist = 0f;
                for (int i = 0; i < AnchorPositions.Length; i++)
                {
                    if (i == entry) continue;
                    float d = PathDistance(entry, i);
                    if (d > farthestDist) { farthestDist = d; farthest = i; }
                }
                if (farthestDist > bestDist) { bestDist = farthestDist; bestEntry = entry; bestExit = farthest; }
                if (bestDist >= 80f) break;
            }
            return (bestEntry, bestExit);
        }

        static float PathDistance(int a, int b)
        {
            Vector2 pa = AnchorPositions[a], pb = AnchorPositions[b];
            float straight = Vector2.Distance(pa, pb);
            AnchorSide GetSide(int i) => i switch
            {
                0 or 1 => AnchorSide.South, 2 or 3 => AnchorSide.North,
                4 or 5 => AnchorSide.West,  _ => AnchorSide.East
            };
            var sa = GetSide(a); var sb = GetSide(b);
            if (sa == sb) return straight;
            bool opp = (sa == AnchorSide.North && sb == AnchorSide.South) ||
                       (sa == AnchorSide.South && sb == AnchorSide.North) ||
                       (sa == AnchorSide.East  && sb == AnchorSide.West)  ||
                       (sa == AnchorSide.West  && sb == AnchorSide.East);
            return straight + (opp ? 20f : 8f);
        }

        // ════════════════════════════════════════════════════════════════
        //  Core筒 Interior
        // ════════════════════════════════════════════════════════════════

        static void AddCoreInterior(List<RoomDef> rooms)
        {
            rooms.Add(MakeRoom(RoomType.CoreStairs,     StairsA,   "Core_StairsA"));
            rooms.Add(MakeRoom(RoomType.CoreStairs,     StairsB,   "Core_StairsB"));
            rooms.Add(MakeRoom(RoomType.CoreElevator,   ElevLobby, "ElevatorLobby"));
            rooms.Add(MakeRoom(RoomType.CoreRestroom,   RestroomM, "Restroom_M"));
            rooms.Add(MakeRoom(RoomType.CoreRestroom,   RestroomF, "Restroom_F"));
            rooms.Add(MakeRoom(RoomType.CoreMechanical, TeaCore,   "Core_Mechanical"));
        }

        // ════════════════════════════════════════════════════════════════
        //  Quadrant Personality
        // ════════════════════════════════════════════════════════════════

        enum QuadDensity { Sparse, Medium, Dense }

        static QuadDensity[] AssignQuadrants(int floor)
        {
            int rot = (floor * 73) % 4;
            var q = new QuadDensity[] { QuadDensity.Medium, QuadDensity.Dense,
                                        QuadDensity.Sparse, QuadDensity.Medium };
            for (int r = 0; r < rot; r++) { var t = q[3]; q[3] = q[2]; q[2] = q[1]; q[1] = q[0]; q[0] = t; }
            return q;
        }

        static int GetQuadrant(float cx, float cz)
        {
            bool n = cz >= CoreZ + CoreD / 2f, e = cx >= CoreX + CoreW / 2f;
            if (n && !e) return 0; if (n && e) return 1;
            if (!n && !e) return 2; return 3;
        }

        // ════════════════════════════════════════════════════════════════
        //  Meeting Rooms (shared by A and B)
        // ════════════════════════════════════════════════════════════════

        static void AddMeetingRooms(List<RoomDef> rooms, List<Rect> blocked,
                                    int floor, int count, QuadDensity[] quadDensity)
        {
            const float beltDepth = 8f;
            var belts = new (Rect zone, bool horiz)[]
            {
                (new(CoreOuterX1, CoreOuterZ2, CoreOuterX2 - CoreOuterX1, beltDepth), true),   // N
                (new(CoreOuterX1, CoreOuterZ1 - beltDepth, CoreOuterX2 - CoreOuterX1, beltDepth), true), // S
                (new(CoreOuterX2, CoreOuterZ1, beltDepth, CoreOuterZ2 - CoreOuterZ1), false),  // E
                (new(CoreOuterX1 - beltDepth, CoreOuterZ1, beltDepth, CoreOuterZ2 - CoreOuterZ1), false), // W
            };

            for (int i = 0; i < count; i++)
            {
                var belt = belts[i % belts.Length];
                float rw = SeededFloat(floor, 521 + i * 31, 47 + i, 3f, 7f);
                float rd = SeededFloat(floor, 607 + i * 29, 59 + i, 3.5f, 7f);
                for (int a = 0; a < 20; a++)
                {
                    float rx = belt.horiz ? belt.zone.x + Random.Range(0.5f, belt.zone.width - rw - 0.5f)
                                          : belt.zone.x + Random.Range(0f, belt.zone.width - rw);
                    float rz = belt.horiz ? belt.zone.y + Random.Range(0f, belt.zone.height - rd)
                                          : belt.zone.y + Random.Range(0.5f, belt.zone.height - rd - 0.5f);
                    GridSnap(ref rx); GridSnap(ref rz);
                    Rect r = new(rx, rz, rw, rd);
                    if (r.xMin < 0.5f || r.yMin < 0.5f || r.xMax > MapW - 0.5f || r.yMax > MapD - 0.5f) continue;
                    if (OverlapsAny(r, blocked)) continue;
                    BlockWithMargin(blocked, r);
                    rooms.Add(MakeRoom(RoomType.ConferenceRoom, r, $"Meeting_{i}"));
                    break;
                }
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  Exterior Offices (Archetype A only)
        // ════════════════════════════════════════════════════════════════

        static void AddExteriorOffices(List<RoomDef> rooms, List<Rect> blocked,
                                       int floor, int count, QuadDensity[] quadDensity)
        {
            const float offDepth = 4f;
            const float margin  = 1.5f;
            var strips = new (Vector2 origin, float len, bool horiz)[]
            {
                (new(margin, MapD - offDepth), MapW - margin * 2, true),   // N
                (new(margin, 0),              MapW - margin * 2, true),   // S
                (new(MapW - offDepth, margin), MapD - margin * 2, false), // E
                (new(0, margin),              MapD - margin * 2, false), // W
            };

            for (int i = 0; i < count; i++)
            {
                var strip = strips[i % strips.Length];
                float ow = SeededFloat(floor, 701 + i * 37, 83 + i, 2.5f, 5f);
                float od = offDepth;
                for (int a = 0; a < 25; a++)
                {
                    float along = Random.Range(0f, strip.len - (strip.horiz ? ow : od) - margin);
                    float rx = strip.horiz ? strip.origin.x + along : strip.origin.x;
                    float rz = strip.horiz ? strip.origin.y : strip.origin.y + along;
                    float rw = strip.horiz ? ow : od, rd = strip.horiz ? od : ow;
                    GridSnap(ref rx); GridSnap(ref rz);
                    Rect r = new(rx, rz, rw, rd);
                    if (r.xMin < 0.5f || r.yMin < 0.5f || r.xMax > MapW - 0.5f || r.yMax > MapD - 0.5f) continue;
                    if (OverlapsAny(r, blocked)) continue;
                    int q = GetQuadrant(r.center.x, r.center.y);
                    float chance = quadDensity[q] switch { QuadDensity.Dense => 0.9f, QuadDensity.Medium => 0.55f, _ => 0.2f };
                    if (Random.value > chance) continue;
                    BlockWithMargin(blocked, r);
                    rooms.Add(MakeRoom(RoomType.Office, r, $"Office_{i}"));
                    break;
                }
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  Server Rooms
        // ════════════════════════════════════════════════════════════════

        static void AddServerRooms(List<RoomDef> rooms, List<Rect> blocked, int floor, int count)
        {
            for (int i = 0; i < count; i++)
                TryPlaceRoom(rooms, blocked,
                    Random.Range(4f, 8f), Random.Range(5f, 8f),
                    RoomType.ServerRoom, $"Server_{i}", 40);
        }

        // ════════════════════════════════════════════════════════════════
        //  High-Value Zone
        // ════════════════════════════════════════════════════════════════

        static void AssignHighValueZone(FloorLayoutData layout, List<RoomDef> rooms, int floor)
        {
            string zoneType = floor switch
            {
                >= 41 => "Executive Suite",
                >= 29 => "Department Vault",
                >= 22 => "Server Core",
                >= 11 => "Archive Room",
                _     => "Security Armory"
            };
            layout.highValueZoneType = zoneType;

            var candidates = rooms.FindAll(r =>
                r.roomType == RoomType.ConferenceRoom ||
                r.roomType == RoomType.Office ||
                r.roomType == RoomType.ServerRoom);
            if (candidates.Count > 0)
            {
                var chosen = candidates[Random.Range(0, candidates.Count)];
                layout.highValueZonePos = chosen.worldPos + chosen.size / 2f;
            }
            else if (rooms.Count > 0)
            {
                var best = rooms[0];
                foreach (var r in rooms)
                    if (r.size.x * r.size.y > best.size.x * best.size.y) best = r;
                layout.highValueZonePos = best.worldPos + best.size / 2f;
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  Desk Clusters
        // ════════════════════════════════════════════════════════════════

        static List<DeskClusterDef> GenerateDeskClusters(List<Rect> blocked, int count,
                                                         int floor, QuadDensity[] quadDensity,
                                                         FloorArchetype archetype)
        {
            var clusters = new List<DeskClusterDef>();
            for (int i = 0; i < count; i++)
            {
                float cw = SeededFloat(floor, 811 + i * 41, 101 + i, 2.5f, 9f);
                float cd = SeededFloat(floor, 907 + i * 43, 113 + i, 2f, 7f);
                for (int a = 0; a < 30; a++)
                {
                    float cx = Random.Range(2f, MapW - cw - 2f);
                    float cz = Random.Range(2f, MapD - cd - 2f);
                    GridSnap(ref cx); GridSnap(ref cz);
                    Rect r = new(cx, cz, cw, cd);
                    if (OverlapsAny(r, blocked)) continue;
                    int q = GetQuadrant(r.center.x, r.center.y);
                    float chance = quadDensity[q] switch { QuadDensity.Dense => 0.85f, QuadDensity.Medium => 0.5f, _ => 0.25f };
                    if (Random.value > chance) continue;
                    BlockWithMargin(blocked, r);
                    int deskCount = Mathf.Clamp(Mathf.RoundToInt((cw * cd) / 2.8f), 3, 24);
                    clusters.Add(new DeskClusterDef
                    {
                        center = r.center, size = new Vector2(cw, cd),
                        deskCount = deskCount, facing = Random.Range(0, 4) * 90f
                    });
                    break;
                }
            }
            return clusters;
        }

        // ════════════════════════════════════════════════════════════════
        //  Columns
        // ════════════════════════════════════════════════════════════════

        static List<Vector2> GenerateColumns(List<Rect> blocked)
        {
            var cols = new List<Vector2>();
            for (float x = ColSpacing; x < MapW; x += ColSpacing)
            {
                for (float z = ColSpacing; z < MapD; z += ColSpacing)
                {
                    if (x > CoreX && x < CoreX + CoreW && z > CoreZ && z < CoreZ + CoreD) continue;
                    bool b = false;
                    foreach (var r in blocked)
                        if (x > r.xMin + 0.5f && x < r.xMax - 0.5f && z > r.yMin + 0.5f && z < r.yMax - 0.5f)
                            { b = true; break; }
                    if (!b) cols.Add(new Vector2(x, z));
                }
            }
            return cols;
        }

        // ════════════════════════════════════════════════════════════════
        //  Partitions
        // ════════════════════════════════════════════════════════════════

        static List<PartitionDef> GeneratePartitions(List<DeskClusterDef> clusters,
                                                     float density, int floor)
        {
            var parts = new List<PartitionDef>();
            if (clusters.Count < 2) return parts;
            Random.InitState(floor * 503 + 29);
            foreach (var c in clusters)
            {
                if (Random.value > density + 0.3f) continue;
                int segs = Mathf.RoundToInt(Random.Range(1f, 3f) * density);
                for (int s = 0; s < segs; s++)
                {
                    float halfW = c.size.x / 2f - 0.5f, halfD = c.size.y / 2f - 0.5f;
                    bool alongX = Random.value > 0.5f;
                    float len = alongX
                        ? Random.Range(c.size.x * 0.4f, c.size.x * 0.9f)
                        : Random.Range(c.size.y * 0.4f, c.size.y * 0.9f);
                    Vector2 start, end;
                    if (alongX)
                    {
                        float sx = c.center.x - len / 2f;
                        float sz = c.center.y + Random.Range(-halfD, halfD);
                        start = new Vector2(sx, sz); end = new Vector2(sx + len, sz);
                    }
                    else
                    {
                        float sx = c.center.x + Random.Range(-halfW, halfW);
                        float sz = c.center.y - len / 2f;
                        start = new Vector2(sx, sz); end = new Vector2(sx, sz + len);
                    }
                    float highChance = Mathf.Lerp(0.8f, 0.2f, Mathf.InverseLerp(2f, 50f, floor));
                    parts.Add(new PartitionDef { start = start, end = end, height = Random.value < highChance ? 1.8f : 1.2f });
                }
            }
            return parts;
        }

        // ════════════════════════════════════════════════════════════════
        //  Helpers
        // ════════════════════════════════════════════════════════════════

        static int SeededRange(int floor, int mulA, int mulB, int min, int max)
        {
            Random.InitState(floor * mulA + mulB);
            return Random.Range(min, max + 1);
        }

        static float SeededFloat(int floor, int mulA, int mulB, float min, float max)
        {
            Random.InitState(floor * mulA + mulB);
            return Random.Range(min, max);
        }

        static void GridSnap(ref float v) => v = Mathf.Round(v);

        static RoomDef MakeRoom(RoomType type, Rect r, string label = null) => new RoomDef
        {
            roomType = type,
            worldPos = new Vector2(r.x, r.y),
            size     = new Vector2(r.width, r.height),
            doorPos  = new Vector2(r.x + r.width / 2f, r.y + r.height / 2f)
        };

        static void TryPlaceRoom(List<RoomDef> rooms, List<Rect> blocked,
                                 float rw, float rd, RoomType type, string label, int maxAttempts)
        {
            for (int a = 0; a < maxAttempts; a++)
            {
                float rx = Random.Range(2f, MapW - rw - 2f);
                float rz = Random.Range(2f, MapD - rd - 2f);
                GridSnap(ref rx); GridSnap(ref rz);
                Rect r = new(rx, rz, rw, rd);
                if (OverlapsAny(r, blocked)) continue;
                BlockWithMargin(blocked, r);
                rooms.Add(MakeRoom(type, r, label));
                return;
            }
        }

        static bool OverlapsAny(Rect a, List<Rect> rects)
        {
            foreach (var b in rects)
                if (a.xMin < b.xMax && a.xMax > b.xMin && a.yMin < b.yMax && a.yMax > b.yMin)
                    return true;
            return false;
        }

        /// <summary>Margin added around every room's blocked rect to prevent wall clipping.</summary>
        const float BlockMargin = WallT + 0.3f; // wall thickness + clearance

        static void BlockWithMargin(List<Rect> blocked, Rect room)
        {
            blocked.Add(new Rect(
                room.x - BlockMargin, room.y - BlockMargin,
                room.width + BlockMargin * 2f, room.height + BlockMargin * 2f));
        }

        // ════════════════════════════════════════════════════════════════
        //  Zone-level post-processing
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Assign <see cref="ZoneLevel"/> to each room based on its Y position,
        /// creating the public→semi-public→workspace→secure spatial gradient.
        /// </summary>
        static void ConfigureZoneLevels(List<RoomDef> rooms)
        {
            foreach (var room in rooms)
            {
                float cy = room.worldPos.y + room.size.y / 2f; // room centre Y

                // Core interior rooms are always Facility.
                if (room.roomType == RoomType.CoreStairs ||
                    room.roomType == RoomType.CoreElevator ||
                    room.roomType == RoomType.CoreRestroom ||
                    room.roomType == RoomType.CoreMechanical)
                {
                    room.zoneLevel = ZoneLevel.Facility;
                    continue;
                }

                // Stairwells on map edges are Facility.
                if (room.roomType == RoomType.Stairwell)
                {
                    room.zoneLevel = ZoneLevel.Facility;
                    continue;
                }

                // Assign zone by Y band.
                if (cy >= 72f)
                    room.zoneLevel = ZoneLevel.Public;
                else if (cy >= 60f)
                    room.zoneLevel = ZoneLevel.SemiPublic;
                else if (cy >= 28f)
                    room.zoneLevel = ZoneLevel.Workspace;
                else
                    room.zoneLevel = ZoneLevel.Secure;

                // Mark windows for rooms on the exterior.
                float x = room.worldPos.x, z = room.worldPos.y;
                float w = room.size.x, d = room.size.y;
                room.hasWindows = (z <= 0.5f || z + d >= MapD - 0.5f ||
                                   x <= 0.5f || x + w >= MapW - 0.5f);
            }
        }
    }
}
