# _App Restructure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Collapse `_App/`'s 21 per-subsystem assemblies into 3 (`_App.Runtime`/`_App.Editor`/`_App.Tests`) and reorganize into a layer-based layout (`Scripts/`, `Content/`, `Tests/`, `Editor/`) without breaking any GUID reference or changing runtime behavior.

**Architecture:** "Assemblies first" — collapse the assemblies *in place* (Phase 1), after which all runtime code lives in one assembly and moving `.cs` files between folders can no longer break compilation (Phase 2). Assets follow by GUID-preserving moves. Cleanup last (Phase 3).

**Tech Stack:** Unity 6000.3.7f1, C# (no namespaces in runtime), VContainer DI, custom `EventBus`, asmdef assemblies. All file operations via Unity MCP (`manage_asset` = `AssetDatabase.MoveAsset`, GUID-safe).

---

## Conventions for this plan (read first)

- **No git by the executor.** The user manages git. Where a normal plan says "commit," this plan says **CHECKPOINT** — a verification gate. The user commits manually if they want a restore point. Do **not** run `git` commands.
- **"Verify" means Unity verification, not unit TDD:**
  - `refresh_unity(compile="request", wait_for_ready=true)` then `read_console(types=["error"])` → expect **zero errors**.
  - At phase gates: `run_tests(mode="EditMode")` → expect the existing suite to pass (same pass/fail set as before migration).
  - Scene gates: `manage_scene(action="load", ...)` then check console for **missing-script / missing-reference** warnings.
- **Path convention:** `manage_asset` paths are project asset paths. This plan writes them as `Assets/_App/...`. On first use, confirm the server accepts the `Assets/`-prefixed form with one `manage_asset(action="get_info", path="Assets/_App/_App.asmdef")` dry-run; if it expects `_App/...` (Assets-relative without prefix), drop the `Assets/` prefix consistently.
- **asmdef references are by NAME, not GUID** — so creating/renaming asmdef files is safe; their own `.meta` GUID is irrelevant. Create asmdef text files with the `Write` tool (filesystem), then `refresh_unity`. Rename existing asmdefs by editing the `name` field and renaming the file.
- **Transient errors are expected mid-Phase-1.** Unity may recompile between operations and show errors while the assembly graph is half-migrated. **Only gate on the console at the end-of-task verification steps**, not mid-batch.
- **Prerequisite every task assumes:** Unity Editor is open and the MCP bridge is connected (`mcpforunity://instances` lists the session). If Unity is closed, stop — moves are not safe outside the editor.

## File Structure (what changes)

**Created:**
- `Assets/_App/Tests/_App.Tests.asmdef`
- `Assets/_App/Scripts/` (+ `Core/`, `Bootstrap/`, and one folder per subsystem)
- `Assets/_App/Scripts/_App.Runtime.asmdef` (relocated from root)
- `Assets/_App/Content/` (+ `Prefabs/`, `Materials/`, `Models/`, `Textures/`, `Shaders/`, `ScriptableObjects/`)
- 23 single-event `.cs` files (from splitting `AppEvents.cs`)

**Modified:**
- `Assets/_App/Editor/PromeonLab.Editor.asmdef` → renamed to `_App.Editor.asmdef` (name + references)
- `Assets/_App/_App.asmdef` → renamed to `_App.Runtime.asmdef` (name + references), then relocated into `Scripts/`
- `InternalsVisibleTo.cs` → target `_App.Tests`
- `CLAUDE.md`, `Assets/_App/Documentation/*.md`

**Deleted (by user in Phase 3, or executor on request):**
- `_App/_Shared/_Shared.asmdef` + 11 `Subsystems/*/Subsystems.*.asmdef` + 5 `Subsystems/*/Tests/Subsystems.*.Tests.asmdef` + `Subsystems/SpatialUi/Editor/Subsystems.SpatialUi.Editor.asmdef`
- 3 tombstones: `Subsystems/RigBuilder/Data/{BoneRecord,IkChainRecord,RigDefinition}.cs`
- `Subsystems/SpatialUi/Scripts/Elements/SceneOutlinerRow.cs` (empty renamed-stub)
- `_App/_Shared/`, `_App/Subsystems/`, `_App/DemoAssets/` (after emptied), `Assets/Resources/` contents, `_Shared/Events/AppEvents.cs`

---

## Task 1: Phase 1 — Atomic assembly collapse

This whole task is **one atomic migration**. Do all steps, then verify once at the end. Expect transient console errors between steps — ignore them until Step 1.10.

**Files:**
- Create: `Assets/_App/Tests/_App.Tests.asmdef`
- Create: `Assets/_App/Editor/_App.Editor.asmdef`
- Rename: `Assets/_App/_App.asmdef` → `Assets/_App/_App.Runtime.asmdef`
- Modify: `Assets/_App/Subsystems/AnimationAuthoring/InternalsVisibleTo.cs`
- Move: 5 test folders, 1 editor script
- Delete: 18 asmdefs + 3 tombstones

- [ ] **Step 1.1: Create the Tests folder and move all test files**

`manage_asset(action="create_folder", path="Assets/_App/Tests")` then for each subsystem create the subfolder and move its test `.cs` files (NOT the old `.asmdef`):

```
create_folder Assets/_App/Tests/AnimationAuthoring
move Assets/_App/Subsystems/AnimationAuthoring/Tests/ActionContainerTests.cs        → Assets/_App/Tests/AnimationAuthoring/ActionContainerTests.cs
move Assets/_App/Subsystems/AnimationAuthoring/Tests/AnimationAuthoringTests.cs      → Assets/_App/Tests/AnimationAuthoring/AnimationAuthoringTests.cs
move Assets/_App/Subsystems/AnimationAuthoring/Tests/AnimationClipboardTests.cs      → Assets/_App/Tests/AnimationAuthoring/AnimationClipboardTests.cs
move Assets/_App/Subsystems/AnimationAuthoring/Tests/AnimationClockTests.cs          → Assets/_App/Tests/AnimationAuthoring/AnimationClockTests.cs
move Assets/_App/Subsystems/AnimationAuthoring/Tests/AnimationDataTests.cs           → Assets/_App/Tests/AnimationAuthoring/AnimationDataTests.cs
create_folder Assets/_App/Tests/RigBuilder
move Assets/_App/Subsystems/RigBuilder/Tests/PromeonProxyRigBuilderTests.cs          → Assets/_App/Tests/RigBuilder/PromeonProxyRigBuilderTests.cs
create_folder Assets/_App/Tests/SceneComposition
move Assets/_App/Subsystems/SceneComposition/Tests/AssetRegistryTests.cs             → Assets/_App/Tests/SceneComposition/AssetRegistryTests.cs
move Assets/_App/Subsystems/SceneComposition/Tests/CommandStackTests.cs              → Assets/_App/Tests/SceneComposition/CommandStackTests.cs
move Assets/_App/Subsystems/SceneComposition/Tests/SceneGraphTests.cs                → Assets/_App/Tests/SceneComposition/SceneGraphTests.cs
move Assets/_App/Subsystems/SceneComposition/Tests/SceneNodeTests.cs                 → Assets/_App/Tests/SceneComposition/SceneNodeTests.cs
move Assets/_App/Subsystems/SceneComposition/Tests/SelectionManagerTests.cs          → Assets/_App/Tests/SceneComposition/SelectionManagerTests.cs
create_folder Assets/_App/Tests/StorageCore
move Assets/_App/Subsystems/StorageCore/Tests/PathProviderTests.cs                   → Assets/_App/Tests/StorageCore/PathProviderTests.cs
move Assets/_App/Subsystems/StorageCore/Tests/SceneSerializerTests.cs                → Assets/_App/Tests/StorageCore/SceneSerializerTests.cs
create_folder Assets/_App/Tests/VrInteraction
move Assets/_App/Subsystems/VrInteraction/Tests/AxisMoveStrategyTests.cs             → Assets/_App/Tests/VrInteraction/AxisMoveStrategyTests.cs
move Assets/_App/Subsystems/VrInteraction/Tests/AxisScaleStrategyTests.cs            → Assets/_App/Tests/VrInteraction/AxisScaleStrategyTests.cs
move Assets/_App/Subsystems/VrInteraction/Tests/BoundsFitterTests.cs                 → Assets/_App/Tests/VrInteraction/BoundsFitterTests.cs
move Assets/_App/Subsystems/VrInteraction/Tests/GizmoActivatorStateTests.cs          → Assets/_App/Tests/VrInteraction/GizmoActivatorStateTests.cs
move Assets/_App/Subsystems/VrInteraction/Tests/RingRotateStrategyTests.cs           → Assets/_App/Tests/VrInteraction/RingRotateStrategyTests.cs
move Assets/_App/Subsystems/VrInteraction/Tests/UniformScaleStrategyTests.cs         → Assets/_App/Tests/VrInteraction/UniformScaleStrategyTests.cs
```

