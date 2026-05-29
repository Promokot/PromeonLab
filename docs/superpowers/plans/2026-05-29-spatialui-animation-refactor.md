# SpatialUi + Animation Script Reorg Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply the role-based naming/folder scheme to `Assets/_App/Scripts/SpatialUi` and merge `AnimationAuthoring/` + `AnimationPlayback/` into `Animation/` — pure rename + relocation, zero runtime behavior change.

**Architecture:** Single runtime assembly (`_App.Runtime`), so moving `.cs` between folders never breaks compilation. File+class renames keep the `.meta` GUID, so prefab/scene `m_Script` references survive with no prefab edits. The only edits beyond renames are **C# type references in other `.cs` files** (no namespaces → type names are global). Work proceeds moves-first (compile-safe) then renames in dependency order, each task ending compile-green.

**Tech Stack:** Unity 6000.3.7f1, C# (no namespaces), VContainer DI, Unity MCP bridge for GUID-safe asset ops.

---

## Conventions for every task

**This is a behavior-preserving refactor. No new unit tests are written (YAGNI).** The regression gate replaces TDD's test-first steps. After each task run this **VERIFY GATE**:

1. `mcp__unityMCP__refresh_unity` then `mcp__unityMCP__read_console` (action=`get`, types=`["error"]`, filter_text=`CS`) → **expect zero `CS####` entries**. Ignore `MCP-FOR-UNITY: Client handler exited` / `disposed object` lines — those are bridge churn, not compile errors.
2. `mcp__unityMCP__run_tests` (mode=`EditMode`) → **expect 143 passed / 7 failed**. The 7 are pre-existing and unrelated: `PathProviderTests` ×4, `RingRotateStrategyTests` ×2, `PromeonProxyRigBuilderTests` ×1. Any *new* failure = stop and fix.
3. (Final task only) Load each scene via `mcp__unityMCP__manage_scene` (`MainMenu`, `VrEditing`, `Sandbox`, `Scenes/_Sandbox/AnimatorPanelSandbox`) → `read_console` shows no "missing script" / `MissingReferenceException`.

**Asset ops:** Use Unity MCP `manage_asset` for all moves/renames (GUID-safe via `AssetDatabase.MoveAsset`). All `path`/`destination` are **relative to `Assets/`**. The `.meta` travels automatically — never move/rename a `.meta` by hand. **`manage_asset move`/`rename` frequently return `success:false` even when they succeed** — do NOT trust the return string; verify with `Glob` on the new path. `manage_asset delete` returns truthful results.

**Class renames:** A rename = (a) edit the `.cs` to rename the class (filename must match class name), (b) rename/move the file, (c) update every referencing `.cs`. Do all three before the VERIFY GATE so compilation is green when checked. After renaming a type, `Grep` the old type name across `Assets/_App` to confirm zero stray code references remain (prefab/scene `.meta`/`.unity` GUID hits are expected and need no edit).

**Git:** Git is user-managed in this repo. **Do NOT run `git add`/`git commit`/any git command.** End each task at the VERIFY GATE; the user commits at checkpoints they choose.

---

## File Structure (full target)

`SpatialUi/` root keeps only infrastructure: `SpatialPanel.cs`, `SpatialPanelDetachable.cs` (←`DetachablePanel`), `UiPanelOrchestrator.cs` (←`UiPanelManager`), `PanelRegistry.cs`, `PanelId.cs`, `PanelType.cs`, `AnimatorPanelConfig.cs`, `NavBarConfig.cs`, `VrKeyboard.cs` (moved in, not renamed).

- `Panels/` — `AnimatorPanel`, `AnimatorSubToolbar`, `AnimatorSubTransport`, `AnimatorSubEmptyState`, `AnimatorSubRuler`, `AnimatorSubPlayhead`, `AnimatorSubLanes`, `OutlinerPanel`, `InspectorPanel`, `SettingsPanel`, `AssetBrowserPanel`, `IkWizardPanel`, `BoneInspectorPanel`, `PropertyPanel`, `MainMenuPanel`, `ScenePickerPanel`, `UserPanel`
- `Elements/` — `OutlinerItem`, `RigOutlinerItem`, `SceneItem`, `LabAssetCard`, `TrackRow`, `TimelineLane`
- `Behaviors/` — `PanelDragHandle`, `DetachablePanelDragHandle`, `TimelineScrollSync`, `TimelineScrubInput` (←`TimelineInputHandler`), `UserPanelOpener`, `UserPanelKeyboardToggle`, `FileBrowserVrAnchor`
- `Events/` — unchanged
- `Scripts/Animation/` — merged from `AnimationAuthoring/` + `AnimationPlayback/`; `Tests/Animation/` ← `Tests/AnimationAuthoring/`

