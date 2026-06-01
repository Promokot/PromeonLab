# Interaction Context Reset + Collider-Registration Cleanup — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix click-selection dying after a scene re-entry, encapsulate the XRI collider re-registration honestly, and delete the unreachable manual-rigging code.

**Architecture:** Three independent edits. (1) The persistent `InteractionMaskBinder` resets its context flags on `ModeChangedEvent` so every scene entry starts in the object-selection mask. (2) `XRPromeonInteractable.RegisterColliders` delegates its manager re-index to a named `RefreshColliderRegistration()` with a truthful comment. (3) Remove the dead `RigRuntime`/`IRigRuntime`/`IkWizardPanel`/`BoneInspectorPanel` cluster and unwire its DI references.

**Tech Stack:** Unity 6000.3.7f1, C#, VContainer DI, XR Interaction Toolkit (NearFarInteractor), custom `EventBus`. Tests: Unity Test Runner (EditMode), driven via Unity MCP.

> **PROJECT RULE — NO GIT.** Never run `git add`/`git commit`/any git. Where a normal plan would commit, this plan ends each task with a **Checkpoint** that the orchestrator runs:
> 1. `refresh_unity` (mode:`force`, scope:`all`, compile:`request`)
> 2. `read_console` (types:`[error]`, filter_text:`CS`) — only `error CS####` matters; `MCP-FOR-UNITY: …`, `MissingReferenceException: m_Targets`, `SerializedObjectNotCreatableException` are harmless noise.
> 3. `run_tests` (mode:`EditMode`) + `get_test_job` (wait_timeout:60).
>
> **Baseline = 6 known pre-existing EditMode failures:** `PathProviderTests` ×4 (Windows `\` vs `/`), `RingRotateStrategyTests` ×2. A task passes its checkpoint when compilation is clean and the failure set is exactly those 6 (zero new).

---

## File Structure

| File | Change | Responsibility |
|---|---|---|
| `Assets/_App/Scripts/VrInteraction/InteractionMaskBinder.cs` | Modify | Subscribe to `ModeChangedEvent`; reset context flags + reapply default mask on transition. |
| `Assets/_App/Scripts/VrInteraction/XRPromeonInteractable.cs` | Modify | Extract manager re-index into `RefreshColliderRegistration()`; correct the rationale comment. |
| `Assets/_App/Scripts/Bootstrap/SceneContext.cs` | Modify | Drop the unused `Rig` (`IRigRuntime`) facade slot. |
| `Assets/_App/Scripts/Bootstrap/SceneContextBinder.cs` | Modify | Stop resolving the removed `IRigRuntime`. |
| `Assets/_App/Scripts/Bootstrap/VrEditingSceneScope.cs` | Modify | Remove dead `RigRuntime`/`IkWizardPanel`/`BoneInspectorPanel` find+register. |
| `Assets/_App/Scripts/Bootstrap/SandboxSceneScope.cs` | Modify | Same removal. |
| `Assets/_App/Scripts/RigBuilder/RigRuntime.cs` (+`.meta`) | Delete | Dead. |
| `Assets/_App/Scripts/RigBuilder/IRigRuntime.cs` (+`.meta`) | Delete | Dead. |
| `Assets/_App/Scripts/SpatialUi/Panels/IkWizardPanel.cs` (+`.meta`) | Delete | Dead. |
| `Assets/_App/Scripts/SpatialUi/Panels/BoneInspectorPanel.cs` (+`.meta`) | Delete | Dead. |

**Task order matters for compilation:** Task 3 removes every *reference* to the dead types first, so Task 4's file deletion leaves nothing dangling.

---

## Task 1: Bug 1 — reset interaction context on scene/mode transition

**Files:**
- Modify: `Assets/_App/Scripts/VrInteraction/InteractionMaskBinder.cs`

**Background:** `InteractionMaskBinder` lives on the persistent XR rig and sets the XR casters' physics mask from three flags (`_bonesMode`, `_panelOpen`, `_hasSelection`). Those flags are driven by scene-scoped events that are NOT re-emitted on a fresh scene, and the binder has no reset hook — so a session ending with a selection leaves the mask stuck on `GizmoHandles`/`BoneProxies`, and on re-entry the ray can't hit `SceneObjects`. `ModeChangedEvent` (`{ AppMode PreviousMode; AppMode CurrentMode; }`) is published on the root `EventBus` by `ModeOrchestrator` *after* the new scene/scope exist — the right reset signal. No automated test (this is a play-mode persistent-state/scene-lifecycle path that EditMode cannot exercise — `OnEnable` and event delivery do not run on `AddComponent` in edit mode); verify by VR/play-mode reproduction at the checkpoint.

- [ ] **Step 1: Subscribe to `ModeChangedEvent` in `Construct`**

In `Construct`, add the subscription as the last line (after the `SelectionChangedEvent` subscribe):

```csharp
    [Inject]
    public void Construct(EventBus bus)
    {
        _bus = bus;
        _bus.Subscribe<BonesVisibilityChangedEvent>(OnBonesVisibility);
        _bus.Subscribe<GizmoToolsPanelOpenedEvent>(OnGizmoPanelOpened);
        _bus.Subscribe<GizmoToolsPanelClosedEvent>(OnGizmoPanelClosed);
        _bus.Subscribe<SelectionChangedEvent>(OnSelectionChanged);
        _bus.Subscribe<ModeChangedEvent>(OnModeChanged);
    }
```

- [ ] **Step 2: Unsubscribe in `OnDestroy`**

Add the matching unsubscribe as the last line of `OnDestroy` (after the `SelectionChangedEvent` unsubscribe):

```csharp
    private void OnDestroy()
    {
        if (_bus == null) return;
        _bus.Unsubscribe<BonesVisibilityChangedEvent>(OnBonesVisibility);
        _bus.Unsubscribe<GizmoToolsPanelOpenedEvent>(OnGizmoPanelOpened);
        _bus.Unsubscribe<GizmoToolsPanelClosedEvent>(OnGizmoPanelClosed);
        _bus.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);
        _bus.Unsubscribe<ModeChangedEvent>(OnModeChanged);
    }
