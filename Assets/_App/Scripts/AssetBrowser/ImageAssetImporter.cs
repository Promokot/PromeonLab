using System;
using System.Threading;
using System.Threading.Tasks;

public class ImageAssetImporter : IAssetImporter
{
    private readonly ImportedSourceProvider _store;

    public ImageAssetImporter(ImportedSourceProvider store) => _store = store;

    public bool CanHandle(string ext) => ext == ".png" || ext == ".jpg" || ext == ".jpeg";

    public AssetType SuggestedType => AssetType.Reference;

    public async Task<ImportedLabAsset> ImportAsync(string sourceFilePath, AssetType chosenType, string displayName, CancellationToken ct)
    {
        var id  = Guid.NewGuid().ToString("N")[..8];
        var rel = await _store.CopyAsync(id, sourceFilePath, ct);
        return new ImportedLabAsset(id, displayName, chosenType, rel);
    }
}
