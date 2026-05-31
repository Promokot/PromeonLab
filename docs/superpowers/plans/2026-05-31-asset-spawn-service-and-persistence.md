# Asset Spawn Service + Persistence — Implementation Plan (Slice 1A)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.
>
> **Spec:** `docs/superpowers/specs/2026-05-31-asset-import-pipeline-design.md` (Slice 1, foundation half).
> **Follow-up:** Slice 1B (glTFast + import wizard) builds on the `IAssetSpawner` architecture this
> plan introduces. Slice 2 (runtime rig) is BLOCKED on the concurrent outline edit to
> `PromeonProxyRigBuilder.cs` — this plan does NOT touch that file.
>
> **Git note:** user (Promokot) commits manually — no auto-commit. "Checkpoint" = stop, user commits.
> **Unity note:** the controller compiles + runs EditMode tests via MCP (`refresh_unity`,
> `read_console`, `run_tests`). There are no terminal builds.

**Goal:** Move spawn behavior off the `ILabAsset` data record into a per-type `IAssetSpawner`
service, make asset records pure data, and route both spawn triggers (browser placement + scene
load) through one registry — so persisted Builtin nodes reload correctly and the architecture is
ready for Imported/Reference spawners in 1B.

**Architecture:** `ILabAsset` becomes data only (`{Id, DisplayName, Type, Source, SourceRef, Icon}`).
A new `AssetSpawnerRegistry` dispatches `SpawnAsync(asset, pose)` to the `IAssetSpawner` whose
`HandledType` matches `asset.Type`. `ObjectSpawner` and `RigSpawner` implement the Builtin-prefab
path now; the glTF/image paths land in 1B. `AssetSpawner` (browser) and `SceneGraph` (load) both call
the registry. `AssetType` is re-spelled `{Object, Rig, Reference}` with integer values preserved so
existing JSON/ScriptableObject data needs no migration.

**Tech Stack:** Unity 6000.3.7f1, C# (no namespaces for runtime), VContainer DI (collection
injection via `IReadOnlyList<T>`), Unity Test Framework (NUnit EditMode), `_App.Tests` assembly.

---

## File Structure

| File | Responsibility | Change |
|---|---|---|
| `Assets/_App/Scripts/AssetBrowser/AssetType.cs` | type taxonomy | Modify: `{Object, Rig, Reference}` (int order preserved) |
| `Assets/_App/Scripts/AssetBrowser/ILabAsset.cs` | asset data contract | Modify: drop `SpawnAsync`; add `Source`, `SourceRef` |
| `Assets/_App/Scripts/AssetBrowser/BuiltinLabAsset.cs` | bundled asset record | Modify: drop `SpawnAsync`; add `Source`/`SourceRef`/`Prefab` |
| `Assets/_App/Scripts/AssetBrowser/ImportedLabAsset.cs` | imported asset record | Modify: drop `SpawnAsync`; `_filePath`→`_sourceRef`; `Source` |
| `Assets/_App/Scripts/AssetBrowser/SavedLabAsset.cs` | saved asset record | Modify: drop `SpawnAsync`; add `Source`/`SourceRef` |
| `Assets/_App/Scripts/AssetBrowser/IAssetSpawner.cs` | per-type spawn contract | Create |
| `Assets/_App/Scripts/AssetBrowser/BuiltinAssetSpawnCore.cs` | shared Builtin-prefab spawn helper | Create |
| `Assets/_App/Scripts/AssetBrowser/ObjectSpawner.cs` | spawner for `Object` | Create |
| `Assets/_App/Scripts/AssetBrowser/RigSpawner.cs` | spawner for `Rig` (static in 1A) | Create |
| `Assets/_App/Scripts/AssetBrowser/AssetSpawnerRegistry.cs` | dispatch by `AssetType` | Create |
| `Assets/_App/Scripts/AssetBrowser/AssetSpawner.cs` | browser-placement trigger | Modify: call registry; `Source` from record |
| `Assets/_App/Scripts/SceneComposition/SceneGraph.cs` | scene-load trigger | Modify: call registry; drop `NotImplementedException` branch |
| `Assets/_App/Scripts/SpatialUi/Panels/AssetBrowserPanel.cs` | import entry (temporary) | Modify: compile against new `ImportedLabAsset` ctor + `AssetType.Object` |
| `Assets/_App/Scripts/Bootstrap/RootLifetimeScope.cs` | DI registration | Modify: register spawners + registry |
| `Assets/_App/Tests/AssetBrowser/AssetTypeBinaryCompatTests.cs` | guards JSON int values | Create |
| `Assets/_App/Tests/AssetBrowser/AssetSpawnerRegistryTests.cs` | dispatch behavior | Create |

