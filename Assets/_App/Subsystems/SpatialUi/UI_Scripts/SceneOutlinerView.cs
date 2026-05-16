using System.Collections.Generic;
using UnityEngine;
using VContainer;

public class SceneOutlinerView : MonoBehaviour
{
    [SerializeField] private Transform        _rowsRoot;
    [SerializeField] private SceneOutlinerRow _rowPrefab;
    [SerializeField] private float            _indentPx = 16f;

    private EventBus          _bus;
    private SceneGraph        _graph;
    private ISelectionManager _selection;

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
        Rebuild();
    }

    private void OnDisable()
    {
        if (_bus == null) return;
        _bus.Unsubscribe<SceneModifiedEvent>(OnModified);
        _bus.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);
    }

    private void OnModified(SceneModifiedEvent _)              => Rebuild();
    private void OnSelectionChanged(SelectionChangedEvent _)   => ApplyHighlight();

    private void Rebuild()
    {
        if (_rowsRoot == null || _rowPrefab == null || _graph == null) return;
        foreach (Transform t in _rowsRoot) Destroy(t.gameObject);

        var byParent = new Dictionary<string, List<SceneNode>>();
        foreach (var pair in _graph.Nodes)
        {
            var p = GetParentId(pair.Value) ?? "";
            if (!byParent.TryGetValue(p, out var list))
                byParent[p] = list = new List<SceneNode>();
            list.Add(pair.Value);
        }
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
            var row = Instantiate(_rowPrefab, _rowsRoot);
            row.Bind(node, depth * _indentPx, () => _selection.Toggle(node.NodeId));
            AddRowsRecursive(node.NodeId, depth + 1, byParent);
        }
    }

    private void ApplyHighlight()
    {
        if (_rowsRoot == null || _selection == null) return;
        var active = _selection.ActiveId;
        var set    = new HashSet<string>(_selection.SelectedIds);
        foreach (var row in _rowsRoot.GetComponentsInChildren<SceneOutlinerRow>())
        {
            var state = row.NodeId == active ? SelectionVisual.Active
                      : set.Contains(row.NodeId) ? SelectionVisual.InSet
                                                 : SelectionVisual.None;
            row.SetVisualState(state);
        }
    }
}
