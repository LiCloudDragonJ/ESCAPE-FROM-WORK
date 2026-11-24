using System.Collections.Generic;
using UnityEngine;

namespace EscapeFromWork.LevelGen
{
    /// <summary>
    /// Everything one chunk owns, resolved once from the floor plan. Box-shaped geometry
    /// (walls, columns, stairs, core, props, pads, light panels) is clipped to the chunk rect,
    /// so pieces tile the source exactly and nothing vanishes when a neighbour unloads.
    /// Point anchors (extraction pads, lights) belong to exactly one chunk, picked by center.
    /// </summary>
    public sealed class FloorChunkManifest
    {
        public int Index;
        public RectF Rect;

        public readonly List<BoxSpec> WallBoxes = new List<BoxSpec>();
        public readonly List<BoxSpec> CoreBoxes = new List<BoxSpec>();
        public readonly List<BoxSpec> ColumnBoxes = new List<BoxSpec>();
        public readonly List<BoxSpec> StairBoxes = new List<BoxSpec>();
        public readonly List<BoxSpec> PanelBoxes = new List<BoxSpec>();

        public readonly List<PropPlacement> Props = new List<PropPlacement>();
        public readonly List<ExtractionPoint> Extractions = new List<ExtractionPoint>();
        public readonly List<RectF> SpawnPads = new List<RectF>();
        public readonly List<Vector2> Lights = new List<Vector2>();

        public float WallVolume
        {
            get
            {
                float v = 0f;
                for (int i = 0; i < WallBoxes.Count; i++) v += WallBoxes[i].Volume;
                return v;
            }
        }

        public int ItemCount
        {
            get
            {
                return WallBoxes.Count + CoreBoxes.Count + ColumnBoxes.Count + StairBoxes.Count
                    + PanelBoxes.Count + Props.Count + Extractions.Count + SpawnPads.Count + Lights.Count;
            }
        }
    }

    public static class FloorChunkPlanner
    {
        /// <summary>Build the per-chunk manifests. Pure: no Unity objects are created.</summary>
        public static FloorChunkManifest[] Plan(FloorPlan plan, FloorChunkGrid grid, int lightBudget = FloorGeometry.LightBudget)
        {
            FloorChunkManifest[] manifests = new FloorChunkManifest[grid.Count];
            for (int i = 0; i < manifests.Length; i++)
            {
                manifests[i] = new FloorChunkManifest();
                manifests[i].Index = i;
                manifests[i].Rect = grid.Rect(i);
            }

            List<BoxSpec> walls = new List<BoxSpec>();
            FloorGeometry.CollectWallBoxes(plan, walls);
            List<BoxSpec> core = new List<BoxSpec>();
            FloorGeometry.CollectCoreBoxes(plan, core);
            List<BoxSpec> columns = new List<BoxSpec>();
            FloorGeometry.CollectColumnBoxes(plan, columns);
            List<BoxSpec> stairs = new List<BoxSpec>();
            FloorGeometry.CollectStairBoxes(plan, stairs);

            for (int i = 0; i < grid.Count; i++)
            {
                RectF rect = manifests[i].Rect;
                ClipInto(manifests[i].WallBoxes, walls, rect);
                ClipInto(manifests[i].CoreBoxes, core, rect);
                ClipInto(manifests[i].ColumnBoxes, columns, rect);
                ClipInto(manifests[i].StairBoxes, stairs, rect);
            }

            // Props and panels are boxes too: clip them like walls so a wide desk or a
            // light panel straddling the chunk border is drawn by both chunks instead of
            // disappearing with its single owner. Pieces tile the source exactly.
            for (int i = 0; i < plan.Props.Count; i++)
            {
                PropPlacement p = plan.Props[i];
                List<int> owners = grid.Overlapping(p.Rect);
                for (int c = 0; c < owners.Count; c++)
                {
                    RectF clippedRect;
                    if (!ClipRect(p.Rect, grid.Rect(owners[c]), out clippedRect)) continue;
                    PropPlacement piece = p;
                    piece.Rect = clippedRect;
                    manifests[owners[c]].Props.Add(piece);
                }
            }

            List<BoxSpec> panels = new List<BoxSpec>();
            FloorGeometry.CollectPanelBoxes(plan, panels);
            for (int i = 0; i < panels.Count; i++)
            {
                BoxSpec b = panels[i];
                RectF area = new RectF(b.Center.x - b.Size.x * 0.5f, b.Center.z - b.Size.z * 0.5f, b.Size.x, b.Size.z);
                List<int> owners = grid.Overlapping(area);
                for (int c = 0; c < owners.Count; c++)
                {
                    BoxSpec clipped;
                    if (Clip(b, grid.Rect(owners[c]), out clipped)) manifests[owners[c]].PanelBoxes.Add(clipped);
                }
            }

            for (int i = 0; i < plan.Extractions.Count; i++)
            {
                ExtractionPoint e = plan.Extractions[i];
                manifests[grid.IndexAt(e.Center)].Extractions.Add(e);
            }

            // Spawn pads are flat plates: clip them so no chunk shows a half pad hole.
            for (int i = 0; i < plan.SpawnZones.Count; i++)
            {
                RectF r = plan.SpawnZones[i];
                List<int> owners = grid.Overlapping(r);
                for (int c = 0; c < owners.Count; c++)
                {
                    RectF clippedRect;
                    if (ClipRect(r, grid.Rect(owners[c]), out clippedRect)) manifests[owners[c]].SpawnPads.Add(clippedRect);
                }
            }

            List<Vector2> candidates = new List<Vector2>();
            FloorGeometry.CollectLightPositions(plan, candidates);
            List<Vector2> sampled = FloorGeometry.SampleLights(candidates, lightBudget);
            for (int i = 0; i < sampled.Count; i++) manifests[grid.IndexAt(sampled[i])].Lights.Add(sampled[i]);

            return manifests;
        }