```

- [ ] **Step 3: Add the `OnModeChanged` handler**

Add this method directly below the existing one-line handlers (`OnBonesVisibility`/`OnGizmoPanelOpened`/`OnGizmoPanelClosed`/`OnSelectionChanged`):

```csharp
    // A scene/mode transition reuses this persistent binder. Scene-scoped publishers (selection,
    // gizmo panel, bones toggle) do NOT re-emit their "off" state for the new scene, so without an
    // explicit reset the caster mask can stay stuck on GizmoHandles/BoneProxies from the previous
    // session — the ray then can't hit SceneObjects and nothing is clickable. Reset to the default
    // object-selection context. ModeChangedEvent fires after the new scene/scope are live.
    private void OnModeChanged(ModeChangedEvent _)
    {
        _bonesMode    = false;
        _panelOpen    = false;
        _hasSelection = false;
        Apply();
    }
```

- [ ] **Step 4: Checkpoint**

Orchestrator runs the Checkpoint (refresh → console → EditMode tests). Expected: clean compile; failure set is exactly the 6 baseline. Manual VR check (do at first runtime opportunity): select an object in `VrEditing`, go to `MainMenu`, re-enter `VrEditing`, confirm an object is selectable by clicking.

---

## Task 2: A2 — encapsulate the collider re-registration

**Files:**
- Modify: `Assets/_App/Scripts/VrInteraction/XRPromeonInteractable.cs`
- Test (existing, no change): `Assets/_App/Tests/VrInteraction/XRPromeonInteractableColliderMapTests.cs`

**Background:** `RegisterColliders` currently inlines an `Unregister/Register` against the `XRInteractionManager`, with a comment that wrongly attributes it to manual rigging. The real reason is spawn-time ordering. This task moves the re-index into a named private method and rewrites the comment truthfully. **Behaviour is unchanged** — the existing regression test must stay green.

- [ ] **Step 1: Confirm the existing regression test passes before the change**

Run (orchestrator): `run_tests` (mode:`EditMode`, test_names:`["XRPromeonInteractableColliderMapTests.RegisterColliders_AfterRegistration_ResolvesThroughManagerMap"]`) + `get_test_job`.
Expected: PASS (this guards the behaviour we're about to refactor).

- [ ] **Step 2: Replace `RegisterColliders` body + add `RefreshColliderRegistration`**

Replace the entire current `RegisterColliders` method (the version containing the inline `interactionManager.UnregisterInteractable(...)`/`RegisterInteractable(...)` block and its comment) with the following two methods:

```csharp
    public void RegisterColliders(IEnumerable<Collider> source)
    {
        if (source == null) return;
        bool added = false;
        foreach (var c in source)
            if (c != null && !colliders.Contains(c))
            {
                colliders.Add(c);
                added = true;
            }
        if (added) RefreshColliderRegistration();
    }

    // XRInteractionManager builds its collider→interactable lookup once, at registration time
    // (OnEnable → RegisterInteractable), and never re-scans it — UnregisterInteractable even documents
    // the assumption that the collider list won't change afterward. There is no incremental
    // "colliders changed" API. Our spawn pipeline appends colliders (object convex children, rig
    // selector boxes) right AFTER the interactable was instantiated and has already registered, so
    // those late colliders are invisible to the ray (selectable via the outliner, never by clicking)
    // until we re-index. Re-register to rebuild the map over the full set. No-op until we're
    // registered (inactive GO): OnEnable will map the complete list itself. The app has no live
    // (post-spawn) collider mutation — manual rigging is not reachable in the shipping build.
    private void RefreshColliderRegistration()
    {
        if (interactionManager == null || !interactionManager.IsRegistered((IXRInteractable)this)) return;
        interactionManager.UnregisterInteractable((IXRInteractable)this);
        interactionManager.RegisterInteractable((IXRInteractable)this);
    }
