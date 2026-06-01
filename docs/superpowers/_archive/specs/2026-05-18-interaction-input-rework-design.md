# Interaction Input Rework — Design

**Date:** 2026-05-18
**Status:** Approved (pending implementation)
**Supersedes (partially):** `2026-05-18-scene-ui-interaction-fixes-design.md` Item 4 (`XRPromeonInteractable` semantics)

## Goal

Replace the `XRPromeonInteractable` tap-vs-hold-on-Select state machine with a button-driven scheme that reads inputs directly from `NearFarInteractor`. Solves two production bugs and aligns behavior to physical button semantics the user expects.

## Bugs being fixed

1. **Click in 3D doesn't select / deselect.** Current code listens only to XRI `selectEntered`/`selectExited`, which fire on the **Select** action (grip button by default). User presses **trigger** expecting selection — nothing happens, because trigger fires the **Activate** action, which the Interactable never reads.
2. **Distance-based drag-mode discrimination (Near=6DoF / Far=PositionOnly) is the wrong axis.** User intent is button-driven, not distance-driven: trigger held = move, grip held = rotate, regardless of how close the controller is to the object.

## Constraints

- Unity 6000.3.7f1, URP, XR Interaction Toolkit **3.0.7** (verified against source in `Library/PackageCache/com.unity.xr.interaction.toolkit@3c62ab08b942`).
- `NearFarInteractor` on `User XR Origin (XR Rig)` prefab variant. `WorldClickCatcher` refs already wired there.
- VContainer DI; `IObjectResolver` lazy resolution pattern for `GizmoController` already in place (`SelectionInteractorFactory`).
- All transform mutations commit through `CommandStack` via `GizmoController.CommitTransform` (already exists).
- `ISelectionManager` (already exists with `Toggle` / `Select` / `Clear` / `SelectedIds`).

## Non-Goals

- Multi-grab (drag whole group). `IDragStrategy` keeps the seam, but no `GroupDragStrategy` is added.
- Snap-to-grid / surface alignment.
- Haptic feedback on edges.
- Per-user tunable tap window in app settings.
- Customizing controller button bindings at runtime.

---

## Final input mapping

| Physical button | XRI input action | Tap (short press) | Hold (≥ tapWindow) |
|---|---|---|---|
| **Trigger** (front, "курок") | `activateInput` | `SelectionManager.Toggle(NodeId)` | Position drag (PositionOnly), if object is in `SelectedIds` |
| **Grip** (side squeeze) | `selectInput` | — (ignored) | Rotation drag (RotationOnly), if object is in `SelectedIds` |

Gating rule: **manipulation (move/rotate) requires the object to already be in `SelectedIds`** at the moment a hold-state is entered. Trigger tap is the only path to add an object to selection. Grip on an unselected object is a no-op.

`tapWindow = 0.5s` (SerializeField, tunable in inspector).

---

## State machine — `XRPromeonInteractable`

Component inherits `XRBaseInteractable` purely for hover/registration. Standard XRI select-flow is **disabled**:

```csharp
public override bool IsSelectableBy(IXRSelectInteractor interactor) => false;
```

This blocks `interactorsSelecting` from ever populating, prevents `OnSelectEntered`/`OnSelectExited` from firing, and leaves `IsHoverableBy` untouched (default `true`). Verified at `XRBaseInteractable.cs:819` and `830` — the two methods are independent virtuals.

### States

```
Idle
 ├─ trigger press edge (any interactor in interactorsHovering, or _lastHovering fallback)
 │     → Lock(interactor), _pressTime = Time.time, _state = TriggerPressed
 │
 └─ grip press edge AND IsObjectSelected()
       → Lock(interactor), CaptureRotationOffset, _state = GripRotate

TriggerPressed
 ├─ trigger release this frame
 │     → SelectionManager.Toggle(NodeId), Reset()
 ├─ Time.time - _pressTime > _tapWindow AND IsObjectSelected()
 │     → CapturePositionOffset(), _state = TriggerMove
 └─ otherwise: stay in TriggerPressed

TriggerMove
 ├─ trigger release this frame
 │     → GizmoController.CommitTransform(transform.position, transform.rotation, transform.localScale), Reset()
 └─ otherwise: ApplyMove()

GripRotate
 ├─ grip release this frame
 │     → GizmoController.CommitTransform(...), Reset()
 └─ otherwise: ApplyRotate()
```

