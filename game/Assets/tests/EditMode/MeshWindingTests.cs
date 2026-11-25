using System.Collections.Generic;
using EscapeFromWork.LevelGen;
using NUnit.Framework;
using UnityEngine;

namespace EscapeFromWork.Tests.EditMode
{
    /// <summary>
    /// Every box face must be wound clockwise as seen from outside, otherwise it is
    /// backface-culled and the box shows only its far (inner) faces - the "box has only
    /// two faces / hollow wall" artifact.
    /// </summary>
    public class MeshWindingTests
    {
        static FloorPlan TowerPlan(int seed)
        {
            FloorSpec spec = FloorSpec.Default(seed);
            spec.Band = FloorBand.Tower;
            spec.Width = 200f;
            spec.Height = 160f;
            return FloorGenerator.Generate(spec);
        }

        [Test]
        public void AccumulatedBoxes_FaceOutward()
        {
            FloorGeometry.MeshAccum acc = new FloorGeometry.MeshAccum();
            acc.AddBox(new Vector3(0f, 0.5f, 0f), Vector3.one);

            List<Vector3> v = acc.Vertices;
            List<Vector3> n = acc.Normals;
            List<int> t = acc.Triangles;
            Assert.AreEqual(36, t.Count, "一个盒子应有 12 个三角形");
            for (int i = 0; i < t.Count; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                Vector3 geo = Vector3.Cross(b - a, c - a);
                Assert.Greater(Vector3.Dot(geo, n[t[i]]), 0f,
                    "三角形 " + (i / 3) + " 的绕序与法线相反（会被背面剔除）");
            }
        }

        [Test]
        public void FloorGeometry_MeshesFaceOutward(
            [Values(1, 3)] int seed)
        {
            FloorPlan plan = TowerPlan(seed);
            GameObject root = FloorGeometry.Build(plan, null, null);
            try
            {
                int checkedMeshes = 0;
                foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    Mesh mesh = filter.sharedMesh;
                    if (mesh == null) continue;
                    Vector3[] v = mesh.vertices;
                    Vector3[] n = mesh.normals;
                    int[] t = mesh.triangles;
                    for (int i = 0; i < t.Length; i += 3)
                    {
                        Vector3 geo = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                        if (geo.sqrMagnitude < 1e-8f) continue;
                        Assert.Greater(Vector3.Dot(geo, n[t[i]]), 0f,
                            filter.name + " 有三角形绕序反了（seed " + seed + "）");
                    }
                    checkedMeshes++;
                }
                Assert.Greater(checkedMeshes, 5, "应该检查到多个网格");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
