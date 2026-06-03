# Phase 1 — Stage-5 Refactors — Design Spec

**Date:** 2026-06-03 · **Branch:** `review-2026-06-03` · **Source:** `docs/review-2026-06-03/04-responsibility-and-duplication.md` + decisions in `09-decisions-and-action-plan.md`.

Phase 1 implements the selected Stage-5 refactors: **A1, B1, A2, B2, A4, B6** (with **B3** and **B4** folded into A1). Read-only analysis is done; this spec is the agreed design. Implementation follows in a separate plan (writing-plans).

**Cross-cutting rules:**
- Runtime code has no namespaces; files live in their subsystem folder under `Scripts/`.
- Commit per logical block (user authorized commits; no AI-trace trailers).
- After each block: `refresh_unity` → `read_console` (no errors/missing refs) → run affected tests (MCP `run_tests`).
- Test strategy = **hybrid**: re-point each test to the new owner type; CRUD tests stay on the façade.
- Archiving uses Unity AssetDatabase move into `Assets/_App/Scripts/_Archive/` (GUIDs preserved; verify via Glob — `manage_asset move` returns false-but-succeeds).

---

## A1 — Split `AnimationAuthoring` (4-way)

`AnimationAuthoring.cs` (726 lines) holds 6 concerns. Split into four types in `Scripts/Animation/`. **The façade keeps the full public API**, so consumers (`AnimatorPanel`, `SceneContext.Authoring`, `SceneExporter.CaptureForExport`) are unaffected.

### `AnimationClipBaker` (static class, no DI)
- **Does:** bakes a track into a legacy `AnimationClip` and applies interpolation tangents.
- **API:** `static AnimationClip BuildClip(AnimTrackData track, int fps, InterpolationMode mode)`; `static void ApplyInterpolation(AnimationCurve curve, InterpolationMode mode)`. Callers invoke statically (matches the current static methods) — no DI registration.
- **Deps:** none. Moves verbatim from `AnimationAuthoring` lines 667–725.
- **Tests:** `AnimationAuthoringInterpolationTests` → re-point to `AnimationClipBaker`.

### `AnimationStorage` (persistence; **B4 inside**)
- **Does:** loads/saves `animation.json`, owns the debounced save.
- **API:** `Task<SceneAnimationData> LoadAsync(string sceneId, CancellationToken ct)`; `void RequestSave(SceneAnimationData data, string sceneId)` (200 ms debounce) → internal `SaveAsync`. `IDisposable` cancels the debounce CTS.
- **Deps:** `PathProvider`.
- **B4 (non-destructive):** on `schemaVersion < 2` **or** `> 2`, return a fresh `SceneAnimationData` and **leave the file untouched** (remove the current `File.Delete` of v1 files); one consistent log line. `SceneSerializer` is **not** touched.
- **Tests:** new `AnimationStorageTests` (load fresh / save round-trip / old-version non-destructive / newer-version untouched). Re-point the load-related parts of `AnimationAuthoringSceneFpsTests` if needed.

### `AnimationPlaybackSampler` (`ITickable`; **B3 inside**)
- **Does:** all runtime sampling — background loops and transport playback — plus scrub.
- **Owns:** `_clips` (active-container baked clips), `_loopCursors`/`_loopClips`/`_loopLastFrame`, `_activeContainerOwner`. Holds a reference to the live `SceneAnimationData` provided by the façade.
- **API:** `Tick()`; `SetActiveContainerOwner(string owner)`; `StartLoopPlayback(owner, startFrame)`; `StopLoopPlayback(owner)`; `SetData(SceneAnimationData data)`; `OnDataChanged(string owner)` (rebuild active/loop clips). Subscribes `FrameChangedEvent` (scrub via the unified sampler) and `PlaybackStateChangedEvent`; publishes `LoopFrameChangedEvent`.
- **B3 (unify sampling):** one private `Sample(ActionContainer c, Dictionary<string,AnimationClip> clips, float seconds)`. The scrub path (was `ApplyFrame`, integer → `frame/fps`) and the play/loop path (was `SampleContainerAt`, fractional) both call it. Eliminates the scrub↔play pose jump.
- **Deps:** `AnimationClock`, `ISceneGraph`, `EventBus` (calls `AnimationClipBaker` statically).
- **Tests:** `AnimationAuthoringLoopTests`, `AnimationAuthoringLoopFrameTests`, `AnimationAuthoringLoopRefreshTests`, `AnimationAuthoringLiveTrackTests`, and the sampling parts of `AnimationClockTests` → re-point to `AnimationPlaybackSampler` (keep the internal test hooks they rely on, e.g. `AdvanceLoopCursor`, `PublishLoopFrameIfChanged`, on the Sampler).

