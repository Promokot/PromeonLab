using NUnit.Framework;
using UnityEngine;

public class SceneContextTests
{
    // --------------- Fake ---------------

    private class FakeSceneGraph : ISceneGraph
    {
        public GameObject GetNode(string nodeId)  => throw new System.NotImplementedException();
        public void AddNode(GameObject go, AssetRef assetRef, string displayName, string parentId = null) => throw new System.NotImplementedException();
        public void RemoveNode(string nodeId)     => throw new System.NotImplementedException();
    }

    // --------------- Tests ---------------

    [Test]
    public void New_HasNoScene()
    {
        var ctx = new SceneContext();

        Assert.IsFalse(ctx.HasScene);
        Assert.IsNull(ctx.Graph);
        Assert.IsNull(ctx.Selection);
    }

    [Test]
    public void Bind_WithGraph_SetsHasSceneAndExposesServices()
    {
        var ctx   = new SceneContext();
        var graph = new FakeSceneGraph();

        ctx.Bind(graph, null, null, null, null, null, null);

        Assert.IsTrue(ctx.HasScene);
        Assert.AreSame(graph, ctx.Graph);
        Assert.IsNull(ctx.Selection);
    }

    [Test]
    public void Clear_NullsEverythingAndHasNoScene()
    {
        var ctx   = new SceneContext();
        var graph = new FakeSceneGraph();

        ctx.Bind(graph, null, null, null, null, null, null);
        ctx.Clear();

        Assert.IsFalse(ctx.HasScene);
        Assert.IsNull(ctx.Graph);
    }
}