### Lock semantics

Once `_locked = NearFarInteractor` is set:
- Subsequent input reads only consult `_locked`, not `interactorsHovering`.
- A second controller's input is ignored entirely until `Reset()`.
- If `_locked` becomes `null` or `!_locked.isActiveAndEnabled` between frames → forced `Reset()`. Defends against the object being despawned (or interactor being disabled) mid-drag.

### `_lastHoveringInteractor` fallback

Reasoning: a 1-frame `interactorsHovering` dropout (ray jitter) at the exact frame of trigger-press would otherwise lose the edge entirely. Each frame in `Idle` we cache the first non-null `NearFarInteractor` from `interactorsHovering`; when an edge is detected and current `interactorsHovering` is empty, we fall back to the cached interactor. Cache is invalidated on `Reset()`.

### One-interactor-wins ordering

When trigger and grip press edges fire in the **same frame** on the same hovered object, the `Idle` branch checks `activateInput` first (trigger), then `selectInput` (grip). Trigger wins. Documented inline as a comment, no runtime config.

---

## Input reading

```csharp
// Inside ProcessInteractable(Dynamic):

if (_state != State.Idle && (_locked == null || !_locked.isActiveAndEnabled))
{ Reset(); return; }

switch (_state)
{
    case State.Idle:
        UpdateLastHovering();   // refresh _lastHoveringInteractor cache
        var ni = ActiveHoverer();   // current or _lastHovering fallback
        if (ni == null) break;

        if (ni.activateInput.ReadWasPerformedThisFrame())
        { Lock(ni); _pressTime = Time.time; _state = State.TriggerPressed; break; }

        if (ni.selectInput.ReadWasPerformedThisFrame() && IsObjectSelected())
        { Lock(ni); CaptureRotationOffset(); _state = State.GripRotate; break; }
        break;

    case State.TriggerPressed:
        if (_locked.activateInput.ReadWasCompletedThisFrame())
        { _selectionManager.Toggle(_node.NodeId); Reset(); break; }
        if (Time.time - _pressTime > _tapWindow && IsObjectSelected())
        { CapturePositionOffset(); _state = State.TriggerMove; }
        break;

    case State.TriggerMove:
        if (_locked.activateInput.ReadWasCompletedThisFrame())
        { _gizmoController.CommitTransform(transform, transform.position, transform.rotation, transform.localScale); Reset(); break; }
        ApplyMove();
        break;

    case State.GripRotate:
        if (_locked.selectInput.ReadWasCompletedThisFrame())
        { _gizmoController.CommitTransform(transform, transform.position, transform.rotation, transform.localScale); Reset(); break; }
        ApplyRotate();
        break;
}
```

### Offsets

Captured at the moment of entering a drag state (not at press) — guarantees no positional/rotational pop:

```csharp
private void CapturePositionOffset()
{
    var attach = _locked.GetAttachTransform(this);
    _grabPosOffset = transform.position - attach.position;
}

private void CaptureRotationOffset()
{
    var attach = _locked.GetAttachTransform(this);
    _grabRotOffset = Quaternion.Inverse(attach.rotation) * transform.rotation;
}
```

### Apply

```csharp
private void ApplyMove()
{
    var attach = _locked.GetAttachTransform(this);
    var targetPos = attach.position + _grabPosOffset;
    _dragStrategy.Apply(transform, targetPos, transform.rotation, DragMode.PositionOnly);
}

private void ApplyRotate()
{
    var attach = _locked.GetAttachTransform(this);
    var targetRot = attach.rotation * _grabRotOffset;
    _dragStrategy.Apply(transform, transform.position, targetRot, DragMode.RotationOnly);
}
```

### Caches

`_node` (`SceneNode`) and `_selectable` references are populated in `Construct(...)` via `GetComponentInParent<SceneNode>()`. `SelectionInteractorFactory` already ensures `SceneNode`/`Selectable` are present before `Construct` runs.