---

## Phase 1 — Data model: AssetType + ILabAsset become pure data

### Task 1.1: Lock AssetType integer values with a test (TDD red)

**Files:**
- Create: `Assets/_App/Tests/AssetBrowser/AssetTypeBinaryCompatTests.cs`

`JsonUtility` serializes enums by their integer value. Existing `imported.json`, `scene.json`, and
the Builtin/Demo ScriptableObjects hold `Model=0, Rig=1, Texture=2`. The new enum MUST keep those
slots so old data reads back unchanged. This test pins it.

- [ ] **Step 1: Write the test.**

```csharp
using NUnit.Framework;

public class AssetTypeBinaryCompatTests
{
    // JsonUtility serializes enums as their underlying int. These values MUST NOT change:
    // old data was Model=0 / Rig=1 / Texture=2 → maps to Object=0 / Rig=1 / Reference=2.
    [Test] public void Object_IsZero()    => Assert.AreEqual(0, (int)AssetType.Object);
    [Test] public void Rig_IsOne()        => Assert.AreEqual(1, (int)AssetType.Rig);
    [Test] public void Reference_IsTwo()  => Assert.AreEqual(2, (int)AssetType.Reference);
}
```

- [ ] **Step 2: Compile + run, expect RED.** `[MCP] refresh_unity (compile)`, then `read_console`.
  Expected: `CS0117` — `'AssetType' does not contain a definition for 'Object'` (the enum still has
  old members). This confirms the test targets the new shape.

### Task 1.2: Re-spell AssetType (TDD green)

**Files:**
- Modify: `Assets/_App/Scripts/AssetBrowser/AssetType.cs`

- [ ] **Step 1: Replace the enum.**

```csharp
public enum AssetType { Object, Rig, Reference }
```

- [ ] **Step 2: Compile.** `[MCP] refresh_unity (compile)` → `read_console`. Expected: now-FAILING
  compiles elsewhere (`AssetType.Model`/`Texture` references in `AssetBrowserPanel`, `AssetImporter`,
  `DemoAssetCatalog` authoring). That is expected — Tasks 1.3–1.6 + 2.x fix call sites. Do NOT run
  tests yet; finish the data layer first.

### Task 1.3: Strip spawn from ILabAsset; add Source + SourceRef

**Files:**
- Modify: `Assets/_App/Scripts/AssetBrowser/ILabAsset.cs`

- [ ] **Step 1: Rewrite the interface to pure data.**

```csharp
using UnityEngine;

public interface ILabAsset
{
    string      Id          { get; }
    string      DisplayName { get; }
    AssetType   Type        { get; }
    AssetSource Source      { get; }   // which library this record lives in
    string      SourceRef   { get; }   // relative path under asset-library/sources; null for Builtin
    Sprite      Icon        { get; }
}
```

### Task 1.4: BuiltinLabAsset — data + expose Prefab

**Files:**
- Modify: `Assets/_App/Scripts/AssetBrowser/BuiltinLabAsset.cs`

- [ ] **Step 1: Replace the file.** (Spawn moves to `BuiltinAssetSpawnCore`; the spawner reads `Prefab`.)

