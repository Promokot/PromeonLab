# UserPanel — Grab & Triple-Lock Design

**Date:** 2026-05-30
**Status:** Approved (design)
**Scope:** Main `UserPanel` only. Other panels (`SpatialPanelDetachable` / `DetachablePanelDragHandle`) are out of scope.

## Problem

Two defects in the main `UserPanel` reported from in-headset testing:

1. **Grab handle is unstable.** Dragging is jerky, the panel sometimes flies away, and the handle often refuses to grab. Should be rebound to **grip** for a natural VR grab.
2. **Lock does not fix the panel.** Toggling the lock does not keep the panel in place — walking or teleporting drags it along, and it keeps rotating to face the user even while "locked". The lock should also be a **triple-mode** cycle, not a binary toggle.

## Root Cause Analysis

### Grab (`PanelDragHandle.cs`)
The handle drives movement through uGUI pointer events (`IBeginDragHandler` / `IDragHandler`). `OnDrag` projects the **screen-space pointer delta** onto a world plane at the panel's depth (`Camera.ScreenToWorldPoint` at `WorldToScreenPoint(panel).z`). Driven by a VR ray this creates a feedback loop (the panel moves, which moves the projected hit, which moves the panel), produces large per-frame deltas, and the `MaxFrameDelta = 0.4` guard discards frames non-deterministically. Net effect: jitter, "fly away", and dropped grabs.

### Lock (`UserPanel.cs`)
The panel is a child of the **persistent XR Rig** (instantiated inside `User XR Origin (XR Rig).prefab`; `RootLifetimeScope` locates it via `FindAnyObjectByType<UserPanel>` and registers the instance). The current `_locked` flag only **skips `UpdateSmartFollow()`** in `LateUpdate`. Two consequences:

- Because the panel's transform is parented to the moving rig, locking the follow script does **not** stop the rig's transform from carrying the panel when the player locomotes/teleports.
- `FaceCameraBelow()` runs **every frame unconditionally** (it is outside the `_locked` guard), so the panel keeps re-orienting to face the user even when "locked".

## Design

### Component model

```
UserPanel (root, world-space Canvas)        ← reparented off the rig at Start
├── ... content ...
└── GrabHandle (bar)
    ├── Image            (hover/drag tint, kept)
    ├── BoxCollider      (ray-hittable, sized to the bar)   ← NEW
    └── PanelGrabHandle  (XRBaseInteractable)               ← NEW, replaces PanelDragHandle
```

### A. Grip-based grab — `PanelGrabHandle`

New component `PanelGrabHandle : XRBaseInteractable`, modeled on the project's existing `XRPromeonInteractable` grip-move path so the panel grab matches the scene-object input model (grip = `selectInput` in this project's `NearFarInteractor` config; XRI standard select-flow stays disabled — we read input directly).

