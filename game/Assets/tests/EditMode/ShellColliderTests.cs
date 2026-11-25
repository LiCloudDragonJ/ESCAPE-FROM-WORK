using EscapeFromWork.LevelGen;
using NUnit.Framework;
using UnityEngine;

namespace EscapeFromWork.Tests.EditMode
{
    public class ShellColliderTests
    {
        [Test]
        public void Shell_SlabHasCollider_CeilingDoesNot()
        {
            FloorSpec spec = FloorSpec.Default(1);
            spec.Band = FloorBand.Tower;
            spec.Width = 200f;
            spec.Height = 160f;
            FloorPlan plan = FloorGenerator.Generate(spec);

            GameObject shell = FloorChunkGeometry.BuildShell(plan, FloorGeometry.FallbackMaterials(), null);
            try
            {
                Transform floor = shell.transform.Find("Floor");
                Assert.IsNotNull(floor, "shell must have a Floor mesh");
                Assert.IsNotNull(floor.GetComponent<MeshCollider>(), "the slab must be walkable: MeshCollider required");

                Transform ceiling = shell.transform.Find("Ceiling");
                Assert.IsNotNull(ceiling, "shell must have a Ceiling mesh");
                Assert.IsNull(ceiling.GetComponent<Collider>(), "ceiling stays collider-free");
            }
            finally
            {
                Object.DestroyImmediate(shell);
            }
        }
    }
}
