# Interaction Input Rework — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **NEVER auto-commit.** The user commits manually. Every task ends with "stage changes; user will commit" — do not invoke `git commit`.

**Goal:** Replace `XRPromeonInteractable`'s tap-vs-hold-on-Select state machine with a direct input-reading state machine driven by physical buttons: Trigger (front) tap = select, Trigger hold = position drag, Grip (side) hold = rotation drag. Switch `WorldClickCatcher` to listen on trigger edge so "click in empty space = deselect" matches the same Trigger semantics.

**Architecture:** `XRPromeonInteractable` keeps `XRBaseInteractable` inheritance only for hover registration. Standard XRI select-flow is disabled via `IsSelectableBy => false`. State machine runs in `ProcessInteractable(Dynamic)` and reads `NearFarInteractor.activateInput` (trigger) and `selectInput` (grip) directly. Manipulation is gated on `ISelectionManager.SelectedIds.Contains(NodeId)` — unselected objects can be tap-selected but not moved/rotated.

**Tech Stack:** Unity 6000.3.7f1, C# 9, VContainer DI, XR Interaction Toolkit 3.0.7 (`NearFarInteractor`, `XRBaseInteractable`, `XRInputButtonReader`), MessagePipe-style `EventBus`, NUnit Test Runner.

**Spec:** `docs/superpowers/specs/2026-05-18-interaction-input-rework-design.md`

**Supersedes (partially):** the Item 4 (`XRPromeonInteractable` semantics) of `docs/superpowers/specs/2026-05-18-scene-ui-interaction-fixes-design.md`. The component's _existence_, factory wiring, and collider hierarchy handling from the prior plan stay as-is.

---

## File Structure

### Modified files

- `Assets/_App/Subsystems/VrInteraction/XRPromeonInteractable.cs` — full rewrite of internal state machine (component identity, fields preserved where useful)
- `Assets/_App/Subsystems/VrInteraction/IDragStrategy.cs` — remove `SixDof` enum value, simplify `SingleDragStrategy.Apply`
- `Assets/_App/Subsystems/VrInteraction/WorldClickCatcher.cs` — replace grip edge detection with trigger edge detection

### Files NOT touched

- `SelectionInteractorFactory.cs` — already wires `XRPromeonInteractable` correctly with collider registration
- `Selectable.cs`, `SelectionManager.cs`, `SelectionVisualSync.cs` — no input semantics
- `GizmoController.cs` — `CommitTransform` already in place
- `GizmoHandle.cs` — separate `XRGrabInteractable`, no overlap
- Scope files (`VrEditingSceneScope`, `SandboxSceneScope`) — DI registrations correct

---

## Verification cadence

After every code change touching C# files:
1. Save the file.
2. Return to Unity Editor — wait for domain reload.
3. Check `Window > General > Console`: zero compile errors.

"Stage changes" means `git add <paths>` only. **Do not run `git commit`** — the user commits manually.

---

## Task 1 — `IDragStrategy` cleanup: drop `SixDof`

**Files:**
- Modify: `Assets/_App/Subsystems/VrInteraction/IDragStrategy.cs`

- [ ] **Step 1.1 — Rewrite the file**

Replace the entire content of `Assets/_App/Subsystems/VrInteraction/IDragStrategy.cs` with:

```csharp
using UnityEngine;

public enum DragMode { PositionOnly, RotationOnly }

public interface IDragStrategy
{
    void Apply(Transform self, Vector3 targetPos, Quaternion targetRot, DragMode mode);
}

public class SingleDragStrategy : IDragStrategy
{
    public void Apply(Transform self, Vector3 targetPos, Quaternion targetRot, DragMode mode)
    {
        if (mode == DragMode.PositionOnly) self.position = targetPos;
        else                                self.rotation = targetRot;
    }
}
```

- [ ] **Step 1.2 — Verify compile**

Switch to Unity, wait for domain reload, Console must be clean. `XRPromeonInteractable.cs` still uses `DragMode.PositionOnly` and `DragMode.RotationOnly` — both still exist. `DragMode.SixDof` was referenced only inside `SingleDragStrategy.Apply`'s switch case which we just removed — no other consumer.

- [ ] **Step 1.3 — Stage**