```csharp
using System;
using UnityEngine;

[Serializable]
public struct BuiltinLabAsset : ILabAsset
{
    [SerializeField] private string     _id;
    [SerializeField] private string     _displayName;
    [SerializeField] private AssetType  _type;
    [SerializeField] private Sprite     _icon;
    [SerializeField] private GameObject _prefab;

    public string      Id          => _id;
    public string      DisplayName => _displayName;
    public AssetType   Type        => _type;
    public AssetSource Source      => AssetSource.Builtin;
    public string      SourceRef   => null;
    public Sprite      Icon        => _icon;
    public GameObject  Prefab      => _prefab;
}
```

### Task 1.5: ImportedLabAsset — data + `_sourceRef`

**Files:**
- Modify: `Assets/_App/Scripts/AssetBrowser/ImportedLabAsset.cs`

> Note: the old `_filePath` field (absolute device path) is replaced by `_sourceRef` (relative path
> under `sources/`, populated by the 1B importer). Any pre-existing `imported.json` entries written
> with `_filePath` will not map and are effectively reset — acceptable, since those entries could
> never spawn anyway (their `SpawnAsync` threw).

- [ ] **Step 1: Replace the file.**

```csharp
using System;
using UnityEngine;

[Serializable]
public class ImportedLabAsset : ILabAsset
{
    [SerializeField] private string    _id;
    [SerializeField] private string    _displayName;
    [SerializeField] private AssetType _type;
    [SerializeField] private string    _sourceRef;

    public string      Id          => _id;
    public string      DisplayName => _displayName;
    public AssetType   Type        => _type;
    public AssetSource Source      => AssetSource.Imported;
    public string      SourceRef   => _sourceRef;
    public Sprite      Icon        => null;

    public ImportedLabAsset() { }

    public ImportedLabAsset(string id, string displayName, AssetType type, string sourceRef)
    {
        _id          = id;
        _displayName = displayName;
        _type        = type;
        _sourceRef   = sourceRef;
    }
}
```

### Task 1.6: SavedLabAsset — data

**Files:**
- Modify: `Assets/_App/Scripts/AssetBrowser/SavedLabAsset.cs`

> Saved-library spawn is Slice 3; here we only make the record pure data. `_assetId` is retained
> as-is (its 1C meaning is unchanged); `SourceRef` returns `null` for now.

- [ ] **Step 1: Replace the file.**

```csharp
using System;
using UnityEngine;

[Serializable]
public class SavedLabAsset : ILabAsset
{
    [SerializeField] private string    _id;
    [SerializeField] private string    _displayName;
    [SerializeField] private AssetType _type;
    [SerializeField] private string    _assetId;

    public string      Id          => _id;
    public string      DisplayName => _displayName;
    public AssetType   Type        => _type;
    public AssetSource Source      => AssetSource.Saved;
    public string      SourceRef   => null;
    public Sprite      Icon        => null;
    public string      AssetId     => _assetId;

    public SavedLabAsset() { }

    public SavedLabAsset(string id, string displayName, AssetType type, string assetId)
    {
        _id          = id;
        _displayName = displayName;
        _type        = type;
        _assetId     = assetId;
    }
}
```

---

## Phase 2 — Spawner service

### Task 2.1: IAssetSpawner contract

**Files:**
- Create: `Assets/_App/Scripts/AssetBrowser/IAssetSpawner.cs`

- [ ] **Step 1: Write the interface.**

```csharp
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public interface IAssetSpawner
{
    AssetType HandledType { get; }
    Task<GameObject> SpawnAsync(ILabAsset asset, Vector3 position, Quaternion rotation, CancellationToken ct);
}
```

### Task 2.2: Shared Builtin spawn helper

**Files:**
- Create: `Assets/_App/Scripts/AssetBrowser/BuiltinAssetSpawnCore.cs`

