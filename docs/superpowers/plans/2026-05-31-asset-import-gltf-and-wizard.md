# Runtime glTF/Image Import + Wizard — Implementation Plan (Slice 1B)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.
>
> **Spec:** `docs/superpowers/specs/2026-05-31-asset-import-pipeline-design.md` (Slice 1, import half).
> **Builds on Slice 1A** (already shipped + VR-verified): `ILabAsset` is pure data
> (`Id, DisplayName, Type, Source, SourceRef, Icon`); `AssetType = {Object, Rig, Reference}`;
> `IAssetSpawner` + `AssetSpawnerRegistry` dispatch spawn by type; `ObjectSpawner`/`RigSpawner`
> currently handle ONLY Builtin (via `BuiltinAssetSpawnCore`, which throws `NotSupportedException`
> for Imported/Saved). Scene persistence works (saved on `ModeExitingEvent`).
>
> **Git note:** user (Promokot) commits manually — no auto-commit. "Checkpoint" = stop, user commits.
> Commit only this slice's files; do not include another instance's in-flight changes.
> **Unity note:** controller compiles + runs EditMode tests via MCP (`refresh_unity`, `read_console`,
> `run_tests`). `[MANUAL EDITOR / MCP]` steps (package add, prefab/wizard authoring, region config,
> in-VR checks) are done by the user or via MCP. After creating NEW `.cs` files, run
> `refresh_unity (mode=force, scope=all)` so Unity imports them before compiling.

**Goal:** Pick a glTF/GLB or image file from the device, choose its type in a wizard, copy the raw
file into per-library storage, and create a typed Imported asset that spawns at runtime (now and on
scene reload) — completing the "real device import" half of Slice 1.

**Architecture:** A `GltfModelLoader` (wraps glTFast) and a `ReferenceQuadFactory` (image→quad) give
the spawners their runtime geometry; `ObjectSpawner`/`RigSpawner`/`ReferenceSpawner` load from
`asset-library/sources/{assetId}.{ext}` for Imported/Saved sources. The import flow becomes:
file-browser pick → `ImportPipeline` sniffs the extension, opens the `importWizard` region (a
`IRegionSurface` like the file browser), the user confirms type + name, the matching
`IAssetImportHandler` copies the raw file and writes the Imported record. Rig in 1B spawns as a
static skinned mesh (runtime rig build is Slice 2).

**Tech Stack:** Unity 6000.3.7f1, glTFast (`com.unity.cloud.gltfast` 6.x: `GLTFast.GltfImport` →
`LoadGltfBinary(byte[], Uri)` → `InstantiateMainSceneAsync(Transform)`), `Texture2D.LoadImage`
(com.unity.modules.imageconversion, already present), VContainer, Unity Test Framework.

---

## File Structure

| File | Responsibility | Change |
|---|---|---|
| `Packages/manifest.json` | package deps | Modify: add `com.unity.cloud.gltfast` |
| `Assets/_App/Scripts/_App.Runtime.asmdef` | runtime assembly refs | Modify: reference the glTFast assembly |
| `Assets/_App/Scripts/StorageCore/PathProvider.cs` | path building | Modify: add `SourcesDir` + `SourcePath` |
| `Assets/_App/Scripts/AssetBrowser/AssetSourceStore.cs` | copy raw files into `sources/` | Create |
| `Assets/_App/Scripts/AssetBrowser/GltfModelLoader.cs` | glTFast runtime load (glb/gltf → GameObject) | Create |
| `Assets/_App/Scripts/AssetBrowser/ReferenceQuadFactory.cs` | image bytes → textured quad GameObject | Create |
| `Assets/_App/Scripts/AssetBrowser/BuiltinAssetSpawnCore.cs` | spawn core | Modify: route Imported/Saved to loaders |
| `Assets/_App/Scripts/AssetBrowser/ObjectSpawner.cs` | Object spawn | Modify: inject loaders |
| `Assets/_App/Scripts/AssetBrowser/RigSpawner.cs` | Rig spawn (static in 1B) | Modify: inject loaders |
| `Assets/_App/Scripts/AssetBrowser/ReferenceSpawner.cs` | Reference (image) spawn | Create |
| `Assets/_App/Scripts/AssetBrowser/IAssetImportHandler.cs` | raw→record contract | Create |
| `Assets/_App/Scripts/AssetBrowser/GltfImportHandler.cs` | .glb/.gltf importer | Create |
| `Assets/_App/Scripts/AssetBrowser/ImageImportHandler.cs` | .png/.jpg importer | Create |
| `Assets/_App/Scripts/AssetBrowser/ImportPipeline.cs` | sniff → wizard → handler → library | Create |
| `Assets/_App/Scripts/AssetBrowser/Events/ImportRequestedEvent.cs` | pipeline→wizard | Create |
| `Assets/_App/Scripts/AssetBrowser/Events/ImportConfirmedEvent.cs` | wizard→pipeline | Create |
| `Assets/_App/Scripts/SpatialUi/Behaviors/ImportWizardSurface.cs` | wizard panel + region surface | Create |
| `Assets/_App/Scripts/SpatialUi/Panels/AssetBrowserPanel.cs` | remove temp import | Modify: drop `HandleImportAsync`/`OnFilePicked` |
| `Assets/_App/Scripts/Bootstrap/RootLifetimeScope.cs` | DI | Modify: register loaders, ReferenceSpawner, handlers, pipeline, wizard inject |
| `Assets/_App/Content/Prefabs/UI/Panels/UserPanel/...` | wizard prefab + region member | `[MANUAL/MCP]` |
| `Assets/_App/...NavBarConfig.asset` (region config) | `importWizard` region/module | `[MANUAL/MCP]` |
| `Assets/_App/Tests/AssetBrowser/*` | EditMode tests | Create |

