# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**PromeonLab** is a Unity VR application for 3D skeletal animation creation targeting Meta Quest 3 (Quest 2 compatible). It runs as a standalone Android app with no PC dependency at runtime.

- **Engine:** Unity 6000.3.7f1
- **Language:** C# (no namespaces for runtime gameplay code; `VrAnimApp.Editor` for editor code; `VrAnimApp.Adapters` for platform wrappers)
- **VR Runtime:** OpenXR (cross-platform, not locked to Meta SDK)
- **DI:** VContainer (Root → Scene → Feature scope hierarchy)
- **Events:** Custom `EventBus` (`Publish<T>`/`Subscribe<T>`, per-scope)
- **Graphics:** URP 17.3.0
- **Serialization:** Unity JsonUtility (all data versioned with `schemaVersion`)

## Build & Development

This is a Unity project — there is no CLI build script. All compilation, builds, and tests run inside the Unity Editor (version 6000.3.7f1).

- **Build target:** Android (Meta Quest standalone)
- **XR configuration:** ProjectSettings/XR (OpenXR + Meta OpenXR loaders)
- **Tests:** Run via Unity Test Runner (`Window > General > Test Runner`); subsystem tests live in `Assets/_App/Tests/<Subsystem>/` (single `_App.Tests` assembly)
- **Editor-only tooling:** `Assets/_App/Editor/` folder; excluded from builds automatically via `.asmdef` platform constraints
- **Third-party packages:** Vendored under `Assets/_App/ThirdParty/` (asset packs + imported C# packages) or pulled via Package Manager — keep `Assets/` root clean

## Architecture

### VR Workflow

1. **VR Editing** — create/edit skeletal animations in immersive VR (rigs, keyframes, NLA composition)
2. **Export** — FBX (via Unity FBX Exporter SDK) or custom JSON fallback

### VContainer Scope Hierarchy

| Scope | Lifetime | Key Registrations |
|---|---|---|
| `RootLifetimeScope` | App lifetime | `AppStorage`, `AssetImporter`, `PathProvider`, `AnimationClock` |
| `SceneLifetimeScope` | Unity scene loaded | `ModeOrchestrator`, `SceneGraph`, `SelectionManager`, `UiPanelManager`, `CommandStack` |
| `FeatureLifetimeScope` | Active app mode | `PlaybackController`, `RigRuntime`, `TrackRecorder` |

Child scopes may depend on parent registrations; **never the reverse**. `FeatureLifetimeScope` is created/disposed by `ModeOrchestrator` on mode transitions.

### App Modes (`ModeOrchestrator` + `AppStateMachine`)

`MainMenu` ↔ `VrEditing`; `MainMenu` ↔ `Sandbox`; `Debug` overlays any mode.

### Subsystems

Located in `Assets/_App/Scripts/<Subsystem>/`. Interfaces and contracts for each subsystem live in that subsystem's own folder; only the two truly-generic primitives (`EventBus.cs`, `ICommand.cs`) live in `Scripts/Core/`.

| Subsystem | Core Responsibility |
|---|---|
| `StorageCore` | File I/O, JSON serialization, `PathProvider`, schema migration via `StorageMigrator` |
| `AssetBrowser` | VR gallery UI over `StorageCore`; drag-and-drop to scene; no direct file access |
| `SceneComposition` | Scene node hierarchy, `CommandStack` (undo/redo), `SelectionManager` |
| `RigBuilder` | Skeletal rigging from imported mesh; IK/FK via Unity Animation Rigging |
| `AnimationAuthoring` | `ActionData`, keyframe recording, NLA composition (`NlaComposer`) |
| `AnimationPlayback` | `PlaybackController`, `AnimationEvaluator`, scrub/loop/speed transport |
| `ExportPipeline` | FBX + custom JSON export; no reverse import |
| `InputBindings` | OpenXR controller mapping; context-switched (`Navigation`, `Ui`, `GizmoManipulation`, …) |
| `ModeOrchestrator` | `AppStateMachine`, `ModeTransitionGraph` SO, `FeatureLifetimeScope` lifecycle |
| `VrInteraction` | `RayInteractor`, `NearInteractor`, `GizmoController`, multi-select |
| `SpatialUi` | VR panels (`BodyLocked` / `WorldFixed` / `Free`), `ToolbarPanel`, billboard mode |
| `ErrorHandling` | `ErrorDispatcher`, three levels (`Warning`/`Error`/`Critical`), async error wrapping |

### Cross-Subsystem Communication

All cross-subsystem messages are `struct` types suffixed `Event` (e.g., `SceneOpenedEvent`, `ModeChangedEvent`), published via the per-scope `EventBus` (`Publish<T>`/`Subscribe<T>`). `*Event` structs live in each subsystem's `Events/` subfolder. **Direct method calls across subsystem boundaries are forbidden.** Key events:

`SceneOpened` → SceneComposition, AssetBrowser  
`SceneModified` → UnsavedChangesGuard  
`SelectionChanged` → PropertyPanel, GizmoController  
`FrameChanged` → AnimationEvaluator, TrackRecorder  
`ModeChanged` → UiPanelManager, FeatureLifetimeScope  

### Data Storage Layout

All paths are built exclusively through `PathProvider` — no manual string concatenation.

```
Application.persistentDataPath/scenes/{SceneId}/
├── scene.json            (scene graph + animation data)
├── asset-catalog.json    (asset registry)
├── assets/Models|Textures|Materials|Media/
├── Rigs/                 rig-{assetId}.json
├── Poses/                pose-{assetId}.json
└── export/               *.fbx / *.json
```

## Folder Structure

```
Assets/
├── _App/                             ← ALL project code, owned content, AND vendored third-party
│   ├── Scripts/                      ← ALL runtime C# code (_App.Runtime.asmdef)
│   │   ├── Core/                     ← Generic primitives: EventBus.cs, ICommand.cs
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

- `[SerializeField] private` for inspector-exposed fields — **never `public` fields**
- No `async void` except Unity lifecycle entry points (`Start`, `Awake`), wrapped with error handling; pass `CancellationToken` as the last parameter on all async methods
- ScriptableObjects for config/graphs/profiles only — **not** for runtime mutable state
- One public type per file; file name matches type name exactly
- All user-reversible actions go through `CommandStack` — no direct mutation bypassing it
- Platform-dependent code behind interfaces declared in the owning subsystem's folder (no concrete platform classes at call sites)
- All serialized data carries a `schemaVersion` field; migrations only in `StorageMigrator`
- Cross-subsystem boundaries are enforced by convention (folder structure + code review) — there are no per-subsystem assemblies; all runtime code compiles into `_App.Runtime`
- Subsystem-specific code stays in its subsystem folder under `Scripts/`

### Strictly Forbidden

- `FindObjectOfType` / `GameObject.Find` at runtime — use DI
- `Singleton.Instance` pattern — use VContainer scopes
- `static` fields holding mutable runtime state
- Generic type name suffixes: `Manager`, `Handler`, `Utils`, `Helper`, `Controller`, `Processor`, `Service`
- `Resources.Load` — `_App` code must not use this; use prefab references via DI or `Content/` folder
- `MonoBehaviour` as a data container
- `Update()`-based polling where an event suffices
- Swallowing exceptions silently
- `#if UNITY_EDITOR` guards in runtime files — editor code goes in `Editor/` folders
