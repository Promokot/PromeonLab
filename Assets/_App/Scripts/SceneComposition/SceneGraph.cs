using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public class SceneGraph : ISceneGraph, IStartable, IDisposable
{
    private readonly EventBus             _bus;
    private readonly IAssetRegistry       _registry;
    private readonly IObjectResolver      _resolver;
    private readonly AppStorage           _storage;
    private readonly AssetEntityBuilderRegistry _spawners;
    private readonly Dictionary<string, SceneNode> _nodes          = new();
    private readonly Dictionary<string, SceneNode> _transientNodes = new();

    private Transform _spawnedRoot;

    public IReadOnlyDictionary<string, SceneNode> Nodes => _nodes;

    public SceneGraph(EventBus bus, IAssetRegistry registry, IObjectResolver resolver, AppStorage storage, AssetEntityBuilderRegistry spawners)
    {
        _bus      = bus;
        _registry = registry;
        _resolver = resolver;
        _storage  = storage;
        _spawners = spawners;
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
        _transientNodes.Clear();
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

    public SceneNode GetNode(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId)) return null;
        if (_nodes.TryGetValue(nodeId, out var n) && n != null) return n;
        if (_transientNodes.TryGetValue(nodeId, out var t))
        {
            if (t == null)
            {
                _transientNodes.Remove(nodeId);
                return null;
            }
            return t;
        }
        return null;
    }

    GameObject ISceneGraph.GetNode(string nodeId) => GetNode(nodeId)?.gameObject;

    public void AddTransientNode(SceneNode sn)
    {
        if (sn == null || string.IsNullOrEmpty(sn.NodeId)) return;
        _transientNodes[sn.NodeId] = sn;
        // Intentionally no SceneModifiedEvent — outliner does not rebuild for bones.
    }

    private SceneNode AddNodeInternal(GameObject go, string nodeId, AssetRef assetRef,
                                       string displayName, string parentId, bool isLoad)
    {
        go.transform.SetParent(_spawnedRoot, worldPositionStays: true);
        // Re-use a pre-attached SceneNode (baked into the prefab per the bake-time refactor)
        // so that all Awake-time references (Selectable._node, XRPromeonInteractable._node) point
        // to the SAME SceneNode instance whose NodeId we now stamp with the runtime GUID.
        var node = go.GetComponent<SceneNode>();
        if (node == null) node = go.AddComponent<SceneNode>();
        node.Init(nodeId, assetRef, displayName);
        if (!string.IsNullOrEmpty(displayName)) go.name = displayName;

        _nodes[nodeId] = node;
        RewriteBoneNodeIds(go, nodeId);

        if (!isLoad) _bus.Publish(new SceneModifiedEvent());
        return node;
    }

    /// Rewrites bake-time-relative bone NodeIds ("pelvis") into runtime composite form
    /// ("bone:{rigNodeId}:pelvis") and registers each bone proxy in the transient-nodes dict.
    private void RewriteBoneNodeIds(GameObject root, string rigNodeId)
    {
        var markers = root.GetComponentsInChildren<BoneSceneNodeMarker>(includeInactive: true);
        foreach (var marker in markers)
        {
            var sn = marker.GetComponent<SceneNode>();
            if (sn == null) continue;
            var boneName = sn.NodeId;
            sn.SetNodeId($"bone:{rigNodeId}:{boneName}");
            AddTransientNode(sn);
        }
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
                    go = await _spawners.RestoreAsync(asset, nd.Position, nd.Rotation, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"SceneGraph: spawn failed for {nd.AssetRef} — skipping node. {ex.Message}");
                    continue;
                }
                go.transform.localScale = nd.Scale;
                AddNodeInternal(go, nd.NodeId, nd.AssetRef, nd.DisplayName, nd.ParentNodeId, isLoad: true);
                _resolver.InjectGameObject(go);
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
        _transientNodes.Clear();
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
