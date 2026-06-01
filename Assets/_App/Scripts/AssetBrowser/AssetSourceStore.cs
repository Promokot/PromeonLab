using System.IO;
using System.Threading;
using System.Threading.Tasks;

public class AssetSourceStore
{
    private readonly PathProvider _paths;

    public AssetSourceStore(PathProvider paths) => _paths = paths;

    /// Copies the picked file into asset-libraries/sources/{assetId}{ext} and returns the path
    /// relative to persistentDataPath (stored as the asset's SourceRef).
    public async Task<string> CopyAsync(string assetId, string sourceFilePath, CancellationToken ct)
    {
        var ext  = Path.GetExtension(sourceFilePath);
        var dest = _paths.SourcePath(assetId, ext);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);

        using (var src = File.OpenRead(sourceFilePath))
        using (var dst = File.Create(dest))
            await src.CopyToAsync(dst, 81920, ct);

        return Path.Combine("asset-libraries", "sources", assetId + ext);
    }

    public string AbsolutePath(string sourceRef) => Path.Combine(_paths.RootForSources, sourceRef);

    /// Deletes the raw source file backing an asset record (no-op if the ref is empty or the
    /// file is already gone). Builtin assets have no SourceRef, so this is only meaningful for
    /// Imported/Saved entries.
    public void Delete(string sourceRef)
    {
        if (string.IsNullOrEmpty(sourceRef)) return;
        var abs = AbsolutePath(sourceRef);
        if (File.Exists(abs)) File.Delete(abs);
    }
}
