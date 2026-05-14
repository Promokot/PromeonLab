using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

public class SceneGraph : IStartable, IDisposable
{
    private readonly EventBus _bus;
    private readonly Dictionary<string, SceneNode> _nodes = new();

    public IReadOnlyDictionary<string, SceneNode> Nodes => _nodes;

    public SceneGraph(EventBus bus) => _bus = bus;

    public void Start() =>
        _bus.Subscribe<SceneOpenedEvent>(OnSceneOpened);

    public void Dispose() =>
        _bus.Unsubscribe<SceneOpenedEvent>(OnSceneOpened);

    public SceneNode AddNode(GameObject go)
    {
        var nodeId = Guid.NewGuid().ToString("N")[..8];
        var node   = go.AddComponent<SceneNode>();
        node.Init(nodeId);
        _nodes[nodeId] = node;
        _bus.Publish(new SceneModifiedEvent());
        return node;
    }

    public void RemoveNode(string nodeId)
    {
        if (!_nodes.TryGetValue(nodeId, out var node)) return;
        _nodes.Remove(nodeId);
        UnityEngine.Object.Destroy(node.gameObject);
        _bus.Publish(new SceneModifiedEvent());
    }

    public SceneNode GetNode(string nodeId) =>
        _nodes.TryGetValue(nodeId, out var n) ? n : null;

    private void OnSceneOpened(SceneOpenedEvent _) { }
}
