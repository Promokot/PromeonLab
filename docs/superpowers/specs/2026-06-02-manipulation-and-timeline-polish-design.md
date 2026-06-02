# Manipulation & Timeline Polish — Design

**Date:** 2026-06-02
**Status:** Approved-for-planning
**Subsystems:** `VrInteraction` (gizmo), `Animation` + `SpatialUi` (timeline), `SpatialUi` (inspector)

This batch fixes three low-severity bugs and reworks gizmo scale/rotate input for a uniform, predictable
VR feel. Four independent items, one spec.

---

## Item 1 — Bug: timeline playhead frozen during loop playback

### Symptom
While a **looping** object's animation plays, the timeline playhead does not advance. (Non-looping
playback already moves the playhead; scrubbing while paused already works via `TimelineScrubInput`.)

### Root cause
A looping owner plays through the background path: `AnimationAuthoring.Tick` advances
`_loopCursors[owner]` and samples the clip directly (`SampleContainerAt`). It never publishes
`FrameChangedEvent`. `AnimatorPanel` moves the playhead **only** on `FrameChangedEvent`
(`OnFrameChanged → _playhead.SetFrame`). The transport clock (`AnimationClock`) — which does publish
`FrameChangedEvent` — is not driving a looping owner (`OnPlayPauseClicked` calls
`StartLoopPlayback` instead of `Clock.Play` for looping owners). So nothing tells the playhead to move.

### Fix
Add a dedicated event and have the loop publish it:

- **New event** `Animation/Events/LoopFrameChangedEvent.cs`:
  ```csharp
  public struct LoopFrameChangedEvent { public string OwnerNodeId; public int Frame; }
  ```
- `AnimationAuthoring.Tick`: for each advancing loop owner, when `Mathf.FloorToInt(cursor)` changes
  since the last published value for that owner, publish
  `LoopFrameChangedEvent { OwnerNodeId = owner, Frame = floorFrame }`. Track the last published integer
  frame per owner (e.g. a `Dictionary<string,int> _loopLastFrame`) so we publish once per frame step,
  not every tick. Clear the entry in `StopLoopPlayback`.
- `AnimatorPanel`: subscribe to `LoopFrameChangedEvent`; in the handler, **only if
  `e.OwnerNodeId == _activeOwner`**, call `_playhead.SetFrame(e.Frame)` and `_toolbar?.SetCurrentFrame`.
  This keeps the playhead bound to the *selected* object even when several loops run concurrently.

Why a new event rather than reusing `FrameChangedEvent`: `FrameChangedEvent` drives clip sampling and
is tied to the transport clock's `CurrentFrame`; reusing it would re-enter sampling and desync the
clock. `LoopFrameChangedEvent` is display-only.

### Testing
- Unit (`AnimationAuthoring`): with a looping container started, simulate ticks (drive `AdvanceLoopCursor`
  via the existing testable seam) and assert a `LoopFrameChangedEvent` is published with the expected
  integer frame, and **not** re-published while the integer frame is unchanged. (Bus capture in EditMode.)
- In-headset: play a looping object → playhead advances and wraps to 0 each loop; pause → playhead
  stops and is draggable.

---

## Item 2 — Bug: stale blue rig outline after re-entering a scene with bones enabled

### Symptom
Leaving a scene while a rig's **Show Bones** is ON, then re-entering, leaves the rig drawn with the blue
**Selected** outline even though the user did not select it. Cosmetic (no functional impact).

### Root cause
The blue is `SelectionVisual.Selected` (priority 0), applied by `Selectable.SetVisualState(Selected)` →
`_outline.enabled = true`. `Selectable.EnsureOutline` **reuses an existing `Outline`** if present (bone
proxies/rig parts can already carry one — `Outline` is `[DisallowMultipleComponent]`). A freshly spawned
`Selectable` never calls `SetVisualState(None)` until the first `SelectionChanged`, so a pre-existing
`Outline` that is **enabled** (left over from bone display) renders immediately on scene re-entry, before
any selection. (The earlier theory that the `Show Bones` toggle re-fires `Select(rig)` on rebind is
**ruled out**: `InspectorPanel.Refresh` sets the toggle via `SetIsOnWithoutNotify`, which raises no event.)

### Fix
Make a freshly spawned `Selectable` start with no outline: in `Selectable.Start()`, disable any
**pre-existing** `Outline` component (do **not** add one — adding an `Outline` to every node at spawn is
expensive due to smooth-normal baking). A real `SelectionChanged` still drives the outline on/off as
before.

