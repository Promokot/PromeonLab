# PromeonLab — Backlog (planned, not yet implemented)

Features that are **specced, aspirational, or stubbed in code but not functional**. Derived from the
2026-06-01 full audit (`docs/superpowers/audit-2026-06-01/`). This is the single source of truth for
"what the docs/CLAUDE.md mention that the code does not actually do yet."

Status legend: **ABSENT** (no code) · **STUB** (placeholder type/field, no behavior) · **PARTIAL**
(some code, incomplete) · **DATA-ONLY** (serialized but nothing consumes it).

---

## AssetBrowser

| Feature | Status | Notes | Ref |
|---|---|---|---|
| **Asset thumbnails / preview generation** | ABSENT | `Icon` exists on records, but only `BuiltinLabAsset` carries an inspector-assigned sprite; `ImportedLabAsset.Icon`/`SavedLabAsset.Icon` return `null`. Imported/Saved cards render iconless. No preview-render pipeline. | audit 02 §4 |
| **Saved library spawn (Slice 3)** | PARTIAL | `SavedAssetLibrary` load/save/add/remove is built & wired, but `SavedLabAsset.Recipe => null` and `RestoreAsync` has no Saved branch → cannot spawn. No "save object from scene" producer either. | audit 02 §4 |
| **Drag-and-drop spawn** (`LabAssetCardDragHandler`) | ABSENT | Replaced by the explicit Spawn button. | audit 02 §4 |
| **`AssetPropertiesView` / per-type property prefabs** | ABSENT | `AssetBrowserPanel` shows a flat text blob instead. | audit 02 §4 |

## Animation

| Feature | Status | Notes | Ref |
|---|---|---|---|
| **NLA composition** (clip blending / action layering) | ABSENT | Mentioned historically; never specced in detail, zero code. | audit 04 §4 |
| **Loop playback** | ABSENT | `AnimationClock` hard-stops at `TotalFrames`. | audit 04 §4 |
| **Multi-container / master timeline** | ABSENT | Only one `ActionContainer` plays at a time (clock reconfigured per selection). | audit 04 §4 |
| **Undo/Redo for animation actions** | ABSENT | Key set/delete/remove mutate directly, bypassing `CommandStack` (out of scope in specs). | audit 04 §4 |
| **`SceneModifiedEvent` on key mutation** | ABSENT | Spec wanted every `SetKey` to also mark the scene dirty for `UnsavedChangesGuard`; not published. | audit 04 §4 |
| ~~First-Add-animation UI refresh~~ | ✅ FIXED 2026-06-01 | `OnContainerChanged` now handles `Added` before the `_activeOwner` guard (refreshes when the new owner matches the current selection). | audit 04 §6 |

## RigBuilder

| Feature | Status | Notes | Ref |
|---|---|---|---|
| **IK chains / FK solving** | DATA-ONLY | `RigDefinition.IkChains` + `IkChainRecord` round-trip in the recipe, but no solver, no constraint build, no extractor populates them. (No Animation Rigging dependency.) | audit 03 §4 |
| **Slice C — rig bake tools** (`RigBakeTool`, "Bake to Built-in Library") | ABSENT | Builtin rigs ship as bare skinned-mesh prefabs and are proxied at runtime; no edit-time bake. | audit 03 §4 |
| **`BoneRecord.TranslationLocked` (per-bone lock)** | DATA-ONLY | Serialized (defaults `true`), never read by build or interaction. | audit 03 §4 |
| **Stable rig `AssetId`** | PARTIAL | `RigDefinitionExtractor` stamps the temp-GO name instead of `record.Id` (harmless; mapping is by bone name). | audit 03 §4 |

## ExportPipeline

| Feature | Status | Notes | Ref |
|---|---|---|---|
| **FBX export** (Unity FBX Exporter SDK) | STUB | `ExportPipeline.cs` is an empty placeholder class. | audit 01/conv |
| **Custom JSON export** | STUB | Same placeholder. | — |

## ErrorHandling

| Feature | Status | Notes | Ref |
|---|---|---|---|
| **`ErrorDispatcher`** (Warning/Error/Critical, async error wrapping) | ABSENT | Only `ErrorLevel` enum + `ErrorOccurredEvent` exist; reporting goes straight to `Debug.Log*`. | convention-drift §2 |

## SpatialUi

| Feature | Status | Notes | Ref |
|---|---|---|---|
| **Detach → add-on split (`PanelDetachAddon`)** | ABSENT | `SpatialPanelDetachable` + `DetachablePanelDragHandle` remain monolithic. (Region-model "out of scope".) | audit 05 §4 |
| **General settings tab content** | STUB | `_generalContent` empty placeholder (explicit Non-Goal). | audit 05 §4 |
| **Runtime rebinding / settings persistence** | ABSENT | Explicit Non-Goal in the settings spec. | audit 05 §4 |
| **Keyboard close-on-submit** | PARTIAL | `VrKeyboard.SubmitWord` only nulls the target; keyboard closes only via its nav button. | audit 05 §6 |
| **`NavBarConfig` → `PanelRegionConfig` rename** | DEFERRED | Trivial follow-up; intentionally not done. | audit 05 §4 |

## Architecture / tech-debt (planned cleanups, not features)

| Item | Status | Notes | Ref |
|---|---|---|---|
| ~~Remove dead `PanelRegistry` / `UiPanelOrchestrator`~~ | ✅ DONE 2026-06-01 | Deleted `PanelRegistry.cs`, `UiPanelOrchestrator.cs`, `DefaultPanelRegistry.asset` + the `_panelRegistry`/`Register<UiPanelOrchestrator>` lines in both scene scopes (GUID sweep was clean). `PanelId.cs` is now fully dead (harmless) — optional later removal. Region model is the sole panel system. | audit 01 §4 |
| **Extract `BaseSceneScope`** | OPEN | `VrEditingSceneScope` and `SandboxSceneScope` are ~90% duplicated. | audit 01 §4, project-cleanup Task 3 |
| **Gizmo moves through `CommandStack`** | OPEN | `TransformCommand` has no constructors / no callers; gizmo drags commit but undo coverage of gizmo moves is unverified. | audit 01 §6 |
| **`AppMode.Debug` overlay** | UNUSED | Enum value declared, never wired. | audit 01 §6 |

---

### Open product questions (need a human decision)
1. **`PanelRegistry`** — abandon (delete) or revive (populate) the top-level mode-gated panel mechanism?
2. **Placeholder subsystems** — are `ExportPipeline`, `InputBindings`, `ErrorHandling` (dispatcher) still
   on the roadmap as real subsystems, or should the stubs be removed?
3. **Thumbnails** — runtime preview-render, or ship iconless imported/saved cards for now?
