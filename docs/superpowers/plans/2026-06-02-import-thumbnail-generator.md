# Import Thumbnail Generator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Generate and display thumbnails for imported assets — models (Object/Rig `.glb`) rendered offscreen to a PNG at import time, images (Reference) reusing their source file — shown on the asset gallery cards.

**Architecture:** A new `ThumbnailRenderer` does an off-screen `Camera.Render()` of a loaded model into a `Texture2D`. `ImportPipeline` calls it at import, writes the PNG, and stamps a relative `_thumbnailRef` on the record (persisted in `imported-lib.json`). `AssetBrowserPanel` loads/caches a `Sprite` from that ref and hands it to each `LabAssetCard`.

**Tech Stack:** Unity 6000.3.7f1, C#, VContainer, glTFast (`GltfModelLoader`), `JsonUtility`, NUnit (EditMode). Compile/verify through the Unity MCP tools.

---

## CRITICAL PROJECT RULES (read before starting)

- **NEVER run any `git` command.** The user commits manually. Every "Checkpoint" means *stop and let the human commit* — do not stage/commit/branch. Overrides the writing-plans default.
- **Compile/test via Unity MCP**, not a shell build:
  - After editing scripts: `mcp__unityMCP__refresh_unity` (force recompile), then `mcp__unityMCP__read_console` filtered to `error` — **no `CS####`** before proceeding.
  - EditMode tests: `mcp__unityMCP__run_tests` (testMode EditMode), poll `mcp__unityMCP__get_test_job`.
  - If multiple Unity instances, pin one via `mcpforunity://instances` + `mcp__unityMCP__set_active_instance`.
  - **Allowed pre-existing failures** (NOT regressions): `PathProviderTests` ×4, `RingRotateStrategyTests` ×2. Any *other* failure is yours to fix.
- No namespaces on runtime gameplay code. `[SerializeField] private` on MonoBehaviour/SO fields. Plain `[Serializable]` JsonUtility data classes use `public`/`[SerializeField] private` fields. One public type per file (nested types fine).
- Some tasks (renderer, pipeline, panel) are **Unity-runtime integration** — they cannot be unit-tested in EditMode. Those tasks gate on **clean compile + full suite green**, and are verified in-headset in the final task. This is expected; do not invent fake tests for them.

---

## File Structure

| File | New/Modify | Responsibility |
|---|---|---|
| `Assets/_App/Scripts/AssetBrowser/ILabAsset.cs` | Modify | Add `string ThumbnailRef { get; }`. |
| `Assets/_App/Scripts/AssetBrowser/BuiltinLabAsset.cs` | Modify | `ThumbnailRef => null`. |
| `Assets/_App/Scripts/AssetBrowser/SavedLabAsset.cs` | Modify | `ThumbnailRef => null`. |
| `Assets/_App/Scripts/AssetBrowser/ImportedLabAsset.cs` | Modify | `_thumbnailRef` field + `ThumbnailRef` + `SetThumbnailRef`. |
| `Assets/_App/Scripts/StorageCore/PathProvider.cs` | Modify | `ThumbnailsDir`, `ThumbnailPath(id)`, `static ThumbnailRelativeRef(id)`. |
| `Assets/_App/Scripts/AssetBrowser/ThumbnailRenderer.cs` | **New** | Offscreen `Render(GameObject,int,Color)` + `static FrameDistance`. |
| `Assets/_App/Scripts/Bootstrap/RootLifetimeScope.cs` | Modify | Register `ThumbnailRenderer`. |
| `Assets/_App/Scripts/AssetBrowser/ImportPipeline.cs` | Modify | Generate thumbnail at import (inject loader/renderer/paths). |
| `Assets/_App/Scripts/SpatialUi/Elements/LabAssetCard.cs` | Modify | `Bind(asset, icon)`. |
| `Assets/_App/Scripts/SpatialUi/Panels/AssetBrowserPanel.cs` | Modify | Resolve + cache sprite, pass to card. |
| `Assets/_App/Tests/AssetBrowser/LabAssetThumbnailRefTests.cs` | **New** | `ThumbnailRef` defaults + round-trip. |
| `Assets/_App/Tests/AssetBrowser/PathProviderThumbnailTests.cs` | **New** | Thumbnail path shape. |
| `Assets/_App/Tests/AssetBrowser/ThumbnailRendererFrameTests.cs` | **New** | `FrameDistance` math. |

