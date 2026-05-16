using System;
using System.Threading;
using UnityEngine;
using VContainer.Unity;

public class AssetSpawner : IStartable, IDisposable
{
    private readonly EventBus   _bus;
    private readonly SceneGraph _graph;

    public AssetSpawner(EventBus bus, SceneGraph graph)
    {
        _bus   = bus;
        _graph = graph;
    }

    public void Start() =>
        _bus.Subscribe<AssetSpawnRequestedEvent>(OnSpawnRequested);

    public void Dispose() =>
        _bus.Unsubscribe<AssetSpawnRequestedEvent>(OnSpawnRequested);

    private void OnSpawnRequested(AssetSpawnRequestedEvent e) =>
        _ = SpawnCoreAsync(e);

    private async System.Threading.Tasks.Task SpawnCoreAsync(AssetSpawnRequestedEvent e)
    {
        try
        {
            var go = await e.Asset.SpawnAsync(e.Position, e.Rotation, CancellationToken.None);
            _graph.AddNode(go);
        }
        catch (Exception ex)
        {
            Debug.LogError($"AssetSpawner: failed to spawn '{e.Asset?.DisplayName}'. {ex}");
        }
    }
}
