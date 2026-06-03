using NUnit.Framework;
using UnityEngine;

public class AnimationAuthoringCompletionTests
{
    private class FakeGraph : ISceneGraph
    {
        private readonly GameObject _go;
        public FakeGraph(GameObject go) => _go = go;
        public GameObject GetNode(string nodeId) => _go;
        public void AddNode(GameObject go, AssetRef assetRef, string displayName, string parentId = null) { }
        public void RemoveNode(string nodeId) { }
    }

    [Test]
    public void Completed_SamplesFrameZero_PosesObjectAtFirstKey()
    {
        var go  = new GameObject("obj1");
        go.transform.localPosition = new Vector3(99, 99, 99); // arbitrary "current" pose

        var bus = new EventBus();
        var graph   = new FakeGraph(go);
        var sampler = new AnimationPlaybackSampler(new AnimationClock(bus), graph, bus);
        var a   = new AnimationAuthoring(graph, null, null, sampler, bus);
        a.InitForTest();
        a.CreateContainer("obj1", 60, 24);
        // first (and only) key sits at frame 30 with a known pose
        a.SetKey("obj1", 30, new Vector3(1, 2, 3), Quaternion.identity, Vector3.one);
        a.SetActiveContainerOwner("obj1");

        bus.Publish(new PlaybackStateChangedEvent { IsPlaying = false, Frame = 0, Completed = true });

        Assert.AreEqual(1f, go.transform.localPosition.x, 0.001f);
        Assert.AreEqual(2f, go.transform.localPosition.y, 0.001f);
        Assert.AreEqual(3f, go.transform.localPosition.z, 0.001f);

        Object.DestroyImmediate(go);
    }
}
