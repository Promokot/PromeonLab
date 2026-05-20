using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class PromeonInteractableRigBuilderTests
{
    [Test]
    public void BuildDiamondMesh_HasSixVertices()
    {
        var mesh = PromeonInteractableRigBuilder.BuildDiamondMesh();
        Assert.AreEqual(6, mesh.vertexCount);
        Object.DestroyImmediate(mesh);
    }

    [Test]
    public void BuildDiamondMesh_HasTwentyFourTriangleIndices()
    {
        var mesh = PromeonInteractableRigBuilder.BuildDiamondMesh();
        Assert.AreEqual(24, mesh.triangles.Length);
        Object.DestroyImmediate(mesh);
    }

    [Test]
    public void BuildDiamondMesh_HeadVertexAtOrigin()
    {
        var mesh = PromeonInteractableRigBuilder.BuildDiamondMesh();
        Assert.AreEqual(Vector3.zero, mesh.vertices[0]);
        Object.DestroyImmediate(mesh);
    }

    [Test]
    public void BuildDiamondMesh_TailVertexAtUnitY()
    {
        var mesh = PromeonInteractableRigBuilder.BuildDiamondMesh();
        Assert.AreEqual(Vector3.up, mesh.vertices[5]);
        Object.DestroyImmediate(mesh);
    }

    [Test]
    public void BuildOrientedDiamondMesh_VerticalAxis_MatchesUnitMesh()
    {
        var mesh = PromeonInteractableRigBuilder.BuildOrientedDiamondMesh(Vector3.up, 1f, 1f);
        Assert.AreEqual(6,  mesh.vertexCount);
        Assert.AreEqual(24, mesh.triangles.Length);
        Assert.AreEqual(new Vector3(0f, 1f, 0f), mesh.vertices[5]);
        Object.DestroyImmediate(mesh);
    }

    [Test]
    public void BuildOrientedDiamondMesh_HorizontalAxis_RotatesVertices()
    {
        var mesh = PromeonInteractableRigBuilder.BuildOrientedDiamondMesh(Vector3.right, 1f, 1f);
        var tail = mesh.vertices[5];
        Assert.AreEqual(1f, tail.x, 0.0001f);
        Assert.AreEqual(0f, tail.y, 0.0001f);
        Assert.AreEqual(0f, tail.z, 0.0001f);
        Object.DestroyImmediate(mesh);
    }

    [Test]
    public void BuildOrientedDiamondMesh_NonUniformScale_AppliesBeforeRotation()
    {
        var mesh = PromeonInteractableRigBuilder.BuildOrientedDiamondMesh(Vector3.up, 2f, 0.5f);
        Assert.AreEqual(2f,   mesh.bounds.size.y, 0.001f);
        Assert.AreEqual(0.5f, mesh.bounds.size.x, 0.001f);
        Assert.AreEqual(0.5f, mesh.bounds.size.z, 0.001f);
        Object.DestroyImmediate(mesh);
    }

    private readonly List<GameObject> _created = new();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _created)
            if (go != null) Object.DestroyImmediate(go);
        _created.Clear();
    }

    private GameObject MakeGO(string name, Transform parent = null)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent);
        _created.Add(go);
        return go;
    }

    [Test]
    public void ExtractPairs_ParentChildBothInSet_ReturnsPair()
    {
        var parent = MakeGO("Hip");
        var child  = MakeGO("Thigh", parent.transform);

        var pairs = PromeonInteractableRigBuilder.ExtractPairs(
            new[] { parent.transform, child.transform });

        Assert.AreEqual(1, pairs.Length);
        Assert.AreEqual(parent.transform, pairs[0].start);
        Assert.AreEqual(child.transform,  pairs[0].end);
    }

    [Test]
    public void ExtractPairs_ChildNotInSet_NoPairReturned()
    {
        var parent = MakeGO("Hip");
        MakeGO("Thigh", parent.transform);

        var pairs = PromeonInteractableRigBuilder.ExtractPairs(new[] { parent.transform });

        Assert.AreEqual(0, pairs.Length);
    }

    [Test]
    public void ExtractPairs_LeafBone_NotReturnedAsPair()
    {
        var leaf = MakeGO("Foot");

        var pairs = PromeonInteractableRigBuilder.ExtractPairs(new[] { leaf.transform });

        Assert.AreEqual(0, pairs.Length);
    }

    [Test]
    public void ExtractPairs_NullTransformInArray_SkippedSafely()
    {
        var parent = MakeGO("Hip");
        var child  = MakeGO("Thigh", parent.transform);

        var pairs = PromeonInteractableRigBuilder.ExtractPairs(
            new Transform[] { null, parent.transform, child.transform, null });

        Assert.AreEqual(1, pairs.Length);
    }

    [Test]
    public void EffectiveWidth_LongBone_ReturnsBoneWidth()
    {
        // 1.0 >> 5 * 0.06 — returns full boneWidth
        Assert.AreEqual(0.06f, PromeonInteractableRigBuilder.EffectiveWidth(0.06f, 1.0f), 0.0001f);
    }

    [Test]
    public void EffectiveWidth_ShortBone_ReturnsCappedWidth()
    {
        // 0.1 * 0.2 = 0.02 < 0.06 — returns scaled width
        Assert.AreEqual(0.02f, PromeonInteractableRigBuilder.EffectiveWidth(0.06f, 0.1f), 0.0001f);
    }

    [Test]
    public void EffectiveWidth_AtThreshold_ReturnsBoneWidth()
    {
        // 0.3 * 0.2 = 0.06 = boneWidth — exactly at threshold
        Assert.AreEqual(0.06f, PromeonInteractableRigBuilder.EffectiveWidth(0.06f, 0.3f), 0.0001f);
    }

    [Test]
    public void BoneFollower_Tick_CopiesLocalPositionFromProxy()
    {
        var boneGo  = MakeGO("bone");
        var proxyGo = MakeGO("proxy");
        proxyGo.transform.localPosition = new Vector3(1f, 2f, 3f);

        var follower = boneGo.AddComponent<BoneFollower>();
        follower.SetProxy(proxyGo.transform);
        follower.Tick();

        Assert.AreEqual(new Vector3(1f, 2f, 3f), boneGo.transform.localPosition);
    }

    [Test]
    public void BoneFollower_Tick_CopiesLocalRotationFromProxy()
    {
        var boneGo  = MakeGO("bone");
        var proxyGo = MakeGO("proxy");
        var expected = Quaternion.Euler(45f, 90f, 0f);
        proxyGo.transform.localRotation = expected;

        var follower = boneGo.AddComponent<BoneFollower>();
        follower.SetProxy(proxyGo.transform);
        follower.Tick();

        Assert.AreEqual(expected, boneGo.transform.localRotation);
    }

    [Test]
    public void BoneFollower_Tick_NullProxy_DoesNotThrow()
    {
        var boneGo   = MakeGO("bone");
        var follower = boneGo.AddComponent<BoneFollower>();
        // No proxy set — Tick must not throw
        Assert.DoesNotThrow(() => follower.Tick());
    }

    [Test]
    public void BuildProxyHierarchy_TwoBones_CreatesTwoProxies()
    {
        var characterGo = MakeGO("Character");
        var pelvisGo    = MakeGO("pelvis", characterGo.transform);
        var spineGo     = MakeGO("spine",  pelvisGo.transform);
        spineGo.transform.localPosition = Vector3.up * 0.5f;

        var rig = characterGo.AddComponent<PromeonInteractableRigBuilder>();
        rig.SetTransforms(new[] { pelvisGo.transform, spineGo.transform });
        rig.Rebuild();

        var proxyRoot   = characterGo.transform.Find("_ProxyBones");
        Assert.IsNotNull(proxyRoot, "_ProxyBones container not found under characterGo");

        var proxyPelvis = proxyRoot.Find("proxy_pelvis");
        Assert.IsNotNull(proxyPelvis, "proxy_pelvis not found under _ProxyBones");

        var proxySpine  = proxyPelvis.Find("proxy_spine");
        Assert.IsNotNull(proxySpine, "proxy_spine not found under proxy_pelvis");
    }

    [Test]
    public void BuildProxyHierarchy_NestedHierarchy_MirrorsParenting()
    {
        var characterGo = MakeGO("Character");
        var pelvisGo    = MakeGO("pelvis", characterGo.transform);
        var spineGo     = MakeGO("spine",  pelvisGo.transform);
        var chestGo     = MakeGO("chest",  spineGo.transform);
        spineGo.transform.localPosition = Vector3.up * 0.5f;
        chestGo.transform.localPosition = Vector3.up * 0.5f;

        var rig = characterGo.AddComponent<PromeonInteractableRigBuilder>();
        rig.SetTransforms(new[] { pelvisGo.transform, spineGo.transform, chestGo.transform });
        rig.Rebuild();

        var proxyRoot   = characterGo.transform.Find("_ProxyBones");
        var proxyPelvis = proxyRoot?.Find("proxy_pelvis");
        var proxySpine  = proxyPelvis?.Find("proxy_spine");
        var proxyChest  = proxySpine?.Find("proxy_chest");

        Assert.IsNotNull(proxyRoot,   "_ProxyBones not found");
        Assert.IsNotNull(proxyPelvis, "proxy_pelvis not found — not child of _ProxyBones");
        Assert.IsNotNull(proxySpine,  "proxy_spine not found — not child of proxy_pelvis");
        Assert.IsNotNull(proxyChest,  "proxy_chest not found — not child of proxy_spine");
    }

    [Test]
    public void BuildProxyHierarchy_AddsBoneFollowerToEachBone()
    {
        var characterGo = MakeGO("Character");
        var pelvisGo    = MakeGO("pelvis", characterGo.transform);
        var spineGo     = MakeGO("spine",  pelvisGo.transform);
        spineGo.transform.localPosition = Vector3.up * 0.5f;

        var rig = characterGo.AddComponent<PromeonInteractableRigBuilder>();
        rig.SetTransforms(new[] { pelvisGo.transform, spineGo.transform });
        rig.Rebuild();

        Assert.IsNotNull(pelvisGo.GetComponent<BoneFollower>(), "pelvis missing BoneFollower");
        Assert.IsNotNull(spineGo.GetComponent<BoneFollower>(),  "spine missing BoneFollower");
    }

    [Test]
    public void BuildProxyHierarchy_LeafBone_UsesDefaultLength()
    {
        // Default length for a leaf bone = _boneWidth * 5 = 0.06 * 5 = 0.3
        var characterGo = MakeGO("Character");
        var pelvisGo    = MakeGO("pelvis", characterGo.transform);
        var spineGo     = MakeGO("spine",  pelvisGo.transform);
        // Put spine 1m above pelvis so pelvis proxy length=1.0 ≠ leaf default 0.3
        spineGo.transform.localPosition = Vector3.up * 1.0f;

        var rig = characterGo.AddComponent<PromeonInteractableRigBuilder>();
        rig.SetTransforms(new[] { pelvisGo.transform, spineGo.transform });
        rig.Rebuild();

        var proxySpine = characterGo.transform
            .Find("_ProxyBones")
            ?.Find("proxy_pelvis")
            ?.Find("proxy_spine");

        Assert.IsNotNull(proxySpine, "proxy_spine not found");
        // spine is a leaf in the set — no children → default length = 0.06 * 5 = 0.3
        Assert.AreEqual(0.3f, proxySpine.localScale.y, 0.001f,
            "Leaf bone proxy scale.y should equal boneWidth * 5 = 0.3");
    }
}
