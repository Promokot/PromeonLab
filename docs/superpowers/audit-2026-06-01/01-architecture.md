# Architecture Backbone Audit — 2026-06-01

Domain: Core, Bootstrap, ModeOrchestrator, StorageCore, SceneComposition + scene/scope/mode lifecycle.
Method: full read of the listed code + listed docs; cross-checked against CLAUDE.md "## Architecture".

---

## 1. Implemented reality

What the code actually provides (confirmed by reading):

- **SceneContext façade** — EXISTS as a root singleton (`Bootstrap/SceneContext.cs`). Exposes 6 nullable read-only services: `Graph` (concrete `SceneGraph`), `Selection`, `Commands`, `Gizmo`, `Authoring`, `Clock`. `HasScene => Graph != null`. Bound/cleared only by `SceneContextBinder` (`Bootstrap/SceneContextBinder.cs:24,37`), which publishes `SceneContextChangedEvent` on both. Confirms CLAUDE.md claim. **Note: there is NO `Rig`/`IRigRuntime` property** (see §3).
- **SceneContextBinder** — scene-scoped `IStartable,IDisposable` entry point; resolves each service defensively via `try/catch(VContainerException)` → null (`SceneContextBinder.cs:43-45`). Registered in both `VrEditingSceneScope` and `SandboxSceneScope` (`RegisterEntryPoint<SceneContextBinder>()`), NOT in `MainMenuSceneScope`. Confirms "Sandbox does not register Authoring/Clock" — Sandbox scope omits both Animation entry points (`SandboxSceneScope.cs` has no `AnimationClock`/`AnimationAuthoring`).
- **ModeExitingEvent before the Single load** — CONFIRMED. `ModeOrchestrator.TransitionTo` publishes `ModeExitingEvent` synchronously at `ModeOrchestrator.cs:36`, *before* `_transition.Load(...)` at `:38`. `SceneAutoSaver` subscribes to it (`SceneAutoSaver.cs:20`) and captures the snapshot synchronously before the first await (`SceneAutoSaver.cs:44`). `ModeChangedEvent` fires only inside the `onLoaded` callback after the Single load (`ModeOrchestrator.cs:38-39`, `SceneTransitionRunner.cs:40`). Ordering claim holds.
- **Single-scene loading + DDOL** — CONFIRMED. `AppBootstrap.Start` calls `DontDestroyOnLoad(_persistentRoot)` then `_transitionRunner.LoadInitial("MainMenu", null)` (`AppBootstrap.cs:18-21`). `SceneTransitionRunner` loads `LoadSceneMode.Single` behind a `HeadFade` (`SceneTransitionRunner.cs:33-42`) with an `IsTransitioning` re-entrancy guard (`:19`). `ModeOrchestrator` is pure policy: graph check + delegate (`ModeOrchestrator.cs:19-40`); no `SceneManager` calls remain.
- **Schema v3 migration in SceneSerializer** — CONFIRMED. `SceneSerializer.Deserialize` does inline migration v1/?→v2 (`Nodes ??= ...`) and <3→v3 (`n.BonePoses ??= ...`) at `SceneSerializer.cs:14-25`. `SceneData.SchemaVersion = 3` default (`SceneData.cs:7`); `NodeData.BonePoses` + `BonePose` (proxy-local TRS) exist. `SceneGraph.CaptureSnapshot` writes v3 and calls `ProxyRigRuntime.CapturePoses()` (`SceneGraph.cs:194,219`); `OnSceneOpenedAsync` calls `ProxyRigRuntime.ApplyPoses` (`SceneGraph.cs:159`). Note: **migration lives in `SceneSerializer`, NOT in a `StorageMigrator`** (see §3 — CLAUDE.md "migrations only in StorageMigrator").
- **CommandStack** — single-stack, max 30, **no redo** (`CommandStack.cs`). Undo only.
- **SelectionManager** — single-select only (`Select(id)`, `SelectedNodeId`); no multi-select. Matches memory note.
- **EventBus** — custom `Publish<T>/Subscribe<T>/Unsubscribe<T>`, `where T : struct`, snapshots handler list on publish (`EventBus.cs`). Matches CLAUDE.md.

