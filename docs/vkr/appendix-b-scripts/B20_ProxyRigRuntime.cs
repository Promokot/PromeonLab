using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;

public class ProxyRigRuntime : MonoBehaviour
{
    private readonly List<GameObject> _proxyGOs = new();
    private Transform               _proxyRoot;
    private readonly List<Collider> _selectorColliders = new(); 
    private readonly Dictionary<string, Transform> _boneProxies = new(); 

    private EventBus       _eventBus;
    private OutlineConfig  _outlineConfig;
    private ProxyRigConfig _proxyConfig;

    [Inject]
    public void Construct(EventBus bus, OutlineConfig outlineConfig, ProxyRigConfig proxyConfig)
    {
        _outlineConfig = outlineConfig;
        _proxyConfig   = proxyConfig;
        if (_eventBus == bus) return;
        if (_eventBus != null) _eventBus.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);
        _eventBus = bus;
        if (_eventBus != null) _eventBus.Subscribe<SelectionChangedEvent>(OnSelectionChanged);
    }

    private void OnDestroy()
    {
        if (_eventBus != null) _eventBus.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);
        _eventBus = null;
    }

    public void Bind(Transform proxyRoot, List<GameObject> proxyGOs, List<Collider> selectorColliders,
                     IReadOnlyDictionary<string, Transform> boneProxies)
    {
        _proxyRoot = proxyRoot;
        _proxyGOs.Clear();
        _proxyGOs.AddRange(proxyGOs);
        _selectorColliders.Clear();
        if (selectorColliders != null) _selectorColliders.AddRange(selectorColliders);
        _boneProxies.Clear();
        if (boneProxies != null)
            foreach (var kv in boneProxies) _boneProxies[kv.Key] = kv.Value;
        SetBonesInteractive(false);
    }

    public List<BonePose> CapturePoses()
    {
        var poses = new List<BonePose>(_boneProxies.Count);
        foreach (var kv in _boneProxies)
        {
            var t = kv.Value;
            if (t == null) continue;
            poses.Add(new BonePose
            {
                BoneName      = kv.Key,
                LocalPosition = t.localPosition,
                LocalRotation = t.localRotation,
                LocalScale    = t.localScale,
            });
        }
        return poses;
    }

    public void ApplyPoses(IReadOnlyList<BonePose> poses)
    {
        if (poses == null) return;
        foreach (var p in poses)
        {
            if (p == null || string.IsNullOrEmpty(p.BoneName)) continue;
            if (!_boneProxies.TryGetValue(p.BoneName, out var t) || t == null) continue;
            t.localPosition = p.LocalPosition;
            t.localRotation = p.LocalRotation;
            t.localScale    = p.LocalScale;
        }
    }

    public void RegisterSelectorColliders()
    {
        var it = GetComponent<XRPromeonInteractable>();
        if (it == null) return;
        it.RegisterColliders(_selectorColliders);
        it.SetInteractionLayer(InteractionLayer.SceneObjects);
    }

    public void SetVisualsEnabled(bool enabled)
    {
        foreach (var go in _proxyGOs)
        {
            if (go == null) continue;
            var mr      = go.GetComponent<MeshRenderer>();
            if (mr      != null) mr.enabled      = enabled;
            var outline = go.GetComponent<Outline>();
            if (outline != null) outline.enabled = enabled;
        }
    }

    public void SetBonesInteractive(bool enabled)
    {
        if (enabled && _proxyRoot != null && !_proxyRoot.gameObject.activeSelf)
            _proxyRoot.gameObject.SetActive(true);

        foreach (var go in _proxyGOs)
        {
            if (go == null) continue;
            if (enabled && !go.activeSelf) go.SetActive(true);

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = enabled;

            if (enabled && mr != null)
            {
                var current = mr.sharedMaterials;
                var cleaned = current.Where(m => m == null ||
                    (!m.name.StartsWith("OutlineMask") && !m.name.StartsWith("OutlineFill"))).ToArray();
                if (cleaned.Length != current.Length)
                    mr.materials = cleaned;
            }

            var outline = go.GetComponent<Outline>();
            if (outline != null)
            {
                if (enabled && _outlineConfig != null)
                    outline.SetOutlineMaterials(_outlineConfig.MaskMaterial, _outlineConfig.FillMaterial);
                outline.enabled = enabled;
                if (enabled)
                {
                    outline.OutlineMode    = Outline.Mode.SilhouetteOnly;
                    outline.RenderPriority = 1; 
                }
            }

            var col = go.GetComponent<Collider>();
            if (col != null) col.enabled = enabled;

            if (enabled)
                go.GetComponent<XRPromeonInteractable>()?.SetInteractionLayer(InteractionLayer.BoneProxies);
        }

        foreach (var sc in _selectorColliders)
            if (sc != null) sc.enabled = !enabled;

        if (enabled) ApplyBoneSelection(null);
    }

    private void OnSelectionChanged(SelectionChangedEvent evt) => ApplyBoneSelection(evt.SelectedNodeId);

    private void ApplyBoneSelection(string selectedId)
    {
        foreach (var go in _proxyGOs)
        {
            if (go == null) continue;
            var sn = go.GetComponent<SceneNode>();
            if (sn == null) continue;
            bool isSelected = sn.NodeId == selectedId;

            var outline = go.GetComponent<Outline>();
            if (outline != null && _outlineConfig != null)
                outline.OutlineColor = isSelected ? _outlineConfig.BoneSelectedColor : _outlineConfig.BoneColor;

            ApplyBoneMaterial(go, isSelected);
        }
    }

    private void ApplyBoneMaterial(GameObject go, bool isSelected)
    {
        if (_proxyConfig == null || _proxyConfig.BoneMaterial == null) return;
        var mr = go.GetComponent<MeshRenderer>();
        if (mr == null) return;

        var target = isSelected && _proxyConfig.BoneSelectedMaterial != null
            ? _proxyConfig.BoneSelectedMaterial
            : _proxyConfig.BoneMaterial;

        var mats = mr.sharedMaterials;
        if (mats.Length == 0 || mats[0] == target) return;
        mats[0] = target;
        mr.sharedMaterials = mats;
    }
}
