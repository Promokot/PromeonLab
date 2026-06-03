using NUnit.Framework;
using UnityEngine;

public class GizmoBoundsComputerTests
{
    private GameObject _root;

    [SetUp]
    public void SetUp() => _root = new GameObject("root");

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_root);

    [Test]
    public void NoRenderers_ReturnsMinSize()
    {
        var size = GizmoBoundsComputer.ComputeSize(_root.transform, boundsCoefficient: 1.5f, minSize: 0.1f, maxSize: 5f);
        Assert.AreEqual(0.1f, size, 1e-4);
    }

    [Test]
    public void SingleCube_FitsToHalfExtentTimesCoefficient()
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.SetParent(_root.transform, false);
        cube.transform.localScale = Vector3.one;
        var size = GizmoBoundsComputer.ComputeSize(_root.transform, 1.5f, 0.1f, 5f);
        Assert.AreEqual(0.75f, size, 0.01f);
    }

    [Test]
    public void LargeMesh_ClampedToMaxSize()
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.SetParent(_root.transform, false);
        cube.transform.localScale = Vector3.one * 100f;
        var size = GizmoBoundsComputer.ComputeSize(_root.transform, 1.5f, 0.1f, 5f);
        Assert.AreEqual(5f, size, 1e-4);
    }

    [Test]
    public void TinyMesh_ClampedToMinSize()
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.SetParent(_root.transform, false);
        cube.transform.localScale = Vector3.one * 0.01f;
        var size = GizmoBoundsComputer.ComputeSize(_root.transform, 1.5f, 0.1f, 5f);
        Assert.AreEqual(0.1f, size, 1e-4);
    }
}