## 2. Doc ↔ code matches (accurate claims)

- DI scope hierarchy: Root → Scene single child; Root is DDOL under `PersistentRoot`; scene scopes parent to `RootLifetimeScope`. ✓
- `HasScene` (Graph bound) does not imply other services non-null; Sandbox omits Authoring/Clock → guard per service. ✓ (binder resolves defensively; `SceneContext` exposes null.)
- Cross-subsystem events are `struct` + `Event` suffix in `Events/` subfolders (`ModeExitingEvent`, `ModeChangedEvent`, `SceneContextChangedEvent`, `Scene*Event`, `SelectionChangedEvent`). ✓
- `ModeTransitionGraph` allows only MainMenu↔VrEditing and MainMenu↔Sandbox (`ModeTransitionGraph.cs:10-16`). ✓
- All storage paths go through `PathProvider`; `schemaVersion` on serialized data. ✓
- `SceneContextChanged → OutlinerPanel/InspectorPanel/PropertyPanel/AnimatorPanel` (Plan B migrated exactly these four). ✓
- `ModeExiting → SceneAutoSaver`; Sandbox not saved (`SceneAutoSaver.cs:30,39` guards `From==VrEditing` and `activeId != "__sandbox__"`). ✓

## 3. Drift / mismatches (CLAUDE.md / specs vs code)

1. **Storage layout paths are WRONG in CLAUDE.md.** CLAUDE.md "Data Storage Layout" shows `asset-library/imported.json`, `asset-library/saved.json`, `asset-library/sources/`. Actual `PathProvider` (`PathProvider.cs:34-41`) uses `asset-libraries/imported-lib.json`, `asset-libraries/saved-lib.json`, `asset-libraries/sources/`. Different folder name (`asset-library` vs `asset-libraries`) and file names (`imported.json` vs `imported-lib.json`). Memory note `reference_imported_lib_json_path` corroborates the real path.
2. **`AnimationClock` is listed as a Root registration in CLAUDE.md scope table — it is not.** `RootLifetimeScope.cs` registers `AnimationClipboard` (`:20`), not `AnimationClock`. `AnimationClock` is registered **scene-scoped** in `VrEditingSceneScope.cs:50` (and absent from Sandbox). CLAUDE.md scope table row for `RootLifetimeScope` should read `AnimationClipboard`.
3. **`SceneContext` has no `Rig` property, but the parent spec + Plan A specify one.** Spec `2026-05-29...redesign-design.md` §2 and Plan A both define a 7-arg `Bind(..., IRigRuntime rig)` and a `Rig` property. The shipped `SceneContext.cs` is 6-arg, no `Rig`. (Plan B intentionally never added it; the manual rig/IK wizard was later removed — memory `interaction_context_reset`.) So the spec/Plan-A signatures are stale relative to code.
4. **`StorageMigrator` does not exist; migration is inline in `SceneSerializer`.** CLAUDE.md "Strictly... migrations only in `StorageMigrator`" and StorageCore row ("inline schema migration in `SceneSerializer.Deserialize`") **contradict each other within CLAUDE.md**; code matches the StorageCore row, not the rule. No `StorageMigrator` file exists.
5. **`SceneTransitionRunner` is coroutine-based, not the async/CancellationToken design in Plan C.** Plan C (`scene-loading-single-ddol.md` Task 2) specifies `async Task RunAsync` + `CancellationTokenSource` + `OnDestroy cancel`. Shipped code is `IEnumerator RunRoutine` with `StartCoroutine` and no cancellation (`SceneTransitionRunner.cs:31-45`). Behaviorally equivalent for the happy path but diverges from the plan; the re-entrancy guard is the only drop-protection (no superseding-cancel).
6. **Player-anchor spec is heavily superseded by code.** `2026-05-21-player-anchor-fall-guard-design.md` describes `PlayerSpawnRequestedEvent`, `XRBodyTransformer.QueueTransformation`, `PlayerSpawnAnchor`, and `EventBus` injection. Actual `PlayerSpawnApplier.cs` teleports to world origin via `XROrigin.MatchOriginUpCameraForward`/`MoveCameraToWorldLocation` on `SceneManager.sceneLoaded` — no EventBus, no anchor, no body transformer. `FallGuard` still exists (Y<-20→Respawn) and matches, but probes the camera, not `transform`. Spec is stale (the `_archive/README.md` line acknowledges "later simplified," but the spec body was never updated).

