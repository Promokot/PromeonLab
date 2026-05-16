public class AssetRegistry : IAssetRegistry
{
    private readonly BuiltinAssetLibrary  _builtin;
    private readonly ImportedAssetLibrary _imported;
    private readonly SavedAssetLibrary    _saved;

    public AssetRegistry(BuiltinAssetLibrary builtin, ImportedAssetLibrary imported, SavedAssetLibrary saved)
    {
        _builtin  = builtin;
        _imported = imported;
        _saved    = saved;
    }

    public ILabAsset Find(AssetRef r)
    {
        IAssetLibrary lib = r.Source switch
        {
            AssetSource.Builtin  => _builtin,
            AssetSource.Imported => _imported,
            AssetSource.Saved    => _saved,
            _                    => null,
        };
        if (lib == null) return null;
        foreach (var a in lib.Assets)
            if (a.Id == r.AssetId) return a;
        return null;
    }
}
