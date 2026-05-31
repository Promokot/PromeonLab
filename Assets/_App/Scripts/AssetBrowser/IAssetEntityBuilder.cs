using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public interface IAssetEntityBuilder
{
    AssetType HandledType { get; }

    // Once: inspect the raw source, decide everything, return a serializable recipe.
    Task<AssetEntityRecipe> BuildAsync(string sourceAbsolutePath, AssetType chosenType, CancellationToken ct);

    // Many: materialize deterministically. Builtin → Instantiate(prefab) (recipe ignored);
    // Imported → load source + apply the recipe. Never makes decisions.
    Task<GameObject> RestoreAsync(ILabAsset asset, AssetEntityRecipe recipe, Vector3 position, Quaternion rotation, CancellationToken ct);
}
