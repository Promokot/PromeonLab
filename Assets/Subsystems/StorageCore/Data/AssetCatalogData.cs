using System;
using System.Collections.Generic;

[Serializable]
public class AssetCatalogData
{
    public int SchemaVersion = 1;
    public string SceneId;
    public List<AssetEntry> Entries = new();
}
