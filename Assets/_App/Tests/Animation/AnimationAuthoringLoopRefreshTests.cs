using NUnit.Framework;
using UnityEngine;

public class AnimationAuthoringLoopRefreshTests
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
    public void SetInterpolation_WhileLooping_TakesEffectWithoutRestart()
    {
        var go  = new GameObject("obj");
        var bus = new EventBus();
        var a   = new AnimationAuthoring(new AnimationClock(bus), new FakeGraph(go), null, null, bus);
        a.InitForTest();
        a.CreateContainer("obj", 60, 24);
        a.SetKey("obj", 0,  new Vector3(1, 0, 0),  Quaternion.identity, Vector3.one);
        a.SetKey("obj", 10, new Vector3(11, 0, 0), Quaternion.identity, Vector3.one);
        a.SetLoop("obj", true);
        a.StartLoopPlayback("obj", 5); // cursor sits at frame 5, between the two keys

        a.SetInterpolation("obj", InterpolationMode.Stepped); // must rebuild the running loop's clips
        a.Tick(); // Time.deltaTime is 0 in EditMode, so cursor stays at 5 and samples there

        Assert.AreEqual(1f, go.transform.localPosition.x, 0.1f,
            "stepped loop holds the frame-0 key value at the mid cursor (proves loop clips were rebuilt)");

        Object.DestroyImmediate(go);
    }
}
