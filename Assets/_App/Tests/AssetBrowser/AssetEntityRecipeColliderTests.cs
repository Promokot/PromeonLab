using NUnit.Framework;
using UnityEngine;

public class AssetEntityRecipeColliderTests
{
    [Test]
    public void ColliderKind_HasConvexMeshAndBoneBoxes()
    {
        // Serialized as ints in recipes — these values are an append-only contract.
        Assert.AreEqual(1, (int)ColliderKind.Box);
        Assert.AreEqual(2, (int)ColliderKind.ConvexMesh);
        Assert.AreEqual(3, (int)ColliderKind.BoneBoxes);
    }

    [Test]
    public void Recipe_RoundTrips_KindAndBoneDepth()
    {
        var recipe = new AssetEntityRecipe { colliderKind = ColliderKind.BoneBoxes, boneColliderDepth = 3 };
        var back   = JsonUtility.FromJson<AssetEntityRecipe>(JsonUtility.ToJson(recipe));
        Assert.AreEqual(ColliderKind.BoneBoxes, back.colliderKind);
        Assert.AreEqual(3, back.boneColliderDepth);
    }
}