### `AnimationAuthoring` (façade; `IStartable`, `IDisposable`)
- **Does:** owns `SceneAnimationData` + `_activeContainerOwner`; all keyframe/container CRUD; event publishing; orchestrates Storage + Sampler.
- **Keeps the public surface:** `CreateContainer`, `EnsureTrack`, `RemoveContainer`, `SetKey`(×2), `DeleteKey`, `SetKeyForFrame`(+`_Test`), `DeleteAllKeysAtFrame`, `CopyFrame`/`PasteFrame`, `SetTotalFrames`/`SetFps`/`SetInterpolation`/`SetLoop`/`SetSceneFps`, queries (`HasContainer`/`GetContainer`/`HasKey`/`GetKeyFrames`/`NearestKeyBefore`/`NearestKeyAfter`/`GetSceneFps`/`GetInterpolation`/`IsLooping`/`IsLoopPlaying`), `CaptureForExport`, static `OwnerOf`, `InitForTest`.
- **Flow:** on mutation → publish event(s) (unchanged) + `_sampler.OnDataChanged(owner)`. On `Start` → `_storage.LoadAsync` → `_sampler.SetData(data)`. Save via `_storage.RequestSave(_data, _sceneId)`.
- **Tests:** CRUD tests (`AnimationAuthoringCreateContainerTests`, `...DeleteKeyTests`, `...EnsureTrackTests`, `ActionContainerTests`, `AnimationDataTests`, `AnimationAuthoringExportTests`, `AnimationAuthoringCompletionTests`) stay on the façade.

### DI (`VrEditingSceneScope`)
Register `AnimationStorage` (`Scoped`, `AsImplementedInterfaces().AsSelf()` so its `IDisposable` is disposed with the scope), `RegisterEntryPoint<AnimationPlaybackSampler>(Scoped).AsSelf()` (it is the `ITickable`), `RegisterEntryPoint<AnimationAuthoring>(Scoped).AsSelf()` (unchanged). `AnimationClipBaker` is static — no registration. `SceneContext.Authoring` keeps pointing to the façade.

### Build order (incremental, commit each)
`AnimationClipBaker` → `AnimationStorage` → `AnimationPlaybackSampler` → façade re-wire.

---

## B1 — Single bone-diamond mesh builder
In `RigEntityFactory`: add `static readonly` base-vertex (`Vector3[]`) and triangle-index (`int[]`) constants, and `private static void AppendDiamond(List<Vector3> verts, List<int> tris, Quaternion rot, float length, float width)`. `BuildOrientedDiamondMesh` (`:217-241`) and `BuildCombinedDiamondMesh` (`:243-279`) both call it; the only per-call difference is rotation/length. Covered by `RigEntityFactoryBuildProxyTests`.

---

## A2 — Split `GizmoActivator`
Extract two collaborators from `GizmoActivator.cs` (`VrInteraction/Gizmo/`):
- **`GizmoHighlightPainter`** — owns `GizmoPart[]`, the capture of base/emission colors, and `Recolor`/`Darken`/`Restore` (pure visual, ~160 lines).
- **`GizmoDragSession`** — owns `_dragActive`, the original-pose snapshot, target-follow switch, and drag-end handling.

`GizmoActivator` shrinks to spawn/visibility wiring + delegating hover/grab callbacks. Tests `GizmoActivatorStateTests`, `GizmoDragSliderTests`, and the strategy tests stay green (re-point only what moves). **Run A2 before B2** — B2 removes the transform-commit that currently lives in the drag-end path (now in `GizmoDragSession`). The `GizmoActivator → GizmoDriver` rename is **Phase 2**, not here.

---

## B2 — Remove the undo subsystem entirely

Decision: **full removal** (transforms were the only undoable action; `TransformCommand` is the only `ICommand`). Investigation confirmed `UndoKeyHandler`'s GUID is **not present in any scene/prefab** — it is never attached, so keyboard-undo is already inert and **no scene/prefab edit is needed**.

