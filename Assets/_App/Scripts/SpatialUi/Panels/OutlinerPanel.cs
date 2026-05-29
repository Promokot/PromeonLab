using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

public class OutlinerPanel : MonoBehaviour
{
    [SerializeField] private Transform       _rowsRoot;
    [SerializeField] private OutlinerItem    _objectRowPrefab;
    [SerializeField] private RigOutlinerItem _rigRowPrefab;
    [SerializeField] private float           _indentPx = 16f;

    private EventBus          _bus;
    private SceneGraph        _graph;
    private ISelectionManager _selection;

    private readonly Dictionary<string, bool> _bonesActiveByRig = new();

    [Inject]
    public void Construct(EventBus bus, SceneGraph graph, ISelectionManager selection)
    {
        _bus       = bus;
        _graph     = graph;
        _selection = selection;
    }

    private void OnEnable()
    {
        if (_bus == null) return;
        _bus.Subscribe<SceneModifiedEvent>(OnModified);
        _bus.Subscribe<SelectionChangedEvent>(OnSelectionChanged);
        _bus.Subscribe<NodeRenamedEvent>(OnNodeRenamed);
        _bus.Subscribe<BonesVisibilityChangedEvent>(OnBonesVisibilityChanged);
        Rebuild();
    }

    private void OnDisable()
    {
        if (_bus == null) return;
        _bus.Unsubscribe<SceneModifiedEvent>(OnModified);
        _bus.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);
        _bus.Unsubscribe<NodeRenamedEvent>(OnNodeRenamed);
        _bus.Unsubscribe<BonesVisibilityChangedEvent>(OnBonesVisibilityChanged);
    }

    private void OnModified(SceneModifiedEvent _)            => Rebuild();
    private void OnSelectionChanged(SelectionChangedEvent _) => ApplyHighlight();

    private void OnNodeRenamed(NodeRenamedEvent e)
    {
        if (_rowsRoot == null) return;
        foreach (var row in _rowsRoot.GetComponentsInChildren<OutlinerItem>())
            if (row.NodeId == e.NodeId) { row.SetLabel(e.NewName); return; }
    }

    private void OnBonesVisibilityChanged(BonesVisibilityChangedEvent e)
    {
        _bonesActiveByRig[e.RigNodeId] = e.Visible;
        if (_rowsRoot == null) return;
        foreach (var row in _rowsRoot.GetComponentsInChildren<RigOutlinerItem>())
            if (row.NodeId == e.RigNodeId) row.SetBonesMode(e.Visible);
    }

    private void Rebuild()
    {
        if (_rowsRoot == null || _objectRowPrefab == null || _rigRowPrefab == null || _graph == null) return;
        foreach (Transform t in _rowsRoot) Destroy(t.gameObject);

        var byParent = new Dictionary<string, List<SceneNode>>();
        foreach (var pair in _graph.Nodes)
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
            var isRig = node.GetComponentInChildren<PromeonProxyRigBuilder>(includeInactive: true) != null;
            OutlinerItem row = isRig
                ? Instantiate(_rigRowPrefab, _rowsRoot)
                : Instantiate(_objectRowPrefab, _rowsRoot);

            row.Bind(node, depth * _indentPx, () => _selection.Select(node.NodeId));

            if (row is RigOutlinerItem rigRow
                && _bonesActiveByRig.TryGetValue(node.NodeId, out var bonesOn))
            {
                rigRow.SetBonesMode(bonesOn);
            }

            AddRowsRecursive(node.NodeId, depth + 1, byParent);
        }
    }

    private void ApplyHighlight()
    {
        if (_rowsRoot == null || _selection == null) return;
        var selectedId = _selection.SelectedNodeId;
        foreach (var row in _rowsRoot.GetComponentsInChildren<OutlinerItem>())
        {
            row.SetVisualState(row.NodeId == selectedId
                ? SelectionVisual.Selected
                : SelectionVisual.None);
        }
    }
}