```csharp
[Inject]
public void Construct(ISelectionManager sm, GizmoController gc)
{
    _selectionManager = sm;
    _gizmoController  = gc;
    _node             = GetComponentInParent<SceneNode>();
}
```

### `IsObjectSelected()`

```csharp
private bool IsObjectSelected()
    => _node != null && _selectionManager != null
       && _selectionManager.SelectedIds.Contains(_node.NodeId);
```

---

## `WorldClickCatcher` — switch to trigger edge

### Change

Replace grip edge-detection with trigger edge:

```csharp
[SerializeField] private NearFarInteractor _leftInteractor;
[SerializeField] private NearFarInteractor _rightInteractor;

private ISelectionManager _selectionManager;

[Inject]
public void Construct(ISelectionManager sm) => _selectionManager = sm;

private void Update()
{
    Check(_leftInteractor);
    Check(_rightInteractor);
}

private void Check(NearFarInteractor interactor)
{
    if (interactor == null || _selectionManager == null) return;
    if (!interactor.activateInput.ReadWasPerformedThisFrame()) return;
    if (IsOverUI(interactor)) return;

    foreach (var hovered in interactor.interactablesHovered)
    {
        var go = (hovered as MonoBehaviour)?.gameObject;
        if (go != null && go.GetComponentInParent<Selectable>() != null) return;
    }

    _selectionManager.Clear();
}

private static bool IsOverUI(NearFarInteractor interactor)
{
    var ray = interactor.GetComponentInChildren<XRRayInteractor>(includeInactive: true);
    if (ray != null && ray.TryGetCurrentUIRaycastResult(out var uiRaycast))
        return uiRaycast.gameObject != null;
    return false;
}
```

Removed: `_leftWasActive` / `_rightWasActive` fields — `IXRInputButtonReader` has built-in edge detection.

### No race with `XRPromeonInteractable`

Both read the same `activateInput.ReadWasPerformedThisFrame()` in the same frame. Verified at `XRBaseInputInteractor.cs:446` — XRI itself reads this method without affecting other readers, because `XRInputButtonReader` wraps `InputAction.WasPressedThisFrame` (frame-state, not consumable event).

Semantic separation is **geographic**:
- Trigger press while ray is on a `Selectable` → `interactor.interactablesHovered` contains it → `WorldClickCatcher` early-returns → no `Clear`. Meanwhile `XRPromeonInteractable.interactorsHovering` contains the interactor → state machine activates.
- Trigger press in empty space → `interactor.interactablesHovered` empty, no UI hit → `Clear`. Meanwhile no `XRPromeonInteractable` has the interactor in its `interactorsHovering` → no state machine runs.

---

## `IDragStrategy` cleanup

```csharp
public enum DragMode { PositionOnly, RotationOnly }   // SixDof removed (unused)

public class SingleDragStrategy : IDragStrategy
{
    public void Apply(Transform self, Vector3 targetPos, Quaternion targetRot, DragMode mode)
    {
        if (mode == DragMode.PositionOnly) self.position = targetPos;
        else                                self.rotation = targetRot;
    }
}
```

`SingleDragStrategy.Apply` is now a clean if/else (only two modes). Future `GroupDragStrategy` slots in by replacing `_dragStrategy` field, no other changes needed.

---

## Files touched

| File | Action |
|---|---|
| `Assets/_App/Subsystems/VrInteraction/XRPromeonInteractable.cs` | **Rewrite** — remove old SelectEnter/Exit overrides, add IsSelectableBy override, add direct-input state machine, drop distance-based discrimination, drop `_nearDistance`/`_moveThreshold`/`_attachStartPos` fields. Cache `_node`. |
| `Assets/_App/Subsystems/VrInteraction/IDragStrategy.cs` | Remove `SixDof` enum value, simplify `SingleDragStrategy.Apply` to two-branch if. |
| `Assets/_App/Subsystems/VrInteraction/WorldClickCatcher.cs` | Switch edge source from `isSelectActive` (grip) to `activateInput.ReadWasPerformedThisFrame()` (trigger). Remove `_was*Active` fields. |