- [ ] **Step 1.2: Write the Tests asmdef**

Write `Assets/_App/Tests/_App.Tests.asmdef`:

```json
{
  "name": "_App.Tests",
  "references": ["_App.Runtime", "VContainer", "Unity.XR.Interaction.Toolkit", "Unity.InputSystem", "QuickOutline"],
  "includePlatforms": ["Editor"],
  "optionalUnityReferences": ["TestAssemblies"],
  "autoReferenced": false
}
```

- [ ] **Step 1.3: Delete the 5 per-subsystem test asmdefs**

```
delete Assets/_App/Subsystems/AnimationAuthoring/Tests/Subsystems.AnimationAuthoring.Tests.asmdef
delete Assets/_App/Subsystems/RigBuilder/Tests/Subsystems.RigBuilder.Tests.asmdef
delete Assets/_App/Subsystems/SceneComposition/Tests/Subsystems.SceneComposition.Tests.asmdef
delete Assets/_App/Subsystems/StorageCore/Tests/Subsystems.StorageCore.Tests.asmdef
delete Assets/_App/Subsystems/VrInteraction/Tests/Subsystems.VrInteraction.Tests.asmdef
```

- [ ] **Step 1.4: Consolidate the Editor assembly**

Move the one nested editor script up, then write the new editor asmdef, then delete both old editor asmdefs:

```
move Assets/_App/Subsystems/SpatialUi/Editor/AnimatorPanelModuleBuilder.cs → Assets/_App/Editor/AnimatorPanelModuleBuilder.cs
delete Assets/_App/Subsystems/SpatialUi/Editor/Subsystems.SpatialUi.Editor.asmdef
delete Assets/_App/Editor/PromeonLab.Editor.asmdef
```

Write `Assets/_App/Editor/_App.Editor.asmdef`:

```json
{
  "name": "_App.Editor",
  "references": ["_App.Runtime", "Unity.TextMeshPro", "Unity.Animation.Rigging"],
  "includePlatforms": ["Editor"],
  "autoReferenced": false
}
```

- [ ] **Step 1.5: Rename the root assembly to `_App.Runtime`**

Edit `Assets/_App/_App.asmdef` contents to:

```json
{
  "name": "_App.Runtime",
  "references": ["VContainer", "Unity.TextMeshPro", "Unity.XR.Interaction.Toolkit", "Unity.InputSystem", "SimpleFileBrowser.Runtime", "QuickOutline", "Unity.Animation.Rigging"],
  "autoReferenced": false
}
```

Then rename the file: `manage_asset(action="rename", path="Assets/_App/_App.asmdef", destination="Assets/_App/_App.Runtime.asmdef")`. (It stays at `_App/` root for now — it will relocate to `Scripts/` in Task 8.)

- [ ] **Step 1.6: Delete `_Shared` and the 11 subsystem asmdefs**

```
delete Assets/_App/_Shared/_Shared.asmdef
delete Assets/_App/Subsystems/AnimationAuthoring/Subsystems.AnimationAuthoring.asmdef
delete Assets/_App/Subsystems/AnimationPlayback/Subsystems.AnimationPlayback.asmdef
delete Assets/_App/Subsystems/AssetBrowser/Subsystems.AssetBrowser.asmdef
delete Assets/_App/Subsystems/ErrorHandling/Subsystems.ErrorHandling.asmdef
delete Assets/_App/Subsystems/ExportPipeline/Subsystems.ExportPipeline.asmdef
delete Assets/_App/Subsystems/InputBindings/Subsystems.InputBindings.asmdef
delete Assets/_App/Subsystems/ModeOrchestrator/Subsystems.ModeOrchestrator.asmdef
delete Assets/_App/Subsystems/RigBuilder/Subsystems.RigBuilder.asmdef
delete Assets/_App/Subsystems/SceneComposition/Subsystems.SceneComposition.asmdef
delete Assets/_App/Subsystems/SpatialUi/Subsystems.SpatialUi.asmdef
delete Assets/_App/Subsystems/StorageCore/Subsystems.StorageCore.asmdef
delete Assets/_App/Subsystems/VrInteraction/Subsystems.VrInteraction.asmdef
```

- [ ] **Step 1.7: Delete the 3 empty tombstones**

These are comment-only files whose real types live in `_Shared/Models/`. Deleting them prevents duplicate-type collisions once everything is one assembly.

```
delete Assets/_App/Subsystems/RigBuilder/Data/BoneRecord.cs
delete Assets/_App/Subsystems/RigBuilder/Data/IkChainRecord.cs
delete Assets/_App/Subsystems/RigBuilder/Data/RigDefinition.cs
```

- [ ] **Step 1.8: Retarget `InternalsVisibleTo`**

