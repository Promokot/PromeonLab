# Scene Export Bundle — Design

**Date:** 2026-06-02
**Status:** Approved-for-planning
**Subsystem:** `ExportPipeline`
**Supersedes:** the stub-manifest scaffold in `SceneExporter.cs` (see `docs/superpowers/exporter-scaffold-handoff.md`)

---

## Goal

Turn the exporter scaffold into a working in-scene feature: from VR editing, the user enters a
file name and taps **Export**, producing a self-contained **ZIP bundle** in
`Documents/{productName}/` that carries the scene description **plus the referenced model/texture
source files**, for consumption by an external (non-PromeonLab) tool.

**Not a re-importable format.** The bundle is a one-way snapshot for an external DCC/script. We do
not need to read it back into PromeonLab.

---

## Decisions (locked during brainstorming)

| Decision | Choice | Rationale |
|---|---|---|
| Scope this pass | UI wiring **and** real content | One full working cycle. |
| Bundle content | Self-contained + geometry | External tool gets models with the scene. |
| Bundle form | **ZIP archive** (single file) | One portable artifact, easy to pull off the headset; `System.IO.Compression` works on Quest/Android. |
| Geometry source | **Copy source files** (`.glb`/`.png`) into the zip | Lossless, light; no fragile runtime mesh-baking. |
| Builtin assets (no source file) | **Reference + `geometryMissing` flag** | Imported models export fully; builtins are referenced by id and flagged — honest v1. |
| Class count | Minimal — DTO + exporter only | No `*Builder`/`*Writer`/`*Manifest` types (user preference + anti-junk-drawer rule). |

---

## Artifact layout

```
Documents/{Application.productName}/{sanitizedName}.zip
├── scene.json                 ← SceneBundle (flat external schema), serialized with JsonUtility
├── models/{assetId}.glb        ← copied source of each Imported Object/Rig node (deduped by assetId)
└── textures/{assetId}.png      ← copied source of each Imported Reference image (deduped by assetId)
```

Builtin nodes contribute an entry in `scene.json` but **no** file under `models/` or `textures/`.

### `scene.json` schema (SceneBundle, schemaVersion 1)

```jsonc
{
  "schemaVersion": 1,
  "exportedAtUtc": "2026-06-02T12:00:00.000Z",
  "scene":  { "id": "ab12cd34", "name": "My Scene" },
  "fps": 24,
  "nodes": [
    {
      "nodeId": "f0a1b2c3",
      "displayName": "Hero",
      "parentNodeId": "",                  // empty when root
      "assetSource": "Imported",           // "Imported" | "Builtin"
      "assetId": "9e8d7c6b",
      "assetType": "Rig",                  // "Object" | "Rig" | "Reference"
      "geometryFile": "models/9e8d7c6b.glb", // "" when geometryMissing
      "geometryMissing": false,            // true for Builtin (no source on disk)
      "position": { "x":0, "y":0, "z":0 },   // Unity JsonUtility object form (see note below)
      "rotation": { "x":0, "y":0, "z":0, "w":1 },
      "scale":    { "x":1, "y":1, "z":1 },
      "bonePoses": [                        // empty for non-rig nodes; reuses BonePose
        { "BoneName": "pelvis", "LocalPosition": {"x":0,"y":0,"z":0},
          "LocalRotation": {"x":0,"y":0,"z":0,"w":1}, "LocalScale": {"x":1,"y":1,"z":1} }
      ],
      "animation": {                        // null when the node has no ActionContainer
        "totalFrames": 60,
        "interpolation": "Linear",          // "Linear" | "Stepped"
        "loop": false,
        "tracks": [
          {
            "targetNodeId": "f0a1b2c3",     // object track = node id; bone track = "bone:{node}:{bone}"
            "keys": [                        // reuses AnimKeyData
              { "Frame": 0,  "Position": [..], "Rotation": [..], "Scale": [..] }
            ]
          }
        ]
      }
    }
  ]
}
```

**Schema notes**
- `bonePoses` reuses the existing `[Serializable] BonePose`; `keys` reuses `AnimKeyData`. We do **not**
  define parallel DTOs for these — only the wrapper (`SceneBundle`) and its `Node` / `Animation` /
  `Track` shapes are new. The node transform is stored inline on `Node` (`position`/`rotation`/`scale`),
  not as a separate `Transform` type.
- **Vector representation:** all vectors/quaternions serialize in Unity `JsonUtility`'s object form
  (`{"x":..,"y":..,"z":..}`), not as JSON arrays — a consequence of reusing `BonePose`/`AnimKeyData`
  (`Vector3`/`Quaternion` fields) and keeping the node transform consistent with them.
