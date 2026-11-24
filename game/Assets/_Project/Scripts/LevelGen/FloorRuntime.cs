using UnityEngine;
using UnityEngine.AI;

namespace EscapeFromWork.LevelGen
{
    /// <summary>
    /// Drops a generated floor into the scene. The editor bake path turns BuildGeometry off
    /// (geometry already lives in the scene) but still rebuilds the NavMesh at play time.
    /// </summary>
    public class FloorRuntime : MonoBehaviour
    {
        public int Seed = 1;
        public FloorBand Band = FloorBand.Tower;
        public FloorPrototype Prototype = FloorPrototype.OpenOffice;
        public int CorridorLevels = 3;
        public FloorMaterials Materials;

        [Tooltip("生成 3D 几何（编辑器里已烘焙时关掉，避免重复）")]
        public bool BuildGeometry = true;
        public bool BuildNavMesh = true;

        public FloorPlan Plan { get; private set; }
        public GameObject Geometry { get; private set; }

        void Awake()
        {
            Plan = GeneratePlan(Seed, Band, Prototype, CorridorLevels);
            if (BuildGeometry) BuildGeometryNow();
            if (BuildNavMesh) BuildNavMeshNow();
        }

        public static FloorPlan GeneratePlan(int seed, FloorBand band, FloorPrototype prototype, int corridorLevels)
        {
            FloorSpec spec = FloorSpec.Default(seed);
            spec.Band = band;
            spec.Prototype = prototype;
            spec.CorridorLevels = corridorLevels;
            switch (band)
            {
                case FloorBand.Podium: spec.Width = 280f; spec.Height = 280f; break;
                case FloorBand.Top: spec.Width = 160f; spec.Height = 120f; break;
                default: spec.Width = 200f; spec.Height = 160f; break;
            }
            return FloorGenerator.Generate(spec);
        }

        public GameObject BuildGeometryNow()
        {
            if (Plan == null) return null;
            if (Geometry != null) Destroy(Geometry);
            Geometry = FloorGeometry.Build(Plan, Materials, transform);
            return Geometry;
        }

        public void BuildNavMeshNow()
        {
            if (Plan == null) return;
            NavMeshData data = FloorGeometry.BuildNavMesh(Plan);
            if (data == null) { Debug.LogWarning("[FloorRuntime] navmesh build failed"); return; }
            NavMesh.AddNavMeshData(data);
            Debug.Log("[FloorRuntime] navmesh " + data.sourceBounds.size);
        }
    }
}
