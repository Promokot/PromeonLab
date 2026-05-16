using NUnit.Framework;

public class SceneGraphTests
{
    [Test]
    public void CaptureSnapshot_EmptyGraph_ReturnsV2WithEmptyNodes()
    {
        var bus = new EventBus();
        var sut = new SceneGraph(bus, null, null, null);

        var snap = sut.CaptureSnapshot("scene-1", "Scene", "2026-05-17");

        Assert.AreEqual(2,         snap.SchemaVersion);
        Assert.AreEqual("scene-1", snap.SceneId);
        Assert.AreEqual("Scene",   snap.DisplayName);
        Assert.IsNotNull(snap.Nodes);
        Assert.AreEqual(0,         snap.Nodes.Count);
    }
}
