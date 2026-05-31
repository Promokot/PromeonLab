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
        public Task<AssetEntityRecipe> BuildAsync(string p, AssetType t, CancellationToken ct)
            => Task.FromResult(new AssetEntityRecipe { type = t });
        public Task<GameObject> RestoreAsync(ILabAsset a, AssetEntityRecipe r, Vector3 pos, Quaternion rot, CancellationToken ct)
        { LastRecipe = r; return Task.FromResult<GameObject>(null); }
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
}