- [ ] **Step 1: Write the helper.** (DRY: both `ObjectSpawner` and `RigSpawner` instantiate a Builtin
  prefab identically; the per-type difference is only future behavior, added in 1B/Slice 2.)

```csharp
using System;
using System.Threading.Tasks;
using UnityEngine;

public static class BuiltinAssetSpawnCore
{
    public static Task<GameObject> SpawnBuiltin(ILabAsset asset, Vector3 position, Quaternion rotation)
    {
        if (asset.Source != AssetSource.Builtin || asset is not BuiltinLabAsset b)
            throw new NotSupportedException(
                $"Spawning source '{asset.Source}' requires the glTF/image loader (Slice 1B). " +
                $"Slice 1A supports only Builtin assets.");
        var go = UnityEngine.Object.Instantiate(b.Prefab, position, rotation);
        return Task.FromResult(go);
    }
}
```

### Task 2.3: ObjectSpawner + RigSpawner

**Files:**
- Create: `Assets/_App/Scripts/AssetBrowser/ObjectSpawner.cs`
- Create: `Assets/_App/Scripts/AssetBrowser/RigSpawner.cs`

- [ ] **Step 1: Write ObjectSpawner.**

```csharp
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class ObjectSpawner : IAssetSpawner
{
    public AssetType HandledType => AssetType.Object;

    public Task<GameObject> SpawnAsync(ILabAsset asset, Vector3 position, Quaternion rotation, CancellationToken ct)
        => BuiltinAssetSpawnCore.SpawnBuiltin(asset, position, rotation);
}
```

- [ ] **Step 2: Write RigSpawner.** (Static in 1A — identical to Object; Slice 2 adds the runtime
  proxy-rig step after the imported skeleton is loaded by 1B.)

```csharp
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class RigSpawner : IAssetSpawner
{
    public AssetType HandledType => AssetType.Rig;

    public Task<GameObject> SpawnAsync(ILabAsset asset, Vector3 position, Quaternion rotation, CancellationToken ct)
        => BuiltinAssetSpawnCore.SpawnBuiltin(asset, position, rotation);
}
```

### Task 2.4: AssetSpawnerRegistry + dispatch test (TDD)

**Files:**
- Create: `Assets/_App/Tests/AssetBrowser/AssetSpawnerRegistryTests.cs`
- Create: `Assets/_App/Scripts/AssetBrowser/AssetSpawnerRegistry.cs`

- [ ] **Step 1: Write the failing test.** (Uses local fakes for `ILabAsset` and `IAssetSpawner` so it
  needs no prefab and no scene.)

```csharp
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

public class AssetSpawnerRegistryTests
{
    private class FakeAsset : ILabAsset
    {
        public FakeAsset(AssetType t) => Type = t;
        public string Id => "fake";
        public string DisplayName => "fake";
        public AssetType Type { get; }
        public AssetSource Source => AssetSource.Builtin;
        public string SourceRef => null;
        public Sprite Icon => null;
    }

    private class FakeSpawner : IAssetSpawner
    {
        public FakeSpawner(AssetType t) => HandledType = t;
        public AssetType HandledType { get; }
        public bool Called;
        public Task<GameObject> SpawnAsync(ILabAsset a, Vector3 p, Quaternion r, CancellationToken ct)
        {
            Called = true;
            return Task.FromResult<GameObject>(null);
        }
    }

    [Test]
    public async Task SpawnAsync_DispatchesToSpawnerForAssetType()
    {
        var obj = new FakeSpawner(AssetType.Object);
        var rig = new FakeSpawner(AssetType.Rig);
        var registry = new AssetSpawnerRegistry(new IAssetSpawner[] { obj, rig });

        await registry.SpawnAsync(new FakeAsset(AssetType.Rig), Vector3.zero, Quaternion.identity, CancellationToken.None);

        Assert.IsTrue(rig.Called);
        Assert.IsFalse(obj.Called);
    }

    [Test]
    public void SpawnAsync_UnknownType_Throws()
    {
        var registry = new AssetSpawnerRegistry(new IAssetSpawner[] { new FakeSpawner(AssetType.Object) });
        Assert.ThrowsAsync<System.NotSupportedException>(async () =>
            await registry.SpawnAsync(new FakeAsset(AssetType.Reference), Vector3.zero, Quaternion.identity, CancellationToken.None));
    }
}
```

