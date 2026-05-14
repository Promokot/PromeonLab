# Demo Development Plan — Design Spec

**Date:** 2026-05-14  
**Scope:** VR Animation App — XR Simulator Demo  
**Approach:** Vertical slice + milestone stubs (Hybrid B+C)

---

## Goal

Build a runnable demo of the full mini-cycle, operable via XR Simulator (no headset required):

```
MainMenu → Create/Open scene
  → VrEditing → Asset Browser → pick model (SimpleFileBrowser UI)
  → model in scene → RigBuilder panel → auto-rig from skeleton
  → select bone → set keyframe → Transport → Play
```

---

## Key Decisions

| Decision | Choice | Rationale |
|---|---|---|
| VR interaction layer | XR Interaction Toolkit (XRI) | Gives XR Origin, Ray/Near Interactor, XR Simulator support, UGUI integration out of the box |
| Model loading | Pre-bundled prefabs + SO catalog | No runtime FBX parsing; SimpleFileBrowser opens real but loads matching prefab. Runtime loader slot reserved in StorageCore. |
| AR subsystem | Coming Soon stub | Panel shown, no implementation |
| Export pipeline | Coming Soon stub | Button exists, opens "feature in development" dialog |
| Animation | Single ActionData per object, no NLA | NLA slot reserved in AnimationAuthoring; multi-strip composition is post-demo |
| Schema migration | Not implemented | No schemaVersion handling in demo; StorageMigrator is a no-op stub |
| Thumbnails | Not generated | ThumbnailService stub; asset list shows icon by type |

---

## What Is Real vs Stubbed

### Real (fully implemented)
- Project folder structure + all `.asmdef` files
- VContainer Root → Scene → Feature scope chain
- XRI: XR Origin, Ray Interactor, Near Interactor, XR UI Input Module
- `ModeOrchestrator`: MainMenu ↔ VrEditing transitions
- `SpatialUi`: ToolbarPanel (body-locked), panel show/hide per mode, billboard
- `StorageCore`: PathProvider, scene JSON save/load, SO-catalog-based asset loading
- `AssetBrowser`: simple list panel, SimpleFileBrowser integration, drag-to-scene
- `SceneComposition`: SceneGraph, SelectionManager, PropertyPanel, basic CommandStack
- `VrInteraction`: XRI-based object selection, translate/rotate/scale gizmos
- `RigBuilder`: RigDefinition from SkinnedMesh skeleton, BoneRenderer, TwoBoneIK, RigRuntime
- `AnimationAuthoring`: AnimationClock, single ActionData, keyframe recording for bone transforms
- `AnimationPlayback`: transport (play/pause/stop/loop/scrub), AnimationEvaluator → PropertyApplicator
- `ErrorHandling`: ErrorDispatcher, Warning/Error/Critical levels, console logging

### Stubbed
- `EnvironmentMapping` — Coming Soon panel
- `ExportPipeline` — Coming Soon dialog
- NLA / NlaComposer — slot exists, not wired
- ThumbnailService — returns null, UI shows type icon
- StorageMigrator — no-op
- InputBindings cheatsheet panel — panel exists, content empty
- `SceneEnvironmentLinker` (AR ↔ scene link) — no-op
- Material slot management — slots visible in PropertyPanel, not editable

---

## Architecture

### VContainer Scope Chain

```
RootLifetimeScope  (app lifetime)
  └─ AppStorage, AssetImporter, PathProvider, AnimationClock

SceneLifetimeScope  (VrEditing scene loaded)
  └─ ModeOrchestrator, SceneGraph, SelectionManager,
     UiPanelManager, CommandStack

FeatureLifetimeScope  (active mode, created by ModeOrchestrator)
  └─ PlaybackController, RigRuntime, TrackRecorder
```

### Scene Setup

| Unity Scene | Contents |
|---|---|
| `Bootstrap.unity` | RootLifetimeScope, loads MainMenu additively |
| `MainMenu.unity` | SceneLifetimeScope (MainMenu), ScenePickerView, AR stub entry |
| `VrEditing.unity` | SceneLifetimeScope (VrEditing), XR Origin, all tool panels |

### XRI Interaction Layers

```
Layer: SceneObjects   — MeshObjects in SceneGraph
Layer: UiPanels       — SpatialPanel canvas colliders
Layer: GizmoHandles   — translate/rotate/scale handles
Layer: BoneProxies    — RigBuilder bone selection
```

Near Interactor (~15 cm sphere) takes priority over Ray Interactor on SceneObjects and BoneProxies.

### Event Bus (per-scope, MessagePipe or VContainer built-in)

| Event | Source → Subscribers |
|---|---|
| `SceneOpenedEvent` | AppStorage → SceneGraph, AssetBrowser |
| `SceneModifiedEvent` | SceneGraph, AnimationAuthoring → UnsavedChangesGuard |
| `AssetImportedEvent` | AppStorage → AssetBrowser |
| `SelectionChangedEvent` | SelectionManager → PropertyPanel, GizmoController |
| `ModeChangedEvent` | ModeOrchestrator → UiPanelManager, FeatureLifetimeScope |
| `FrameChangedEvent` | AnimationClock → AnimationEvaluator, TrackRecorder |
| `PlaybackStateChangedEvent` | PlaybackController → ToolbarPanel transport UI |

### Model Loading Flow (demo-specific)

```
SimpleFileBrowser.ShowLoadDialog()
  → user picks file path
  → AssetImporter.ImportAsync(path, ct)
      → match filename against DemoAssetCatalog SO
      → Instantiate(prefab) into scene
      → register in AppStorage cache
      → publish AssetImportedEvent
```