---

## Phase 1 — Add glTFast + confirm assembly/API

### Task 1.1: Add the package

- [ ] **Step 1 [MCP]:** `manage_packages add_package` with `com.unity.cloud.gltfast` (let UPM resolve
  the latest 6.x). Poll `status` until done.
- [ ] **Step 2 [MCP]:** `manage_packages get_package_info` for `com.unity.cloud.gltfast`. Record the
  resolved version and — importantly — the **runtime assembly name** (the asmdef glTFast ships, e.g.
  `glTFast`). The next task references it.
- [ ] **Step 3 [controller]:** `read_console` → expect no package/compile errors.

### Task 1.2: Reference glTFast from `_App.Runtime`

**Files:**
- Modify: `Assets/_App/Scripts/_App.Runtime.asmdef`

- [ ] **Step 1:** Add the glTFast runtime assembly (name from Task 1.1 Step 2) to the `"references"`
  array of `_App.Runtime.asmdef`. Example (adjust the exact name to what was resolved):
```json
  "references": [
    "VContainer",
    "Unity.XR.Interaction.Toolkit",
    "glTFast"
  ]
```
  (Keep all existing references; only append the glTFast one.)
- [ ] **Step 2:** Add a 6-line smoke file to confirm the namespace/API resolve, then delete it after
  the compile check. Create `Assets/_App/Scripts/AssetBrowser/_GltfApiSmoke.cs`:
```csharp
using GLTFast;

internal static class _GltfApiSmoke
{
    // Compile-only proof that GLTFast.GltfImport + LoadGltfBinary + InstantiateMainSceneAsync resolve.
    public static System.Type Probe() => typeof(GltfImport);
}
```
- [ ] **Step 3 [controller]:** `refresh_unity (mode=force, scope=all)` → `read_console`. Expect no
  `CS0246` for `GLTFast`. If `GltfImport` is in a different namespace in the resolved version, fix the
  `using` here and note it (later loader code uses the same namespace).
- [ ] **Step 4:** Delete `_GltfApiSmoke.cs`.
- [ ] **Step 5: Checkpoint (user commits)** — `chore(deps): add glTFast runtime package + asmdef reference`

---

## Phase 2 — Source file storage

### Task 2.1: PathProvider source paths (TDD)

**Files:**
- Create: `Assets/_App/Tests/AssetBrowser/PathProviderSourceTests.cs`
- Modify: `Assets/_App/Scripts/StorageCore/PathProvider.cs`

- [ ] **Step 1: Write the failing test.**
```csharp
using System.IO;
using NUnit.Framework;

public class PathProviderSourceTests
{
    [Test]
    public void SourcePath_CombinesAssetLibrarySourcesIdAndExt()
    {
        var p = new PathProvider("/root");
        var expected = Path.Combine("/root", "asset-library", "sources", "abc123.glb");
        Assert.AreEqual(expected, p.SourcePath("abc123", ".glb"));
    }

    [Test]
    public void SourcePath_NormalizesExtensionWithoutDot()
    {
        var p = new PathProvider("/root");
        Assert.AreEqual(p.SourcePath("id", ".png"), p.SourcePath("id", "png"));
    }
}
```
- [ ] **Step 2 [controller]:** `refresh_unity (force, all)` → `run_tests` (EditMode, filter
  `PathProviderSourceTests`). Expect RED (`SourcePath` undefined).
- [ ] **Step 3: Add the methods to `PathProvider`** (after `SavedLibraryPath`, `:38`):
```csharp
    public string SourcesDir =>
        Path.Combine(_root, "asset-library", "sources");

    public string SourcePath(string assetId, string ext)
    {
        var clean = string.IsNullOrEmpty(ext) ? "" : (ext[0] == '.' ? ext : "." + ext);
        return Path.Combine(SourcesDir, assetId + clean);
    }
```
- [ ] **Step 4 [controller]:** `run_tests` (filter `PathProviderSourceTests`). Expect GREEN.

### Task 2.2: AssetSourceStore — copy raw files (TDD)

**Files:**
- Create: `Assets/_App/Tests/AssetBrowser/AssetSourceStoreTests.cs`
- Create: `Assets/_App/Scripts/AssetBrowser/AssetSourceStore.cs`

- [ ] **Step 1: Write the failing test.**
```csharp
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

public class AssetSourceStoreTests
{
    [Test]
    public async Task Copy_PlacesFileUnderSourcesAndReturnsRelativeRef()
    {
        var root = Path.Combine(Path.GetTempPath(), "promeon_src_" + Path.GetRandomFileName());
        var srcFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".glb");
        File.WriteAllBytes(srcFile, new byte[] { 1, 2, 3 });

        var store = new AssetSourceStore(new PathProvider(root));
        var rel = await store.CopyAsync("asset9", srcFile, CancellationToken.None);

        var abs = Path.Combine(root, rel);
        Assert.IsTrue(File.Exists(abs), "copied file must exist under sources/");
        Assert.AreEqual(3, new FileInfo(abs).Length);
        StringAssert.Contains("asset9.glb", rel);

        Directory.Delete(root, true);
        File.Delete(srcFile);
    }
}
```
- [ ] **Step 2 [controller]:** `refresh_unity (force, all)` → `run_tests` (filter
  `AssetSourceStoreTests`). Expect RED.
