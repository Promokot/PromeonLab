using UnityEngine;

public interface ILabAsset
{
    string      Id          { get; }
    string      DisplayName { get; }
    AssetType   Type        { get; }
    AssetSource Source      { get; }   // which library this record lives in
    string      SourceRef   { get; }   // relative path under asset-libraries/sources; null for Builtin
    Sprite      Icon        { get; }
    string      ThumbnailRef { get; }  // relative path (under persistentDataPath) to a thumbnail image; null when none
    AssetEntityRecipe Recipe { get; }  // baked Build→Restore contract; null until baked (Builtin) / imported (Imported); always null for Saved (Slice 3)
}
