using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using EscapeFromWork.LevelGen;

namespace EscapeFromWork.EditorTools
{
    /// <summary>
    /// Diagnostic: find floor area inside the envelope that no room / corridor / stair / core covers.
    /// Those leftovers are the "narrow useless strip between two walls" the plan still shows.
    /// </summary>
    public static class DeadSpaceProbe
    {
        const float Step = 0.25f;

        [MenuItem("Escape From Work/Debug/Dead Space Probe")]
        public static void Run()
        {
            RaidPlan raid = SlicePlanner.Generate(1);
            for (int f = 0; f < raid.FloorCount; f++)
            {
                FloorPlan plan = raid.Floors[f].Plan;
                RectF b = plan.Bounds;
                int nx = Mathf.CeilToInt(b.W / Step), nz = Mathf.CeilToInt(b.H / Step);
                bool[,] open = new bool[nx, nz];
                int dead = 0;
                for (int x = 0; x < nx; x++)
                    for (int z = 0; z < nz; z++)
                    {
                        Vector2 p = new Vector2(b.X + (x + 0.5f) * Step, b.Y + (z + 0.5f) * Step);
                        bool inside = FloorEnvelope.Contains(plan.Envelope.Outline, p);
                        bool covered = Covered(plan, p);
                        open[x, z] = inside && !covered;
                        if (open[x, z]) dead++;
                    }

                var report = new StringBuilder();
                int components = 0;
                bool[,] seen = new bool[nx, nz];
                var stack = new Stack<Vector2Int>();
                for (int x = 0; x < nx; x++)
                    for (int z = 0; z < nz; z++)
                    {
                        if (!open[x, z] || seen[x, z]) continue;
                        components++;
                        int minX = x, maxX = x, minZ = z, maxZ = z, cells = 0;
                        stack.Clear();
                        stack.Push(new Vector2Int(x, z));
                        seen[x, z] = true;
                        while (stack.Count > 0)
                        {
                            Vector2Int c = stack.Pop();
                            cells++;
                            minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x);
                            minZ = Mathf.Min(minZ, c.y); maxZ = Mathf.Max(maxZ, c.y);
                            for (int dx = -1; dx <= 1; dx++)
                                for (int dz = -1; dz <= 1; dz++)
                                {
                                    int ax = c.x + dx, az = c.y + dz;
                                    if (ax < 0 || az < 0 || ax >= nx || az >= nz) continue;
                                    if (!open[ax, az] || seen[ax, az]) continue;
                                    seen[ax, az] = true;
                                    stack.Push(new Vector2Int(ax, az));
                                }
                        }
                        if (components <= 12)
                            report.AppendLine(string.Format("  strip {0:0.00}x{1:0.00} m at ({2:0.0},{3:0.0}) cells={4}",
                                (maxX - minX + 1) * Step, (maxZ - minZ + 1) * Step,
                                b.X + (minX + 0.5f) * Step, b.Y + (minZ + 0.5f) * Step, cells));
                    }
                Debug.Log(string.Format("[DeadSpace] F{0}: {1} uncovered cells = {2:0} m2 in {3} strips\n{4}",
                    f, dead, dead * Step * Step, components, report));
            }
        }

        static bool Covered(FloorPlan plan, Vector2 p)
        {
            for (int i = 0; i < plan.Rooms.Count; i++) if (plan.Rooms[i].Rect.Contains(p)) return true;
            for (int i = 0; i < plan.Corridors.Count; i++) if (plan.Corridors[i].Rect.Contains(p)) return true;
            for (int i = 0; i < plan.Stairs.Count; i++) if (plan.Stairs[i].Contains(p)) return true;
            if (plan.Core.Contains(p)) return true;
            return false;
        }
    }
}
