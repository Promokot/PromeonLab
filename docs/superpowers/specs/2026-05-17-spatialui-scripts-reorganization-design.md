# SpatialUi Scripts Reorganization — Design

**Goal:** Replace the flat `UI_Scripts/` folder and root-level script mix in `SpatialUi/` with a single `Scripts/` folder containing three semantic subfolders: `Panels/`, `Views/`, `Elements/`.

---

## Problem

The `SpatialUi/` subsystem currently has two locations for scripts:

- Root `SpatialUi/` — base classes and system-level scripts (`SpatialPanel`, `UiPanelManager`, `DetachablePanel`)
- `UI_Scripts/` — everything else, 22 files with no internal grouping

Scripts in both locations share the `*Panel` suffix, making it unclear which layer a file belongs to. There is no documented convention for when to use `Panel` vs `View` vs `Module` vs other suffixes.

## Design

### Folder Structure

```
SpatialUi/
├── Data/              (unchanged — ScriptableObjects and data types)
└── Scripts/
    ├── Panels/        (top-level UI windows and their infrastructure)
    ├── Views/         (content components that live inside panels)
    └── Elements/      (atomic reusable UI pieces)
```

### Suffix Semantics

| Suffix | Folder | Meaning |
|---|---|---|
| `*Panel` | `Panels/` | A self-contained UI window. Manages its own visibility, world position, and drag. Inherits `SpatialPanel`. |
| `*Module` | `Panels/` | An embedded feature block that lives inside a panel and occupies a content region. Has `IsVisible` + `Toggle()`. Not a window — no drag, no world position. |
| `*Wizard` | `Panels/` | A multi-step guided flow. Conceptually a module with sequential steps. |
| `*View` | `Views/` | A read-only content display component. Subscribes to domain state (SceneGraph, SelectionManager) and renders it into UI elements. Can be hosted in different panels. |
| `*Row` / `*Item` | `Elements/` | A single entry in a scrollable list. Spawned dynamically by a View. |
| `*Card` | `Elements/` | A single entry in a gallery or grid. Spawned dynamically. |
| `*Handle` | `Elements/` | Handles pointer/drag events and delegates movement to a panel. |
| No suffix (misc) | `Elements/` | Utility scripts that do not fit the above categories (opener triggers, keyboard toggle, VR keyboard, anchor bridges). |

### File Mapping

**`Scripts/Panels/`** (11 files):

| File | From |
|---|---|
| `SpatialPanel.cs` | `SpatialUi/` (root) |
| `DetachablePanel.cs` | `SpatialUi/` (root) |
| `UiPanelManager.cs` | `SpatialUi/` (root) |
| `UserPanel.cs` | `UI_Scripts/` |
| `MainMenuPanel.cs` | `UI_Scripts/` |
| `ScenePickerPanel.cs` | `UI_Scripts/` |
| `BoneInspectorPanel.cs` | `UI_Scripts/` |
| `PropertyPanel.cs` | `UI_Scripts/` |
| `SettingsModule.cs` | `UI_Scripts/` |
| `AssetBrowserModule.cs` | `UI_Scripts/` |
| `IkSetupWizard.cs` | `UI_Scripts/` |

**`Scripts/Views/`** (3 files):

| File | From |
|---|---|
| `SceneOutlinerView.cs` | `UI_Scripts/` |
| `SceneInspectorView.cs` | `UI_Scripts/` |
| `AssetPropertiesView.cs` | `UI_Scripts/` |

**`Scripts/Elements/`** (10 files):

| File | From |
|---|---|
| `OutlinerItem.cs` | `UI_Scripts/` |
| `SceneOutlinerRow.cs` | `UI_Scripts/` |
| `SceneItem.cs` | `UI_Scripts/` |
| `LabAssetCard.cs` | `UI_Scripts/` |
| `PanelDragHandle.cs` | `UI_Scripts/` |
| `DetachablePanelDragHandle.cs` | `UI_Scripts/` |
| `UserPanelOpener.cs` | `UI_Scripts/` |
| `UserPanelKeyboardToggle.cs` | `UI_Scripts/` |
| `VrKeyboard.cs` | `UI_Scripts/` |
| `FileBrowserVrAnchor.cs` | `UI_Scripts/` |

**Deleted:**

| File | Reason |
|---|---|
| `ToolbarPanel.cs` | Redundant — replaced by the NavBar system. Marked for deletion in previous session; delete if still present. |

### Migration Approach

Move each `.cs` file alongside its `.meta` file via the filesystem (`Move-Item`). Unity GUIDs live in `.meta` files — as long as both files travel together, no script references in prefabs or scenes are broken. After all moves, `Assets > Refresh` in Unity Editor triggers recompilation with the new paths.

No class renames. No `using` changes (no namespaces in runtime code).

## Success Criteria

- `SpatialUi/UI_Scripts/` folder no longer exists
- All scripts reachable under `SpatialUi/Scripts/{Panels,Views,Elements}/`
- Unity compiles without errors after refresh
- No missing script references on prefabs
