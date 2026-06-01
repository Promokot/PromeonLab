# SpatialUi + Animation Script Reorg — Design

> Status: design (awaiting review). Scope **A only** — pure rename + relocation, zero behavior change. Behavioral reworks are deferred to a separate spec (**B**, see Follow-ups).

**Goal:** Resolve the overloaded `View` suffix in `SpatialUi` and give every UI script a role-based home, plus merge the two animation script folders. No runtime behavior changes.

**Why now:** `View` currently labels two unrelated roles (smart controllers *and* dumb widgets); smart controllers also wear `Panel`/`Module`; dumb widgets are split across `Views/` and `Elements/`. The result is that the folder/suffix tells you nothing reliable about what a script does. This is the long-pending "document UI naming" task — here we both define the rule *and* make the code obey it.

**Constraint:** Single runtime assembly (`_App.Runtime`), so moving `.cs` between folders cannot break compilation. Class+file renames keep the `.meta` GUID, so prefab/scene `m_Script` references survive automatically. The only code that needs editing is **C# type references in other `.cs` files** (no namespaces → type names are global).

---

## Role taxonomy (the criteria)

A script's role is determined by what it does in code, not by its current name:

| Role | Criteria (by signature) | Suffix | Folder |
|---|---|---|---|
| **Panel** | Root MonoBehaviour of a panel/module/overlay. Holds `[SerializeField]` control refs **and** logic: receives services via `[Inject]`/constructor and/or subscribes to `EventBus`; coordinates the domain. | `*Panel` | `Panels/` |
| **Sub-part** | A fixed, singular piece a *complex* panel was split into. Dumb widget: no DI, no EventBus; driven by its parent via `Bind`/`Set*`/`On*`. | `<Panel>Sub<Part>` | `Panels/` (flat) |
| **Element** | A widget **instantiated per list entry** (rows, cards). Dumb: `Bind`/`Set*` only. | descriptive (`Item`/`Card`/`Row`/`Lane`) | `Elements/` |
| **Behavior** | Adds one interaction/behavior to its GameObject (drag/scroll/toggle/anchor/input translation). Does not render domain data, does not own panel logic, is not a list row. | descriptive (`Handle`/`Toggle`/`Anchor`/`Sync`/`Input`) | `Behaviors/` |
| **Framework / Config** | Base classes, the panel orchestrator, the registry SO, enums, config SOs. | as-is | `SpatialUi/` root |

Notes:
- A *simple* panel needs only its root Panel script (it is both "brain" and "hands"). Sub-parts appear only when a panel is large enough to split (today: Animator only).
- `Panels/` is **flat** — no per-panel subfolders. The `<Panel>` prefix sorts a panel and its sub-parts together (e.g. `AnimatorPanel`, `AnimatorSubToolbar`, `AnimatorSubRuler`).
- `Module`/`Overlay` are **not** script types — they are *placements* of a Panel. Placement is expressed by hierarchy, not by the suffix.

---

## Target structure

```
SpatialUi/
├── (root — shared infrastructure only)
│   ├── SpatialPanel.cs            base class (untouched)
│   ├── SpatialPanelDetachable.cs  ← DetachablePanel (rename only; logic adapt deferred to B)
│   ├── UiPanelOrchestrator.cs     ← UiPanelManager
│   ├── PanelRegistry.cs           SO (name kept)
│   ├── PanelId.cs / PanelType.cs  enums
│   ├── AnimatorPanelConfig.cs / NavBarConfig.cs   config SOs
│   └── VrKeyboard.cs              moved here, NOT renamed (reclassification deferred to B)
├── Panels/
│   ├── AnimatorPanel.cs           ← AnimatorPanelView
│   ├── AnimatorSubToolbar.cs      ← AnimatorToolbarView
│   ├── AnimatorSubTransport.cs    ← AnimatorTransportView
│   ├── AnimatorSubEmptyState.cs   ← AnimatorEmptyStateView
│   ├── AnimatorSubRuler.cs        ← TimelineRulerView
│   ├── AnimatorSubPlayhead.cs     ← TimelinePlayheadView
│   ├── AnimatorSubLanes.cs        ← TimelineLanesView
│   ├── OutlinerPanel.cs           ← SceneOutlinerView
│   ├── InspectorPanel.cs          ← SceneInspectorView
│   ├── SettingsPanel.cs           ← SettingsModule
│   ├── AssetBrowserPanel.cs       ← AssetBrowserModule
│   ├── IkWizardPanel.cs           ← IkSetupWizard (rename only; import rework deferred to B)
│   ├── BoneInspectorPanel.cs      (move only)
│   ├── PropertyPanel.cs           (move only)
│   ├── MainMenuPanel.cs           (move only)
│   ├── ScenePickerPanel.cs        (move only)
│   └── UserPanel.cs               (move only)
├── Elements/
│   ├── OutlinerItem.cs            (move only)
│   ├── RigOutlinerItem.cs         (move only; : OutlinerItem)
│   ├── SceneItem.cs               (move only)
│   ├── LabAssetCard.cs            (move only)
│   ├── TrackRow.cs                ← TrackRowView
│   └── TimelineLane.cs            ← TimelineLaneView
├── Behaviors/
│   ├── PanelDragHandle.cs         (move only)
│   ├── DetachablePanelDragHandle.cs  (move; update field type → SpatialPanelDetachable)
│   ├── TimelineScrollSync.cs      (move only)
│   ├── TimelineScrubInput.cs      ← TimelineInputHandler (suffix `Handler` is forbidden)
│   ├── UserPanelOpener.cs         (move only)
│   ├── UserPanelKeyboardToggle.cs (move only)
│   └── FileBrowserVrAnchor.cs     (move only)
└── Events/                        (unchanged)
    ├── KeyboardFocusEvent.cs
    ├── PanelClosedEvent.cs
    ├── PanelDetachedEvent.cs
    └── PanelLinkedEvent.cs
```