- [ ] **Step 3: Write `AssetSourceStore`.** Returns a path RELATIVE to `persistentDataPath`
  (`SourceRef` is stored relative so it survives reinstalls/path changes).
```csharp
using System.IO;
using System.Threading;
using System.Threading.Tasks;

public class AssetSourceStore
{
    private readonly PathProvider _paths;

    public AssetSourceStore(PathProvider paths) => _paths = paths;

    /// Copies the picked file into asset-library/sources/{assetId}{ext} and returns the path
    /// relative to persistentDataPath (stored as the asset's SourceRef).
    public async Task<string> CopyAsync(string assetId, string sourceFilePath, CancellationToken ct)
    {
        var ext  = Path.GetExtension(sourceFilePath);
        var dest = _paths.SourcePath(assetId, ext);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);

        using (var src = File.OpenRead(sourceFilePath))
        using (var dst = File.Create(dest))
            await src.CopyToAsync(dst, 81920, ct);

        return Path.Combine("asset-library", "sources", assetId + ext);
    }

    public string AbsolutePath(string sourceRef) => Path.Combine(_paths.RootForSources, sourceRef);
}
```
- [ ] **Step 4: Add `RootForSources` to `PathProvider`** so `AssetSourceStore.AbsolutePath` can rebuild
  the absolute path from the relative `SourceRef`. In `PathProvider`, add:
```csharp
    public string RootForSources => _root;
```
- [ ] **Step 5 [controller]:** `run_tests` (filter `AssetSourceStoreTests`). Expect GREEN.
- [ ] **Step 6: Checkpoint (user commits)** — `feat(assets): source-file storage (PathProvider.SourcePath + AssetSourceStore)`

---

## Phase 3 — Runtime loaders

### Task 3.1: GltfModelLoader (glTFast wrapper)

**Files:**
- Create: `Assets/_App/Scripts/AssetBrowser/GltfModelLoader.cs`

> No unit test: glTFast loading is integration-level (verified in VR, Phase 8). Keep the class a thin,
> obvious wrapper so there is little to test in isolation.

- [ ] **Step 1: Write the loader.** (API per glTFast 6.x docs: `LoadGltfBinary(byte[], Uri)` then
  `InstantiateMainSceneAsync(Transform)`. `.gltf` text files also load via `LoadGltfBinary` only if
  self-contained; 1B targets self-contained `.glb` — `.gltf` with external buffers is out of scope and
  the import handler warns on it.)
```csharp
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;

public class GltfModelLoader
{
    /// Loads a .glb from an absolute file path and instantiates it under a fresh root GameObject
    /// placed at pose. Returns the root (or null on failure).
    public async Task<GameObject> LoadAsync(string absolutePath, Vector3 position, Quaternion rotation, CancellationToken ct)
    {
        var bytes = await File.ReadAllBytesAsync(absolutePath, ct);

        var gltf = new GltfImport();
        var ok   = await gltf.LoadGltfBinary(bytes, new Uri(absolutePath));
        if (!ok)
        {
            Debug.LogError($"GltfModelLoader: failed to parse '{absolutePath}'");
            return null;
        }

        var root = new GameObject("ImportedModel") { transform = { position = position, rotation = rotation } };
        var instantiated = await gltf.InstantiateMainSceneAsync(root.transform);
        if (!instantiated)
        {
            Debug.LogError($"GltfModelLoader: failed to instantiate '{absolutePath}'");
            UnityEngine.Object.Destroy(root);
            return null;
        }
        return root;
    }
}
```

### Task 3.2: ReferenceQuadFactory (image → quad)

**Files:**
- Create: `Assets/_App/Scripts/AssetBrowser/ReferenceQuadFactory.cs`

- [ ] **Step 1: Write the factory.** (Builds a unit quad whose material shows the image, sized to the
  image aspect ratio. Uses URP's `Universal Render Pipeline/Unlit` so it renders in this project's URP.)
```csharp
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class ReferenceQuadFactory
{
    public Task<GameObject> CreateAsync(string absolutePath, Vector3 position, Quaternion rotation, CancellationToken ct)
    {
        var bytes = File.ReadAllBytes(absolutePath);
        var tex   = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
        if (!tex.LoadImage(bytes))
        {
            Debug.LogError($"ReferenceQuadFactory: not a readable image '{absolutePath}'");
            Object.Destroy(tex);
            return Task.FromResult<GameObject>(null);
        }

        var go   = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name  = "ReferenceImage";
        go.transform.SetPositionAndRotation(position, rotation);

        var aspect = tex.height == 0 ? 1f : (float)tex.width / tex.height;
        go.transform.localScale = new Vector3(aspect, 1f, 1f);

        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { mainTexture = tex };
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;

        return Task.FromResult(go);
    }
}
```

