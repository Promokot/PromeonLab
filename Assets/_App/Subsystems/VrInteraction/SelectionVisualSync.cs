using System;
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
        foreach (var pair in _graph.Nodes)
        {
            var sel = pair.Value.GetComponent<Selectable>();
            if (sel == null) continue;
            sel.SetVisualState(pair.Key == e.SelectedNodeId
                ? SelectionVisual.Selected
                : SelectionVisual.None);
        }
    }
}
