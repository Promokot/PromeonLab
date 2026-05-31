using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Reference = a textured quad standing on the floor. Build reads the image dimensions once to fix
// aspect + collider box; Restore rebuilds the quad from the recipe and attaches capability.
public class ReferenceEntityBuilder : IAssetEntityBuilder
{
    private readonly AssetSourceStore     _store;
    private readonly ReferenceQuadFactory _quads;

    public ReferenceEntityBuilder(AssetSourceStore store, ReferenceQuadFactory quads)
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

        const float h = 1f, gap = 0.5f;
        var recipe = new AssetEntityRecipe
        {
            type               = AssetType.Reference,
            selectable         = true,
            interactionLayer   = InteractionLayer.SceneObjects,
            colliderKind       = ColliderKind.Box,
            colliderCenter     = new Vector3(0f, gap + h * 0.5f, 0f),
            colliderSize       = new Vector3(aspect, h, 0.02f),
            referenceAspect    = aspect,
            referenceBottomGap = gap,
            referenceTwoSided  = true,
        };
        return Task.FromResult(recipe);
    }

    public async Task<GameObject> RestoreAsync(ILabAsset asset, AssetEntityRecipe recipe, Vector3 position, Quaternion rotation, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(asset.SourceRef))
            throw new NotSupportedException($"Reference asset '{asset.Id}' has no SourceRef");

        var abs = _store.AbsolutePath(asset.SourceRef);
        var r   = recipe ?? await BuildAsync(abs, AssetType.Reference, ct); // legacy fallback

        var go = await _quads.CreateAsync(abs, position, rotation,
            r.referenceAspect, r.referenceBottomGap, r.referenceTwoSided, ct);
        if (go == null) return null;

        InteractionCapability.Apply(go, r.interactionLayer, r.colliderKind, r.colliderCenter, r.colliderSize, r.selectable);
        return go;
    }
}
