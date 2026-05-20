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
        // pelvis has spine as child — proxy_pelvis oriented toward spine
        // spine is a leaf — proxy_spine exists, oriented along the chain at half pelvis's length
        var characterGo = MakeGO("Character");
        var armatureGo  = MakeGO("Armature", characterGo.transform);
        var pelvisGo    = MakeGO("pelvis",   armatureGo.transform);
        var spineGo     = MakeGO("spine",    pelvisGo.transform);
        spineGo.transform.localPosition = Vector3.up * 0.5f;

        var rig = characterGo.AddComponent<PromeonInteractableRigBuilder>();
        rig.SetTransforms(new[] { pelvisGo.transform, spineGo.transform });
        rig.Rebuild();

        var proxyRig    = characterGo.transform.Find("ProxyRig");
        Assert.IsNotNull(proxyRig, "ProxyRig container not found under characterGo");

        var proxyPelvis = proxyRig.Find("proxy_pelvis");
        Assert.IsNotNull(proxyPelvis, "proxy_pelvis not found under ProxyRig");

        var proxySpine  = proxyPelvis.Find("proxy_spine");
        Assert.IsNotNull(proxySpine, "proxy_spine not found under proxy_pelvis");
    }

    [Test]
    public void BuildProxyHierarchy_NestedHierarchy_MirrorsParenting()
    {
        var characterGo = MakeGO("Character");
        var armatureGo  = MakeGO("Armature", characterGo.transform);
        var pelvisGo    = MakeGO("pelvis",   armatureGo.transform);
        var spineGo     = MakeGO("spine",    pelvisGo.transform);
        var chestGo     = MakeGO("chest",    spineGo.transform);
        spineGo.transform.localPosition = Vector3.up * 0.5f;
        chestGo.transform.localPosition = Vector3.up * 0.5f;

        var rig = characterGo.AddComponent<PromeonInteractableRigBuilder>();
        rig.SetTransforms(new[] { pelvisGo.transform, spineGo.transform, chestGo.transform });
        rig.Rebuild();

        var proxyRig    = characterGo.transform.Find("ProxyRig");
        var proxyPelvis = proxyRig?.Find("proxy_pelvis");
        var proxySpine  = proxyPelvis?.Find("proxy_spine");
        var proxyChest  = proxySpine?.Find("proxy_chest");

        Assert.IsNotNull(proxyRig,    "ProxyRig not found");
        Assert.IsNotNull(proxyPelvis, "proxy_pelvis not found under ProxyRig");
        Assert.IsNotNull(proxySpine,  "proxy_spine not found under proxy_pelvis");
        Assert.IsNotNull(proxyChest,  "proxy_chest not found under proxy_spine");
    }

    [Test]
    public void BuildProxyHierarchy_AddsBoneFollowerToEachBone()
    {
        var characterGo = MakeGO("Character");
        var armatureGo  = MakeGO("Armature", characterGo.transform);
        var pelvisGo    = MakeGO("pelvis",   armatureGo.transform);
        var spineGo     = MakeGO("spine",    pelvisGo.transform);
        spineGo.transform.localPosition = Vector3.up * 0.5f;

        var rig = characterGo.AddComponent<PromeonInteractableRigBuilder>();
        rig.SetTransforms(new[] { pelvisGo.transform, spineGo.transform });
        rig.Rebuild();

        Assert.IsNotNull(pelvisGo.GetComponent<BoneFollower>(), "pelvis missing BoneFollower");
        Assert.IsNotNull(spineGo.GetComponent<BoneFollower>(),  "spine missing BoneFollower");
    }

    [Test]
    public void BuildProxyHierarchy_LeafBone_SizedSmallerThanParent()
    {
        // spine is a leaf — its proxy length should be half of (pelvis→spine distance),
        // which is also pelvis proxy's length. Leaf must be smaller than the bone preceding it.
        var characterGo = MakeGO("Character");
        var armatureGo  = MakeGO("Armature", characterGo.transform);
        var pelvisGo    = MakeGO("pelvis",   armatureGo.transform);
        var spineGo     = MakeGO("spine",    pelvisGo.transform);
        spineGo.transform.localPosition = Vector3.up * 1.0f;

        var rig = characterGo.AddComponent<PromeonInteractableRigBuilder>();
        rig.SetTransforms(new[] { pelvisGo.transform, spineGo.transform });
        rig.Rebuild();

        var proxyPelvis = characterGo.transform.Find("ProxyRig/proxy_pelvis");
        var proxySpine  = proxyPelvis?.Find("proxy_spine");
        Assert.IsNotNull(proxyPelvis, "proxy_pelvis not found");
        Assert.IsNotNull(proxySpine,  "proxy_spine should exist — terminal bones still get a proxy");

        var pelvisMesh = proxyPelvis.GetComponent<MeshFilter>().sharedMesh;
        var spineMesh  = proxySpine.GetComponent<MeshFilter>().sharedMesh;

        // Pelvis→spine distance = 1.0; leaf length = 1.0 * 0.5 = 0.5
        Assert.AreEqual(0.5f, spineMesh.bounds.size.y, 0.001f,
            "Leaf proxy length should be half the previous bone's length");
        Assert.Less(spineMesh.bounds.size.y, pelvisMesh.bounds.size.y,
            "Leaf proxy must be smaller than its parent's proxy");
    }

    [Test]
    public void BuildProxyHierarchy_ProxyRig_MirrorsArmatureLocalTransform()
    {
        var characterGo = MakeGO("Character");
        var armatureGo  = MakeGO("Armature", characterGo.transform);
        armatureGo.transform.localPosition = new Vector3(1f, 2f, 3f);
        armatureGo.transform.localRotation = Quaternion.Euler(10f, 20f, 30f);
        armatureGo.transform.localScale    = new Vector3(1.5f, 1.5f, 1.5f);

        var pelvisGo = MakeGO("pelvis", armatureGo.transform);
        var spineGo  = MakeGO("spine",  pelvisGo.transform);
        spineGo.transform.localPosition = Vector3.up * 0.5f;

        var rig = characterGo.AddComponent<PromeonInteractableRigBuilder>();
        rig.SetTransforms(new[] { pelvisGo.transform, spineGo.transform });
        rig.Rebuild();

        var proxyRig = characterGo.transform.Find("ProxyRig");
        Assert.IsNotNull(proxyRig, "ProxyRig not found");
        Assert.AreEqual(armatureGo.transform.localPosition, proxyRig.localPosition,
            "ProxyRig.localPosition must mirror Armature");
        Assert.Less(Quaternion.Angle(armatureGo.transform.localRotation, proxyRig.localRotation), 0.001f,
            "ProxyRig.localRotation must mirror Armature");
        Assert.AreEqual(armatureGo.transform.localScale,    proxyRig.localScale,
            "ProxyRig.localScale must mirror Armature");
    }

    [Test]
    public void BuildProxyHierarchy_ProxyGO_ScaleIsOne()
    {
        var characterGo = MakeGO("Character");
        var armatureGo  = MakeGO("Armature", characterGo.transform);
        var pelvisGo    = MakeGO("pelvis",   armatureGo.transform);
        var spineGo     = MakeGO("spine",    pelvisGo.transform);
        spineGo.transform.localPosition = Vector3.up * 0.5f;

        var rig = characterGo.AddComponent<PromeonInteractableRigBuilder>();
        rig.SetTransforms(new[] { pelvisGo.transform, spineGo.transform });
        rig.Rebuild();

        var proxyPelvis = characterGo.transform.Find("ProxyRig/proxy_pelvis");
        var proxySpine  = characterGo.transform.Find("ProxyRig/proxy_pelvis/proxy_spine");

        Assert.IsNotNull(proxyPelvis, "proxy_pelvis not found");
        Assert.IsNotNull(proxySpine,  "proxy_spine not found");
        Assert.AreEqual(Vector3.one, proxyPelvis.localScale, "proxy_pelvis.localScale must be (1,1,1)");
        Assert.AreEqual(Vector3.one, proxySpine.localScale,  "proxy_spine.localScale must be (1,1,1)");
    }

    [Test]
    public void BuildProxyHierarchy_MultipleChildren_BuildsCombinedMesh()
    {
        // pelvis has TWO children in set (leftHip and rightHip) — proxy_pelvis mesh should
        // contain TWO diamonds fused into one Mesh (12 verts, 48 triangle indices total).
        var characterGo = MakeGO("Character");
        var armatureGo  = MakeGO("Armature", characterGo.transform);
        var pelvisGo    = MakeGO("pelvis",   armatureGo.transform);
        var leftHipGo   = MakeGO("leftHip",  pelvisGo.transform);
        var rightHipGo  = MakeGO("rightHip", pelvisGo.transform);
        leftHipGo.transform.localPosition  = new Vector3(-0.2f, 0f, 0f);
        rightHipGo.transform.localPosition = new Vector3( 0.2f, 0f, 0f);

        var rig = characterGo.AddComponent<PromeonInteractableRigBuilder>();
        rig.SetTransforms(new[] { pelvisGo.transform, leftHipGo.transform, rightHipGo.transform });
        rig.Rebuild();

        var proxyPelvis = characterGo.transform.Find("ProxyRig/proxy_pelvis");
        Assert.IsNotNull(proxyPelvis, "proxy_pelvis not found");

        var mesh = proxyPelvis.GetComponent<MeshFilter>().sharedMesh;
        Assert.IsNotNull(mesh, "proxy_pelvis has no Mesh");
        Assert.AreEqual(12, mesh.vertexCount,
            "Combined mesh: 6 verts per child diamond × 2 children = 12");
        Assert.AreEqual(48, mesh.triangles.Length,
            "Combined mesh: 24 triangle indices per child × 2 children = 48");
    }
}
