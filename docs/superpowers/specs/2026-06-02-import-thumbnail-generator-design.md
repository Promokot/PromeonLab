# Import Thumbnail Generator — Design

**Date:** 2026-06-02
**Status:** Approved-for-planning
**Subsystem:** `AssetBrowser` (+ small `StorageCore`/`SpatialUi` touches)
**Prior art:** `Assets/_App/Documentation/architecture_context.md` specced a `ThumbnailService` + `.thumbnails/` cache long ago; never implemented. `ILabAsset.Icon` is the existing display hook.

---

## Goal

Give imported assets a real thumbnail in the VR asset gallery. Imported **models** (Object/Rig `.glb`)
are rendered offscreen to a PNG at import time; imported **images** (Reference) reuse their own source
file as the thumbnail. Builtin assets already carry an inspector-assigned sprite and are unchanged.

Today `ImportedLabAsset.Icon` returns `null`, so imported cards render iconless. This feature fills that
gap end-to-end: generate → persist → display.

---

## Decisions (locked during brainstorming)

| Decision | Choice | Rationale |
|---|---|---|
| Scope | Models rendered; images reuse source | Rendering an image is pointless — the file *is* the thumbnail. |
| When generated | At import, one-shot | The import already loads the model; browsing then just loads a ready PNG. |
| Persistence | PNG on disk + ref on the record | Survives restart; no per-session re-render. |
| Background | Neutral solid grey (no alpha) | Simpler RT (RGB24), no alpha plumbing. |
| Offscreen park position | Far **down** (large negative Y) | Below the scene; temporary, on a dedicated layer, so FallGuard (player-rig only) never touches it. |
| New classes | Only `ThumbnailRenderer` | Everything else is additive edits; display folds into the existing panel. |

---

## Architecture

```
Import (ImportPipeline.RunImportAsync, after recipe build)
  ├─ Object/Rig:  GltfModelLoader.LoadAsync(glb, parked far -Y)
  │                 → ThumbnailRenderer.Render(model, 256, neutralGrey) → Texture2D
  │                 → EncodeToPNG → write asset-libraries/thumbnails/{assetId}.png
  │                 → record.SetThumbnailRef("asset-libraries/thumbnails/{assetId}.png")
  │                 → destroy the offscreen model
  └─ Reference:   record.SetThumbnailRef(record.SourceRef)   // the image file itself

Persist (ImportedAssetLibrary): _thumbnailRef rides inside imported-lib.json per entry.

Display (AssetBrowserPanel.RebuildGrid)
  for each asset:
    sprite = asset.Icon != null            ? asset.Icon                       // Builtin
           : !string.IsNullOrEmpty(ThumbnailRef) ? LoadCached(ThumbnailRef)   // Imported (model PNG or image source)
           : null
    card.Bind(asset, sprite)
```

The thumbnail ref is stored **relative to `persistentDataPath`** (exactly like `SourceRef`), so
`AssetSourceStore.AbsolutePath(ref)` resolves both model PNGs (`asset-libraries/thumbnails/...`) and
image sources (`asset-libraries/sources/...`) with no new path plumbing on the display side.

---

## Components

### `ThumbnailRenderer` (new — the only new class)

Root-scoped singleton, injected into `ImportPipeline`. Pure offscreen capture; knows nothing about
glTF (the caller hands it an already-instantiated `GameObject`).

```
public Texture2D Render(GameObject model, int size, Color background)
internal static float FrameDistance(Bounds bounds, float verticalFovDeg)   // testable framing math
```

`Render`:
1. Build combined world `Bounds` from `model.GetComponentsInChildren<Renderer>()`.
2. Create a temporary `Camera` (`clearFlags = SolidColor`, `backgroundColor = background`, `enabled = false`
   so it never draws to the screen) and a temporary directional `Light` (fill) aimed at the model.
3. The caller has already parked the model far **down** (large negative Y), clear of the live scene.
   Place the camera on a 3/4 view direction (≈ normalized `(1, 0.7, -1)`) at `FrameDistance(bounds, fov)`
   from `bounds.center`, looking at it; set `nearClipPlane`/`farClipPlane` tight around the bounds so only
   the parked model is in frustum.
4. Assign a `RenderTexture(size, size, 24)` (RGB24) to `camera.targetTexture` and call `camera.Render()`
   — an **off-screen** render to the RT; the live scene and the on-screen display are untouched.
5. `ReadPixels` from the active RT into a `Texture2D(size, size, RGB24)`; `Apply()`.
6. Destroy the temp camera/light, release the RT (restore `RenderTexture.active`). Return the `Texture2D`.

No dedicated layer or TagManager edit is needed: `camera.Render()` to a `targetTexture` is inherently
off-screen, the parked model sits far from any scene geometry, and the temp fill light guarantees the
model is lit regardless of scene lighting.