- [ ] **Step 2 [controller]:** `refresh_unity (force, all)` → `read_console`. Expect no `CS` errors.
- [ ] **Step 3: Checkpoint (user commits)** — `feat(assets): runtime glTF loader + reference-image quad factory`

---

## Phase 4 — Spawners load imported geometry

### Task 4.1: Route Imported/Saved through the loaders

**Files:**
- Modify: `Assets/_App/Scripts/AssetBrowser/BuiltinAssetSpawnCore.cs`
- Modify: `Assets/_App/Scripts/AssetBrowser/ObjectSpawner.cs`
- Modify: `Assets/_App/Scripts/AssetBrowser/RigSpawner.cs`

- [ ] **Step 1: Rename `BuiltinAssetSpawnCore` → `ModelSpawnCore` and add the imported path.** Replace
  the file body so it handles both sources (Builtin prefab; Imported/Saved via `GltfModelLoader`):
```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Shared model-spawn logic for Object + Rig (both load a mesh and place it). Slice 2 will extend the
// Rig path with runtime proxy-rig building.
public static class ModelSpawnCore
{
    public static Task<GameObject> SpawnAsync(
        ILabAsset asset, Vector3 position, Quaternion rotation,
        AssetSourceStore store, GltfModelLoader loader, CancellationToken ct)
    {
        if (asset.Source == AssetSource.Builtin)
        {
            if (asset is not BuiltinLabAsset b)
                throw new NotSupportedException($"Builtin asset '{asset.Id}' is not a BuiltinLabAsset");
            return Task.FromResult(UnityEngine.Object.Instantiate(b.Prefab, position, rotation));
        }

        if (string.IsNullOrEmpty(asset.SourceRef))
            throw new NotSupportedException($"Imported/Saved asset '{asset.Id}' has no SourceRef");

        var abs = store.AbsolutePath(asset.SourceRef);
        return loader.LoadAsync(abs, position, rotation, ct);
    }
}
```
- [ ] **Step 2: Update `ObjectSpawner` to inject the deps and call `ModelSpawnCore`.**
```csharp
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class ObjectSpawner : IAssetSpawner
{
    private readonly AssetSourceStore _store;
    private readonly GltfModelLoader  _loader;

    public ObjectSpawner(AssetSourceStore store, GltfModelLoader loader)
    {
        _store  = store;
        _loader = loader;
    }

    public AssetType HandledType => AssetType.Object;

    public Task<GameObject> SpawnAsync(ILabAsset asset, Vector3 position, Quaternion rotation, CancellationToken ct)
        => ModelSpawnCore.SpawnAsync(asset, position, rotation, _store, _loader, ct);
}
```
- [ ] **Step 3: Update `RigSpawner` the same way** (static in 1B — identical body; Slice 2 adds the
  proxy-rig step after the mesh loads):
```csharp
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class RigSpawner : IAssetSpawner
{
    private readonly AssetSourceStore _store;
    private readonly GltfModelLoader  _loader;

    public RigSpawner(AssetSourceStore store, GltfModelLoader loader)
    {
        _store  = store;
        _loader = loader;
    }

    public AssetType HandledType => AssetType.Rig;

    public Task<GameObject> SpawnAsync(ILabAsset asset, Vector3 position, Quaternion rotation, CancellationToken ct)
        => ModelSpawnCore.SpawnAsync(asset, position, rotation, _store, _loader, ct);
}
```

### Task 4.2: ReferenceSpawner

**Files:**
- Create: `Assets/_App/Scripts/AssetBrowser/ReferenceSpawner.cs`

- [ ] **Step 1: Write it.**
```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class ReferenceSpawner : IAssetSpawner
{
    private readonly AssetSourceStore     _store;
    private readonly ReferenceQuadFactory _quads;

    public ReferenceSpawner(AssetSourceStore store, ReferenceQuadFactory quads)
    {
        _store = store;
        _quads = quads;
    }

    public AssetType HandledType => AssetType.Reference;

    public Task<GameObject> SpawnAsync(ILabAsset asset, Vector3 position, Quaternion rotation, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(asset.SourceRef))
            throw new NotSupportedException($"Reference asset '{asset.Id}' has no SourceRef");
        return _quads.CreateAsync(_store.AbsolutePath(asset.SourceRef), position, rotation, ct);
    }
}
```
- [ ] **Step 2 [controller]:** `refresh_unity (force, all)` → `read_console`. Expect errors ONLY about
  `ObjectSpawner`/`RigSpawner` constructors now needing args at their DI registration (fixed in Phase 7
  Task 7.1) — note them and proceed. No other `CS` errors.
- [ ] **Step 3: Checkpoint (user commits)** — `feat(assets): spawners load imported glTF/image geometry`

---

## Phase 5 — Import handlers (raw → typed record)

### Task 5.1: IAssetImportHandler contract

**Files:**
- Create: `Assets/_App/Scripts/AssetBrowser/IAssetImportHandler.cs`

- [ ] **Step 1: Write it.**
```csharp
using System.Threading;
using System.Threading.Tasks;

public interface IAssetImportHandler
{
    bool CanHandle(string fileExtension);           // ext is lower-case incl. dot, e.g. ".glb"
    AssetType SuggestedType { get; }                // wizard's default selection for this file kind
    Task<ImportedLabAsset> ImportAsync(string sourceFilePath, AssetType chosenType, string displayName, CancellationToken ct);
}
```

