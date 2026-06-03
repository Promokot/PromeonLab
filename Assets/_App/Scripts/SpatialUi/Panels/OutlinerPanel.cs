using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

public class OutlinerPanel : MonoBehaviour
{
    [SerializeField] private Transform       _rowsRoot;
    [SerializeField] private OutlinerNode_Item    _objectRowPrefab;
    [SerializeField] private OutlinerNode_Rig_Item _rigRowPrefab;
    [SerializeField] private float           _indentPx = 16f;

    private EventBus    _bus;
    private SceneContext _ctx;

    private readonly Dictionary<string, bool> _bonesActiveByRig = new();

    [Inject]
    public void Construct(EventBus bus, SceneContext ctx)
    {
        _bus = bus;
        _ctx = ctx;
    }

    private void OnEnable()
    {
        if (_bus == null) return;
        _bus.Subscribe<SceneContextChangedEvent>(OnSceneContextChanged);
        _bus.Subscribe<SceneModifiedEvent>(OnModified);
        _bus.Subscribe<SelectionChangedEvent>(OnSelectionChanged);
        _bus.Subscribe<NodeRenamedEvent>(OnNodeRenamed);
        _bus.Subscribe<BonesVisibilityChangedEvent>(OnBonesVisibilityChanged);
        Rebuild();
    }

    private void OnDisable()
    {
        if (_bus == null) return;
        _bus.Unsubscribe<SceneContextChangedEvent>(OnSceneContextChanged);
        _bus.Unsubscribe<SceneModifiedEvent>(OnModified);
        _bus.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);
        _bus.Unsubscribe<NodeRenamedEvent>(OnNodeRenamed);
        _bus.Unsubscribe<BonesVisibilityChangedEvent>(OnBonesVisibilityChanged);
    }

    private void OnModified(SceneModifiedEvent _)            => Rebuild();
    private void OnSelectionChanged(SelectionChangedEvent _) => ApplyHighlight();

    private bool AnyBonesModeActive()
    {
        foreach (var on in _bonesActiveByRig.Values)
            if (on) return true;
        return false;
    }

    private void OnNodeRenamed(NodeRenamedEvent e)
    {
        if (_rowsRoot == null) return;
        foreach (var row in _rowsRoot.GetComponentsInChildren<OutlinerNode_Item>())
            if (row.NodeId == e.NodeId) { row.SetLabel(e.NewName); return; }
    }

    private void OnBonesVisibilityChanged(BonesVisibilityChangedEvent e)
    {
        _bonesActiveByRig[e.RigNodeId] = e.Visible;
        if (_rowsRoot == null) return;
        foreach (var row in _rowsRoot.GetComponentsInChildren<OutlinerNode_Rig_Item>())
            if (row.NodeId == e.RigNodeId) row.SetBonesMode(e.Visible);
    }

    private void Rebuild()
    {
        if (_rowsRoot == null || _objectRowPrefab == null || _rigRowPrefab == null || _ctx.Graph == null) return;
        foreach (Transform t in _rowsRoot) Destroy(t.gameObject);

        var byParent = new Dictionary<string, List<SceneNode>>();
        foreach (var pair in _ctx.Graph.Nodes)
        {
            var p = GetParentId(pair.Value) ?? "";
            if (!byParent.TryGetValue(p, out var list))
                byParent[p] = list = new List<SceneNode>();
            list.Add(pair.Value);
        }
        foreach (var list in byParent.Values)
            list.Sort((a, b) => string.Compare(
                a.DisplayName ?? "", b.DisplayName ?? "",
                StringComparison.OrdinalIgnoreCase));
        AddRowsRecursive(null, 0, byParent);
        ApplyHighlight();
    }

    private string GetParentId(SceneNode n)
    {
        var p = n.transform.parent;
        if (p == null) return null;
        var pn = p.GetComponent<SceneNode>();
        return pn != null ? pn.NodeId : null;
    }

    private void AddRowsRecursive(string parentId, int depth,
                                   Dictionary<string, List<SceneNode>> byParent)
    {
        if (!byParent.TryGetValue(parentId ?? "", out var children)) return;
        foreach (var node in children)
        {
            var isRig = node.GetComponentInChildren<ProxyRigRuntime>(includeInactive: true) != null;
            OutlinerNode_Item row = isRig
                ? Instantiate(_rigRowPrefab, _rowsRoot)
                : Instantiate(_objectRowPrefab, _rowsRoot);

            row.Bind(node, depth * _indentPx, () =>
            {
                // Isolated bone mode: scene-object selection via the outliner is disabled so the user
                // can't break out of bone editing by clicking a row. Bones are picked in-scene; exit
                // is the inspector's Show Bones toggle (which selects programmatically, not via here).
                if (AnyBonesModeActive()) return;
                _ctx.Selection?.Select(node.NodeId);
            });

            if (row is OutlinerNode_Rig_Item rigRow
                && _bonesActiveByRig.TryGetValue(node.NodeId, out var bonesOn))
            {
                rigRow.SetBonesMode(bonesOn);
            }

            AddRowsRecursive(node.NodeId, depth + 1, byParent);
        }
    }

    private void ClearRows()
    {
        if (_rowsRoot == null) return;
        foreach (Transform t in _rowsRoot) Destroy(t.gameObject);
    }

    private void OnSceneContextChanged(SceneContextChangedEvent e)
    {
        // Bones mode is per-scene transient. This panel persists across scene swaps (it lives on the
        // persistent UserPanel), so a rig left in bones mode would otherwise carry _bonesActiveByRig =
        // true into the next scene — leaving the rig row bones-blue and AnyBonesModeActive() blocking
        // all outliner selection until a toggle. Reset it at every scene boundary.
        _bonesActiveByRig.Clear();
        if (e.HasScene) Rebuild();
        else            ClearRows();
    }

    private void ApplyHighlight()
    {
        if (_rowsRoot == null || _ctx.Selection == null) return;
        var selectedId = _ctx.Selection.SelectedNodeId;
        foreach (var row in _rowsRoot.GetComponentsInChildren<OutlinerNode_Item>())
        {
            row.SetVisualState(row.NodeId == selectedId
                ? SelectionVisual.Selected
                : SelectionVisual.None);
        }
    }
}
