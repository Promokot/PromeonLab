using NUnit.Framework;
using UnityEngine;

public class BoneEditModeTests
{
    private class NullGraph : ISceneGraph
    {
        public GameObject GetNode(string nodeId) => null;
        public void AddNode(GameObject go, AssetRef assetRef, string displayName, string parentId = null) { }
        public void RemoveNode(string nodeId) { }
    }

    [Test]
    public void SetActive_NoRigNode_IsNoOp()
    {
        var sut = new BoneEditMode(null, new NullGraph(), new EventBus());
        sut.SetActive("missing-rig", true);
        Assert.IsFalse(sut.IsActive, "no rig node → mode does not activate");
        Assert.IsNull(sut.ActiveRigId);
    }

    [Test]
    public void ClearActive_ResetsState()
    {
        var sut = new BoneEditMode(null, new NullGraph(), new EventBus());
        sut.ClearActive();
        Assert.IsFalse(sut.IsActive);
        Assert.IsNull(sut.ActiveRigId);
    }
}
