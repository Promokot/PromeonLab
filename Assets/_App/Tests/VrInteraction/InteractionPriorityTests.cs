using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class InteractionPriorityTests
{
    // --- PickWinnerIndex: pure priority-then-distance selection ---

    [Test]
    public void PickWinnerIndex_Empty_ReturnsMinusOne()
    {
        var idx = InteractionLayers.PickWinnerIndex(new int[0], new float[0]);
        Assert.AreEqual(-1, idx);
    }

    [Test]
    public void PickWinnerIndex_SingleHit_ReturnsZero()
    {
        var idx = InteractionLayers.PickWinnerIndex(new[] { 2 }, new[] { 5f });
        Assert.AreEqual(0, idx);
    }

    [Test]
    public void PickWinnerIndex_HigherPriorityWins_EvenWhenFarther()
    {
        // priorities: index0 = SceneObjects(2) near, index1 = GizmoHandles(0) far.
        var priorities = new[] { 2, 0 };
        var distances  = new[] { 1f, 9f };
        var idx = InteractionLayers.PickWinnerIndex(priorities, distances);
        Assert.AreEqual(1, idx, "GizmoHandles (priority 0) must win over SceneObjects (2) regardless of distance");
    }

    [Test]
    public void PickWinnerIndex_SameLayer_NearestWins()
    {
        var priorities = new[] { 2, 2, 2 };
        var distances  = new[] { 4f, 1.5f, 8f };
        var idx = InteractionLayers.PickWinnerIndex(priorities, distances);
        Assert.AreEqual(1, idx, "within one layer the smallest distance wins");
    }

    [Test]
    public void PickWinnerIndex_MixedLayers_PicksNearestWithinTopLayer()
    {
        // BoneProxies(1) at 6 and 2; SceneObjects(2) at 1. Bone wins; nearest bone is index2.
        var priorities = new[] { 2, 1, 1 };
        var distances  = new[] { 1f, 6f, 2f };
        var idx = InteractionLayers.PickWinnerIndex(priorities, distances);
        Assert.AreEqual(2, idx);
    }

    // --- Unity layer mapping (the three layers exist in the project) ---

    [Test]
    public void UnityLayer_ResolvesAllThree()
    {
        Assert.GreaterOrEqual(InteractionLayers.UnityLayer(InteractionLayer.GizmoHandles), 0);
        Assert.GreaterOrEqual(InteractionLayers.UnityLayer(InteractionLayer.BoneProxies), 0);
        Assert.GreaterOrEqual(InteractionLayers.UnityLayer(InteractionLayer.SceneObjects), 0);
    }

    [Test]
    public void Mask_IncludesAllThreeLayers()
    {
        int mask = InteractionLayers.Mask;
        Assert.AreNotEqual(0, mask & (1 << InteractionLayers.UnityLayer(InteractionLayer.GizmoHandles)));
        Assert.AreNotEqual(0, mask & (1 << InteractionLayers.UnityLayer(InteractionLayer.BoneProxies)));
        Assert.AreNotEqual(0, mask & (1 << InteractionLayers.UnityLayer(InteractionLayer.SceneObjects)));
    }

    [Test]
    public void TryGetPriority_MapsUnityLayerBackToEnumPriority()
    {
        Assert.IsTrue(InteractionLayers.TryGetPriority(
            InteractionLayers.UnityLayer(InteractionLayer.BoneProxies), out var p));
        Assert.AreEqual((int)InteractionLayer.BoneProxies, p);

        // A layer that is not one of the three returns false (0 = Default layer).
        Assert.IsFalse(InteractionLayers.TryGetPriority(0, out _));
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

    [Test]
    public void Resolver_HigherLayerBehindLowerLayer_PicksHigherLayer()
    {
        var near = GameObject.CreatePrimitive(PrimitiveType.Cube); // SceneObjects, near
        var far  = GameObject.CreatePrimitive(PrimitiveType.Cube); // GizmoHandles, far
        try
        {
            near.transform.position = new Vector3(0, 0, 2);
            far.transform.position  = new Vector3(0, 0, 5);
            near.SetInteractionLayer(InteractionLayer.SceneObjects);
            far.SetInteractionLayer(InteractionLayer.GizmoHandles);
            Physics.SyncTransforms();

            var resolver = new RayInteractionResolver();
            var winner = resolver.ResolvePrimary(new Ray(Vector3.zero, Vector3.forward), 50f);

            Assert.IsNotNull(winner);
            Assert.AreEqual(far.GetComponent<Collider>(), winner,
                "GizmoHandles layer must win over a nearer SceneObjects");
        }
        finally { Object.DestroyImmediate(near); Object.DestroyImmediate(far); }
    }

    [Test]
    public void Resolver_NoInteractionLayerHit_ReturnsNull()
    {
        var plain = GameObject.CreatePrimitive(PrimitiveType.Cube); // stays on Default layer
        try
        {
            plain.transform.position = new Vector3(0, 0, 3);
            Physics.SyncTransforms();
            var resolver = new RayInteractionResolver();
            var winner = resolver.ResolvePrimary(new Ray(Vector3.zero, Vector3.forward), 50f);
            Assert.IsNull(winner, "a hit outside the interaction mask is not a winner");
        }
        finally { Object.DestroyImmediate(plain); }
    }

    [Test]
    public void SetInteractionLayerOnColliders_TagsChildColliderNotJustRoot()
    {
        // Mirrors a multi-part asset: root holds no collider, child mesh holds it.
        var root  = new GameObject("assetRoot");
        var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
        child.transform.SetParent(root.transform, false);
        try
        {
            root.SetInteractionLayerOnColliders(InteractionLayer.SceneObjects);

            Assert.AreEqual(LayerMask.NameToLayer("SceneObjects"), child.layer,
                "the child carrying the collider must get the interaction layer");
            Assert.AreEqual(0, root.layer,
                "a collider-less root is left untouched (Default)");
        }
        finally { Object.DestroyImmediate(root); }
    }
}