```csharp
private void Start()
{
    // A pre-existing Outline (e.g. left enabled from bone display, or baked on a proxy) must not show
    // until this node is actually selected. Disable it without adding one to non-outlined nodes.
    var existing = GetComponent<Outline>();
    if (existing != null) existing.enabled = false;
}
```

If in-headset this proves insufficient (the stale outline comes from a different component/path), treat
it as a follow-up diagnosis — it is explicitly low-impact.

### Testing
- In-headset: enable bones on a rig → leave scene (to Main Menu / other mode) → re-enter → the rig has
  no blue outline and nothing is selected. (No unit test — this is a UI lifecycle/event-ordering bug;
  verified manually.)

---

## Item 3 — Feature: uniform displacement-driven gizmo scale & rotate

### Problem
Both the scale handle and the rotation rings derive their magnitude from the hand position **relative to
the object pivot**:
- `AxisScaleStrategy`: `factor = distNow / distAtGrab` (projection onto the axis). Near the pivot
  `distAtGrab` is tiny → hypersensitive; crossing the pivot flips the sign → clamps to a min and jumps.
  Shrinking is nearly impossible; the rate is non-uniform.
- `RingRotateStrategy`: `angle = SignedAngle(grabDir, nowDir)` of normalized pivot-relative vectors on
  the ring plane. Near the center those vectors are tiny → normalization is unstable → small hand moves
  cause large jumps ("резкое прокручивание").

### Principle (locked during brainstorming)
Replace pivot-relative geometry with a **self-establishing 1D slider** driven by controller
displacement at a **constant gain** — uniform rate, no degeneracy at the center. **The pivot and the
rotation/scale axis are unchanged**; only the *magnitude* source changes.

Shared drag mechanism (used by both the scale strategy and the ring strategy):
1. **On grab:** store the controller (virtual-hand) start position `start`. `refDir` is unset.
2. **Deadzone:** until the controller moves more than `DeadzoneMeters` (default **0.02 m**) from `start`,
   apply no change. The first displacement past the deadzone **locks** `refDir = (pos − start).normalized`.
3. **Per frame after lock:** `s = Vector3.Dot(pos − start, refDir) − DeadzoneMeters` — a signed scalar
   in metres, **baselined so `s = 0` at the moment of lock** (no pop when `refDir` engages). Pushing
   further along `refDir` makes `s` grow; pulling back toward and past `start` makes `s` go negative
   (shrink / reverse). The object's size/rotation is preserved exactly at the lock point, not at `start`.
4. **Re-grab** (release + grab again) resets `start` and `refDir`.

`pos` is the existing virtual-hand position the gizmo already feeds the strategy
(`controllerPos + forward * grabRayDistance`, see `GizmoHandle`).

### Scale (`AxisScaleStrategy`, and the uniform/center handle)
```
factor              = Mathf.Exp(ScaleGain * s)        // multiplicative; s in metres
localScale[axisIdx] = originalScale[axisIdx] * factor // per-axis handle
// uniform/center handle: apply factor to all three components
```
Exponential so growing and shrinking are symmetric (`+s` doubles where `−s` halves at the same rate) and
scale never reaches 0 or goes negative. `ScaleGain` is tuned so a comfortable swipe gives a useful range
(e.g. ≈ `ln(2)/0.15` → ×2 per 15 cm; final value tuned in-editor).

### Ring rotate (`RingRotateStrategy`)
```
angle           = RotGain * s                          // degrees; s in metres
target.rotation = Quaternion.AngleAxis(angle, axisWorld) * originalRot   // axisWorld through the pivot — UNCHANGED
```
Linear in displacement → uniform °/cm, unlimited travel (keep swiping to keep rotating), no wrist limit.
`RotGain` default e.g. ≈ `360/0.30` (a 30 cm swipe = full turn; tuned in-editor).

### Config
Add `DeadzoneMeters`, `ScaleGain`, `RotGain` to `GizmoConfig` (`[SerializeField] private`, inspector-
tunable). The strategies read them at `BeginDrag` (passed in by `GizmoActivator`, which already owns the
config) — keep the strategies free of singletons.

### What does NOT change
- The gizmo handles/rings prefab and which axis each handle represents.
- `UniformScaleStrategy`’s role (center handle = all axes) — only its magnitude source changes to the
  shared slider.