```

(`IXRInteractable` is already in scope — the file already uses it; no new `using` needed.)

- [ ] **Step 3: Checkpoint**

Orchestrator runs the Checkpoint. Expected: clean compile; `XRPromeonInteractableColliderMapTests` still PASS; failure set exactly the 6 baseline.

---

## Task 3: Remove all references to the dead manual-rigging types

**Files:**
- Modify: `Assets/_App/Scripts/Bootstrap/SceneContext.cs`
- Modify: `Assets/_App/Scripts/Bootstrap/SceneContextBinder.cs`
- Modify: `Assets/_App/Scripts/Bootstrap/VrEditingSceneScope.cs`
- Modify: `Assets/_App/Scripts/Bootstrap/SandboxSceneScope.cs`

**Background:** `IRigRuntime`/`RigRuntime`/`IkWizardPanel`/`BoneInspectorPanel` are unreachable (verified: no scene/prefab/test references; all DI registrations are null-guarded and `SceneContext.Rig` has no reader). This task strips every *reference* so the next task can delete the files cleanly. After this task the four dead types still exist but are referenced by nothing.

- [ ] **Step 1: Remove `Rig` from `SceneContext`**

Replace the full contents of `Assets/_App/Scripts/Bootstrap/SceneContext.cs` with:

```csharp
// Root-scoped façade exposing the currently-loaded scene scope's services, or null when no
// scene scope is live. Persistent UI (UserPanel modules) will read scene services THROUGH this
// (later plan) so it never holds a reference that outlives the scene scope. Populated/cleared
// only by SceneContextBinder.
public class SceneContext
{
    public SceneGraph        Graph     { get; private set; }
    public ISelectionManager Selection { get; private set; }
    public CommandStack      Commands  { get; private set; }
    public GizmoController    Gizmo     { get; private set; }
    public AnimationAuthoring Authoring { get; private set; }
    public AnimationClock     Clock     { get; private set; }

    public bool HasScene => Graph != null;

