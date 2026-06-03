# Project Review 2026-06-03 — Part 02: Roadmap (Completed vs Planned) + Centralized Unimplemented-Features List

**Scope:** Stages 2 & 3 of the review.
(2) A completed-vs-planned ledger per subsystem/feature, code-verified.
(3) One centralized list of every designed-but-unimplemented feature mentioned across the **live** docs (`CLAUDE.md`, `docs/BACKLOG.md`, `docs/superpowers/audit-2026-06-01/`), with exact locations, plus a "cut from main docs" checklist.

**Method:** Read `CLAUDE.md`, `docs/BACKLOG.md`, all 7 `audit-2026-06-01/*.md`, the historical `_archive/{specs,plans,reports}`, and `docs/reports/`. Spot-checked code under `Assets/_App/Scripts/` with ripgrep + targeted reads to confirm shipped vs absent (not doc-claims alone). Verification hits are noted inline.

> **Note on freshness:** the `audit-2026-06-01/*.md` files predate several 2026-06-02/03 commits. `CLAUDE.md` (and `BACKLOG.md`) are newer and already mark Loop, Interpolation, scrub preview, thumbnails, and the ZIP exporter as DONE. Where the audits still call those "absent," CLAUDE.md/BACKLOG.md + code win. This is flagged per-row.

---

## (A) Completed-vs-Planned Ledger

Status: **SHIPPED** (functional, class cited) · **PARTIAL** · **DATA-ONLY** (serialized, nothing consumes it) · **STUB** (type/field, no behavior) · **PLANNED/ABSENT** (no code).

### Architecture / Bootstrap / Core

| Feature | Status | Implementing class (code-verified) | Notes |
|---|---|---|---|
| VContainer Root→Scene scope hierarchy, DDOL root | SHIPPED | `RootLifetimeScope`, `VrEditingSceneScope`, `SandboxSceneScope`, `MainMenuSceneScope` | audit 01 §1 |
| `SceneContext` façade (6 nullable services) | SHIPPED | `Bootstrap/SceneContext.cs`, `SceneContextBinder.cs` | No `Rig` property (spec wanted one — stale) |
| EventBus `Publish/Subscribe` | SHIPPED | `Core/EventBus.cs` | |
| Mode policy + single-scene load behind HeadFade | SHIPPED | `ModeOrchestrator.cs`, `SceneTransitionRunner.cs`, `HeadFade.cs` | `ModeExitingEvent` before load confirmed |
| Schema-v3 inline migration | SHIPPED | `StorageCore/SceneSerializer.cs` (`Deserialize`) | No `StorageMigrator` (CLAUDE.md rule line is wrong) |
| CommandStack (undo only, max 30) | SHIPPED | `SceneComposition/CommandStack.cs` | **Redo = PLANNED/ABSENT** |
| `BaseSceneScope` extraction (de-dup scopes) | PLANNED/ABSENT | — | Scopes ~90% duplicated; cleanup not done |
| Remove dead `PanelRegistry`/`UiPanelOrchestrator` | PLANNED/ABSENT (still live) | `SpatialUi/PanelRegistry.cs`, `UiPanelOrchestrator.cs` | Dead-but-registered (BACKLOG marks the *delete* DONE 2026-06-01, but audit 01 §4 says still present — re-verify; conflicting status) |
| Gizmo move through `CommandStack` undo | PARTIAL | `SceneComposition/TransformCommand.cs` (exists) | Commits route via `GizmoController.CommitTransform`; audit 01 §6 flags `TransformCommand` ctor callers as unverified |
| `AppMode.Debug` overlay | STUB | `AppMode.cs` | Enum value, zero usages |

### StorageCore

| Feature | Status | Class | Notes |
|---|---|---|---|
| File I/O, JSON, PathProvider, versioned data | SHIPPED | `PathProvider.cs`, `SceneSerializer.cs` | |
| Bone-pose persistence (schema v3) | SHIPPED | `BonePose.cs`, `NodeData.BonePoses`, `ProxyRigRuntime.Capture/ApplyPoses` | audit 03 §1 |

### AssetBrowser / Import-Spawn

