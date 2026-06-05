using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

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
        var recipe = asset.Recipe;

        if (recipe == null && asset.Source == AssetSource.Builtin)
            throw new NotSupportedException(
                $"Builtin asset '{asset.Id}' has no baked recipe – bake it in the BuiltinAssetLibrary inspector.");

        var go = await Resolve(asset.Type).RestoreAsync(asset, recipe, position, rotation, ct);

        if (go != null && recipe != null)
            InteractionCapability.Apply(go, recipe.interactionLayer, recipe.colliderKind,
                recipe.colliderCenter, recipe.colliderSize, recipe.selectable);

        if (go != null && recipe != null && recipe.colliderKind == ColliderKind.BoneBoxes)
            go.GetComponent<ProxyRigRuntime>()?.RegisterSelectorColliders();

        return go;
    }

    private IAssetEntityBuilder Resolve(AssetType type)
    {
        if (!_byType.TryGetValue(type, out var b))
            throw new NotSupportedException($"No entity builder registered for asset type {type}");
        return b;
    }
}
