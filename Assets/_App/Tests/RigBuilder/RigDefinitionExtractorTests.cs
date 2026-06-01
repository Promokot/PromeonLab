using NUnit.Framework;
using UnityEngine;

public class RigDefinitionExtractorTests
{
    [Test]
    public void FromSkinnedMesh_NullRenderer_ReturnsNull()
    {
        Assert.IsNull(RigDefinitionExtractor.FromSkinnedMesh(null));
    }

    [Test]
    public void FromSkinnedMesh_NoBones_ReturnsNull()
    {
        var go  = new GameObject("smr");
        var smr = go.AddComponent<SkinnedMeshRenderer>();
        smr.bones = new Transform[0];

        Assert.IsNull(RigDefinitionExtractor.FromSkinnedMesh(smr));

        Object.DestroyImmediate(go);
    }

    [Test]
    public void FromSkinnedMesh_CollectsBoneNamesInOrder()
    {
        var root  = new GameObject("smr");
        var smr   = root.AddComponent<SkinnedMeshRenderer>();
        var hips  = new GameObject("hips").transform;
        var spine = new GameObject("spine").transform;
        smr.bones = new[] { hips, spine };

        var def = RigDefinitionExtractor.FromSkinnedMesh(smr);

        Assert.IsNotNull(def);
        Assert.AreEqual(2, def.Bones.Count);
        Assert.AreEqual("hips",  def.Bones[0].BoneName);
        Assert.AreEqual("spine", def.Bones[1].BoneName);

        Object.DestroyImmediate(root);
        Object.DestroyImmediate(hips.gameObject);
        Object.DestroyImmediate(spine.gameObject);
    }
}
