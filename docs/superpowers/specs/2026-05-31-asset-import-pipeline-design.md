# Asset Import Pipeline — Architecture Design (high-level)

> **Scope of this doc:** the *whole* asset pipeline at the architecture level — conceptual
> layers, type taxonomy, storage, and component/data-flow — **without implementation detail**.
> Implementation is split into slices (see Decomposition); each slice gets its own spec → plan →
> implementation cycle. **Slice 1 is the implementation target driven from this design.**
>
> **Git note:** user commits manually — this doc is written, not committed.

## Problem

Today "import" means: pick a file → store a JSON record with a **hardcoded** `AssetType.Model` →
nothing else. Concretely (by code):

- `FileBrowserSurface` publishes `FilePickedEvent{Path}`; `AssetBrowserPanel.HandleImportAsync`
  (`:185`) builds an `ImportedLabAsset` with `type: AssetType.Model`, adds it to
  `ImportedAssetLibrary`, saves JSON. **No dialog, no type choice, no processing.**
- There is **no runtime model loader** in the project (no glTF/FBX/Assimp). The unused
  `AssetImporter` only matches a filename against the bundled `DemoAssetCatalog` and instantiates a
  pre-made prefab.
- The raw file is **not copied** into storage — only an absolute device path is kept.
- `ImportedLabAsset.SpawnAsync` / `SavedLabAsset.SpawnAsync` `throw NotImplementedException`. On
  scene load, `SceneGraph.OnSceneOpenedAsync` (`:146-152`) catches it and **silently drops the
  node** ("before Spec B"). → imported objects **disappear after a session**.

**Goal:** a proper import flow — file → wizard (choose type) → per-type processing → full save →
survives restart. Runtime source format: **glTF/GLB** (+ images). FBX is **out of scope** (no
viable runtime FBX path on standalone Quest without a paid/native dependency; see Rejected Options).

## Rejected Options (runtime FBX)

- **Unity FBX Exporter (`com.unity.formats.fbx`)** — export-only AND editor-only. Cannot read FBX,
  does not ship to device. Nothing to "rewrite for import".
- **`com.autodesk.fbx`** — could import in principle, but its native libs ship only for the desktop
  editor; no Android arm64 build. Building the Autodesk FBX SDK for arm64 ourselves is out of scope
  (native toolchain + licensing).
- **TriLib 2** — the only robust runtime-FBX path; paid (~$70). Deferred unless FBX becomes a hard
  requirement.
- **Decision:** runtime import = **glTF/GLB via glTFast** (`com.unity.cloud.gltfast`, free,
  official, runs on Quest) + images. FBX excluded.

## Conceptual Model — three layers

The core fix is a clear boundary between *raw data* and *app asset*, which does not exist today.

1. **Source (raw).** The imported file's bytes, copied into storage. Addressed by a relative path.
   Immutable after import.
2. **Asset (typed).** A library record: `{ Id, DisplayName, Type, Source, SourceRef, Icon, Meta }`.
   The "unit the app works with." References a Source (for Imported/Saved). **Source-agnostic:** an
   Asset lives in **one of three libraries** — `Builtin` (shipped in the build), `Imported`
   (produced by the wizard), `Saved` (manually saved out of a scene by the user). `Icon` is retained
   on every record.
3. **SceneNode (instance).** A placement of an Asset in a scene: `AssetRef{Source, AssetId}` +
   transform + parent. Already exists in `scene.json` (`NodeData` / `CaptureSnapshot`).

The three libraries already exist in code (`AssetSource` enum, `AssetRegistry.Find` resolves by
source). This design generalizes the Asset layer and removes spawn behavior from it (below).

### Library semantics (do not conflate)

- **Builtin** — assets shipped in the build (fed by `DemoAssetCatalog`). Geometry = a prefab ref.
- **Imported** — produced by the import wizard from a device file. Geometry = a Source file.
- **Saved** — produced by the user **manually saving an object/assembly out of a scene** into a
  reusable asset. This is NOT "reuse of an import result"; it is a distinct, scene-origin flow
  (Slice 3).

## Type Taxonomy

`AssetType` becomes `{ Object, Rig, Reference }`. The wizard produces exactly these three.

| Type | Meaning | Notes |
|---|---|---|
| **Object** (was `Model`) | static glTF mesh — the "normal object" | base type; Selectable/movable |
| **Rig** | skinned mesh + skeleton | riggable character; runtime rig build is Slice 2 |
| **Reference** (was `Texture`) | image (PNG/JPG) → placeable reference plane | no dependency |