- [ ] **Step 2: Run, expect RED** (`AssetSpawnerRegistry` undefined). `[MCP] run_tests` (EditMode,
  filter `AssetSpawnerRegistryTests`). Expected: compile error / fail.

- [ ] **Step 3: Write the registry.**

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class AssetSpawnerRegistry
{
    private readonly Dictionary<AssetType, IAssetSpawner> _byType = new();

    public AssetSpawnerRegistry(IReadOnlyList<IAssetSpawner> spawners)
    {
        foreach (var s in spawners) _byType[s.HandledType] = s;
    }

    public Task<GameObject> SpawnAsync(ILabAsset asset, Vector3 position, Quaternion rotation, CancellationToken ct)
    {
        if (!_byType.TryGetValue(asset.Type, out var spawner))
            throw new NotSupportedException($"No spawner registered for asset type {asset.Type}");
        return spawner.SpawnAsync(asset, position, rotation, ct);
    }
}
```

- [ ] **Step 4: Run, expect GREEN.** `[MCP] run_tests` (EditMode, filter `AssetSpawnerRegistryTests`
  and `AssetTypeBinaryCompatTests`). Expected: PASS. (`AssetType*` now compiles too.)

- [ ] **Step 5: Checkpoint (user commits)** — `feat(assets): asset records become data + per-type spawner registry`

---

## Phase 3 — Route both triggers through the registry

### Task 3.1: AssetSpawner (browser placement) → registry

**Files:**
- Modify: `Assets/_App/Scripts/AssetBrowser/AssetSpawner.cs`

- [ ] **Step 1: Inject the registry.** Replace the constructor + fields (`:10-19`):

```csharp
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
```

- [ ] **Step 2: Use the registry + read `Source` off the record.** Replace `SpawnCoreAsync` (`:27-49`):

```csharp
    private async System.Threading.Tasks.Task SpawnCoreAsync(AssetSpawnRequestedEvent e)
    {
        try
        {
            var go = await _spawners.SpawnAsync(e.Asset, e.Position, e.Rotation, CancellationToken.None);
            var assetRef = new AssetRef { Source = e.Asset.Source, AssetId = e.Asset.Id };
            // SceneGraph.AddNode handles RewriteBoneNodeIds + AddTransientNode for bone proxies.
            _graph.AddNode(go, assetRef, e.Asset.DisplayName);
            _resolver.InjectGameObject(go);
        }
        catch (Exception ex)
        {
            Debug.LogError($"AssetSpawner: failed to spawn '{e.Asset?.DisplayName}'. {ex}");
        }
    }
```

- [ ] **Step 2 (controller): compile.** `[MCP] refresh_unity (compile)` → `read_console`. Expected: no
  `CS` errors from this file (the brittle `is BuiltinLabAsset ? …` source inference is gone).

### Task 3.2: SceneGraph (scene load) → registry

**Files:**
- Modify: `Assets/_App/Scripts/SceneComposition/SceneGraph.cs`

- [ ] **Step 1: Add the registry dependency.** Add a field and extend the constructor (`:11-28`):

```csharp
    private readonly EventBus             _bus;
    private readonly IAssetRegistry       _registry;
    private readonly IObjectResolver      _resolver;
    private readonly AppStorage           _storage;
    private readonly AssetSpawnerRegistry _spawners;
    // ...existing _nodes / _transientNodes / _spawnedRoot fields unchanged...

    public SceneGraph(EventBus bus, IAssetRegistry registry, IObjectResolver resolver, AppStorage storage, AssetSpawnerRegistry spawners)
    {
        _bus      = bus;
        _registry = registry;
        _resolver = resolver;
        _storage  = storage;
        _spawners = spawners;
    }
