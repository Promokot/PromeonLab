using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Object = static mesh. Build loads the glTF once to measure its collider; Restore reloads the mesh
// (imported) or instantiates the prefab (builtin) and applies the baked recipe.
public class ObjectEntityBuilder : IAssetEntityBuilder
{
    protected readonly AssetSourceStore  _store;
    protected readonly GltfModelLoader   _loader;
    protected readonly IColliderStrategy _collider;

    public ObjectEntityBuilder(AssetSourceStore store, GltfModelLoader loader, IColliderStrategy collider)
    {
        _store    = store;
        _loader   = loader;
        _collider = collider;
    }

    public virtual AssetType HandledType => AssetType.Object;

    public virtual async Task<AssetEntityRecipe> BuildAsync(string sourceAbsolutePath, AssetType chosenType, CancellationToken ct)
    {
        var recipe = new AssetEntityRecipe
        {
            type             = chosenType,
            selectable       = true,
            interactionLayer = InteractionLayer.SceneObjects,
        };

        var temp = await _loader.LoadAsync(sourceAbsolutePath, Vector3.zero, Quaternion.identity, ct);
        if (temp == null)
            throw new NotSupportedException($"ObjectEntityBuilder: cannot load '{sourceAbsolutePath}'");
        try
        {
            _collider.Measure(temp, out var kind, out var center, out var size);
            recipe.colliderKind   = kind;
            recipe.colliderCenter = center;
            recipe.colliderSize   = size;
        }
        finally { UnityEngine.Object.Destroy(temp); }

        return recipe;
    }

    public virtual Task<GameObject> RestoreAsync(ILabAsset asset, AssetEntityRecipe recipe, Vector3 position, Quaternion rotation, CancellationToken ct)
    {
        if (asset.Source == AssetSource.Builtin)
        {
            if (asset is not BuiltinLabAsset b)
                throw new NotSupportedException($"Builtin asset '{asset.Id}' is not a BuiltinLabAsset");
            return Task.FromResult(UnityEngine.Object.Instantiate(b.Prefab, position, rotation));
        }

        if (string.IsNullOrEmpty(asset.SourceRef))
            throw new NotSupportedException($"Imported asset '{asset.Id}' has no SourceRef");

        // Capability is applied by AssetEntityBuilderRegistry.RestoreAsync (single point).
        return _loader.LoadAsync(_store.AbsolutePath(asset.SourceRef), position, rotation, ct);
    }
}
