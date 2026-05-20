using System;
using System.Threading;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public class AssetSpawner : IStartable, IDisposable
{
    private readonly EventBus              _bus;
    private readonly SceneGraph            _graph;
    private readonly IInteractableFactory  _factory;
    private readonly IObjectResolver       _resolver;
    private IRigRuntime                    _rigRuntimeCached;
    private bool                           _rigRuntimeResolved;

    public AssetSpawner(EventBus bus, SceneGraph graph, IInteractableFactory factory, IObjectResolver resolver)
    {
        _bus      = bus;
        _graph    = graph;
        _factory  = factory;
        _resolver = resolver;
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
            var assetRef = new AssetRef
            {
                Source  = e.Asset is BuiltinLabAsset  ? AssetSource.Builtin
                        : e.Asset is ImportedLabAsset ? AssetSource.Imported
                        : AssetSource.Saved,
                AssetId = e.Asset.Id,
            };
            _graph.AddNode(go, assetRef, e.Asset.DisplayName);
            _factory.MakeInteractable(go, e.Asset.Capabilities);

            if ((e.Asset.Capabilities & AssetCapabilities.Rig) != 0)
                ApplyRig(go);
        }
        catch (Exception ex)
        {
            Debug.LogError($"AssetSpawner: failed to spawn '{e.Asset?.DisplayName}'. {ex}");
        }
    }

    private void ApplyRig(GameObject go)
    {
        var rigRuntime = ResolveRigRuntimeOrNull();
        if (rigRuntime == null)
        {
            Debug.LogWarning($"AssetSpawner: '{go.name}' has Rig capability but no IRigRuntime is registered in this scope — skipping rig setup.", go);
            return;
        }
        var smr = go.GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true);
        if (smr == null)
        {
            Debug.LogWarning($"AssetSpawner: '{go.name}' has Rig capability but no SkinnedMeshRenderer.", go);
            return;
        }
        var def = rigRuntime.BuildFromSkinnedMesh(smr);
        rigRuntime.ApplyDefinition(def, smr);
    }

    private IRigRuntime ResolveRigRuntimeOrNull()
    {
        if (_rigRuntimeResolved) return _rigRuntimeCached;
        _rigRuntimeResolved = true;
        try { _rigRuntimeCached = _resolver.Resolve<IRigRuntime>(); }
        catch { _rigRuntimeCached = null; }
        return _rigRuntimeCached;
    }
}