No changes to:
- `SelectionInteractorFactory` (already attaches `XRPromeonInteractable`, registers colliders, lazy-resolves `GizmoController`).
- `Selectable`, `SelectionManager`, `SelectionVisualSync`.
- `GizmoController` (`CommitTransform` already exists).
- `SceneOutlinerView` / `SceneInspectorView` / scopes.
- `GizmoHandle` (separate XRGrabInteractable, no overlap with scene objects).
- `Selectable` outline visuals.

---

## Non-conflict audit

| Component | Channel | Conflict? |
|---|---|---|
| XRI standard select-flow on grip | `selectInput` → `m_LogicalSelectState` → `OnSelectEntered` | None. `IsSelectableBy` returns `false` → no select transitions on `XRPromeonInteractable`. Grip held is read by us directly. |
| XRI activate-while-selected flow | `activateInput` + `hasSelection` → `OnActivated` | None. `hasSelection=false` because select-flow is disabled → XRI never fires activate event for this object. We read raw activate input ourselves. |
| `XRUIInputModule` UI clicks | Trigger via "UI Press" action over UI canvas | None. UI hits don't populate `interactor.interactablesHovered` or `XRPromeonInteractable.interactorsHovering`. `WorldClickCatcher.IsOverUI` excludes them. |
| `GizmoHandle : XRGrabInteractable` | Standard select-flow on its own collider | None. Gizmo handles are separate GameObjects with own interaction layer. |
| `Selectable` outline | `SelectionChangedEvent` via `SelectionVisualSync` | None — passive subscriber, no input semantics. |

---

## Testing

### Manual on Quest 3 (after Build & Run)

| Scenario | Expected |
|---|---|
| Tap trigger (front) on unselected object | Object becomes Active in outliner; inspector shows it |
| Tap trigger on selected object | Object removed from selection |
| Tap trigger in empty space | All selection cleared |
| Tap trigger on UserPanel button / outliner row | UI button fires; selection unchanged |
| Hold trigger ≥ 0.5 s on selected object | Position drag begins; object slides along ray / follows hand |
| Release trigger after move | Position fixed; Ctrl+Z (or in-app Undo) reverts |
| Hold trigger ≥ 0.5 s on unselected object | Nothing visibly moves; on release, Toggle fires (object becomes selected) |
| Hold grip (side) on selected object | Rotation drag begins; object rotates around its pivot following controller orientation |
| Release grip after rotate | Rotation fixed; Undo reverts |
| Hold grip on unselected object | No-op |
| Trigger and grip pressed in same frame on selected object | Trigger wins → enters TriggerPressed branch |
| Object destroyed (via Command Stack Undo) while held in drag | `_locked` becomes null/disabled → `Reset()` → no exceptions, no dangling state |

### Tuning

`_tapWindow` SerializeField — adjust on the `XRPromeonInteractable` component on a spawned object in Play mode if 0.5s feels off; once a good value is found, set as the default in source.

---

## Risks (verified)

| # | Risk | Status | Note |
|---|---|---|---|
| 1 | `selectInput`/`activateInput` not public | Refuted | `XRBaseInputInteractor.cs:200, 214` public properties |
| 2 | `ProcessInteractable(Dynamic)` skips frames | Refuted | `XRInteractionManager.cs:414` per-frame |
| 3 | Multiple readers of `ReadWasPerformedThisFrame` cause conflicts | Refuted | InputSystem frame-state, not consumable |
| 4 | `IsSelectableBy=>false` breaks hover or visuals | Refuted | `IsHoverableBy` independent virtual |
| 5 | Ray-jitter loses edge | Accepted, mitigated | `_lastHoveringInteractor` fallback cache |
| 6 | Hover lists stale across frame phases | Refuted | XRI updates in Dynamic before `ProcessInteractable` |
| 7 | XRI activate-while-selected double-fires | Refuted | Gated on `hasSelection` which is always false for us |
| 8 | `_tapWindow` UX feel | Accepted, non-technical | SerializeField, tune on Quest 3 |
| 9 | Trigger over UI fires state machine | Refuted | UI hits separate path, `interactorsHovering` empty |
| 10 | Object destroyed mid-drag → dangling state | Accepted, mitigated | `_locked` null/disabled check forces `Reset()` |
| 11 | Same-frame trigger+grip ambiguity | Accepted, deterministic | Trigger checked first in switch — wins |
