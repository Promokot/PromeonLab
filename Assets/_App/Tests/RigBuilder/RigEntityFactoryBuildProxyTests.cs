using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class RigEntityFactoryBuildProxyTests
{
    private static (GameObject root, SkinnedMeshRenderer smr) MakeSkeleton()
    {
        var root     = new GameObject("rig");
        var armature  = new GameObject("Armature"); armature.transform.SetParent(root.transform);
        var bone     = new GameObject("Bone");      bone.transform.SetParent(armature.transform);
        bone.transform.localPosition = Vector3.zero;
        var boneChild = new GameObject("Bone.001"); boneChild.transform.SetParent(bone.transform);
        boneChild.transform.localPosition = new Vector3(0f, 1f, 0f);

        var smr = root.AddComponent<SkinnedMeshRenderer>();
        smr.bones = new[] { bone.transform, boneChild.transform };
        return (root, smr);
    }

    private static RigEntityFactory MakeFactory()
    {
        var cfg = ScriptableObject.CreateInstance<ProxyRigConfig>();
        return new RigEntityFactory(new GltfModelLoader(), cfg);
    }

    [Test]
    public void BuildProxyRig_AllBones_CreatesProxyHierarchyAndRuntime()
    {
        var (root, _) = MakeSkeleton();
        MakeFactory().BuildProxyRig(root, null); // null → all smr.bones

        Assert.IsNotNull(root.GetComponent<ProxyRigRuntime>(), "ProxyRigRuntime attached to rig root");
        var proxyRoot = root.transform.Find("Armature/ProxyRig") ?? FindDeep(root.transform, "ProxyRig");
        Assert.IsNotNull(proxyRoot, "ProxyRig container created");
        var markers = root.GetComponentsInChildren<BoneSceneNodeMarker>(true);
        Assert.AreEqual(2, markers.Length, "one proxy per bone");
        Assert.AreEqual(2, root.GetComponentsInChildren<BoneFollower>(true).Length, "a follower per bone");

        Object.DestroyImmediate(root);
    }

    [Test]
    public void BuildProxyRig_NoBones_IsNoOp()
    {
        var root = new GameObject("empty");
        root.AddComponent<SkinnedMeshRenderer>().bones = new Transform[0];

        MakeFactory().BuildProxyRig(root, null);

        Assert.IsNull(root.GetComponent<ProxyRigRuntime>(), "no skeleton → no proxy rig");
        Object.DestroyImmediate(root);
    }

    [Test]
    public void BuildProxyRig_NamedSubset_BuildsOnlyMatchedBones()
    {
        var (root, _) = MakeSkeleton();
        MakeFactory().BuildProxyRig(root, new List<string> { "Bone" }); // only the root bone

        var markers = root.GetComponentsInChildren<BoneSceneNodeMarker>(true);
        Assert.AreEqual(1, markers.Length, "only the named bone gets a proxy");
        Object.DestroyImmediate(root);
    }

    private static Transform FindDeep(Transform t, string name)
    {
        foreach (Transform c in t)
        {
            if (c.name == name) return c;
            var r = FindDeep(c, name);
            if (r != null) return r;
        }
        return null;
    }
}