```bash
git add Assets/_App/Subsystems/VrInteraction/IDragStrategy.cs
```

Tell the user: "Task 1 staged."

---

## Task 2 — `XRPromeonInteractable` rewrite

**Files:**
- Modify: `Assets/_App/Subsystems/VrInteraction/XRPromeonInteractable.cs`

This is a single atomic rewrite of the file. The component identity, the `RegisterColliders(IEnumerable<Collider>)` API used by `SelectionInteractorFactory`, and the VContainer `[Inject] Construct(ISelectionManager, GizmoController)` signature are preserved. Everything else inside the class changes.

- [ ] **Step 2.1 — Replace the file's content**

Overwrite `Assets/_App/Subsystems/VrInteraction/XRPromeonInteractable.cs` with:

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using VContainer;

public class XRPromeonInteractable : XRBaseInteractable
{
    [SerializeField] private float _tapWindow = 0.5f;

    private ISelectionManager _selectionManager;
    private GizmoController   _gizmoController;
    private IDragStrategy     _dragStrategy = new SingleDragStrategy();
    private SceneNode         _node;

    private enum State { Idle, TriggerPressed, TriggerMove, GripRotate }
    private State              _state;
    private NearFarInteractor  _locked;
    private NearFarInteractor  _lastHovering;
    private float              _pressTime;
    private Vector3            _grabPosOffset;
    private Quaternion         _grabRotOffset;

    public void RegisterColliders(IEnumerable<Collider> source)
    {
        if (source == null) return;
        foreach (var c in source)
            if (c != null && !colliders.Contains(c))
                colliders.Add(c);
    }

    [Inject]
    public void Construct(ISelectionManager selectionManager, GizmoController gizmoController)
    {
        _selectionManager = selectionManager;
        _gizmoController  = gizmoController;
        _node             = GetComponentInParent<SceneNode>();
    }

    // Disable XRI standard select-flow. We read inputs directly. Hover still works
    // (IsHoverableBy stays default true).
    public override bool IsSelectableBy(IXRSelectInteractor interactor) => false;