```

- [ ] **Step 2: Spawn via the registry; drop the NotImplemented special-case.** Replace the spawn
  block inside `OnSceneOpenedAsync` (`:143-156`):

```csharp
                GameObject go;
                try
                {
                    go = await _spawners.SpawnAsync(asset, nd.Position, nd.Rotation, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"SceneGraph: spawn failed for {nd.AssetRef} — skipping node. {ex.Message}");
                    continue;
                }
                go.transform.localScale = nd.Scale;
                AddNodeInternal(go, nd.NodeId, nd.AssetRef, nd.DisplayName, nd.ParentNodeId, isLoad: true);
                _resolver.InjectGameObject(go);
```

- [ ] **Step 3 (controller): compile.** `[MCP] refresh_unity (compile)` → `read_console`. Expected: no
  `CS` errors. (The `catch (NotImplementedException)` block is gone; a general guard remains so one
  bad asset never aborts the whole load.)

### Task 3.3: AssetBrowserPanel — compile against the new record

**Files:**
- Modify: `Assets/_App/Scripts/SpatialUi/Panels/AssetBrowserPanel.cs:185-199`

> The full import rework is Slice 1B. Here we only keep the file compiling: the old code built
> `new ImportedLabAsset(id, name, AssetType.Model, filePath)` — `Model` is gone and the 4th arg is now
> `sourceRef`. Minimal edit; still non-functional (imported assets can't spawn until 1B), which is
> the documented 1A boundary.

- [ ] **Step 1: Update the record construction.** Replace the `HandleImportAsync` body (`:185-199`):

```csharp
    private async System.Threading.Tasks.Task HandleImportAsync(string filePath)
    {
        // SLICE 1A: temporary — produces a record only; real copy-into-storage + typed import
        // is Slice 1B (ImportPipeline). Imported assets do not spawn until 1B.
        var asset = new ImportedLabAsset(
            id:          Guid.NewGuid().ToString("N")[..8],
            displayName: Path.GetFileNameWithoutExtension(filePath),
            type:        AssetType.Object,
            sourceRef:   filePath
        );

        _importedLibrary.Add(asset);
        await _importedLibrary.SaveAsync(CancellationToken.None);

        if (_activeLibrary == _importedLibrary)
            RefreshGrid();
    }
```

- [ ] **Step 2 (controller): compile.** `[MCP] refresh_unity (compile)` → `read_console`. Expected: no
  `CS` errors in `AssetBrowserPanel`.

---

## Phase 4 — DI registration

### Task 4.1: Register spawners + registry at Root

**Files:**
- Modify: `Assets/_App/Scripts/Bootstrap/RootLifetimeScope.cs:30-32`

`SceneGraph`/`AssetSpawner` are scene-scoped and may depend on a root-scoped registry (child→parent
is allowed). The spawners are stateless singletons.

- [ ] **Step 1: Add registrations right after the library/registry block (`:32`).**

```csharp
        builder.Register<AssetRegistry>(Lifetime.Singleton).As<IAssetRegistry>();

        // Per-type spawners + dispatcher. Imported/Reference spawners are added in Slice 1B.
        builder.Register<ObjectSpawner>(Lifetime.Singleton).As<IAssetSpawner>();
        builder.Register<RigSpawner>(Lifetime.Singleton).As<IAssetSpawner>();
        builder.Register<AssetSpawnerRegistry>(Lifetime.Singleton);
