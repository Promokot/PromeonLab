# SpatialUi Audit — 2026-06-01

Domain: panels, region/navbar model, UserPanel (grab+lock), VR keyboard, Settings/Controls-bindings panel.
Excludes AnimatorPanel + timeline (other agent). Read-only audit; code paths verified against docs.

---

## 1. Implemented reality

### Region / navbar model — IMPLEMENTED, root-lifetime
- `PanelRegionRouter` is a plain-C# `IDisposable` registered as **`Lifetime.Singleton` in `RootLifetimeScope`** (`RootLifetimeScope.cs:104-147`), NOT scene-scoped. It owns BOTH module surfaces and nav buttons, and is the single `ModeChangedEvent` subscriber that drives panel open/close + button visibility + active-highlight from `AppMode` (`PanelRegionRouter.cs:9-27,107-161`). Matches project memory `region_router_root_lifetime`.
- Config is `NavBarConfig : ScriptableObject, IRegionConfig` (`NavBarConfig.cs:5`). The planned rename to `PanelRegionConfig` was **deliberately deferred** (region-model spec addendum item 2) — `NavBarConfig.Entry` carries `Id` / `VisibleModes` / `ExclusiveGroup` / `IsRegionDefault` (`NavBarConfig.cs:7-14`); router consumes the `IRegionConfig` façade (`IRegionConfig.cs`).
- Region model: one open surface per `ExclusiveGroup` (`PanelRegionRouter.Open` 58-80, mutual-exclusion 64-69); `Close` auto-reopens a region's `IsRegionDefault` member (82-97); `EnsureRegionDefaults` fills empty regions on mode entry/startup (123-150). This is the generic mechanism that restores `Default` when the keyboard closes (spec "Implementation correction").
- `IRegionSurface { Show/Hide/IsOpen }` (`IRegionSurface.cs`); `RegionMember : MonoBehaviour, IRegionSurface` is the default registrar, delegating to a sibling `IRegionSurface` when present (`RegimeMember.cs:12-38` — e.g. `FileBrowserSurface`).
- `RegionNavButton` is a **thin** button: forwards click → `router.Toggle`; visibility/highlight pushed in by router via `SetVisible`/`SetActiveHighlight` (`RegionNavButton.cs:7,38-48,83-86`). Lifecycle-safe lazy idempotent setup (Construct may precede Awake on an inactive panel).
- Discovery wiring in `RootLifetimeScope` build callback: injects + registers all `RegionNavButton`, `RegionMember`, plus persistent `AssetBrowserPanel`/`FileBrowserSurface`/`ImportWizardSurface`/`AnimatorPanel`, then calls `router.ApplyMode(currentMode)` (`RootLifetimeScope.cs:109-146`). Scene scopes additionally register their own scene-bound `RegionMember`s against the root router (`VrEditingSceneScope.cs:66-73`, `SandboxSceneScope.cs:60-64`).

### Panel registry (separate level) — IMPLEMENTED
- `PanelRegistry` (SO) + `UiPanelOrchestrator` (`IStartable`/`IDisposable`, scene-scoped) spawn top-level panels per mode and toggle visibility on `ModeChangedEvent` (`UiPanelOrchestrator.cs:25-57`). Deliberately kept separate from the region registry (spec "Registry merge" rejected variant A). `PanelId` enum still includes `UserPanel`, `RigBuilder`, `KeyframeEditor`, `ComingSoon` (`PanelId.cs:1`).

