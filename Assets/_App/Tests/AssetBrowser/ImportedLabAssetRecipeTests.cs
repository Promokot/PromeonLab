using NUnit.Framework;
using UnityEngine;

public class ImportedLabAssetRecipeTests
{
    [Test]
    public void Recipe_SerializesWithRecord()
    {
        var recipe = new AssetEntityRecipe { type = AssetType.Object, colliderSize = new Vector3(2,3,4) };
        var asset  = new ImportedLabAsset("id", "n", AssetType.Object, "asset-library/sources/id.glb", recipe);

        var json = JsonUtility.ToJson(asset);
        var back = JsonUtility.FromJson<ImportedLabAsset>(json);

        Assert.IsNotNull(back.Recipe);
        Assert.That(back.Recipe.colliderSize.z, Is.EqualTo(4f).Within(1e-4));
    }
}
