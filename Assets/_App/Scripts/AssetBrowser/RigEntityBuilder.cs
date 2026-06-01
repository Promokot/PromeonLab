using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Rig (Slice A): a static skinned mesh, selectable as a whole, PLUS a baked skeleton descriptor in the
// recipe for the future proxy-rig slice. BuildAsync measures the collider AND extracts the skeleton
// (graceful: no skeleton → recipe.rig stays null → behaves as a static object). RestoreAsync loads the
// static mesh; whole-rig selectability is applied by the registry. Proxy construction is Slice B.
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

    public async Task<AssetEntityRecipe> BuildAsync(string sourceAbsolutePath, AssetType chosenType, CancellationToken ct)
    {
        var recipe = new AssetEntityRecipe
        {
            type             = AssetType.Rig,
            selectable       = true,
            interactionLayer = InteractionLayer.SceneObjects,
        };

        var temp = await _factory.CreateAsync(sourceAbsolutePath, Vector3.zero, Quaternion.identity, ct);
        if (temp == null)
            throw new NotSupportedException($"RigEntityBuilder: cannot load '{sourceAbsolutePath}'");
        try
        {
            _collider.Measure(temp, out var kind, out var center, out var size);
            recipe.colliderKind   = kind;
            recipe.colliderCenter = center;
            recipe.colliderSize   = size;

            var smr = temp.GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true);
            recipe.rig = RigDefinitionExtractor.FromSkinnedMesh(smr);
            if (recipe.rig == null)
                Debug.LogWarning($"RigEntityBuilder: '{sourceAbsolutePath}' has no skeleton — importing as a static object.");
        }
        finally { UnityEngine.Object.Destroy(temp); }

        return recipe;
    }

    public async Task<GameObject> RestoreAsync(ILabAsset asset, AssetEntityRecipe recipe, Vector3 position, Quaternion rotation, CancellationToken ct)
    {
        GameObject       go;
        TerminalBoneAxis axis;
        if (asset.Source == AssetSource.Builtin)
        {
            if (asset is not BuiltinLabAsset b)
                throw new NotSupportedException($"Builtin asset '{asset.Id}' is not a BuiltinLabAsset");
            go   = UnityEngine.Object.Instantiate(b.Prefab, position, rotation);
            axis = b.TerminalAxis;
        }
        else
        {
            if (string.IsNullOrEmpty(asset.SourceRef))
                throw new NotSupportedException($"Imported asset '{asset.Id}' has no SourceRef");
            go   = await _factory.CreateAsync(_store.AbsolutePath(asset.SourceRef), position, rotation, ct);
            axis = recipe != null && recipe.HasRig ? recipe.rig.TerminalAxis : TerminalBoneAxis.Auto;
        }

        if (go == null) return null;

        var boneNames = recipe != null && recipe.HasRig
            ? recipe.rig.Bones.Select(bn => bn.BoneName).ToList()
            : null;
        _factory.BuildProxyRig(go, boneNames, axis);

        return go;
    }
}
