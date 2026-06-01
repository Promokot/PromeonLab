# Animator — Playback Modes, Live Refresh, Scene FPS & Config-Driven Metrics (design)

**Date:** 2026-06-01
**Status:** Approved (pending user spec review)
**Scope:** Five cohesive improvements to the animator, all inside the `Animation` subsystem and the
`AnimatorPanel` UI. Single-object + rig (per-bone tracks) both in scope for the live-refresh fix.

## Context

After the unified single-list timeline redesign, keyframes place correctly and the play icon swaps.
Remaining issues reported by the user:

1. Track rows (count **and** names) only refresh on panel reopen. For rigs a per-bone track appears
   only after the first keyframe **and** a reopen.
2. The toolbar input fields (current frame / total frames / fps) do not actually apply when edited.
3. (2.1) FPS is currently per-`ActionContainer`; it should be **scene-wide**.
4. Visual/metric constants are hardcoded instead of pulled from `AnimatorPanelConfig` ("config
   doesn't affect keyframe color etc.").
5. (4 / 4.1) No loop mode. Need a mode switch (play-once / loop) like the play/pause button, and in
   play-once mode the object must return to frame 0 posed as its first keyframe after playback ends.

### Current-state facts (verified in code)

- `AnimationClock` (`Scripts/Animation/AnimationClock.cs`) holds `CurrentFrame/TotalFrames/Fps/IsPlaying`.
  On `next >= TotalFrames` it clamps to `TotalFrames` and stops. No loop. `Configure(total, fps)`.
- FPS lives in **two** places: `ActionContainer.Fps` (persisted per container) and `AnimationClock.Fps`
  (runtime, set from the active container via `ApplyContainerToClock`).
- `AnimationAuthoring.SetKey` creates a track via `GetOrCreateTrack` but publishes only
  `AnimationKeyframeChangedEvent` (Added/Overwritten). The panel's `OnKeyframeChanged` →
  `RefreshRowKeys()` updates keys of **existing** rows only; it never adds a row for a newly created
  track. New rows appear only through `RebuildTimeline()`, which runs on `Refresh()` (reselect/reopen).
- Toolbar two-way wiring **exists** (`WireToolbar`: `OnTotalFramesSubmitted → SetTotalFrames`,
  `OnFpsSubmitted → SetFps`, `OnCurrentFrameSubmitted → Seek`). The break is at the VR commit path:
  `TMP_InputField.onEndEdit` does not fire from the VR keyboard flow, so the callbacks never run.
- All four `_config` slots in `AnimatorPanelModule.prefab` and the one in `TimelineRow.prefab`
  reference the **same** asset `DefaultAnimatorPanelConfig` (guid `4b710848b9de3b74b97536367c823ac8`).
  Colors **are** read in `TimelineRow.SetKeys`. Hardcoded (not config) values: key diamond size
  `22f`/`26f` (`TimelineRow`), tick heights `24f`/`16f` (`AnimatorSubRuler`), row height `52`
  (`TimelineRow.prefab` `LayoutElement`).
- `AnimationAuthoring.ApplyFrame(frame)` samples each track's legacy `AnimationClip` at
  `t = frame / fps`. Unity `AnimationCurve` clamps to the **first key's value** for any `t` before it,
  so sampling at frame 0 already yields the first-keyframe pose. Gated behind `_clock.IsPlaying`.

## Decisions (locked with the user)

1. **Play-once reset target:** after play-once playback ends, the playhead returns to **frame 0** and
   the object is posed **as at its first keyframe**. (Sampling at frame 0 achieves both, because the
   curve holds the first key's value before the first key.)
2. **Mode switch button:** a **new** button in the transport bar, with two sprite slots
   (`_onceSprite` / `_loopSprite`) the user fills later — exactly like the play/pause icon swap.
3. **Input fields don't currently apply — it is a bug.** Fix the VR commit path so edits apply.
4. **Two playback modes only:** `Once` and `Loop` (no ping-pong — YAGNI).
5. **Scene-wide FPS without a schema bump:** add `SceneAnimationData.Fps`; normalize on load when
   absent/0. Keep per-container `Fps` field for back-compat but ignore it for playback.
6. **Play mode is runtime-only** (not persisted). Persisting it is deferred.

## Design

### A. Live track refresh (#1)

**`AnimationAuthoring.SetKey(nodeId, frame, pos, rot, scale)`** — detect whether the track is new:
```csharp
var owner = OwnerOf(nodeId);
// ...
var existingTrack = c.FindTrack(nodeId);
bool trackIsNew   = existingTrack == null;
var track         = c.GetOrCreateTrack(nodeId);
bool existed      = track.HasKey(frame);
track.UpsertKey(frame, pos, rot, scale);

if (trackIsNew)
    _bus.Publish(new AnimationContainerChangedEvent
        { OwnerNodeId = owner, Change = ContainerChange.TracksChanged });

_bus.Publish(new AnimationKeyframeChangedEvent { /* unchanged */ });
```
The panel already maps `TracksChanged → RebuildTimeline()` (for `e.OwnerNodeId == _activeOwner`), so a
new object/bone track now produces its row immediately — no reopen. `PasteFrame` (which also calls
`GetOrCreateTrack`) gets the same new-track detection and `TracksChanged` publish.

**Names on rename:** `RebuildRows` reads `go.DisplayName` each rebuild. If a `NodeRenamedEvent` (or the
existing scene-modified signal) exists, the panel subscribes and calls `RebuildTimeline()`; otherwise
names continue to refresh on the next rebuild and this is noted as a minor deferred item. (The plan's
research step confirms which rename event exists before wiring.)

### B. Input fields apply in VR (#2)

Root cause to confirm in the plan's first step: `TMP_InputField.onEndEdit` is not raised by the VR
keyboard. Fix approach (chosen during implementation after confirming the keyboard API):
- Ensure the VR keyboard's "confirm/close" commits the field and raises `onEndEdit` (or have the
  toolbar additionally listen to `onSubmit`, or read the field value on keyboard-close).
- `AnimatorSubToolbar` keeps `OnCurrentFrameSubmitted/OnTotalFramesSubmitted/OnFpsSubmitted`; the panel
  wiring is unchanged. After applying, the panel echoes the clamped value back via
  `SetTotalFrames/SetFps/SetCurrentFrame` (`SetTextWithoutNotify`) so the field shows the real value.
- Verification is an EditMode test that drives the toolbar callback directly (proving the
  apply path), plus an in-headset check for the VR commit.

### C. Scene-wide FPS (#2.1)

- **`SceneAnimationData`**: add `public int Fps = 24;` (schemaVersion stays **2**).
- **Load normalize** (`AnimationAuthoring.LoadAsync`, after assigning `_data`): if `_data.Fps <= 0`,
  set it from the first container's fps if any, else the config/default 24. New scenes default to 24.
- **`AnimationAuthoring`** new API:
  - `int GetSceneFps()` → `_data?.Fps ?? 24`.
  - `void SetSceneFps(int fps)` → clamps `>=1`, sets `_data.Fps`, publishes a container-changed signal
    for the active owner (`Change = FpsChanged`) so the panel reconfigures the clock, then
    `RebuildActiveClips()` + `RequestSave()`.
- **Playback uses scene fps everywhere container fps was used:** `RebuildActiveClips` →
  `RebuildClip(t, GetSceneFps())`; `ApplyFrame` → `t = frame / GetSceneFps()`.
- **Panel:** `OnFpsSubmitted` → `_ctx.Authoring.SetSceneFps(f)` (no longer per-owner). The fps input is
  populated from `GetSceneFps()` in `ApplyContainerToClock`. `_ctx.Clock.Configure(totalFrames, sceneFps)`.
- **Per-container `ActionContainer.Fps`:** kept in the data class for JSON back-compat; no longer read
  for playback. (Optional: on save, mirror scene fps into it — not required.)

### D. Config-driven metrics (#config)

Add to `AnimatorPanelConfig`:
```csharp
[Header("Key marker sizes")]
public float KeySize         = 22f;
public float KeySizeSelected = 26f;

[Header("Ruler tick sizes")]
public float MajorTickHeight = 24f;
public float MinorTickHeight = 16f;

[Header("Track row")]
public float RowHeight = 52f;   // (joins existing TrackRow_* colors)
```
Wire:
- `TimelineRow.SetKeys` uses `_config.KeySize` / `_config.KeySizeSelected` instead of `22f`/`26f`.
- `TimelineRow.Bind` sets the row's `LayoutElement.minHeight/preferredHeight = _config.RowHeight`.
- `AnimatorSubRuler.Rebuild` uses `_config.MajorTickHeight` / `_config.MinorTickHeight` instead of
  `24f`/`16f`.
- Colors already read from config; `TimelineRow.SetKeys` already overwrites the diamond's baked Image
  color on every call, so config colors win at runtime. (No change needed; covered by a test.)

### E. Loop / Once mode + switch button (#4, #4.1)

**`AnimationClock`:**
```csharp
public enum AnimationPlayMode { Once, Loop }
public AnimationPlayMode PlayMode { get; private set; } = AnimationPlayMode.Once;
public void SetPlayMode(AnimationPlayMode mode) { PlayMode = mode; }
```
In `Tick`, replace the end-of-range branch:
```csharp
if (next >= TotalFrames)
{
    if (PlayMode == AnimationPlayMode.Loop)
    {
        CurrentFrame = 0;
        _accumulated = 0f;
        _bus.Publish(new FrameChangedEvent { Frame = 0 });   // still playing → authoring samples 0
        return;
    }
    // Once: stop, rewind playhead to 0, flag completion
    IsPlaying    = false;
    CurrentFrame = 0;
    _accumulated = 0f;
    _bus.Publish(new FrameChangedEvent         { Frame = 0 });
    _bus.Publish(new PlaybackStateChangedEvent { IsPlaying = false, Frame = 0, Completed = true });
    return;
}
```
- **`PlaybackStateChangedEvent`**: add `public bool Completed;` (default false). All existing
  publishers pass `Completed = false` implicitly (field defaults to false for the ones not set).
- **Frame-0 pose on completion (4.1):** `AnimationAuthoring` subscribes to `PlaybackStateChangedEvent`;
  when `e.Completed`, it force-samples frame 0:
  ```csharp
  private void OnPlaybackState(PlaybackStateChangedEvent e)
  {
      if (e.Completed) ApplyFrame(0);   // curve clamps to first key → first-keyframe pose at frame 0
  }
  ```
  `ApplyFrame` must run even though `IsPlaying` is now false; it is called directly here (the
  `IsPlaying` guard lives in `OnFrameChanged`, not in `ApplyFrame`, so no change to `ApplyFrame`).
  Loop wrap keeps sampling through the normal `OnFrameChanged` path (still playing).

**Transport UI:**
- `AnimatorSubTransport`: add `[SerializeField] Button _modeButton;`, `[SerializeField] Image _modeIcon;`,
  `[SerializeField] Sprite _onceSprite;`, `[SerializeField] Sprite _loopSprite;`, `public Action OnToggleMode;`,
  and `public void SetMode(bool loop) { if (_modeIcon != null) _modeIcon.sprite = loop ? _loopSprite : _onceSprite; }`.
  `Awake` wires `_modeButton.onClick → OnToggleMode`.
- `AnimatorPanel.WireTransport`: `_transport.OnToggleMode = OnToggleModeClicked;` where
  `OnToggleModeClicked` flips `_ctx.Clock.PlayMode` (`Once↔Loop`), calls `_ctx.Clock.SetPlayMode(...)`,
  and `_transport.SetMode(loop)`. On `Refresh()`/initial wire, `_transport.SetMode(_ctx.Clock.PlayMode == Loop)`.
- **Prefab:** add the mode button to the transport bar in `AnimatorPanelModule.prefab` (a sibling of
  the play/pause button), wire `_modeButton`/`_modeIcon`; leave `_onceSprite`/`_loopSprite` empty for
  the user to fill.

## Data flow — unchanged elsewhere

`SceneAnimationData`/`ActionContainer`/`AnimTrackData` shape (besides the new scene `Fps`), selection,
persistence path, and the timeline coordinate model (`TrackNameWidth` offset, `FramePx`) are unchanged.

## Components touched

| File / asset | Change |
|---|---|
| `Scripts/Animation/AnimationClock.cs` | `AnimationPlayMode`, `PlayMode`, `SetPlayMode`, loop/once end-of-range branch |
| `Scripts/Animation/Events/PlaybackStateChangedEvent.cs` | add `bool Completed` |
| `Scripts/Animation/SceneAnimationData.cs` | add `int Fps` |
| `Scripts/Animation/AnimationAuthoring.cs` | new-track `TracksChanged` in `SetKey`/`PasteFrame`; `GetSceneFps`/`SetSceneFps`; scene-fps in `RebuildActiveClips`/`ApplyFrame`; load-normalize; subscribe `PlaybackStateChanged` → frame-0 sample on `Completed` |
| `Scripts/SpatialUi/AnimatorPanelConfig.cs` | `KeySize`, `KeySizeSelected`, `MajorTickHeight`, `MinorTickHeight`, `RowHeight` |
| `Scripts/SpatialUi/Elements/TimelineRow.cs` | key sizes + row height from config |
| `Scripts/SpatialUi/Panels/AnimatorSubRuler.cs` | tick heights from config |
| `Scripts/SpatialUi/Panels/AnimatorSubToolbar.cs` | VR commit fix; `_modeButton`/`_modeIcon`/`_onceSprite`/`_loopSprite`? (transport, see below) |
| `Scripts/SpatialUi/Panels/AnimatorSubTransport.cs` | mode button + icon swap |
| `Scripts/SpatialUi/Panels/AnimatorPanel.cs` | `OnFpsSubmitted→SetSceneFps`; fps from `GetSceneFps`; toggle-mode wiring; rename-event rebuild (if event exists) |
| `Content/Prefabs/UI/Panels/UserPanel/AnimatorPanelModule.prefab` | add mode button + wire; (VR keyboard commit if prefab-side) |

## Verification

**EditMode unit tests:**
- `AnimationClock`: Once mode stops at end with `CurrentFrame == 0` and a `Completed` event; Loop mode
  wraps `TotalFrames → 0` and keeps `IsPlaying`.
- `AnimationAuthoring`: `SetKey` on a brand-new track publishes `ContainerChange.TracksChanged`;
  `SetSceneFps` changes `GetSceneFps()` and is reflected by rebuilt clips; load-normalize sets a sane
  fps when the file has none.
- `SceneAnimationData`: round-trips `Fps` through `JsonUtility` and keeps `schemaVersion == 2`.

**Structural (EditMode):** `AnimatorPanelModule.prefab` has the mode button wired
(`_modeButton`/`_modeIcon` non-null); `AnimatorPanelConfig` exposes the new metric fields.

**In-headset:**
1. Keyframe a bone on a rig (no reopen) → its row appears immediately; keyframe a new object track →
   row appears immediately.
2. Edit total-frames / fps / current-frame in the toolbar (VR keyboard) → values apply (timeline
   length, playback speed, playhead move).
3. Change fps → **all** animations in the scene play at the new rate.
4. Toggle mode to Loop → playback wraps and repeats; toggle to Once → after the end the object snaps to
   frame 0 in its first-keyframe pose. The mode button icon swaps.
5. Adjust config (key size, tick height, row height, colors) → the timeline reflects it.

## Out of scope (deferred)

- Persisting the play mode across sessions.
- Ping-pong / hold-last-frame playback modes.
- Mirroring scene fps back into per-container `Fps` (kept only for JSON back-compat).
- Live track-name refresh on rename **if** no rename event currently exists (falls back to rebuild on
  reselect; revisit if a rename event is added).

## Risks

- VR keyboard commit: the exact API that does/doesn't raise `onEndEdit` must be confirmed before
  picking the fix (plan's first task investigates `VrKeyboard`).
- `PlaybackStateChangedEvent` gains a field — every publisher compiles unchanged (struct field defaults
  to false), but each call site should be eyeballed so `Completed` is only true on the once-end path.
- Prefab edit (new transport button) is verified visually in headset; anchors aren't compile-checked.