---

## Task 1: Create target folders

**Files:**
- Create: `_App/Scripts/SpatialUi/Panels/`, `_App/Scripts/SpatialUi/Elements/` (exists), `_App/Scripts/SpatialUi/Behaviors/`

- [ ] **Step 1: Create `Panels/` and `Behaviors/`**

```
mcp__unityMCP__manage_asset action=create_folder path="_App/Scripts/SpatialUi/Panels"
mcp__unityMCP__manage_asset action=create_folder path="_App/Scripts/SpatialUi/Behaviors"
```
(`Elements/` and `Events/` already exist.)

- [ ] **Step 2: Verify folders exist**

`Glob` pattern `Assets/_App/Scripts/SpatialUi/Panels/**` and `.../Behaviors/**` (empty match is fine; confirm folders created via `manage_asset get_info` if Glob shows nothing). Folder creation has no compile impact — skip the test run, just confirm the two `.meta` folders exist.

---

## Task 2: Merge animation folders

**Files:**
- Move: `_App/Scripts/AnimationAuthoring/` → `_App/Scripts/Animation/`
- Move: `_App/Scripts/AnimationPlayback/AnimationPlayback.cs` → `_App/Scripts/Animation/AnimationPlayback.cs`
- Delete: `_App/Scripts/AnimationPlayback/` (after empty)
- Move: `_App/Tests/AnimationAuthoring/` → `_App/Tests/Animation/`

No class is renamed → no reference edits, no `[Inject]` churn.

- [ ] **Step 1: Rename the authoring folder to `Animation/`**

```
mcp__unityMCP__manage_asset action=move path="_App/Scripts/AnimationAuthoring" destination="_App/Scripts/Animation"
```
Verify with `Glob Assets/_App/Scripts/Animation/**/*.cs` → expect the 16 files incl. `AnimationAuthoring.cs`, `AnimationClock.cs`, `Events/`.

- [ ] **Step 2: Move `AnimationPlayback.cs` into `Animation/`**

```
mcp__unityMCP__manage_asset action=move path="_App/Scripts/AnimationPlayback/AnimationPlayback.cs" destination="_App/Scripts/Animation/AnimationPlayback.cs"
```
Verify `Glob Assets/_App/Scripts/Animation/AnimationPlayback.cs` exists.

- [ ] **Step 3: Delete the now-empty `AnimationPlayback/` folder**

```
mcp__unityMCP__manage_asset action=delete path="_App/Scripts/AnimationPlayback"
```
Verify `Glob Assets/_App/Scripts/AnimationPlayback/**` → no matches.

- [ ] **Step 4: Rename the test folder to `Animation/`**

```
mcp__unityMCP__manage_asset action=move path="_App/Tests/AnimationAuthoring" destination="_App/Tests/Animation"
```
Verify `Glob Assets/_App/Tests/Animation/*.cs`.

- [ ] **Step 5: VERIFY GATE** (compile + tests). Class names are unchanged so this must be green with no edits.

---

## Task 3: Move-only relocations (no renames)

These files keep their class names; only their folder changes. Moves never break compilation, so this whole task is one batch + one gate.

**Files (move `path` → `destination`, all under `_App/Scripts/SpatialUi/`):**

- [ ] **Step 1: Move behaviors into `Behaviors/`**

```
mcp__unityMCP__manage_asset action=move path="_App/Scripts/SpatialUi/Elements/PanelDragHandle.cs"            destination="_App/Scripts/SpatialUi/Behaviors/PanelDragHandle.cs"
mcp__unityMCP__manage_asset action=move path="_App/Scripts/SpatialUi/Elements/DetachablePanelDragHandle.cs"  destination="_App/Scripts/SpatialUi/Behaviors/DetachablePanelDragHandle.cs"
mcp__unityMCP__manage_asset action=move path="_App/Scripts/SpatialUi/Elements/TimelineScrollSync.cs"         destination="_App/Scripts/SpatialUi/Behaviors/TimelineScrollSync.cs"
mcp__unityMCP__manage_asset action=move path="_App/Scripts/SpatialUi/Elements/UserPanelOpener.cs"            destination="_App/Scripts/SpatialUi/Behaviors/UserPanelOpener.cs"
mcp__unityMCP__manage_asset action=move path="_App/Scripts/SpatialUi/Elements/UserPanelKeyboardToggle.cs"    destination="_App/Scripts/SpatialUi/Behaviors/UserPanelKeyboardToggle.cs"
mcp__unityMCP__manage_asset action=move path="_App/Scripts/SpatialUi/Elements/FileBrowserVrAnchor.cs"        destination="_App/Scripts/SpatialUi/Behaviors/FileBrowserVrAnchor.cs"
```

