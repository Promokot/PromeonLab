using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class PromeonInteractableRigBuilderTests
{
    [Test]
    public void BuildDiamondMesh_HasSixVertices()
    {
        var mesh = PromeonInteractableRigBuilder.BuildDiamondMesh();
        Assert.AreEqual(6, mesh.vertexCount);
        Object.DestroyImmediate(mesh);
    }

    [Test]
    public void BuildDiamondMesh_HasTwentyFourTriangleIndices()
    {
        var mesh = PromeonInteractableRigBuilder.BuildDiamondMesh();
        Assert.AreEqual(24, mesh.triangles.Length);
        Object.DestroyImmediate(mesh);
    }

    [Test]
    public void BuildDiamondMesh_HeadVertexAtOrigin()
    {
        var mesh = PromeonInteractableRigBuilder.BuildDiamondMesh();
        Assert.AreEqual(Vector3.zero, mesh.vertices[0]);
        Object.DestroyImmediate(mesh);
    }

    [Test]
    public void BuildDiamondMesh_TailVertexAtUnitY()
    {
        var mesh = PromeonInteractableRigBuilder.BuildDiamondMesh();
        Assert.AreEqual(Vector3.up, mesh.vertices[5]);
        Object.DestroyImmediate(mesh);
    }

    private readonly List<GameObject> _created = new();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _created)
            if (go != null) Object.DestroyImmediate(go);
        _created.Clear();
    }

    private GameObject MakeGO(string name, Transform parent = null)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent);
        _created.Add(go);
        return go;
    }

    [Test]
    public void ExtractPairs_ParentChildBothInSet_ReturnsPair()
    {
        var parent = MakeGO("Hip");
        var child  = MakeGO("Thigh", parent.transform);

        var pairs = PromeonInteractableRigBuilder.ExtractPairs(
            new[] { parent.transform, child.transform });

        Assert.AreEqual(1, pairs.Length);
        Assert.AreEqual(parent.transform, pairs[0].start);
        Assert.AreEqual(child.transform,  pairs[0].end);
    }

    [Test]
    public void ExtractPairs_ChildNotInSet_NoPairReturned()
    {
        var parent = MakeGO("Hip");
        MakeGO("Thigh", parent.transform);

        var pairs = PromeonInteractableRigBuilder.ExtractPairs(new[] { parent.transform });

        Assert.AreEqual(0, pairs.Length);
    }

    [Test]
    public void ExtractPairs_LeafBone_NotReturnedAsPair()
    {
        var leaf = MakeGO("Foot");

        var pairs = PromeonInteractableRigBuilder.ExtractPairs(new[] { leaf.transform });

        Assert.AreEqual(0, pairs.Length);
    }

    [Test]
    public void ExtractPairs_NullTransformInArray_SkippedSafely()
    {
        var parent = MakeGO("Hip");
        var child  = MakeGO("Thigh", parent.transform);

        var pairs = PromeonInteractableRigBuilder.ExtractPairs(
            new Transform[] { null, parent.transform, child.transform, null });

        Assert.AreEqual(1, pairs.Length);
    }
}
