using NUnit.Framework;
using UnityEngine;

public class SceneNodeTests
{
    [Test]
    public void Init_StoresValues()
    {
        var go = new GameObject("n");
        var sn = go.AddComponent<SceneNode>();
        sn.Init("id-1", new AssetRef { Source = AssetSource.Builtin, AssetId = "a" }, "Display");

        Assert.AreEqual("id-1",    sn.NodeId);
        Assert.AreEqual("Display", sn.DisplayName);
        Assert.AreEqual("a",       sn.AssetRef.AssetId);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void SetNodeId_ChangesNodeIdValue()
    {
        var go = new GameObject("n");
        var sn = go.AddComponent<SceneNode>();
        sn.Init("old", default, "x");

        sn.SetNodeId("new");

        Assert.AreEqual("new", sn.NodeId);

        Object.DestroyImmediate(go);
    }
}