- [ ] **Step 2: Move keep-name panels into `Panels/`**

```
mcp__unityMCP__manage_asset action=move path="_App/Scripts/SpatialUi/BoneInspectorPanel.cs"  destination="_App/Scripts/SpatialUi/Panels/BoneInspectorPanel.cs"
mcp__unityMCP__manage_asset action=move path="_App/Scripts/SpatialUi/PropertyPanel.cs"        destination="_App/Scripts/SpatialUi/Panels/PropertyPanel.cs"
mcp__unityMCP__manage_asset action=move path="_App/Scripts/SpatialUi/MainMenuPanel.cs"        destination="_App/Scripts/SpatialUi/Panels/MainMenuPanel.cs"
mcp__unityMCP__manage_asset action=move path="_App/Scripts/SpatialUi/ScenePickerPanel.cs"     destination="_App/Scripts/SpatialUi/Panels/ScenePickerPanel.cs"
mcp__unityMCP__manage_asset action=move path="_App/Scripts/SpatialUi/UserPanel.cs"            destination="_App/Scripts/SpatialUi/Panels/UserPanel.cs"
```

- [ ] **Step 3: Move `VrKeyboard` to the `SpatialUi/` root**

`VrKeyboard.cs` is currently in `Elements/`. Move it to the `SpatialUi/` root so `Elements/` holds only list items:

```
mcp__unityMCP__manage_asset action=move path="_App/Scripts/SpatialUi/Elements/VrKeyboard.cs" destination="_App/Scripts/SpatialUi/VrKeyboard.cs"
```
(Class name unchanged — `RootLifetimeScope` / `KeyboardButtonController` references resolve by type/GUID, no edits.)

- [ ] **Step 4: Verify moves**

`Glob Assets/_App/Scripts/SpatialUi/Behaviors/*.cs` (6 files), `.../Panels/*.cs` (5 files), `.../VrKeyboard.cs` present, and `.../Elements/` now contains only `OutlinerItem`, `RigOutlinerItem`, `SceneItem`, `LabAssetCard`, `TrackRowView`, `TimelineLaneView`.

- [ ] **Step 5: VERIFY GATE** (compile + tests). Moves only → must be green.

---

## Task 4: Rename framework types

Two renames at `SpatialUi/` root + their references. Do all edits, then verify.

**Files:**
- Modify+rename: `_App/Scripts/SpatialUi/UiPanelManager.cs` → `UiPanelOrchestrator.cs`
- Modify+rename: `_App/Scripts/SpatialUi/DetachablePanel.cs` → `SpatialPanelDetachable.cs`
- Modify: `_App/Scripts/Bootstrap/VrEditingSceneScope.cs:15`, `_App/Scripts/Bootstrap/SandboxSceneScope.cs:15`
- Modify: `_App/Scripts/SpatialUi/Panels/UserPanel.cs`, `_App/Scripts/SpatialUi/Behaviors/DetachablePanelDragHandle.cs`

- [ ] **Step 1: Rename class `UiPanelManager` → `UiPanelOrchestrator`**

Edit `UiPanelManager.cs`: change `public class UiPanelManager : IStartable, IDisposable` → `public class UiPanelOrchestrator : IStartable, IDisposable` and the constructor `public UiPanelManager(` → `public UiPanelOrchestrator(`. Then:
```
mcp__unityMCP__manage_asset action=rename path="_App/Scripts/SpatialUi/UiPanelManager.cs" destination="UiPanelOrchestrator.cs"
```

- [ ] **Step 2: Update DI registrations**

In **both** `VrEditingSceneScope.cs` and `SandboxSceneScope.cs`, line 15, replace:
```csharp
builder.Register<UiPanelManager>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
```
with:
```csharp
builder.Register<UiPanelOrchestrator>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
```