## 4. Planned-but-not-implemented

- **Plan "project-cleanup" (`2026-05-30-project-cleanup.md`) Task 1 — NOT done.** The "dead `PanelRegistry`/`UiPanelOrchestrator`" system still exists (`SpatialUi/PanelRegistry.cs`, `SpatialUi/UiPanelOrchestrator.cs`) and is still registered in `VrEditingSceneScope.cs:7,17` and `SandboxSceneScope.cs:7,17` (`_panelRegistry` field + `RegisterInstance` + `Register<UiPanelOrchestrator>`). `UiPanelOrchestrator.Start` walks `_registry.Panels` (empty asset → no-op) — confirmed dead-but-live.
- **Plan "project-cleanup" Task 3 — NOT done.** No `BaseSceneScope.cs` exists; `VrEditingSceneScope` and `SandboxSceneScope` remain ~90% duplicated (Sandbox just drops `UnsavedChangesGuard`, `SceneAutoSaver`, and the two Animation entry points). Micro dead-code from that task also remains (see §6).
- **Plan D docs cleanup (`2026-05-30-docs-and-archive-cleanup.md`) — PARTIALLY done / overstated.** `CLAUDE.md` was updated to the new model (scope table, ModeExiting/ModeChanged, SceneContextChanged) — good. But:
  - `FeatureLifetimeScope` still appears in `Assets/_App/Documentation/architecture_context.md` and `.planning/docs/ARCHITECTURE.md` (Plan D Task 1/3 not executed there).
  - **No specs/plans were actually moved into `_archive/`.** Every doc the `_archive/README.md` lists as "archived" is still in `docs/superpowers/specs/` and `plans/`. The README is aspirational, not reflective.
  - Broken link: the isolation *spec* banner points to `docs/superpowers/_archive/plans/2026-05-30-scene-loading-single-ddol.md`, but that file is still at `docs/superpowers/plans/...` (never moved). The isolation *plan* (`plans/2026-05-20-scene-loading-isolation.md`) got NO superseded banner at all.

## 5. Stale-doc candidates (do not delete — status + reason)

