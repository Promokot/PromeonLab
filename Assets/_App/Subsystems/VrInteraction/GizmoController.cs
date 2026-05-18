using System;
using VContainer.Unity;

public class GizmoController : IStartable, IDisposable
{
    private readonly EventBus _bus;
    private readonly CommandStack _commands;
    private readonly SceneGraph _sceneGraph;

    private SceneNode _target;

    public GizmoController(EventBus bus, CommandStack commands, SceneGraph sceneGraph)
    {
        _bus        = bus;
        _commands   = commands;
        _sceneGraph = sceneGraph;
    }

    public void Start() =>
        _bus.Subscribe<SelectionChangedEvent>(OnSelectionChanged);

    public void Dispose() =>
        _bus.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);

    private void OnSelectionChanged(SelectionChangedEvent e)
    {
        _target = e.SelectedNodeId != null ? _sceneGraph.GetNode(e.SelectedNodeId) : null;
    }

    public void CommitTransform(
        UnityEngine.Transform   target,
        UnityEngine.Vector3     newPosition,
        UnityEngine.Quaternion  newRotation,
        UnityEngine.Vector3     newScale)
    {
        var cmd = new TransformCommand(target, newPosition, newRotation, newScale);
        _commands.Execute(cmd);
    }

    public void CommitMove(UnityEngine.Transform target, UnityEngine.Vector3 newPosition)
        => CommitTransform(target, newPosition, target.rotation, target.localScale);
}