### Animation folder merge

```
Scripts/Animation/                 ← renamed from Scripts/AnimationAuthoring/
  AnimationAuthoring.cs            (class name unchanged)
  AnimationPlayback.cs             ← moved in from Scripts/AnimationPlayback/
  AnimationClock.cs, ActionContainer.cs, … (all 16 unchanged)
  Events/                          (incl. PlaybackStateChangedEvent — already lived here)
Tests/Animation/                   ← renamed from Tests/AnimationAuthoring/
```

`Scripts/AnimationPlayback/` is deleted once empty. **Class names are not changed** (`AnimationAuthoring`, `AnimationPlayback`, `AnimationClock` stay) → zero churn at `[Inject]` sites.

---

## Rename fan-out (type references to update)

Renames are GUID-safe for prefabs/scenes; these are the **code** references that must be updated:

| Renamed type | Referencing files to update |
|---|---|
| `UiPanelManager` → `UiPanelOrchestrator` | `Bootstrap/VrEditingSceneScope.cs`, `Bootstrap/SandboxSceneScope.cs` (DI registration) |
| `DetachablePanel` → `SpatialPanelDetachable` | `Panels/UserPanel.cs`, `Behaviors/DetachablePanelDragHandle.cs` |
| `TimelineInputHandler` → `TimelineScrubInput` | `Panels/AnimatorPanel.cs` (`_timelineInput` field) |
| Animator sub-parts (`AnimatorToolbarView`→`AnimatorSubToolbar`, etc.) | `Panels/AnimatorPanel.cs` (serialized fields + `AnimatorSubEmptyState.State` enum access) |
| `TrackRowView` → `TrackRow`, `TimelineLaneView` → `TimelineLane` | `Panels/AnimatorPanel.cs` (`_trackRowPrefab`, `_rowPool`, lane access) |
| Presenters renamed (`SceneOutlinerView`→`OutlinerPanel`, etc.) | grep each type before applying — most are referenced only via prefab + DI, not by type in other `.cs` |

The implementation plan must grep each renamed type across `Assets/` and update every `.cs` hit. Prefab/scene hits are GUID-based and need no edit.

---

## Migration approach & verification

1. Create folders `Panels/`, `Elements/`, `Behaviors/` (Events/ exists).
2. For each rename: rename file + class together (GUID preserved), then update the type references listed above.
3. For move-only files: relocate (GUID preserved).
4. Animation: rename folder `AnimationAuthoring/`→`Animation/`, move `AnimationPlayback.cs` in, delete empty folder, rename `Tests/AnimationAuthoring/`→`Tests/Animation/`.
5. Verify after each batch: compile clean (`read_console`, no `CS####`), Test Runner green against the 143/150 baseline (7 known pre-existing failures unrelated to UI/animation), open `MainMenu`/`VrEditing`/`Sandbox`/`AnimatorPanelSandbox` scenes and confirm no "missing script" warnings.

Moves are done GUID-safe via `AssetDatabase` (Unity MCP `manage_asset move`); return strings are unreliable in this repo, so verify real state with `Glob`/console (see project memory).

---

## Documentation updates (part of this work)

- **`conventions.md`** — add the SpatialUi role taxonomy table (Panel / Sub / Element / Behavior + criteria). This is the deliverable of the original "document UI naming" task.
- **`CLAUDE.md`** — merge the `AnimationAuthoring` + `AnimationPlayback` subsystem rows into a single `Animation` row; refresh the SpatialUi description.
- **`STRUCTURE.md`** — regenerate the `SpatialUi/` and `Animation/` subtrees.

---

## Out of scope — deferred to spec B (behavioral rework)

These change runtime behavior and need their own design pass; recorded here so they are not lost:

1. **Overlays → modules** — remove the "overlay" mechanism and give those surfaces module-like behavior (replace a content region). Affects `VrKeyboard`, the keyboard toggle, context menu, file browser.
2. **`VrKeyboard` reclassification + rename** — it is `RootLifetimeScope`-registered (app-lifetime, not a scene panel) and referenced by the third-party `KeyboardButtonController`. Its correct role is exactly what (1) decides. In A it is only **moved to the `SpatialUi` root** (GUID-safe, no type-name change → no ThirdParty/DI edits) so `Elements/` stays pure; the rename waits for B.
3. **`SpatialPanelDetachable` logic adaptation** — adapt the detachable-panel chrome to the unified model from (1).
4. **`IkWizardPanel` import rework** — the IK setup flow needs a larger rework tied to import.
5. **`FileBrowserVrAnchor` `FindAnyObjectByType` debt** — forbidden lookup; replace with DI when its owning panel is reworked.

Deliberately *not* changed in A: `SpatialPanel` (base, name kept), `PanelRegistry` (SO name kept), config SO locations.

---

## Risks

- **ThirdParty coupling:** avoided in A by deferring the `VrKeyboard` rename (the only rename that would touch `ThirdParty/Keyboard Package`).
- **Two types per file:** `TrackRowView.cs` also declares `enum TrackRowKind`. On rename to `TrackRow.cs` the enum stays in the same file (pre-existing one-type-per-file deviation); optionally split into `TrackRowKind.cs`. Low priority.
- **Missed type reference:** mitigated by grepping each renamed type and by the compile/console verification gate after every batch.
```