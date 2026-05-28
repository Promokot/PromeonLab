using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DemoAssetCatalog", menuName = "PromeonLab/DemoAssetCatalog")]
public class DemoAssetCatalog : ScriptableObject
{
    [Serializable]
    public struct DemoEntry
    {
        public string FileName;
        public GameObject Prefab;
        public AssetType Type;
        public Sprite Icon;
    }

    [SerializeField] private List<DemoEntry> _entries = new();

    public bool TryFind(string fileName, out DemoEntry entry)
    {
        foreach (var e in _entries)
        {
            if (string.Equals(e.FileName, fileName, StringComparison.OrdinalIgnoreCase))
            {
                entry = e;
                return true;
            }
        }
        entry = default;
        return false;
    }

    public IReadOnlyList<DemoEntry> AllEntries => _entries;
}
