using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Type-keyed dispatch for both Build (import) and Restore (spawn / scene-load). Reads the baked recipe
// straight off the record (ILabAsset.Recipe) so callers never deal with recipes directly.
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
        var recipe = asset.Recipe; // null for un-baked Builtin (throws below) or Saved (not yet implemented)

        // Builtin must be baked: a bare prefab with no recipe would spawn uninteractive, so refuse it.
        // The existing catches in AssetSpawner / SceneGraph.OnSceneOpenedAsync log this without crashing.
        if (recipe == null && asset.Source == AssetSource.Builtin)
            throw new NotSupportedException(
                $"Builtin asset '{asset.Id}' has no baked recipe — bake it in the BuiltinAssetLibrary inspector.");

        var go = await Resolve(asset.Type).RestoreAsync(asset, recipe, position, rotation, ct);

        // Single finalization point: builders produce only geometry; selectability/collider/identity
        // are applied here from the recipe. Idempotent (skips if XRPromeonInteractable already present).
        if (go != null && recipe != null)
            InteractionCapability.Apply(go, recipe.interactionLayer, recipe.colliderKind,
                recipe.colliderCenter, recipe.colliderSize, recipe.selectable);

        // BoneBoxes selectors are built on the rig side; register them with the interactable Apply just
        // created so a hit on any selector box selects the whole rig.
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