- [ ] **Step 3: Rename class `DetachablePanel` → `SpatialPanelDetachable`**

Edit `DetachablePanel.cs`: `public class DetachablePanel : MonoBehaviour` → `public class SpatialPanelDetachable : MonoBehaviour`. Then:
```
mcp__unityMCP__manage_asset action=rename path="_App/Scripts/SpatialUi/DetachablePanel.cs" destination="SpatialPanelDetachable.cs"
```

- [ ] **Step 4: Update `DetachablePanel` references**

In `Behaviors/DetachablePanelDragHandle.cs`, change the field type `[SerializeField] private DetachablePanel _panel;` → `[SerializeField] private SpatialPanelDetachable _panel;`.
In `Panels/UserPanel.cs`, `Grep` for `DetachablePanel` and replace each **type** occurrence with `SpatialPanelDetachable` (variable names may stay).

- [ ] **Step 5: Confirm no stray references**

`Grep "\bDetachablePanel\b"` and `Grep "UiPanelManager"` across `Assets/_App` → expect zero `.cs` hits (the `DetachablePanelDragHandle` *filename/classname* keeps "DetachablePanel" — that is intentional and is not a type reference to the renamed class).

- [ ] **Step 6: VERIFY GATE**

---

## Task 5: Rename standalone presenters → `*Panel`

Rename five smart controllers, move them into `Panels/`, and update their DI/injection references. Apply per-type (each rename + its refs) so compilation stays green; verify once at the end.

**Files:**
- `_App/Scripts/SpatialUi/Views/SceneOutlinerView.cs` → `Panels/OutlinerPanel.cs`
- `_App/Scripts/SpatialUi/Views/SceneInspectorView.cs` → `Panels/InspectorPanel.cs`
- `_App/Scripts/SpatialUi/SettingsModule.cs` → `Panels/SettingsPanel.cs`
- `_App/Scripts/SpatialUi/AssetBrowserModule.cs` → `Panels/AssetBrowserPanel.cs`
- `_App/Scripts/SpatialUi/IkSetupWizard.cs` → `Panels/IkWizardPanel.cs`
- Modify: `Bootstrap/VrEditingSceneScope.cs`, `Bootstrap/SandboxSceneScope.cs`, `Bootstrap/RootLifetimeScope.cs`, `Panels/BoneInspectorPanel.cs`, `Behaviors/FileBrowserVrAnchor.cs`

- [ ] **Step 1: `SceneOutlinerView` → `OutlinerPanel`**

Edit `Views/SceneOutlinerView.cs`: `public class SceneOutlinerView : MonoBehaviour` → `public class OutlinerPanel : MonoBehaviour`. Move+rename:
```
mcp__unityMCP__manage_asset action=move path="_App/Scripts/SpatialUi/Views/SceneOutlinerView.cs" destination="_App/Scripts/SpatialUi/Panels/OutlinerPanel.cs"
```
In `VrEditingSceneScope.cs` and `SandboxSceneScope.cs` replace `Object.FindAnyObjectByType<SceneOutlinerView>` → `Object.FindAnyObjectByType<OutlinerPanel>` (the `outliner` local var name may stay).

- [ ] **Step 2: `SceneInspectorView` → `InspectorPanel`**

Edit `Views/SceneInspectorView.cs`: `public class SceneInspectorView : MonoBehaviour` → `public class InspectorPanel : MonoBehaviour`. Move+rename:
```
mcp__unityMCP__manage_asset action=move path="_App/Scripts/SpatialUi/Views/SceneInspectorView.cs" destination="_App/Scripts/SpatialUi/Panels/InspectorPanel.cs"
```
In `VrEditingSceneScope.cs` and `SandboxSceneScope.cs` replace `FindAnyObjectByType<SceneInspectorView>` → `FindAnyObjectByType<InspectorPanel>`.

- [ ] **Step 3: `SettingsModule` → `SettingsPanel`**

Edit `SettingsModule.cs`: `public class SettingsModule : MonoBehaviour` → `public class SettingsPanel : MonoBehaviour`. Move+rename:
```
mcp__unityMCP__manage_asset action=move path="_App/Scripts/SpatialUi/SettingsModule.cs" destination="_App/Scripts/SpatialUi/Panels/SettingsPanel.cs"
```
`Grep "SettingsModule"` across `Assets/_App` → expect no other `.cs` references (it is an empty stub, referenced only via prefab GUID). If any appear, replace the type.