| Feature | Status | Class | Notes |
|---|---|---|---|
| 3 libraries (Builtin/Imported/Saved) + registry | SHIPPED | `BuiltinAssetLibrary`, `ImportedAssetLibrary`, `SavedAssetLibrary`, `AssetRegistry` | |
| Import pipeline (glTF/GLB + images) | SHIPPED | `ImportPipeline.cs`, `GltfImportHandler`, `ImageImportHandler`, `ImportWizardSurface` | |
| Build-once/restore-many entity builders | SHIPPED | `IAssetEntityBuilder`, `AssetEntityBuilderRegistry`, Object/Rig/Reference builders | renamed from spec's `IAssetSpawner` |
| Capability funnel | SHIPPED | `InteractionCapability.Apply` | spec called it `Attach` |
| Spawn (camera-forward) | SHIPPED | `AssetSpawner.cs` | |
| Builtin recipe bake (editor) | SHIPPED | `BuiltinRecipeBaker`, `BuiltinAssetLibraryEditor`, `ReferenceImagePrefabGenerator` | |
| **Import thumbnails** | SHIPPED (2026-06-02) | `ThumbnailRenderer.cs`, `ImportedLabAsset._thumbnailRef`, `AssetBrowserPanel.ResolveIcon` | audit 02 §4 still says "NOT implemented" — **stale**; BACKLOG marks DONE; code present |
| **Saved-library spawn (Slice 3)** | PARTIAL → effectively ABSENT | `SavedAssetLibrary` (persist works); `SavedLabAsset.Recipe => null` (verified `SavedLabAsset.cs:19`) | No restore branch, no save-from-scene producer |
| Saved-library thumbnails | PLANNED/ABSENT | `SavedLabAsset.ThumbnailRef => null` | blocked on Slice 3 |
| Drag-and-drop spawn (`LabAssetCardDragHandler`) | PLANNED/ABSENT | — | replaced by explicit Spawn button |
| `AssetPropertiesView` / per-type property prefabs | PLANNED/ABSENT | — | flat text blob instead |
| `IColliderStrategy`/`BoundsBoxColliderStrategy` | PLANNED/ABSENT | — | specced, never shipped; collider choice hardcoded |
| Record `Meta` field | PLANNED/ABSENT | — | role absorbed by `AssetEntityRecipe` |

### RigBuilder

| Feature | Status | Class | Notes |
|---|---|---|---|
| Runtime proxy-bone rig | SHIPPED | `RigEntityFactory.BuildProxyRig`, `ProxyRigRuntime`, `BoneFollower` | |
| Bone-mode UI / selector boxes | SHIPPED | `InspectorPanel`, `OutlinerPanel`, `BoneSelectorBoxPlanner` | |
| **IK chains / FK solving** | DATA-ONLY | `RigDefinition.IkChains`, `IkChainRecord` (verified in `RigDefinition.cs`) | No solver, no Animation Rigging (0 code hits) |
| Slice C — rig bake tools (`RigBakeTool`) | PLANNED/ABSENT | — | builtin rigs proxied at runtime |
| `BoneRecord.TranslationLocked` (per-bone lock) | DATA-ONLY | `BoneRecord.cs` (verified) | serialized, never read |
| Stable rig `AssetId` | PARTIAL | `RigDefinitionExtractor` | stamps temp-GO name (harmless) |

### Animation

| Feature | Status | Class | Notes |
|---|---|---|---|
| Per-ActionContainer keyframe authoring | SHIPPED | `AnimationAuthoring.cs`, `ActionContainer.cs`, `AnimTrackData.cs` | |
| Transport (scrub + play/pause + fps) | SHIPPED | `AnimationClock.cs` | single-shot |
| Linear/Stepped interpolation | SHIPPED (2026-06-02) | `InterpolationMode.cs`, `BuildClip` | audit 04 predates; CLAUDE.md/BACKLOG DONE |
| Scrub preview | SHIPPED (2026-06-02) | `AnimationAuthoring.OnFrameChanged` | audit 04 predates |
| Per-object Loop (background playback) | SHIPPED (2026-06-02) | `ActionContainer.Loop`, `AnimationAuthoring` (ITickable), `LoopFrameChangedEvent.cs` | audit 04 §4 says "not implemented" — **stale** |
| Continuous (fractional) playback sampling | SHIPPED | `AnimationClock.CurrentFrameContinuous`, `AnimationAuthoring.Tick` | |
| AnimatorPanel + AnimatorSub* modules | SHIPPED | `AnimatorPanel.cs` + `AnimatorSub*` | timeline since redesigned to unified `TimelineRow` (post-audit) |
| **NLA composition / master timeline** | PLANNED/ABSENT | — | 0 code hits; never specced in detail |
| Undo/Redo for animation actions | PLANNED/ABSENT | — | key mutations bypass `CommandStack` |
| `SceneModifiedEvent` on key mutation | PLANNED/ABSENT | — | spec wanted it; not published |

