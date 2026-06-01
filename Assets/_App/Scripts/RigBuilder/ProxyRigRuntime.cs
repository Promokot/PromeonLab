using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;

// Per-rig runtime coordinator for the proxy-bone hierarchy. Built and bound by
// RigEntityFactory.BuildProxyRig. Drives selection outline + visuals/bone-mode toggles.
// Holds no construction logic (that is the factory's job).
public class ProxyRigRuntime : MonoBehaviour
{
    private readonly List<GameObject> _proxyGOs = new();
    private Transform               _proxyRoot;
    private readonly List<Collider> _selectorColliders = new(); // whole-rig select boxes (SceneObjects)
    private readonly Dictionary<string, Transform> _boneProxies = new(); // boneName → proxy transform (pose I/O)

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

    // Called once by the factory right after construction.
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
        SetBonesInteractive(false); // start in whole-rig select mode
    }

    // Per-bone pose I/O for scene persistence. The proxy's LOCAL transform is the authoritative pose
    // input (BoneFollower copies it onto the real bone each LateUpdate), so we capture/restore proxy
    // locals keyed by bone name. No-ops for null/empty input or unknown bone names.
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

    // Registers the whole-rig selector boxes with the root interactable so a hit on any of them selects
    // the rig. Called after InteractionCapability.Apply has created the interactable (by the registry).
    // No-op if there is no interactable yet.
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

            // QuickOutline.OnEnable appends outlineMask/outlineFill without dedupe — strip stacked
            // copies before re-enabling so stencil writes don't conflict (the "bone outline needs a
            // click" bug).
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
                    outline.RenderPriority = 1; // above the selected-mesh outline (priority 0)
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

    // Reflects the current selection on every bone proxy: outline color (selected vs idle) AND the
    // primary-submesh material — the selected bone swaps to BoneSelectedMaterial (emissive warm orange),
    // mirroring how the gizmo adopts its active material. Outline passes (submeshes 1+) are untouched.
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

    // Swaps submesh 0 (the diamond's base material) between the idle and selected materials from the
    // config, leaving any appended outline passes (submeshes 1+) in place. Shared materials, so no
    // per-instance leak. No-op if the config or its materials are unassigned.
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
