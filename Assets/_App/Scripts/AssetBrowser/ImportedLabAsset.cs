using System;
using UnityEngine;

[Serializable]
public class ImportedLabAsset : ILabAsset
{
    [SerializeField] private string    _id;
    [SerializeField] private string    _displayName;
    [SerializeField] private AssetType _type;
    [SerializeField] private string    _sourceRef;

    public string      Id          => _id;
    public string      DisplayName => _displayName;
    public AssetType   Type        => _type;
    public AssetSource Source      => AssetSource.Imported;
    public string      SourceRef   => _sourceRef;
    public Sprite      Icon        => null;

    public ImportedLabAsset() { }

    public ImportedLabAsset(string id, string displayName, AssetType type, string sourceRef)
    {
        _id          = id;
        _displayName = displayName;
        _type        = type;
        _sourceRef   = sourceRef;
    }
}