- `specs/2026-05-28-app-restructure-design.md` + `plans/2026-05-28-app-restructure.md` — **DONE.** Three-asmdef / `Scripts/Core` layout is the live reality (folder structure matches §4 of the spec). Archive candidates.
- `specs/2026-05-29-scene-scope-lifecycle-redesign-design.md` — **DONE (with caveats).** A+B+C landed; the spec's `IRigRuntime Rig` in §2 is stale vs shipped `SceneContext`. Archive, but note the Rig divergence.
- `plans/2026-05-30-scene-context-foundation.md` (Plan A) — **DONE.** `SceneContext`/`Binder`/event all exist. (Plan A's 7-arg `Bind`+`Rig` superseded by shipped 6-arg.)
- `plans/2026-05-30-scene-context-consumer-migration.md` (Plan B) — **DONE.** Four panels migrated; `SceneContext.Graph` is concrete `SceneGraph` as specified.
- `plans/2026-05-30-scene-loading-single-ddol.md` (Plan C) — **DONE** (behaviorally), but runner is coroutine- not async-based vs the plan (§3.5).
- `plans/2026-05-30-docs-and-archive-cleanup.md` (Plan D) — **PARTIAL / NOT-FULLY-DONE.** Do NOT archive; outstanding work (FeatureLifetimeScope in 2 docs; the archive move itself).
- `specs/2026-05-20-scene-loading-isolation.md` + `plans/2026-05-20-scene-loading-isolation.md` — **SUPERSEDED-BY Plan C** (single load removes render-bleed). Spec has the banner (with a broken target path); plan is missing the banner.
- `specs/2026-05-21-player-anchor-fall-guard-design.md` — **OBSOLETE / SUPERSEDED-by-code.** Anchor+event+XRBodyTransformer design replaced by teleport-to-origin (§3.6).
- `session-reports/2026-05-20-scene-loading-isolation.md` + `reports/2026-05-21-project-audit.md` — historical session/audit reports; **SUPERSEDED** by current state (additive-load era).

Count: **7 DONE archive-ready docs** (restructure spec+plan, redesign spec, Plans A/B/C), **2 SUPERSEDED** (isolation spec+plan), **1 OBSOLETE** (player-anchor spec), **1 PARTIAL/blocked** (Plan D).

## 6. Rudimentary / dead code (in-domain)

- **`SceneComposition/Constraints/ConstraintFreezePosition.cs` — ORPHAN/garbage.** Despite the filename, the file contains a single empty class named `Акуу` (Cyrillic), `using UnityEngine;`, no members, no callers. Pure dead file (filename≠type name also violates the "one public type per file, name matches" convention).
- **`SceneComposition/TransformCommand.cs` — likely dead.** Implements `ICommand` but no `new TransformCommand(...)` anywhere; the grep hits are the file itself plus `GizmoController`/`GizmoActivator` which mention `Transform`/commands but do not construct it. Gizmo drag does not route through it → `CommandStack` undo does not cover gizmo moves (consistent with memory `hotkeys`: Undo exists but gizmo-move reversibility is questionable). Verify before removal.
- **`UiPanelOrchestrator` + `PanelRegistry` — dead-but-registered** (see §4). `UiPanelOrchestrator.cs:15` even hardcodes `_currentMode = AppMode.VrEditing` and subscribes to `ModeChangedEvent` for an empty panel set.
- **`AppMode.Debug`** (`AppMode.cs:1`) — declared, **zero usages** in code (`SceneNameFor` returns null for it; not in `ModeTransitionGraph`). Flagged by project-cleanup Task 3 as "risky to remove" — still present, still unused.
- **`Bootstrap/VrInputFieldProxy.cs` — service-locator smell.** Resolves `EventBus` via `LifetimeScope.Find<RootLifetimeScope>().Container.Resolve<EventBus>()` in `Awake` (`:15-16`) instead of constructor DI. Borderline vs the "no singleton/FindObjectOfType" rules; functional but inconsistent with the DI-everywhere convention.
- **`AppMode.cs` switch default** returns `null` scene name → `SceneTransitionRunner.Load` no-ops on null (`SceneTransitionRunner.cs:19`). Safe, but means an unmapped mode silently does nothing.

---

### Biggest-impact items
1. CLAUDE.md storage paths wrong (`asset-library/imported.json` vs real `asset-libraries/imported-lib.json`).
2. CLAUDE.md scope table lists `AnimationClock` at Root; code registers `AnimationClipboard` (Clock is scene-scoped).
3. `_archive/README.md` claims an archival that never physically happened; isolation-spec banner link is broken.
4. Plan D + project-cleanup (PanelRegistry removal, BaseSceneScope, FeatureLifetimeScope doc purge) left unfinished.
5. CLAUDE.md internally contradicts itself on migrations (`StorageMigrator` rule vs `SceneSerializer` reality); no `StorageMigrator` exists.
