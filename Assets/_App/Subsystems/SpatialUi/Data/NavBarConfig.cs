using System;
using UnityEngine;

[CreateAssetMenu(menuName = "PromeonLab/NavBarConfig")]
public class NavBarConfig : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public string    Id;
        public AppMode[] VisibleModes;
        public bool      StartsEnabled;
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

    public bool IsVisibleInMode(string id, AppMode mode)
    {
        if (!TryGetEntry(id, out var e)) return false;
        if (e.VisibleModes == null) return false;
        foreach (var m in e.VisibleModes)
            if (m == mode) return true;
        return false;
    }
}
