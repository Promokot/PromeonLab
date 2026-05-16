using System;

[Serializable]
public struct AssetRef
{
    public AssetSource Source;
    public string      AssetId;

    public override string ToString() => $"{Source}/{AssetId}";
}