- [ ] **Step 4: `AssetBrowserModule` → `AssetBrowserPanel`**

Edit `AssetBrowserModule.cs`: `public class AssetBrowserModule : MonoBehaviour` → `public class AssetBrowserPanel : MonoBehaviour`. Move+rename:
```
mcp__unityMCP__manage_asset action=move path="_App/Scripts/SpatialUi/AssetBrowserModule.cs" destination="_App/Scripts/SpatialUi/Panels/AssetBrowserPanel.cs"
```
Replace `AssetBrowserModule` → `AssetBrowserPanel` (type occurrences) in: `VrEditingSceneScope.cs` (L55), `SandboxSceneScope.cs` (L53), `RootLifetimeScope.cs` (L36), and `Behaviors/FileBrowserVrAnchor.cs` (field `private AssetBrowserModule _target;` and `FindAnyObjectByType<AssetBrowserModule>`).

- [ ] **Step 5: `IkSetupWizard` → `IkWizardPanel`**

Edit `IkSetupWizard.cs`: `public class IkSetupWizard : MonoBehaviour` → `public class IkWizardPanel : MonoBehaviour`. Move+rename:
```
mcp__unityMCP__manage_asset action=move path="_App/Scripts/SpatialUi/IkSetupWizard.cs" destination="_App/Scripts/SpatialUi/Panels/IkWizardPanel.cs"
```
Replace `IkSetupWizard` → `IkWizardPanel` (type occurrences) in: `VrEditingSceneScope.cs` (L36-37), `SandboxSceneScope.cs` (L34-35), and `Panels/BoneInspectorPanel.cs` (field `private IkSetupWizard _ikWizard;` and ctor param `IkSetupWizard ikWizard`).

- [ ] **Step 6: Confirm no stray references**

`Grep` each old name (`SceneOutlinerView`, `SceneInspectorView`, `SettingsModule`, `AssetBrowserModule`, `IkSetupWizard`) across `Assets/_App` → zero `.cs` hits. Do **not** delete `Views/` yet — it still holds the Animator cluster files (`AnimatorPanelView`, `AnimatorToolbarView`, `AnimatorTransportView`, `AnimatorEmptyStateView`), which leave in Task 6.

- [ ] **Step 7: VERIFY GATE**

---

## Task 6: Rename the Animator cluster

The Animator panel is the only split panel. `AnimatorPanel` (its root) references every sub-part, the track row, the lane, and the timeline input by type — so rename each child first (updating the one referencing field in `AnimatorPanel`), then rename the root. All files end up in `Panels/` (sub-parts) or `Elements/` (row/lane).

**Files:**
- `Elements/TrackRowView.cs` → `Elements/TrackRow.cs`
- `Elements/TimelineLaneView.cs` → `Elements/TimelineLane.cs`
- `Elements/TimelineRulerView.cs` → `Panels/AnimatorSubRuler.cs`
- `Elements/TimelinePlayheadView.cs` → `Panels/AnimatorSubPlayhead.cs`
- `Elements/TimelineLanesView.cs` → `Panels/AnimatorSubLanes.cs`
- `Elements/TimelineInputHandler.cs` → `Behaviors/TimelineScrubInput.cs` (renamed here, not in Task 3)
- `Views/AnimatorToolbarView.cs` → `Panels/AnimatorSubToolbar.cs`
- `Views/AnimatorTransportView.cs` → `Panels/AnimatorSubTransport.cs`
- `Views/AnimatorEmptyStateView.cs` → `Panels/AnimatorSubEmptyState.cs`
- `Views/AnimatorPanelView.cs` → `Panels/AnimatorPanel.cs`
- Modify: `Bootstrap/VrEditingSceneScope.cs:62`

> Note: `TimelineInputHandler.cs` is currently in `Elements/` (it was NOT in the Task 3 behavior-move list because it is renamed here). Confirm its current path with `Glob` before moving.

- [ ] **Step 1: `TimelineInputHandler` → `TimelineScrubInput`**

Edit the file: `public class TimelineInputHandler : MonoBehaviour, IPointerDownHandler, IDragHandler` → `public class TimelineScrubInput : MonoBehaviour, IPointerDownHandler, IDragHandler`. Move+rename:
```
mcp__unityMCP__manage_asset action=move path="_App/Scripts/SpatialUi/Elements/TimelineInputHandler.cs" destination="_App/Scripts/SpatialUi/Behaviors/TimelineScrubInput.cs"
```
In `Views/AnimatorPanelView.cs` change field `[SerializeField] private TimelineInputHandler _timelineInput;` → `[SerializeField] private TimelineScrubInput _timelineInput;`.