Edit `Assets/_App/Subsystems/AnimationAuthoring/InternalsVisibleTo.cs` to exactly:

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("_App.Tests")]
```

- [ ] **Step 1.9: Refresh and compile**

Run: `refresh_unity(scope="all", compile="request", mode="force", wait_for_ready=true)`
Expected: editor returns to ready (`isCompiling=false`).

- [ ] **Step 1.10: Verify zero compile errors**

Run: `read_console(action="get", types=["error"], count="50", format="detailed")`
Expected: **no error entries.** If errors mention a missing assembly reference (e.g. a runtime file using a `QuickOutline`/`Animation.Rigging`/`SimpleFileBrowser`/`InputSystem` type), add the missing package name to `_App.Runtime.asmdef.references` and re-run Step 1.9. If errors mention a missing package in tests, add it to `_App.Tests.asmdef.references`.

- [ ] **Step 1.11: Run the existing test suite**

Run: `run_tests(mode="EditMode")` then poll `get_test_job` until done.
Expected: same pass set as before migration (no NEW failures introduced).

- [ ] **Step 1.12: CHECKPOINT — scene + missing-script verification (USER)**

Run: `manage_scene(action="load", path="Assets/_App/Scenes/Bootstrap.unity")`, then `read_console(types=["error","warning"], count="50")`.
Expected: no "missing script" / "missing reference" entries. **User confirms** the scene looks intact and (optionally) commits a restore point here. Do not proceed to Task 2 until confirmed.

---

> **State after Task 1:** 3 assemblies (`_App.Runtime` at `_App/` root, `_App.Editor`, `_App.Tests`). All runtime code still physically under `Subsystems/`, `_Shared/`, `Bootstrap/` but compiled into one assembly. Folder reshape is now risk-free for compilation.

---

## Task 2: Phase 2 — Create Scripts/ skeleton and move subsystem code folders

**Files:** create `Assets/_App/Scripts/`; move 11 subsystem folders + `Bootstrap/` into it. (SpatialUi handled separately in Task 3 due to its nested `Scripts/` and `Prefabs/`.)

- [ ] **Step 2.1: Create the Scripts root and Core folder**

```
create_folder Assets/_App/Scripts
create_folder Assets/_App/Scripts/Core
```

- [ ] **Step 2.2: Move Core primitives**

```
move Assets/_App/_Shared/Events/EventBus.cs   → Assets/_App/Scripts/Core/EventBus.cs
move Assets/_App/_Shared/Interfaces/ICommand.cs → Assets/_App/Scripts/Core/ICommand.cs
```

- [ ] **Step 2.3: Move whole subsystem folders into Scripts/**

Each is a folder move (carries all contained `.cs`, `Data/`, and—where present—`Prefabs/`/`UI/`, which Task 7/8 will relocate). Do NOT move SpatialUi here.

```
move Assets/_App/Bootstrap                          → Assets/_App/Scripts/Bootstrap
move Assets/_App/Subsystems/AnimationAuthoring      → Assets/_App/Scripts/AnimationAuthoring
move Assets/_App/Subsystems/AnimationPlayback       → Assets/_App/Scripts/AnimationPlayback
move Assets/_App/Subsystems/AssetBrowser            → Assets/_App/Scripts/AssetBrowser
move Assets/_App/Subsystems/ErrorHandling           → Assets/_App/Scripts/ErrorHandling
move Assets/_App/Subsystems/ExportPipeline          → Assets/_App/Scripts/ExportPipeline
move Assets/_App/Subsystems/InputBindings           → Assets/_App/Scripts/InputBindings
move Assets/_App/Subsystems/ModeOrchestrator        → Assets/_App/Scripts/ModeOrchestrator
move Assets/_App/Subsystems/RigBuilder              → Assets/_App/Scripts/RigBuilder
move Assets/_App/Subsystems/SceneComposition        → Assets/_App/Scripts/SceneComposition
move Assets/_App/Subsystems/StorageCore             → Assets/_App/Scripts/StorageCore
move Assets/_App/Subsystems/VrInteraction           → Assets/_App/Scripts/VrInteraction
```

- [ ] **Step 2.4: Refresh, compile, verify**

Run: `refresh_unity(scope="all", compile="request", wait_for_ready=true)` then `read_console(types=["error"])`.
Expected: zero errors (everything is still under the `_App/` root umbrella).

---

## Task 3: Phase 2 — Flatten SpatialUi

SpatialUi's code is nested under `Subsystems/SpatialUi/Scripts/{Panels,Views,Elements}` and its prefabs/SO/data live alongside. Target: `Scripts/SpatialUi/` with `Panels` flattened to root, `Views/` and `Elements/` kept as subfolders. Prefabs/SO move to Content/ in Task 8.

**Files:** create `Assets/_App/Scripts/SpatialUi/{Views,Elements}`; move scripts.

- [ ] **Step 3.1: Create SpatialUi script folders**

```
create_folder Assets/_App/Scripts/SpatialUi
create_folder Assets/_App/Scripts/SpatialUi/Views
create_folder Assets/_App/Scripts/SpatialUi/Elements
```

- [ ] **Step 3.2: Move Panels (flatten to SpatialUi root)**

```
move Assets/_App/Subsystems/SpatialUi/Scripts/Panels/AssetBrowserModule.cs   → Assets/_App/Scripts/SpatialUi/AssetBrowserModule.cs
move Assets/_App/Subsystems/SpatialUi/Scripts/Panels/BoneInspectorPanel.cs   → Assets/_App/Scripts/SpatialUi/BoneInspectorPanel.cs
move Assets/_App/Subsystems/SpatialUi/Scripts/Panels/DetachablePanel.cs      → Assets/_App/Scripts/SpatialUi/DetachablePanel.cs
move Assets/_App/Subsystems/SpatialUi/Scripts/Panels/IkSetupWizard.cs        → Assets/_App/Scripts/SpatialUi/IkSetupWizard.cs
move Assets/_App/Subsystems/SpatialUi/Scripts/Panels/MainMenuPanel.cs        → Assets/_App/Scripts/SpatialUi/MainMenuPanel.cs
move Assets/_App/Subsystems/SpatialUi/Scripts/Panels/PropertyPanel.cs        → Assets/_App/Scripts/SpatialUi/PropertyPanel.cs
move Assets/_App/Subsystems/SpatialUi/Scripts/Panels/ScenePickerPanel.cs     → Assets/_App/Scripts/SpatialUi/ScenePickerPanel.cs
move Assets/_App/Subsystems/SpatialUi/Scripts/Panels/SettingsModule.cs       → Assets/_App/Scripts/SpatialUi/SettingsModule.cs
move Assets/_App/Subsystems/SpatialUi/Scripts/Panels/SpatialPanel.cs         → Assets/_App/Scripts/SpatialUi/SpatialPanel.cs
move Assets/_App/Subsystems/SpatialUi/Scripts/Panels/UiPanelManager.cs       → Assets/_App/Scripts/SpatialUi/UiPanelManager.cs
move Assets/_App/Subsystems/SpatialUi/Scripts/Panels/UserPanel.cs            → Assets/_App/Scripts/SpatialUi/UserPanel.cs
```

- [ ] **Step 3.3: Move Views/ and Elements/ contents**

```
move Assets/_App/Subsystems/SpatialUi/Scripts/Views/AnimatorEmptyStateView.cs → Assets/_App/Scripts/SpatialUi/Views/AnimatorEmptyStateView.cs
move Assets/_App/Subsystems/SpatialUi/Scripts/Views/AnimatorPanelView.cs       → Assets/_App/Scripts/SpatialUi/Views/AnimatorPanelView.cs
move Assets/_App/Subsystems/SpatialUi/Scripts/Views/AnimatorToolbarView.cs     → Assets/_App/Scripts/SpatialUi/Views/AnimatorToolbarView.cs
move Assets/_App/Subsystems/SpatialUi/Scripts/Views/AnimatorTransportView.cs   → Assets/_App/Scripts/SpatialUi/Views/AnimatorTransportView.cs
move Assets/_App/Subsystems/SpatialUi/Scripts/Views/AssetPropertiesView.cs     → Assets/_App/Scripts/SpatialUi/Views/AssetPropertiesView.cs
move Assets/_App/Subsystems/SpatialUi/Scripts/Views/SceneInspectorView.cs      → Assets/_App/Scripts/SpatialUi/Views/SceneInspectorView.cs
move Assets/_App/Subsystems/SpatialUi/Scripts/Views/SceneOutlinerView.cs       → Assets/_App/Scripts/SpatialUi/Views/SceneOutlinerView.cs
move Assets/_App/Subsystems/SpatialUi/Scripts/Elements/DetachablePanelDragHandle.cs → Assets/_App/Scripts/SpatialUi/Elements/DetachablePanelDragHandle.cs
move Assets/_App/Subsystems/SpatialUi/Scripts/Elements/FileBrowserVrAnchor.cs  → Assets/_App/Scripts/SpatialUi/Elements/FileBrowserVrAnchor.cs
move Assets/_App/Subsystems/SpatialUi/Scripts/Elements/LabAssetCard.cs         → Assets/_App/Scripts/SpatialUi/Elements/LabAssetCard.cs
move Assets/_App/Subsystems/SpatialUi/Scripts/Elements/OutlinerItem.cs         → Assets/_App/Scripts/SpatialUi/Elements/OutlinerItem.cs
move Assets/_App/Subsystems/SpatialUi/Scripts/Elements/PanelDragHandle.cs      → Assets/_App/Scripts/SpatialUi/Elements/PanelDragHandle.cs
move Assets/_App/Subsystems/SpatialUi/Scripts/Elements/RigOutlinerItem.cs      → Assets/_App/Scripts/SpatialUi/Elements/RigOutlinerItem.cs
move Assets/_App/Subsystems/SpatialUi/Scripts/Elements/SceneItem.cs            → Assets/_App/Scripts/SpatialUi/Elements/SceneItem.cs
move Assets/_App/Subsystems/SpatialUi/Scripts/Elements/TimelineInputHandler.cs → Assets/_App/Scripts/SpatialUi/Elements/TimelineInputHandler.cs
move Assets/_App/Subsystems/SpatialUi/Scripts/Elements/TimelineLaneView.cs     → Assets/_App/Scripts/SpatialUi/Elements/TimelineLaneView.cs
move Assets/_App/Subsystems/SpatialUi/Scripts/Elements/TimelineLanesView.cs    → Assets/_App/Scripts/SpatialUi/Elements/TimelineLanesView.cs
move Assets/_App/Subsystems/SpatialUi/Scripts/Elements/TimelinePlayheadView.cs → Assets/_App/Scripts/SpatialUi/Elements/TimelinePlayheadView.cs
move Assets/_App/Subsystems/SpatialUi/Scripts/Elements/TimelineRulerView.cs    → Assets/_App/Scripts/SpatialUi/Elements/TimelineRulerView.cs
move Assets/_App/Subsystems/SpatialUi/Scripts/Elements/TimelineScrollSync.cs   → Assets/_App/Scripts/SpatialUi/Elements/TimelineScrollSync.cs
move Assets/_App/Subsystems/SpatialUi/Scripts/Elements/TrackRowView.cs         → Assets/_App/Scripts/SpatialUi/Elements/TrackRowView.cs
move Assets/_App/Subsystems/SpatialUi/Scripts/Elements/UserPanelKeyboardToggle.cs → Assets/_App/Scripts/SpatialUi/Elements/UserPanelKeyboardToggle.cs
move Assets/_App/Subsystems/SpatialUi/Scripts/Elements/UserPanelOpener.cs      → Assets/_App/Scripts/SpatialUi/Elements/UserPanelOpener.cs
move Assets/_App/Subsystems/SpatialUi/Scripts/Elements/VrKeyboard.cs           → Assets/_App/Scripts/SpatialUi/Elements/VrKeyboard.cs
```

- [ ] **Step 3.4: Delete the empty renamed-stub**

```
delete Assets/_App/Subsystems/SpatialUi/Scripts/Elements/SceneOutlinerRow.cs
```
(Empty file with a "renamed" comment — verify it has no type with `read` before deleting; if it still defines a type, stop and report.)

- [ ] **Step 3.5: Move SpatialUi Data/ config scripts to SpatialUi root**

```
move Assets/_App/Subsystems/SpatialUi/Data/AnimatorPanelConfig.cs → Assets/_App/Scripts/SpatialUi/AnimatorPanelConfig.cs
move Assets/_App/Subsystems/SpatialUi/Data/NavBarConfig.cs         → Assets/_App/Scripts/SpatialUi/NavBarConfig.cs
move Assets/_App/Subsystems/SpatialUi/Data/PanelRegistry.cs        → Assets/_App/Scripts/SpatialUi/PanelRegistry.cs
move Assets/_App/Subsystems/SpatialUi/Data/PanelType.cs            → Assets/_App/Scripts/SpatialUi/PanelType.cs
```
(The `.asset` SO instances in `SpatialUi/Data/` move to Content/ in Task 8.)

- [ ] **Step 3.6: Refresh, compile, verify**

Run: `refresh_unity(scope="all", compile="request", wait_for_ready=true)` then `read_console(types=["error"])`.
Expected: zero errors.

---

## Task 4: Phase 2 — Disperse `_Shared` interfaces, models, and data to owning subsystems

Each line is a GUID-safe `manage_asset(action="move")`. Reclassification vs the old target: `ContainerChange`/`KeyframeChange` go to AnimationAuthoring (animation-specific), NOT Core.

**Files:** moves only.

- [ ] **Step 4.1: Move interfaces to owners**

```
move Assets/_App/_Shared/Interfaces/ISceneGraph.cs        → Assets/_App/Scripts/SceneComposition/ISceneGraph.cs
move Assets/_App/_Shared/Interfaces/ISelectionManager.cs  → Assets/_App/Scripts/SceneComposition/ISelectionManager.cs
move Assets/_App/_Shared/Interfaces/IRigRuntime.cs        → Assets/_App/Scripts/RigBuilder/IRigRuntime.cs
move Assets/_App/_Shared/Interfaces/IAssetLibrary.cs      → Assets/_App/Scripts/AssetBrowser/IAssetLibrary.cs
move Assets/_App/_Shared/Interfaces/IAssetRegistry.cs     → Assets/_App/Scripts/AssetBrowser/IAssetRegistry.cs
move Assets/_App/_Shared/Interfaces/ILabAsset.cs          → Assets/_App/Scripts/AssetBrowser/ILabAsset.cs
```

- [ ] **Step 4.2: Move models to owners**

```
move Assets/_App/_Shared/Models/AppMode.cs            → Assets/_App/Scripts/ModeOrchestrator/AppMode.cs
move Assets/_App/_Shared/Models/AssetEntry.cs         → Assets/_App/Scripts/AssetBrowser/AssetEntry.cs
move Assets/_App/_Shared/Models/AssetType.cs          → Assets/_App/Scripts/AssetBrowser/AssetType.cs
move Assets/_App/_Shared/Models/RigDefinition.cs      → Assets/_App/Scripts/RigBuilder/RigDefinition.cs
move Assets/_App/_Shared/Models/BoneRecord.cs         → Assets/_App/Scripts/RigBuilder/BoneRecord.cs
move Assets/_App/_Shared/Models/IkChainRecord.cs      → Assets/_App/Scripts/RigBuilder/IkChainRecord.cs
move Assets/_App/_Shared/Models/BoneSceneNodeMarker.cs → Assets/_App/Scripts/RigBuilder/BoneSceneNodeMarker.cs
move Assets/_App/_Shared/Models/GizmoMode.cs          → Assets/_App/Scripts/VrInteraction/GizmoMode.cs
move Assets/_App/_Shared/Models/PanelId.cs            → Assets/_App/Scripts/SpatialUi/PanelId.cs
move Assets/_App/_Shared/Models/ErrorLevel.cs         → Assets/_App/Scripts/ErrorHandling/ErrorLevel.cs
```

- [ ] **Step 4.3: Move data + remaining event-enum files to owners**

```
move Assets/_App/_Shared/Data/AssetRef.cs        → Assets/_App/Scripts/AssetBrowser/AssetRef.cs
move Assets/_App/_Shared/Data/AssetSource.cs     → Assets/_App/Scripts/AssetBrowser/AssetSource.cs
move Assets/_App/_Shared/Data/SelectionVisual.cs → Assets/_App/Scripts/VrInteraction/SelectionVisual.cs
move Assets/_App/_Shared/Events/ContainerChange.cs → Assets/_App/Scripts/AnimationAuthoring/ContainerChange.cs
move Assets/_App/_Shared/Events/KeyframeChange.cs  → Assets/_App/Scripts/AnimationAuthoring/KeyframeChange.cs
move Assets/_App/_Shared/Events/AnimationContainerChangedEvent.cs → Assets/_App/Scripts/AnimationAuthoring/AnimationContainerChangedEvent.cs
move Assets/_App/_Shared/Events/BonesVisibilityChangedEvent.cs    → Assets/_App/Scripts/RigBuilder/BonesVisibilityChangedEvent.cs
```

- [ ] **Step 4.4: Refresh, compile, verify**

Run: `refresh_unity(scope="all", compile="request", wait_for_ready=true)` then `read_console(types=["error"])`.
Expected: zero errors. (`_Shared/` now contains only `Events/AppEvents.cs` plus empty folders.)

---

## Task 5: Phase 2 — Split `AppEvents.cs` into one file per event

Create each file with the `Write` tool (or `create_script`) at the path shown, then delete `AppEvents.cs`. Content is the exact struct from the current `AppEvents.cs`. No namespaces.

**Files:** create 23; delete `Assets/_App/_Shared/Events/AppEvents.cs`.

- [ ] **Step 5.1: SceneComposition events**

```csharp
// Assets/_App/Scripts/SceneComposition/SceneOpenedEvent.cs
public struct SceneOpenedEvent { public string SceneId; }
```
```csharp
// Assets/_App/Scripts/SceneComposition/SceneModifiedEvent.cs
public struct SceneModifiedEvent { }
```
```csharp
// Assets/_App/Scripts/SceneComposition/SceneClosedEvent.cs
public struct SceneClosedEvent { }
```
```csharp
// Assets/_App/Scripts/SceneComposition/SceneSelectedEvent.cs
public struct SceneSelectedEvent { public string SceneId; public string DisplayName; }
```
```csharp
// Assets/_App/Scripts/SceneComposition/SelectionChangedEvent.cs
public struct SelectionChangedEvent { public string SelectedNodeId; }
```
```csharp
// Assets/_App/Scripts/SceneComposition/NodeRenamedEvent.cs
public struct NodeRenamedEvent { public string NodeId; public string NewName; }
```

- [ ] **Step 5.2: ModeOrchestrator event**

```csharp
// Assets/_App/Scripts/ModeOrchestrator/ModeChangedEvent.cs
public struct ModeChangedEvent { public AppMode PreviousMode; public AppMode CurrentMode; }
```

- [ ] **Step 5.3: AnimationAuthoring events**

```csharp
// Assets/_App/Scripts/AnimationAuthoring/FrameChangedEvent.cs
public struct FrameChangedEvent { public int Frame; }
```
```csharp
// Assets/_App/Scripts/AnimationAuthoring/PlaybackStateChangedEvent.cs
public struct PlaybackStateChangedEvent { public bool IsPlaying; public int Frame; }
```
```csharp
// Assets/_App/Scripts/AnimationAuthoring/AnimationKeyframeChangedEvent.cs
public struct AnimationKeyframeChangedEvent
{
    public string          NodeId;
    public string          OwnerNodeId;
    public int             Frame;
    public KeyframeChange  Change;
}
```

- [ ] **Step 5.4: AssetBrowser events**

```csharp
// Assets/_App/Scripts/AssetBrowser/AssetImportedEvent.cs
public struct AssetImportedEvent { public string AssetId; }
```
```csharp
// Assets/_App/Scripts/AssetBrowser/AssetSpawnRequestedEvent.cs
public struct AssetSpawnRequestedEvent { public ILabAsset Asset; public UnityEngine.Vector3 Position; public UnityEngine.Quaternion Rotation; }
```

- [ ] **Step 5.5: ErrorHandling event**

```csharp
// Assets/_App/Scripts/ErrorHandling/ErrorOccurredEvent.cs
public struct ErrorOccurredEvent { public ErrorLevel Level; public string Message; }
```

- [ ] **Step 5.6: SpatialUi events**

```csharp
// Assets/_App/Scripts/SpatialUi/KeyboardFocusEvent.cs
public struct KeyboardFocusEvent { public TMPro.TMP_InputField Target; }
```
```csharp
// Assets/_App/Scripts/SpatialUi/PanelDetachedEvent.cs
public struct PanelDetachedEvent { public string EntryId; }
```
```csharp
// Assets/_App/Scripts/SpatialUi/PanelLinkedEvent.cs
public struct PanelLinkedEvent { public string EntryId; }
```
```csharp
// Assets/_App/Scripts/SpatialUi/PanelClosedEvent.cs
public struct PanelClosedEvent { public string EntryId; }
```

- [ ] **Step 5.7: VrInteraction events**

```csharp
// Assets/_App/Scripts/VrInteraction/GizmoToolsPanelOpenedEvent.cs
public struct GizmoToolsPanelOpenedEvent { }
```
```csharp
// Assets/_App/Scripts/VrInteraction/GizmoToolsPanelClosedEvent.cs
public struct GizmoToolsPanelClosedEvent { }
```
```csharp
// Assets/_App/Scripts/VrInteraction/GizmoModeChangedEvent.cs
public struct GizmoModeChangedEvent { public GizmoMode Mode; }
```
```csharp
// Assets/_App/Scripts/VrInteraction/GizmoDragStartedEvent.cs
public struct GizmoDragStartedEvent { public string TargetNodeId; }
```
```csharp
// Assets/_App/Scripts/VrInteraction/GizmoDragEndedEvent.cs
public struct GizmoDragEndedEvent { public string TargetNodeId; }
```

- [ ] **Step 5.8: Delete the old aggregate file**

```
delete Assets/_App/_Shared/Events/AppEvents.cs
```

- [ ] **Step 5.9: Refresh, compile, verify**

Run: `refresh_unity(scope="all", compile="request", wait_for_ready=true)` then `read_console(types=["error"])`.
Expected: zero errors. If an error reports a duplicate definition of an event struct, the corresponding line was not removed from `AppEvents.cs` before deletion — confirm `AppEvents.cs` is gone.

---

## Task 6: Phase 2 — Flatten subsystem `Data/` subfolders

Pull each `Data/` DTO/SO-script up to its subsystem root, then the now-empty `Data/` folders remain for Phase 3 cleanup. Keep `VrInteraction/Gizmo/Strategies/` as-is. (SpatialUi `Data/` scripts already handled in Task 3.5.)

**Files:** moves only.

- [ ] **Step 6.1: AnimationAuthoring/Data → root**

```
move Assets/_App/Scripts/AnimationAuthoring/Data/ActionContainer.cs      → Assets/_App/Scripts/AnimationAuthoring/ActionContainer.cs
move Assets/_App/Scripts/AnimationAuthoring/Data/AnimKeyData.cs           → Assets/_App/Scripts/AnimationAuthoring/AnimKeyData.cs
move Assets/_App/Scripts/AnimationAuthoring/Data/AnimTrackData.cs         → Assets/_App/Scripts/AnimationAuthoring/AnimTrackData.cs
move Assets/_App/Scripts/AnimationAuthoring/Data/FrameClipboard.cs        → Assets/_App/Scripts/AnimationAuthoring/FrameClipboard.cs
move Assets/_App/Scripts/AnimationAuthoring/Data/FrameClipboardEntry.cs   → Assets/_App/Scripts/AnimationAuthoring/FrameClipboardEntry.cs
move Assets/_App/Scripts/AnimationAuthoring/Data/SceneAnimationData.cs    → Assets/_App/Scripts/AnimationAuthoring/SceneAnimationData.cs
```

- [ ] **Step 6.2: AssetBrowser/Data → root**

```
move Assets/_App/Scripts/AssetBrowser/Data/BuiltinLabAsset.cs   → Assets/_App/Scripts/AssetBrowser/BuiltinLabAsset.cs
move Assets/_App/Scripts/AssetBrowser/Data/DemoAssetCatalog.cs  → Assets/_App/Scripts/AssetBrowser/DemoAssetCatalog.cs
move Assets/_App/Scripts/AssetBrowser/Data/ImportedLabAsset.cs  → Assets/_App/Scripts/AssetBrowser/ImportedLabAsset.cs
move Assets/_App/Scripts/AssetBrowser/Data/SavedLabAsset.cs     → Assets/_App/Scripts/AssetBrowser/SavedLabAsset.cs
```

- [ ] **Step 6.3: SceneComposition/Data → root**

```
move Assets/_App/Scripts/SceneComposition/Data/CommandStack.cs      → Assets/_App/Scripts/SceneComposition/CommandStack.cs
move Assets/_App/Scripts/SceneComposition/Data/TransformCommand.cs  → Assets/_App/Scripts/SceneComposition/TransformCommand.cs
```

- [ ] **Step 6.4: ModeOrchestrator/Data, StorageCore/Data → root**

```
move Assets/_App/Scripts/ModeOrchestrator/Data/ModeTransitionGraph.cs → Assets/_App/Scripts/ModeOrchestrator/ModeTransitionGraph.cs
move Assets/_App/Scripts/StorageCore/Data/AssetCatalogData.cs         → Assets/_App/Scripts/StorageCore/AssetCatalogData.cs
move Assets/_App/Scripts/StorageCore/Data/NodeData.cs                 → Assets/_App/Scripts/StorageCore/NodeData.cs
move Assets/_App/Scripts/StorageCore/Data/SceneData.cs               → Assets/_App/Scripts/StorageCore/SceneData.cs
```

- [ ] **Step 6.5: VrInteraction Gizmo/UI → Gizmo root**

```
move Assets/_App/Scripts/VrInteraction/Gizmo/UI/GizmoToolsPanel.cs → Assets/_App/Scripts/VrInteraction/Gizmo/GizmoToolsPanel.cs
```

- [ ] **Step 6.6: Refresh, compile, verify**

Run: `refresh_unity(scope="all", compile="request", wait_for_ready=true)` then `read_console(types=["error"])`.
Expected: zero errors.

---

## Task 7: Phase 2 — Relocate the runtime asmdef into Scripts/

Now that every runtime `.cs` lives under `Scripts/`, move the umbrella so the runtime assembly == the `Scripts/` folder.

**Files:** move `_App.Runtime.asmdef`.

- [ ] **Step 7.1: Confirm no runtime `.cs` remain outside Scripts/**

Run: `manage_asset(action="search", path="Assets/_App", search_pattern="*.cs", page_size="200")`. Inspect results: every `.cs` must be under `Assets/_App/Scripts/`, `Assets/_App/Editor/`, or `Assets/_App/Tests/`. If any remain under `_App/_Shared/` or `_App/Subsystems/`, move them to the correct `Scripts/<Subsystem>/` first.

- [ ] **Step 7.2: Move the asmdef**

```
move Assets/_App/_App.Runtime.asmdef → Assets/_App/Scripts/_App.Runtime.asmdef
```

- [ ] **Step 7.3: Refresh, compile, verify**

Run: `refresh_unity(scope="all", compile="request", mode="force", wait_for_ready=true)` then `read_console(types=["error"])`.
Expected: zero errors. The umbrella now covers `Scripts/` only; `Editor/` and `Tests/` keep their own asmdefs; `_Shared/`/`Subsystems/` hold no `.cs`.

- [ ] **Step 7.4: CHECKPOINT — full verify (USER)**

Run `run_tests(mode="EditMode")` (expect prior pass set), then load each scene and check console for missing scripts:
```
manage_scene(action="load", path="Assets/_App/Scenes/Bootstrap.unity")
manage_scene(action="load", path="Assets/_App/Scenes/MainMenu.unity")
manage_scene(action="load", path="Assets/_App/Scenes/VrEditing.unity")
manage_scene(action="load", path="Assets/_App/Scenes/Sandbox.unity")
```
After each: `read_console(types=["error","warning"], filter_text="missing")`. Expected: no missing-script entries. **User confirms** before Task 8.

---

## Task 8: Phase 2 — Move owned assets into Content/

Create the Content/ tree, then move assets by GUID. Create each destination folder before moving into it. All moves preserve GUIDs so prefab/SO/material/scene references stay intact.

**Files:** create `Content/` tree; move prefabs, SOs, materials, models, textures, shaders.

- [ ] **Step 8.1: Create the Content tree**

```
create_folder Assets/_App/Content
create_folder Assets/_App/Content/Prefabs
create_folder Assets/_App/Content/Prefabs/UI
create_folder Assets/_App/Content/Prefabs/UI/Items
create_folder Assets/_App/Content/Prefabs/UI/Panels
create_folder Assets/_App/Content/Prefabs/UI/Panels/Static
create_folder Assets/_App/Content/Prefabs/UI/Panels/UserPanel
create_folder Assets/_App/Content/Prefabs/Gizmos
create_folder Assets/_App/Content/Prefabs/Assets
create_folder Assets/_App/Content/Prefabs/Environment
create_folder Assets/_App/Content/Prefabs/XR
create_folder Assets/_App/Content/ScriptableObjects
create_folder Assets/_App/Content/Materials
create_folder Assets/_App/Content/Materials/Gizmo
create_folder Assets/_App/Content/Models
create_folder Assets/_App/Content/Models/Characters
create_folder Assets/_App/Content/Models/Gizmos
create_folder Assets/_App/Content/Shaders
create_folder Assets/_App/Content/Textures
create_folder Assets/_App/Content/Textures/Checkers
create_folder Assets/_App/Content/Textures/Pbr
create_folder Assets/_App/Content/Textures/Icons
create_folder Assets/_App/Content/Textures/Misc
```

- [ ] **Step 8.2: Move UI prefabs (folder moves)**

```
move Assets/_App/Scripts/SpatialUi/Prefabs/Items   → Assets/_App/Content/Prefabs/UI/Items
move Assets/_App/Scripts/SpatialUi/Prefabs/Panels/Static    → Assets/_App/Content/Prefabs/UI/Panels/Static
move Assets/_App/Scripts/SpatialUi/Prefabs/Panels/UserPanel → Assets/_App/Content/Prefabs/UI/Panels/UserPanel
move Assets/_App/Scripts/AnimationAuthoring/UI/KeyframeMarker.prefab → Assets/_App/Content/Prefabs/UI/KeyframeMarker.prefab
```
(Note: SpatialUi prefabs rode along into `Scripts/SpatialUi/Prefabs/` during Task 3's folder context. If they are still at `Assets/_App/Subsystems/SpatialUi/Prefabs/...`, use that source path instead — confirm with a `search` first.)

- [ ] **Step 8.3: Move ScriptableObject instances (flat)**

```
move Assets/_App/DemoAssets/DefaultDemoAssetCatalog.asset                 → Assets/_App/Content/ScriptableObjects/DefaultDemoAssetCatalog.asset
move Assets/_App/Scripts/AnimationAuthoring/Data/DefaultAnimatorPanelConfig.asset → Assets/_App/Content/ScriptableObjects/DefaultAnimatorPanelConfig.asset
move Assets/_App/Scripts/AssetBrowser/Data/DefaultBuiltinAssetLibrary.asset → Assets/_App/Content/ScriptableObjects/DefaultBuiltinAssetLibrary.asset
move Assets/_App/Scripts/AssetBrowser/Data/NoRigsBuiltinAssetLibrary.asset  → Assets/_App/Content/ScriptableObjects/NoRigsBuiltinAssetLibrary.asset
move Assets/_App/Scripts/ModeOrchestrator/Data/DefaultModeTransitionGraph.asset → Assets/_App/Content/ScriptableObjects/DefaultModeTransitionGraph.asset
move Assets/_App/Scripts/SceneComposition/Data/DefaultGizmoConfig.asset      → Assets/_App/Content/ScriptableObjects/DefaultGizmoConfig.asset
move Assets/_App/Scripts/SpatialUi/Data/AnimatorPanelConfig.asset            → Assets/_App/Content/ScriptableObjects/AnimatorPanelConfig.asset
move Assets/_App/Scripts/SpatialUi/Data/DefaultNavBarConfig.asset            → Assets/_App/Content/ScriptableObjects/DefaultNavBarConfig.asset
move Assets/_App/Scripts/SpatialUi/Data/DefaultPanelRegistry.asset           → Assets/_App/Content/ScriptableObjects/DefaultPanelRegistry.asset
```
(Confirm each `.asset`'s current path with a `search` if a prior folder move relocated it; adjust the source accordingly.)

- [ ] **Step 8.4: Move Resources/ content into Content/ (per map)**

Materials (flatten Simple/ + triplanar .mat into Materials/):
```
move Assets/Resources/Materials/CheckerFloor_Blue.mat          → Assets/_App/Content/Materials/CheckerFloor_Blue.mat
move Assets/Resources/Materials/CheckerFloor_Neutral.mat       → Assets/_App/Content/Materials/CheckerFloor_Neutral.mat
move Assets/Resources/Materials/CheckerFloor_Neutralediting.mat → Assets/_App/Content/Materials/CheckerFloor_Neutralediting.mat
move Assets/Resources/Materials/CheckerFloor_Tests.mat         → Assets/_App/Content/Materials/CheckerFloor_Tests.mat
move Assets/Resources/Materials/crush_dummy_UE4.mat            → Assets/_App/Content/Materials/crush_dummy_UE4.mat
move Assets/Resources/Materials/crush_dummy_UE4_red.mat        → Assets/_App/Content/Materials/crush_dummy_UE4_red.mat
move Assets/Resources/Materials/NoSignal_Material.mat          → Assets/_App/Content/Materials/NoSignal_Material.mat
move Assets/Resources/Materials/PromeonBoneRenderer_Material.mat → Assets/_App/Content/Materials/PromeonBoneRenderer_Material.mat
move Assets/Resources/Materials/Simple/MainMenuPanel-Bg.mat    → Assets/_App/Content/Materials/MainMenuPanel-Bg.mat
move Assets/Resources/Materials/Simple/WhiteUnlit_Blue.mat     → Assets/_App/Content/Materials/WhiteUnlit_Blue.mat
move Assets/Resources/Materials/Simple/WhiteUnlit_Green.mat    → Assets/_App/Content/Materials/WhiteUnlit_Green.mat
move Assets/Resources/Materials/Simple/WhiteUnlit_Red.mat      → Assets/_App/Content/Materials/WhiteUnlit_Red.mat
move Assets/Resources/Materials/Simple/WhiteUnlit_Yellow.mat   → Assets/_App/Content/Materials/WhiteUnlit_Yellow.mat
move Assets/Resources/Materials/Shaders/triplanarSpecific/TriplanarBase_000.mat → Assets/_App/Content/Materials/TriplanarBase_000.mat
```
Shaders (shadergraph + its texture):
```
move Assets/Resources/Materials/Shaders/triplanarSpecific/URP_TriplanarSimplified_Promokot.shadergraph → Assets/_App/Content/Shaders/URP_TriplanarSimplified_Promokot.shadergraph
move Assets/Resources/Materials/Shaders/triplanarSpecific/CheckerBase.png → Assets/_App/Content/Shaders/CheckerBase.png
```
Models + gizmo materials:
```
move Assets/Resources/Models/Characters/crush_dummy_UE4_skinned.fbx → Assets/_App/Content/Models/Characters/crush_dummy_UE4_skinned.fbx
move Assets/Resources/Models/Gizmos/Gizmo_Move.fbx   → Assets/_App/Content/Models/Gizmos/Gizmo_Move.fbx
move Assets/Resources/Models/Gizmos/Gizmo_Rotate.fbx → Assets/_App/Content/Models/Gizmos/Gizmo_Rotate.fbx
move Assets/Resources/Models/Gizmos/Gizmo_Scale.fbx  → Assets/_App/Content/Models/Gizmos/Gizmo_Scale.fbx
move Assets/Resources/Models/Gizmos/Gizmo_Default.mat → Assets/_App/Content/Materials/Gizmo/Gizmo_Default.mat
move Assets/Resources/Models/Gizmos/Gizmo_Red.mat    → Assets/_App/Content/Materials/Gizmo/Gizmo_Red.mat
move Assets/Resources/Models/Gizmos/Gizmo_Green.mat  → Assets/_App/Content/Materials/Gizmo/Gizmo_Green.mat
move Assets/Resources/Models/Gizmos/Gizmo_Blue.mat   → Assets/_App/Content/Materials/Gizmo/Gizmo_Blue.mat
```
Prefabs:
```
move Assets/Resources/Prefabs/_User/User XR Origin (XR Rig).prefab → Assets/_App/Content/Prefabs/XR/User XR Origin (XR Rig).prefab
move Assets/Resources/Prefabs/_User/EventSystem.prefab             → Assets/_App/Content/Prefabs/XR/EventSystem.prefab
move Assets/Resources/Prefabs/Environment/FloorDefault.prefab      → Assets/_App/Content/Prefabs/Environment/FloorDefault.prefab
move Assets/Resources/Prefabs/Gizmos/SceneOriginGizmo.prefab       → Assets/_App/Content/Prefabs/Gizmos/SceneOriginGizmo.prefab
move Assets/Resources/Prefabs/Gizmos/Vr3D_Gizmos.prefab            → Assets/_App/Content/Prefabs/Gizmos/Vr3D_Gizmos.prefab
move Assets/Resources/Prefabs/AssetLibraryPrefabs/BuiltinLab_ObjectPrefabs → Assets/_App/Content/Prefabs/Assets
```
After moving the `BuiltinLab_ObjectPrefabs` folder, move its `icons/` out to Textures/Icons and remove the now-nested icons folder:
```
move Assets/_App/Content/Prefabs/Assets/icons → Assets/_App/Content/Textures/Icons
```
Textures:
```
move Assets/Resources/Textures/Checkers → Assets/_App/Content/Textures/Checkers
move Assets/Resources/Textures/Pbr      → Assets/_App/Content/Textures/Pbr
move Assets/Resources/Textures/Sprites  → Assets/_App/Content/Textures/Icons_Sprites
move Assets/Resources/Textures/Тo-signal-Иackground-Сolorful.jpg → Assets/_App/Content/Textures/Misc/Тo-signal-Иackground-Сolorful.jpg
```
(`Sprites` is moved as a folder to `Textures/Icons_Sprites`; if you want them merged flat into `Textures/Icons`, move the individual sprite files instead and delete the empty `Icons_Sprites` after. The target merges Sprites + builtin icons into `Textures/Icons/`.)

- [ ] **Step 8.5: Refresh, then CHECKPOINT — full scene verify (USER)**

Run `refresh_unity(scope="all", mode="force", wait_for_ready=true)`, then `read_console(types=["error"])` (expect zero). Then load all four scenes (Bootstrap, MainMenu, VrEditing, Sandbox) and `read_console(filter_text="missing")` after each. Expected: no missing references on floors, gizmos, XR rig, panels, or materials. **User confirms** assets render correctly (enter Play on Bootstrap if possible) before proceeding.

---

## Task 9 (optional): Tighten `SceneGraph` injections to `ISceneGraph`

Separable cleanup so the interface+DI pattern is consistent. Skip if you prefer to leave it.

**Files:** `Assets/_App/Scripts/SpatialUi/Views/SceneOutlinerView.cs`, `.../AnimatorPanelView.cs`, `.../SceneInspectorView.cs`

- [ ] **Step 9.1: Verify the interface covers the used members**

`read` `Assets/_App/Scripts/SceneComposition/ISceneGraph.cs` and confirm it declares every member these three views call on their `SceneGraph` field. If a called member is missing from `ISceneGraph`, either add it to the interface or leave that view on the concrete type. Do not change behavior.

- [ ] **Step 9.2: Swap field + Construct param types**

In each of the three files, change the field type and the `Construct(...)` parameter type from `SceneGraph` to `ISceneGraph` (keep the field name). DI registration already exposes `SceneGraph` `As<ISceneGraph>()` where the interface is used elsewhere — confirm the relevant `LifetimeScope` registers it that way; if not, leave as concrete.

- [ ] **Step 9.3: Refresh, compile, verify**

Run: `refresh_unity(compile="request", wait_for_ready=true)` then `read_console(types=["error"])`. Expected: zero errors. Then `run_tests(mode="EditMode")` — expect prior pass set.

---

## Task 10: Update documentation

**Files:** `CLAUDE.md`; `Assets/_App/Documentation/architecture_context.md`, `conventions.md`; `Assets/_App/Documentation/STRUCTURE.md`

- [ ] **Step 10.1: Update CLAUDE.md**

Edit the Architecture section to reflect: 3 assemblies (`_App.Runtime` in `Scripts/`, `_App.Editor`, `_App.Tests`); subsystems are folders under `Scripts/`, not assemblies; cross-subsystem boundaries are convention (interface+DI + `EventBus`), not asmdef-enforced; `_Shared/` no longer exists (contracts live with their owning subsystem; only `EventBus`+`ICommand` in `Scripts/Core/`); the event bus is the custom `EventBus`, **not** MessagePipe; tests live in `_App/Tests/<Subsystem>/`. Remove the now-false rules: "No `.asmdef` cross-references between subsystems — contracts flow through `_Shared`" and any MessagePipe claim.

- [ ] **Step 10.2: Update Documentation/*.md**

Apply the same corrections to `architecture_context.md` and `conventions.md`. Regenerate or hand-edit `STRUCTURE.md` to reflect the new `_App/{Scripts,Editor,Tests,Content,Scenes,Documentation}` tree.

- [ ] **Step 10.3: CHECKPOINT (USER)**

User reviews doc changes. No compile impact.

---

## Task 11: Phase 3 — Delete emptied folders (USER)

After Task 8's checkpoint passes, the old folders hold only empty subfolders and `.meta` files. Per the user's git/deletion preference, the **user** deletes these in Unity (or asks the executor to run the `manage_asset(action="delete")` calls).

- [ ] **Step 11.1: Delete emptied trees**

```
delete Assets/_App/_Shared
delete Assets/_App/Subsystems
delete Assets/_App/DemoAssets
```

- [ ] **Step 11.2: Empty Resources/ content (keep folder per target)**

Delete any leftover empty subfolders under `Assets/Resources/`. Leave `Assets/Resources/` itself present (reserved for future `Resources.Load`).

- [ ] **Step 11.3: Final verify**

`refresh_unity(scope="all", mode="force", wait_for_ready=true)` → `read_console(types=["error"])` (zero) → `run_tests(mode="EditMode")` (prior pass set) → load all four scenes, check `filter_text="missing"` (none). **User commits the completed migration.**

---

## Task 12 (optional, last): Move TextMesh Pro into UnityPacks/

Highest path-risk step; do only if you want it, and after everything else is verified.

- [ ] **Step 12.1: Move the folder**

```
create_folder Assets/UnityPacks/TextMesh Pro  (if UnityPacks exists)
move Assets/TextMesh Pro → Assets/UnityPacks/TextMesh Pro
```

- [ ] **Step 12.2: Verify TMP still loads**

`refresh_unity(mode="force", wait_for_ready=true)` → `read_console(types=["error","warning"])`. Load a scene with TMP text (MainMenu) and confirm fonts render (TMP resolves `Resources.Load("Fonts & Materials/...")` from the nested `Resources/` folder — Unity scans all `Resources/` folders regardless of location). If fonts break or settings reset, **move it back** to `Assets/TextMesh Pro`.

---

## Self-Review

**Spec coverage:** D1 (3 asmdef)→Tasks 1,7; D2 (Core as folder)→Steps 2.1-2.2; D3 (no namespaces)→all event files written without namespaces, no `using` rewrites; D4 (contracts to owners)→Tasks 4,5; D5 (interface+DI+events, optional cleanup)→Task 9 + preserved patterns; D6 (don't move XR/XRI/Composition; TMP optional)→Task 12 only, XR/XRI/Composition untouched; D7 (Resources→Content)→Task 8; D8 (assemblies first)→Task 1 before all moves; D9 (Claude via MCP, user verifies/deletes)→checkpoints + Task 11. All covered.

**Placeholder scan:** every event struct shown in full (Task 5); asmdef JSON shown in full; every move has exact source→dest. No "TODO"/"similar to"/"handle edge cases."

**Type consistency:** assembly names `_App.Runtime`/`_App.Editor`/`_App.Tests` used consistently in references and InternalsVisibleTo; event struct names/fields match the verified `AppEvents.cs`; `ContainerChange`/`KeyframeChange` consistently placed in AnimationAuthoring (Step 4.3) and referenced by `AnimationKeyframeChangedEvent` (Step 5.3).

**Known soft spots flagged inline (not gaps):** exact `manage_asset` path-prefix convention (confirm via dry-run, Conventions section); SpatialUi prefab source path depends on whether they rode along in Task 3 (Step 8.2 note); `ISceneGraph` member coverage gated by a read (Step 9.1).

**Discovered during execution (Task 1):** External asmdefs OUTSIDE `_App/` may reference the deleted subsystem assemblies and break (CS0246) when those assemblies are removed. Found: `Assets/UnityPacks/Keyboard Package/Scripts/KeyboardPackage.asmdef` referenced `Subsystems.SpatialUi` (uses our `VrKeyboard`); repointed to `_App.Runtime`. A project-wide grep of all `*.asmdef` for `"Subsystems.*"`/`"_Shared"`/`"_App"` found this as the ONLY external dangling reference. Re-run this grep if assembly names change again.