### UserPanel grab + triple-lock — IMPLEMENTED, verified in-headset
- `PanelGrabHandle : XRBaseInteractable` (`PanelGrabHandle.cs`): grip (`selectInput`) grab, `IsSelectableBy => false` (direct input), position-only `MoveTo`, NON-trigger collider ownership cleared from base auto-discovery (41-46), primary-hit gate via ray `TryGetCurrent3DRaycastHit` (136-151), hover/grab tint. Pure `CaptureOffset`/`ApplyOffset` helpers are unit-tested (plan reports PanelGrabHandleTests 2/2).
- `UserPanel.LockMode` triple cycle `Follow→LockPosition→LockPositionRotation` with ping-pong `_lockDir` (`UserPanel.cs:7,209-220`); `FaceCameraBelow` gated inside the mode (102-114 — runs during grab except full lock); 3-color lock indication (53-55,236-245). `Start` reparents off the rig (`DetachToWorld` 247-258 — `SetParent(null)` + `DontDestroyOnLoad`). Size +/- multiplier with clamp (222-234). `OnModeChanged` resets to Follow + hides the panel (72-81).
- `UserPanelOpener` toggles the panel via direct `SetActive` + `ResetPosition` on `<XRController>/primaryButton` (X/A) — left or right (`UserPanelOpener.cs`). Note: it does NOT route through `PanelRegionRouter` (the whole panel is opened/closed outside the region model; only its inner modules are region-managed).

### VR keyboard — IMPLEMENTED (brain only; toggle migrated to region model)
- `VrKeyboard` is the root-scoped typing brain: subscribes `KeyboardFocusEvent`, `AddLetter`/`DeleteLetter`/`SubmitWord` write to the focused `TMP_InputField` (`VrKeyboard.cs`). Injected in `RootLifetimeScope.cs:89-91`.
- `VrInputFieldProxy` publishes `KeyboardFocusEvent` on pointer-down, resolving `EventBus` from **`RootLifetimeScope`** (`VrInputFieldProxy.cs:15` — note: resolves Root, not the `SceneLifetimeScope` the old design/dev-note describe).
- `UserPanelKeyboardToggle` and `KeyboardFocusOpener` **do not exist** — the keyboard became a plain `RegionMember` in region `overlays` opened by a `RegionNavButton`, per the region-model spec addendum.

### Settings / Controls-bindings — IMPLEMENTED
- `SettingsPanel` master-detail (General placeholder / Bindings) builds `BindingSectionCard` + `BindingRow` from `ControlsProfile` at runtime, grouped by `Category`, null-guarded (`SettingsPanel.cs:65-93`).
- `ControlsProfile` SO (`schemaVersion=1`, `ControlBinding[]`) (`ControlsProfile.cs`); `ControlBinding` struct + `ControlBindingCategory {Movement,Selection,System}` + `ControlHand` (`InputBindings/Data/`). `BindingRow.Bind` maps hand→label and shows action/description/input (`BindingRow.cs`).
- Editor exporter `ControlsProfileExporter` writes `docs/controls-bindings.md` from the asset via `Tools/Promeon/Export Controls Doc` (`Editor/Tooling/ControlsProfileExporter.cs`).

### Other
- `HeadFade` (frame-locked coroutine fade for scene transitions) implemented (`HeadFade.cs`) — drives the `SceneTransitionRunner` black-out.
- `SpatialPanel` base (BodyLocked/WorldFixed/Free + billboard/lazy-follow) (`SpatialPanel.cs`); `SpatialPanelDetachable` + `DetachablePanelDragHandle` (link/unlink/lock/close floating chrome) still present and functional.
- Elements: `OutlinerItem`/`RigOutlinerItem`, `SceneItem`, `BindingRow`/`BindingSectionCard` all present.

---

## 2. Doc ↔ code matches