`_App.Runtime` already declares `[assembly: InternalsVisibleTo("_App.Tests")]`, so `internal static FrameDistance` is test-visible — no new file needed.

---

## Task 1: `ThumbnailRef` on records

**Files:**
- Modify: `Assets/_App/Scripts/AssetBrowser/ILabAsset.cs`, `BuiltinLabAsset.cs`, `SavedLabAsset.cs`, `ImportedLabAsset.cs`
- Test: `Assets/_App/Tests/AssetBrowser/LabAssetThumbnailRefTests.cs`

- [ ] **Step 1: Write the failing test**

Create `Assets/_App/Tests/AssetBrowser/LabAssetThumbnailRefTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

public class LabAssetThumbnailRefTests
{
    [Test]
    public void ImportedLabAsset_ThumbnailRef_DefaultsNull_ThenReflectsSetValue()
    {
        var a = new ImportedLabAsset("id1", "Name", AssetType.Object, "asset-libraries/sources/id1.glb");
        Assert.IsTrue(string.IsNullOrEmpty(a.ThumbnailRef), "fresh record has no thumbnail ref");

        a.SetThumbnailRef("asset-libraries/thumbnails/id1.png");
        Assert.AreEqual("asset-libraries/thumbnails/id1.png", a.ThumbnailRef);
    }

    [Test]
    public void ImportedLabAsset_ThumbnailRef_RoundTripsThroughJson()
    {
        var a = new ImportedLabAsset("id2", "Name2", AssetType.Reference, "asset-libraries/sources/id2.png");
        a.SetThumbnailRef("asset-libraries/sources/id2.png");

        var json = JsonUtility.ToJson(a);
        var back = JsonUtility.FromJson<ImportedLabAsset>(json);

        Assert.AreEqual("asset-libraries/sources/id2.png", back.ThumbnailRef);
    }

    [Test]
    public void SavedLabAsset_ThumbnailRef_IsNull()
    {
        var s = new SavedLabAsset("sid", "S", AssetType.Object, "aid");
        Assert.IsNull(s.ThumbnailRef);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

`refresh_unity`, `read_console`: expect `CS1061` — `ImportedLabAsset`/`SavedLabAsset` have no `ThumbnailRef`/`SetThumbnailRef`.

- [ ] **Step 3: Add `ThumbnailRef` to the interface**

In `Assets/_App/Scripts/AssetBrowser/ILabAsset.cs`, add the member after `Icon`:

```csharp
public interface ILabAsset
{
    string      Id          { get; }
    string      DisplayName { get; }
    AssetType   Type        { get; }
    AssetSource Source      { get; }   // which library this record lives in
    string      SourceRef   { get; }   // relative path under asset-libraries/sources; null for Builtin
    Sprite      Icon        { get; }
    string      ThumbnailRef { get; }  // relative path (under persistentDataPath) to a thumbnail image; null when none
    AssetEntityRecipe Recipe { get; }
}
```

- [ ] **Step 4: Implement on each record**

In `BuiltinLabAsset.cs`, add next to `Icon`:

```csharp
    public string      ThumbnailRef => null;   // Builtin uses its inspector Icon sprite
```

In `SavedLabAsset.cs`, add next to `Icon`:

```csharp
    public string      ThumbnailRef => null;   // Saved-library spawn flow is Slice 3 (not implemented)
```

In `ImportedLabAsset.cs`, add the serialized field (with the others), the property (next to `Icon`), and a setter (next to `SetRecipe`):

```csharp
    [SerializeField] private string            _thumbnailRef;
```
```csharp
    public string            ThumbnailRef => _thumbnailRef;
```
```csharp
    public void SetThumbnailRef(string thumbnailRef) => _thumbnailRef = thumbnailRef;
```

- [ ] **Step 5: Run to verify it passes**

`refresh_unity`, `read_console` (no `CS####`), `run_tests` EditMode filtered to `LabAssetThumbnailRefTests`. Expect 3 PASS.

