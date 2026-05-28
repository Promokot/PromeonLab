# VR Animation App — Project Conventions

---

## Folder Structure

```
Assets/
├── _App/                         ← ALL project code and owned assets
│   ├── Scripts/                  ← ALL runtime C# (_App.Runtime.asmdef)
│   │   ├── Core/                 ← Generic primitives only: EventBus.cs, ICommand.cs
│   │   ├── Bootstrap/            ← LifetimeScopes, AppBootstrap, scene loader
│   │   └── {SubsystemName}/      ← one folder per subsystem
│   ├── Editor/                   ← Editor-only code (_App.Editor.asmdef), excluded from builds
│   ├── Tests/                    ← All tests (_App.Tests.asmdef)
│   │   └── {SubsystemName}/
│   ├── Content/                  ← Owned art/asset files
│   │   ├── Prefabs/              ← UI, Gizmos, Assets, Environment, XR
│   │   ├── ScriptableObjects/
│   │   ├── Materials/
│   │   ├── Models/
│   │   ├── Textures/
│   │   └── Shaders/
│   ├── Scenes/
│   ├── Documentation/
│   └── ThirdParty/               ← Vendored third-party packs (QuickOutline, SimpleFileBrowser, …)
```

### Per-Subsystem Layout

```
Scripts/{SubsystemName}/
├── {SubsystemName}.cs            ← Primary façade / entry point (if applicable)
├── Data/                         ← Subsystem-specific data structs, enums, SOs
├── Events/                       ← *Event structs published by this subsystem
└── UI/                           ← Subsystem-specific panels and views
```

---

## Naming — Files & Types

| Category | Convention | Example |
|---|---|---|
| Classes, structs | `PascalCase` | `SceneGraph`, `RigDefinition` |
| Interfaces | `I` + `PascalCase` | `IAnimationExporter`, `ISelectionManager` |
| ScriptableObjects | suffix by role: `Config`, `Profile`, `Graph` | `ModeTransitionGraph`, `BindingProfile` |
| Enums | `PascalCase` type, `PascalCase` members | `AppMode.VrEditing`, `GizmoMode.Rotate` |
| Event message types | suffix `Event` | `SceneOpenedEvent`, `MarkerDetectedEvent` |
| Abstract base classes | prefix `Base` | `BaseInteractor`, `BaseSpatialPanel` |
| Data containers | suffix by domain role | `ActionData`, `MappingData`, `ExportConfig` |

---

## Naming — Members

| Category | Convention | Example |
|---|---|---|
| Private fields | `_camelCase` | `_sceneGraph`, `_currentFrame` |
| Public properties | `PascalCase` | `CurrentFrame`, `IsPlaying` |
| Local variables | `camelCase` | `loadedAsset`, `nodeId` |
| Method parameters | `camelCase` | `assetId`, `targetBone` |
| Constants | `UPPER_SNAKE_CASE` | `MAX_KEYFRAME_COUNT` |
| Static readonly | `PascalCase` | `DefaultFps` |
| C# event instances (intra-subsystem) | `On` + `PascalCase` | `OnModeChanged`, `OnFrameChanged` |
| Coroutines | suffix `Routine` | `LoadSceneRoutine` |
| Async methods | suffix `Async` | `ImportAssetAsync` |

---

## Naming — Assets (Project Files)

| Category | Convention | Example |
|---|---|---|
| Prefabs | `PascalCase` | `SpatialPanel.prefab`, `BoneProxy.prefab` |
| ScriptableObject assets | `PascalCase` | `DefaultBindingProfile.asset` |
| Scenes | `PascalCase`, mode-prefixed where applicable | `Root.unity`, `VrEditing.unity` |
| Materials | `PascalCase` | `BoneGizmo.mat` |
| Shaders | `PascalCase` | `PassthroughOverlay.shader` |
| Textures | `PascalCase` + type suffix | `GripIcon_UI.png`, `BoneShape_Mesh.png` |
| Asmdef files | match assembly role | `_App.Runtime.asmdef`, `_App.Editor.asmdef`, `_App.Tests.asmdef` |

---

## Naming — Folders

- `PascalCase` always
- No spaces, no underscores (except `_App` — leading underscore for sort priority only)
- Subsystem folder names must match the subsystem name exactly

---

## Code Conventions

### Fields & Properties
- `[SerializeField] private` for inspector-exposed fields — never `public` fields
- Backing fields: `_camelCase`; auto-properties where no backing logic is needed
- No `[HideInInspector] public` — use `[SerializeField] private` + a property instead