- **Region model spec** (`2026-05-29-spatialui-region-model-design.md`) — matches code including both post-design corrections: keyboard = plain `RegionMember` in region `overlays` (no `UserPanelKeyboardToggle`), `IsRegionDefault` + auto-reopen in `Close`, `NavBarConfig` kept (no `PanelRegionConfig` rename), `FileBrowserSurface` adapter + `FilePickedEvent`. The verification doc's "router is now Singleton in RootLifetimeScope" matches `RootLifetimeScope.cs:107`.
- **Grab & triple-lock** (`2026-05-30-userpanel-grab-and-lock-design.md` + plan) — matches `PanelGrabHandle.cs` / `UserPanel.cs`. The plan's COMPLETED deviation list (non-trigger collider, `raycastTarget=0`, hover tint, rotation-gate during grab, `CycleLockMode` clears velocity) all match the code.
- **Settings panel + controls bindings** (`2026-05-30-settings-panel-controls-bindings-design.md` + plan, COMPLETED) — matches `SettingsPanel`, `ControlsProfile`, `BindingRow`, exporter, the file list, and the warn-not-crash error handling.
- **CLAUDE.md** SpatialUi/InputBindings rows match: `ToolbarPanel`/billboard, `PanelRegionRouter` as Root singleton, `FilePicked`→ImportPipeline, context-switched InputBindings.

---

## 3. Drift / mismatches

- **`docs/developer-notes/vr-keyboard.md` is OBSOLETE** — describes `UserPanelKeyboardToggle` (deleted), file paths under `_App/Subsystems/SpatialUi/UI_Scripts/` (folder no longer exists; code is in `Scripts/SpatialUi/...`), `KeyboardFocusEvent` "in `AppEvents.cs`" (actually `Events/KeyboardFocusEvent.cs`), and says `VrInputFieldProxy` resolves `EventBus` from `SceneLifetimeScope` — code resolves **`RootLifetimeScope`** (`VrInputFieldProxy.cs:15`). Every "How it works" step references deleted/renamed types.
- **`docs/developer-notes/2026-05-17-navbar-manual-wiring-guide.md` is OBSOLETE** — entire wiring model gone: `NavBarConfig` `StartsEnabled` field (replaced by `IsRegionDefault`/`ExclusiveGroup`), `NavBarBinding[]` on `UserPanel` (removed — modules are discovery-registered), `DetachablePanel` (renamed `SpatialPanelDetachable`), paths under `Subsystems/SpatialUi/Data/`. Step 5 ("assign NavBarBinding[]") describes an API that no longer exists.
- **`docs/developer-notes/ui-conventions.md` partially stale** — §6 "Подключение к NavBar" tells devs to add a `NavBarBinding` to `UserPanel._bindings` (removed); §4 hierarchy uses `DetachablePanel` (now `SpatialPanelDetachable`); paths use `Subsystems/SpatialUi/Data/`. The general rules (§1 Z=0, §2 World-Space Canvas, §5 module/DI pattern, §7 forbidden list) are still valid.
- **`docs/developer-notes/2026-05-17-scene-outliner-manual-setup.md` stale** — references `Subsystems/` tree, `SceneOutlinerRow`/`SceneOutlinerView`/`SceneInspectorView` (current code: `OutlinerItem`/`OutlinerPanel`/`InspectorPanel`), `UserPanel_ContextMenu_*` prefabs, and the deleted `WorldClickCatcher`/`ContextSlot` model.
- **`docs/developer-notes/2026-05-17-xr-ui-bugfixes-and-navbar-design.md` stale** — session notes built around `WorldClickCatcher` + old NavBar design (superseded by region model + the current interaction-mask model).
- Minor: `SpatialPanel` exposes both `Init(...)`/`SetVisible` and is the base for `UserPanel`, but `UserPanel` ships inactive and is opened by `UserPanelOpener`/`ModeChanged`, not by `UiPanelOrchestrator` visibility — the two visibility systems (registry vs opener/region) coexist; not a bug but worth noting for future consolidation.

---

## 4. Planned-but-not-implemented

