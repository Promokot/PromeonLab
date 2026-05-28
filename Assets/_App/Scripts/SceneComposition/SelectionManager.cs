using System;
using VContainer.Unity;

public class SelectionManager : ISelectionManager, IStartable, IDisposable
{
    private readonly EventBus _bus;
    private string _selectedNodeId;

    public string SelectedNodeId => _selectedNodeId;

    public SelectionManager(EventBus bus) => _bus = bus;

    public void Start()   { }
    public void Dispose() { }

    public void Select(string nodeId)
    {
        if (_selectedNodeId == nodeId) return;
        _selectedNodeId = nodeId;
        _bus.Publish(new SelectionChangedEvent { SelectedNodeId = _selectedNodeId });
    }
}