- [ ] **Step 6: Checkpoint** — stop; the user commits.

---

## Task 2: `PathProvider` thumbnail paths

**Files:**
- Modify: `Assets/_App/Scripts/StorageCore/PathProvider.cs`
- Test: `Assets/_App/Tests/AssetBrowser/PathProviderThumbnailTests.cs`

- [ ] **Step 1: Write the failing test**

Create `Assets/_App/Tests/AssetBrowser/PathProviderThumbnailTests.cs` (expected values built with `Path.Combine` so the test is OS-agnostic — the pre-existing `PathProviderTests` failures come from hard-coded forward slashes; do not repeat that mistake):

```csharp
using System.IO;
using NUnit.Framework;

public class PathProviderThumbnailTests
{
    [Test]
    public void ThumbnailPath_IsUnderAssetLibrariesThumbnails()
    {
        var root = Path.Combine(Path.GetTempPath(), "promeon_pp");
        var pp = new PathProvider(root);

        var expected = Path.Combine(root, "asset-libraries", "thumbnails", "abc.png");
        Assert.AreEqual(expected, pp.ThumbnailPath("abc"));
        Assert.AreEqual(Path.Combine(root, "asset-libraries", "thumbnails"), pp.ThumbnailsDir);
    }

    [Test]
    public void ThumbnailRelativeRef_IsRootIndependentRelativePath()
    {
        var expected = Path.Combine("asset-libraries", "thumbnails", "abc.png");
        Assert.AreEqual(expected, PathProvider.ThumbnailRelativeRef("abc"));
    }
}
```

- [ ] **Step 2: Run to verify it fails**

`refresh_unity`, `read_console`: expect `CS1061`/`CS0117` — `PathProvider` has no `ThumbnailPath`/`ThumbnailsDir`/`ThumbnailRelativeRef`.

- [ ] **Step 3: Add the methods**

In `Assets/_App/Scripts/StorageCore/PathProvider.cs`, add after `SourcePath(...)` (before `RootForSources`):

```csharp
    public string ThumbnailsDir =>
        System.IO.Path.Combine(_root, "asset-libraries", "thumbnails");

    public string ThumbnailPath(string assetId) =>
        System.IO.Path.Combine(ThumbnailsDir, assetId + ".png");

    /// Root-independent relative ref stored on the record (mirrors how SourceRef is stored).
    public static string ThumbnailRelativeRef(string assetId) =>
        System.IO.Path.Combine("asset-libraries", "thumbnails", assetId + ".png");
```

- [ ] **Step 4: Run to verify it passes**

`refresh_unity`, `read_console` (no `CS####`), `run_tests` EditMode filtered to `PathProviderThumbnailTests`. Expect 2 PASS.

- [ ] **Step 5: Checkpoint** — stop; the user commits.

---

## Task 3: `ThumbnailRenderer`

**Files:**
- Create: `Assets/_App/Scripts/AssetBrowser/ThumbnailRenderer.cs`
- Modify: `Assets/_App/Scripts/Bootstrap/RootLifetimeScope.cs`
- Test: `Assets/_App/Tests/AssetBrowser/ThumbnailRendererFrameTests.cs`

The `Render` method is a Unity-runtime render (verified in-headset). Only the pure `FrameDistance` math is unit-tested here.

- [ ] **Step 1: Write the failing test**

Create `Assets/_App/Tests/AssetBrowser/ThumbnailRendererFrameTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

public class ThumbnailRendererFrameTests
{
    [Test]
    public void FrameDistance_UnitCubeAt60Fov_FitsBoundingSphere()
    {
        // unit cube: extents (0.5,0.5,0.5) -> bounding-sphere radius ~0.86603
        // d = radius / sin(fov/2) = 0.86603 / sin(30deg) = 0.86603 / 0.5 = 1.73205
        var bounds = new Bounds(Vector3.zero, Vector3.one);
        var d = ThumbnailRenderer.FrameDistance(bounds, 60f);
        Assert.AreEqual(1.73205f, d, 0.001f);
    }

    [Test]
    public void FrameDistance_LargerBounds_GivesLargerDistance()
    {
        var small = ThumbnailRenderer.FrameDistance(new Bounds(Vector3.zero, Vector3.one), 60f);
        var big   = ThumbnailRenderer.FrameDistance(new Bounds(Vector3.zero, Vector3.one * 4f), 60f);
        Assert.Greater(big, small);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

`refresh_unity`, `read_console`: expect `CS0246`/`CS0117` — `ThumbnailRenderer.FrameDistance` does not exist.

- [ ] **Step 3: Create `ThumbnailRenderer`**

Create `Assets/_App/Scripts/AssetBrowser/ThumbnailRenderer.cs`:

```csharp
using UnityEngine;

