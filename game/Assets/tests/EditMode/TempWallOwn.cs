using System.Collections.Generic;
using EscapeFromWork.LevelGen;
using NUnit.Framework;
using UnityEngine;

namespace EscapeFromWork.Tests
{
    public class TempWallOwn
    {
        static int Owner(FloorPlan plan, Vector2 p)
        {
            for (int i = 0; i < plan.Rooms.Count; i++)
                if (plan.Rooms[i].Rect.Contains(p)) return 1000 + i;
            for (int i = 0; i < plan.Corridors.Count; i++)
                if (plan.Corridors[i].Rect.Contains(p)) return 2000 + i;
            if (plan.Core.Contains(p)) return 3000;
            for (int i = 0; i < plan.Stairs.Count; i++)
                if (plan.Stairs[i].Contains(p)) return 3100 + i;
            for (int i = 0; i < plan.SpawnZones.Count; i++)
                if (plan.SpawnZones[i].Contains(p)) return 3200 + i;
            return -1;
        }

        [Test]
        public void Report()
        {
            int seed = 1;
            FloorSpec spec = FloorSpec.Default(seed);
            spec.Band = FloorBand.Tower;
            spec.Width = 200f; spec.Height = 160f;
            FloorPlan plan = FloorGenerator.Generate(spec);
            List<BoxSpec> boxes = new List<BoxSpec>();
            FloorGeometry.CollectWallBoxes(plan, boxes);
            int same = 0, total = 0, outside = 0;
            var lines = new List<string>();
            for (int b = 0; b < boxes.Count; b++)
            {
                BoxSpec box = boxes[b];
                total++;
                bool horizontal = box.Size.x >= box.Size.z;
                float halfW = (horizontal ? box.Size.x : box.Size.z) * 0.5f;
                float thick = (horizontal ? box.Size.z : box.Size.x) * 0.5f;
                Vector2 mid = horizontal ? new Vector2(box.Center.x, box.Center.z) : new Vector2(box.Center.x, box.Center.z);
                Vector2 n = horizontal ? new Vector2(0f, 1f) : new Vector2(1f, 0f);
                Vector2 a = mid - n * (thick + 0.25f);
                Vector2 c = mid + n * (thick + 0.25f);
                int oa = Owner(plan, a), oc = Owner(plan, c);
                if (oa == -1 || oc == -1) { outside++; if (lines.Count < 12 && halfW > 0.25f) lines.Add(" OUTSIDE line " + mid + " len=" + (halfW * 2f).ToString("F2") + " oa=" + oa + " oc=" + oc); continue; }
                if (oa == oc)
                {
                    same++;
                    if (lines.Count < 25) lines.Add(" SAME line " + mid + " len=" + (halfW * 2f).ToString("F2") + " owner=" + oa + " thick=" + (thick * 2f).ToString("F2"));
                }
            }
            Debug.Log("[own] boxes=" + total + " sameSide=" + same + " outside=" + outside);
            for (int i = 0; i < lines.Count; i++) Debug.Log("[own]" + lines[i]);
        }
    }
}