- **Null animation:** `JsonUtility` cannot emit `null` for a nested `[Serializable]` reference, so a
  node with no `ActionContainer` serializes as an empty `animation` object (`totalFrames:0`, empty
  `tracks`). The external tool should read "empty `tracks`" as "no animation". The in-memory field is
  still `null` (what the unit tests assert).
- Field casing follows whatever the reused types already use (`BonePose.BoneName`, `AnimKeyData.Frame`);
  the external tool is told the schema, so mixed casing is acceptable for v1.
- `targetNodeId` carries the raw internal track id, including bone-composite ids
  (`bone:{rigNodeId}:{boneName}`). The external tool can split on `:` if it cares.

---

## Data flow

```
ExportPanel  ──SceneExportRequestedEvent{FileName}──►  SceneExporter
                                                          │ (main thread)
                                                          ├─ SceneContext.Graph.CaptureSnapshot()      → SceneData (nodes, transforms, bone poses)
                                                          ├─ SceneContext.Authoring.CaptureForExport() → SceneAnimationData (or null in Sandbox)
                                                          ├─ IAssetRegistry.Find(assetRef)             → ILabAsset (Source, Type, SourceRef)
                                                          ├─ PathProvider.RootForSources + SourceRef   → absolute source path
                                                          │
                                                          ├─ SceneExporter.BuildBundle(...)  [static, pure]
                                                          │     → (SceneBundle, list of (entryPath, absSourcePath))
                                                          │
                                                          └─ WriteZip(path, bundle, sourceFiles)  [file IO, Task.Run]
                                                                └──SceneExportedEvent{Path,Success,Message}──►  ExportPanel
```

- **Unity-touching work** (graph snapshot, registry lookups, resolving source paths) runs on the main
  thread. The returned data is plain C# (no `UnityEngine.Object` references), so the **zip write** can
  run on a thread-pool thread via `Task.Run` without touching the Unity API.
- The exporter is **root-lifetime**; it reads scene services through the root `SceneContext` façade,
  which is non-null only while a scene scope is live. Export is only reachable from VrEditing, but the
  exporter still guards on `ctx.HasScene` and a null `Authoring` (Sandbox does not register it).

---

## Components

Two new/changed code units. No `*Builder`/`*Writer`/`*Manifest` classes.

| Unit | File | Responsibility |
|---|---|---|
| `SceneBundle` (DTO) | `Assets/_App/Scripts/ExportPipeline/SceneBundle.cs` | `[Serializable]` external schema. Top-level type + nested `SceneRef`, `Node` (transform inline), `Animation`, `Track`. Reuses `BonePose` and `AnimKeyData`. JsonUtility-friendly (public fields, flat). |
| `SceneExporter` (rewrite) | `Assets/_App/Scripts/ExportPipeline/SceneExporter.cs` | Orchestrator. Captures live state, resolves assets, calls its own `static BuildBundle(...)` (pure, testable via `InternalsVisibleTo`), writes the zip, publishes the result. Keeps `BuildTargetPath` (now `.zip`). |

**Supporting changes (not new classes):**
- `AnimationAuthoring.CaptureForExport()` — read accessor returning the live `SceneAnimationData`
  (or `null` if none). Read-only use by the exporter.
- `SceneExporter` constructor gains `SceneContext`, `IAssetRegistry`, `PathProvider` (all root-scoped),
  alongside the existing `EventBus`/`AppStorage`.
- `ExportPanel.cs` — `_pathLabel` shows the `.zip` path (it already calls `BuildTargetPath`, so the
  change is in the exporter; no panel logic change beyond label copy if any literal mentions `.json`).
- Events `SceneExportRequestedEvent{FileName}` and `SceneExportedEvent{Path,Success,Message}` are
  unchanged.

`Assets/_App/Tests/ExportPipeline/InternalsVisibleTo.cs` (or reuse an existing assembly-level attribute)
exposes `BuildBundle` and the zip writer to `_App.Tests`.

---

## ZIP writing

- Use `System.IO.Compression.ZipArchive` over a `FileStream` (`ZipArchiveMode.Create`).
- Write `scene.json` as one entry (the serialized `SceneBundle`).
- For each `(entryPath, absSourcePath)` in the bundle's source list, create an entry at `entryPath`
  (e.g. `models/9e8d7c6b.glb`) and copy the file bytes in. **Dedup** by `entryPath` so a model used by
  several nodes is copied once.
- Missing source file on disk → skip the entry, set that node's `geometryMissing = true`, and append a
  warning to the result `Message` (do not fail the whole export).
- Ensure the output directory exists; sanitize the file name (existing `SanitizeFileName`, now forcing
  `.zip`).