/// <summary>
/// Renders a loaded model GameObject to a square thumbnail Texture2D via an off-screen camera.
/// Knows nothing about glTF — the caller hands it an already-instantiated, already-parked model.
/// The render is off-screen (Camera.Render to a targetTexture), so the live scene/display are untouched.
/// </summary>
public class ThumbnailRenderer
{
    private const float FovDeg = 30f;
    private static readonly Vector3 ViewDir = new Vector3(1f, 0.7f, -1f).normalized;

    /// <summary>Camera distance that fits the bounding sphere of <paramref name="bounds"/> at the given vertical FOV.</summary>
    internal static float FrameDistance(Bounds bounds, float verticalFovDeg)
    {
        float radius  = Mathf.Max(0.0001f, bounds.extents.magnitude);
        float halfFov = verticalFovDeg * 0.5f * Mathf.Deg2Rad;
        return radius / Mathf.Sin(halfFov);
    }

    /// <summary>Renders <paramref name="model"/> to a size×size RGB24 Texture2D on a solid background.</summary>
    public Texture2D Render(GameObject model, int size, Color background)
    {
        var bounds = ComputeBounds(model);

        var camGo = new GameObject("ThumbnailCamera");
        var cam   = camGo.AddComponent<Camera>();
        cam.enabled         = false;                 // never draws to the screen; we call Render() manually
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = background;
        cam.fieldOfView     = FovDeg * 2f;           // FovDeg is the half-angle used by FrameDistance
        cam.cullingMask     = ~0;

        var dist = FrameDistance(bounds, cam.fieldOfView);
        cam.transform.position = bounds.center + ViewDir * dist;
        cam.transform.LookAt(bounds.center);
        cam.nearClipPlane = Mathf.Max(0.01f, dist - bounds.extents.magnitude * 2f);
        cam.farClipPlane  = dist + bounds.extents.magnitude * 4f;

        var lightGo = new GameObject("ThumbnailLight");
        var light   = lightGo.AddComponent<Light>();
        light.type  = LightType.Directional;
        light.transform.rotation = Quaternion.LookRotation(bounds.center - cam.transform.position);
        light.intensity = 1.1f;

        var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
        var prevActive = RenderTexture.active;
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture.active = rt;
        var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        tex.Apply();

        RenderTexture.active = prevActive;
        cam.targetTexture    = null;
        rt.Release();
        Object.Destroy(rt);
        Object.Destroy(camGo);
        Object.Destroy(lightGo);
        return tex;
    }

    private static Bounds ComputeBounds(GameObject model)
    {
        var renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(model.transform.position, Vector3.one);

        var b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);
        return b;
    }
}
```

- [ ] **Step 4: Register it in DI**

In `Assets/_App/Scripts/Bootstrap/RootLifetimeScope.cs`, immediately after the `GltfModelLoader` registration (line ~50 `builder.Register<GltfModelLoader>(Lifetime.Singleton);`), add:

```csharp
        builder.Register<ThumbnailRenderer>(Lifetime.Singleton);
```

- [ ] **Step 5: Run to verify it passes**

`refresh_unity`, `read_console` (no `CS####`), `run_tests` EditMode filtered to `ThumbnailRendererFrameTests`. Expect 2 PASS.

- [ ] **Step 6: Checkpoint** — stop; the user commits.

---

## Task 4: Generate the thumbnail at import (integration)

No unit test — this is glTF load + GPU render + file IO. It gates on clean compile + full suite green, and is verified in-headset (Task 6).

