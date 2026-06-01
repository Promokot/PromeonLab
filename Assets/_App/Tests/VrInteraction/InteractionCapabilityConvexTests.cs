using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class InteractionCapabilityConvexTests
{
    [Test]
    public void Apply_ConvexMesh_AddsConvexMeshColliderPerRenderer_AndRegisters()
    {
        var root = new GameObject("obj");
        var a = GameObject.CreatePrimitive(PrimitiveType.Cube);   // has MeshFilter
        var b = GameObject.CreatePrimitive(PrimitiveType.Sphere); // has MeshFilter
        // strip the primitives' own colliders so only our convex ones are counted
        Object.DestroyImmediate(a.GetComponent<Collider>());
        Object.DestroyImmediate(b.GetComponent<Collider>());
        a.transform.SetParent(root.transform);
        b.transform.SetParent(root.transform);
        try
        {
            InteractionCapability.Apply(root, InteractionLayer.SceneObjects,
                ColliderKind.ConvexMesh, Vector3.zero, Vector3.one, selectable: true);

            var meshCols = root.GetComponentsInChildren<MeshCollider>(true);
            Assert.AreEqual(2, meshCols.Length, "one convex MeshCollider per mesh renderer");
            Assert.IsTrue(meshCols.All(m => m.convex));

            var it = root.GetComponent<XRPromeonInteractable>();
            Assert.IsNotNull(it);
            foreach (var m in meshCols)
                Assert.IsTrue(it.IsRegistered(m), "each convex collider must be registered to the interactable");
        }
        finally { Object.DestroyImmediate(root); }
    }
}