---

## UI wiring (editor / MCP work)

The user will do final visual layout; this work only needs the **fields wired** and the tab reachable.

1. **Panel prefab:** In `UserPanel`, copy the existing `AssetBrowserModule` instance, **unpack** it,
   strip the asset-browser-specific innards, and save it as a new prefab
   `Assets/_App/Content/Prefabs/UI/Panels/UserPanel/ExportModule.prefab`. It inherits the working
   Canvas + `TrackedDeviceGraphicRaycaster` from the copy ([[project_vr_panel_raycaster_gotcha]]).
2. **Swap the panel script:** The copy carries the source panel's own MonoBehaviour
   (`AssetBrowserPanel`) plus its asset-browser wiring. **Remove that component** (and any
   asset-browser-only sub-components), then **add the `ExportPanel` script** to the prefab root.
   This is the "script on the panel itself" — without it the fields have nothing to bind to.
3. **Fields:** Wire the five `ExportPanel` fields on the new prefab:
   `_fileNameInput` (TMP_InputField), `_pathLabel`, `_sceneNameLabel`, `_statusLabel` (TMP_Text),
   `_exportButton` (Button). The file-name input needs a `VrInputFieldProxy` so the VR keyboard sees
   it (publishes `KeyboardFocusEvent` on pointer-down).
4. **Region membership:** Add `RegionMember{ModuleId="export"}` to the prefab root.
5. **NavBar config:** In `NavBarConfig` add a region entry `"export"` (Label/Icon;
   `VisibleInModes = VrEditing`).
6. **Nav button:** Duplicate the **Gizmo Tools** nav button in the same navbar group; set its
   `RegionId = "export"`. `RootLifetimeScope.RegisterBuildCallback` auto-injects and registers both the
   `RegionMember` and the `RegionNavButton` — no scope code changes needed.
7. **Persistence:** The prefab must live in the persistent UserPanel hierarchy (found via
   `FindObjectsByType<ExportPanel>` in the root build-callback at boot).

---

## Testing (EditMode)

Pure-logic and IO tests, runnable without entering Play mode.

1. **`BuildBundle` — node mapping:** given a hand-built `SceneData` (one Imported Object, one Imported
   Rig with bone poses, one Builtin) and a fake asset-resolver delegate, assert the produced
   `SceneBundle` has the right node count, `assetSource`/`assetType`, `geometryFile` for imported,
   `geometryMissing=true` + empty `geometryFile` for builtin, and correct transforms/bone poses.
2. **`BuildBundle` — animation mapping:** given a `SceneAnimationData` with a container (interpolation
   Stepped, loop true, two tracks), assert the matching node's `animation` block carries
   `totalFrames`/`interpolation`/`loop` and the tracks/keys; nodes without a container get
   `animation == null`.
3. **`BuildBundle` — source dedup:** two nodes referencing the same imported `assetId` produce a single
   entry in the source-files list.
4. **Zip round-trip:** write a bundle to a temp dir; reopen the `.zip`; assert it contains `scene.json`
   plus the expected `models/*.glb` entries; deserialize `scene.json` back into `SceneBundle` and
   confirm node count and ids.
5. **Missing source:** an imported node whose source file is absent yields `geometryMissing=true`, no
   zip entry, and a non-fatal warning in the result.

**Allowed pre-existing failures** (do not block): `PathProviderTests` ×4, `RingRotateStrategyTests` ×2.

---

## Implementation order (single plan, phased)

1. **Code + tests:** `SceneBundle` DTO → `AnimationAuthoring.CaptureForExport()` → rewrite
   `SceneExporter` (capture + `static BuildBundle` + zip write) → `ExportPanel` `.zip` label → EditMode
   tests. Compile via MCP, console clean of `CS####`, run EditMode tests.
2. **UI:** copy/unpack `AssetBrowserModule` → `ExportModule.prefab`, wire fields + `RegionMember`,
   `NavBarConfig` region, duplicate Gizmo Tools nav button. (MCP / editor.)
3. **In-headset verification** (user): tab appears, fields populate, Export writes a `.zip` to Documents
   with `scene.json` + models; visual polish done by user.

**Git:** not touched by the agent; the user commits manually.

---

## Out of scope (this pass)

- Re-importing the bundle back into PromeonLab.
- Baking builtin/primitive geometry into the bundle (builtins are referenced + flagged).
- FBX export (no runtime FBX SDK).
- Real `ErrorDispatcher` surfacing — export errors still go to `Debug.LogError` +
  `SceneExportedEvent.Message`.
- Embedding materials/shaders beyond what the source `.glb` already contains.
