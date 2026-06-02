# Exporter scaffold — handoff notes

> ✅ **COMPLETED 2026-06-02.** This handoff is historical. The exporter was finished and shipped as a
> working ZIP-bundle export (not the stub manifest described below) and verified in-headset. The output
> is `Documents/{productName}/{name}.zip` (`scene.json` + `models/`/`textures/`), the panel is wired
> into the `exporter` nav-bar tab, and `SceneExporter.ExportManifest` was replaced by `SceneBundle` +
> `BuildBundle`. Current truth: `CLAUDE.md` (ExportPipeline row), `docs/BACKLOG.md` (ExportPipeline),
> and `docs/superpowers/{specs,plans}/2026-06-02-scene-export-bundle*`. Kept for reference only.

Scope: what the code session delivered vs. what still needs editor/prefab work to be usable in-headset.

---

## What was built (code only, no prefabs)

| File | Purpose |
|---|---|
| `Assets/_App/Scripts/ExportPipeline/SceneExporter.cs` | App-lifetime service; `IStartable`+`IDisposable`; subscribes to `SceneExportRequestedEvent`, writes `{MyDocuments}/{productName}/{name}.json`, publishes `SceneExportedEvent` |
| `Assets/_App/Scripts/ExportPipeline/Events/SceneExportRequestedEvent.cs` | Request struct — panel→exporter |
| `Assets/_App/Scripts/ExportPipeline/Events/SceneExportedEvent.cs` | Result struct — exporter→panel |
| `Assets/_App/Scripts/SpatialUi/Panels/ExportPanel.cs` | `MonoBehaviour` panel module; `[Inject] Construct(EventBus, SceneContext, SceneExporter, AppStorage)` |
| `Assets/_App/Scripts/Bootstrap/RootLifetimeScope.cs` | Added `RegisterEntryPoint<SceneExporter>` + `FindObjectsByType<ExportPanel>` inject in the build-callback |

Output path format: `{Environment.SpecialFolder.MyDocuments}/{Application.productName}/{sanitizedFileName}.json`

---

## What remains (editor + prefab work)

### 1. Create the ExportPanel prefab

In the Unity Editor, create a new child `GameObject` inside the UserPanel hierarchy (same level as the AnimatorPanel GO, e.g. under `UserPanel/Modules/`).

Required child layout (all under the new GO):

| GameObject name | Component | Wired to field |
|---|---|---|
| `FileNameInput` | `TMP_InputField` | `_fileNameInput` |
| `PathLabel` | `TMP_Text` | `_pathLabel` |
| `SceneNameLabel` | `TMP_Text` | `_sceneNameLabel` |
| `ExportButton` | `Button` | `_exportButton` |
| `StatusLabel` | `TMP_Text` | `_statusLabel` |

Add the `ExportPanel` script to the root GO. Wire all five serialized fields in the Inspector.

Save the GO as a prefab in `Assets/_App/Content/Prefabs/UI/` (or wherever the other panel prefabs live; check the `UserPanel` prefab).

### 2. Add `TrackedDeviceGraphicRaycaster`

Any panel that detaches or lives on the XR rig needs a `TrackedDeviceGraphicRaycaster` on its Canvas root — otherwise VR pointer clicks are dead. Confirm the ExportPanel's Canvas (or the shared UserPanel Canvas) already has one; if not, add it.

### 3. Register as a NavBar region

Open the `NavBarConfig` ScriptableObject (assigned on `RootLifetimeScope._navBarConfig`; lives in `Assets/_App/Content/ScriptableObjects/`).

Add a new entry to its regions list with:
- **ModuleId** — a unique string key, e.g. `"export"` (this is what `RegionMember.ModuleId` must match).
- **Label / Icon** — the nav-bar button label and icon sprite.
- **VisibleInModes** — which app modes expose this tab (e.g. `VrEditing`; omit `MainMenu` / `Sandbox` unless desired).

### 4. Add `RegionMember` to the prefab

On the ExportPanel root GO, add a `RegionMember` component and set its **ModuleId** field to the same string chosen above (e.g. `"export"`).

`RegionMember` is picked up by the `RegisterBuildCallback` loop in `RootLifetimeScope` at boot: it calls `c.Inject(rm)` and `router.RegisterModule(rm.ModuleId, rm)` automatically — no further code changes needed.

### 5. Add a `RegionNavButton` for the new tab

On the UserPanel nav-bar (the row of tab buttons), duplicate an existing `RegionNavButton` GO. Set its **RegionId** field to `"export"` and point its **Label**/**Icon** components to the assets chosen in step 3.

`RootLifetimeScope.RegisterBuildCallback` also loops over all `RegionNavButton`s and calls `c.Inject(nav)` + `router.RegisterButton(nav)` — again, no code change needed.

### 6. Verify injection order

Because `ExportPanel` is found via `FindObjectsByType` in `RootLifetimeScope.RegisterBuildCallback`, it must be present in the persistent scene (i.e. the XR rig's `DontDestroyOnLoad` hierarchy) at app start. If the prefab is only in a mode scene, move it to the persistent `PersistentRoot` or the XR rig variant.

### 7. Remaining TODOs in the code

- `SceneExporter.ExportManifest` — replace the four-field stub with real mesh nodes, bone poses, and keyframe tracks (marked with `// TODO: real geometry/animation export`).
- Error surfacing — `ErrorDispatcher` is not implemented yet (see `docs/BACKLOG.md`); export errors currently go to `Debug.LogError` and `SceneExportedEvent.Message`.
