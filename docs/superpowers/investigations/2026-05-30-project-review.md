# PromeonLab — Read-Only Code Review (2026-05-30)

Scope: `Assets/_App/Scripts/**` + `Assets/_App/Editor/**`, judged against `CLAUDE.md` conventions.
Read-only investigation — no files changed, no Unity tooling used.

> **Git-history caveat:** the `git log`/`git show` portion of the brief could not be run — both
> the Bash and PowerShell tools denied `git` invocation in this session. The "Recent-changes
> hygiene" section is therefore reconstructed from source state + planning docs, not from diffs.
> Re-run `git log --oneline -30` / `git show` manually to confirm the recent-commit angle.

---

## Summary (prioritized)

1. **HIGH — `PanelRegistry` + `UiPanelOrchestrator` are an effectively dead, superseded panel system.**
   `DefaultPanelRegistry.asset` has `_panels: []` (empty). `UiPanelOrchestrator.SpawnPanels()`
   iterates that empty list, so it spawns nothing, its `_panels` dict stays empty, and
   `GetPanel()` (its only public query) has zero callers. The live panel system is the newer
   *region model* (`PanelRegionRouter` + `NavBarConfig` + `RegionMember` + `RegionNavButton`).
   The orchestrator is still registered in both scene scopes and runs as a no-op.
2. **HIGH — Leftover debug logging across SpatialUi + Gizmo.** The `[RegionDBG]` logs are
   confirmed (3 sites). Additionally `GizmoActivator` and `GizmoToolsPanel` carry ~8 unconditional
   `Debug.Log` calls in hot lifecycle/selection paths, and `GizmoHandle` has hover/grip spam.
3. **MED — `VrEditingSceneScope` and `SandboxSceneScope` are ~90% duplicated** DI bootstrap with
   no shared base; drift risk is real (they already differ subtly).
4. **MED — `GizmoActivator` responsibility bloat:** one MonoBehaviour owns gizmo spawn/despawn,
   per-frame follow, event routing, drag dispatch, scale-inversion math, and command commit.
5. **LOW — Small dead members/types:** `EditorPlaceholder`, `GizmoController.CommitMove` + its
   dead `_target` field, `AppMode.Debug` enum value, `SpatialPanel.GetPanel`-related plumbing.

---

## Dead code

### D1 — `UiPanelOrchestrator` spawn path + `PanelRegistry` (HIGH)
- `Assets/_App/Scripts/SpatialUi/UiPanelOrchestrator.cs:36` (`SpawnPanels`), `:59` (`GetPanel`)
- `Assets/_App/Scripts/SpatialUi/PanelRegistry.cs` (whole SO)
- `Assets/_App/Content/ScriptableObjects/DefaultPanelRegistry.asset:15` → `_panels: []`
- Registered: `VrEditingSceneScope.cs:15`, `SandboxSceneScope.cs:15`; the SO is wired into both
  scopes via `[SerializeField] private PanelRegistry _panelRegistry` (`VrEditingSceneScope.cs:7`,
  `SandboxSceneScope.cs:7`).

**What breaks if it's empty:** nothing — that is the point. With `_panels` empty,
`SpawnPanels()` instantiates zero panels, `RefreshVisibility()` iterates an empty dict,
`OnModeChanged` does nothing useful, and `GetPanel(id)` always returns `null`. `GetPanel` has
**no callers anywhere** (only its own definition matches). So the orchestrator is a live but inert
component, and `PanelRegistry`/`PanelId`/`PanelType` exist only to satisfy its signatures.

