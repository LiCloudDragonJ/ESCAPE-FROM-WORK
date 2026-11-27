using System.Collections.Generic;
using UnityEngine;

namespace EscapeFromWork.LevelGen
{
    /// <summary>
    /// Builds the streamed floor out of GameObject pieces: a always-loaded shell (slab,
    /// ceiling, outer walls) plus one GameObject per chunk. Chunk meshes are merged per
    /// material, so a loaded chunk costs at most a dozen draw calls, and the returned mesh
    /// list lets the streamer destroy GPU memory when the chunk unloads.
    /// </summary>
    public static class FloorChunkGeometry
    {
        /// <summary>Slab, ceiling and the four outer walls: always present, never streamed.</summary>
        public static GameObject BuildShell(FloorPlan plan, FloorMaterials mats, Transform parent)
        {
            if (mats == null) mats = FloorGeometry.FallbackMaterials();

            GameObject root = new GameObject("FloorShell_" + plan.Spec.Seed);
            if (parent != null) root.transform.SetParent(parent, false);

            FloorGeometry.MeshAccum floor = new FloorGeometry.MeshAccum();
            FloorGeometry.MeshAccum ceiling = new FloorGeometry.MeshAccum();
            FloorGeometry.AddPlateBoxes(plan, floor, ceiling);

            List<BoxSpec> walls = new List<BoxSpec>();
            FloorGeometry.CollectShellBoxes(plan, walls);
            FloorGeometry.MeshAccum shellWalls = new FloorGeometry.MeshAccum();
            FloorGeometry.AddBoxes(shellWalls, walls);

            // The slab needs a collider (the player stands on it); ceiling stays collider-free.
            FloorGeometry.Emit(root, "Floor", floor, mats.Floor, true);
            FloorGeometry.Emit(root, "Ceiling", ceiling, mats.Ceiling, false);
            FloorGeometry.Emit(root, "OuterWalls", shellWalls, mats.Wall, true);
            FloorGeometry.EmitRoomSurfaces(plan, mats, root);
            return root;
        }

        /// <summary>Build one chunk and return the meshes that must be destroyed on unload.</summary>
        public static GameObject BuildChunk(FloorChunkManifest manifest, FloorMaterials mats, Transform parent, out List<Mesh> meshes)
        {
            return BuildChunk(manifest, mats, parent, null, out meshes);
        }