- **Detach → add-on split (`PanelDetachAddon`)** — region-model spec "Out of scope" item: split `SpatialPanelDetachable` so docked behavior is a normal region module and detach/float becomes an optional add-on. NOT done; `SpatialPanelDetachable` + `DetachablePanelDragHandle` remain the monolithic detachable-chrome implementation.
- **`VrKeyboard` rename** — region-model spec deferred this (referenced by type in ThirdParty `KeyboardButtonController`). Not done (intentional).
- **`NavBarConfig` → `PanelRegionConfig` rename** — deferred "trivial follow-up" (spec addendum item 2). Not done.
- **General settings tab content** — explicit Non-Goal in settings spec; `_generalContent` is an empty placeholder (`SettingsPanel.cs:51`). Expected.
- **Runtime rebinding / settings persistence / vertical-fly / comfort vignette** — explicit Non-Goals in settings spec. Not implemented (expected).
- **Context menu** — region model "leaves room" for a future `router.Open(...)` consumer; nothing built (expected, not drift).

---

## 5. Stale-doc candidates (DO NOT delete)

Bugfix specs/plans — all superseded by later rewrites of the same files:

- `specs/2026-05-16-double-userpanel-cursor-fix-design.md` + `plans/2026-05-16-double-userpanel-cursor-fix.md` — **SUPERSEDED / DONE.** Fix targeted `DefaultPanelRegistry`/`UiPanelManager.SpawnPanels` double-spawn; `UiPanelManager` is now `UiPanelOrchestrator` and `UserPanel` is no longer in the panel registry (it is rig-persistent). Cursor-hide fix landed in `AppBootstrap`. Historical.
- `reports/2026-05-16-double-userpanel-cursor-fixes.md` — **DONE (report).** Same fix; references removed `UiPanelManager`. Historical record.
- `specs/2026-05-16-userpanel-filebrowser-fixes-design.md` + `plans/2026-05-16-userpanel-filebrowser-fixes.md` — **SUPERSEDED.** Y-drift/`FaceCamera`-below logic was rewritten by the 2026-05-30 grab-and-lock work (`UserPanel.UpdateSmartFollow`/`FaceCameraBelow` now mode-gated); `FileBrowserVrAnchor` was replaced by `FileBrowserSurface` (region model). Paths use `Subsystems/.../UI_Scripts/`.
- `specs/2026-05-16-userpanel-menu-button-fix-design.md` + `plans/2026-05-16-userpanel-menu-button-fix.md` — **DONE.** Main-menu button null-orchestrator fix; `UserPanel` is now injected in `RootLifetimeScope` (`RootLifetimeScope.cs:80-83`) and `OnMainMenu → _orchestrator.TransitionTo` works (`UserPanel.cs:260`). Mentions `ArMapping` mode (no longer a target).
- `specs/2026-05-15-panel-consolidation-design.md` + `plans/2026-05-15-panel-consolidation.md` — **OBSOLETE.** Whole premise (per-subsystem `SpatialUi.asmdef`, `_Shared/Interfaces`, `UI_Scripts/`, `BoneInspectorPanel`/`IkSetupWizard`) contradicts current single-`_App.Runtime`-assembly, no-namespace, `Scripts/SpatialUi/` topology. Manual rig/IK wizard removed (memory `interaction_context_reset`).
- `specs/2026-05-17-navbar-panel-system-design.md` + `plans/2026-05-17-navbar-panel-system.md` — **SUPERSEDED-BY** `2026-05-29-spatialui-region-model`. Introduced `NavBarBinding[]`/`ContextSlot`/`DetachablePanel`/`StartsEnabled` — all replaced by the region model.
- `specs/2026-05-18-navbar-exclusive-groups-design.md` — **SUPERSEDED-BY** region model. `ExclusiveGroup` survived (now consumed as region key via `IRegionConfig`), but the `UserPanel.HideAllPanels` mechanism it describes is gone.
- `specs/2026-05-17-spatialui-scripts-reorganization-design.md` — **PARTIALLY SUPERSEDED.** Proposed `Scripts/{Panels,Views,Elements}`; current tree is `SpatialUi/{Panels,Elements,Behaviors,Events}` (no `Views/`; added `Behaviors/`). The Panel/Module/View suffix-semantics table is the still-useful part but does not match the final folder set.
- Developer-notes flagged in §3 (`vr-keyboard.md`, `2026-05-17-navbar-manual-wiring-guide.md`, `2026-05-17-scene-outliner-manual-setup.md`, `2026-05-17-xr-ui-bugfixes-and-navbar-design.md`) — **OBSOLETE manual-wiring guides** for a model that is now DI-discovery-driven. `ui-conventions.md` — **NEEDS UPDATE** (mixed valid/stale).

