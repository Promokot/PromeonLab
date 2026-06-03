# Architecture Audit — 2026-06-03 (Stage 1)

**Scope:** Verify the *real* architecture of PromeonLab against how it is *described* in `CLAUDE.md`,
`docs/BACKLOG.md`, and the prior `docs/superpowers/audit-2026-06-01/`. Method: full reads of the core
backbone classes + targeted greps across `Assets/_App/Scripts/`. The 2026-06-01 audit was treated as
**one input, not ground truth** — every claim below was re-verified against current code, and several of
its findings are now **stale** (the code moved on).

**Bottom line:** the codebase matches `CLAUDE.md`'s "## Architecture" section very closely. Most live
drift is **doc-vs-doc** (CLAUDE.md storage-layout text, internal contradiction on migrations) rather than
code-vs-doc. Notably, **three items the prior audit flagged as outstanding are now actually done in
code** (PanelRegistry/UiPanelOrchestrator deleted; the `Акуу` orphan file gone; gizmo moves now route
through `CommandStack`). No `Strictly Forbidden` convention violations were found in runtime code.

Line numbers are as-read on 2026-06-03.

---

## Drift summary table

| Claim (source) | Status | Evidence |
|---|---|---|
| Root → Scene VContainer hierarchy; Root DDOL under `PersistentRoot` | CONFIRMED | `RootLifetimeScope.cs`; `AppBootstrap.cs` |
| Root registers `AppStorage`, `PathProvider`, `EventBus`, `SceneContext`, `ModeOrchestrator`, `ISceneTransition`, `PanelRegionRouter`, `AnimationClipboard`, asset libraries, `AssetRegistry`, `ImportPipeline`, `VrKeyboard`, `UserPanel` | CONFIRMED | `RootLifetimeScope.cs:16-160` |
| CLAUDE.md scope table lists `AnimationClipboard` (not `AnimationClock`) at Root | CONFIRMED | `RootLifetimeScope.cs:20` registers `AnimationClipboard`; `AnimationClock` is scene-scoped (`VrEditingSceneScope.cs:47`). (Fixes the prior-audit drift #2 — CLAUDE.md is now correct.) |
| `SceneExporter`/`ExportPipeline` registered at Root (app-lifetime) | CONFIRMED | `RootLifetimeScope.cs:73` |
| VrEditing scene scope registers `SceneGraph`, `SelectionManager`, `CommandStack`, `GizmoController`, `AssetSpawner`, + `AnimationClock`, `AnimationAuthoring`, `SceneAutoSaver`, `UnsavedChangesGuard`; binds `SceneContext` via `SceneContextBinder` | CONFIRMED | `VrEditingSceneScope.cs:12-48` |
| Sandbox omits `AnimationAuthoring`/`AnimationClock`/`SceneAutoSaver`/`UnsavedChangesGuard` | CONFIRMED | `SandboxSceneScope.cs` (none present); `SceneContextBinder` resolves them to null |
| `AssetSpawner` is a scene-scope registration | CONFIRMED | Registered in **both** VrEditing (`:34`) and Sandbox (`:32`) — i.e. Sandbox also spawns. CLAUDE.md's scope table lists it under "Scene scope" generally, which holds. |
| EventBus = custom `Publish<T>`/`Subscribe<T>`, `where T : struct`, per-scope | CONFIRMED | `Core/EventBus.cs` |
| `ModeOrchestrator` is pure policy: validates `ModeTransitionGraph`, delegates to `ISceneTransition`; re-entrancy guard | CONFIRMED | `ModeOrchestrator.cs:19-40`; `SceneTransitionRunner.cs:19` |
| `ModeExitingEvent` published **before** the Single load (outgoing scope alive); `ModeChangedEvent` **after** | CONFIRMED | `ModeOrchestrator.cs:36` (exiting) then `:38-39` (changed inside onLoaded); `SceneTransitionRunner.cs:40` |
| Single-scene load behind `HeadFade` | CONFIRMED | `SceneTransitionRunner.cs:31-45` |
| `ModeTransitionGraph` allows only MainMenu↔VrEditing, MainMenu↔Sandbox | CONFIRMED | `ModeTransitionGraph.cs:10-16` |
| `SceneContext` façade: 6 nullable services, `HasScene => Graph != null`, no `Rig` property | CONFIRMED | `SceneContext.cs:7-14` |
| `SceneContextBinder` fills/clears context, publishes `SceneContextChangedEvent`, defensive resolve | CONFIRMED | `SceneContextBinder.cs:24-44` |
| `scene.json` schemaVersion 3 (+ per-rig bone poses); inline migration in `SceneSerializer` | CONFIRMED | `SceneData.cs:7`; `SceneSerializer.cs:14-25`; `NodeData.BonePoses` |
| `animation.json` schemaVersion 2 (per-container interp/loop + scene fps) | CONFIRMED | `SceneAnimationData.cs:7-9`; `ActionContainer.cs:10-12` |
| **No `StorageMigrator`** — migration inline at deserialize boundary | CONFIRMED (code); CLAUDE.md self-contradicts | `SceneSerializer.cs`; CLAUDE.md "Strictly Forbidden" wording vs StorageCore row (see Convention §X) |
| CLAUDE.md storage-layout folder/file names | CONFIRMED ACCURATE (now) | `PathProvider.cs:34-50` uses `asset-libraries/`, `imported-lib.json`, `saved-lib.json`, `sources/`, `thumbnails/` — matches CLAUDE.md. (Prior-audit drift #1 about `asset-library/imported.json` is **resolved** in current CLAUDE.md.) |
| Export ZIP bundle = `scene.json` + `models/{id}.glb` + `textures/{id}.png`, deduped, builtin→`geometryMissing`, written to `Documents/{productName}/{name}.zip` on thread pool | CONFIRMED | `SceneExporter.cs:71-218,254-257`; `SceneBundle.cs` |
| `CommandStack`: undo-only, max 30, no redo | CONFIRMED | `CommandStack.cs:9-26` |
| `SelectionManager`: single-select (`Select(id)`/`SelectedNodeId`) | CONFIRMED | `SelectionManager.cs` |
| RigBuilder: proxy-bone rig built on spawn (`BuildProxyRig` → `BoneFollower`, `ProxyRigRuntime`) | CONFIRMED | `RigEntityFactory.cs:28`; `RigEntityBuilder.cs:89` |
| IK chains serialized but **no solver consumes them** (DATA-ONLY) | CONFIRMED | `RigDefinition.cs:12`; only refs are the definition + a recipe comment |
| `BoneRecord.TranslationLocked` serialized, never read | CONFIRMED | `BoneRecord.cs:7`; no consumers |
| Bone poses persist proxy-local via `NodeData.BonePoses`, `ProxyRigRuntime.CapturePoses`/`ApplyPoses` | CONFIRMED | `ProxyRigRuntime.cs:55,73`; `SceneGraph.cs:159,219` |
| Animation: `AnimationAuthoring` is `IStartable, ITickable, IDisposable`; per-object Loop background cursors | CONFIRMED | `AnimationAuthoring.cs:9,19-21` |
| `AnimationClock` single-shot transport; `CurrentFrameContinuous` fractional | CONFIRMED | `AnimationClock.cs:14,30-45` (rewinds to 0 at end) |
| VrInteraction: tap-trigger=select, hold-trigger>tapWindow=rotate, hold-grip=move; XRI select-flow disabled | CONFIRMED | `XRPromeonInteractable.cs:112,125-189` |
| Gizmo + selection commit through `CommandStack` (`TransformCommand`) | CONFIRMED — **prior audit stale** | `GizmoController.cs:36`; called from `XRPromeonInteractable.cs:172,185` and `GizmoActivator.cs:444`. (Prior audit/BACKLOG say `TransformCommand` is dead / "Gizmo through CommandStack OPEN" — now wired.) |
| SpatialUi `PanelType` = BodyLocked/WorldFixed/Free | CONFIRMED | `PanelType.cs:1` |
| Detachable panels neutralized (inert) | CONFIRMED | `SpatialPanelDetachable.cs:5-44` (all operational code commented) |
| `PanelRegionRouter` is a Root singleton; nav model at Root | CONFIRMED | `RootLifetimeScope.cs:104-156` |
| ErrorHandling: only `ErrorLevel` + `ErrorOccurredEvent`; **no `ErrorDispatcher`**; reporting via `Debug.Log*` | CONFIRMED | `ErrorHandling.cs` (placeholder), `ErrorLevel.cs`, `ErrorOccurredEvent.cs`; no `ErrorDispatcher` file |
| InputBindings: `ControlsProfile` (SO) + `ControlBinding` for SettingsPanel | CONFIRMED | `ControlsProfile.cs`; `Data/ControlBinding.cs` |
| `PanelRegistry`/`UiPanelOrchestrator` removed | CONFIRMED — **prior audit stale** | Files do not exist; not registered in any scope. (Prior audit §4 said still-live.) |
| `Акуу` Cyrillic orphan class (`Constraints/`) | RESOLVED — **prior audit stale** | `SceneComposition/Constraints/` directory no longer exists |
| `AppMode.Debug` declared but unused | CONFIRMED | `AppMode.cs:1`; zero `AppMode.Debug` usages |

---

## Per-subsystem findings

### Bootstrap / DI scope hierarchy — CONFIRMED
`RootLifetimeScope` (`RootLifetimeScope.cs`) is the app-lifetime container. It registers exactly the
services CLAUDE.md lists, plus the import/export/render plumbing. Scene scopes (`VrEditingSceneScope`,
`SandboxSceneScope`, `MainMenuSceneScope`) parent to it. Child→parent dependency direction holds; no
scene service is referenced from Root except via the `SceneContext` façade.

- The `Find*` calls in scopes are **all** inside `Configure`/`RegisterBuildCallback` — the one legal home
  for the bootstrap shim per CLAUDE.md. See Convention §1.
- **`VrEditingSceneScope` and `SandboxSceneScope` are ~90% duplicated** (Sandbox just drops the four
  VrEditing-only registrations). CLAUDE.md/BACKLOG flag "Extract `BaseSceneScope`" as OPEN — still OPEN
  (no `BaseSceneScope.cs`). Tech-debt, not a doc drift.

### StorageCore — CONFIRMED
`PathProvider` builds all paths; `asset-libraries/imported-lib.json|saved-lib.json|sources|thumbnails`
match CLAUDE.md's storage layout exactly (`PathProvider.cs:34-57`). Schema migration is inline in
`SceneSerializer.Deserialize` (v1/?→v2→v3, `:14-25`); no `StorageMigrator` exists (matches the StorageCore
row; contradicts CLAUDE.md's "Strictly Forbidden" line — see Convention §X).
- **Vestigial-but-harmless:** `PathProvider.ExportDir(sceneId)` (`:28`) and `AssetPath(...)` (`:25`) are
  unused by the live exporter (which writes to `Documents/...`). CLAUDE.md already notes `ExportDir` is
  legacy/unused.

### SceneComposition — CONFIRMED
`CommandStack` (undo-only, max 30), `SelectionManager` (single-select). `TransformCommand` exists and **is
now constructed** (`GizmoController.cs:36`). The previously-flagged `Constraints/Акуу` orphan file is gone.

### AssetBrowser — CONFIRMED (with documented gaps)
Three libraries keyed by `AssetSource`; build-once/restore-many via `AssetEntityBuilderRegistry`
(`RestoreAsync`, Object/Rig/Reference builders) with `InteractionCapability.Apply`. Spawning via
`AssetSpawner`. Thumbnails at import (`ThumbnailRenderer`, `ImportedLabAsset`). **Saved-library spawn
remains unimplemented**: `SavedLabAsset.Recipe => null` and `ThumbnailRef => null`
(`SavedLabAsset.cs:18-19`); `AssetEntityBuilderRegistry.RestoreAsync` has no Saved branch
(`:23` comment) — matches BACKLOG.

### RigBuilder — CONFIRMED (IK + per-bone lock are DATA-ONLY)
Runtime proxy rig built on spawn; bone poses persist (schema v3). `RigDefinition.IkChains`
(`RigDefinition.cs:12`) and `BoneRecord.TranslationLocked` (`BoneRecord.cs:7`) round-trip but have **no
consumer** — matches CLAUDE.md ("IK chains serialized but no solver consumes them yet") and BACKLOG.

### Animation — CONFIRMED
`AnimationAuthoring` is `IStartable, ITickable, IDisposable` with per-owner loop cursors
(`_loopCursors`/`_loopClips`/`_loopLastFrame`, `:19-21`). `AnimationClock` is single-shot
(`AdvanceFrame` rewinds to 0 and stops at `TotalFrames`, `:30-45`) and exposes `CurrentFrameContinuous`
for smooth playback sampling. `ActionContainer` carries `InterpolationMode` + `Loop`. NLA/master timeline
absent — matches BACKLOG.

### ExportPipeline — CONFIRMED
`SceneExporter` (app-lifetime, request/result events) captures live state via `SceneContext`
(`Graph.CaptureSnapshot` + `Authoring.CaptureForExport`), runs pure `BuildBundle`, writes the ZIP on a
thread-pool thread via `ZipArchive`. Builtin/missing-source nodes → `geometryMissing:true`. `SceneBundle`
is the flat one-way external schema (schemaVersion 1). Matches CLAUDE.md precisely. (`ExportPipeline.cs`
is just a doc-pointer file.)

### VrInteraction — CONFIRMED
`XRPromeonInteractable` reads inputs directly on `NearFarInteractor`; `IsSelectableBy => false` disables
the XRI select-flow (`:112`). State machine Idle→TriggerPressed→TriggerRotate / →GripMove
(`:125-189`): tap trigger selects, holding trigger past `_tapWindow` rotates, grip on a selected object
moves; both rotate/move commit through `GizmoController.CommitTransform` → `CommandStack`. Single-select.

### SpatialUi — CONFIRMED
`SpatialPanel` (BodyLocked/WorldFixed/Free + billboard). Region/nav model lives at Root
(`PanelRegionRouter` + `NavBarConfig` + `RegionMember`). `SpatialPanelDetachable` is neutralized (inert,
all operational code commented, `:5-44`). Matches BACKLOG "STUB (neutralized)".

### InputBindings — CONFIRMED
`ControlsProfile` SO (`SchemaVersion`, `Bindings`) + `ControlBinding` data render the Settings panel. The
input *model* itself lives in VrInteraction, as CLAUDE.md states.

### ErrorHandling — CONFIRMED (placeholder subsystem)
`ErrorHandling.cs` is a literal placeholder (`ErrorHandlingPlaceholder {}`). Only `ErrorLevel` enum +
`ErrorOccurredEvent` struct exist; **no `ErrorDispatcher`**, no `Publish<ErrorOccurredEvent>` anywhere.
Reporting goes to `Debug.Log*`. Matches CLAUDE.md/BACKLOG.

---

## Convention violations (CLAUDE.md "Key Conventions" / "Strictly Forbidden")

Result: **no runtime-code violations found.** Detail:

1. **`Find*` in gameplay code** — NONE outside the allowed shim. All 28 `FindAnyObjectByType` /
   `FindObjectsByType` hits are inside `LifetimeScope.Configure` / `RegisterBuildCallback`
   (`RootLifetimeScope.cs`, `VrEditingSceneScope.cs`, `SandboxSceneScope.cs`) — exactly the exception
   CLAUDE.md carves out. No `Find*` in any panel/behavior/service.
2. **`Resources.Load`** — NONE. The only two grep hits are *comments* stating it is deliberately not used
   (`OutlineConfig.cs:4`, `FileBrowserSurface.cs:25`).
3. **`#if UNITY_EDITOR` in runtime files** — NONE.
4. **`Singleton.Instance` / static mutable state** — NONE. No `static` mutable fields; the only statics are
   pure functions (`SceneSerializer`, `SceneExporter.BuildBundle/WriteZipBundle`, `AnimationAuthoring.OwnerOf`,
   `PathProvider.ThumbnailRelativeRef`) and `const`.
5. **Public fields on MonoBehaviour/SO** — NONE that violate the rule. All `public` fields are on
   `[Serializable]` `JsonUtility` data classes / event structs (explicitly allowed), e.g. `SceneBundle.cs`,
   `AnimKeyData.cs`, `ActionContainer.cs`, `*Event.cs`. SOs (`ControlsProfile`, `ModeTransitionGraph`,
   `RootLifetimeScope` fields) correctly use `[SerializeField] private`. Two UI sub-modules expose `public
   System.Action` callback fields (`TimelineScrubInput.cs:9`, `AnimatorSubTransport.cs:22-23`) — these are
   C# delegate wiring, not inspector data, so they are not the "public data field on a behavior" the rule
   targets. **Minor stylistic note only**, not a violation.
6. **Junk-drawer type names** — NONE. The only `*Manager`/`*Controller` names are `SelectionManager` and
   `GizmoController`, both explicitly whitelisted in CLAUDE.md as domain roles. No bare
   `Manager`/`Utils`/`Helper`/`*Service` grab-bags.
7. **`async void`** — `AssetSpawner.OnSpawnRequested`/`SceneExporter.OnExportRequested` use
   `_ = ...Async(...)` (fire-and-forget Task, wrapped in try/catch) rather than `async void` — compliant.

### X. CLAUDE.md internal contradiction (doc bug, not code)
CLAUDE.md "Strictly Forbidden" / Key Conventions implies migrations are centralized, while the StorageCore
row and "All serialized data carries a `schemaVersion`… migrations are inline at the deserialization
boundary (e.g. `SceneSerializer.Deserialize`) — there is no separate `StorageMigrator` class" correctly
describes the code. **The code matches the inline-migration description.** Any lingering "`StorageMigrator`"
phrasing elsewhere is stale. (Flagged by the prior audit too; still worth a doc cleanup.)

---

## Items where the PRIOR audit (2026-06-01) is now STALE vs current code

These are *improvements* since 2026-06-01 — recorded so Stage 2 doesn't re-flag them:

1. **`PanelRegistry` / `UiPanelOrchestrator` deleted.** Prior audit §4 said both still existed and were
   registered in both scene scopes. Current: files gone, no registrations. BACKLOG already marks this
   "DONE 2026-06-01" — code now agrees.
2. **`Constraints/Акуу` orphan removed.** Prior audit §6 flagged it; the `Constraints/` folder is gone.
3. **`TransformCommand` is live.** Prior audit §6 + BACKLOG ("Gizmo moves through CommandStack — OPEN")
   call it dead/uncovered. It is constructed in `GizmoController.cs:36` and reached on every gizmo move
   (`GizmoActivator.cs:444`), rotate and grip-move (`XRPromeonInteractable.cs:172,185`). Gizmo moves now
   commit through `CommandStack`. **Recommend updating BACKLOG.**
4. **CLAUDE.md storage paths corrected.** Prior audit's biggest item (`asset-library/imported.json`) no
   longer applies; current CLAUDE.md text matches `PathProvider`.

## Items STILL outstanding (confirmed against code)

- **`BaseSceneScope` not extracted** — `VrEditingSceneScope`/`SandboxSceneScope` ~90% duplicated. OPEN.
- **`AppMode.Debug`** declared, zero usages (`SceneNameFor` returns null for it). Harmless, unused.
- **`SceneTransitionRunner` is coroutine-based** (`IEnumerator RunRoutine`, no `CancellationToken`) vs the
  planned async design. Behaviorally fine; re-entrancy guard is the only drop-protection.
- **`VrInputFieldProxy` service-locator smell** — resolves `EventBus` via
  `LifetimeScope.Find<RootLifetimeScope>().Container.Resolve<EventBus>()` in `Awake` (`:15-16`) instead of
  constructor DI. Functional but inconsistent with the DI-everywhere convention; borderline, not a hard
  "Strictly Forbidden" hit (it's not `Singleton.Instance` and the `Find` locates the *scope*, not a
  gameplay object). Worth noting.
- **Saved-library spawn / IK solver / NLA / FBX export / ErrorDispatcher** — all ABSENT/DATA-ONLY per
  BACKLOG; verified still absent.

---

## Unverifiable / out of scope for code-read
- Runtime *behavior* (in-headset) of fades, thumbnails, looping playback — assertions about behavior in
  CLAUDE.md/BACKLOG ("Verified in-headset") were not re-tested here; only the code paths were confirmed
  present and wired.
- Prefab wiring (which `RegionMember`/buttons sit on which prefab, serialized-field assignments) — not a
  C# read; not audited.
