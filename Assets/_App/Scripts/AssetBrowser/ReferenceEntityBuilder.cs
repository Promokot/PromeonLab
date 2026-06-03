using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Reference = a textured quad standing on the floor. Build reads the image dimensions once to fix
// aspect + collider box; Restore rebuilds the quad from the recipe and attaches capability.
public class ReferenceEntityBuilder : IAssetEntityBuilder
{
    private readonly ImportedSourceProvider       _store;
    private readonly ReferenceEntityFabricator _quads;

    public ReferenceEntityBuilder(ImportedSourceProvider store, ReferenceEntityFabricator quads)
    {
        _store = store;
        _quads = quads;
    }

    public AssetType HandledType => AssetType.Reference;

    public Task<AssetEntityRecipe> BuildAsync(string sourceAbsolutePath, AssetType chosenType, CancellationToken ct)
    {
        var bytes = File.ReadAllBytes(sourceAbsolutePath);
        var tex   = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
        float aspect = 1f;
        if (tex.LoadImage(bytes) && tex.height != 0)
            aspect = (float)tex.width / tex.height;
        UnityEngine.Object.DestroyImmediate(tex);

        return Task.FromResult(RecipeFromImage(aspect));
    }

    // Single source of the reference-image recipe constants (collider box, floor gap, two-sided),
    // shared by this runtime import path and the editor builtin baker (ReferenceImagePrefabGenerator),
    // so an imported reference and a builtin one always get the same box size / floor clearance.
    public static AssetEntityRecipe RecipeFromImage(float aspect)
    {
        const float h = 1f, gap = 0.5f;
        return new AssetEntityRecipe
        {
            type               = AssetType.Reference,
            selectable         = true,
            interactionLayer   = InteractionLayer.SceneObjects,
            colliderKind       = ColliderKind.Box,
            // Quad pivot is its geometry center; the node's localScale (aspect,1,1) stretches both the
            // mesh and the box, so the box is unit-sized in local space and thin on Z.
            colliderCenter     = Vector3.zero,
            colliderSize       = new Vector3(1f, h, 0.02f),
            // Lift once at spawn so the image's bottom clears the floor by gap, with a centered pivot.
            spawnOffset        = new Vector3(0f, gap + h * 0.5f, 0f),
            referenceAspect    = aspect,
            referenceBottomGap = gap,
            referenceTwoSided  = true,
        };
    }

    public Task<GameObject> RestoreAsync(ILabAsset asset, AssetEntityRecipe recipe, Vector3 position, Quaternion rotation, CancellationToken ct)
    {
        // Builtin Reference is a generated prefab (see ReferenceImagePrefabGenerator): instantiate it
        // like Object/Rig; capability is applied by AssetEntityBuilderRegistry from the baked recipe.
        if (asset.Source == AssetSource.Builtin)
        {
            if (asset is not BuiltinLabAsset b)
                throw new NotSupportedException($"Builtin asset '{asset.Id}' is not a BuiltinLabAsset");
            return Task.FromResult(UnityEngine.Object.Instantiate(b.Prefab, position, rotation));
        }

        if (string.IsNullOrEmpty(asset.SourceRef))
            throw new NotSupportedException($"Reference asset '{asset.Id}' has no SourceRef");
        if (recipe == null)
            throw new NotSupportedException($"Reference asset '{asset.Id}' has no recipe");

        var abs = _store.AbsolutePath(asset.SourceRef);
        // Capability is applied by AssetEntityBuilderRegistry.RestoreAsync (single point).
        return _quads.CreateAsync(abs, position, rotation, recipe.referenceAspect, recipe.referenceTwoSided, ct);
    }
}
