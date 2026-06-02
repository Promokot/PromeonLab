# Interaction Context Reset + Collider-Registration Cleanup — Design

**Date:** 2026-06-01
**Status:** ✅ Implemented & verified (2026-06-01) — EditMode green (6 baseline only), VR-verified by user.
**Scope:** Three independent, cohesive changes in `VrInteraction` + `Bootstrap` wiring. No new features.

## Problem

Two runtime bugs and one code-quality smell surfaced after the type-keyed selection-collider
work:

1. **Bug 1 — re-entering a scene kills click-selection.** First entry into `VrEditing` is fine;
   after returning to `MainMenu` and re-entering, nothing can be selected by clicking (the outliner
   still selects). Objects spawn and save correctly — only ray/click selection is dead.

2. **Smell — manual `XRInteractionManager` re-registration.** The recent click-selection fix made
   `XRPromeonInteractable.RegisterColliders` call `Unregister/Register` on the interaction manager.
   It works but reads as if we are poking a "systemic" service. The rationale baked into the code
   (manual rigging) is factually wrong (see Investigation).

3. **Dead code — runtime manual-rigging path.** `IkWizardPanel`, `BoneInspectorPanel`, `RigRuntime`,
   and `IRigRuntime` are unreferenced by any scene/prefab/test. They exist only as wiring that never
   fires.

A third reported bug (bone poses not persisted across save/load) is **out of scope** — it is a
serialization gap in `SceneGraph.CaptureSnapshot`, tracked separately.

## Investigation (root causes, verified against the code)

### Bug 1 root cause — stale persistent interaction state

`InteractionMaskBinder` lives on the persistent XR rig (`DontDestroyOnLoad`) and switches the XR
casters' physics layer mask by context:

- `(_panelOpen && _hasSelection)` → `GizmoHandles`
- `_bonesMode` → `BoneProxies`
- otherwise → `SceneObjects`

Those flags are driven by `SelectionChangedEvent` / `GizmoToolsPanelOpened/Closed` /
`BonesVisibilityChangedEvent`. Two facts combine into the bug:

- `SelectionManager.Start()` is empty — entering a fresh scene does **not** publish
  `SelectionChanged(null)`, so `_hasSelection` is never reset for the new scene.
- `InteractionMaskBinder` does **not** subscribe to `ModeChangedEvent` / `SceneContextChangedEvent`
  — it has no reset hook at all.

So a session that ends with something selected (the normal case) leaves the persistent binder with
stale `_hasSelection`/`_panelOpen`, and on re-entry the caster mask is stuck on `GizmoHandles` (or
`BoneProxies`). The ray then cannot hit the `SceneObjects` layer → nothing is clickable. The very
first launch works because the binder starts with clean flags.

This is **independent** of the collider/manager work: `XR Interaction Manager` sits under
`PersistentRoot` (confirmed in `Bootstrap.unity`), so it is `DontDestroyOnLoad` and is **not**
recreated on scene reload.

### Smell — why the manager must be re-indexed at all

`XRInteractionManager` builds its `collider → interactable` lookup once, in
`RegisterInteractable` (called from `XRBaseInteractable.OnEnable`), by reading
`interactable.colliders` at that instant. `UnregisterInteractable` carries an explicit comment that
it assumes the collider list has not changed since registration. There is **no incremental
"colliders changed" API**.

At spawn the registry adds the interactable via `InteractionCapability.Apply` (its `OnEnable`
registers with an empty/root-only list) and then appends the real colliders (convex child colliders
for objects, bone selector boxes for rigs). Those late colliders are invisible to the ray unless we
re-index. So the re-register is genuinely required — but its trigger is **spawn-time ordering**, not
manual rigging.

### Dead code — manual rigging is unreachable

`RigRuntime.ApplyDefinition` / `BuildFromSkinnedMesh` are called only by `BoneInspectorPanel`
(`_buildRigButton`) and `IkWizardPanel`. By GUID search:

| Type | Script GUID | Scene/prefab refs | Status |
|---|---|---|---|
| `InspectorPanel` | `c80757e098dae5048a35a538cf3b7e97` | `SceneInspectorModule.prefab` | **live** |
| `BoneInspectorPanel` | `10f9c3b833735084fa42f947b0cd842c` | none | dead |
| `IkWizardPanel` | `a5b5ff4e63bf30248884449bbb45c36e` | none | dead |
| `RigRuntime` | `2d8086d90a1f24d4fa13d9f414aa25b4` | none | dead |

The live "show bones" UI is `InspectorPanel._showBonesToggle` → `ProxyRigRuntime.SetBonesInteractive`
+ `BonesVisibilityChangedEvent` — it only **toggles** already-built bone colliders, it never builds a
rig. The similarly-named `BoneInspectorPanel` is the dead one (naming collision caused the initial
confusion).

All DI registrations of the dead types are null-guarded
(`var x = FindAnyObjectByType<…>(); if (x != null) builder.Register…`) and
`SceneContextBinder.Resolve<IRigRuntime>()` is wrapped in `try/catch → null`. Because none of the
dead components exist in any scene, the guards never fire and `SceneContext.Rig` is always null. No
live code reads `SceneContext.Rig`.

## Design

### Part 1 — Bug 1: reset interaction context on scene/mode transition

**File:** `Assets/_App/Scripts/VrInteraction/InteractionMaskBinder.cs`

