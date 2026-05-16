using System;
using System.Collections.Generic;
using VContainer.Unity;

public class SelectionVisualSync : IStartable, IDisposable
{
    private readonly EventBus   _bus;
    private readonly SceneGraph _graph;

    public SelectionVisualSync(EventBus bus, SceneGraph graph)
    {
        _bus   = bus;
        _graph = graph;
    }

    public void Start()   => _bus.Subscribe<SelectionChangedEvent>(OnSelectionChanged);
    public void Dispose() => _bus.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);

    private void OnSelectionChanged(SelectionChangedEvent e)
    {
        var activeId = e.SelectedNodeId;
        var set      = e.SelectedNodeIds == null ? new HashSet<string>() : new HashSet<string>(e.SelectedNodeIds);
        foreach (var pair in _graph.Nodes)
        {
            var sel = pair.Value.GetComponent<Selectable>();
            if (sel == null) continue;
            var state = pair.Key == activeId
                ? SelectionVisual.Active
                : set.Contains(pair.Key) ? SelectionVisual.InSet
                                         : SelectionVisual.None;
            sel.SetVisualState(state);
        }
    }
}