Behavior:
- `IsSelectableBy(IXRSelectInteractor) => false` — read inputs directly; hover stays enabled.
- A serialized reference to the **target panel transform** (`UserPanel`), not the handle itself.
- In `ProcessInteractable(UpdatePhase.Dynamic)`:
  - **Idle:** if hovered by a `NearFarInteractor` that is the primary hit for this collider, and its `selectInput` (grip) `ReadWasPerformedThisFrame()` → lock the interactor, capture the attach offset of the **panel root** (`attach.InverseTransformPoint(panel.position)`), enter `Grabbing`, and call `panel.SetDragging(true)`.
  - **Grabbing:** each frame set `panel.position = attach.TransformPoint(offset)` — **position only**, no rotation change (the panel's rotation continues to be driven by its current lock mode). If `selectInput.ReadWasCompletedThisFrame()` (grip released) → end: `panel.SetDragging(false)`, return to Idle.
  - Defensive: if the locked interactor becomes null/disabled mid-grab, end the grab.
- `colliders` ownership follows `XRPromeonInteractable`'s pattern (clear `base.Awake()` auto-discovery, use only own colliders) to avoid duplicate-registration warnings.
- Hover/drag tint on the `Image` is preserved (driven from the new component's hover/grab transitions).

`PanelDragHandle` is **removed from the `UserPanel.prefab`**. The `PanelDragHandle.cs` file is left in the repo (unused by UserPanel, not referenced elsewhere) per the project's "comment/keep, don't delete" convention.

### B. Triple-lock mode — `UserPanel`

Replace the `bool _locked` with a three-state cycle. The lock button advances the mode on each click, wrapping around:

```
Follow → LockPosition → LockPositionRotation → Follow
```

```csharp
private enum LockMode { Follow, LockPosition, LockPositionRotation }
```

`LateUpdate` behavior per mode (when not actively grabbing):

| Mode | `UpdateSmartFollow()` | `FaceCameraBelow()` | Effect |
|---|---|---|---|
| `Follow` | run | run | Smart-follows the user, faces them (current default) |
| `LockPosition` | skip | **run** | Frozen position in world; still rotates to face the user |
| `LockPositionRotation` | skip | **skip** | Fully frozen — position and rotation held |

Note the fix: `FaceCameraBelow()` moves **inside** the mode gate (currently it always runs).

Grab interaction with modes: grab works in all three modes; while grabbing, follow is suspended via `SetDragging(true)`. On release, `Follow` reclaims position (smart-follow returns the panel to the user), while both lock modes hold the new position. The mode itself changes **only** via the lock button — grabbing never changes the mode.

`ResetPosition()` (called by `UserPanelOpener` on open) resets the mode to `Follow`.

### Lock-button indication — 3 colors

`_lockButtonImage.color` reflects the mode (extend the existing 2-color scheme):

| Mode | Color |
|---|---|
| `Follow` | green — `ColorFollow` (existing `ColorUnlocked` ≈ `0.62, 1.00, 0.77`) |
| `LockPosition` | amber — `ColorLockPosition` (new, ≈ `1.00, 0.78, 0.35`) |
| `LockPositionRotation` | red — `ColorLockPosRot` (existing `ColorLocked` ≈ `1.00, 0.42, 0.42`) |

### C. Detach from the XR Rig

In `UserPanel.Start()` (after all `Awake()` calls have run, so `UserPanelOpener` has already cached its `_panel` reference and `RootLifetimeScope` has already registered the instance), reparent the panel out of the rig onto a persistent holder:

- `transform.SetParent(null, worldPositionStays: true)` then `DontDestroyOnLoad(gameObject)` (the panel must survive `LoadSceneMode.Single` scene swaps, like the rig it came from).
- Smart-follow is script-driven against the world-space camera transform, so it continues to work with no parent. With the panel no longer parented to the rig, both lock modes keep the panel fixed in world space while the player locomotes.

Affected reference holders, verified safe:
- `UserPanelOpener` caches `_panel` in `Awake` (before `Start`) and holds the reference — reparent does not break it.
- `RootLifetimeScope` registers the instance at bootstrap via `FindAnyObjectByType` and retains the reference.
- `PanelRegionRouter` / nav buttons reference the panel by injected instance, not by hierarchy path.

## Files

| File | Change |
|---|---|
| `Assets/_App/Scripts/SpatialUi/Behaviors/PanelGrabHandle.cs` | **New** — `XRBaseInteractable` grip-grab, position-only follow |
| `Assets/_App/Scripts/SpatialUi/Panels/UserPanel.cs` | **Modify** — `LockMode` enum + cycle, gate `FaceCameraBelow` behind mode, 3-color indication, reparent in `Start` |
| `Assets/_App/Content/Prefabs/UI/Panels/UserPanel/UserPanel.prefab` | **Modify** — add BoxCollider + `PanelGrabHandle` to the grab bar, wire panel ref, remove `PanelDragHandle`; assign 3 lock colors |
| `Assets/_App/Scripts/SpatialUi/Behaviors/PanelDragHandle.cs` | Left in repo, unused (per keep-don't-delete convention) |

## Out of Scope

- `SpatialPanelDetachable` / `DetachablePanelDragHandle` (other detachable panels) — same grab approach could apply later; not part of this change.
- Lock-button icon/sprite swap (color-only indication chosen).
- Snap-back animation polish beyond the existing `SmoothDamp` follow.

## Testing (in-headset acceptance)

1. **Grab stability:** point at the grab bar, hold grip — panel follows the controller 1:1 in world space with no jitter or fly-away; release drops it cleanly. Repeated grabs always engage.
2. **Follow mode:** panel smart-follows and faces the user (unchanged default).
3. **Lock position:** lock once → walk/teleport away; panel stays put in world but rotates to keep facing the user.
4. **Lock position+rotation:** lock again → panel stays fully fixed (position and orientation) as the user moves around it.
5. **Cycle wrap:** lock a third time → returns to Follow; button color tracks green → amber → red → green.
6. **Grab × mode:** grabbing works in all three modes; releasing in Follow returns the panel to the user, releasing in either lock mode holds the placement.
7. **Reopen:** closing and reopening the panel (X/A) resets to Follow.
