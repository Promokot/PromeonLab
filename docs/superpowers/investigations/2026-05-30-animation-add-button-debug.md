# Animation "Add Animation" Button Does Nothing — Static Root-Cause Investigation

Date: 2026-05-30
Mode: read-only static analysis (no runtime available)
Symptom: User reports that even the "Add Animation" button in the Animator UI does nothing
when clicked; nothing further could be tested.

All claims below are grounded in files read in full this session (file:line cited). No
fabricated references.

Files read in full:
- `Assets/_App/Scripts/SpatialUi/Panels/AnimatorPanel.cs`
- `Assets/_App/Scripts/SpatialUi/Panels/AnimatorSubEmptyState.cs`
- `Assets/_App/Scripts/Bootstrap/VrEditingSceneScope.cs`
- `Assets/_App/Scripts/Bootstrap/SandboxSceneScope.cs`
- `Assets/_App/Scripts/Bootstrap/SceneContext.cs`
- `Assets/_App/Scripts/Bootstrap/SceneContextBinder.cs`
- `Assets/_App/Scripts/Bootstrap/RootLifetimeScope.cs`
- `Assets/_App/Scripts/Animation/AnimationAuthoring.cs`

Path-confirmed but not opened (not needed for the root cause; listed for follow-up):
- `Assets/_App/Content/Prefabs/UI/Panels/UserPanel/AnimatorPanelModule.prefab`
- `Assets/_App/Scenes/_Sandbox/AnimatorPanelSandbox.unity`  (dedicated isolation test scene)
- `Assets/_App/Editor/AnimatorPanelModuleBuilder.cs`

---

## Summary

The full click chain is confirmed end-to-end. The Unity Button -> delegate -> handler ->
service wiring is intact in code:

- `AnimatorSubEmptyState.Awake()`: `_addAnimationButton.onClick.AddListener(() => OnAddAnimationClicked?.Invoke());`
  (`AnimatorSubEmptyState.cs:17-18`; button field `:9`; delegate `:11`).
- `AnimatorPanel.WireEmptyState()`: `_emptyState.OnAddAnimationClicked = OnAddAnimationClicked;`
  (`AnimatorPanel.cs:92`).
- Handler `AnimatorPanel.OnAddAnimationClicked()` (`AnimatorPanel.cs:147-153`):

```csharp
private void OnAddAnimationClicked()
{
    if (_ctx.Authoring == null) return;                                    // 149  silent no-op
    var owner = AnimationAuthoring.OwnerOf(_ctx.Selection?.SelectedNodeId);// 150
    if (string.IsNullOrEmpty(owner)) return;                              // 151  silent no-op
    _ctx.Authoring.CreateContainer(owner);                                // 152
}
```

The handler has two bare, log-free early returns (`:149`, `:151`). Either one makes the
click do nothing with zero console output — matching the report exactly.

**#1 root cause (confirmed from source): the button was almost certainly clicked in Sandbox
mode (or the `AnimatorPanelSandbox` isolation scene), where `_ctx.Authoring` is null, so the
handler dead-ends at `AnimatorPanel.cs:149`.** This is no longer an inference — the scope
files prove it:

- `VrEditingSceneScope` registers the animation services AND injects the panel:
  - `builder.RegisterEntryPoint<AnimationClock>(...)` — `VrEditingSceneScope.cs:60`
  - `builder.RegisterEntryPoint<AnimationAuthoring>(...)` — `VrEditingSceneScope.cs:61`
  - injects `AnimatorPanel` — `VrEditingSceneScope.cs:63-65`
- `SandboxSceneScope` does NEITHER. There is no `AnimationClock`/`AnimationAuthoring`
  registration anywhere in the file, and no `AnimatorPanel` inject block (compare the full
  listing `SandboxSceneScope.cs:10-82` against VrEditing `:60-65`).
- `SceneContextBinder` resolves each service defensively and returns null if unregistered:
  `Resolve<AnimationAuthoring>()` at `SceneContextBinder.cs:29`, with the try/catch that maps
  an unregistered service to null at `SceneContextBinder.cs:42-46`. The class comment at
  `:5-8` states verbatim: "Sandbox registers no AnimationAuthoring/AnimationClock/RigRuntime".
- `SceneContext.Authoring` is therefore null in Sandbox (`SceneContext.cs:11`, set only via
  `Bind`/`Clear`).

So in Sandbox the panel still renders (its `Refresh()` shows the empty state with the Add
button when `Authoring == null` — `AnimatorPanel.cs:212`, comment `:209-211`), but the Add
handler returns immediately at `:149`. Visible button, dead handler, no log. Exact match.

Important consequence: in Sandbox the AnimatorPanel is **not injected at all** (no inject
block in `SandboxSceneScope`). If the panel lives in the scene-scoped UI rather than the
persistent root UserPanel, then `_bus`/`_ctx` are null and `OnEnable()` bails at
`AnimatorPanel.cs:37` before wiring anything — see H4, which in Sandbox may co-occur with H1.

---

## Call Chain (button -> handler -> service)

