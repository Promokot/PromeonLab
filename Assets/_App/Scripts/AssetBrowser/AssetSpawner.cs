using System;
using System.Threading;
using UnityEngine;
using VContainer;
using VContainer.Unity;
// VContainer.Unity for IObjectResolver.InjectGameObject extension.

public class AssetSpawner : IStartable, IDisposable
{
    private readonly EventBus              _bus;
    private readonly SceneGraph            _graph;
    private readonly IObjectResolver       _resolver;
    private readonly AssetSpawnerRegistry  _spawners;

    public AssetSpawner(EventBus bus, SceneGraph graph, IObjectResolver resolver, AssetSpawnerRegistry spawners)
    {
        _bus      = bus;
        _graph    = graph;
        _resolver = resolver;
        _spawners = spawners;
    }

    public void Start()   => _bus.Subscribe<AssetSpawnRequestedEvent>(OnSpawnRequested);
    public void Dispose() => _bus.Unsubscribe<AssetSpawnRequestedEvent>(OnSpawnRequested);

    private void OnSpawnRequested(AssetSpawnRequestedEvent e) =>
        _ = SpawnCoreAsync(e);

    private async System.Threading.Tasks.Task SpawnCoreAsync(AssetSpawnRequestedEvent e)
    {
        try
        {
            var go = await _spawners.SpawnAsync(e.Asset, e.Position, e.Rotation, CancellationToken.None);
            var assetRef = new AssetRef { Source = e.Asset.Source, AssetId = e.Asset.Id };
            // SceneGraph.AddNode handles RewriteBoneNodeIds + AddTransientNode for bone proxies.
            _graph.AddNode(go, assetRef, e.Asset.DisplayName);
            // Resolve DI on every MonoBehaviour in the spawned hierarchy (XRPromeonInteractable.Construct,
            // PromeonProxyRigBuilder.Construct, etc.).
            _resolver.InjectGameObject(go);
            go.SetInteractionLayerOnColliders(InteractionLayer.SceneObjects);
        }
        catch (Exception ex)
        {
            Debug.LogError($"AssetSpawner: failed to spawn '{e.Asset?.DisplayName}'. {ex}");
        }
    }
}