    public void Bind(SceneGraph graph, ISelectionManager selection, CommandStack commands,
                     GizmoController gizmo, AnimationAuthoring authoring, AnimationClock clock)
    {
        Graph = graph; Selection = selection; Commands = commands;
        Gizmo = gizmo; Authoring = authoring; Clock = clock;
    }

    public void Clear()
    {
        Graph = null; Selection = null; Commands = null;
        Gizmo = null; Authoring = null; Clock = null;
    }
}
```

- [ ] **Step 2: Stop resolving `IRigRuntime` in `SceneContextBinder`**

In `Assets/_App/Scripts/Bootstrap/SceneContextBinder.cs`: (a) update the class comment so it no longer lists `RigRuntime`, and (b) drop the `Resolve<IRigRuntime>()` argument from the `_ctx.Bind(...)` call.

Replace the comment block (lines beginning `// Scene-scoped entry point:`) — change the parenthetical from:

```
// (Sandbox registers no AnimationAuthoring/AnimationClock/RigRuntime), so each is resolved
```

to:

```
// (Sandbox registers no AnimationAuthoring/AnimationClock), so each is resolved
```

Replace the `_ctx.Bind(...)` call:

```csharp
        _ctx.Bind(
            Resolve<SceneGraph>(),
            Resolve<ISelectionManager>(),
            Resolve<CommandStack>(),
            Resolve<GizmoController>(),
            Resolve<AnimationAuthoring>(),
            Resolve<AnimationClock>(),
            Resolve<IRigRuntime>());
```

with:

```csharp
        _ctx.Bind(
            Resolve<SceneGraph>(),
            Resolve<ISelectionManager>(),
            Resolve<CommandStack>(),
            Resolve<GizmoController>(),
            Resolve<AnimationAuthoring>(),
            Resolve<AnimationClock>());
```

- [ ] **Step 3: Remove the dead find+register blocks from `VrEditingSceneScope`**

In `Assets/_App/Scripts/Bootstrap/VrEditingSceneScope.cs`, delete these three blocks (currently between the `undo` block and the `propPanel` block):

```csharp
        var rigRuntime = Object.FindAnyObjectByType<RigRuntime>(FindObjectsInactive.Include);
        if (rigRuntime != null) builder.RegisterInstance(rigRuntime).AsImplementedInterfaces().AsSelf();

        var ikWizard = Object.FindAnyObjectByType<IkWizardPanel>(FindObjectsInactive.Include);
        if (ikWizard != null) builder.RegisterInstance(ikWizard);

        var bonePanel = Object.FindAnyObjectByType<BoneInspectorPanel>(FindObjectsInactive.Include);
        if (bonePanel != null) builder.RegisterInstance(bonePanel);

```

The `undo` block must remain immediately followed by the `propPanel` block.

- [ ] **Step 4: Remove the dead find+register blocks from `SandboxSceneScope`**

In `Assets/_App/Scripts/Bootstrap/SandboxSceneScope.cs`, delete the identical three blocks (currently between the `undo` block and the `propPanel` block):

```csharp
        var rigRuntime = Object.FindAnyObjectByType<RigRuntime>(FindObjectsInactive.Include);
        if (rigRuntime != null) builder.RegisterInstance(rigRuntime).AsImplementedInterfaces().AsSelf();

        var ikWizard = Object.FindAnyObjectByType<IkWizardPanel>(FindObjectsInactive.Include);
        if (ikWizard != null) builder.RegisterInstance(ikWizard);

        var bonePanel = Object.FindAnyObjectByType<BoneInspectorPanel>(FindObjectsInactive.Include);
        if (bonePanel != null) builder.RegisterInstance(bonePanel);

```

- [ ] **Step 5: Checkpoint**

Orchestrator runs the Checkpoint. Expected: clean compile (the 4 dead types still exist but are now unreferenced); failure set exactly the 6 baseline.

---

## Task 4: Delete the dead manual-rigging scripts