**Files:**
- Modify: `Assets/_App/Scripts/AssetBrowser/ImportPipeline.cs`

- [ ] **Step 1: Extend the constructor and fields**

In `ImportPipeline.cs`, add the three dependencies. Replace the field block + constructor with:

```csharp
    private readonly EventBus                  _bus;
    private readonly ImportedAssetLibrary      _library;
    private readonly IReadOnlyList<IAssetImportHandler> _handlers;
    private readonly AssetEntityBuilderRegistry _builders;
    private readonly AssetSourceStore           _store;
    private readonly GltfModelLoader            _loader;
    private readonly ThumbnailRenderer          _renderer;
    private readonly PathProvider               _paths;

    public ImportPipeline(EventBus bus, ImportedAssetLibrary library, IReadOnlyList<IAssetImportHandler> handlers,
                          AssetEntityBuilderRegistry builders, AssetSourceStore store,
                          GltfModelLoader loader, ThumbnailRenderer renderer, PathProvider paths)
    {
        _bus      = bus;
        _library  = library;
        _handlers = handlers;
        _builders = builders;
        _store    = store;
        _loader   = loader;
        _renderer = renderer;
        _paths    = paths;
    }
```

(`RegisterEntryPoint<ImportPipeline>` resolves these automatically — `GltfModelLoader`, `ThumbnailRenderer` (Task 3), and `PathProvider` are all root-registered, so no scope change beyond Task 3's line.)

- [ ] **Step 2: Generate before adding to the library**

In `RunImportAsync`, insert the thumbnail call between `record.SetRecipe(recipe);` and `_library.Add(record);`:

```csharp
            record.SetRecipe(recipe);

            await GenerateThumbnailAsync(record, CancellationToken.None);

            _library.Add(record);
```

- [ ] **Step 3: Add the generation method**

Add this method to `ImportPipeline` (e.g. after `RunImportAsync`). The required usings are already present at the top of the file (`using System;`, `using System.IO;`, `using System.Threading;`, `using System.Threading.Tasks;`, `using UnityEngine;`) — no using changes needed:

```csharp
    private async Task GenerateThumbnailAsync(ImportedLabAsset record, CancellationToken ct)
    {
        try
        {
            if (record.Type == AssetType.Reference)
            {
                // The image file itself is the thumbnail — no render.
                record.SetThumbnailRef(record.SourceRef);
                return;
            }

            // Object / Rig: render the .glb off-screen, parked far below the scene.
            var abs   = _store.AbsolutePath(record.SourceRef);
            var model = await _loader.LoadAsync(abs, new Vector3(0f, -10000f, 0f), Quaternion.identity, ct);
            if (model == null) return;

            try
            {
                var tex = _renderer.Render(model, 256, new Color(0.22f, 0.22f, 0.24f, 1f));
                var png = tex.EncodeToPNG();
                UnityEngine.Object.Destroy(tex);

                var path = _paths.ThumbnailPath(record.Id);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllBytesAsync(path, png, ct);

                record.SetThumbnailRef(PathProvider.ThumbnailRelativeRef(record.Id));
            }
            finally
            {
                UnityEngine.Object.Destroy(model);
            }
        }
        catch (Exception ex)
        {
            // A missing thumbnail must never abort the import.
            Debug.LogError($"ImportPipeline: thumbnail generation failed for '{record.Id}'. {ex}");
        }
    }
```

- [ ] **Step 4: Verify compile + full suite**

`refresh_unity`, `read_console` (no `CS####`). Run the **full** EditMode suite (`run_tests` EditMode, poll `get_test_job`). Expect green except the allowed pre-existing failures (`PathProviderTests` ×4, `RingRotateStrategyTests` ×2).

- [ ] **Step 5: Checkpoint** — stop; the user commits.

---

## Task 5: Display the thumbnail (integration)

No unit test — sprite loading (`Texture2D.LoadImage`) and panel grid build are Unity-runtime. Gates on clean compile + full suite green; verified in-headset.

**Files:**
- Modify: `Assets/_App/Scripts/SpatialUi/Elements/LabAssetCard.cs`
- Modify: `Assets/_App/Scripts/SpatialUi/Panels/AssetBrowserPanel.cs`

- [ ] **Step 1: Change `LabAssetCard.Bind` to take the resolved sprite**

In `LabAssetCard.cs`, replace the `Bind` method:

```csharp
    public void Bind(ILabAsset asset, Sprite icon)
    {
        _asset         = asset;
        _nameText.text = asset.DisplayName;

        if (icon != null)
            _iconImage.sprite = icon;

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => Selected?.Invoke(this));

        SetSelected(false);
    }
```

(The old body read `asset.Icon` directly; the panel now resolves the sprite — builtin sprite *or* loaded thumbnail — and passes it in.)

- [ ] **Step 2: Resolve + cache the sprite in the panel**

In `AssetBrowserPanel.cs`:

(a) Add a cache field next to the other private fields (near `_selectedCard`):

```csharp
    private readonly System.Collections.Generic.Dictionary<string, Sprite> _thumbCache = new();
```

(b) Change the grid-build call (the line `card.Bind(asset);`, ~line 105) to:

```csharp
            card.Bind(asset, ResolveIcon(asset));
```

(c) Add the resolver method to the class:

```csharp
    // Builtin assets carry an inspector sprite; imported assets carry a relative ThumbnailRef
    // (a rendered model PNG, or the source image for References). Loaded sprites are cached by ref.
    private Sprite ResolveIcon(ILabAsset asset)
    {
        if (asset.Icon != null) return asset.Icon;

        var refPath = asset.ThumbnailRef;
        if (string.IsNullOrEmpty(refPath)) return null;

        if (_thumbCache.TryGetValue(refPath, out var cached)) return cached;

        Sprite sprite = null;
        try
        {
            var abs = _sources.AbsolutePath(refPath);
            if (System.IO.File.Exists(abs))
            {
                var bytes = System.IO.File.ReadAllBytes(abs);
                var tex   = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(bytes))
                    sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"AssetBrowserPanel: failed to load thumbnail '{refPath}'. {ex.Message}");
        }

        _thumbCache[refPath] = sprite;   // cache null too — don't retry a broken ref every rebuild
        return sprite;
    }
```

> `_sources` is the existing `AssetSourceStore` field on `AssetBrowserPanel` (declared at line 31,
> assigned in `Construct`). `AbsolutePath(relRef)` resolves any path relative to `persistentDataPath`,
> so it handles both `asset-libraries/thumbnails/*.png` and `asset-libraries/sources/*` refs.

- [ ] **Step 3: Verify compile + full suite**

`refresh_unity`, `read_console` (no `CS####`). Run the **full** EditMode suite. Expect green except the allowed pre-existing failures. (If any test called `LabAssetCard.Bind(asset)` with one arg, it will now fail to compile — update that call to pass a sprite, e.g. `null`, and note it.)

- [ ] **Step 4: Checkpoint** — stop; the user commits.

---

## Task 6: In-headset verification (user-performed)

Hand back to the user with this checklist:

- [ ] Import a `.glb` model → its gallery card shows a rendered thumbnail (3/4 view, neutral grey background); the live scene is undisturbed during import.
- [ ] Import a `.png`/`.jpg` → its card shows the image itself.
- [ ] Restart the app → both thumbnails still show (loaded from `asset-libraries/thumbnails/` and `sources/`, no re-render).
- [ ] Import a deliberately broken/empty model → the import still completes, the card is iconless, and a `[ImportPipeline] thumbnail generation failed` error is logged (import not aborted).
- [ ] Assets imported **before** this feature show iconless (expected — no backfill); re-importing produces a thumbnail.

---

## Notes carried from the spec

- **Out of scope:** Builtin/Saved thumbnails, backfill of pre-existing imports, transparent/multi-angle previews, editor-time baking.
- **Schema:** `imported-lib.json` stays `schemaVersion 2` — `_thumbnailRef` is additive and JsonUtility-tolerant. If, while implementing, you find an EditMode test that asserts the imported-lib schema version equals 2, that assertion still holds (no bump); do not change it.
- **Git:** not touched by the agent; the user commits manually.
