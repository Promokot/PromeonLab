# UserPanel Grab & Triple-Lock Implementation Plan

## ✅ Status: COMPLETED (2026-05-31) — verified in-headset

All 5 tasks done, two-stage reviewed, EditMode tests green (UserPanelLockModeTests 3/3, PanelGrabHandleTests 2/2), and confirmed working in VR by the user (grab stable, three lock modes + colour cycle correct, detach + scene transitions clean).

**Deviations from the plan as written (all intentional, applied during execution):**
- **Collider must be NON-trigger.** The plan said `isTrigger = true` — that was wrong. The rig's `NearFarInteractor` far/near casters use `RaycastTriggerInteraction`/`PhysicsTriggerInteraction = Ignore` (value `1`), so a trigger collider is invisible to the ray. Fixed to `m_IsTrigger: 0`. No Rigidbody needed (raycast hits static colliders).
- **Handle `Image.raycastTarget` set to `0`.** Removes uGUI/3D raycast contention so the ray resolves the handle as a clean 3D hit; the Image stays purely decorative.
- **Hover/grab tint added to `PanelGrabHandle`.** The plan's component had no visual feedback; added `_handleGraphic` + normal/hover/grab colours via `OnHoverEntered/Exited` and grab state.
- **`LateUpdate` rotation gate (Task 1 quality fix).** `FaceCameraBelow()` now runs during grab too (except `LockPositionRotation`) so a position-only grab keeps the panel facing the user; only `UpdateSmartFollow()` is suspended while grabbing.
- **`CycleLockMode` clears `_activeTarget`/`_followVelocity` on every transition** (was Follow-only) — robustness.
- **`PanelGrabHandle` MonoBehaviour needs the full `XRBaseInteractable` serialized field set** in the prefab YAML (esp. `m_InteractionLayers: m_Bits: 1`). A minimal hand-written block defaulted interaction layers to `Nothing`, silently disabling hover.
- **`PanelDragHandle.cs`** kept in repo but its one `MoveDelta` call was switched to `MoveTo` (UserPanel dropped `MoveDelta`); it is no longer on the prefab.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the main `UserPanel` grabbable with the grip (stable, world-space) and turn its lock into a three-state cycle (Follow / LockPosition / LockPositionRotation) that actually fixes the panel in world space.

**Architecture:** Replace the uGUI screen-drag (`PanelDragHandle`) with `PanelGrabHandle : XRBaseInteractable`, modeled on the project's `XRPromeonInteractable` grip-move path (direct-input read, attach-transform follow, position-only). Convert `UserPanel`'s `bool _locked` into a `LockMode` enum cycle gating follow/face-camera, and detach the panel from the persistent XR Rig at `Start` so locking holds world position.

**Tech Stack:** Unity 6000.3.7f1, C# (`_App.Runtime`, no namespaces), XR Interaction Toolkit 3.0.7 (`NearFarInteractor`), world-space uGUI Canvas, VContainer. Implementation via Unity MCP. Unity Test Runner (`_App.Tests`, EditMode) for the pure-logic tests.

**Testing approach:** Pure logic (lock-mode cycle, grab-offset transform math) is covered by EditMode unit tests run through MCP `run_tests`. VR integration (collider hits the ray, grip grab feel, reparent + DontDestroyOnLoad, the three lock behaviors) is verified in-headset per the acceptance checklist in Task 5 — matching this project's established MCP + in-headset workflow.

**Git:** Do NOT run any git commands. The user commits manually. Each code task closes with "save + verify 0 compile errors via `read_console`" instead of a commit.

---

## File Structure

