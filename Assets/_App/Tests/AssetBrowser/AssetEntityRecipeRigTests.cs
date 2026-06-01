using NUnit.Framework;
using UnityEngine;

public class AssetEntityRecipeRigTests
{
    [Test]
    public void Recipe_WithRig_RoundTripsThroughJsonUtility()
    {
        var recipe = new AssetEntityRecipe { type = AssetType.Rig };
        recipe.rig = new RigDefinition { AssetId = "a" };
        recipe.rig.Bones.Add(new BoneRecord { BoneName = "hips" });
        recipe.rig.Bones.Add(new BoneRecord { BoneName = "spine" });

        var json = JsonUtility.ToJson(recipe);
        var back = JsonUtility.FromJson<AssetEntityRecipe>(json);

        Assert.IsTrue(back.HasRig);
        Assert.AreEqual(2, back.rig.Bones.Count);
        Assert.AreEqual("hips",  back.rig.Bones[0].BoneName);
        Assert.AreEqual("spine", back.rig.Bones[1].BoneName);
    }

    [Test]
    public void Recipe_WithoutRig_HasRigIsFalseAfterRoundTrip()
    {
        var recipe = new AssetEntityRecipe { type = AssetType.Object };
        var json = JsonUtility.ToJson(recipe);
        var back = JsonUtility.FromJson<AssetEntityRecipe>(json);

        Assert.IsFalse(back.HasRig);
    }
}