    public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase phase)
    {
        base.ProcessInteractable(phase);
        if (phase != XRInteractionUpdateOrder.UpdatePhase.Dynamic) return;

        // Defensive: dangling lock from destroyed/disabled interactor.
        if (_state != State.Idle && (_locked == null || !_locked.isActiveAndEnabled))
        { Reset(); return; }

        switch (_state)
        {
            case State.Idle:
                UpdateLastHovering();
                var ni = CurrentHoverer();
                if (ni == null) break;

                // Order matters: trigger checked first → wins same-frame ties.
                if (ni.activateInput.ReadWasPerformedThisFrame())
                {
                    Lock(ni);
                    _pressTime = Time.time;
                    _state = State.TriggerPressed;
                    break;
                }

                if (ni.selectInput.ReadWasPerformedThisFrame() && IsObjectSelected())
                {
                    Lock(ni);
                    CaptureRotationOffset();
                    _state = State.GripRotate;
                }
                break;

            case State.TriggerPressed:
                if (_locked.activateInput.ReadWasCompletedThisFrame())
                {
                    if (_node != null) _selectionManager.Toggle(_node.NodeId);
                    Reset();
                    break;
                }
                if (Time.time - _pressTime > _tapWindow && IsObjectSelected())
                {
                    CapturePositionOffset();
                    _state = State.TriggerMove;
                }
                break;

            case State.TriggerMove:
                if (_locked.activateInput.ReadWasCompletedThisFrame())
                {
                    _gizmoController.CommitTransform(transform,
                        transform.position, transform.rotation, transform.localScale);
                    Reset();
                    break;
                }
                ApplyMove();
                break;

            case State.GripRotate:
                if (_locked.selectInput.ReadWasCompletedThisFrame())
                {
                    _gizmoController.CommitTransform(transform,
                        transform.position, transform.rotation, transform.localScale);
                    Reset();
                    break;
                }
                ApplyRotate();
                break;
        }
    }

    private void UpdateLastHovering()
    {
        foreach (var ix in interactorsHovering)
        {
            var ni = ix as NearFarInteractor;
            if (ni != null) { _lastHovering = ni; return; }
        }
    }

    private NearFarInteractor CurrentHoverer()
    {
        foreach (var ix in interactorsHovering)
        {
            var ni = ix as NearFarInteractor;
            if (ni != null) return ni;
        }
        // 1-frame jitter fallback.
        return _lastHovering != null && _lastHovering.isActiveAndEnabled ? _lastHovering : null;
    }

    private bool IsObjectSelected()
        => _node != null && _selectionManager != null
           && _selectionManager.SelectedIds.Contains(_node.NodeId);

    private void Lock(NearFarInteractor interactor) => _locked = interactor;

    private void Reset()
    {
        _locked       = null;
        _lastHovering = null;
        _state        = State.Idle;
    }

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

    private void ApplyMove()
    {
        var attach    = _locked.GetAttachTransform(this);
        var targetPos = attach.position + _grabPosOffset;
        _dragStrategy.Apply(transform, targetPos, transform.rotation, DragMode.PositionOnly);
    }

    private void ApplyRotate()
    {
        var attach    = _locked.GetAttachTransform(this);
        var targetRot = attach.rotation * _grabRotOffset;
        _dragStrategy.Apply(transform, transform.position, targetRot, DragMode.RotationOnly);
    }
}
```

- [ ] **Step 2.2 — Verify compile**

Switch to Unity, wait for domain reload, Console must be clean.

If a compile error references `interactorsHovering` not being iterable: `XRBaseInteractable.interactorsHovering` is `List<IXRHoverInteractor>` (public property) — verified in XRI 3.0.7 source. Cast inside the loop to `NearFarInteractor` (already done in the snippet).

If a compile error references `XRInteractionUpdateOrder.UpdatePhase.Dynamic`: the namespace import `using UnityEngine.XR.Interaction.Toolkit;` provides it.

- [ ] **Step 2.3 — Stage**

```bash
git add Assets/_App/Subsystems/VrInteraction/XRPromeonInteractable.cs
```

Tell the user: "Task 2 staged."

---

## Task 3 — `WorldClickCatcher`: switch to trigger edge

**Files:**
- Modify: `Assets/_App/Subsystems/VrInteraction/WorldClickCatcher.cs`

- [ ] **Step 3.1 — Rewrite the file**

Overwrite `Assets/_App/Subsystems/VrInteraction/WorldClickCatcher.cs` with:

```csharp
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using VContainer;

public class WorldClickCatcher : MonoBehaviour
{
    [SerializeField] private NearFarInteractor _leftInteractor;
    [SerializeField] private NearFarInteractor _rightInteractor;

    private ISelectionManager _selectionManager;