DONE (keep as record): `plans/2026-05-30-userpanel-grab-and-lock.md` (COMPLETED 2026-05-31), `plans/2026-05-30-settings-panel-controls-bindings.md` (COMPLETED 2026-05-30). Region-model plan + prefab-verification plan are current/accurate.

---

## 6. Rudimentary / dead code

- **`PanelDragHandle.cs`** — dead on the UserPanel (replaced by `PanelGrabHandle`); kept per "comment/keep, don't delete" convention. Still references `UserPanel.SetDragging`/`MoveTo` so it compiles, but is not on any prefab (grab-and-lock plan deviation note). Candidate for explicit "unused" marker.
- **`InputBindings/InputBindings.cs`** — file is a single comment line only (placeholder façade); no type. Settings spec said "delete placeholder if it gets in the way"; it remains as a near-empty stub.
- **`VrKeyboard.SubmitWord()`** — only sets `_target = null` (`VrKeyboard.cs:29`); no submit/blur event is published, and no `KeyboardFocusOpener` exists to close the keyboard region on submit. The keyboard closes only via its `RegionNavButton`. Minor rudiment vs the original "close on submit" intent.
- **`PanelType` enum** (`BodyLocked/WorldFixed/Free`) — only `BodyLocked` is acted on in `SpatialPanel.LateUpdate`; `WorldFixed`/`Free` have no distinct behavior (both fall through to no follow + optional billboard). Rudimentary.
- **`SpatialPanel.PanelId`/`Init`** path overlaps with the region model; `UserPanel` never uses its `SpatialPanel` registry-visibility (opened by `UserPanelOpener`). Coexisting-systems smell, not a hard bug.
- `RegionMember` filename: typo-free, but note the class lives at `RegionMember.cs` (the §1 reference to "RegimeMember" above is a transcription slip — actual file/type is `RegionMember`).

---

### Summary

Region model: fully implemented and **root-lifetime** (`PanelRegionRouter` = Singleton in `RootLifetimeScope`), with `IRegionConfig`/`RegionMember`/`RegionNavButton`/`IsRegionDefault` auto-reopen; `NavBarConfig` kept (rename deferred); keyboard + file browser folded in as region members. UserPanel grab (grip `XRBaseInteractable`) + triple-lock and the Settings master-detail + `ControlsProfile` + md exporter are all COMPLETE and verified.

Now-stale bugfix docs: the four 2026-05-16 userpanel/cursor specs+plans (double-panel, filebrowser-fixes, menu-button) + their report are all SUPERSEDED/DONE; the 2026-05-15 panel-consolidation and 2026-05-17 navbar-panel-system / exclusive-groups / scripts-reorg specs are superseded by the region model + app-restructure. Four developer-notes (vr-keyboard, navbar-manual-wiring, scene-outliner-setup, xr-ui-bugfixes) are OBSOLETE manual-wiring guides; ui-conventions needs a partial update.

Planned-not-implemented: `PanelDetachAddon` detach/add-on split, `VrKeyboard` rename, `NavBarConfig`→`PanelRegionConfig` rename, and General-tab content (all explicitly deferred/Non-Goal). Dead/rudimentary: `PanelDragHandle.cs`, the `InputBindings.cs` stub, `VrKeyboard.SubmitWord` (no close-on-submit), and the inert `WorldFixed`/`Free` `PanelType` cases.