1. Unity Button onClick -> delegate: `AnimatorSubEmptyState.cs:17-18` (field `:9`, delegate `:11`).
   - Listener added only `if (_addAnimationButton != null)` (`:17`) — unassigned prefab field = dead button.
2. Delegate assigned in `AnimatorPanel.OnEnable() -> WireEmptyState()`:
   - `OnEnable()` first guard: `if (_bus == null) return;` (`AnimatorPanel.cs:37`) — skips ALL wiring if not injected.
   - `WireEmptyState()`: `if (_emptyState == null) return;` (`:91`); assign delegate (`:92`); field `:11`.
3. Handler `AnimatorPanel.OnAddAnimationClicked()` (`:147-153`):
   - `if (_ctx.Authoring == null) return;` (`:149`).
   - `AnimationAuthoring.OwnerOf(_ctx.Selection?.SelectedNodeId)` (`:150`).
     `OwnerOf` returns null when input is null (`AnimationAuthoring.cs:35-41`).
   - `if (string.IsNullOrEmpty(owner)) return;` (`:151`).
   - `_ctx.Authoring.CreateContainer(owner)` (`:152`).
4. `CreateContainer` (`AnimationAuthoring.cs:49-61`): creates the container and publishes
   `AnimationContainerChangedEvent { Change = Added }` (`:53-57`).
5. `_ctx`/`_bus`/`_clipboard` injected via `[Inject] Construct(EventBus, AnimationClipboard, SceneContext)`
   (`AnimatorPanel.cs:27-33`).
6. UI refresh after success is event-driven: handler does NOT call `Refresh()`; it relies on
   the Added event handled at `AnimatorPanel.cs:118-137` (Added branch `:134-136`).

---

## Ranked Hypotheses

### H1 (most likely — CONFIRMED from source) — Clicked in Sandbox/AnimatorPanelSandbox: `_ctx.Authoring` is null -> silent no-op at `AnimatorPanel.cs:149`
Evidence (all from full reads):
- VrEditing registers `AnimationAuthoring`/`AnimationClock` (`VrEditingSceneScope.cs:60-61`)
  and injects the panel (`:63-65`).
- Sandbox registers neither and does not inject the panel (`SandboxSceneScope.cs:10-82`, no
  matching lines).
- `SceneContextBinder` returns null for unregistered services (`SceneContextBinder.cs:29, 42-46`;
  comment `:5-8`).
- `Refresh()` documents and handles exactly this: null Authoring -> show empty state with Add
  button (`AnimatorPanel.cs:209-212`).
- A dedicated isolation scene exists: `Assets/_App/Scenes/_Sandbox/AnimatorPanelSandbox.unity`.
- No console output because `:149` is a bare `return;`.

Verification:
- Reproduce in VrEditing (select a node, click Add). If it works there but not in
  Sandbox/AnimatorPanelSandbox, H1 is the cause.
- Temporary `Debug.Log` at `AnimatorPanel.cs:149` fires in Sandbox, not in VrEditing.

### H2 (CONFIRMED logic) — No node selected / unresolved owner -> silent no-op at `AnimatorPanel.cs:151` (also affects VrEditing)
Evidence:
- `owner = OwnerOf(_ctx.Selection?.SelectedNodeId)`; `OwnerOf(null) == null` (`AnimationAuthoring.cs:35-41`);
  then `if (string.IsNullOrEmpty(owner)) return;` (`AnimatorPanel.cs:150-151`).
- The empty state shown with no selection is `State.NoSelection` (`AnimatorPanel.cs:221`), and
  the Add button is wired unconditionally. If the prefab shows the Add button in the
  `_noSelectionPanel` (`AnimatorSubEmptyState.cs:7, 23`), clicking it can never succeed — there
  is no owner. This is a likely UX/design bug independent of H1.

Verification:
- In VrEditing, select a node, then click Add. If selecting first fixes it, H2 contributes.
- Log `owner` at `AnimatorPanel.cs:150`. Inspect the prefab: does the Add button live in the
  NoSelection panel?

### H3 — Prefab wiring gap (`_emptyState` or `_addAnimationButton` unassigned)
Evidence:
- `WireEmptyState()` no-ops if `_emptyState == null` (`AnimatorPanel.cs:91`); `Awake()` adds the
  onClick only if `_addAnimationButton != null` (`AnimatorSubEmptyState.cs:17`).
- Possible in `AnimatorPanelModule.prefab` (built by `AnimatorPanelModuleBuilder.cs`).

Verification:
- Open `Assets/_App/Content/Prefabs/UI/Panels/UserPanel/AnimatorPanelModule.prefab`; confirm
  both serialized fields are assigned. Cross-check `AnimatorPanelModuleBuilder.cs`.

