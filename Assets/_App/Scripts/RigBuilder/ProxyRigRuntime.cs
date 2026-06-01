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
    private Transform     _proxyRoot;
    private Collider      _rootCollider;       // resolved lazily (added by the registry AFTER build)
    private bool          _rootColliderResolved;

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
    public void Bind(Transform proxyRoot, List<GameObject> proxyGOs)
    {
        _proxyRoot = proxyRoot;
        _proxyGOs.Clear();
        _proxyGOs.AddRange(proxyGOs);
        SetBonesInteractive(false); // start in whole-rig select mode
    }

    private Collider RootCollider()
    {
        if (!_rootColliderResolved)
        {
            _rootCollider = GetComponent<Collider>();
            _rootColliderResolved = true;
        }
        return _rootCollider;
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

        var rootCol = RootCollider();
        if (rootCol != null) rootCol.enabled = !enabled;

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