        static void ClipInto(List<BoxSpec> output, List<BoxSpec> boxes, RectF rect)
        {
            for (int i = 0; i < boxes.Count; i++)
            {
                BoxSpec clipped;
                if (Clip(boxes[i], rect, out clipped)) output.Add(clipped);
            }
        }

        /// <summary>Clip a 2D rect (X/Y as X/Z) to a chunk rect; false when the pieces is a sliver.</summary>
        public static bool ClipRect(RectF box, RectF rect, out RectF clipped)
        {
            float x0 = Mathf.Max(box.X, rect.X);
            float x1 = Mathf.Min(box.XMax, rect.XMax);
            float z0 = Mathf.Max(box.Y, rect.Y);
            float z1 = Mathf.Min(box.YMax, rect.YMax);
            if (x1 - x0 <= 0.0005f || z1 - z0 <= 0.0005f)
            {
                clipped = default(RectF);
                return false;
            }
            clipped = new RectF(x0, z0, x1 - x0, z1 - z0);
            return true;
        }

        /// <summary>
        /// Clip an axis-aligned box to a chunk rect on X/Z. The chunks partition the floor,
        /// so the clipped pieces are disjoint and their total volume equals the source box.
        /// </summary>
        public static bool Clip(BoxSpec box, RectF rect, out BoxSpec clipped)
        {
            float x0 = Mathf.Max(box.Center.x - box.Size.x * 0.5f, rect.X);
            float x1 = Mathf.Min(box.Center.x + box.Size.x * 0.5f, rect.XMax);
            float z0 = Mathf.Max(box.Center.z - box.Size.z * 0.5f, rect.Y);
            float z1 = Mathf.Min(box.Center.z + box.Size.z * 0.5f, rect.YMax);
            if (x1 - x0 <= 0.0005f || z1 - z0 <= 0.0005f)
            {
                clipped = default(BoxSpec);
                return false;
            }
            clipped = new BoxSpec(
                new Vector3((x0 + x1) * 0.5f, box.Center.y, (z0 + z1) * 0.5f),
                new Vector3(x1 - x0, box.Size.y, z1 - z0));
            return true;
        }
    }
}
