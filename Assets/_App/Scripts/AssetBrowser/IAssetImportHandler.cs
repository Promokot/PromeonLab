using System.Threading;
using System.Threading.Tasks;

public interface IAssetImportHandler
{
    bool CanHandle(string fileExtension);           // ext is lower-case incl. dot, e.g. ".glb"
    AssetType SuggestedType { get; }                // wizard's default selection for this file kind
    Task<ImportedLabAsset> ImportAsync(string sourceFilePath, AssetType chosenType, string displayName, CancellationToken ct);
}