### Task 5.2: GltfImportHandler + ImageImportHandler (TDD on selection/extension)

**Files:**
- Create: `Assets/_App/Tests/AssetBrowser/ImportHandlerTests.cs`
- Create: `Assets/_App/Scripts/AssetBrowser/GltfImportHandler.cs`
- Create: `Assets/_App/Scripts/AssetBrowser/ImageImportHandler.cs`

- [ ] **Step 1: Write the failing test** (extension routing + that import copies the file and stamps
  the chosen type; uses a temp PathProvider/AssetSourceStore and a fake file):
```csharp
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

public class ImportHandlerTests
{
    [Test]
    public void Gltf_HandlesGlbAndGltf_NotPng()
    {
        var h = new GltfImportHandler(null);
        Assert.IsTrue(h.CanHandle(".glb"));
        Assert.IsTrue(h.CanHandle(".gltf"));
        Assert.IsFalse(h.CanHandle(".png"));
        Assert.AreEqual(AssetType.Object, h.SuggestedType);
    }

    [Test]
    public void Image_HandlesPngJpg_NotGlb()
    {
        var h = new ImageImportHandler(null);
        Assert.IsTrue(h.CanHandle(".png"));
        Assert.IsTrue(h.CanHandle(".jpg"));
        Assert.IsTrue(h.CanHandle(".jpeg"));
        Assert.IsFalse(h.CanHandle(".glb"));
        Assert.AreEqual(AssetType.Reference, h.SuggestedType);
    }

    [Test]
    public async Task Import_CopiesSource_AndStampsChosenType()
    {
        var root = Path.Combine(Path.GetTempPath(), "promeon_imp_" + Path.GetRandomFileName());
        var src  = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".glb");
        File.WriteAllBytes(src, new byte[] { 9, 9 });

        var store = new AssetSourceStore(new PathProvider(root));
        var h      = new GltfImportHandler(store);

        var record = await h.ImportAsync(src, AssetType.Rig, "Hero", CancellationToken.None);

        Assert.AreEqual(AssetType.Rig, record.Type);
        Assert.AreEqual("Hero", record.DisplayName);
        Assert.AreEqual(AssetSource.Imported, record.Source);
        Assert.IsFalse(string.IsNullOrEmpty(record.SourceRef));
        Assert.IsTrue(File.Exists(Path.Combine(root, record.SourceRef)));

        Directory.Delete(root, true);
        File.Delete(src);
    }
}
```
- [ ] **Step 2 [controller]:** `refresh_unity (force, all)` → `run_tests` (filter `ImportHandlerTests`).
  Expect RED.
- [ ] **Step 3: Write `GltfImportHandler`.**
```csharp
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class GltfImportHandler : IAssetImportHandler
{
    private readonly AssetSourceStore _store;

    public GltfImportHandler(AssetSourceStore store) => _store = store;

    public bool CanHandle(string ext) => ext == ".glb" || ext == ".gltf";

    // Default selection. The wizard lets the user switch to Rig for skinned characters; runtime
    // skeleton auto-detection is deferred (it requires a full load) — see plan notes.
    public AssetType SuggestedType => AssetType.Object;

    public async Task<ImportedLabAsset> ImportAsync(string sourceFilePath, AssetType chosenType, string displayName, CancellationToken ct)
    {
        if (Path.GetExtension(sourceFilePath).ToLowerInvariant() == ".gltf")
            Debug.LogWarning("GltfImportHandler: .gltf with external buffers/textures may not load at runtime; prefer self-contained .glb.");

        var id  = Guid.NewGuid().ToString("N")[..8];
        var rel = await _store.CopyAsync(id, sourceFilePath, ct);
        return new ImportedLabAsset(id, displayName, chosenType, rel);
    }
}
```
- [ ] **Step 4: Write `ImageImportHandler`.**
```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

public class ImageImportHandler : IAssetImportHandler
{
    private readonly AssetSourceStore _store;

    public ImageImportHandler(AssetSourceStore store) => _store = store;

    public bool CanHandle(string ext) => ext == ".png" || ext == ".jpg" || ext == ".jpeg";

    public AssetType SuggestedType => AssetType.Reference;

    public async Task<ImportedLabAsset> ImportAsync(string sourceFilePath, AssetType chosenType, string displayName, CancellationToken ct)
    {
        var id  = Guid.NewGuid().ToString("N")[..8];
        var rel = await _store.CopyAsync(id, sourceFilePath, ct);
        return new ImportedLabAsset(id, displayName, chosenType, rel);
    }
}
```
- [ ] **Step 5 [controller]:** `run_tests` (filter `ImportHandlerTests`). Expect GREEN.
- [ ] **Step 6: Checkpoint (user commits)** — `feat(assets): glTF + image import handlers (copy source, stamp type)`

---

## Phase 6 — ImportPipeline + wizard events

### Task 6.1: Wizard events

**Files:**
- Create: `Assets/_App/Scripts/AssetBrowser/Events/ImportRequestedEvent.cs`
- Create: `Assets/_App/Scripts/AssetBrowser/Events/ImportConfirmedEvent.cs`