        /// <summary>Same as above, but prop names found in the catalog become prefab instances.</summary>
        public static GameObject BuildChunk(FloorChunkManifest manifest, FloorMaterials mats, Transform parent,
            PropCatalog catalog, out List<Mesh> meshes)
        {
            if (manifest == null) throw new System.ArgumentNullException("manifest");
            if (mats == null) mats = FloorGeometry.FallbackMaterials();
            meshes = new List<Mesh>();

            GameObject root = new GameObject("Chunk_" + manifest.Index);
            if (parent != null) root.transform.SetParent(parent, false);

            FloorGeometry.MeshAccum walls = new FloorGeometry.MeshAccum();
            FloorGeometry.AddBoxes(walls, manifest.WallBoxes);
            FloorGeometry.AddBoxes(walls, manifest.ChamferBoxes);
            FloorGeometry.MeshAccum core = new FloorGeometry.MeshAccum();
            FloorGeometry.AddBoxes(core, manifest.CoreBoxes);
            FloorGeometry.MeshAccum columns = new FloorGeometry.MeshAccum();
            FloorGeometry.AddBoxes(columns, manifest.ColumnBoxes);
            FloorGeometry.MeshAccum stairs = new FloorGeometry.MeshAccum();
            FloorGeometry.AddBoxes(stairs, manifest.StairBoxes);
            FloorGeometry.MeshAccum panels = new FloorGeometry.MeshAccum();
            FloorGeometry.AddBoxes(panels, manifest.PanelBoxes);

            FloorGeometry.MeshAccum large = new FloorGeometry.MeshAccum();
            FloorGeometry.MeshAccum medium = new FloorGeometry.MeshAccum();
            FloorGeometry.MeshAccum small = new FloorGeometry.MeshAccum();
            FloorGeometry.MeshAccum sightBreak = new FloorGeometry.MeshAccum();
            for (int i = 0; i < manifest.Props.Count; i++)
            {
                PropPlacement p = manifest.Props[i];
                if (p.Spec.Name == "隔断墙")
                {
                    sightBreak.AddBox(FloorGeometry.Center(p.Rect, FloorGeometry.CeilingY * 0.5f),
                        new Vector3(p.Rect.W, FloorGeometry.CeilingY, p.Rect.H));
                    continue;
                }
                if (PropSpawner.TrySpawn(catalog, p, root.transform)) continue;
                float h = p.Spec.Size == PropSize.Large ? 1.05f : p.Spec.Size == PropSize.Medium ? 0.75f : 0.5f;
                FloorGeometry.MeshAccum target = p.Spec.Size == PropSize.Large ? large
                    : p.Spec.Size == PropSize.Medium ? medium : small;
                target.AddBox(FloorGeometry.Center(p.Rect, h * 0.5f), new Vector3(p.Rect.W, h, p.Rect.H));
            }

            FloorGeometry.MeshAccum extraction = new FloorGeometry.MeshAccum();
            for (int i = 0; i < manifest.Extractions.Count; i++)
            {
                ExtractionPoint e = manifest.Extractions[i];
                extraction.AddBox(new Vector3(e.Center.x, 0.03f, e.Center.y), new Vector3(1.4f, 0.06f, 1.4f));
                extraction.AddBox(new Vector3(e.Rect.X + 0.1f, 1.1f, e.Rect.Y + 0.1f), new Vector3(0.16f, 2.2f, 0.16f));
            }

            FloorGeometry.MeshAccum spawn = new FloorGeometry.MeshAccum();
            for (int i = 0; i < manifest.SpawnPads.Count; i++)
            {
                RectF r = manifest.SpawnPads[i];
                spawn.AddBox(new Vector3(r.Center.x, 0.03f, r.Center.y), new Vector3(r.W - 0.6f, 0.06f, r.H - 0.6f));
            }

            EmitTracked(root, meshes, "Walls", walls, mats.Wall);
            EmitTracked(root, meshes, "Core", core, mats.Core);
            EmitTracked(root, meshes, "Columns", columns, mats.Column);
            EmitTracked(root, meshes, "Stairs", stairs, mats.Stair);
            EmitTracked(root, meshes, "Props_Large", large, mats.PropLarge);
            EmitTracked(root, meshes, "Props_Medium", medium, mats.PropMedium);
            EmitTracked(root, meshes, "Props_Small", small, mats.PropSmall);
            EmitTracked(root, meshes, "SightBreaks", sightBreak, mats.SightBreak);
            EmitTracked(root, meshes, "LightPanels", panels, mats.LightPanel, false);
            EmitTracked(root, meshes, "Extractions", extraction, mats.Extraction, false);
            EmitTracked(root, meshes, "SpawnPads", spawn, mats.Spawn, false);

            BuildChunkLights(root, manifest.Lights);
            SignageRenderer.Build(manifest.SourcePlan, manifest.Signs, root.transform);
            return root;
        }

        static void EmitTracked(GameObject root, List<Mesh> meshes, string name,
            FloorGeometry.MeshAccum acc, Material material, bool collider = true)
        {
            if (acc.Vertices.Count == 0) return;
            FloorGeometry.Emit(root, name, acc, material, collider);
            Transform child = root.transform.Find(name);
            if (child != null)
            {
                MeshFilter filter = child.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null) meshes.Add(filter.sharedMesh);
            }
        }

        static void BuildChunkLights(GameObject root, List<Vector2> positions)
        {
            if (positions.Count == 0) return;
            GameObject lights = new GameObject("Lights");
            lights.transform.SetParent(root.transform, false);
            for (int i = 0; i < positions.Count; i++)
            {
                GameObject go = new GameObject("Light_" + i);
                go.transform.SetParent(lights.transform, false);
                go.transform.position = new Vector3(positions[i].x, FloorGeometry.CeilingY - 0.3f, positions[i].y);
                Light l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 18f;
                l.intensity = 9f;
                l.color = new Color(1f, 0.95f, 0.87f);
                l.shadows = LightShadows.None;
            }
        }
    }
}
