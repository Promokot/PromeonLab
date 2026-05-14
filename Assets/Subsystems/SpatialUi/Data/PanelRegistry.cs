using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "PanelRegistry", menuName = "PromeonLab/PanelRegistry")]
public class PanelRegistry : ScriptableObject
{
    [System.Serializable]
    public struct PanelEntry
    {
        public PanelId Id;
        public SpatialPanel Prefab;
        public AppMode[] VisibleInModes;
    }

    [SerializeField] private List<PanelEntry> _panels = new();

    public IReadOnlyList<PanelEntry> Panels => _panels;

    public bool IsVisibleIn(PanelId id, AppMode mode)
    {
        foreach (var entry in _panels)
        {
            if (entry.Id != id) continue;
            foreach (var m in entry.VisibleInModes)
                if (m == mode) return true;
        }
        return false;
    }
}
