using System;
using UnityEngine;

// Render presets for runtime-imported assets, keyed by AssetType. glTF models bring their own
// PBR materials, so today only the Reference (image) path consults this — but Object/Rig spawners
// can opt into overrides here later without changing call sites.
[CreateAssetMenu(menuName = "PromeonLab/ImportRenderProfile")]
public class ImportRenderProfile : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public AssetType Type;
        public Shader    Shader;     // leave null to fall back to a built-in default at the call site
        public bool      TwoSided;   // when true, the material is rendered with Cull Off (both faces)
    }

    [SerializeField] private Entry[] _entries;

    public bool TryGet(AssetType type, out Entry entry)
    {
        if (_entries != null)
            foreach (var e in _entries)
                if (e.Type == type) { entry = e; return true; }
        entry = default;
        return false;
    }
}