`DemoAssetCatalog` ScriptableObject:
```csharp
[Serializable]
struct DemoAssetEntry {
    string fileName;       // e.g. "Mannequin.fbx"
    AssetType type;
    GameObject prefab;
    Sprite icon;
}
```

---

## Phase Breakdown

### Phase 1 — Foundation
**Deliverable:** XR Simulator shows empty VR scene; VContainer scopes boot without errors.

- Add `com.unity.xr.interaction.toolkit` to manifest
- Create full folder structure (`_App`, `_Shared`, `Subsystems/*`) + all `.asmdef` files
- `Bootstrap.unity` + `MainMenu.unity` + `VrEditing.unity`
- Root/Scene/Feature `LifetimeScope` shells (registered but empty)
- XR Origin prefab (Head + Left/Right XRI controllers)
- XR Simulator confirmed working (mouse/keyboard controller simulation)

### Phase 2 — SpatialUi + Navigation
**Deliverable:** ToolbarPanel floats in front of player; ray interactor clicks UGUI buttons; mode transition MainMenu ↔ VrEditing fires.

- `UiPanelManager`, `SpatialPanel` (BodyLocked / WorldFixed / Free)
- `ToolbarPanel` prefab (body-locked, always visible in VrEditing)
- XR UI Input Module wired to UGUI canvases
- `ModeOrchestrator` with `ModeTransitionGraph` SO; wires up FeatureLifetimeScope create/dispose
- `PanelRegistry` SO with default panel positions

### Phase 3 — MainMenu + StorageCore
**Deliverable:** Can create and open a named scene; scene persists to JSON on disk.

- `PathProvider` (all paths from `persistentDataPath/scenes/{SceneId}/`)
- `ScenePickerView` (list scenes, create/open/delete)
- `AppStorage` with in-memory cache; `SceneSerializer` (save/load `scene.json`)
- `UnsavedChangesGuard` listening to `SceneModifiedEvent`

### Phase 4 — Asset Browser + Model Loading
**Deliverable:** SimpleFileBrowser opens; selecting a name loads a pre-bundled skinned mesh into the VR scene.

- `DemoAssetCatalog` SO + 2–3 imported FBX prefabs in `Assets/_App/DemoAssets/`
- `AssetImporter` demo implementation (catalog lookup → Instantiate)
- `AssetBrowserPanel` (simple vertical list, type filter, import button)
- `DropPayload` → `SceneGraph.AddNode()`

### Phase 5 — SceneComposition + VrInteraction
**Deliverable:** Select objects with ray, move with gizmos, see transforms in PropertyPanel; Ctrl+Z undoes last move.

- `SceneGraph`, `SceneNode`, `SelectionManager`
- XRI-based `SelectionInteractor` (ray click → `SelectionChangedEvent`)
- `GizmoController` (translate/rotate/scale handles on selected object)
- `DirectManipulator` (grab + move via ray/near)
- `PropertyPanel` (position/rotation/scale, read/write)
- `CommandStack` with `TransformCommand` (undo/redo)

### Phase 6 — RigBuilder
**Deliverable:** Select skinned mesh → "Build Rig" → bones appear as selectable proxies; IK chain created between two bones.

- `RigDefinition` + `RigSerializer`
- `BoneInspector` panel (bone list from SkinnedMeshRenderer)
- `BoneRenderer` via Animation Rigging package
- `BoneProxy` prefab (selectable sphere per bone, on BoneProxies layer)
- `IkSetupWizard` (select root → end → confirm → adds `TwoBoneIKConstraint`)
- `RigRuntime` (applies `RigDefinition` constraints via Animation Rigging RigBuilder)

### Phase 7 — Animation Authoring + Playback
**Deliverable:** Select bone → press "Set Key" → scrub timeline → press Play → bone animates.

- `AnimationClock` (current frame, FPS=30, frame range)
- `TrackRecorder` (captures bone transform at current frame → writes `Keyframe` to `ActionData`)
- `KeyframeEditor` panel (horizontal scrubber, frame counter, "Set Key" button)
- `AnimationEvaluator` (interpolates `ActionData`, Linear/Stepped)
- `PropertyApplicator` (writes evaluated values to bone Transform)
- Transport controls in `ToolbarPanel` (play/pause/stop/loop, speed 0.25x–2x)

### Phase 8 — Integration, Stubs, Polish
**Deliverable:** Full golden path runs without crashes; AR and Export show Coming Soon; demo is presentable.

- `EnvironmentMapping` Coming Soon panel (button in MainMenu)
- `ExportPipeline` Coming Soon dialog (button in ToolbarPanel)
- `ErrorDispatcher` wired to all subsystems; errors surface as toast notifications
- `InputBindings` cheatsheet panel (XR Simulator key bindings listed)
- Full golden path walkthrough + bug fixes
- `DemoAssetCatalog` populated with at least 2 usable test models with skeletons

---

## Error Handling

All `async` methods wrapped with try-catch delegating to `ErrorDispatcher`. Demo uses Warning and Error levels. Critical triggers a modal with "Return to Main Menu". No crash reporter.

---

## Testing

Each subsystem's `Tests/` folder targets Edit Mode tests for pure C# logic:
- `PathProvider` path construction
- `SceneSerializer` round-trip (serialize → deserialize → compare)
- `AnimationEvaluator` interpolation (Linear, Stepped, boundary frames)

Play Mode and XR Simulator walkthroughs are the primary verification method for integration.

---

## Packages to Add

| Package | ID |
|---|---|
| XR Interaction Toolkit | `com.unity.xr.interaction.toolkit` |
| MessagePipe | `jp.hadashikick.messagepipe` — отдельный пакет от автора VContainer; evaluate в Phase 1, может быть заменён простыми C# events для demo-скоупа |
