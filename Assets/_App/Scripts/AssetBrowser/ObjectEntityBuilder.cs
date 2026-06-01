using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Object = static mesh. Build loads the glTF once (imported) to confirm it loads; the collider is a
// per-renderer convex MeshCollider built at restore from the live mesh, so the recipe only records the
// kind. RecipeFromInstance is shared with the editor builtin bake.
public class ObjectEntityBuilder : IAssetEntityBuilder
{
    protected readonly AssetSourceStore    _store;
    protected readonly ObjectEntityFactory _factory;

    public ObjectEntityBuilder(AssetSourceStore store, ObjectEntityFactory factory)
    {
        _store   = store;
        _factory = factory;
    }

    public virtual AssetType HandledType => AssetType.Object;

    // Shared, synchronous, DI-light: decide the recipe from a live GameObject. Object → ConvexMesh.
    // `instance` is unused here (the convex hull is derived from the live mesh at restore, not at bake),
    // but the param is kept so this mirrors RigEntityBuilder.RecipeFromInstance and lets the editor bake
    // call both via the same Func<GameObject, AssetEntityRecipe>.
    public static AssetEntityRecipe RecipeFromInstance(GameObject instance, AssetType chosenType)
        => new AssetEntityRecipe
        {
            type             = chosenType,
            selectable       = true,
            interactionLayer = InteractionLayer.SceneObjects,
            colliderKind     = ColliderKind.ConvexMesh,
        };

    public virtual async Task<AssetEntityRecipe> BuildAsync(string sourceAbsolutePath, AssetType chosenType, CancellationToken ct)
    {
        var temp = await _factory.CreateAsync(sourceAbsolutePath, Vector3.zero, Quaternion.identity, ct);
        if (temp == null)
            throw new NotSupportedException($"ObjectEntityBuilder: cannot load '{sourceAbsolutePath}'");
        try { return RecipeFromInstance(temp, chosenType); }
        finally { UnityEngine.Object.Destroy(temp); }
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

        return _factory.CreateAsync(_store.AbsolutePath(asset.SourceRef), position, rotation, ct);
    }
}