    [Inject]
    public void Construct(ISelectionManager selectionManager) => _selectionManager = selectionManager;

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
}
```

Changes from the previous version:
- Removed `_leftWasActive`/`_rightWasActive` fields (edge detection is built into `IXRInputButtonReader`).
- Removed `OnEnable()` (no edge-bootstrap needed).
- `Check` signature simplified — no `ref bool wasActive` parameter.
- Edge source: `interactor.activateInput.ReadWasPerformedThisFrame()` (trigger) instead of `interactor.isSelectActive` (grip).

- [ ] **Step 3.2 — Verify compile**

Switch to Unity, wait for domain reload, Console must be clean.

- [ ] **Step 3.3 — Stage**

```bash
git add Assets/_App/Subsystems/VrInteraction/WorldClickCatcher.cs
```

Tell the user: "Task 3 staged. **Code complete — Task 4 is manual on-device verification.**"

---

## Task 4 — Manual smoke test on Quest 3

Perform after Tasks 1–3 staged and committed.

- [ ] **Step 4.1 — Open `VrEditing` (or `Sandbox`) scene, Build & Run on Quest 3**

Unity → File > Build Settings → Android → Build And Run. Headset must be connected via Link or developer USB.

- [ ] **Step 4.2 — Verify selection via trigger tap**

In-headset:
- Point ray at a 3D object → quickly pull trigger (< 0.5s) → release.
- Expected: object becomes Active in `SceneOutliner`; inspector shows its data.
- Tap trigger again on the same object → expected: deselected.
- Tap trigger on a different object → expected: that object becomes Active.

If selection does not toggle:
- Ensure object has `XRPromeonInteractable` and at least one collider registered (factory does both at spawn time).
- Ensure `interactionLayerMask` on `NearFarInteractor` includes the layer the interactable uses (default for both — should match).

- [ ] **Step 4.3 — Verify deselect on empty trigger tap**

- Select an object via Step 4.2.
- Tap trigger pointing at empty space (no UI, no object under ray).
- Expected: selection cleared.

- [ ] **Step 4.4 — Verify UI clicks don't deselect**

- Select an object.
- Tap trigger on a UserPanel button or an outliner row.
- Expected: button fires its action / outliner row toggles; the original object's selection state changes only if that's what the button/row did (e.g., outliner row toggle), not as a spurious clear.

- [ ] **Step 4.5 — Verify trigger-hold = position drag (selected object only)**

- Select an object.
- Pull trigger and hold (> 0.5s) while pointing at it. Sweep the ray.
- Expected: object slides along the ray, rotation unchanged.
- Release trigger.
- Expected: position is committed (Undo via existing input reverts).

- [ ] **Step 4.6 — Verify trigger-hold on unselected object is a no-op (then becomes tap-select on release)**

- Ensure no objects selected (tap trigger in empty space first).
- Pull and hold trigger over an unselected object for > 0.5s without moving the controller.
- Expected: object does not move.
- Release trigger.
- Expected: object becomes selected (the hold-release falls back to Toggle because no rotation/move was activated).

- [ ] **Step 4.7 — Verify grip-hold = rotation drag (selected object only)**

- Select an object.
- Squeeze grip while pointing at it. Rotate the controller's wrist.
- Expected: object rotates around its own pivot, following the controller's rotation. Position unchanged.
- Release grip.
- Expected: rotation is committed (Undo reverts).

- [ ] **Step 4.8 — Verify grip-hold on unselected object is a no-op**

- Deselect everything.
- Squeeze grip over an unselected object.
- Expected: nothing happens. No movement, no selection.
- Release grip.
- Expected: still nothing.

- [ ] **Step 4.9 — Verify trigger + grip same-frame: trigger wins**

- Select an object.
- Pull both trigger and grip simultaneously over the object.
- Expected: hold continues as TriggerMove (position drag), not rotation. (Trigger checked first in the switch.)
- Release both.
- Expected: position committed.

- [ ] **Step 4.10 — Tuning**

If tap feels too long (selection requires too much trigger-press time):
- Edit `_tapWindow` SerializeField on the `XRPromeonInteractable` component of a spawned object in Play mode.
- Try values in 0.25–0.4 range.
- Once a good value is found, update the default in `XRPromeonInteractable.cs:9` (`[SerializeField] private float _tapWindow = 0.5f;`).

Tell the user the results and any tuning applied.

---

## Spec coverage check

| Spec section | Task(s) |
|---|---|
| Input mapping (Trigger/Grip → Activate/Select) | T2 (state machine), T3 (WorldClickCatcher) |
| State machine (Idle / TriggerPressed / TriggerMove / GripRotate) | T2 |
| `IsSelectableBy => false` (disable XRI select-flow) | T2 |
| `_lastHoveringInteractor` ray-jitter fallback | T2 (`UpdateLastHovering`, `CurrentHoverer`) |
| Trigger-first ordering on same-frame ties | T2 (switch order documented in code comment) |
| Dangling-lock defense (`_locked` null/disabled → Reset) | T2 (`if (_state != Idle && ...) { Reset(); return; }`) |
| Offset capture at drag-state entry (not at press) | T2 (`CapturePositionOffset`, `CaptureRotationOffset`) |
| `WorldClickCatcher` reads `activateInput` edge | T3 |
| Geographic conflict resolution with `WorldClickCatcher` (Selectable in hovered list) | T3 (existing `foreach interactablesHovered` guard preserved) |
| `IsOverUI` UI guard preserved | T3 (unchanged from prior implementation) |
| `IDragStrategy` cleanup (drop SixDof) | T1 |
| Manual on-device verification | T4 |
| Tuning `_tapWindow` | T4 Step 4.10 |
| Files NOT touched (`SelectionInteractorFactory`, `GizmoController`, scopes, etc.) | Confirmed in plan header |
