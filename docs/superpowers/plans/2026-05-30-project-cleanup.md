# Project Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans (deletion + log removal + a scope refactor — inline with checkpoints). Checkbox steps.
>
> **Root-cause reference:** `docs/superpowers/investigations/2026-05-30-project-review.md`.
>
> **Git note:** user (Promokot) commits manually — no auto-commit. **Unity note:** controller compiles via MCP after each deletion; **[MANUAL EDITOR]** none required for code, but verify no broken serialized refs after asset deletion.
>
> **Ordering:** Task 3 (BaseSceneScope extraction) touches both scene scopes and therefore should run AFTER Plan C (which also edits them) to avoid conflicts. Tasks 1-2 are independent and can run anytime.

**Goal:** Remove the dead `PanelRegistry` panel system (user decision: delete fully), strip leftover debug logging, and (after Plan C) de-duplicate the two scene scopes — reducing dead/confusing code surfaced by the project review.

**Tech Stack:** Unity 6000.3.7f1, VContainer.

---

## Task 1: Delete the dead `PanelRegistry` system

`DefaultPanelRegistry.asset` is empty; its only reader `UiPanelOrchestrator` walks an empty list (no-op); `PanelRegistry.GetPanel()` has zero callers. The live per-mode system is the region model. **User decision: delete fully.**

- [ ] **Step 1 (controller): grep guard.** `Grep` for `UiPanelOrchestrator`, `PanelRegistry`, `PanelId`, `PanelType` across `Assets/`. Confirm the ONLY references are: the type defs, the two scene-scope registrations, and the `_panelRegistry` serialized fields. If anything else resolves/uses `UiPanelOrchestrator` (e.g. as `IStartable`/another injectee), STOP and report — deletion premise changed.
- [ ] **Step 2:** Remove the registrations from `VrEditingSceneScope.cs` and `SandboxSceneScope.cs`: delete `[SerializeField] private PanelRegistry _panelRegistry;`, the `builder.RegisterInstance(_panelRegistry);` line, and the `builder.Register<UiPanelOrchestrator>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();` line. (Leave the `SceneContextBinder` entry-point and everything else.)
- [ ] **Step 3:** Delete the files (+ their `.meta`): `Assets/_App/Scripts/SpatialUi/PanelRegistry.cs`, `Assets/_App/Scripts/SpatialUi/UiPanelOrchestrator.cs`, `Assets/_App/Scripts/SpatialUi/PanelId.cs` (if fully dead per Step 1), and the asset `Assets/_App/Content/ScriptableObjects/DefaultPanelRegistry.asset`. Keep `PanelType.cs` only if still referenced (grep — it may be unrelated).
- [ ] **Step 4 (controller): compile.** No `CS` errors. Run full `_App.Tests` — no new failures beyond the 7 baseline.
- [ ] **Step 5 [MANUAL EDITOR]:** Open `Bootstrap`/`VrEditing`/`Sandbox` scenes; confirm no "missing script"/"missing reference" warnings from the removed `_panelRegistry` serialized field on the scene LifetimeScope objects.
- [ ] **Step 6: Checkpoint (user commits)** — `chore: remove dead PanelRegistry/UiPanelOrchestrator panel system`

---

## Task 2: Strip leftover debug logs

The review confirmed stray `Debug.Log`s firing in normal flow.

- [ ] **Step 1:** Remove the `[RegionDBG]` logs: `PanelRegionRouter.cs:33` (`RegisterModule`) and `:43` (`RegisterButton`); `RegionNavButton.cs:85` (`OnClick`).
- [ ] **Step 2:** Remove the gizmo debug logs flagged by the review: `GizmoActivator.cs` (the unconditional `Debug.Log`s in Construct/selection/refresh/grab — incl. the `[GizmoActivator] OnHandleGrabbed` at `:157` and Construct at `:48`), `GizmoToolsPanel.cs:32,38`, `GizmoHandle.cs:64,69,78,99`. **Keep** genuine error/warning logging; remove only the development trace `Debug.Log`s. Read each site to confirm it's a trace log, not an error path.
- [ ] **Step 3 (controller): compile.** No `CS`.
- [ ] **Step 4: Checkpoint (user commits)** — `chore: remove leftover RegionDBG/Gizmo debug logging`

---

## Task 3 (AFTER Plan C): de-duplicate scene scopes + micro dead-code

`VrEditingSceneScope` and `SandboxSceneScope` are ~90% identical. **Do this only after Plan C has landed** (Plan C edits both scopes), to avoid rebasing churn.

- [ ] **Step 1:** Extract a `BaseSceneScope : LifetimeScope` holding the shared registrations (SceneGraph, SelectionManager, CommandStack, GizmoController, SelectionVisualSync, AssetImporter, AssetSpawner, the SceneContextBinder entry-point, the find-and-inject build callback, the region-member re-registration + ApplyMode). `VrEditingSceneScope`/`SandboxSceneScope` override a hook (e.g. `protected virtual void ConfigureExtra(IContainerBuilder)`) for their differences (VrEditing adds SceneAutoSaver + the Animation entry points + AnimatorPanel inject; both share the rest). Verify against the post-Plan-C versions of both files.
- [ ] **Step 2:** Micro dead-code (verify each with grep before removing): `EditorPlaceholder` (if unreferenced); `GizmoController.CommitMove` + its never-read `_target` (confirm no callers); the unused `AppMode.Debug` value — **only** if removing it doesn't break the `ModeTransitionGraph`/switch defaults or any serialized references (it is risky; leave it if uncertain and just note it).
- [ ] **Step 3 (controller): compile + full tests.** No `CS`; no new test failures.
- [ ] **Step 4 (user, VR): smoke** VrEditing + Sandbox still load and behave identically to before (the dedup is behavior-preserving).
- [ ] **Step 5: Checkpoint (user commits)** — `refactor(scope): extract BaseSceneScope; remove micro dead-code`

---

## Self-Review

**Coverage:** PanelRegistry deletion (files + registrations + asset, guarded by a reference grep) → Task 1; debug-log removal (`[RegionDBG]` + gizmo traces) → Task 2; scene-scope dedup + small dead members → Task 3 (gated after Plan C). **Placeholders:** none — exact files/lines; each deletion is guarded by a grep/compile gate, and the riskiest item (`AppMode.Debug` removal) is explicitly conditional. **Risk:** deleting `UiPanelOrchestrator` is safe only if nothing else resolves it → Task 1 Step 1 grep guard. `PanelId`/`PanelType` kept unless proven dead. BaseSceneScope deferred to avoid colliding with Plan C's scope edits.
