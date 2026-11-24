using System.Collections.Generic;
using EscapeFromWork.LevelGen;
using EscapeFromWork.UI;
using NUnit.Framework;
using UnityEngine;

namespace EscapeFromWork.Tests
{
    public class MapFogTests
    {
        static FloorPlan Plan()
        {
            FloorSpec spec = FloorSpec.Default(1);
            spec.Band = FloorBand.Tower;
            spec.Prototype = FloorPrototype.OpenOffice;
            spec.Width = 200f;
            spec.Height = 160f;
            spec.CorridorLevels = 3;
            return FloorGenerator.Generate(spec);
        }

        [Test]
        public void Fog_StartsFullyHidden()
        {
            MapFog fog = new MapFog(Plan());
            Assert.AreEqual(0f, fog.ExploredRatio, 1e-4f);
        }

        [Test]
        public void Fog_RevealsCellsAheadButNotBehind()
        {
            FloorPlan plan = Plan();
            MapFog fog = new MapFog(plan);
            Vector2 eye = plan.SpawnZones[0].Center;

            fog.Mark(eye, Vector2.up, 30f, 45f);

            float ahead = 0f;
            float behind = 0f;
            for (int y = 0; y < fog.CellH; y++)
            {
                for (int x = 0; x < fog.CellW; x++)
                {
                    if (!fog.Explored[y * fog.CellW + x]) continue;
                    Vector2 c = fog.CellCenter(x, y);
                    if (c.y > eye.y + 4f && Mathf.Abs(c.x - eye.x) < 6f) ahead += 1f;
                    if (c.y < eye.y - 4f) behind += 1f;
                }
            }
            Assert.Greater(ahead, 20f, "area ahead of the viewer should be revealed");
            Assert.AreEqual(0f, behind, "nothing behind the viewer should be revealed");
        }

        [Test]
        public void Fog_RespectsRange()
        {
            FloorPlan plan = Plan();
            MapFog fog = new MapFog(plan);
            Vector2 eye = plan.SpawnZones[0].Center;
            fog.Mark(eye, Vector2.up, 20f, 70f);
            Assert.Less(fog.ExploredRatio, 0.2f, "a 20 m cone cannot cover a 32,000 sqm floor");

            for (int y = 0; y < fog.CellH; y++)
            {
                for (int x = 0; x < fog.CellW; x++)
                {
                    if (!fog.Explored[y * fog.CellW + x]) continue;
                    Assert.LessOrEqual((fog.CellCenter(x, y) - eye).magnitude, 21f);
                }
            }
        }

        [Test]
        public void Fog_RatioGrowsWithExploration()
        {
            FloorPlan plan = Plan();
            MapFog fog = new MapFog(plan);
            Vector2 eye = plan.SpawnZones[0].Center;
            fog.Mark(eye, Vector2.up, 30f, 45f);
            float first = fog.ExploredRatio;
            fog.Mark(eye, Vector2.down, 30f, 45f);
            Assert.Greater(fog.ExploredRatio, first);
        }

        [Test]
        public void Raster_CoversWholeFootprint()
        {
            FloorPlan plan = Plan();
            Texture2D tex = MapRaster.Build(plan);
            Assert.AreEqual(Mathf.CeilToInt(plan.Bounds.W * MapRaster.PixelsPerMeter), tex.width);
            Assert.AreEqual(Mathf.CeilToInt(plan.Bounds.H * MapRaster.PixelsPerMeter), tex.height);
            Assert.Greater(MapRaster.CollectDoors(plan).Count, 50);
            Object.DestroyImmediate(tex);
        }
    }
}