- [ ] **Step 2: `TrackRowView` → `TrackRow`**

Edit `Elements/TrackRowView.cs`: `public class TrackRowView : MonoBehaviour` → `public class TrackRow : MonoBehaviour`. **Leave `public enum TrackRowKind` as-is** in the same file (pre-existing two-types-per-file; out of scope to split). Rename:
```
mcp__unityMCP__manage_asset action=rename path="_App/Scripts/SpatialUi/Elements/TrackRowView.cs" destination="TrackRow.cs"
```
In `Views/AnimatorPanelView.cs` replace the three `TrackRowView` type uses: field `private TrackRowView _trackRowPrefab;` → `TrackRow`, `private readonly List<TrackRowView> _rowPool` → `List<TrackRow>`, and `private TrackRowView GetOrCreateRow(int idx)` → `private TrackRow GetOrCreateRow(int idx)`. (`TrackRowKind` references stay.)

- [ ] **Step 3: `TimelineLaneView` → `TimelineLane`**

Edit `Elements/TimelineLaneView.cs`: `public class TimelineLaneView : MonoBehaviour` → `public class TimelineLane : MonoBehaviour`. Rename:
```
mcp__unityMCP__manage_asset action=rename path="_App/Scripts/SpatialUi/Elements/TimelineLaneView.cs" destination="TimelineLane.cs"
```
`Grep "TimelineLaneView"` across `Assets/_App/Scripts` → update each type use to `TimelineLane`. Known sites: `Views/AnimatorPanelView.cs` and `Elements/TimelineLanesView.cs` (its `Lanes` collection / `FindLane` return type / instantiation).

- [ ] **Step 4: Rename the four `*View` sub-parts → `AnimatorSub*` and move to `Panels/`**

For each, edit the class declaration then move:

```
# AnimatorToolbarView → AnimatorSubToolbar
edit: public class AnimatorToolbarView   → public class AnimatorSubToolbar
mcp__unityMCP__manage_asset action=move path="_App/Scripts/SpatialUi/Views/AnimatorToolbarView.cs"   destination="_App/Scripts/SpatialUi/Panels/AnimatorSubToolbar.cs"

# AnimatorTransportView → AnimatorSubTransport
edit: public class AnimatorTransportView → public class AnimatorSubTransport
mcp__unityMCP__manage_asset action=move path="_App/Scripts/SpatialUi/Views/AnimatorTransportView.cs" destination="_App/Scripts/SpatialUi/Panels/AnimatorSubTransport.cs"

# AnimatorEmptyStateView → AnimatorSubEmptyState  (has nested `public enum State`)
edit: public class AnimatorEmptyStateView → public class AnimatorSubEmptyState
mcp__unityMCP__manage_asset action=move path="_App/Scripts/SpatialUi/Views/AnimatorEmptyStateView.cs" destination="_App/Scripts/SpatialUi/Panels/AnimatorSubEmptyState.cs"

# TimelineRulerView → AnimatorSubRuler
edit: public class TimelineRulerView → public class AnimatorSubRuler
mcp__unityMCP__manage_asset action=move path="_App/Scripts/SpatialUi/Elements/TimelineRulerView.cs" destination="_App/Scripts/SpatialUi/Panels/AnimatorSubRuler.cs"

# TimelinePlayheadView → AnimatorSubPlayhead
edit: public class TimelinePlayheadView → public class AnimatorSubPlayhead
mcp__unityMCP__manage_asset action=move path="_App/Scripts/SpatialUi/Elements/TimelinePlayheadView.cs" destination="_App/Scripts/SpatialUi/Panels/AnimatorSubPlayhead.cs"

# TimelineLanesView → AnimatorSubLanes
edit: public class TimelineLanesView → public class AnimatorSubLanes
mcp__unityMCP__manage_asset action=move path="_App/Scripts/SpatialUi/Elements/TimelineLanesView.cs" destination="_App/Scripts/SpatialUi/Panels/AnimatorSubLanes.cs"
```

- [ ] **Step 5: Update sub-part references in `AnimatorPanelView.cs`**

In `Views/AnimatorPanelView.cs` update the serialized field types and the empty-state enum access:

