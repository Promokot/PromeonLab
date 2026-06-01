using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

public class AssetEntityBuilderRegistryTests
{
    private class FakeBuilder : IAssetEntityBuilder
    {
        public AssetType HandledType { get; set; }
        public AssetEntityRecipe LastRecipe;
        public GameObject ReturnGo;
        public Task<AssetEntityRecipe> BuildAsync(string p, AssetType t, CancellationToken ct)
            => Task.FromResult(new AssetEntityRecipe { type = t });
        public Task<GameObject> RestoreAsync(ILabAsset a, AssetEntityRecipe r, Vector3 pos, Quaternion rot, CancellationToken ct)
        { LastRecipe = r; return Task.FromResult(ReturnGo); }
    }

    [Test]
    public void RestoreAsync_DispatchesByType_AndPassesRecord()
    {
        var refBuilder = new FakeBuilder { HandledType = AssetType.Reference };
        var reg = new AssetEntityBuilderRegistry(new IAssetEntityBuilder[] { refBuilder });

        var recipe = new AssetEntityRecipe { type = AssetType.Reference, referenceAspect = 3f };
        var asset  = new ImportedLabAsset("id1", "name", AssetType.Reference, "asset-library/sources/id1.png", recipe);

        reg.RestoreAsync(asset, Vector3.zero, Quaternion.identity, CancellationToken.None);

        Assert.IsNotNull(refBuilder.LastRecipe);
        Assert.That(refBuilder.LastRecipe.referenceAspect, Is.EqualTo(3f));
    }

    [Test]
    public void RestoreAsync_AppliesCapabilityFromRecipe_WhenRecipePresent()
    {
        var go  = new GameObject("imported");
        var b   = new FakeBuilder { HandledType = AssetType.Object, ReturnGo = go };
        var reg = new AssetEntityBuilderRegistry(new IAssetEntityBuilder[] { b });

        var recipe = new AssetEntityRecipe
        {
            type = AssetType.Object, selectable = true,
            colliderKind = ColliderKind.Box, colliderSize = new Vector3(2f, 3f, 4f),
        };
        var asset = new ImportedLabAsset("id", "n", AssetType.Object, "asset-libraries/sources/id.glb", recipe);

        var result = reg.RestoreAsync(asset, Vector3.zero, Quaternion.identity, CancellationToken.None)
                        .GetAwaiter().GetResult();

        var box = result.GetComponent<BoxCollider>();
        Assert.IsNotNull(box, "Registry should apply InteractionCapability when a recipe is present");
        Assert.AreEqual(new Vector3(2f, 3f, 4f), box.size);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void RestoreAsync_SkipsCapability_WhenRecipeNull()
    {
        var go  = new GameObject("builtin-like");
        var b   = new FakeBuilder { HandledType = AssetType.Object, ReturnGo = go };
        var reg = new AssetEntityBuilderRegistry(new IAssetEntityBuilder[] { b });

        var asset = new ImportedLabAsset("id", "n", AssetType.Object, "ref", null); // no recipe

        var result = reg.RestoreAsync(asset, Vector3.zero, Quaternion.identity, CancellationToken.None)
                        .GetAwaiter().GetResult();

        Assert.IsNull(result.GetComponent<BoxCollider>(), "No recipe → no capability applied by the registry");
        Object.DestroyImmediate(go);
    }
}
