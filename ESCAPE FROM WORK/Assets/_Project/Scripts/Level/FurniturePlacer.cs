using System.Collections.Generic;
using UnityEngine;
using EscapeFromWork.Data;
using EscapeFromWork.Loot;

namespace EscapeFromWork.Level
{
    /// <summary>
    /// Places furniture inside a room based on the room's <see cref="RoomFurnitureSet"/>.
    /// Phase 1: builds furniture with primitive Cubes and attaches
    /// <see cref="LootContainer"/> for interaction.
    /// </summary>
    public static class FurniturePlacer
    {
        /// <summary>
        /// Place all mandatory + optional furniture in a room.
        /// Returns every placed piece with its loot-container reference.
        /// </summary>
        public static List<PlacedFurniture> PlaceFurniture(
            RoomDef room,
            RoomFurnitureSet furnitureSet,
            int seed,
            Transform parent)
        {
            Random.InitState(seed);
            var placed = new List<PlacedFurniture>();

            if (furnitureSet == null) return placed;

            Vector3 roomMin = new Vector3(room.worldPos.x, 0f, room.worldPos.y);
            Vector3 roomMax = roomMin + new Vector3(room.size.x, 0f, room.size.y);

            // Place mandatory furniture.
            if (furnitureSet.mandatoryFurniture != null)
            {
                foreach (var fp in furnitureSet.mandatoryFurniture)
                {
                    int count = Random.Range(fp.minCount, fp.maxCount + 1);
                    for (int i = 0; i < count; i++)
                    {
                        var pf = PlaceOne(fp, roomMin, roomMax, placed, parent);
                        if (pf != null) placed.Add(pf);
                    }
                }
            }

            // Place optional furniture (gated by spawnChance).
            if (furnitureSet.optionalFurniture != null)
            {
                foreach (var fp in furnitureSet.optionalFurniture)
                {
                    if (Random.value > fp.spawnChance) continue;
                    int count = Random.Range(fp.minCount, fp.maxCount + 1);
                    for (int i = 0; i < count; i++)
                    {
                        var pf = PlaceOne(fp, roomMin, roomMax, placed, parent);
                        if (pf != null) placed.Add(pf);
                    }
                }
            }

            // Pick one supervisor desk (if any furniture supports it).
            ApplySupervisorVariant(placed, furnitureSet);

            // Pick one ransacked furniture.
            ApplyRansackedVariant(placed, furnitureSet);

            return placed;
        }

        private static PlacedFurniture PlaceOne(
            FurniturePlacement fp,
            Vector3 roomMin, Vector3 roomMax,
            List<PlacedFurniture> alreadyPlaced,
            Transform parent)
        {
            if (fp.template == null) return null;

            // Find a free spot in the room (simple random-then-check, max 10 attempts).
            Vector3 pos = Vector3.zero;
            bool found = false;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                pos = new Vector3(
                    Random.Range(roomMin.x + 0.5f, roomMax.x - 0.5f),
                    0f,
                    Random.Range(roomMin.z + 0.5f, roomMax.z - 0.5f));

                // Check against already-placed furniture with a simple 1m clearance.
                if (!OverlapsAny(pos, alreadyPlaced, 1f))
                {
                    found = true;
                    break;
                }
            }

            if (!found) return null;

            // Build visual.
            GameObject go = BuildFurnitureVisual(fp.template, pos, parent);

            // Attach LootContainer.
            LootContainer lc = go.AddComponent<LootContainer>();
            if (fp.template.lootTable != null)
                lc.LootTable = fp.template.lootTable;

            return new PlacedFurniture
            {
                template      = fp.template,
                position      = pos,
                lootContainer = lc,
                isSupervisorVariant = false,
                isRansacked   = false
            };
        }

        private static bool OverlapsAny(Vector3 pos, List<PlacedFurniture> placed, float clearance)
        {
            foreach (var pf in placed)
            {
                float dx = Mathf.Abs(pos.x - pf.position.x);
                float dz = Mathf.Abs(pos.z - pf.position.z);
                if (dx < clearance && dz < clearance)
                    return true;
            }
            return false;
        }

        private static void ApplySupervisorVariant(List<PlacedFurniture> placed, RoomFurnitureSet set)
        {
            // Pick one eligible desk, upgrade it.
            foreach (var pf in placed)
            {
                var fp = FindPlacement(pf.template, set);
                if (fp == null || !fp.canBeSupervisor) continue;

                pf.isSupervisorVariant = true;
                // Visual hint: slightly tinted.
                var mr = pf.lootContainer?.GetComponent<MeshRenderer>();
                if (mr != null) mr.material.color = new Color(0.2f, 0.2f, 0.35f); // dark blue desk
                break; // only one supervisor per room
            }
        }

        private static void ApplyRansackedVariant(List<PlacedFurniture> placed, RoomFurnitureSet set)
        {
            foreach (var pf in placed)
            {
                var fp = FindPlacement(pf.template, set);
                if (fp == null || !fp.canBeBroken) continue;

                pf.isRansacked = true;
                // Visual hint: slightly tilted / darker.
                pf.lootContainer?.transform.Rotate(5f, 0f, 3f);
                var mr = pf.lootContainer?.GetComponent<MeshRenderer>();
                if (mr != null) mr.material.color *= 0.5f;
                break;
            }
        }

        private static FurniturePlacement FindPlacement(FurnitureTemplate template, RoomFurnitureSet set)
        {
            foreach (var fp in set.mandatoryFurniture)
                if (fp.template == template) return fp;

            if (set.optionalFurniture != null)
                foreach (var fp in set.optionalFurniture)
                    if (fp.template == template) return fp;

            return null;
        }

        private static GameObject BuildFurnitureVisual(FurnitureTemplate template, Vector3 pos, Transform parent)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = template.furnitureType.ToString();
            go.transform.position = pos + Vector3.up * 0.4f; // slight elevation
            go.transform.localScale = new Vector3(1f, 0.8f, 0.6f);
            go.transform.SetParent(parent);

            // Collider: make trigger for cover detection. Loot interaction is
            // handled by LootContainer component on a separate child GameObject.
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }

            // Tag for cover detection (PlayerHealth.IsInCover checks "Furniture" tag).
            go.tag = "Furniture";

            return go;
        }
    }

    /// <summary>
    /// One piece of placed furniture with its runtime state.
    /// </summary>
    public class PlacedFurniture
    {
        public FurnitureTemplate template;
        public Vector3 position;
        public LootContainer lootContainer;
        public bool isSupervisorVariant;
        public bool isRansacked;
    }
}
