using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Type-keyed dispatch for both Build (import) and Restore (spawn / scene-load). Extracts the baked
// recipe from the asset record so callers never deal with recipes directly.
public class AssetEntityBuilderRegistry
{
    private readonly Dictionary<AssetType, IAssetEntityBuilder> _byType = new();

    public AssetEntityBuilderRegistry(IReadOnlyList<IAssetEntityBuilder> builders)
    {
        foreach (var b in builders) _byType[b.HandledType] = b;
    }

    public Task<AssetEntityRecipe> BuildAsync(AssetType type, string sourceAbsolutePath, CancellationToken ct)
        => Resolve(type).BuildAsync(sourceAbsolutePath, type, ct);

    public async Task<GameObject> RestoreAsync(ILabAsset asset, Vector3 position, Quaternion rotation, CancellationToken ct)
    {
        var recipe = (asset as ImportedLabAsset)?.Recipe; // null for builtin (prefab already baked)
        var go = await Resolve(asset.Type).RestoreAsync(asset, recipe, position, rotation, ct);

        // Single finalization point: builders produce only geometry; selectability/collider/identity
        // are applied here from the recipe. Builtin (recipe == null) is pre-baked, so skip.
        if (go != null && recipe != null)
            InteractionCapability.Apply(go, recipe.interactionLayer, recipe.colliderKind,
                recipe.colliderCenter, recipe.colliderSize, recipe.selectable);

        return go;
    }

    private IAssetEntityBuilder Resolve(AssetType type)
    {
        if (!_byType.TryGetValue(type, out var b))
            throw new NotSupportedException($"No entity builder registered for asset type {type}");
        return b;
    }
}
