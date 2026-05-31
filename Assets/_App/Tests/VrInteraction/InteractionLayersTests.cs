using NUnit.Framework;
using UnityEngine;

public class InteractionLayersTests
{
    [Test]
    public void UnityLayer_ResolvesAllThree()
    {
        Assert.GreaterOrEqual(InteractionLayers.UnityLayer(InteractionLayer.GizmoHandles), 0);
        Assert.GreaterOrEqual(InteractionLayers.UnityLayer(InteractionLayer.BoneProxies), 0);
        Assert.GreaterOrEqual(InteractionLayers.UnityLayer(InteractionLayer.SceneObjects), 0);
    }

    [Test]
    public void SetInteractionLayer_AssignsNamedUnityLayer()
    {
        var go = new GameObject("layered");
        try
        {
            go.SetInteractionLayer(InteractionLayer.GizmoHandles);
            Assert.AreEqual(LayerMask.NameToLayer("GizmoHandles"), go.layer);
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void SetInteractionLayer_SceneObjects_AssignsSceneObjectsLayer()
    {
        var go = new GameObject("layered2");
        try
        {
            go.SetInteractionLayer(InteractionLayer.SceneObjects);
            Assert.AreEqual(LayerMask.NameToLayer("SceneObjects"), go.layer);
        }
        finally { Object.DestroyImmediate(go); }
    }
}
