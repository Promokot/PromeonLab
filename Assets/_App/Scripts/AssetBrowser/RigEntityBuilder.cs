using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Rig: a static skinned mesh + a baked skeleton descriptor in the recipe. The shared RecipeFromInstance
// measures the collider AND extracts the skeleton (graceful: no skeleton → recipe.rig null → static
// object). RestoreAsync instantiates the geometry (builtin prefab / imported glTF), then builds the
// runtime proxy rig using axis/invert/bone-names taken from the recipe.
public class RigEntityBuilder : IAssetEntityBuilder
{
    private readonly AssetSourceStore  _store;
    private readonly RigEntityFactory  _factory;
    private readonly IColliderStrategy _collider;

    public RigEntityBuilder(AssetSourceStore store, RigEntityFactory factory, IColliderStrategy collider)
    {
        _store    = store;
        _factory  = factory;
        _collider = collider;
    }

    public AssetType HandledType => AssetType.Rig;

    // Shared with the editor builtin bake. axis/invert are folded into recipe.rig when a skeleton exists;
    // the import path passes Auto/false here and ImportPipeline stamps the wizard choice afterward.
    public static AssetEntityRecipe RecipeFromInstance(GameObject instance, IColliderStrategy collider,
                                                       TerminalBoneAxis axis, bool invert)
    {
        var recipe = new AssetEntityRecipe
        {
            type             = AssetType.Rig,
            selectable       = true,
            interactionLayer = InteractionLayer.SceneObjects,
        };
        collider.Measure(instance, out var kind, out var center, out var size);
        recipe.colliderKind   = kind;
        recipe.colliderCenter = center;
        recipe.colliderSize   = size;

        var smr = instance.GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true);
        recipe.rig = RigDefinitionExtractor.FromSkinnedMesh(smr);
        if (recipe.rig != null)
        {
            recipe.rig.TerminalBonesAxis       = axis;
            recipe.rig.InvertTerminalBonesAxis = invert;
        }
        return recipe;
    }

    public async Task<AssetEntityRecipe> BuildAsync(string sourceAbsolutePath, AssetType chosenType, CancellationToken ct)
    {
        var temp = await _factory.CreateAsync(sourceAbsolutePath, Vector3.zero, Quaternion.identity, ct);
        if (temp == null)
            throw new NotSupportedException($"RigEntityBuilder: cannot load '{sourceAbsolutePath}'");
        try
        {
            var recipe = RecipeFromInstance(temp, _collider, TerminalBoneAxis.Auto, invert: false);
            if (recipe.rig == null)
                Debug.LogWarning($"RigEntityBuilder: '{sourceAbsolutePath}' has no skeleton — importing as a static object.");
            return recipe;
        }
        finally { UnityEngine.Object.Destroy(temp); }
    }

    public async Task<GameObject> RestoreAsync(ILabAsset asset, AssetEntityRecipe recipe, Vector3 position, Quaternion rotation, CancellationToken ct)
    {
        GameObject go;
        if (asset.Source == AssetSource.Builtin)
        {
            if (asset is not BuiltinLabAsset b)
                throw new NotSupportedException($"Builtin asset '{asset.Id}' is not a BuiltinLabAsset");
            go = UnityEngine.Object.Instantiate(b.Prefab, position, rotation);
        }
        else
        {
            if (string.IsNullOrEmpty(asset.SourceRef))
                throw new NotSupportedException($"Imported asset '{asset.Id}' has no SourceRef");
            go = await _factory.CreateAsync(_store.AbsolutePath(asset.SourceRef), position, rotation, ct);
        }

        if (go == null) return null;

        // Axis/invert/bone-names come from the recipe for both sources (builtin is guaranteed to have a
        // recipe — the registry throws otherwise). No skeleton in the recipe → all-bones fallback.
        var axis      = recipe != null && recipe.HasRig ? recipe.rig.TerminalBonesAxis : TerminalBoneAxis.Auto;
        var invert    = recipe != null && recipe.HasRig && recipe.rig.InvertTerminalBonesAxis;
        var boneNames = recipe != null && recipe.HasRig
            ? recipe.rig.Bones.Select(bn => bn.BoneName).ToList()
            : null;
        _factory.BuildProxyRig(go, boneNames, axis, invert);

        return go;
    }
}
