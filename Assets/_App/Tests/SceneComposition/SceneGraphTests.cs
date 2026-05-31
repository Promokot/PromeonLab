using NUnit.Framework;
using UnityEngine;

public class SceneGraphTests
{
    [Test]
    public void CaptureSnapshot_EmptyGraph_ReturnsV2WithEmptyNodes()
    {
        var bus = new EventBus();
        var sut = new SceneGraph(bus, null, null, null, null);

        var snap = sut.CaptureSnapshot("scene-1", "Scene", "2026-05-17");

        Assert.AreEqual(2,         snap.SchemaVersion);
        Assert.AreEqual("scene-1", snap.SceneId);
        Assert.AreEqual("Scene",   snap.DisplayName);
        Assert.IsNotNull(snap.Nodes);
        Assert.AreEqual(0,         snap.Nodes.Count);
    }

    [Test]
    public void AddTransientNode_DoesNotPublishSceneModified()
    {
        var bus = new EventBus();
        var sut = new SceneGraph(bus, null, null, null, null);
        var sn = MakeSceneNode("bone:rig:pelvis");

        int events = 0;
        bus.Subscribe<SceneModifiedEvent>(_ => events++);
        sut.AddTransientNode(sn);

        Assert.AreEqual(0, events, "AddTransientNode must not publish SceneModifiedEvent");

        Object.DestroyImmediate(sn.gameObject);
    }

    [Test]
    public void GetNode_FindsTransientNode()
    {
        var sut = new SceneGraph(new EventBus(), null, null, null, null);
        var sn = MakeSceneNode("bone:rig:pelvis");
        sut.AddTransientNode(sn);

        var found = sut.GetNode("bone:rig:pelvis");

        Assert.IsNotNull(found);
        Assert.AreSame(sn, found);

        Object.DestroyImmediate(sn.gameObject);
    }

    [Test]
    public void GetNode_NotInEitherDictionary_ReturnsNull()
    {
        var sut = new SceneGraph(new EventBus(), null, null, null, null);
        Assert.IsNull(sut.GetNode("missing"));
    }

    [Test]
    public void Nodes_DoesNotIncludeTransientNodes()
    {
        var sut = new SceneGraph(new EventBus(), null, null, null, null);
        var sn = MakeSceneNode("bone:rig:pelvis");
        sut.AddTransientNode(sn);

        Assert.AreEqual(0, sut.Nodes.Count,
            "outliner must not see bone proxies via Nodes enumeration");

        Object.DestroyImmediate(sn.gameObject);
    }

    [Test]
    public void GetNode_DestroyedTransient_ReturnsNullAndPrunes()
    {
        var sut = new SceneGraph(new EventBus(), null, null, null, null);
        var sn = MakeSceneNode("bone:rig:pelvis");
        sut.AddTransientNode(sn);

        Object.DestroyImmediate(sn.gameObject);
        var found = sut.GetNode("bone:rig:pelvis");

        Assert.IsNull(found, "destroyed transient must be reported as null");
        Assert.IsNull(sut.GetNode("bone:rig:pelvis"));
    }

    private static SceneNode MakeSceneNode(string nodeId)
    {
        var go = new GameObject(nodeId);
        var sn = go.AddComponent<SceneNode>();
        sn.Init(nodeId, default, nodeId);
        return sn;
    }
}