### Namespaces
- No namespaces for runtime gameplay code
- `Editor` code: namespace `VrAnimApp.Editor`
- Third-party adapters/wrappers: namespace `VrAnimApp.Adapters`

### Events
- All cross-subsystem messages are `struct` types suffixed `Event`; live in the owning subsystem's `Events/` subfolder
- Published via the per-scope `EventBus` (`Publish<T>`/`Subscribe<T>`) — never via direct method calls across subsystem boundaries
- C# `event` delegates used only for intra-subsystem callbacks

### Async
- All I/O operations are `async`/`await`
- No `async void` except Unity lifecycle entry points (`Start`, `Awake`), wrapped with explicit error handling
- `CancellationToken` passed as the last parameter on all async methods

### ScriptableObjects
- Used for configuration, graphs, and profiles — not for runtime mutable state
- SO assets live in the subsystem's `Data/` folder

---

## Architectural Rules

### ✗ Avoid

- **Circular dependencies** between subsystems
- **Direct subsystem-to-subsystem calls** — communicate via events or injected interfaces only
- **Static mutable global state** — no `static` fields holding runtime data
- **Generic type names without domain context:** `Manager`, `Handler`, `Utils`, `Helper`, `Controller`, `Processor`, `Service`
- **`FindObjectOfType` / `GameObject.Find`** at runtime — use DI
- **Singleton `Instance` pattern** — use VContainer scopes instead
- **God objects** — types responsible for more than one cohesive concern
- **Concrete cross-subsystem dependencies** — depend on interfaces declared in the owning subsystem's folder
- **Magic strings** for `ChannelPath`, asset paths, scene names — use constants or typed wrappers
- **`Resources.Load`** outside of explicitly justified cases — use prefab references
- **MonoBehaviours as data containers** — data structs are plain C# classes/structs
- **`Update()`-based polling** where an event suffices
- **Catching and swallowing exceptions silently** — log or rethrow

### ✓ Follow

- **Prefab references over runtime lookup** — wire in inspector or via DI
- **Context-specific and domain-oriented type names** — named after the domain concept, not the pattern
- **Editor-only code inside `Editor/`** — no `#if UNITY_EDITOR` guards in runtime files
- **One public type per file** — file name matches the type name exactly
- **`CommandStack` for all user-reversible actions** — no direct mutation bypassing commands
- **`PathProvider` as the single path-building authority** — no manual string concatenation for asset/scene paths
- **Interface-first for platform-dependent code** — wrappers behind interfaces in the owning subsystem's folder, not concrete platform classes at call sites
- **Per-scope `EventBus`** — Root-scope events never carry scene-specific data
- **Feature code in `FeatureLifetimeScope`** — nothing mode-specific in `SceneLifetimeScope` or above
- **Subsystem-specific code stays in its subsystem folder** under `Scripts/`
- **All serialized data versioned** with a `schemaVersion` field — migrations handled exclusively in `StorageMigrator`

---

## VContainer Scope Rules

| Scope | Contents |
|---|---|
| `RootLifetimeScope` | App-lifetime singletons: `AppStorage`, `AssetImporter`, `PathProvider`, `AnimationClock` |
| `SceneLifetimeScope` | Scene-lifetime services: `ModeOrchestrator`, `SceneGraph`, `SelectionManager`, `UiPanelManager`, `CommandStack` |
| `FeatureLifetimeScope` | Mode-specific: `PlaybackController`, `RigRuntime`, `TrackRecorder` |

- Child scopes may depend on parent scope registrations; parent scopes must never depend on child scopes
- `FeatureLifetimeScope` is created and disposed by `ModeOrchestrator` on mode transitions

---

## Assembly Definitions

The project uses exactly three `.asmdef` files:

| Assembly | Location | Contents |
|---|---|---|
| `_App.Runtime` | `Assets/_App/Scripts/_App.Runtime.asmdef` | All runtime gameplay C# — every subsystem and `Core/` |
| `_App.Editor` | `Assets/_App/Editor/` | All editor-only tooling; `Editor` platform constraint |
| `_App.Tests` | `Assets/_App/Tests/` | All unit and integration tests |

Subsystems are organizational folders within `_App.Runtime`, not separate assemblies. Cross-subsystem boundaries are enforced by convention (folder structure + code review), not by assembly references.