### H4 — Panel not injected -> `_bus`/`_ctx` null -> `OnEnable` bails (CONFIRMED real in Sandbox)
Evidence:
- `OnEnable()` bails at `if (_bus == null) return;` (`AnimatorPanel.cs:37`) before wiring.
- `_bus`/`_ctx` set only via `[Inject] Construct` (`:27-33`).
- Sandbox does NOT inject `AnimatorPanel` (no inject block in `SandboxSceneScope.cs`), unlike
  VrEditing (`:63-65`). So if the AnimatorPanel is a scene-resident object in the Sandbox UI
  (not the persistent root UserPanel), it is never injected -> whole panel inert, button dead.
- This may co-occur with H1 in Sandbox; whichever runs first, the result is the same dead button.

Verification:
- Temporary log in `Construct` (`AnimatorPanel.cs:28`); if it never fires in the tested mode,
  injection is missing. Determine whether the panel is part of the persistent UserPanel (root-
  injected) or the scene UI (scene-injected, absent in Sandbox).

### H5 (least likely; real logic smell) — `CreateContainer` succeeds but UI never refreshes (first Added event dropped)
Evidence:
- `CreateContainer` publishes Added (`AnimationAuthoring.cs:53-57`).
- `OnContainerChanged` first guards `if (e.OwnerNodeId != _activeOwner) return;` (`AnimatorPanel.cs:120`).
  At click time no container exists, so `_activeOwner` is null (set null at `:220`/`:229`). The
  Added event carries the new (non-null) owner, so it is DROPPED at `:120` before reaching the
  Added branch at `:134-136`. The newly created container would therefore not appear until some
  other refresh (e.g. reselecting the node) fires `Refresh()`.
- This only applies once `_ctx.Authoring` is non-null (i.e. VrEditing). It does not explain
  Sandbox, but it would make even a "successful" Add look like it did nothing in VrEditing.

Verification:
- Log in `OnContainerChanged` (`AnimatorPanel.cs:118`): confirm an Added event arrives and is
  dropped at `:120` while `_activeOwner` is null. Fix candidate: in the Added branch, accept the
  event when `_activeOwner` is null and the new owner matches the current selection's owner, then
  `Refresh()`.

---

## DI / Scope Analysis (confirmed from source)

- `AnimatorPanel` consumes scene services only via the root `SceneContext` facade
  (`_ctx.Authoring/Clock/Selection/Graph`), injected through `Construct` (`AnimatorPanel.cs:27-33`).
- `SceneContext` is a root singleton (`RootLifetimeScope.cs:17`) whose service slots are filled
  ONLY by `SceneContextBinder` (`SceneContext.cs:1-30`).
- `SceneContextBinder` is registered per scene scope (`VrEditingSceneScope.cs:14`,
  `SandboxSceneScope.cs:14`) and resolves each service defensively, mapping unregistered ->
  null (`SceneContextBinder.cs:24-46`).
- VrEditing registers `AnimationClock` + `AnimationAuthoring` (`VrEditingSceneScope.cs:60-61`)
  and injects `AnimatorPanel` (`:63-65`).
- Sandbox registers NEITHER and injects no AnimatorPanel (`SandboxSceneScope.cs` full file).
- Net: in Sandbox, `SceneContext.Authoring == null` and the AnimatorPanel is uninjected.
  Both H1 and H4 hold there. In VrEditing, `Authoring` is non-null and the panel is injected,
  so failures there are H2 (no selection), H3 (prefab), or H5 (event drop).

Conclusion: If the user tested in Sandbox / `AnimatorPanelSandbox`, the button is dead by
construction (H1, possibly compounded by H4). The fix is either to test in VrEditing, or to
decide whether the Animator panel should even be reachable/active in Sandbox (it currently has
no backing services there).

---

## Console / Logging Expectation

Every failure path is silent: `AnimatorPanel.cs:149` and `:151` are bare `return;` (no
`Debug.Log`/`throw`/`ErrorDispatcher`); the H4 path bails silently at `:37`; the H5 path
silently drops the event at `:120`. This matches the user seeing nothing and no console output.
A temporary `Debug.LogWarning` at `:149` and `:151` will disambiguate H1 vs H2 instantly.
(Note: this silent-guard style sits uneasily next to CLAUDE.md's "don't swallow silently".)

---

## Recommended Next Checks (priority order)

1. Ask the user which mode/scene they tested. Sandbox/AnimatorPanelSandbox -> H1 (+H4).
   VrEditing -> H2/H3/H5.
2. If VrEditing: confirm a node was selected before clicking. Log `owner` at `AnimatorPanel.cs:150`.
3. Inspect `AnimatorPanelModule.prefab`: confirm `AnimatorPanel._emptyState` and
   `AnimatorSubEmptyState._addAnimationButton` are assigned; check whether the Add button sits
   in the NoSelection panel (H2 design bug).
4. Add temporary logs at `AnimatorPanel.cs:149` and `:151`; click Add per mode — the firing log
   names the exact guard.
5. Investigate the H5 event-drop: confirm whether the first Added event is dropped at
   `AnimatorPanel.cs:120` because `_activeOwner` is null, leaving a successful create invisible.
6. Decide product behavior for Sandbox: either hide/disable the Animator entry there, or wire
   the animation services into `SandboxSceneScope` (and inject the panel) if animating in
   Sandbox is intended.