- The pivot and axis of every transform.
- **Direct hold-trigger rotate** (`XRPromeonInteractable` `TriggerRotate` → `ApplyRotate`): already a
  relative 1:1 orientation-delta follow (`objRot = ctrlRotNow · ctrlRot0⁻¹ · objRot0`). **No code
  change**; verify in-headset. (If it still feels off, a gain control is a follow-up, not part of this
  spec.)
- **Direct grip-move** — out of scope.

### Testing
- Unit (`AxisScaleStrategy`): drive `BeginDrag` then `UpdateDrag` with synthetic virtual-hand positions;
  assert (a) no change inside the deadzone, (b) after a >2 cm move that locks `refDir`, pushing further
  along it multiplies scale by `exp(gain·s)`, (c) pulling back past the start **shrinks** below the
  original (proving shrink works and is symmetric), (d) the non-dragged axes are untouched.
- Unit (`RingRotateStrategy`): same drag pattern; assert `angle ≈ RotGain·s` and the rotation is
  `AngleAxis(angle, axis)·originalRot` (pivot/axis preserved), and the deadzone yields no rotation.
- These mirror the existing `AxisScaleStrategyTests` / `RingRotateStrategyTests` patterns (pure strategy
  classes, no Unity scene). Update those existing tests to the new behavior.
- In-headset: scale a small object down easily; rotate a ring with uniform speed and no center jump.

---

## Item 4 — Bug: "Add animation" in bone mode creates empty tracks (no owner track)

### Symptom
Pressing **Add animation** while in a rig's bone-edit mode creates the container but with **no tracks**
(empty timeline). Outside bone mode, Add always creates the object's own **owner track**.

### Root cause
`AnimatorPanel.OnAddAnimationClicked` gates owner-track creation behind `if (!isBone && owner == selected)`.
In bone mode the selected node is a bone (`selected = "bone:{rig}:{bone}"`, `isBone == true`,
`owner = OwnerOf(selected) = rig`), so `owner != selected` **and** `isBone` is true → the
`EnsureTrack(owner, owner)` call is skipped → the container has no owner track. (If *nothing* is selected
in bone mode, `selected` is null → `owner` is null → the method returns early and nothing is created.)

### Fix
The owner track (the object/rig's own transform track) should always exist for the container's owner,
independent of whether the current selection is the object or one of its bones. Rewrite the handler:

```csharp
private void OnAddAnimationClicked()
{
    if (_ctx.Authoring == null) return;
    var selected = _ctx.Selection?.SelectedNodeId;
    var owner    = AnimationAuthoring.OwnerOf(selected);
    if (string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(_boneModeRig))
        owner = _boneModeRig;                         // bone mode, nothing selected → target the rig
    if (string.IsNullOrEmpty(owner)) return;

    _ctx.Authoring.CreateContainer(owner, _config.DefaultTotalFrames, _config.DefaultFps);
    _ctx.Authoring.EnsureTrack(owner, owner);         // owner track ALWAYS — object/rig's own transform
}
```

Behavior after the fix:
- Outside bone mode (object selected): unchanged — owner == selected, owner track created.
- Bone mode with a bone selected: owner = rig → rig's owner track created (the reported fix).
- Bone mode with nothing selected: owner falls back to `_boneModeRig` → rig container + owner track
  created (previously a no-op).

### Testing
- Unit (`AnimationAuthoring` / panel-config seam where feasible): after `CreateContainer(owner)` +
  `EnsureTrack(owner, owner)`, the container for `owner` contains a track whose `NodeId == owner`.
  (The handler itself depends on `SceneContext`/UI; the testable core is that `EnsureTrack(owner, owner)`
  yields an owner track — assert at the `AnimationAuthoring` level.)
- In-headset: enter bone mode on a rig → Add animation → an owner row for the rig appears (not empty).

---

## Out of scope
- Direct hold-trigger rotate math, direct grip-move feel, two-handed manipulation.
- Snapping / increments, numeric entry, gizmo visual restyle.
- Per-axis independent gains (single `ScaleGain`/`RotGain` for now).

## Notes
- Git is not touched by the agent; the user commits manually.
- Allowed pre-existing test failures stay allowed: `PathProviderTests` ×4, `RingRotateStrategyTests` ×2
  — **note:** the `RingRotateStrategyTests` are being rewritten for the new behavior in Item 3, which
  should turn those 2 green; re-baseline the allowed-failures list to `PathProviderTests` ×4 afterward.
