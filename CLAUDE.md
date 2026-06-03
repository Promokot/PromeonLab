# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**PromeonLab** is a Unity VR application for 3D skeletal animation creation targeting Meta Quest 3 (Quest 2 compatible). It runs as a standalone Android app with no PC dependency at runtime.

- **Engine:** Unity 6000.3.7f1
- **Language:** C# (no namespaces for runtime gameplay code; `VrAnimApp.Editor` for editor code; `VrAnimApp.Adapters` for platform wrappers)
- **VR Runtime:** OpenXR (cross-platform, not locked to Meta SDK)
- **DI:** VContainer (Root → Scene scope hierarchy; `RootLifetimeScope` is `DontDestroyOnLoad`)
- **Events:** Custom `EventBus` (`Publish<T>`/`Subscribe<T>`, per-scope)
- **Graphics:** URP 17.3.0
- **Serialization:** Unity JsonUtility (all data versioned with `schemaVersion`)
- **Runtime model import:** glTF/GLB via glTFast (`com.unity.cloud.gltfast`) + images (PNG/JPG); **FBX import is not supported at runtime** (export-side FBX is unchanged)

## Build & Development

This is a Unity project — there is no CLI build script. All compilation, builds, and tests run inside the Unity Editor (version 6000.3.7f1).