| File | Responsibility |
|---|---|
| `Assets/_App/Scripts/SpatialUi/Panels/UserPanel.cs` | Lock-mode state machine, follow/face gating, 3-color indicator, detach-from-rig, grab API (`SetDragging`, `MoveTo`) |
| `Assets/_App/Scripts/SpatialUi/Behaviors/PanelGrabHandle.cs` | **New.** Grip-grab interactable on the handle bar; position-only world follow of the controller |
| `Assets/_App/Tests/SpatialUi/UserPanelLockModeTests.cs` | **New.** EditMode test: lock-mode cycle wraps through three modes |
| `Assets/_App/Tests/SpatialUi/PanelGrabHandleTests.cs` | **New.** EditMode test: grab-offset capture/apply round-trip |
| `Assets/_App/Content/Prefabs/UI/Panels/UserPanel/UserPanel.prefab` | Add BoxCollider + `PanelGrabHandle` to `DragHandle`, wire panel ref, remove `PanelDragHandle` |
| `Assets/_App/Scripts/SpatialUi/Behaviors/PanelDragHandle.cs` | Left in repo, unused (keep-don't-delete convention) |

---

## Task 1: UserPanel triple-lock state machine + grab API

Convert the binary `_locked` toggle into a three-mode cycle, gate `FaceCameraBelow()` behind the mode, add the absolute-position grab API, and rewire the lock button. Reparenting is Task 2.

**Files:**
- Modify: `Assets/_App/Scripts/SpatialUi/Panels/UserPanel.cs`
- Test: `Assets/_App/Tests/SpatialUi/UserPanelLockModeTests.cs`

- [ ] **Step 1: Write the failing EditMode test**

Create `Assets/_App/Tests/SpatialUi/UserPanelLockModeTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

public class UserPanelLockModeTests
{
    [Test]
    public void CycleLockMode_WrapsThroughThreeModes()
    {
        var go = new GameObject("UserPanel");          // inactive-safe: Awake/Start not called in EditMode
        var panel = go.AddComponent<UserPanel>();

        Assert.AreEqual(UserPanel.LockMode.Follow, panel.CurrentLockMode);
        panel.CycleLockMode();
        Assert.AreEqual(UserPanel.LockMode.LockPosition, panel.CurrentLockMode);
        panel.CycleLockMode();
        Assert.AreEqual(UserPanel.LockMode.LockPositionRotation, panel.CurrentLockMode);
        panel.CycleLockMode();
        Assert.AreEqual(UserPanel.LockMode.Follow, panel.CurrentLockMode);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void ResetPosition_ReturnsToFollow()
    {
        var go = new GameObject("UserPanel");
        var panel = go.AddComponent<UserPanel>();
        panel.CycleLockMode();                          // -> LockPosition
        panel.ResetPosition();
        Assert.AreEqual(UserPanel.LockMode.Follow, panel.CurrentLockMode);
        Object.DestroyImmediate(go);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run via MCP `run_tests` (mode `EditMode`, filter `UserPanelLockModeTests`) — or `Window > General > Test Runner > EditMode`.
Expected: FAIL — compile error, `UserPanel` has no `LockMode` / `CurrentLockMode` / `CycleLockMode`.

- [ ] **Step 3: Implement the lock-mode state machine in `UserPanel.cs`**

Replace the full contents of `Assets/_App/Scripts/SpatialUi/Panels/UserPanel.cs` with (this is the complete final file for Tasks 1+2 — Task 2 adds only `DetachToWorld` + its call, already included here so the file is whole):

```csharp
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class UserPanel : SpatialPanel
{
    public enum LockMode { Follow, LockPosition, LockPositionRotation }

    [Header("Navigation")]
    [SerializeField] private Button _mainMenuButton;
    [SerializeField] private Button _exitButton;

    [Header("Lock")]
    [SerializeField] private Button _lockButton;
    [SerializeField] private Image  _lockButtonImage;

    [Header("Smart Follow")]
    [SerializeField] private float _recenterAngle     = 45f;
    [SerializeField] private float _smoothTime        = 0.5f;
    [SerializeField] private float _minDistance       = 0.25f;
    [SerializeField] private float _preferredDistance = 0.7f;
    [SerializeField] private float _maxDistance       = 1.25f;
    [SerializeField] private float _yOffset           = -0.15f;
    [Range(0f, 0.5f)]
    [SerializeField] private float _faceBelowOffset   = 0.15f;

    private ModeOrchestrator _orchestrator;

    private LockMode _lockMode = LockMode.Follow;
    private bool     _initialized;
    private bool     _isDragging;
    private Vector3  _followVelocity;
    private Vector3? _activeTarget;

    public LockMode CurrentLockMode => _lockMode;

    private static readonly Color ColorFollow       = new Color(0.62f, 1.00f, 0.77f, 0.90f); // green
    private static readonly Color ColorLockPosition = new Color(1.00f, 0.78f, 0.35f, 0.90f); // amber
    private static readonly Color ColorLockPosRot   = new Color(1.00f, 0.42f, 0.42f, 0.90f); // red

    [Inject]
    public void Construct(ModeOrchestrator orchestrator) => _orchestrator = orchestrator;

    private void Start()
    {
        _mainMenuButton?.onClick.AddListener(OnMainMenu);
        _exitButton?.onClick.AddListener(OnExit);
        _lockButton?.onClick.AddListener(CycleLockMode);
        ApplyLockVisual();
        DetachToWorld();
    }

    protected override void LateUpdate()
    {
        if (_cameraTransform == null) return;
        if (_isDragging) return;                       // grab fully drives the transform (position-only)

        if (_lockMode == LockMode.Follow)
            UpdateSmartFollow();

        if (_lockMode != LockMode.LockPositionRotation)
            FaceCameraBelow();
    }

    private void UpdateSmartFollow()
    {
        if (!_initialized)
        {
            var fwd = GetCameraYawForward();
            transform.position = new Vector3(
                _cameraTransform.position.x + fwd.x * _preferredDistance,
                _cameraTransform.position.y + _yOffset,
                _cameraTransform.position.z + fwd.z * _preferredDistance);
            _initialized    = true;
            _followVelocity = Vector3.zero;
            return;
        }

        var camXZ   = new Vector3(_cameraTransform.position.x, 0f, _cameraTransform.position.z);
        var panelXZ = new Vector3(transform.position.x,        0f, transform.position.z);
        var delta   = panelXZ - camXZ;
        var xzDist  = delta.magnitude;

        if (xzDist > 0.001f)
        {
            var yaw   = GetCameraYawForward();
            var angle = Vector3.Angle(yaw, delta.normalized);

            if (angle > _recenterAngle)
            {
                var targetXZ = camXZ + yaw * _preferredDistance;
                _activeTarget = new Vector3(targetXZ.x, _cameraTransform.position.y + _yOffset, targetXZ.z);
            }
            else if (xzDist < _minDistance || xzDist > _maxDistance)
            {
                var targetXZ = camXZ + delta.normalized * _preferredDistance;
                _activeTarget = new Vector3(targetXZ.x, _cameraTransform.position.y + _yOffset, targetXZ.z);
            }
        }

        if (_activeTarget.HasValue)
        {
            transform.position = Vector3.SmoothDamp(
                transform.position, _activeTarget.Value,
                ref _followVelocity, _smoothTime);

            if (Vector3.Distance(transform.position, _activeTarget.Value) < 0.015f)
            {
                transform.position = _activeTarget.Value;
                _activeTarget      = null;
                _followVelocity    = Vector3.zero;
            }
        }
    }

    private Vector3 GetCameraYawForward()
    {
        var f = _cameraTransform.forward;
        f.y = 0f;
        return f.sqrMagnitude > 0.001f ? f.normalized : Vector3.forward;
    }

    private void FaceCameraBelow()
    {
        var target = _cameraTransform.position + Vector3.down * _faceBelowOffset;
        var dir    = transform.position - target;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir);
    }

    public void ResetPosition()
    {
        _initialized = false;
        _lockMode    = LockMode.Follow;
        _activeTarget   = null;
        _followVelocity = Vector3.zero;
        ApplyLockVisual();
    }

    public void SetDragging(bool active)
    {
        _isDragging = active;
        if (!active)
        {
            _activeTarget   = null;
            _followVelocity = Vector3.zero;
        }
    }

    // Absolute world-space move used by the grip grab (position only).
    public void MoveTo(Vector3 worldPosition)
    {
        if (_isDragging)
            transform.position = worldPosition;
    }

    public void CycleLockMode()
    {
        _lockMode = (LockMode)(((int)_lockMode + 1) % 3);
        if (_lockMode == LockMode.Follow)
        {
            _activeTarget   = null;   // resume follow from current position cleanly
            _followVelocity = Vector3.zero;
        }
        ApplyLockVisual();
    }

    private void ApplyLockVisual()
    {
        if (_lockButtonImage == null) return;
        _lockButtonImage.color = _lockMode switch
        {
            LockMode.Follow       => ColorFollow,
            LockMode.LockPosition => ColorLockPosition,
            _                     => ColorLockPosRot,
        };
    }

    private void DetachToWorld()
    {
        // The panel ships parented under the persistent XR Rig. Locking only the follow script
        // cannot stop the rig's transform from carrying the panel when the player locomotes, so
        // detach to a top-level persistent object. Smart-follow is script-driven against the
        // world-space camera, so it keeps working with no parent; both lock modes then hold
        // world position. Start runs on the panel's first activation (it ships inactive), which
        // is after UserPanelOpener.Awake has cached its reference and after RootLifetimeScope
        // registered the instance — so detaching here breaks no existing reference holder.
        transform.SetParent(null, worldPositionStays: true);
        DontDestroyOnLoad(gameObject);
    }

    private void OnMainMenu() => _orchestrator?.TransitionTo(AppMode.MainMenu);
    private void OnExit()     => Application.Quit();
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run via MCP `run_tests` (mode `EditMode`, filter `UserPanelLockModeTests`).
Expected: PASS (2/2). Then `read_console` — expect 0 compile errors.

- [ ] **Step 5: Save & verify compilation**

After Unity recompiles, `read_console` (filter Error). Expected: 0 errors, 0 new warnings. (No git — user commits.)

---

## Task 2: Verify detach-from-rig behavior

`DetachToWorld()` was written into `UserPanel.cs` in Task 1. This task verifies it behaves correctly at runtime (it cannot be unit-tested without PlayMode + scene plumbing, so it is verified in PlayMode/headset).

**Files:**
- Verify: `Assets/_App/Scripts/SpatialUi/Panels/UserPanel.cs` (no new edits unless a defect is found)

- [ ] **Step 1: Confirm reference holders are unaffected (static read)**

Read these and confirm none locate the panel by hierarchy path after startup:
- `Assets/_App/Scripts/SpatialUi/Behaviors/UserPanelOpener.cs` — caches `_panel` via `GetComponentInChildren<UserPanel>(true)` in `Awake` (before the panel's first activation → before `Start`/detach). Holds the reference afterward. ✓
- `Assets/_App/Scripts/Bootstrap/RootLifetimeScope.cs` — registers the instance via `FindAnyObjectByType<UserPanel>(FindObjectsInactive.Include)` at bootstrap and retains it. ✓

Expected: both hold a cached reference; neither re-resolves by path. If either re-resolves the panel by transform path post-start, note it as a defect and stop.

- [ ] **Step 2: PlayMode verification (in-headset or editor PlayMode)**

Enter PlayMode. Open the panel (X/A). In the Hierarchy, confirm the `UserPanel` GameObject has moved to the `DontDestroyOnLoad` scene and its parent is now the scene root (no longer under `User XR Origin (XR Rig)`).
Expected: panel is a root object under `DontDestroyOnLoad`; panel still renders and follows.

- [ ] **Step 3: Locomotion hold check**

In Follow mode, walk/teleport — panel follows (unchanged). This confirms script-follow still works without the parent.
Expected: panel smoothly follows the camera as before.

- [ ] **Step 4: `read_console`**

Expected: no `MissingReference`/`NullReference` from the panel after detach during a scene transition (MainMenu ↔ VrEditing ↔ Sandbox). If a scene swap destroys or orphans the panel, note as defect.

---

## Task 3: PanelGrabHandle grip-grab component

New interactable on the handle bar that grabs the panel with the grip and moves it (position only) following the controller's attach transform in world space.

**Files:**
- Create: `Assets/_App/Scripts/SpatialUi/Behaviors/PanelGrabHandle.cs`
- Test: `Assets/_App/Tests/SpatialUi/PanelGrabHandleTests.cs`

- [ ] **Step 1: Write the failing EditMode test**

Create `Assets/_App/Tests/SpatialUi/PanelGrabHandleTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

public class PanelGrabHandleTests
{
    [Test]
    public void GrabOffset_RoundTrips_WhenAttachUnmoved()
    {
        var attach = new GameObject("attach").transform;
        attach.position = new Vector3(1f, 2f, 3f);
        attach.rotation = Quaternion.Euler(10f, 45f, 0f);
        var panelWorld = new Vector3(2f, 1f, 5f);

        var offset = PanelGrabHandle.CaptureOffset(attach, panelWorld);
        var result = PanelGrabHandle.ApplyOffset(attach, offset);

        Assert.That(Vector3.Distance(result, panelWorld), Is.LessThan(1e-4f));
        Object.DestroyImmediate(attach.gameObject);
    }

    [Test]
    public void GrabOffset_FollowsAttach_WhenAttachMoves()
    {
        var attach = new GameObject("attach").transform;
        attach.position = Vector3.zero;
        attach.rotation = Quaternion.identity;
        var panelWorld = new Vector3(0f, 0f, 1f);

        var offset = PanelGrabHandle.CaptureOffset(attach, panelWorld);
        attach.position = new Vector3(5f, 0f, 0f);     // controller moved +5 on X
        var result = PanelGrabHandle.ApplyOffset(attach, offset);

        Assert.That(Vector3.Distance(result, new Vector3(5f, 0f, 1f)), Is.LessThan(1e-4f));
        Object.DestroyImmediate(attach.gameObject);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run via MCP `run_tests` (mode `EditMode`, filter `PanelGrabHandleTests`).
Expected: FAIL — compile error, `PanelGrabHandle` does not exist.

- [ ] **Step 3: Implement `PanelGrabHandle.cs`**

Create `Assets/_App/Scripts/SpatialUi/Behaviors/PanelGrabHandle.cs`:

```csharp
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// Grip-based grab for the UserPanel. Hover the handle bar with a NearFarInteractor, hold grip
// (selectInput in this project's mapping) to move the panel in world space (position only),
// release to drop. Mirrors XRPromeonInteractable's direct-input model; XRI standard select-flow
// stays disabled (IsSelectableBy => false) so we read input ourselves and never fight the gizmo.
public class PanelGrabHandle : XRBaseInteractable
{
    [SerializeField] private UserPanel _panel;

    private enum State { Idle, Grabbing }
    private State              _state;
    private NearFarInteractor  _locked;
    private NearFarInteractor  _lastHovering;
    private Vector3            _grabOffset;   // panel world position expressed in attach-local space

    // Pure helpers — unit-testable grab-offset math.
    public static Vector3 CaptureOffset(Transform attach, Vector3 worldPos)
        => attach.InverseTransformPoint(worldPos);
    public static Vector3 ApplyOffset(Transform attach, Vector3 localOffset)
        => attach.TransformPoint(localOffset);

    protected override void Awake()
    {
        base.Awake();
        // base.Awake auto-discovers GetComponentsInChildren<Collider>(true). Take ownership:
        // use only colliders on this GameObject (the handle bar's own BoxCollider).
        colliders.Clear();
        foreach (var c in GetComponents<Collider>())
            if (c != null && !colliders.Contains(c))
                colliders.Add(c);
    }

    // Read inputs directly; hover stays enabled (IsHoverableBy default true).
    public override bool IsSelectableBy(IXRSelectInteractor interactor) => false;

    public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase phase)
    {
        base.ProcessInteractable(phase);
        if (phase != XRInteractionUpdateOrder.UpdatePhase.Dynamic) return;
        if (_panel == null) return;

        // Defensive: dangling lock from a destroyed/disabled interactor.
        if (_state == State.Grabbing && (_locked == null || !_locked.isActiveAndEnabled))
        { EndGrab(); return; }

        switch (_state)
        {
            case State.Idle:
                UpdateLastHovering();
                var ni = CurrentHoverer();
                if (ni == null) break;
                if (!IsPrimaryFor(ni)) break;            // only the closest ray hit processes input
                if (ni.selectInput.ReadWasPerformedThisFrame())
                {
                    _locked     = ni;
                    var attach  = _locked.GetAttachTransform(this);
                    _grabOffset = CaptureOffset(attach, _panel.transform.position);
                    _panel.SetDragging(true);
                    _state = State.Grabbing;
                }
                break;

            case State.Grabbing:
                if (_locked.selectInput.ReadWasCompletedThisFrame())
                { EndGrab(); break; }
                var a = _locked.GetAttachTransform(this);
                _panel.MoveTo(ApplyOffset(a, _grabOffset));
                break;
        }
    }

    private void EndGrab()
    {
        if (_panel != null) _panel.SetDragging(false);
        _locked = null;
        _state  = State.Idle;
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

    private bool IsPrimaryFor(NearFarInteractor ni)
    {
        var ray = ni.GetComponentInChildren<XRRayInteractor>(includeInactive: true);
        if (ray != null)
        {
            if (ray.TryGetCurrent3DRaycastHit(out var hit) && hit.collider != null)
                return colliders.Contains(hit.collider);
            return false; // ray exists but hits nothing — not primary
        }

        // True near path (physical hand, no ray).
        if (ni.interactablesHovered.Count > 0)
            return ReferenceEquals(ni.interactablesHovered[0], this);

        return false;
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run via MCP `run_tests` (mode `EditMode`, filter `PanelGrabHandleTests`).
Expected: PASS (2/2).

- [ ] **Step 5: Save & verify compilation**

`read_console` (filter Error). Expected: 0 errors, 0 new warnings.

---

## Task 4: Wire the prefab (MCP)

Add the collider + grab component to the handle bar, wire the panel reference, and remove the old uGUI drag component. All edits target `Assets/_App/Content/Prefabs/UI/Panels/UserPanel/UserPanel.prefab`. Use MCP prefab/component tools; verify each change (MCP `manage_*` can return false-but-succeed — re-read the prefab to confirm).

**Files:**
- Modify: `Assets/_App/Content/Prefabs/UI/Panels/UserPanel/UserPanel.prefab`

Target IDs (verified in the current prefab):
- UserPanel root GameObject: `276269292298337097`
- UserPanel component (grab target ref): `8573857923733603883`
- `DragHandle` GameObject: `6664021387027771371`
- `PanelDragHandle` component to remove: `385984591946434082`

- [ ] **Step 1: Add a BoxCollider to the `DragHandle` GameObject**

On `DragHandle` (`6664021387027771371`) add a `BoxCollider`. The handle RectTransform is 180×18 and the panel canvas lossyScale is 0.001, so set the collider in local units:
- `size = (180, 18, 20)`  → world ≈ 0.18 × 0.018 × 0.02 m
- `center = (0, 0, 0)`
- `isTrigger = true` (UI element, no physics response; ray raycast still hits triggers — the panel's `TrackedDeviceGraphicRaycaster` uses `m_RaycastTriggerInteraction: 1`/Collide and the ray interactor's 3D raycast hits triggers by default).

Re-read the prefab and confirm the BoxCollider exists on `6664021387027771371`.

- [ ] **Step 2: Add the `PanelGrabHandle` component to `DragHandle`**

Add component `PanelGrabHandle` to `DragHandle` (`6664021387027771371`).
Expected: an `XRBaseInteractable`-derived component is added; Unity also requires it has access to the collider added in Step 1 (same GameObject). Re-read to confirm the component is present.

- [ ] **Step 3: Wire `PanelGrabHandle._panel` → the UserPanel component**

Set the new component's `_panel` field to reference the `UserPanel` MonoBehaviour at `{fileID: 8573857923733603883}` (same object the old `PanelDragHandle._panel` referenced).
Re-read and confirm `_panel: {fileID: 8573857923733603883}`.

- [ ] **Step 4: Remove the `PanelDragHandle` component**

Remove the `PanelDragHandle` MonoBehaviour (`385984591946434082`) from `DragHandle`. Leave the `Image` (`7270415353658703359`) — it stays as the visible handle bar.
Re-read and confirm `PanelDragHandle` is gone and the `Image` remains.

- [ ] **Step 5: Confirm lock-button refs are intact**

Re-read the UserPanel component (`8573857923733603883`) and confirm `_lockButton` and `_lockButtonImage` are still wired (unchanged by this task). The 3 lock colors are code constants (`static readonly`), so there are no serialized color fields to set.
Expected: `_lockButton` and `_lockButtonImage` non-zero fileIDs.

- [ ] **Step 6: `read_console` + recompile check**

`refresh_unity`, then `read_console` (filter Error/Warning). Expected: 0 errors; no "missing script" or "collider already registered" warnings on the panel.

---

## Task 5: In-headset acceptance pass

Validate the full feature against the spec's acceptance criteria. No code changes unless a defect is found (loop back to the owning task).

**Files:** none (verification only)

- [ ] **Step 1: Grab stability** — point at the handle bar, hold grip: the panel follows the controller 1:1 in world space, no jitter, no fly-away; release drops it cleanly; repeated grabs always engage. (If the ray never grabs, check the BoxCollider layer is in the ray interactor's raycast mask — adjust the `DragHandle` layer if needed.)

- [ ] **Step 2: Follow mode** — default after open: panel smart-follows and faces the user (unchanged).

- [ ] **Step 3: Lock position (1st click — amber)** — walk/teleport away: panel stays put in world but rotates to keep facing the user.

- [ ] **Step 4: Lock position+rotation (2nd click — red)** — panel stays fully fixed (position and orientation) while moving around it.

- [ ] **Step 5: Cycle wrap (3rd click — green)** — returns to Follow; button color tracks green → amber → red → green.

- [ ] **Step 6: Grab × mode** — grab works in all three modes; releasing in Follow returns the panel to the user; releasing in either lock mode holds the new placement.

- [ ] **Step 7: Reopen reset** — close and reopen (X/A): panel reopens in Follow.

- [ ] **Step 8: Scene transitions** — toggle MainMenu ↔ VrEditing ↔ Sandbox with the panel detached: panel persists, no console errors.

---

## Self-Review Notes

- **Spec coverage:** Grab→grip (Task 3+4), position-only (Task 3 `MoveTo`/offset), triple lock (Task 1), 3-color indicator (Task 1 `ApplyLockVisual`), freeze rotation in `LockPositionRotation` (Task 1 `LateUpdate` gate), detach from rig (Task 1 `DetachToWorld` + Task 2 verify), remove `PanelDragHandle` from prefab (Task 4), grab×mode behavior (Task 5 §6). All covered.
- **Type consistency:** `LockMode { Follow, LockPosition, LockPositionRotation }`, `CurrentLockMode`, `CycleLockMode()`, `ResetPosition()`, `SetDragging(bool)`, `MoveTo(Vector3)`, `PanelGrabHandle.CaptureOffset/ApplyOffset` — names match across tasks and tests.
- **Known integration risk (flagged, not a blocker):** the handle BoxCollider must sit on a layer included in the NearFarInteractor ray's raycast mask, and mixed uGUI-graphic + 3D-collider raycasting on the same canvas must resolve the handle as the 3D hit. Verified in Task 5 §1; layer adjustment is the fallback.