### ExportPipeline

| Feature | Status | Class | Notes |
|---|---|---|---|
| ZIP-bundle export (scene.json + models + textures) | SHIPPED (2026-06-02) | `SceneExporter.cs`, `SceneBundle.cs`, `ExportPanel.cs`, `ExportPipeline.cs` | nav-bar `exporter` tab |
| Custom JSON export | SHIPPED | `SceneBundle` (schemaVersion 1) | **one-way / not re-importable** by design |
| **Real FBX export** (Unity FBX Exporter SDK) | PLANNED/ABSENT | — | 0 code hits; bundle copies source `.glb` instead |
| **Geometry for builtin assets** | PLANNED/ABSENT | — | builtin → reference + `geometryMissing:true` (verified `geometryMissing` in `SceneExporter.cs`/`SceneBundle.cs`) |

### ErrorHandling

| Feature | Status | Class | Notes |
|---|---|---|---|
| `ErrorLevel` + `ErrorOccurredEvent` | SHIPPED (data only) | `ErrorHandling/ErrorLevel.cs`, `ErrorHandling.cs` | folder has only these 2 files |
| **`ErrorDispatcher`** (Warning/Error/Critical + async wrapping) | PLANNED/ABSENT | — | 0 code hits; reporting goes straight to `Debug.Log*` |

### InputBindings

| Feature | Status | Class | Notes |
|---|---|---|---|
| Controls vocabulary + Settings rendering | SHIPPED | `ControlsProfile.cs`, `ControlBinding`, `SettingsPanel.cs`, `BindingRow.cs` | |
| `InputBindings/InputBindings.cs` | STUB | (one-line comment) | placeholder façade |

### SpatialUi

| Feature | Status | Class | Notes |
|---|---|---|---|
| Region/navbar model (root-lifetime) | SHIPPED | `PanelRegionRouter.cs`, `NavBarConfig`, `RegionMember`, `RegionNavButton` | |
| UserPanel grab + triple-lock | SHIPPED | `UserPanel.cs`, `PanelGrabHandle.cs`, `UserPanelOpener.cs` | |
| VR keyboard (brain) | SHIPPED | `VrKeyboard.cs`, `VrInputFieldProxy.cs` | |
| Settings master-detail + md exporter | SHIPPED | `SettingsPanel.cs`, `ControlsProfileExporter.cs` | |
| **Detachable / floating panels** | STUB (neutralized) | `SpatialPanelDetachable.cs`, `DetachablePanelDragHandle.cs` | operational code commented out 2026-06-02; region router rules |
| `PanelDetachAddon` dock-vs-detach split | PLANNED/ABSENT | — | never built |
| General settings tab content | STUB | `SettingsPanel._generalContent` | explicit Non-Goal |
| Runtime rebinding / settings persistence | PLANNED/ABSENT | — | explicit Non-Goal |
| Keyboard close-on-submit | PARTIAL | `VrKeyboard.SubmitWord` | fires `onEndEdit` but panel hides only via nav button |
| `NavBarConfig`→`PanelRegionConfig` rename | PLANNED/DEFERRED | — | trivial follow-up |

### VrInteraction

| Feature | Status | Class | Notes |
|---|---|---|---|
| Direct-input model (tap=select/hold-trigger=rotate/hold-grip=move) | SHIPPED | `XRPromeonInteractable.cs` | single-select |
| Gizmo (Move/Rotate/Scale strategies) | SHIPPED | `GizmoController`, `GizmoActivator`, `GizmoHandle`, `Gizmo/Strategies/*` | |
| Contextual cast-masks | SHIPPED | `InteractionMaskBinder.cs`, `InteractionLayers.cs` | |
| QuickOutline (patched, per-instance stencil, priority layering) | SHIPPED | `ThirdParty/QuickOutline/Outline.cs`, `OutlineConfig.cs`, `Selectable.cs` | |
| `GroupDragStrategy` (multi-grab seam) | PLANNED/ABSENT | — | intentional non-goal |