Dropped / relocated:
- **Material** — a sub-resource of a model (arrives inside glTF), not a separately imported asset.
- **Video / Audio** — removed until needed (YAGNI; no source/flow).
- **Pose** — an internal *saved* artifact authored inside the app, not an import output. Belongs to
  the Saved side, not the import wizard.

Migration: `StorageMigrator` maps `Model→Object`, `Texture→Reference`, bumps `schemaVersion`.

## Component / Data Flow

### Import (produces a record; never spawns)

```
FileBrowser → FilePickedEvent{Path}
      │
      ▼
ImportPipeline  (new; replaces the hardcoded HandleImportAsync)
      │ 1. Sniff: extension + introspection
      │      .glb/.gltf → has skeleton? → Rig : Object
      │      .png/.jpg  → Reference
      ▼
ImportWizard  (SpatialUi panel — "dialog + type choice")
      │ shows: file name, detected kind, type selector (detected pre-selected),
      │        asset display name, [Import] / [Cancel]
      ▼ confirm(type, displayName)
IAssetImportHandler[type].Import(rawPath)            ← the "processing"
      │ validate → copy raw into sources/{assetId}.{ext} → build Meta → create Asset record
      ▼
ImportedAssetLibrary.Add + SaveAsync → AssetImportedEvent → grid refresh
```