```

- [ ] **Step 2 (controller): compile.** `[MCP] refresh_unity (compile)` → `read_console`. Expected: no
  `CS` errors anywhere. (`AssetSpawnerRegistry` resolves `IReadOnlyList<IAssetSpawner>` — VContainer
  collection injection over the two `.As<IAssetSpawner>()` registrations.)

- [ ] **Step 3: Run the full EditMode suite.** `[MCP] run_tests` (EditMode). Expected: all green,
  including `AssetTypeBinaryCompatTests` and `AssetSpawnerRegistryTests`, and the existing
  `SceneGraphTests` / `PromeonProxyRigBuilderTests` (no regressions).

- [ ] **Step 4: Checkpoint (user commits)** — `feat(assets): route browser + scene-load spawns through AssetSpawnerRegistry`

---

## Phase 5 — In-app verification (user)

### Task 5.1: Builtin spawn + persistence round-trip (VR / play mode)

- [ ] **Step 1 [MANUAL / VR or Play]:** In VrEditing, open the asset browser, select a **Builtin**
  asset, place it. Expected: it spawns (unchanged behavior).
- [ ] **Step 2 [MANUAL / VR or Play]:** Leave the scene and re-open it (or restart). Expected: the
  placed Builtin object **reloads in place** — confirms `SceneGraph` now spawns via the registry on
  load and the `Source` stored in `scene.json` resolves correctly.
- [ ] **Step 3 [MANUAL / VR or Play]:** Confirm no console errors referencing `SpawnAsync`,
  `NotImplementedException`, or unresolved DI for `AssetSpawnerRegistry`/`IAssetSpawner`.
- [ ] **Step 4:** Note the result here. If Builtin spawn or reload regressed, the likely cause is a
  missing spawner registration (Task 4.1) or a `Source` mismatch in `AssetRef`.

---

## Self-Review

**Spec coverage (the 1A subset):**
- "Remove `SpawnAsync` from `ILabAsset`; record = pure data" → Tasks 1.3–1.6.
- "`AssetType → {Object, Rig, Reference}`; no migration needed" → Tasks 1.1–1.2 (int values pinned by
  test; `JsonUtility` int-enum compatibility documented).
- "`IAssetSpawner` + by-type registry; Builtin path preserved" → Phase 2 + Task 4.1.
- "Both triggers through one path; drop the `NotImplementedException` drop; persistence fix" → Phase 3
  + Task 5.1.
- Deferred to 1B (explicitly, not placeholders): glTF/image loaders, `ReferenceSpawner`, source-file
  copy, `ImportPipeline`/`IAssetImportHandler`, `ImportWizard`. The `BuiltinAssetSpawnCore` throw and
  the temporary `HandleImportAsync` mark those seams.

**Placeholder scan:** none. The two `NotSupportedException` sites (`BuiltinAssetSpawnCore`, registry
unknown-type) are intentional, tested runtime guards with explicit messages, not unfinished stubs.

**Type consistency:** `IAssetSpawner.HandledType` / `SpawnAsync(ILabAsset, Vector3, Quaternion,
CancellationToken)` is identical across `ObjectSpawner`, `RigSpawner`, the registry, and the test
fakes. `AssetSpawnerRegistry` ctor takes `IReadOnlyList<IAssetSpawner>` (matches VContainer collection
injection in Task 4.1). `ILabAsset` members (`Id, DisplayName, Type, Source, SourceRef, Icon`) match
every implementer (`BuiltinLabAsset`, `ImportedLabAsset`, `SavedLabAsset`) and both test fakes.
`AssetRef { Source, AssetId }` usage in `AssetSpawner` matches the struct (`AssetRef.cs`).

**Risks:**
- VContainer must resolve `IReadOnlyList<IAssetSpawner>` from multiple `.As<IAssetSpawner>()`
  registrations — verified pattern in this codebase's DI style; Task 4.1 Step 2 compile + Task 5.1
  startup confirm it. If resolution fails, register an explicit factory returning the list.
- DemoAssetCatalog / BuiltinAssetLibrary entries authored as `Model`/`Texture` keep working only
  because int values are preserved (Task 1.1 guards this); no re-authoring required.
