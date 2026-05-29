using System;
using UnityEngine;

[CreateAssetMenu(menuName = "PromeonLab/NavBarConfig")]
public class NavBarConfig : ScriptableObject, IRegionConfig
{
    [Serializable]
    public struct Entry
    {
        public string    Id;
        public AppMode[] VisibleModes;
        public string    ExclusiveGroup;
        public bool      IsRegionDefault;
    }

    [SerializeField] private Entry[] _entries;

    public bool TryGetEntry(string id, out Entry entry)
    {
        if (_entries != null)
            foreach (var e in _entries)
                if (e.Id == id) { entry = e; return true; }
        entry = default;
        return false;
    }

    public bool TryGetRegion(string moduleId, out string regionKey)
    {
        if (TryGetEntry(moduleId, out var e)) { regionKey = e.ExclusiveGroup; return true; }
        regionKey = null;
        return false;
    }

    public bool TryGetRegionDefault(string regionKey, out string moduleId)
    {
        if (_entries != null && !string.IsNullOrEmpty(regionKey))
            foreach (var e in _entries)
                if (e.IsRegionDefault && e.ExclusiveGroup == regionKey) { moduleId = e.Id; return true; }
        moduleId = null;
        return false;
    }

    public bool IsVisibleInMode(string id, AppMode mode)
    {
        if (!TryGetEntry(id, out var e)) return false;
        if (e.VisibleModes == null) return false;
        foreach (var m in e.VisibleModes)
            if (m == mode) return true;
        return false;
    }
}