**Per-type handler — `IAssetImportHandler`:** `Detect(file)` (drives the wizard's suggestion) +
`Import(rawPath) → Asset record` (validate, copy source, metadata). This is the raw→typed-asset
converter — the "прогонка". Import **only** writes to the library; it does not instantiate anything.

### Why import and spawn must be separate (root-cause record)

Today `ILabAsset` (`:12`) declares `SpawnAsync(...)`, so the **asset data record is forced to
instantiate itself**. This was modeled around `BuiltinLabAsset` (holds a `_prefab`, spawns via a
trivial `Instantiate`). It is impossible for `Imported`/`Saved`, which are `[Serializable]` records
deserialized from JSON — no prefab, no DI, no loader — hence their `throw NotImplementedException`.
Spawning needs services a data object must not carry.

**Resolution:** remove `SpawnAsync` from the asset record. The record is pure data. Spawning moves
to a service.

### Spawn (consumes records; two triggers, one path)

**`IAssetSpawner`** — a service resolved **by type** via DI, with its own dependencies (glTFast
loader, Builtin prefab map, `IObjectResolver`). `Spawn(asset, pose) → GameObject`. Geometry comes
**by source**; behavior is applied **by type**:

| Source | Geometry |
|---|---|
| Builtin | `Instantiate(prefab)` (unchanged) |
| Imported / Saved | load from `sources/{assetId}.{ext}` — glTFast for `.glb/.gltf`; `Texture2D.LoadImage` for images |

Both spawn triggers go through the same registry:

```
TRIGGER 1 — place from browser:
  AssetBrowserPanel → AssetSpawnRequestedEvent{asset, pose}
    → AssetSpawner → spawners[asset.Type].Spawn(asset, pose)
    → SceneGraph.AddNode(go, AssetRef{source, assetId}, name) → InjectGameObject

TRIGGER 2 — scene load:
  SceneGraph.OnSceneOpenedAsync → per NodeData:
    registry.Find(node.AssetRef) → spawners[asset.Type].Spawn(asset, pose)
    → AddNodeInternal(isLoad: true)
```

Spawner behavior by type: Object → mesh + Selectable/interactable; Reference → textured quad; Rig →
**Slice 1: static skinned mesh** (visible, bind pose, via the Object path); **Slice 2: + runtime
proxy-rig** (see Decomposition).

### How this fixes "disappears after a session"

1. `scene.json` already stores `AssetRef{Imported, assetId}` + transform (`CaptureSnapshot`).
2. On load, `AssetRegistry.Find` resolves the record in the global `imported.json` (lives outside
   `scenes/`, survives the session).
3. The spawner loads geometry from `sources/{assetId}.glb` — a stable path independent of where the
   user originally picked the file.
4. The node is restored. The `catch (NotImplementedException)` drop in `SceneGraph` becomes
   unnecessary and is removed.

## Storage Layout

Imported assets are **global and reusable across scenes** (matches the "save into the imported
library" flow):

```
persistentDataPath/
├── asset-library/
│   ├── imported.json          ← Asset records (exists; extended)
│   ├── saved.json             ← Saved-library records (Slice 3)
│   └── sources/               ← NEW: copied raw source files
│       ├── {assetId}.glb
│       └── {assetId}.png
└── scenes/{sceneId}/scene.json ← nodes by AssetRef (exists)
```

`PathProvider` gains source-path accessors. The existing per-scene `asset-catalog.json` and
per-scene `assets/` layout are **left untouched** here; reconciling per-scene portability is a
Slice 3 concern.

## Dependencies

- **glTFast** (`com.unity.cloud.gltfast`) — runtime glTF/GLB loading. Free, official, Quest-capable.

## Decomposition

### Slice 1 — Import + Persist foundation (this design's implementation target)

In scope:
- Add glTFast.
- Data refactor: `ILabAsset` → pure data (drop `SpawnAsync`); `AssetType → {Object, Rig, Reference}`;
  record = `{Id, DisplayName, Type, Source, SourceRef, Icon, Meta}`.
- `IAssetSpawner` + by-type registry; `ObjectSpawner` (glTFast + Builtin prefab), `ReferenceSpawner`
  (textured quad). Builtin path preserved.
- Source storage: copy raw into `asset-library/sources/{assetId}.{ext}`; `PathProvider` additions.
- `ImportPipeline`: sniff → wizard → handler → library. `IAssetImportHandler` for Object / Reference.
- `ImportWizard` SpatialUi panel (dialog + type choice, detected type pre-selected, manual override).
- Persistence: `SceneGraph` load delegates to the spawner registry; imported nodes survive restart;
  the `NotImplementedException` drop is removed.
- Migration in `StorageMigrator` (`Model→Object`, `Texture→Reference`, schema bump).
- Cleanup: fold the catalog-based `AssetImporter` into `ImportPipeline`; keep `DemoAssetCatalog` as
  the Builtin library's source.
- **Rig in Slice 1:** detected and stored as `Rig`, but **spawned as a static skinned mesh** (no
  proxy bones) until Slice 2. Explicit expectation — no surprise.

### Slice 2 — Rig functionality (runtime proxy-rig) — BLOCKED on outline work

Make an imported skinned character riggable inside the app. glTFast yields a `SkinnedMeshRenderer` +
skeleton, but the app's pose/animation authoring needs **proxy bones** — the selectable/grabbable
bone widgets generated by `PromeonProxyRigBuilder`. That builder is **edit-time only** today:
`Rebuild()` runs from the editor and bakes proxies into a prefab; at runtime `OnEnable` only
re-populates baked proxies. Slice 2 adds a **runtime rig-build path** (`RigSpawner` drives the
builder at runtime over a freshly-loaded skeleton). **Touches `PromeonProxyRigBuilder.cs`** —
blocked until the concurrent outline edit lands.

### Slice 3 — Saved library (manual save-from-scene)

The user manually saves an object/assembly out of a scene as a reusable asset (`saved.json` +
`SavedLabAsset` spawn via the same `IAssetSpawner`). Distinct from import. Also the place to
reconcile per-scene `asset-catalog.json` / scene portability if desired.

## Risks / Open Items

- **Async load on scene open:** glTFast load is async; `SceneGraph` already awaits and loads nodes
  sequentially. Acceptable for now; batch/parallel load is a later optimization.
- **`AssetRef.Source` correctness:** must be set so `Find` hits the right library. With records as
  pure data, the owning library tags `Source` (or it is stored on the record). Today `AssetSpawner`
  infers it from the concrete class — that inference is replaced.
- **Imported thumbnails:** `Icon` may be `null` for imported assets in Slice 1; preview generation is
  a later nicety.
- **Reference object behavior:** image quad's interactivity/lock semantics decided at plan time.
- **Slice 2 coordination:** do not start until `PromeonProxyRigBuilder` is free of the outline edit.

## Self-Review

- **Placeholders:** none; deferred items are explicitly assigned to Slice 2/3, not left as TBD.
- **Consistency:** Asset record shape (`{Id, DisplayName, Type, Source, SourceRef, Icon, Meta}`) is
  used identically in the Conceptual Model, Component Flow, and Slice 1 scope. `IAssetImportHandler`
  (raw→record) and `IAssetSpawner` (record→GameObject) are the only two per-type abstractions and
  appear consistently. The three libraries (Builtin/Imported/Saved) and three types
  (Object/Rig/Reference) are stable throughout.
- **Scope:** this is architecture-only; Slice 1 is the single implementable unit. Slices 2/3 are
  named but deferred (2 is hard-blocked).
- **Ambiguity:** "Saved" is pinned to manual save-from-scene (not import-result reuse); "Rig in
  Slice 1" is pinned to static skinned mesh.
