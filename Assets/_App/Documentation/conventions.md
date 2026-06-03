# VR Animation App — Project Conventions

---

## Folder Structure

```
Assets/
├── _App/                         ← ALL project code and owned assets
│   ├── Scripts/                  ← ALL runtime C# (_App.Runtime.asmdef)
│   │   ├── Core/                 ← Generic primitives only: EventBus.cs
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
└── UI/                           ← Subsystem-specific panels and views (most subsystems)
```

> **`SpatialUi` is the exception** — instead of a single `UI/` folder it uses role-based folders. See "SpatialUi Script Roles" below.

---

## SpatialUi Script Roles

`SpatialUi` scripts are classified by **what the code does** (not by name). The role determines suffix and folder:

| Role | Criteria (by signature) | Suffix | Folder |
|---|---|---|---|
| **Panel** | Root MonoBehaviour of a panel/module/overlay. Holds `[SerializeField]` control refs **and** logic: receives services via `[Inject]`/constructor and/or subscribes to `EventBus`. | `*Panel` | `Panels/` |
| **Sub-part** | A fixed, singular piece a *complex* panel was split into. Dumb widget — no DI, no `EventBus`; driven by its parent via `Bind`/`Set*`/`On*`. | `<Panel>Sub<Part>` | `Panels/` (flat) |
| **Element** | A widget **instantiated per list entry** (rows, cards). Dumb — `Bind`/`Set*` only. | `Item` / `Card` / `Row` / `Lane` | `Elements/` |
| **Behavior** | Adds one interaction/behavior to its GameObject (drag / scroll / toggle / anchor / input translation). Renders no domain data, owns no panel logic, is not a list row. | descriptive (`Handle`/`Toggle`/`Anchor`/`Sync`/`Input`) | `Behaviors/` |
| **Framework / Config** | Base class (`SpatialPanel`), orchestrator (`UiPanelOrchestrator`), registry SO (`PanelRegistry`), enums (`PanelId`/`PanelType`), config SOs. | as-is | `SpatialUi/` root |

Notes:
- A *simple* panel needs only its root `*Panel` script (it is both "brain" and "hands"). Sub-parts appear only when a panel grows large enough to split (today: `AnimatorPanel`).
- `Panels/` is **flat** — the `<Panel>` prefix groups a panel with its sub-parts (e.g. `AnimatorPanel`, `AnimatorToolbarView`, `AnimatorRulerView`).
- `Module` / `Overlay` are *placements* of a Panel, not script types — expressed by hierarchy, not the suffix.

### Region model (panel open/close)

All panel opening goes through one mechanism, not per-panel show/hide logic:

- **`PanelRegionRouter`** (Framework, `RootLifetimeScope` — app-lifetime, since the UserPanel + nav buttons live on the persistent XR rig) — `Open`/`Close`/`Toggle(moduleId)`, keeps **at most one open surface per region**, publishes `RegionChangedEvent`.
- **`IRegionSurface`** (`Show`/`Hide`/`IsOpen`) — the router speaks only this, never raw `SetActive`. **`RegionMember`** carries a module's `moduleId` and is the default surface (SetActive), delegating to a sibling `IRegionSurface` adapter when present (e.g. `FileBrowserPanel` over the SimpleFileBrowser modal).
- A module's **region** is its `NavBarConfig.ExclusiveGroup`; `NavBarConfig` (`IRegionConfig`) also supplies per-mode visibility and an optional **per-region default** (`IsRegionDefault`) that the router auto-reopens when its region empties (this is how the keyboard's nav-bar overlay restores on close).
- **Triggers live in `Behaviors/`** and call the router: `RegionNavButton` (button → `Toggle`, plus per-mode button visibility and open/closed brightness from `RegionChangedEvent`) — used by both nav buttons and the keyboard button. Code paths open imperatively too (e.g. `AssetBrowserPanel` → `router.Open("fileBrowser")`).
- Modules **self-describe** (a `RegionMember` with a `moduleId`); the scene scope discovers all `RegionMember`s (including inactive) in a build callback and registers them — hosts do not hold module lists.

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
- **No undo/redo subsystem** — the `CommandStack`/`ICommand` undo stack was removed; mutations apply directly
- **`PathProvider` as the single path-building authority** — no manual string concatenation for asset/scene paths
- **Interface-first for platform-dependent code** — wrappers behind interfaces in the owning subsystem's folder, not concrete platform classes at call sites
- **Per-scope `EventBus`** — Root-scope events never carry scene-specific data
- **Mode-specific services live in the mode scene's own scope** (e.g. VrEditing registers `AnimationAuthoring`/`AnimationClock`; Sandbox does not) — consumers read them through the root `SceneContext` façade and must guard on the specific service, not just `HasScene`
- **Subsystem-specific code stays in its subsystem folder** under `Scripts/`
- **All serialized data versioned** with a `schemaVersion` field — migrations handled inline at the deserialization boundary (e.g. `SceneSerializer.Deserialize`); there is no separate `StorageMigrator`

---

## VContainer Scope Rules

| Scope | Contents |
|---|---|
| `RootLifetimeScope` | App-lifetime singletons (`DontDestroyOnLoad` under `PersistentRoot`): `AppStorage`, `PathProvider`, `AnimationClock`, `EventBus`, `SceneContext`, `ModeOrchestrator`, `ISceneTransition`/`SceneTransitionRunner`, `PanelRegionRouter` |
| Mode scene scope | The loaded scene's own `LifetimeScope` (`MainMenu`/`VrEditing`/`Sandbox`): `SceneGraph`, `SelectionManager`, `AssetSpawner`; binds `SceneContext` via `SceneContextBinder` |

- Child scopes may depend on parent scope registrations; parent scopes must never depend on child scopes
- Exactly one mode scene is loaded at a time (`LoadSceneMode.Single`); its scope parents to the persistent root and is disposed with the scene. Scene services are surfaced app-wide via the root `SceneContext` (bound/cleared by `SceneContextBinder`, signalled by `SceneContextChangedEvent`)

---

## Assembly Definitions

The project uses exactly three `.asmdef` files:

| Assembly | Location | Contents |
|---|---|---|
| `_App.Runtime` | `Assets/_App/Scripts/_App.Runtime.asmdef` | All runtime gameplay C# — every subsystem and `Core/` |
| `_App.Editor` | `Assets/_App/Editor/` | All editor-only tooling; `Editor` platform constraint |
| `_App.Tests` | `Assets/_App/Tests/` | All unit and integration tests |

Subsystems are organizational folders within `_App.Runtime`, not separate assemblies. Cross-subsystem boundaries are enforced by convention (folder structure + code review), not by assembly references.
