using UnityEngine;

public interface ILabAsset
{
    string      Id          { get; }
    string      DisplayName { get; }
    AssetType   Type        { get; }
    AssetSource Source      { get; }   // which library this record lives in
    string      SourceRef   { get; }   // relative path under asset-libraries/sources; null for Builtin
    Sprite      Icon        { get; }
}
