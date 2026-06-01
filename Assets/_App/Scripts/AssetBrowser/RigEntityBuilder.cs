using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Rig: a static skinned mesh + a baked skeleton descriptor. Selection collider = box colliders along
// the skeleton to boneColliderDepth, built at restore by RigEntityFactory (BoneBoxes). A skeleton-less
// import is a static mesh → ConvexMesh fallback so it is still selectable.
public class RigEntityBuilder : IAssetEntityBuilder
{
    private readonly AssetSourceStore _store;
    private readonly RigEntityFactory _factory;

    public RigEntityBuilder(AssetSourceStore store, RigEntityFactory factory)
    {
        _store   = store;
        _factory = factory;
    }

    public AssetType HandledType => AssetType.Rig;

    // Shared with the editor builtin bake. axis/invert fold into recipe.rig when a skeleton exists;
    // the import path passes Auto/false here and ImportPipeline stamps the wizard choice afterward.
    public static AssetEntityRecipe RecipeFromInstance(GameObject instance, TerminalBoneAxis axis, bool invert)
    {
        var recipe = new AssetEntityRecipe
        {
            type             = AssetType.Rig,
            selectable       = true,
            interactionLayer = InteractionLayer.SceneObjects,
        };

        var smr = instance.GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true);
        recipe.rig = RigDefinitionExtractor.FromSkinnedMesh(smr);
        if (recipe.rig != null)
        {
            recipe.rig.TerminalBonesAxis       = axis;
            recipe.rig.InvertTerminalBonesAxis = invert;
            recipe.colliderKind     = ColliderKind.BoneBoxes;
            recipe.boneColliderDepth = 3;
        }
        else
        {
            recipe.colliderKind = ColliderKind.ConvexMesh; // skeleton-less → static mesh
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
            var recipe = RecipeFromInstance(temp, TerminalBoneAxis.Auto, invert: false);
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

        var axis      = recipe != null && recipe.HasRig ? recipe.rig.TerminalBonesAxis : TerminalBoneAxis.Auto;
        var invert    = recipe != null && recipe.HasRig && recipe.rig.InvertTerminalBonesAxis;
        var depth     = recipe != null ? recipe.boneColliderDepth : 3;
        var boneNames = recipe != null && recipe.HasRig
            ? recipe.rig.Bones.Select(bn => bn.BoneName).ToList()
            : null;
        _factory.BuildProxyRig(go, boneNames, axis, invert, depth);
        // The selector boxes are built+bound here, but RegisterSelectorColliders() is the registry's
        // job — it runs after InteractionCapability.Apply creates the root interactable (see
        // AssetEntityBuilderRegistry.RestoreAsync). Don't register here: the interactable doesn't exist yet.
        return go;
    }
}