`size` = 256 (square). The renderer never destroys the model — ownership stays with the caller.

### `PathProvider` (additive methods)

```
public string ThumbnailsDir            => Path.Combine(_root, "asset-libraries", "thumbnails");
public string ThumbnailPath(string id) => Path.Combine(ThumbnailsDir, id + ".png");
```
The persisted ref is the relative form `asset-libraries/thumbnails/{id}.png`.

### `ILabAsset` / records (additive)

- `ILabAsset` gains `string ThumbnailRef { get; }`.
- `BuiltinLabAsset.ThumbnailRef => null` (uses its `Icon` sprite).
- `SavedLabAsset.ThumbnailRef => null` (Slice 3, out of scope).
- `ImportedLabAsset`: new `[SerializeField] private string _thumbnailRef;`, `ThumbnailRef => _thumbnailRef`,
  and `public void SetThumbnailRef(string r) => _thumbnailRef = r;`.

`ImportedAssetLibrary.LibraryJson.schemaVersion` stays **2** — the field is additive and JsonUtility
tolerates its absence in old records (they render iconless until re-import; no backfill — YAGNI). If a
test asserts the imported-lib schema version, bump to 3 with an inline note instead.

### `ImportPipeline` (generation)

Inject `GltfModelLoader`, `ThumbnailRenderer`, `PathProvider` (alongside the existing deps). After the
recipe is built and assigned, call a private `GenerateThumbnailAsync(record, ct)` **before** `_library.Add`:

- `Object`/`Rig`: load the glb offscreen (parked far -Y), render, `EncodeToPNG`, write to
  `PathProvider.ThumbnailPath(record.Id)` (create dir), set the relative ref, destroy the model.
- `Reference`: set ref = `record.SourceRef` (no render).
- Any failure → `Debug.LogError` and leave the ref empty; **the import still succeeds** (a missing
  thumbnail must never abort an import).

### `AssetBrowserPanel` + `LabAssetCard` (display)

- `AssetBrowserPanel` keeps a `Dictionary<string, Sprite> _thumbCache` keyed by `ThumbnailRef`. A helper
  `ResolveIcon(ILabAsset)` returns `asset.Icon` for builtin, else loads/caches a `Sprite` from
  `AssetSourceStore.AbsolutePath(asset.ThumbnailRef)` via `Texture2D.LoadImage`, else `null`.
- The grid loop passes the resolved sprite: `card.Bind(asset, ResolveIcon(asset))`.
- `LabAssetCard.Bind(ILabAsset asset, Sprite icon)` — new `icon` param; assigns `_iconImage.sprite` when
  non-null (replaces the current `asset.Icon` read inside `Bind`).

---

## Error handling

- Render/load/write failures during import are caught, logged, and leave `ThumbnailRef` empty. Import
  completes normally; the card simply shows no icon.
- Display-side load failure (corrupt/missing PNG) → cache `null` for that ref and render iconless; never
  throws into the grid build.

---

## Testing

**EditMode unit tests:**
- `ThumbnailRenderer.FrameDistance(bounds, fov)` — pure framing math (e.g. a unit-cube bounds at a known
  FOV yields the expected distance; larger bounds → larger distance).
- `PathProvider.ThumbnailPath` / `ThumbnailsDir` — path shape under `asset-libraries/thumbnails/`.
- `ImportedLabAsset` JsonUtility round-trip carrying `_thumbnailRef`.
- `ThumbnailRef` values: `BuiltinLabAsset` → null; `ImportedLabAsset` → the set value.

**In-headset / manual (Unity-runtime, not unit-testable):**
- Import a `.glb` → card shows a rendered thumbnail; the live scene is undisturbed during import.
- Import a `.png/.jpg` → card shows the image itself.
- Restart the app → thumbnails persist (loaded from disk, no re-render).
- Import a deliberately broken model → import still succeeds, card iconless, error logged.

Allowed pre-existing failures (do not block): `PathProviderTests` ×4, `RingRotateStrategyTests` ×2.

---

## Data / storage layout (addition)

```
Application.persistentDataPath/asset-libraries/
├── imported-lib.json        (now also stores per-entry _thumbnailRef; schemaVersion 2, additive)
├── sources/{assetId}.{ext}  (unchanged — raw imports; image thumbnails point here)
└── thumbnails/{assetId}.png (NEW — rendered model thumbnails, RGB24, 256²)
```

---

## Out of scope

- Thumbnails for Builtin (already have inspector sprites) and Saved (Slice 3 not implemented).
- Backfilling thumbnails for assets imported before this feature (re-import to get one).
- Transparent/alpha thumbnails, multi-angle or turntable previews, regeneration UI.
- Editor-time thumbnail baking.
