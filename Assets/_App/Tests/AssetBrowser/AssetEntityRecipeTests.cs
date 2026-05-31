using NUnit.Framework;
using UnityEngine;

public class AssetEntityRecipeTests
{
    [Test]
    public void JsonRoundTrip_PreservesFields()
    {
        var r = new AssetEntityRecipe
        {
            type = AssetType.Reference,
            interactionLayer = InteractionLayer.SceneObjects,
            colliderKind = ColliderKind.Box,
            colliderCenter = new Vector3(0f, 1f, 0f),
            colliderSize = new Vector3(1.5f, 1f, 0.05f),
            referenceAspect = 1.5f,
            referenceBottomGap = 0.5f,
            referenceTwoSided = true,
        };

        var json = JsonUtility.ToJson(r);
        var back = JsonUtility.FromJson<AssetEntityRecipe>(json);

        Assert.AreEqual(AssetType.Reference, back.type);
        Assert.AreEqual(ColliderKind.Box, back.colliderKind);
        Assert.That(back.colliderCenter.y, Is.EqualTo(1f).Within(1e-4));
        Assert.That(back.referenceAspect, Is.EqualTo(1.5f).Within(1e-4));
        Assert.AreEqual(1, back.schemaVersion);
    }
}
