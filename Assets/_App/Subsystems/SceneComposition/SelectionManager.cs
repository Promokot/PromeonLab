using System;
using System.Collections.Generic;
using VContainer.Unity;

public class SelectionManager : ISelectionManager, IStartable, IDisposable
{
    private readonly EventBus     _bus;
    private readonly List<string> _selected = new();   // List сохраняет порядок вставки
    private string _active;

    public IReadOnlyList<string> SelectedIds   => _selected;
    public string                ActiveId      => _active;
    public string                SelectedNodeId => _active;     // back-compat

    public SelectionManager(EventBus bus) => _bus = bus;

    public void Start()   { }
    public void Dispose() { }

    public void Toggle(string nodeId)
    {
        var idx = _selected.IndexOf(nodeId);
        if (idx >= 0)
        {
            _selected.RemoveAt(idx);
            if (_active == nodeId)
                _active = _selected.Count == 0 ? null : _selected[^1];
        }
        else
        {
            _selected.Add(nodeId);
            _active = nodeId;
        }
        Publish();
    }

    public void Select(string nodeId)
    {
        _selected.Clear();
        _selected.Add(nodeId);
        _active = nodeId;
        Publish();
    }

    public void Clear()
    {
        if (_selected.Count == 0) return;
        _selected.Clear();
        _active = null;
        Publish();
    }

    private void Publish() =>
        _bus.Publish(new SelectionChangedEvent
        {
            SelectedNodeId  = _active,
            SelectedNodeIds = _selected.ToArray(),
        });
}