```csharp
[SerializeField] private AnimatorSubToolbar    _toolbar;
[SerializeField] private AnimatorSubTransport  _transport;
[SerializeField] private AnimatorSubEmptyState _emptyState;
[SerializeField] private AnimatorSubRuler      _ruler;
[SerializeField] private AnimatorSubLanes      _lanes;
[SerializeField] private AnimatorSubPlayhead   _playhead;
```
and replace every `AnimatorEmptyStateView.State` → `AnimatorSubEmptyState.State` (in `ShowEmpty(...)` signature and the `.NoSelection` / `.NoContainer` call sites).

- [ ] **Step 6: Rename the root `AnimatorPanelView` → `AnimatorPanel`**

Edit `Views/AnimatorPanelView.cs`: `public class AnimatorPanelView : MonoBehaviour` → `public class AnimatorPanel : MonoBehaviour`. Move+rename:
```
mcp__unityMCP__manage_asset action=move path="_App/Scripts/SpatialUi/Views/AnimatorPanelView.cs" destination="_App/Scripts/SpatialUi/Panels/AnimatorPanel.cs"
```
In `VrEditingSceneScope.cs` (L62) replace `FindAnyObjectByType<AnimatorPanelView>` → `FindAnyObjectByType<AnimatorPanel>`.

- [ ] **Step 7: Confirm no stray references and clean up**

`Grep` each old name (`AnimatorPanelView`, `AnimatorToolbarView`, `AnimatorTransportView`, `AnimatorEmptyStateView`, `TimelineRulerView`, `TimelinePlayheadView`, `TimelineLanesView`, `TimelineLaneView`, `TrackRowView`, `TimelineInputHandler`) across `Assets/_App/Scripts` → zero `.cs` hits. If `SpatialUi/Views/` is now empty, delete it:
```
mcp__unityMCP__manage_asset action=delete path="_App/Scripts/SpatialUi/Views"
```

- [ ] **Step 8: VERIFY GATE**

---

## Task 7: Update documentation

**Files:**
- Modify: `Assets/_App/Documentation/conventions.md`
- Modify: `CLAUDE.md`
- Modify: `Assets/_App/Documentation/STRUCTURE.md`

- [ ] **Step 1: Add the SpatialUi role taxonomy to `conventions.md`**

Add a "SpatialUi script roles" section with the criteria table from the spec (Panel / `<Panel>Sub<Part>` / Element / Behavior / Framework — definition + folder for each). This is the deliverable of the original "document UI naming" task.

- [ ] **Step 2: Merge the animation subsystem rows in `CLAUDE.md`**

In the Subsystems table, replace the two rows `AnimationAuthoring` and `AnimationPlayback` with a single `Animation` row (responsibility: `ActionData`, keyframe recording, NLA composition, `PlaybackController`/clock/scrub-loop-speed transport). Update any prose that lists them separately and any `Scripts/AnimationAuthoring` path reference.

- [ ] **Step 3: Regenerate the affected subtrees in `STRUCTURE.md`**

Update the `Scripts/` tree: `Animation/` (merged, with `AnimationPlayback.cs` inside) and the new `SpatialUi/` layout (`Panels/`, `Elements/`, `Behaviors/`, root infra, no `Views/`). Update the `Tests/` subtree (`Animation/`). Refresh the per-subsystem `.cs` counts if the file lists them.

- [ ] **Step 4: VERIFY GATE — final pass**

Run the full gate **including Step 3** (scene loads): open `MainMenu`, `VrEditing`, `Sandbox`, and `Scenes/_Sandbox/AnimatorPanelSandbox`; `read_console` → no missing-script / `MissingReferenceException`. Confirm Test Runner still 143/7.

---

## Notes / known deviations (do NOT "fix" here — deferred to spec B)

- `VrKeyboard` is only moved, not renamed (root-scoped + referenced by third-party `KeyboardButtonController`).
- `SpatialPanelDetachable` keeps its current logic; chrome/behavior rework is spec B.
- `IkWizardPanel` keeps its current logic; import rework is spec B.
- `FileBrowserVrAnchor` keeps its `FindAnyObjectByType<AssetBrowserPanel>` lookup (pre-existing forbidden pattern) — replacing with DI is spec B.
- `TrackRowKind` enum remains in `TrackRow.cs` (pre-existing two-types-per-file).
- `PanelRegistry` SO name and `SpatialPanel` base name are intentionally unchanged.