- [ ] **Step 1: Write both event structs.**
```csharp
// pipeline → wizard: a file was picked; show the wizard with this suggestion.
public struct ImportRequestedEvent
{
    public string    FilePath;
    public string    SuggestedName;
    public AssetType SuggestedType;
}
```
```csharp
// wizard → pipeline: the user confirmed (Confirmed=false means cancelled).
public struct ImportConfirmedEvent
{
    public bool      Confirmed;
    public string    FilePath;
    public string    DisplayName;
    public AssetType ChosenType;
}
```

### Task 6.2: ImportPipeline

**Files:**
- Create: `Assets/_App/Scripts/AssetBrowser/ImportPipeline.cs`

- [ ] **Step 1: Write the pipeline.** It owns the FilePicked→wizard→handler→library flow (replacing
  `AssetBrowserPanel.HandleImportAsync`). Root-scoped `IStartable`.
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

public class ImportPipeline : IStartable, IDisposable
{
    private readonly EventBus                  _bus;
    private readonly ImportedAssetLibrary      _library;
    private readonly IReadOnlyList<IAssetImportHandler> _handlers;

    public ImportPipeline(EventBus bus, ImportedAssetLibrary library, IReadOnlyList<IAssetImportHandler> handlers)
    {
        _bus      = bus;
        _library  = library;
        _handlers = handlers;
    }

    public void Start()
    {
        _bus.Subscribe<FilePickedEvent>(OnFilePicked);
        _bus.Subscribe<ImportConfirmedEvent>(OnImportConfirmed);
    }

    public void Dispose()
    {
        _bus.Unsubscribe<FilePickedEvent>(OnFilePicked);
        _bus.Unsubscribe<ImportConfirmedEvent>(OnImportConfirmed);
    }

    private void OnFilePicked(FilePickedEvent e)
    {
        var handler = HandlerFor(e.Path);
        if (handler == null)
        {
            Debug.LogWarning($"ImportPipeline: no handler for '{Path.GetExtension(e.Path)}'");
            return;
        }
        _bus.Publish(new ImportRequestedEvent
        {
            FilePath      = e.Path,
            SuggestedName = Path.GetFileNameWithoutExtension(e.Path),
            SuggestedType = handler.SuggestedType,
        });
    }

    private void OnImportConfirmed(ImportConfirmedEvent e)
    {
        if (!e.Confirmed) return;
        _ = RunImportAsync(e);
    }

    private async Task RunImportAsync(ImportConfirmedEvent e)
    {
        try
        {
            var handler = HandlerFor(e.FilePath);
            if (handler == null) return;
            var record = await handler.ImportAsync(e.FilePath, e.ChosenType, e.DisplayName, CancellationToken.None);
            _library.Add(record);
            await _library.SaveAsync(CancellationToken.None);
            _bus.Publish(new AssetImportedEvent { AssetId = record.Id });
        }
        catch (Exception ex)
        {
            Debug.LogError($"ImportPipeline: import failed for '{e.FilePath}'. {ex}");
        }
    }

    private IAssetImportHandler HandlerFor(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return _handlers.FirstOrDefault(h => h.CanHandle(ext));
    }
}
```

### Task 6.3: AssetBrowserPanel — stop handling the import itself

**Files:**
- Modify: `Assets/_App/Scripts/SpatialUi/Panels/AssetBrowserPanel.cs`

- [ ] **Step 1: Remove the temporary import code.** Delete the `_bus.Subscribe<FilePickedEvent>`
  (`:60`) and `_bus.Unsubscribe<FilePickedEvent>` (`:71`) lines, the `OnFilePicked` method (`:183`),
  and the whole `HandleImportAsync` method (`:185-199`). The pipeline now owns FilePicked.
- [ ] **Step 2: Refresh the grid when an import completes.** In `Start()` add
  `_bus?.Subscribe<AssetImportedEvent>(OnAssetImported);`, in `OnDestroy()` add the matching
  `Unsubscribe`, and add:
```csharp
    private void OnAssetImported(AssetImportedEvent e)
    {
        if (_activeLibrary == _importedLibrary)
            RefreshGrid();
    }
```
- [ ] **Step 3:** Remove the now-unused `using System.IO;` / `using System.Threading;` /
  `using System;` from `AssetBrowserPanel` ONLY if no longer referenced (check `Guid`, `Path`,
  `CancellationToken` are gone). Leave any still used.
- [ ] **Step 4 [controller]:** `refresh_unity (force, all)` → `read_console`. Expect errors ONLY about
  DI registration for the new services (fixed in Phase 7). No errors inside `AssetBrowserPanel`.
- [ ] **Step 5: Checkpoint (user commits)** — `feat(assets): ImportPipeline owns FilePicked→wizard→library`

---

## Phase 7 — DI wiring + wizard UI

### Task 7.1: Register everything at Root

**Files:**
- Modify: `Assets/_App/Scripts/Bootstrap/RootLifetimeScope.cs`

- [ ] **Step 1: Replace the spawner block from Slice 1A** (the three lines added after
  `AssetRegistry`) with the full 1B registration:
```csharp
        // Runtime loaders + per-type spawners.
        builder.Register<AssetSourceStore>(Lifetime.Singleton);
        builder.Register<GltfModelLoader>(Lifetime.Singleton);
        builder.Register<ReferenceQuadFactory>(Lifetime.Singleton);
        builder.Register<ObjectSpawner>(Lifetime.Singleton).As<IAssetSpawner>();
        builder.Register<RigSpawner>(Lifetime.Singleton).As<IAssetSpawner>();
        builder.Register<ReferenceSpawner>(Lifetime.Singleton).As<IAssetSpawner>();
        builder.Register<AssetSpawnerRegistry>(Lifetime.Singleton);

        // Import handlers + pipeline.
        builder.Register<GltfImportHandler>(Lifetime.Singleton).As<IAssetImportHandler>();
        builder.Register<ImageImportHandler>(Lifetime.Singleton).As<IAssetImportHandler>();
        builder.RegisterEntryPoint<ImportPipeline>(Lifetime.Singleton).AsSelf();