**Files:**
- Delete: `Assets/_App/Scripts/RigBuilder/RigRuntime.cs` + `Assets/_App/Scripts/RigBuilder/RigRuntime.cs.meta`
- Delete: `Assets/_App/Scripts/RigBuilder/IRigRuntime.cs` + `Assets/_App/Scripts/RigBuilder/IRigRuntime.cs.meta`
- Delete: `Assets/_App/Scripts/SpatialUi/Panels/IkWizardPanel.cs` + `Assets/_App/Scripts/SpatialUi/Panels/IkWizardPanel.cs.meta`
- Delete: `Assets/_App/Scripts/SpatialUi/Panels/BoneInspectorPanel.cs` + `Assets/_App/Scripts/SpatialUi/Panels/BoneInspectorPanel.cs.meta`

**Background:** With every reference removed (Task 3), these four scripts compile to nothing-used. None is placed in any scene/prefab (verified by GUID), so deletion creates no missing-script. `RigDefinitionExtractor`, `RigDefinition`/`IkChainRecord`, `ProxyRigRuntime`, `RigEntityFactory`, `RigEntityBuilder`, and `InspectorPanel` are LIVE and must NOT be touched.

- [ ] **Step 1: Delete the four scripts and their `.meta` files**

Remove all eight files listed above (each `.cs` together with its `.cs.meta`).

PowerShell (note `-LiteralPath` — the repo path contains `[02]` brackets):

```powershell
$base = "S:\[02] Projects\[02] Study\[00] Repositories\PromeonLab\Assets\_App\Scripts"
Remove-Item -LiteralPath "$base\RigBuilder\RigRuntime.cs", "$base\RigBuilder\RigRuntime.cs.meta", `
  "$base\RigBuilder\IRigRuntime.cs", "$base\RigBuilder\IRigRuntime.cs.meta", `
  "$base\SpatialUi\Panels\IkWizardPanel.cs", "$base\SpatialUi\Panels\IkWizardPanel.cs.meta", `
  "$base\SpatialUi\Panels\BoneInspectorPanel.cs", "$base\SpatialUi\Panels\BoneInspectorPanel.cs.meta"
```

- [ ] **Step 2: Checkpoint**

Orchestrator runs the Checkpoint (full refresh forces Unity to drop the deleted types). Expected: clean compile; failure set exactly the 6 baseline; no new `error CS` referencing `RigRuntime`/`IRigRuntime`/`IkWizardPanel`/`BoneInspectorPanel`.

---

## Final Verification (after all tasks)

- [ ] Full `run_tests` (mode:`EditMode`) + `get_test_job`: total count drops by 0 tests (no test referenced the deleted types); failures are exactly the 6 baseline, zero new.
- [ ] `read_console` (types:`[error]`, filter_text:`CS`): no `error CS####`.
- [ ] Hand back to the user for VR verification of Bug 1 (select → MainMenu → re-enter VrEditing → click-select works) and a smoke check that object/rig spawning + selection still work.

---

## Self-Review

**Spec coverage:**
- Spec Part 1 (context reset) → Task 1. ✓
- Spec Part 2 (encapsulate re-register) → Task 2. ✓
- Spec Part 3 (delete dead scripts + unwire scopes/SceneContext/Binder) → Task 3 (unwire) + Task 4 (delete). ✓
- Spec "keep live types" constraint → called out in Task 4 background. ✓
- Spec out-of-scope (bone poses, build-then-enable) → not present in any task. ✓

**Placeholder scan:** No TBD/TODO/"handle edge cases"/vague steps. Every code step shows full replacement code. ✓

**Type consistency:** `RefreshColliderRegistration()` named identically in Task 2 method + body. `OnModeChanged`/`ModeChangedEvent` consistent across subscribe/unsubscribe/handler in Task 1. `SceneContext.Bind` 6-arg signature in Task 3 Step 1 matches the 6-arg call in Task 3 Step 2. ✓

**Compilation order:** Task 3 removes all references before Task 4 deletes the types — no dangling-reference window. ✓