---

## (B) Centralized Unimplemented-Features List

The single consolidated list of everything DESIGNED/INTENDED but NOT functional, drawn from the live docs. Each row: feature, 1–2 line description, exact doc location(s), code-verification.

| # | Feature | Description | Mentioned in (live docs) | Code-verified |
|---|---|---|---|---|
| 1 | **NLA composition / master timeline** | Clip blending / action layering; a master timeline scrubbing & composing several actions together. Today: one selected container drives the transport; multiple *looped* containers play in background, but no composition. | `CLAUDE.md:32` (VR Workflow); `CLAUDE.md:62` ("No NLA / master timeline yet"); `BACKLOG.md:26` (NLA composition ABSENT); `BACKLOG.md:28` (Master timeline / NLA PARTIAL); audit 04 §4 | 0 hits for `NLA`/`AnimationEvaluator` in `Scripts/` |
| 2 | **Real FBX export** | True FBX re-encoding via Unity FBX Exporter SDK. Today: ZIP bundle copies the source `.glb`; no runtime FBX SDK. | `CLAUDE.md:16` ("FBX import is not supported at runtime"; export-side noted); `CLAUDE.md:33` ("Real FBX export still planned"); `CLAUDE.md:63` ("Real FBX / richer JSON still planned"); `BACKLOG.md:50`; audit 01/conv | 0 hits for FBX export class |
| 3 | **Geometry export for builtin assets** | Builtin/primitive nodes export as reference + `geometryMissing:true`; no mesh-bake (runtime meshes often `isReadable:false`). | `CLAUDE.md:63` ("flagged `geometryMissing`"); `BACKLOG.md:50-51`; (Data Storage note `CLAUDE.md`) | `geometryMissing` present in `SceneExporter.cs`/`SceneBundle.cs` (it's the *gap* flag) |
| 4 | **Export bundle re-import** | `scene.json` (`SceneBundle`) is one-way; the bundle cannot be re-imported into the app (format for an external tool). | `CLAUDE.md:63` ("one-way / not re-importable"); `CLAUDE.md` Data-Storage note; `BACKLOG.md:49` | by-design limitation |
| 5 | **IK chains / FK solving (Animation Rigging)** | `IkChains`/`IkChainRecord` round-trip in the recipe but no solver, no constraint build, no extractor populates them. | `CLAUDE.md:61` ("IK chains serialized but no solver consumes them yet (no Animation Rigging)"); `BACKLOG.md:39`; audit 03 §4 | `IkChains` only in `RigDefinition.cs`; 0 hits for AnimationRigging/solver |
| 6 | **`BoneRecord.TranslationLocked` (per-bone lock)** | Serialized (defaults `true`), never read by build or interaction. | `BACKLOG.md:41`; audit 03 §4 | field in `BoneRecord.cs`, no reader |
| 7 | **Slice C — rig bake tools** (`RigBakeTool`, "Bake to Built-in Library") | Edit-time bake of proxies into prefabs; builtin rigs are proxied at runtime instead. | `BACKLOG.md:40`; audit 03 §4 | absent |
| 8 | **`ErrorDispatcher`** | Warning/Error/Critical dispatch + async error wrapping. Today only `ErrorLevel` enum + `ErrorOccurredEvent`; reporting goes to `Debug.Log*`. | `CLAUDE.md:68` ("`ErrorDispatcher` is not implemented"); `CLAUDE.md` ErrorHandling subsystem row; `BACKLOG.md:57`; convention-drift §2 | `ErrorHandling/` has only `ErrorLevel.cs` + `ErrorHandling.cs` |
| 9 | **Saved-library spawn (Slice 3)** | `SavedAssetLibrary` persists, but `SavedLabAsset.Recipe => null` and `RestoreAsync` has no Saved branch → cannot spawn. No "save object from scene" producer. | `CLAUDE.md:95` ("spawn-from-saved/Slice 3 not yet implemented"); `CLAUDE.md:108` ("Saved … not yet implemented"); `BACKLOG.md:18`; audit 02 §4 | `SavedLabAsset.cs:19` `Recipe => null` |
| 10 | **Saved-library thumbnails** | Nothing to render until Slice 3 spawn exists. | `BACKLOG.md:17` | `SavedLabAsset.ThumbnailRef => null` |
| 11 | **Redo** | `CommandStack` is undo-only (max 30); no redo stack. | `CLAUDE.md:60` ("undo only, max 30 — no redo"); audit 01 §1 | `CommandStack.cs` undo-only; 0 `Redo` hits |
| 12 | **Undo/Redo for animation actions** | Key set/delete/remove mutate directly, bypassing `CommandStack`. | `BACKLOG.md:31`; audit 04 §4 | no `ICommand` integration in `AnimationAuthoring` |
| 13 | **`SceneModifiedEvent` on key mutation** | Spec wanted every `SetKey` to mark the scene dirty for `UnsavedChangesGuard`; not published. | `BACKLOG.md:32`; audit 04 §4 | not published |
| 14 | **Detachable / floating panels** (`SpatialPanelDetachable`, `DetachablePanelDragHandle`, `PanelDetachAddon`) | Detach/float UX wired into no live UX; code commented out (inert) 2026-06-02; region router governs panels. | `CLAUDE.md:67` (SpatialUi row lists `SpatialPanel: BodyLocked/WorldFixed/Free`); `BACKLOG.md:63`; audit 05 §1/§4 | `SpatialPanelDetachable.cs` neutralized |
| 15 | **General settings tab content** | `_generalContent` empty placeholder (explicit Non-Goal). | `BACKLOG.md:64`; audit 05 §4 | `SettingsPanel._generalContent` empty |
| 16 | **Runtime rebinding / settings persistence** | Explicit Non-Goal in the settings spec; bindings are read-only SO. | `BACKLOG.md:65`; audit 05 §4 | absent |
| 17 | **Keyboard close-on-submit** | Keyboard panel hides only via its nav button, not on word submit. | `BACKLOG.md:66`; audit 05 §6 | `VrKeyboard.SubmitWord` no close |
| 18 | **`NavBarConfig` → `PanelRegionConfig` rename** | Trivial deferred follow-up. | `BACKLOG.md:67`; audit 05 §4 | `NavBarConfig` kept |
| 19 | **Drag-and-drop spawn** (`LabAssetCardDragHandler`) | Grab-card→release-outside-panel spawn; replaced by explicit Spawn button. | `BACKLOG.md:19`; audit 02 §4 | absent |
| 20 | **`AssetPropertiesView` / per-type property prefabs** | Per-type asset property UI; flat text blob shown instead. | `BACKLOG.md:20`; audit 02 §4 | absent |
| 21 | **`BaseSceneScope` extraction** | De-dup ~90%-identical `VrEditing`/`Sandbox` scopes. (tech-debt, not a feature) | `BACKLOG.md:74`; audit 01 §4 | no `BaseSceneScope.cs` |
| 22 | **Gizmo moves through `CommandStack`** | `TransformCommand` ctor/callers unverified; gizmo-move undo coverage uncertain. (tech-debt) | `BACKLOG.md:75`; audit 01 §6 | `TransformCommand.cs` exists; caller path unverified |
| 23 | **`AppMode.Debug` overlay** | Enum value declared, never wired. (tech-debt) | `BACKLOG.md:76`; audit 01 §6 | `AppMode.cs` unused value |
| 24 | **Remove dead `PanelRegistry`/`UiPanelOrchestrator`** | Dead-but-registered top-level panel mechanism. **Status conflict:** `BACKLOG.md:73` marks the delete ✅ DONE 2026-06-01, but audit 01 §4 says still present/registered. Needs a re-check. | `BACKLOG.md:73,81`; audit 01 §4 | `PanelRegistry.cs`/`UiPanelOrchestrator.cs` still on disk — re-verify registration |
| 25 | **Multi-grab / `GroupDragStrategy`** | Reusable group-drag seam; intentional non-goal (single-select). | audit 06 §4 | absent |
| 26 | **`IColliderStrategy` / `BoundsBoxColliderStrategy`** | Specced collider-strategy objects; collider choice hardcoded into recipe cores instead. | audit 02 §3 | absent |

> Open product questions (need a human decision), from `BACKLOG.md:80-85`: (1) abandon vs revive `PanelRegistry`; (2) are `ExportPipeline`/`InputBindings`/`ErrorHandling`(dispatcher) still roadmap subsystems or remove stubs; (3) saved/imported iconless cards. (Item 3 is now resolved by thumbnails shipping for Imported.)

**Total distinct unimplemented/aspirational items: 26** (≈22 product features + 4 tech-debt cleanups). The headline gaps are **NLA / master timeline**, **real FBX export**, **IK solver (Animation Rigging)**, **`ErrorDispatcher`**, **Saved-library spawn (Slice 3)**, and **Redo** (both scene and animation).

---

## (C) "Mentions to cut from main docs" checklist

`docs/BACKLOG.md` is the central home. The goal: every "planned / not yet / no solver yet" qualifier should live in BACKLOG only, and `CLAUDE.md`'s architecture description should describe the *current* state cleanly, pointing to BACKLOG once. **These are proposals only — no edits applied.** Each item gives the exact text to cut/trim, its location, and where it goes.

### C.1 — Already correctly homed (KEEP, do not cut)

These CLAUDE.md mentions already point to BACKLOG and are short; they are the intended "one pointer" pattern. Leave as-is:

- `CLAUDE.md:37` — `> For features specced/aspirational but **not yet implemented**, see docs/BACKLOG.md.` (this is the canonical pointer — keep.)
- `CLAUDE.md:60` — `(undo only, max 30 — **no redo**)` — factual current-state, keep.
- `CLAUDE.md:61` — `**IK chains are serialized but no solver consumes them yet** (no Animation Rigging)` — accurate current-state; could trim "yet" but acceptable.
- `CLAUDE.md:68` — ErrorHandling row `**ErrorDispatcher is not implemented**` — accurate; already says "see docs/BACKLOG.md".

### C.2 — Trim the "planned" tail; keep the current-state fact (CLAUDE.md)

| Location | Exact text today | Proposed cut / trim | Goes to |
|---|---|---|---|
| `CLAUDE.md:32` | "...keyframe authoring on a per-`ActionContainer` timeline; **NLA composition is planned, not yet built — see `docs/BACKLOG.md`**)" | Cut the clause "`; NLA composition is planned, not yet built — see docs/BACKLOG.md`" (the workflow bullet should describe what VR Editing *does*; the NLA gap is item #1 in BACKLOG). | BACKLOG.md → Animation (#1, already present `:26-28`) |
| `CLAUDE.md:33` | "...reachable from the `exporter` nav-bar tab. **Real FBX export still planned (see `docs/BACKLOG.md`)**" | Cut "`Real FBX export still planned (see docs/BACKLOG.md)`". | BACKLOG.md → ExportPipeline (#2, already `:50`) |
| `CLAUDE.md:62` | "...handles **scrub** while paused. **No NLA / master timeline yet** (see `docs/BACKLOG.md`). UI: `AnimatorPanel`..." | Cut "`No NLA / master timeline yet (see docs/BACKLOG.md). `" | BACKLOG.md → Animation (#1, already `:26-28`) |
| `CLAUDE.md:63` | "...`ExportPanel` on the `exporter` nav-bar tab (`ExportModule.prefab`). **Real FBX / richer JSON still planned (see `docs/BACKLOG.md`)**" | Cut "`Real FBX / richer JSON still planned (see docs/BACKLOG.md)`". (richer JSON is implied; FBX is BACKLOG #2.) | BACKLOG.md → ExportPipeline (#2). Consider adding a "richer JSON export" sub-bullet to BACKLOG. |
| `CLAUDE.md:95` (Data-Storage tree comment) | "`saved-lib.json (Saved-library records; persisted, but spawn-from-saved/Slice 3 not yet implemented)`" | Trim to "`saved-lib.json (Saved-library records)`"; drop the "spawn-from-saved/Slice 3 not yet implemented" parenthetical. | BACKLOG.md → AssetBrowser (#9, already `:18`) |
| `CLAUDE.md:108` (Data-Storage prose) | "...`Saved` is a distinct, scene-origin flow (manual save-out), **not yet implemented**." | Cut "`, not yet implemented`" or the whole final sentence (Slice 3 is BACKLOG #9). | BACKLOG.md → AssetBrowser (#9) |

> The `geometryMissing` and "one-way / not re-importable" notes on `CLAUDE.md:63` describe *current export behavior*, not a roadmap promise — these can stay as factual current-state (they're also captured as BACKLOG #3/#4). Optional: trim them too for brevity, but they are not "planned-feature" leakage.

### C.3 — Audit-2026-06-01 sentences that are now STALE (contradict shipped code)

These live-doc sentences say a feature is unimplemented when it has since shipped. They should be corrected or struck so the audit no longer misleads. (Audits are historical snapshots; if kept, add a "superseded by 2026-06-02" banner rather than silent edit.)

| Location | Stale quote | Reality | Action |
|---|---|---|---|
| `audit-2026-06-01/02-asset-browser.md` §4 (line ~78) | "**THUMBNAILS — CONFIRMED NOT IMPLEMENTED.** `grep -i thumbnail` … = 0 hits." | Thumbnails shipped 2026-06-02 (`ThumbnailRenderer.cs`). | Strike or banner; move nothing (BACKLOG already marks DONE `:16`). |
| `audit-2026-06-01/04-animation.md` §1/§3/§4 (lines ~31, ~88, ~123-125) | "Clock … **NO loop**"; "no NLA and **no loop** anywhere (grep: zero hits … `loop`)"; "**Loop playback** … still not implemented" | Per-object Loop shipped 2026-06-02 (`ActionContainer.Loop`, `LoopFrameChangedEvent.cs`). | Strike loop claims; **keep** the NLA/master-timeline claim (still true). |
| `audit-2026-06-01/04-animation.md` §1 (line ~57) | "Multi-container simultaneous playback, loop, and NLA are absent." | Loop + concurrent looped-container background playback now ship; only NLA/master-timeline remain absent. | Trim to "NLA/master-timeline absent". |
| `audit-2026-06-01/01-architecture.md` §4 / `BACKLOG.md:73` conflict | audit: "PanelRegistry/UiPanelOrchestrator still exists … still registered"; BACKLOG: "✅ DONE 2026-06-01 … Deleted PanelRegistry.cs …" | `PanelRegistry.cs`/`UiPanelOrchestrator.cs` **still on disk** (verified). The two live docs disagree. | **Re-verify** registration in `VrEditingSceneScope`/`SandboxSceneScope`; then make BACKLOG and audit agree (this is unimplemented-list item #24). |

> Audit §3 "Drift" entries (storage-path `asset-library` vs `asset-libraries`, `AnimationClock` root-vs-scene, `IAssetSpawner` naming, `ActionData`/`AnimationPlayback` fictional types, `FrameChanged → AnimationEvaluator/TrackRecorder`) are CLAUDE.md *accuracy* bugs, not unimplemented-feature leakage — they belong to **Part 01 (doc-accuracy)** of this review, not the roadmap. Flagged here only so they aren't double-counted as "planned features."

---

### Appendix — Verification commands run

- `rg "class ErrorDispatcher|Redo|IkSolver|AnimationRigging|MultiParentConstraint|FbxExport"` over `Scripts/` → **0 hits** (confirms #2, #5, #8, #11).
- `rg "IkChains|TranslationLocked|geometryMissing|Loop|InterpolationMode"` → present in `RigDefinition.cs`/`BoneRecord.cs` (data-only #5/#6), `SceneExporter.cs`/`SceneBundle.cs` (#3), `ActionContainer.cs`/`AnimationAuthoring.cs`/`LoopFrameChangedEvent.cs` (Loop SHIPPED), `InterpolationMode.cs` (SHIPPED).
- `SavedLabAsset.cs:19` → `Recipe => null` (confirms #9).
- `ErrorHandling/` dir → only `ErrorLevel.cs` + `ErrorHandling.cs` (confirms #8).
- `SceneExporter.cs`, `ThumbnailRenderer.cs`, `ProxyRigRuntime.cs`, `SceneAutoSaver.cs`, `InteractionMaskBinder.cs` → all present (confirm those rows SHIPPED).