- **Build target:** Android (Meta Quest standalone)
- **XR configuration:** ProjectSettings/XR (OpenXR + Meta OpenXR loaders)
- **Tests:** Run via Unity Test Runner (`Window > General > Test Runner`); subsystem tests live in `Assets/_App/Tests/<Subsystem>/` (single `_App.Tests` assembly)
- **Editor-only tooling:** `Assets/_App/Editor/` folder; excluded from builds automatically via `.asmdef` platform constraints
- **Third-party packages:** Vendored under `Assets/_App/ThirdParty/` (asset packs + imported C# packages) or pulled via Package Manager — keep `Assets/` root clean

## Architecture

### VR Workflow

1. **VR Editing** — create/edit skeletal animations in immersive VR (rigs, keyframe authoring on a per-`ActionContainer` timeline; NLA composition is planned, not yet built — see `docs/BACKLOG.md`)
2. **Export** — scene → self-contained **ZIP bundle** (`scene.json` + copied model/texture sources) via `ExportPipeline`, reachable from the `exporter` nav-bar tab. Real **FBX** export still planned (see `docs/BACKLOG.md`)

> **Current-state reference:** `CLAUDE.md` is the authoritative overview. For a code-verified
> reconciliation of every subsystem (and where docs drifted), see `docs/superpowers/audit-2026-06-01/`.
> For features specced/aspirational but **not yet implemented**, see `docs/BACKLOG.md`.

### VContainer Scope Hierarchy

| Scope | Lifetime | Key Registrations |
|---|---|---|
| `RootLifetimeScope` | App lifetime (`DontDestroyOnLoad` under `PersistentRoot`) | `AppStorage`, `PathProvider`, `EventBus`, `SceneContext`, `ModeOrchestrator`, `ISceneTransition`/`SceneTransitionRunner`, `PanelRegionRouter`, `AnimationClipboard`, asset libraries (`Builtin`/`Imported`/`Saved`), `AssetRegistry`, `ImportPipeline`, `VrKeyboard`, `UserPanel` |
| Scene scope | The loaded mode scene's own `LifetimeScope` (`MainMenu`/`VrEditing`/`Sandbox`) | `SceneGraph`, `SelectionManager`, `AssetSpawner`; **VrEditing only** also registers `AnimationClock`, `AnimationAuthoring` (+ `AnimationStorage`, `AnimationPlaybackSampler`), `BoneEditMode`, `SceneAutoSaver`, `SceneDirtyTracker`; binds `SceneContext` via `SceneContextBinder` |

Child scopes may depend on parent registrations; **never the reverse**. Exactly one mode scene is loaded at a time (`LoadSceneMode.Single`); its scope parents to the persistent `RootLifetimeScope`. Scene-scoped services are exposed app-wide through the root `SceneContext` façade, populated on scene-scope start and cleared on dispose by `SceneContextBinder` (which publishes `SceneContextChangedEvent`). **`HasScene` (Graph bound) does not imply other services are non-null** — Sandbox does not register `AnimationAuthoring`/`AnimationClock`, so guard on the specific service a consumer uses.

### App Modes (`ModeOrchestrator` + `ModeTransitionGraph`)

`MainMenu` ↔ `VrEditing`; `MainMenu` ↔ `Sandbox`. `ModeOrchestrator` is pure policy: it validates the transition against `ModeTransitionGraph`, then delegates the scene swap to `ISceneTransition` (`SceneTransitionRunner`), which fades the VR view to black (`HeadFade`), loads the target scene `Single`, and only then invokes the callback that publishes `ModeChangedEvent` — so the event always fires *after* the new scene and its scope exist. Before delegating the load, the orchestrator publishes `ModeExitingEvent` **synchronously while the outgoing scene and its scope are still alive** — this is the hook for save-on-exit work (`SceneAutoSaver`), since scene-scoped services are disposed during the Single load, *before* `ModeChangedEvent` fires. A re-entrancy guard drops overlapping transition requests.

### Subsystems

Located in `Assets/_App/Scripts/<Subsystem>/`. Interfaces and contracts for each subsystem live in that subsystem's own folder; only the truly-generic primitive (`EventBus.cs`) lives in `Scripts/Core/`.

| Subsystem | Core Responsibility |
|---|---|
| `StorageCore` | File I/O, JSON serialization, `PathProvider`, inline schema migration in `SceneSerializer.Deserialize` (versioned `scene.json`; current v3 adds per-rig bone poses) |
| `AssetBrowser` | VR gallery UI over three asset libraries keyed by `AssetSource` (`Builtin`/`Imported`/`Saved`); runtime import pipeline (`ImportPipeline` + `ImportWizardPanel`) for glTF/GLB (via glTFast) and images; **build-once/restore-many** entity pipeline (`IAssetEntityBuilder` + `AssetEntityBuilderRegistry`, Object/Rig/Reference builders) with capability applied via `InteractionCapability.Apply`; spawning via `AssetSpawner`; **thumbnails generated at import** (`ThumbnailRenderer` off-screen-renders models → `thumbnails/{id}.png`; images reuse their source; shown by `AssetBrowserPanel.ResolveIcon` per `ILabAsset.ThumbnailRef`); no direct file access (delegates to `StorageCore`/`ImportedSourceProvider`) |
| `SceneComposition` | Scene node hierarchy, `SelectionManager` (single-select: `Select(id?)` / `SelectedNodeId`). **No undo/redo** — the `CommandStack`/`ICommand` subsystem was removed; mutations apply directly |
| `RigBuilder` | Runtime proxy-bone rig built on spawn (`RigEntityFabricator.BuildProxyRig` → per-bone proxy GO + `BoneFollower`; coordinated by `ProxyRigRuntime`). Bone poses persist via schema-v3 `NodeData.BonePoses`. **IK chains are serialized but no solver consumes them yet** (no Animation Rigging) |
| `Animation` | Per-`ActionContainer` keyframe authoring. `AnimationAuthoring` is a CRUD/orchestration façade split (A1) into static `AnimationClipBaker` (track→clip + interpolation tangents), `AnimationPlaybackSampler` (`ITickable` sampling/loop playback), and `AnimationStorage` (`animation.json` load/save, non-destructive on unsupported versions). Features: selected-track keying, per-container **Linear/Stepped interpolation** (runtime tangents, not `AnimationUtility`), scrub preview, debounced persistence, `AnimationClip`-based sampling. Transport (`AnimationClock`) is always **single-shot** (scrub + play/pause + scene-wide fps; rewinds to the first keyframe at end). **Per-object Loop** is background playback — `AnimationPlaybackSampler` is the `ITickable` and samples every looping container on its own cursor, so multiple looped objects play concurrently regardless of selection; the transport drives only the selected object. During transport **playback** `AnimationPlaybackSampler.Tick` samples the active container at the clock's **fractional** position (`AnimationClock.CurrentFrameContinuous`) each render frame — motion is smooth, not quantized to the animation fps; the integer `FrameChangedEvent` only steps the playhead, and `ApplyFrame` (integer) handles **scrub** while paused. **No NLA / master timeline yet** (see `docs/BACKLOG.md`). UI: `AnimatorPanel` + `Animator*View` modules |
| `ExportPipeline` | **Working ZIP-bundle export.** `SceneExporter` (app-lifetime, request/result events) captures live state via `SceneContext` (Graph snapshot + `AnimationAuthoring.CaptureForExport`), runs a pure `static BuildBundle`, and writes `Documents/{Application.productName}/{name}.zip` = `scene.json` (flat external schema, `SceneBundle`, **one-way / not re-importable**) + `models/{assetId}.glb` + `textures/{assetId}.png` (copied import sources, deduped). Builtin assets carry no source file → flagged `geometryMissing`. Zip via `System.IO.Compression.ZipArchive` (Quest-safe), written on a thread-pool thread. UI: `ExportPanel` on the `exporter` nav-bar tab (`ExportModule.prefab`). Real **FBX** / richer JSON still planned (see `docs/BACKLOG.md`) |
| `InputBindings` | Controls vocabulary for the Settings panel: `ControlsProfile` (SO) + `ControlBinding` data, rendered by `SettingsPanel`. (The interaction *input model* itself lives in `VrInteraction`.) |
| `ModeOrchestrator` | Mode policy: validates `ModeTransitionGraph`, delegates to `ISceneTransition`/`SceneTransitionRunner` (single-scene load behind `HeadFade`); publishes `ModeExitingEvent` before the load (outgoing scope still alive) and `ModeChangedEvent` after the load |
| `VrInteraction` | `XRPromeonInteractable` direct-input on `NearFarInteractor` (tap-trigger = select, hold-trigger = rotate, hold-grip = move; XRI select-flow disabled); `GizmoDriver` gizmo (highlight via `GizmoHighlightPainter`, drag via `GizmoDragSession`); `InteractionMaskBinder` contextual cast-masks; QuickOutline-based outline. **Single-select** |
| `SpatialUi` | VR panels (`SpatialPanel`: `BodyLocked` / `WorldFixed` / `Free` + billboard); root-lifetime region/navbar model (`PanelRegionRouter` + `NavBarConfig` + `RegionMember`); `UserPanel` (grip-grab + triple-lock); `SettingsPanel`; `AnimatorPanel` |
| `ErrorHandling` | `ErrorLevel` enum + `ErrorOccurredEvent`. **`ErrorDispatcher` is not implemented** — error reporting currently goes straight to `Debug.Log*` (see `docs/BACKLOG.md`) |

### Cross-Subsystem Communication

All cross-subsystem messages are `struct` types suffixed `Event` (e.g., `SceneOpenedEvent`, `ModeChangedEvent`), published via the per-scope `EventBus` (`Publish<T>`/`Subscribe<T>`). `*Event` structs live in each subsystem's `Events/` subfolder. **Direct method calls across subsystem boundaries are forbidden.** Key events:

`SceneOpened` → SceneComposition, AssetBrowser  
`SceneModified` → SceneDirtyTracker  
`SelectionChanged` → PropertyPanel, GizmoDriver, `ProxyRigRuntime`, `SelectionVisualSync`  
`FrameChanged` → `AnimationAuthoring` (samples the clip), `AnimatorPanel` (moves playhead)  
`ModeExiting` → SceneAutoSaver (fired *before* the Single load, while the outgoing scene/scope are still alive)  
`ModeChanged` → SpatialUi region router / nav-bar visibility (fired *after* the new scene has loaded)  
`SceneContextChanged` → OutlinerPanel, InspectorPanel, PropertyPanel, AnimatorPanel (scene services bound/unbound)  
`FilePicked` → ImportPipeline (picks an `IAssetImporter` by extension, opens the import wizard)  
`ImportRequested` → ImportWizardPanel (show wizard: file name, suggested type/name)  
`ImportConfirmed` → ImportPipeline (handler copies source + writes the library record)  
`AssetImported` → AssetBrowser grid refresh  
`AssetSpawnRequested` → `AssetSpawner` (restores through `AssetEntityBuilderRegistry.RestoreAsync`)  

### Data Storage Layout

All paths are built exclusively through `PathProvider` — no manual string concatenation.

```
Application.persistentDataPath/
├── asset-libraries/                  (global, reusable across scenes)
│   ├── imported-lib.json             (Imported-library records; recipe-per-entry, schemaVersion 2)
│   ├── saved-lib.json                (Saved-library records; persisted, but spawn-from-saved/Slice 3 not yet implemented)
│   ├── sources/{assetId}.{ext}       (copied raw import files — .glb/.gltf/.png/.jpg/.jpeg)
│   └── thumbnails/{assetId}.png      (rendered model thumbnails, RGB24 256²; images reuse their source instead)
└── scenes/{SceneId}/
    ├── scene.json            (scene graph + per-rig bone poses, schemaVersion 3)
    ├── animation.json        (per-ActionContainer keyframe data + per-container interpolation/loop + scene-wide fps, schemaVersion 2; written by AnimationAuthoring)
    └── asset-catalog.json    (per-scene asset registry)
```

> **Export output is NOT written under `scenes/`** — `SceneExporter` writes the ZIP bundle to
> `Documents/{Application.productName}/{name}.zip` (outside `persistentDataPath`). The legacy
> `PathProvider.ExportDir(sceneId)` (`scenes/{id}/export/`) is unused by the current exporter.

Imported assets are global: a node in `scene.json` stores `AssetRef{Source, AssetId}`, and the spawner restores geometry from `asset-libraries/sources/` by the record's `SourceRef` (stored **relative** to `persistentDataPath`). Rig definitions and bone poses are carried **inline** — rig data in the asset recipe, bone poses in each node's `NodeData.BonePoses` — so the old per-scene `Rigs/` and `Poses/` folders are no longer written. `Saved` is a distinct, scene-origin flow (manual save-out), not yet implemented.

## Folder Structure

```
Assets/
├── _App/                             ← ALL project code, owned content, AND vendored third-party
│   ├── Scripts/                      ← ALL runtime C# code (_App.Runtime.asmdef)
│   │   ├── Core/                     ← Generic primitive: EventBus.cs
│   │   ├── Bootstrap/                ← LifetimeScopes, AppBootstrap, scene loader
│   │   └── {SubsystemName}/          ← one folder per subsystem
│   │       ├── {Name}.cs             ← primary façade (if needed)
│   │       ├── Data/                 ← structs, enums, ScriptableObjects
│   │       ├── Events/               ← *Event structs for this subsystem
│   │       └── UI/                   ← subsystem-specific panels/views
│   ├── Editor/                       ← project-wide editor-only code (_App.Editor.asmdef)
│   ├── Tests/                        ← all tests (_App.Tests.asmdef)
│   │   └── {SubsystemName}/          ← tests per subsystem
│   ├── Content/                      ← owned art/asset files
│   │   ├── Prefabs/                  ← UI, Gizmos, Assets, Environment, XR
│   │   ├── ScriptableObjects/
│   │   ├── Materials/
│   │   ├── Models/
│   │   ├── Textures/
│   │   └── Shaders/
│   ├── Scenes/
│   ├── Documentation/
│   └── ThirdParty/                   ← vendored 3rd-party packs (QuickOutline, SimpleFileBrowser, Keyboard Package, ColorSkies, model packs); reimport overwrites local patches
└── (root also holds package/engine-managed folders — Settings/, XR/, XRI/, TextMesh Pro/, CompositionLayers/, Samples/ — do not relocate)
```

## Key Conventions

### Naming

| Category | Convention |
|---|---|
| Classes / structs | `PascalCase` |
| Interfaces | `I` + `PascalCase` |
| ScriptableObjects | suffix `Config`, `Profile`, or `Graph` |
| Event message types | suffix `Event` |
| Abstract base classes | prefix `Base` |
| Private fields | `_camelCase` |
| Async methods | suffix `Async` |
| Coroutines | suffix `Routine` |
| C# intra-subsystem events | prefix `On` (`OnFrameChanged`) |
| Constants | `UPPER_SNAKE_CASE` |

### Rules

- `[SerializeField] private` for inspector-exposed `MonoBehaviour`/`ScriptableObject` fields — **never `public` fields** on behaviors/SOs. (Plain `[Serializable]` data classes serialized by `JsonUtility` — `BoneRecord`, `NodeData`, `AnimKeyData`, event structs — may use `public` fields, since `JsonUtility` won't serialize private fields without `[SerializeField]` and these carry no behavior.)
- No `async void` except Unity lifecycle entry points (`Start`, `Awake`), wrapped with error handling; pass `CancellationToken` as the last parameter on all async methods
- ScriptableObjects for config/graphs/profiles only — **not** for runtime mutable state
- One public type per file; file name matches type name exactly
- **No undo/redo subsystem** — the `CommandStack`/`ICommand`/`TransformCommand` undo stack was removed; mutations apply directly (transform undo may be reconsidered later, see `docs/BACKLOG.md`)
- Platform-dependent code behind interfaces declared in the owning subsystem's folder (no concrete platform classes at call sites)
- All serialized data carries a `schemaVersion` field; migrations are inline at the deserialization boundary (e.g. `SceneSerializer.Deserialize` does v1/v2→v3) — there is no separate `StorageMigrator` class
- Cross-subsystem boundaries are enforced by convention (folder structure + code review) — there are no per-subsystem assemblies; all runtime code compiles into `_App.Runtime`
- Subsystem-specific code stays in its subsystem folder under `Scripts/`

### Strictly Forbidden

- `FindObjectOfType` / `FindAnyObjectByType` / `GameObject.Find` in **gameplay/runtime** code — use DI. **Exception:** the DI-bootstrap shim inside `LifetimeScope.Configure` (and its `RegisterBuildCallback`s) may use `FindAnyObjectByType` / `FindObjectsByType(..., FindObjectsInactive.Include)` *solely* to locate scene-placed `MonoBehaviour`s and hand them to `builder.Inject` / `RegisterInstance`. That is the only legal home for `Find*`; it must never appear in a panel, behavior, or service.
- `Singleton.Instance` pattern — use VContainer scopes
- `static` fields holding mutable runtime state (pure-function statics and `static readonly` cached constants are fine)
- **Junk-drawer type names** — don't reach for `*Manager`/`*Handler`/`*Helper`/`*Utils`/`*Processor`/`*Service`/`*Controller` as a *default* when a domain noun exists (`SceneGraph` not `SceneManager`; `SelectionManager` not `SelectionController`). A pattern suffix is acceptable when it **is** the domain role with a specific prefix (`SelectionManager`, `ModeOrchestrator`, `*Orchestrator`). Banned outright: bare/over-generic names with no domain prefix (`Manager`, `Utils`, `Helper`, `DataController`) and catch-all `*Service`/`*Utils`/`*Helper` grab-bag classes.
- `Resources.Load` — `_App` code must not use this; use prefab references via DI or `Content/` folder
- `MonoBehaviour` as a data container
- `Update()`-based polling where an event suffices
- Swallowing exceptions silently
- `#if UNITY_EDITOR` guards in runtime files — editor code goes in `Editor/` folders