- Inject/subscribe to `ModeChangedEvent` on the existing root `EventBus` (the binder already injects
  `EventBus` in `Construct`). `ModeChangedEvent` fires *after* the new scene and its scope exist, so
  the binder is reset once the new scene is live.
- On `ModeChangedEvent`: set `_bonesMode = _panelOpen = _hasSelection = false`, then call `Apply()`.
  This forces the caster mask back to the default `SceneObjects` context for every scene entry.
- Add the matching `Unsubscribe<ModeChangedEvent>` in `OnDestroy` (the binder already unsubscribes
  its other events there).

Rationale: the reset is authoritative and self-contained in the one persistent component that owns
the mask. It does not depend on scene-scoped services re-emitting their "off" state.

**Behavioural contract:** after any `MainMenu ↔ VrEditing ↔ Sandbox` transition, the freshly loaded
scene starts in object-selection context (`SceneObjects` mask), nothing selected. In-session
behaviour (selecting → gizmo, toggling bones) is unchanged.

### Part 2 — A2: encapsulate the re-registration

**File:** `Assets/_App/Scripts/VrInteraction/XRPromeonInteractable.cs`

- Extract the `Unregister/Register` block from `RegisterColliders` into a private method
  `RefreshColliderRegistration()`.
- `RegisterColliders` keeps its current guarded behaviour: add new colliders to `colliders`, and if
  any were added AND we are already registered with a manager, call `RefreshColliderRegistration()`.
- Replace the comment with the accurate rationale: XRI builds the collider→interactable map once at
  `OnEnable` and never re-scans; the spawn pipeline appends colliders right after the interactable
  has registered, so we re-index here. Note explicitly that there is no live (post-spawn) collider
  mutation in the app.

No behavioural change — the existing fix already performs the action; this only restructures and
corrects the documentation. The regression test
`XRPromeonInteractableColliderMapTests.RegisterColliders_AfterRegistration_ResolvesThroughManagerMap`
continues to guard it.

### Part 3 — Remove dead manual-rigging code

**Delete (script + .meta):**

- `Assets/_App/Scripts/SpatialUi/Panels/BoneInspectorPanel.cs`
- `Assets/_App/Scripts/SpatialUi/Panels/IkWizardPanel.cs`
- `Assets/_App/Scripts/RigBuilder/RigRuntime.cs`
- `Assets/_App/Scripts/RigBuilder/IRigRuntime.cs`

**Unwire:**

- `Assets/_App/Scripts/Bootstrap/VrEditingSceneScope.cs` — remove the `RigRuntime`, `IkWizardPanel`,
  and `BoneInspectorPanel` `FindAnyObjectByType` + register blocks (current lines 34–41).
- `Assets/_App/Scripts/Bootstrap/SandboxSceneScope.cs` — remove the analogous blocks.
- `Assets/_App/Scripts/Bootstrap/SceneContext.cs` — remove the `Rig` property, the `rig` parameter in
  `Bind`, the `Rig = rig;` assignment, and the `Rig = null;` line in `Clear`. Update the comment in
  `SceneContextBinder` that lists `RigRuntime` as a per-scope-optional service.
- `Assets/_App/Scripts/Bootstrap/SceneContextBinder.cs` — remove `Resolve<IRigRuntime>()` from the
  `_ctx.Bind(...)` call.

**Keep (live — must NOT be touched):** `RigDefinitionExtractor`, `RigDefinition` and its records
(`IkChainRecord`, bone records — serialized into `recipe.rig`), `ProxyRigRuntime`, `RigEntityFactory`,
`RigEntityBuilder`, `InspectorPanel`.

**No scene/prefab/asset edits required** — none of the deleted types are placed in any scene or
prefab (verified by GUID), so there is no missing-script risk. **No test edits required** — no test
references the deleted types.

## Testing

- **Part 1:** No new automated test (the bug is a play-mode persistent-state/scene-lifecycle issue
  that EditMode cannot exercise — `OnEnable`/event delivery do not run on `AddComponent` in edit
  mode). Validate by VR/play-mode reproduction: select an object in `VrEditing`, return to
  `MainMenu`, re-enter `VrEditing`, confirm click-selection works.
- **Part 2:** Covered by the existing `XRPromeonInteractableColliderMapTests` (must stay green after
  the extraction).
- **Part 3:** Compilation is the gate — after deletion + unwiring the project must compile with no
  `error CS####`. Full EditMode run must show only the 6 known baseline failures
  (`PathProviderTests` ×4 — Windows `\`, `RingRotateStrategyTests` ×2), zero new failures.

## Out of scope

- **Bone-pose persistence** (`SceneGraph.CaptureSnapshot` saves only the root node transform, no
  per-bone poses). Separate serialization task.
- Any further build-then-enable refactor of the spawn pipeline (rejected: rigs need an explicit
  collider subset, and a deactivate/reactivate window adds lifecycle churn for no functional gain
  now that there is no live collider mutation).

## Risks

- Removing `SceneContext.Rig` changes the `SceneContext` constructor/`Bind` signature. Confirmed no
  live reader exists, but the implementer must update both call sites (`Bind` definition + the single
  `SceneContextBinder` caller) atomically so the project compiles.
- `RigDefinitionExtractor` is shared between the dead `RigRuntime` and the live `RigEntityBuilder`.
  It stays; only `RigRuntime` (one of its two callers) is removed.
