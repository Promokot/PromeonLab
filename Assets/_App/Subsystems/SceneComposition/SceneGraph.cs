using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

public class SceneGraph : ISceneGraph, IStartable, IDisposable
{
    private readonly EventBus             _bus;
    private readonly IAssetRegistry       _registry;
    private readonly IInteractableFactory _factory;
    private readonly AppStorage           _storage;
    private readonly Dictionary<string, SceneNode> _nodes = new();

    private Transform _spawnedRoot;

    public IReadOnlyDictionary<string, SceneNode> Nodes => _nodes;

    public SceneGraph(EventBus bus, IAssetRegistry registry, IInteractableFactory factory, AppStorage storage)
    {
        _bus      = bus;
        _registry = registry;
        _factory  = factory;
        _storage  = storage;
    }

    public void Start()
    {
        _spawnedRoot = new GameObject("[Spawned]").transform;
        _bus.Subscribe<SceneOpenedEvent>(OnSceneOpened);

        var activeId = _storage.ActiveSceneId;
        if (!string.IsNullOrEmpty(activeId))
            _ = OnSceneOpenedAsync(new SceneOpenedEvent { SceneId = activeId });
    }

    public void Dispose()
    {
        _bus.Unsubscribe<SceneOpenedEvent>(OnSceneOpened);
        _nodes.Clear();
        if (_spawnedRoot != null)
            UnityEngine.Object.Destroy(_spawnedRoot.gameObject);
    }

    public SceneNode AddNode(GameObject go, AssetRef assetRef, string displayName, string parentId = null)
    {
        var nodeId = Guid.NewGuid().ToString("N")[..8];
        return AddNodeInternal(go, nodeId, assetRef, displayName, parentId, isLoad: false);
    }

    void ISceneGraph.AddNode(GameObject go, AssetRef assetRef, string displayName, string parentId) =>
        AddNode(go, assetRef, displayName, parentId);

    public void RemoveNode(string nodeId)
    {
        if (!_nodes.TryGetValue(nodeId, out var node)) return;
        _nodes.Remove(nodeId);
        UnityEngine.Object.Destroy(node.gameObject);
        _bus.Publish(new SceneModifiedEvent());
    }

    public SceneNode GetNode(string nodeId) =>
        _nodes.TryGetValue(nodeId, out var n) ? n : null;

    GameObject ISceneGraph.GetNode(string nodeId) => GetNode(nodeId)?.gameObject;

    private SceneNode AddNodeInternal(GameObject go, string nodeId, AssetRef assetRef,
                                       string displayName, string parentId, bool isLoad)
    {
        go.transform.SetParent(_spawnedRoot, worldPositionStays: true);
        var node = go.AddComponent<SceneNode>();
        node.Init(nodeId, assetRef, displayName);
        if (!string.IsNullOrEmpty(displayName)) go.name = displayName;

        // Selectable wiring is done by SelectionInteractorFactory after SceneNode is added.
        // See SelectionInteractorFactory.MakeInteractable — it looks up SceneNode at call time.

        _nodes[nodeId] = node;
        if (!isLoad) _bus.Publish(new SceneModifiedEvent());
        return node;
    }

    private void OnSceneOpened(SceneOpenedEvent e) => _ = OnSceneOpenedAsync(e);

    private async Task OnSceneOpenedAsync(SceneOpenedEvent e)
    {
        try
        {
            ClearAll();
            var data = await _storage.LoadSceneAsync(e.SceneId, CancellationToken.None);
            if (data?.Nodes == null) return;

            foreach (var nd in data.Nodes)
            {
                var asset = _registry.Find(nd.AssetRef);
                if (asset == null)
                {
                    Debug.LogWarning($"SceneGraph: asset not found {nd.AssetRef}");
                    continue;
                }
                GameObject go;
                try
                {
                    go = await asset.SpawnAsync(nd.Position, nd.Rotation, CancellationToken.None);
                }
                catch (NotImplementedException)
                {
                    Debug.LogWarning($"SceneGraph: SpawnAsync not implemented for {nd.AssetRef} (likely Imported/Saved before Spec B)");
                    continue;
                }
                // Order: spawn → graph adds (and SceneNode) → factory (Selectable links to SceneNode)
                go.transform.localScale = nd.Scale;
                AddNodeInternal(go, nd.NodeId, nd.AssetRef, nd.DisplayName, nd.ParentNodeId, isLoad: true);
                _factory.MakeInteractable(go, asset.Capabilities);
            }

            foreach (var nd in data.Nodes)
            {
                if (string.IsNullOrEmpty(nd.ParentNodeId)) continue;
                if (_nodes.TryGetValue(nd.NodeId, out var child)
                    && _nodes.TryGetValue(nd.ParentNodeId, out var parent))
                {
                    child.transform.SetParent(parent.transform, worldPositionStays: true);
                }
            }
            _bus.Publish(new SceneModifiedEvent());
        }
        catch (Exception ex)
        {
            Debug.LogError($"SceneGraph.OnSceneOpenedAsync failed for '{e.SceneId}': {ex}");
        }
    }

    private void ClearAll()
    {
        _nodes.Clear();
        if (_spawnedRoot != null)
        {
            foreach (Transform t in _spawnedRoot)
                UnityEngine.Object.Destroy(t.gameObject);
        }
    }

    public SceneData CaptureSnapshot(string sceneId, string displayName, string createdAt)
    {
        var data = new SceneData
        {
            SchemaVersion = 2,
            SceneId       = sceneId,
            DisplayName   = displayName,
            CreatedAt     = createdAt,
        };
        foreach (var pair in _nodes)
        {
            var id   = pair.Key;
            var node = pair.Value;
            string parentId = null;
            if (node.transform.parent != null && node.transform.parent != _spawnedRoot)
            {
                var pn = node.transform.parent.GetComponent<SceneNode>();
                if (pn != null) parentId = pn.NodeId;
            }
            data.Nodes.Add(new NodeData
            {
                NodeId       = id,
                AssetRef     = node.AssetRef,
                Position     = node.transform.position,
                Rotation     = node.transform.rotation,
                Scale        = node.transform.localScale,
                DisplayName  = node.DisplayName,
                ParentNodeId = parentId,
            });
        }
        return data;
    }
}