```
- [ ] **Step 2: Inject the wizard surface (persistent panel, like AssetBrowserPanel/FileBrowserSurface).**
  Inside the `if (_navBarConfig != null)` build callback, next to the `FileBrowserSurface` inject loop
  (`:84-85`), add:
```csharp
                foreach (var iw in Object.FindObjectsByType<ImportWizardSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    c.Inject(iw);
```
- [ ] **Step 3 [controller]:** `refresh_unity (force, all)` → `read_console`. Expect no `CS` errors
  (assuming `ImportWizardSurface` exists — created in Task 7.2). VContainer resolves
  `IReadOnlyList<IAssetSpawner>` and `IReadOnlyList<IAssetImportHandler>` from the multiple
  `.As<…>()` registrations.

### Task 7.2: ImportWizardSurface (region surface + panel controller)

**Files:**
- Create: `Assets/_App/Scripts/SpatialUi/Behaviors/ImportWizardSurface.cs`

- [ ] **Step 1: Write the controller.** Mirrors `FileBrowserSurface` (an `IRegionSurface` that the
  router shows/hides), driven by the pipeline's events. UI refs are serialized; logic is here.
```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class ImportWizardSurface : MonoBehaviour, IRegionSurface
{
    [Header("Wizard UI")]
    [SerializeField] private TMP_Text       _fileNameLabel;
    [SerializeField] private TMP_InputField _nameInput;
    [SerializeField] private Toggle         _objectToggle;
    [SerializeField] private Toggle         _rigToggle;
    [SerializeField] private Toggle         _referenceToggle;
    [SerializeField] private Button         _importButton;
    [SerializeField] private Button         _cancelButton;

    private EventBus          _bus;
    private PanelRegionRouter _router;
    private string            _filePath;
    private bool              _open;

    public bool IsOpen => _open;

    [Inject]
    public void Construct(EventBus bus, PanelRegionRouter router)
    {
        _bus    = bus;
        _router = router;
    }

    private void Awake()
    {
        _importButton?.onClick.AddListener(OnImport);
        _cancelButton?.onClick.AddListener(OnCancel);
    }

    private void OnEnable()  => _bus?.Subscribe<ImportRequestedEvent>(OnImportRequested);
    private void OnDisable() => _bus?.Unsubscribe<ImportRequestedEvent>(OnImportRequested);

    private void OnImportRequested(ImportRequestedEvent e)
    {
        _filePath = e.FilePath;
        if (_fileNameLabel != null) _fileNameLabel.text = System.IO.Path.GetFileName(e.FilePath);
        if (_nameInput != null)     _nameInput.text     = e.SuggestedName;
        SetTypeSelection(e.SuggestedType);
        _router?.Open("importWizard");
    }

    public void Show() => _open = true;   // region router calls this when the region opens
    public void Hide() => _open = false;

    private void OnImport()
    {
        _bus?.Publish(new ImportConfirmedEvent
        {
            Confirmed   = true,
            FilePath    = _filePath,
            DisplayName = string.IsNullOrWhiteSpace(_nameInput?.text) ? System.IO.Path.GetFileNameWithoutExtension(_filePath) : _nameInput.text,
            ChosenType  = SelectedType(),
        });
        _router?.Close("importWizard");
    }

    private void OnCancel()
    {
        _bus?.Publish(new ImportConfirmedEvent { Confirmed = false, FilePath = _filePath });
        _router?.Close("importWizard");
    }

    private void SetTypeSelection(AssetType t)
    {
        if (_objectToggle    != null) _objectToggle.isOn    = t == AssetType.Object;
        if (_rigToggle       != null) _rigToggle.isOn       = t == AssetType.Rig;
        if (_referenceToggle != null) _referenceToggle.isOn = t == AssetType.Reference;
    }

    private AssetType SelectedType()
    {
        if (_rigToggle       != null && _rigToggle.isOn)       return AssetType.Rig;
        if (_referenceToggle != null && _referenceToggle.isOn) return AssetType.Reference;
        return AssetType.Object;
    }
}
```

> Wire the three type toggles into a single `ToggleGroup` in the prefab so they act as radio buttons.

- [ ] **Step 2 [controller]:** `refresh_unity (force, all)` → `read_console`. Expect no `CS` errors.

### Task 7.3: Wizard prefab + region config (editor)

- [ ] **Step 1 [MANUAL EDITOR / MCP]:** Build the wizard panel under the UserPanel `Center_top`
  region group (sibling of `SimpleFileBrowserCanvas`), as a region module named `importWizard`:
  a panel with a filename `TMP_Text`, a `TMP_InputField` for the name, three `Toggle`s
  (Object/Rig/Reference) in a shared `ToggleGroup`, and Import/Cancel `Button`s. Add the
  `ImportWizardSurface` component and a `RegionMember` with `ModuleId = "importWizard"`; wire all
  serialized refs.
- [ ] **Step 2 [MANUAL EDITOR / MCP]:** In the region config (`NavBarConfig`/`IRegionConfig`), add the
  `importWizard` module to the same region (`center_top`) as `fileBrowser`, with no nav button
  (it is opened programmatically by the pipeline, like the file browser is opened by the "+" button).
- [ ] **Step 3 [MANUAL / VR or Play]:** Smoke: "+" in asset browser → file browser → pick a `.glb` →
  the wizard appears showing the filename with Object preselected.
- [ ] **Step 4: Checkpoint (user commits)** — `feat(assets): import wizard (type selection) wired into the asset-browser flow`

---

## Phase 8 — End-to-end verification (user)

### Task 8.1: Import → spawn → persist (VR)

- [ ] **Step 1 [MANUAL / VR]:** Import a `.glb` as **Object**, place it → it spawns. Leave to MainMenu,
  reopen the scene → it persists (geometry reloads from `sources/`).
- [ ] **Step 2 [MANUAL / VR]:** Import a `.glb` of a skinned character as **Rig** → it spawns as a
  static skinned mesh (no proxy bones — that is Slice 2), and persists.
- [ ] **Step 3 [MANUAL / VR]:** Import a `.png` as **Reference** → a textured quad appears at the right
  aspect ratio, and persists.
- [ ] **Step 4 [MANUAL / VR]:** Cancel in the wizard → nothing is added; the asset browser reopens.
- [ ] **Step 5:** Confirm `Application.persistentDataPath/asset-library/sources/` holds the copied
  files and `imported.json` lists the records with correct `Type`/`SourceRef`.

---

## Self-Review

**Spec coverage (1B / import half):**
- glTFast runtime load (glb) → `GltfModelLoader` (Task 3.1), wired into Object/Rig spawners (Task 4.1).
- Image → reference quad → `ReferenceQuadFactory` + `ReferenceSpawner` (Tasks 3.2, 4.2).
- Copy raw file into `sources/` → `PathProvider.SourcePath` + `AssetSourceStore` (Phase 2).
- Wizard (dialog + type choice) launched from the asset-browser import button flow → `ImportPipeline`
  + `ImportWizardSurface` as an `importWizard` region opened after FilePicked (Phases 6-7), matching
  the existing file-browser region pattern.
- Typed Imported record + persistence → handlers write `ImportedLabAsset{Type,SourceRef}`; spawners
  reload from `SourceRef` (so scene reload restores them, via the Slice 1A registry path).
- Rig in 1B = static skinned mesh (Task 8.1 Step 2); runtime proxy-rig is Slice 2.

**Conscious simplification (flag for the user):** the wizard's default type is by **file extension**
(glb/gltf → Object, image → Reference) with manual override to Rig. Runtime **skeleton
auto-detection** (load the glb, check for `SkinnedMeshRenderer`, pre-select Rig) is deferred — it
needs a full throwaway load on device, and the actual rig build is Slice 2 anyway. If you want
auto-detect now, it becomes a task in `GltfImportHandler` (load+inspect+destroy) before the wizard.

**Placeholder scan:** none. Every code step shows complete code. `[MANUAL EDITOR / MCP]` steps are
prefab/region authoring (cannot be code), each with concrete acceptance.

**Type consistency:** `IAssetSpawner.SpawnAsync(ILabAsset, Vector3, Quaternion, CancellationToken)`
unchanged from 1A; `ObjectSpawner`/`RigSpawner`/`ReferenceSpawner` all match. `ModelSpawnCore.SpawnAsync`
signature is used identically by Object+Rig. `AssetSourceStore.CopyAsync`/`AbsolutePath` ↔ `SourceRef`
(relative path) are consistent across store, handlers, and spawners. `IAssetImportHandler`
(`CanHandle`/`SuggestedType`/`ImportAsync`) matches both handlers and the pipeline's usage.
`ImportRequestedEvent`/`ImportConfirmedEvent` fields match between `ImportPipeline` and
`ImportWizardSurface`. `ImportedLabAsset(id, displayName, type, sourceRef)` ctor matches the 1A record.

**Risks / open items:**
- glTFast exact API/assembly name is pinned by the Phase 1 spike; later loader code uses that
  namespace (`GLTFast`) — adjust if the resolved version differs.
- `.gltf` with external buffers/textures may not load at runtime (handler warns; `.glb` is the
  supported path).
- `Shader.Find("Universal Render Pipeline/Unlit")` must be included in the build — if the reference
  quad renders magenta on device, add that shader to Always-Included Shaders (Graphics settings).
- Imported `Icon` is null (no thumbnail) — cards show without an icon; preview generation is a later
  nicety, consistent with the spec.
- `ToggleGroup` wiring for the three type toggles is prefab-side (Task 7.3) — without it, multiple
  types could be selected; `SelectedType()` still resolves deterministically (Rig > Reference > Object).