**Is it still used at all?** No functional use. The actual per-mode panel visibility is driven by
`PanelRegionRouter.ApplyMode` reading `NavBarConfig` (`RootLifetimeScope.cs:50-82`). `PanelRegistry`
and the region `NavBarConfig` are two parallel "which panels per mode" mechanisms; only the latter
is populated/used. (The design docs at `docs/superpowers/specs/2026-05-29-spatialui-region-model-design.md:135`
explicitly chose to keep them separate, intending `PanelRegistry` to still own "top-level scene
panels" — but in practice no entries were ever added, so that half is unrealized.)

**Recommended action (describe, do not apply):** Decide between two paths:
(a) if top-level mode-gated panels are still planned, this is *unfinished wiring*, not dead code —
populate `DefaultPanelRegistry.asset`; or (b) if the region model fully replaced it, delete
`UiPanelOrchestrator`, `PanelRegistry`, `DefaultPanelRegistry.asset`, the two `_panelRegistry`
serialized fields + `RegisterInstance(_panelRegistry)` lines, and `SpatialPanel.Init/PanelId`
plumbing. `PanelId`/`PanelType` enums would then also be dead (see D2). This is a product decision
— flag, don't auto-remove.

### D2 — `PanelId`, `PanelType`, `SpatialPanel.Init/GetPanel` plumbing (MED, contingent on D1)
- `Assets/_App/Scripts/SpatialUi/PanelId.cs` — enum referenced only by `PanelRegistry`,
  `UiPanelOrchestrator`, and `SpatialPanel.PanelId` (all in the dead path). No `PanelId.<value>`
  literal is used anywhere (grep for `PanelId\.` returns nothing).
- `Assets/_App/Scripts/SpatialUi/PanelType.cs` — used only inside `SpatialPanel.cs:6,37` for
  body-lock/billboard. `SpatialPanel` itself is subclassed only by `UserPanel`
  (`Panels/UserPanel.cs:5`), and `UserPanel` is found+injected directly in
  `RootLifetimeScope.cs:30`, never via the registry. So `PanelType` is reachable but the only live
  consumer is the `UserPanel` base behavior; `Init(PanelId,…)` is never called now that the
  orchestrator spawns nothing.

**Recommended action:** Tie to D1's decision. If D1(b), `PanelId` is fully dead;
`SpatialPanel.PanelId`/`Init` can be dropped (keep `PanelType`/billboard logic for `UserPanel`).

### D3 — `EditorPlaceholder` (LOW)
- `Assets/_App/Editor/EditorPlaceholder.cs` — `public static class EditorPlaceholder {}`, empty.
  Pure scaffolding placeholder; the Editor asmdef now has real content
  (`PromeonProxyRigBuilderEditor`, `RemoveMissingScriptsTool`, `AnimatorPanelModuleBuilder`).
- **Action:** safe to delete.

### D4 — `GizmoController.CommitMove` + dead `_target` (LOW)
- `Assets/_App/Scripts/VrInteraction/GizmoController.cs:40` `CommitMove(...)` — no callers
  (only `CommitTransform` is invoked, from `GizmoActivator.cs:219`).
- `GizmoController.cs:10` `_target` field is assigned in `OnSelectionChanged` (`:27`) but **never
  read**. The subscription exists solely to keep `_target` updated — dead bookkeeping.
- **Action:** remove `CommitMove`; remove `_target` + its `SelectionChangedEvent` sub/unsub if
  nothing else needs it (verify there's no reflection/test access first).

### D5 — `AppMode.Debug` enum value (LOW)
- `Assets/_App/Scripts/ModeOrchestrator/AppMode.cs:1` declares `Debug`. No `AppMode.Debug`,
  `TransitionTo(AppMode.Debug)`, or `case AppMode.Debug` anywhere (grep empty). The "Debug overlays
  any mode" idea from `CLAUDE.md` is unimplemented.
- **Action:** either implement the Debug overlay or drop the enum member; harmless but misleading.

### D6 — `IDragStrategy` / `SingleDragStrategy` (LOW — verify)
- `Assets/_App/Scripts/VrInteraction/IDragStrategy.cs` — `SingleDragStrategy` is instantiated in
  `XRPromeonInteractable.cs:19` and used for the legacy hand-drag path. This is **not** the same as
  the gizmo `IGizmoDragStrategy` family (`Gizmo/Strategies/`). Two unrelated drag abstractions
  coexist. Confirm `XRPromeonInteractable`'s hold-grip move still routes through `_dragStrategy`
  (per memory note `interaction_input_model`); if that path was superseded by the gizmo system,
  `IDragStrategy`/`SingleDragStrategy`/`DragMode` are dead. Currently it *is* referenced, so: keep
  but flag for confirmation.

---

## Duplication

### DUP1 — Scene scopes near-identical (MED)
- `Assets/_App/Scripts/Bootstrap/VrEditingSceneScope.cs` vs `Bootstrap/SandboxSceneScope.cs`.
  Lines ~5-62 are essentially identical: same `_panelRegistry`/`_gizmoConfig` fields, same
  `RegisterInstance`/`Register<…>` block, same `FindAnyObjectByType` + `RegisterBuildCallback`
  injections (WorldClickCatcher, UndoKeyHandler, RigRuntime, IkWizardPanel, BoneInspectorPanel,
  PropertyPanel, AssetSpawner, OutlinerPanel, InspectorPanel, GizmoActivator, GizmoToolsPanel), and
  the same closing region-router build callback.
- Real differences: `VrEditing` also registers `UnsavedChangesGuard`, `SceneAutoSaver`, and the
  animation entry points (`AnimationClock`, `AnimationAuthoring`, `AnimatorPanel` injection)
  (`VrEditingSceneScope.cs:16,18,58-63`) — `Sandbox` omits these. This subtle divergence is exactly
  the bug surface copy-paste creates.
- **Action:** extract a shared `BaseSceneScope` (abstract `LifetimeScope`) with a protected
  `ConfigureCommon(builder)` helper for the shared block; let each subclass add its mode-specific
  registrations. Watch the `Base` prefix convention from `CLAUDE.md`.

### DUP2 — Repeated `FindAnyObjectByType + Inject` pattern (LOW)
- Same idiom appears ~10× per scope and again in `RootLifetimeScope.cs`. It's the project's
  accepted DI-bootstrap shim (these run in `LifetimeScope.Configure`, the sanctioned exception to
  the no-`FindObjectOfType` rule). Not a violation, but a private
  `InjectIfPresent<T>(builder)` / `RegisterIfPresent<T>` helper would cut the noise and centralize
  the `FindObjectsInactive.Include` choice.

### DUP3 — Region build-callback block duplicated 3× (LOW)
- The "resolve router → foreach RegionMember inject+RegisterModule → ApplyMode(currentMode)" block
  is copy-pasted in `RootLifetimeScope.cs:55-81`, `VrEditingSceneScope.cs:76-89`,
  `SandboxSceneScope.cs:66-79`. Folds naturally into the DUP1 base-scope refactor.

---

## Responsibility bloat

### B1 — `GizmoActivator` is a god-object (MED)
- `Assets/_App/Scripts/VrInteraction/Gizmo/GizmoActivator.cs` (269 lines) mixes: DI + event
  subscription (`:32-49`), per-frame target-follow (`LateUpdate :68-77`), gizmo prefab
  spawn/despawn lifecycle (`:119-153`), drag input dispatch (`OnHandleGrabbed/Dragged/Released`
  `:155-227`), **scale-inversion math** (`:191-203`), abort handling (`:229-247`), strategy factory
  (`ResolveStrategy :257-267`), and command commit. The scale-ratio sync math and the
  spawn/lifecycle concern are the clearest extraction candidates.
- **Action:** consider splitting target-follow + spawn lifecycle into one component and the
  drag/strategy commit flow into another (the `IGizmoDragStrategy` types already exist to host the
  per-mode math). Lower priority than the dead-code items.

### B2 — `AnimationAuthoring` size (LOW — borderline, not flagged as bloat)
- `Assets/_App/Scripts/Animation/AnimationAuthoring.cs` (474 lines, ~25 methods) is large but
  cohesive: it owns container/key authoring + debounced persistence (`DebouncedSave :130`,
  `LoadAsync :381`, `SaveAsync :427`). The persistence concern *could* move behind a serializer
  (mirrors how `StorageCore`/`RigSerializer` separate I/O), but the class stays on-topic. Note only.

### B3 — `AnimatorPanel` is NOT bloated (positive finding)
- `Assets/_App/Scripts/SpatialUi/Panels/AnimatorPanel.cs` (367 lines) cleanly delegates to
  `AnimatorSubToolbar/Transport/EmptyState/Ruler/Lanes/Playhead` sub-modules (`:9-15`). Good
  decomposition — left as a reference example, no action.

---

## ScriptableObjects (incl. PanelRegistry)

| SO | File | Kind | Responsibility | Read by | Status |
|---|---|---|---|---|---|
| `PanelRegistry` | `SpatialUi/PanelRegistry.cs` | config/registry | Lists `SpatialPanel` prefabs + per-mode visibility (`IsVisibleIn`) | `UiPanelOrchestrator` only, via `VrEditingSceneScope`/`SandboxSceneScope` `_panelRegistry` field | **Empty asset → inert. See D1.** |
| `NavBarConfig` | `SpatialUi/NavBarConfig.cs` | config (`IRegionConfig`) | Per-module region key / region default / mode visibility for the nav model | `PanelRegionRouter` (Root) | **Live, this is the real one.** |
| `AnimatorPanelConfig` | `SpatialUi/AnimatorPanelConfig.cs` | config | Animator timeline visual config | `AnimatorPanel` | Live |
| `GizmoConfig` | `VrInteraction/Gizmo/GizmoConfig.cs` | config | Gizmo prefab + bounds-fit coefficients | `GizmoActivator`, scene scopes | Live |
| `ModeTransitionGraph` | `ModeOrchestrator/ModeTransitionGraph.cs` | graph | Allowed mode transitions | `ModeOrchestrator` (Root) | Live |
| `DemoAssetCatalog` | `AssetBrowser/DemoAssetCatalog.cs` | config | Demo import catalog | `AssetImporter`, Root | Live |
| `BuiltinAssetLibrary` | `AssetBrowser/BuiltinAssetLibrary.cs` | config/library | Built-in spawnable assets | Root | Live |

**PanelRegistry verdict (the brief's special focus):**
- *What it's for:* an SO-driven registry the orchestrator was meant to walk to instantiate each
  top-level `SpatialPanel` prefab and gate its visibility per `AppMode`.
- *Who reads it:* exclusively `UiPanelOrchestrator` (constructor `UiPanelOrchestrator.cs:17`,
  `SpawnPanels`/`RefreshVisibility`), injected from `VrEditingSceneScope.cs:12` and
  `SandboxSceneScope.cs:12`. Nobody else.
- *What breaks when empty:* functionally **nothing breaks** — no panels are spawned by the
  orchestrator, so there's no crash, just a no-op. There is no null-guard risk because the field is
  assigned (the asset exists, it's just an empty list). The risk is the opposite: it *looks* like a
  configuration point but silently does nothing, which will confuse anyone trying to "register a
  panel here" (the planning docs at `.planning/phases/*` still instruct adding entries to it).
- *Still used at all:* No. The region model (`NavBarConfig` + `PanelRegionRouter`) replaced its
  role. Treat as dead/unfinished per D1.

---

## Recent-changes hygiene

> Reconstructed from source + planning docs; git diff not available this session (see caveat).

### H1 — `[RegionDBG]` debug logs (confirmed) (HIGH)
- `Assets/_App/Scripts/SpatialUi/PanelRegionRouter.cs:33` — `RegisterModule` log
- `Assets/_App/Scripts/SpatialUi/PanelRegionRouter.cs:43` — `RegisterButton` log
- `Assets/_App/Scripts/SpatialUi/Behaviors/RegionNavButton.cs:85` — `OnClick` log
- The region work's own design note flags these for removal:
  `docs/superpowers/specs/2026-05-29-scene-scope-lifecycle-redesign-design.md:134`.
- **Action:** remove all three (region model is complete per that doc).

### H2 — Stray Gizmo debug logging (HIGH — beyond the brief's list)
- `Assets/_App/Scripts/VrInteraction/Gizmo/GizmoActivator.cs:48,81,106,113,157` — unconditional
  `Debug.Log` on Construct, panel-open, selection-change, every `RefreshVisibility`, and every
  handle-grab. These fire in normal interaction flow (per-selection / per-grab), i.e. log spam in a
  shipping VR build.
- `Assets/_App/Scripts/VrInteraction/Gizmo/GizmoToolsPanel.cs:32,38` — OnEnable / publish logs.
- `Assets/_App/Scripts/VrInteraction/Gizmo/GizmoHandle.cs:64,69,78,99` — hover BEGAN/ENDED, grip
  down/up logs (plus already-commented dumps at `:30,75,96`).
- **Action:** strip these (or gate behind a debug flag / `ErrorDispatcher`). These look like
  left-in diagnostics from gizmo-drag bug hunts (cf. memory notes `gizmo_system`,
  `ik_interactable_conflict`).

### H3 — Legitimate logging (no action)
- `Debug.LogWarning`/`Debug.LogError` in `RootLifetimeScope` (`:23,85`), `AnimationAuthoring`,
  `SceneGraph`, `SceneAutoSaver`, `ModeOrchestrator`, `AssetSpawner`, `PromeonProxyRigBuilder`,
  `PlayerSpawnApplier`, `SceneSerializer` are intentional error/migration reporting — keep.
  (Note: none of these route through `ErrorDispatcher` — minor inconsistency with the
  `ErrorHandling` subsystem, but out of scope here.)

### H4 — TODO / half-finished markers
- No `// TODO`/`// HACK`/`// FIXME` found in `Assets/_App/Scripts/**` during this pass (the planning
  docs hold the TODOs, not the runtime code). The main "half-finished refactor" signal is D1 itself:
  `PanelRegistry` was left in place but never populated when the region model landed.

---

## Open questions (need Unity runtime / editor state — couldn't determine read-only)

1. **PanelRegistry product intent:** Is the empty `DefaultPanelRegistry` an abandoned mechanism
   (delete) or planned-but-unwired (populate)? Needs a human/product call. Confirms whether D1/D2
   are "dead code" or "unfinished feature."
2. **Prefab references to dead types:** `SpatialPanel`/`PanelRegistry` GUIDs may still be referenced
   by scene/prefab `.asset` files. Before any deletion, a GUID-reference sweep
   (`cf920f1c0606a6c4ca8cb6082d5abf0f` for `PanelRegistry`) is needed — couldn't run a full asset
   scan reliably here. Memory note `name_collision_suffix` mentions checking GUID refs first.
3. **`XRPromeonInteractable` drag path (D6):** whether hold-grip move still uses
   `SingleDragStrategy` at runtime, or whether the gizmo system fully supplants it. Needs play-mode
   confirmation.
4. **Unreferenced prefabs/assets under `Content/`:** a true unused-asset audit requires Unity's
   dependency graph (or AssetDatabase) — not determinable from C# source alone. Recommend running
   it inside the editor separately.
5. **Recent-commit specifics:** the `git log`/`git show` review the brief asked for needs a session
   where git is permitted; re-run to catch anything left in the last few commits that isn't visible
   in current source state.
