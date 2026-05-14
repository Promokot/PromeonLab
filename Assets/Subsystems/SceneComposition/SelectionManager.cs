using System;
using VContainer.Unity;

public class SelectionManager : IStartable, IDisposable
{
    private readonly EventBus _bus;
    private string _selectedNodeId;

    public string SelectedNodeId => _selectedNodeId;

    public SelectionManager(EventBus bus) => _bus = bus;

    public void Start()  { }
    public void Dispose() { }

    public void Select(string nodeId)
    {
        if (_selectedNodeId == nodeId) return;
        _selectedNodeId = nodeId;
        _bus.Publish(new SelectionChangedEvent { SelectedNodeId = nodeId });
    }

    public void Deselect()
    {
        if (_selectedNodeId == null) return;
        _selectedNodeId = null;
        _bus.Publish(new SelectionChangedEvent { SelectedNodeId = null });
    }
}