### Archive (move to `_Archive`; all GUID-clean vs prefabs/scenes)
- `SceneComposition/TransformCommand.cs` (only `ICommand`)
- `Core/ICommand.cs` (no remaining implementers)
- `SceneComposition/CommandStack.cs`
- `Bootstrap/UndoKeyHandler.cs` (MonoBehaviour, attached nowhere)
- `VrInteraction/GizmoController.cs` (pure command bridge; its `_target`/`SelectionChanged` tracking is never read)

### Cut tests
- `SceneComposition/CommandStackTests.cs` (orphaned once `CommandStack` is gone).

### Edit (remove the now-dangling references)
- `XRPromeonInteractable.cs`: drop `_gizmoController` field, the `Construct` param, and the two `CommitTransform` calls (`:172`, `:185`).
- `GizmoActivator.cs` / `GizmoDragSession` (post-A2): drop `_gizmoController` + the drag-end `CommitTransform` (`:444`).
- `SceneContext.cs`: remove `Commands` and `Gizmo` properties + their `Bind`/constructor params (no external readers found).
- `SceneContextBinder.cs`: remove `Resolve<CommandStack>()` and `Resolve<GizmoController>()`.
- `VrEditingSceneScope.cs` + `SandboxSceneScope.cs`: remove `CommandStack` + `GizmoController` registrations and the `UndoKeyHandler` find/inject blocks.

### Behavior
Transforms are applied live during drag and the autosaver snapshots the live scene graph on `ModeExiting`, so removing the commit path loses nothing functionally. Ctrl-Z (already inert) is gone. `GizmoDragStartedEvent`/`GizmoDragEndedEvent` remain (other subscribers/visuals); only `UndoKeyHandler`'s subscription disappears.

### Doc obligation
`CLAUDE.md` documents `CommandStack` (SceneComposition row, "undo only, max 30"), `Core/ICommand.cs` as one of two generic primitives, and "all user-reversible actions go through `CommandStack`". These become stale → fold the corrections into the **Stage-3** doc consolidation (not done in Phase 1).

---

## A4 — Extract `BoneEditMode` service
A scene-scoped service owning bone-edit-mode state, replacing the private `_activeBoneRigId` in `InspectorPanel` and the duplicated `_boneModeRig` tracking in `AnimatorPanel`.

- **Placement:** `Scripts/RigBuilder/BoneEditMode.cs`. Registered in **both** `VrEditingSceneScope` and `SandboxSceneScope` (`InspectorPanel` lives in both).
- **Deps:** `ISelectionManager`, `ISceneGraph`, `EventBus`. `ProxyRigRuntime` is a per-rig MonoBehaviour reached via `GetComponentInChildren` (as `InspectorPanel` already does), not injected.
- **Owns:** `_activeRigId` + the enter/exit transition (deselect/reselect, `ProxyRigRuntime.SetBonesInteractive`, publish `BonesVisibilityChangedEvent`).
- **API:** `void Toggle(string rigNodeId)`, `void Enter(string rigNodeId)`, `void Exit()`, `bool IsActive`, `string ActiveRigId`.
- **Consumers:** `InspectorPanel` calls `boneEditMode.Toggle(...)` (its `OnShowBonesToggleChanged` delegates). `AnimatorPanel` (root-scoped — cannot inject a scene service) keeps observing `BonesVisibilityChangedEvent` via the bus.
- **Tests:** new `BoneEditModeTests` (enter/exit/toggle state + event publication).

---

## B6 — Share the Reference recipe
Add `static AssetEntityRecipe RecipeFromImage(int width, int height)` to `ReferenceEntityBuilder` (mirroring `ObjectEntityBuilder`/`RigEntityBuilder` `RecipeFromInstance`). The runtime builder and the editor `ReferenceImagePrefabGenerator` both call it, so reference collider size / `spawnOffset` / aspect constants have a single source. Covered by `ReferenceEntityFactoryQuadTests`, `ReferenceEntityBuilderTests`, `ImportedLabAssetRecipeTests`.

---

## Execution order & verification
`A1 (Baker → Storage → Sampler → façade) → B1 → A2 → B2 → A4 → B6`.
After each block: `refresh_unity` + `read_console` (zero errors/missing refs) + `run_tests` on the affected assembly; commit the block. Final pass: full `run_tests` EditMode, console clean.

## Out of scope (this phase)
A3, A5, B5, B7, B8 (not selected); all Stage-6 renames (Phase 2); Stage-3 doc consolidation (separate); the `GizmoActivator → GizmoDriver` rename.
