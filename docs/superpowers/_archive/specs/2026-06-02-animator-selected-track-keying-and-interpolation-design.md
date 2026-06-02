# Animator — Per-Object Track, Selected-Track Keying, Interpolation Mode & Scrub Preview (design)

**Date:** 2026-06-02
**Status:** Approved (pending user spec review)
**Scope:** One rig bug fix + three animator features, all inside the `Animation` subsystem and the
`AnimatorPanel`/`AnimatorSubToolbar` UI. (A separate primitive scene exporter is being scaffolded in
parallel and is NOT part of this spec.)

## Context

After the playback/fps/config work, animation works for plain objects. Remaining rig issue + three
requested features:

- **Bug:** Adding animation to a **rig** does not create a track for the rig object itself. A track for
  the owner object must be created automatically on Add for **every** object type.
- **F1:** The "+ key" / "− key" toolbar buttons must set/delete a key on **only the currently selected
  track**, not on all tracks at once.
- **F2:** A toolbar button to switch the **interpolation type** of the object's animation block —
  **Linear** or **Stepped** (constant/hold). Stored per `ActionContainer`.
- **F3:** Scrubbing to a frame **between** keyframes must show the **interpolated** intermediate pose
  (respecting the interpolation mode), even when not playing.

### Current-state facts (verified in code)

- `AnimatorPanel.OnAddAnimationClicked` pre-creates the owner track only for a non-rig plain object:
  ```csharp
  bool isBone = selected != null && selected.StartsWith("bone:");
  var ownerGo = _ctx.Graph?.GetNode(owner);
  bool isRig  = ownerGo != null && ownerGo.GetComponentInChildren<ProxyRigRuntime>() != null;
  if (!isBone && !isRig && owner == selected)
      _ctx.Authoring.EnsureTrack(owner, owner);
  ```
  The `!isRig` guard is the bug — rigs are skipped.
- `OnSetKeyClicked` calls `_ctx.Authoring.SetKeyForFrame(_activeOwner, active, frame)`, which snapshots
  and keys the active node **plus every existing track**. `OnDeleteKeyClicked` calls
  `DeleteAllKeysAtFrame(_activeOwner, frame)` (deletes the frame on all tracks).
- `AnimationAuthoring.SetKey(string nodeId, int frame)` already keys a **single** track from the
  scene-graph transform, creating the track (and publishing `TracksChanged`, from the live-track work)
  when new. `DeleteKey(string nodeId, int frame)` removes a single key and drops the track when it
  becomes empty — but it does **not** publish `TracksChanged`, so an emptied track's row lingers until
  the next full rebuild.
- The toolbar buttons are labelled "+ key" / "− key" (wired to `_setKeyButton`/`_deleteKeyButton` →
  `OnSetKey`/`OnDeleteKey`). Toolbar has no interpolation control yet.
- `ActionContainer` is a `[Serializable]` JSON class (`OwnerNodeId`, `Fps`, `TotalFrames`,
  `List<AnimTrackData> Tracks`). `AnimKeyData` carries only `Frame/Position/Rotation/Scale` — no
  per-key interpolation. `SceneAnimationData.schemaVersion == 2`.
- `AnimationAuthoring.RebuildClip(track, fps)` builds a legacy `AnimationClip` with `AddKey(t, value)`
  on ten curves (pos xyz, rot xyzw, scale xyz). `ApplyFrame(frame)` samples those clips.
- `AnimationAuthoring.OnFrameChanged` only applies while playing:
  ```csharp
  private void OnFrameChanged(FrameChangedEvent e)
  {
      if (_data == null || _clock == null || !_clock.IsPlaying) return;
      ApplyFrame(e.Frame);
  }
  ```
- `AnimationUtility.SetKeyLeftTangentMode/...` is **editor-only** (UnityEditor namespace) — must NOT be
  used in runtime code. Tangents must be set numerically on `Keyframe` structs at runtime.

## Decisions (locked with the user)

1. **"+ key" / "− key" affect only the selected track.** The old "key all tracks at once" behavior is
   removed (no separate control kept).
2. **Interpolation switch is a text-label button** (shows "Linear" / "Stepped"), not a sprite swap.
3. **Interpolation is per `ActionContainer`** ("the object's animation block") — one mode for all of
   that object's tracks. Default **Linear**.
4. **No schema bump** — `InterpolationMode` is a new `ActionContainer` field defaulting to `Linear`
   (enum serializes as int 0); old `animation.json` loads as Linear. `schemaVersion` stays 2.
5. **Scrub preview is always on when there is an active animation container** (drop the `IsPlaying`
   guard); `ApplyFrame` already no-ops without an active owner.
6. **Loop is a per-object property with background playback** (supersedes the prior scene-wide clock
   `PlayMode`). A looped object starts looping when **Play** is pressed and keeps looping on its own
   independent cursor even after another object is selected. The transport (Play/Pause/scrub) acts only
   on the **selected** object; a background loop is stopped by re-selecting that object and pausing, or
   by turning its Loop flag off.

## Design

### A. Rig bug — owner track on Add for every type

`AnimatorPanel.OnAddAnimationClicked`: remove the `isRig` computation and guard. Keep only the
bone/owner check so we create the owner's own track for any non-bone selection (plain object **or**
rig root):
```csharp
    _ctx.Authoring.CreateContainer(owner, _config.DefaultTotalFrames, _config.DefaultFps);

    bool isBone = selected != null && selected.StartsWith("bone:");
    if (!isBone && owner == selected)
        _ctx.Authoring.EnsureTrack(owner, owner);
```
(When a rig root is selected, `owner == selected` and it is not a bone, so the rig now gets its own
track — visible immediately via the existing `TracksChanged → RebuildTimeline` path.)

### B. F1 — selected-track keying

`AnimatorPanel.OnSetKeyClicked`:
```csharp
    private void OnSetKeyClicked()
    {
        if (string.IsNullOrEmpty(_activeOwner)) return;
        var target = _ctx.Selection?.SelectedNodeId ?? _activeOwner;
        _ctx.Authoring.SetKey(target, _ctx.Clock.CurrentFrame);  // keys only the selected track
        RefreshKeyButtonStates();
    }
```
`AnimatorPanel.OnDeleteKeyClicked`:
```csharp
    private void OnDeleteKeyClicked()
    {
        if (string.IsNullOrEmpty(_activeOwner)) return;
        var target = _ctx.Selection?.SelectedNodeId ?? _activeOwner;
        _ctx.Authoring.DeleteKey(target, _ctx.Clock.CurrentFrame);
    }
```
`SetKey(nodeId, frame)` (no-arg-transform overload) reads the node's current transform from the scene
graph and upserts the key on that single track, creating it (with `TracksChanged`) if new. `DeleteKey`
removes the single key.

**Track-removal liveness:** update `AnimationAuthoring.DeleteKey` so that when removing the last key
drops the track, it also publishes `ContainerChange.TracksChanged` (so the emptied row disappears
without a reopen):
```csharp
        track.RemoveKey(frame);
        bool trackRemoved = false;
        if (track.Keys.Count == 0) { c.Tracks.Remove(track); trackRemoved = true; }

        _bus.Publish(new AnimationKeyframeChangedEvent { /* …Removed… */ });
        if (trackRemoved)
            _bus.Publish(new AnimationContainerChangedEvent
                { OwnerNodeId = owner, Change = ContainerChange.TracksChanged });
        RequestSave();
        RebuildActiveClips();
```

> The now-unused `SetKeyForFrame` / `DeleteAllKeysAtFrame` / `SetKeyForFrame_Test` stay in
> `AnimationAuthoring` (they have existing tests); they are simply no longer wired to the buttons.
> Removing them is out of scope.

### C. F2 — interpolation mode per container

- **New enum** `Assets/_App/Scripts/Animation/InterpolationMode.cs`:
  ```csharp
  public enum InterpolationMode { Linear, Stepped }
  ```
- **`ActionContainer`**: add `public InterpolationMode Interpolation = InterpolationMode.Linear;`
  (after `Fps`). JsonUtility serializes it as an int; absent in old files ⇒ 0 ⇒ Linear.
- **`ContainerChange`**: add `InterpolationChanged`.
- **`AnimationAuthoring`**:
  - `InterpolationMode GetInterpolation(string ownerNodeId)` → container's mode (Linear if none).
  - `void SetInterpolation(string ownerNodeId, InterpolationMode mode)` → set on the container,
    publish `AnimationContainerChangedEvent { Change = InterpolationChanged }`, `RebuildActiveClips()`,
    `RequestSave()`.
  - `RebuildActiveClips` passes the container's mode: `RebuildClip(t, GetSceneFps(), c.Interpolation);`
  - `RebuildClip(AnimTrackData track, int fps, InterpolationMode mode)` — after populating the ten
    curves with `AddKey`, run a tangent pass per curve:
    ```csharp
    private static void ApplyInterpolation(AnimationCurve curve, InterpolationMode mode)
    {
        var keys = curve.keys;
        for (int i = 0; i < keys.Length; i++)
        {
            if (mode == InterpolationMode.Stepped)
            {
                keys[i].inTangent  = float.PositiveInfinity;
                keys[i].outTangent = float.PositiveInfinity;
            }
            else // Linear
            {
                if (i < keys.Length - 1)
                {
                    float dt = keys[i + 1].time - keys[i].time;
                    keys[i].outTangent = dt > 0f ? (keys[i + 1].value - keys[i].value) / dt : 0f;
                }
                if (i > 0)
                {
                    float dt = keys[i].time - keys[i - 1].time;
                    keys[i].inTangent = dt > 0f ? (keys[i].value - keys[i - 1].value) / dt : 0f;
                }
            }
        }
        curve.keys = keys;
    }
    ```
    Call `ApplyInterpolation(curve, mode)` on each of the ten curves before `clip.SetCurve(...)`.
    (`float.PositiveInfinity` out-tangent is Unity's runtime "Constant/stepped" — the segment holds the
    left key's value. Linear sets straight-line slopes to each neighbor.)
- **`AnimatorSubToolbar`**: add `[SerializeField] private Button _interpolationButton;` and
  `[SerializeField] private TMP_Text _interpolationLabel;`, `public Action OnToggleInterpolation;`,
  wire the button in `Awake`, and `public void SetInterpolationLabel(string text) { if (_interpolationLabel != null) _interpolationLabel.text = text; }`.
- **`AnimatorPanel`**:
  - `WireToolbar`: `_toolbar.OnToggleInterpolation = OnToggleInterpolationClicked;`
  - Handler:
    ```csharp
    private void OnToggleInterpolationClicked()
    {
        if (string.IsNullOrEmpty(_activeOwner)) return;
        var cur  = _ctx.Authoring.GetInterpolation(_activeOwner);
        var next = cur == InterpolationMode.Stepped ? InterpolationMode.Linear : InterpolationMode.Stepped;
        _ctx.Authoring.SetInterpolation(_activeOwner, next);
        _toolbar.SetInterpolationLabel(next.ToString());
    }
    ```
  - In `ApplyContainerToClock` (or `ShowActive`/`RebuildTimeline`), set the label to the current mode:
    `_toolbar?.SetInterpolationLabel(_ctx.Authoring.GetInterpolation(_activeOwner).ToString());`
  - Handle `ContainerChange.InterpolationChanged` in `OnContainerChanged` (for `_activeOwner`):
    `RebuildTimeline()` is unnecessary (keys don't move), but re-sample the current frame so the
    preview updates: `ApplyContainerToClock(); RefreshRowKeys();` — minimally, do nothing extra since
    `SetInterpolation` already rebuilt clips; just ensure the label reflects it.

### D. F3 — scrub preview

`AnimationAuthoring.OnFrameChanged` — drop the `IsPlaying` guard so the pose updates on scrub/seek too:
```csharp
    private void OnFrameChanged(FrameChangedEvent e)
    {
        if (_data == null) return;
        ApplyFrame(e.Frame);
    }
```
`ApplyFrame` already returns early when `_activeContainerOwner` is empty, so preview only happens for
the active animation. With F2, `ApplyFrame` samples clips whose tangents encode Linear vs Stepped, so a
mid-key scrub shows the correct interpolated (or held) pose.

> Note: keys captured by "+ key" after a scrub snapshot the currently-sampled (interpolated) transform
> — standard animation behavior.

### E. Per-object Loop with background playback (supersedes the prior scene-wide clock Loop)

**Model.** Loop is a property of the object's animation block (`ActionContainer.Loop`, persisted). A
looped object, once started with **Play**, keeps looping on its own independent cursor even when another
object is selected. The transport acts only on the selected object. Stop a background loop by
re-selecting it and pausing, or by turning its Loop flag off.

**Clock simplification (reconciliation with the prior round).** The scene-wide `AnimationClock.PlayMode`
/ `SetPlayMode` and the Loop branch in `AdvanceFrame` are **removed**; the clock is now always
single-shot — at the end it stops, rewinds to 0, and publishes `Completed` (the first-keyframe reset
4.1 is unchanged). The `AnimationPlayMode` enum is **deleted**. (None of the prior Loop wiring was used
in-headset — the mode button's sprites were never assigned — so repurposing it is free. The prior
`AnimationClockTests` Loop/Once-mode tests are updated accordingly: the once-end-resets behavior becomes
the clock's only end behavior.)

**Data.** `ActionContainer.Loop` (`public bool Loop = false;`, JSON-serialized; schema stays 2).
`ContainerChange` gains `LoopChanged`.

**Background loop runtime — `AnimationAuthoring` also implements `ITickable`.**
VContainer already calls `Tick()` because `AnimationAuthoring` is registered via `RegisterEntryPoint`
(which wires every lifecycle interface) — **no DI change**. New private state:
```csharp
    private readonly Dictionary<string, float> _loopCursors = new();                          // owner → frame cursor
    private readonly Dictionary<string, Dictionary<string, AnimationClip>> _loopClips = new(); // owner → its track clips
```
API:
- `bool IsLooping(string ownerNodeId)` → container's `Loop` flag.
- `void SetLoop(string ownerNodeId, bool loop)` → set flag, publish `AnimationContainerChangedEvent { Change = LoopChanged }`, `RequestSave()`; if `loop == false` also `StopLoopPlayback(owner)`.
- `bool IsLoopPlaying(string ownerNodeId)` → `_loopCursors.ContainsKey(owner)`.
- `void StartLoopPlayback(string ownerNodeId, int startFrame)` → only if the container exists and is `Loop`; build the owner's clips into `_loopClips[owner]` (one `RebuildClip` per track, scene fps + container interpolation); `_loopCursors[owner] = startFrame`.
- `void StopLoopPlayback(string ownerNodeId)` → remove from both dictionaries.
- `Tick()`:
  ```csharp
  public void Tick()
  {
      if (_data == null || _loopCursors.Count == 0) return;
      float fps = GetSceneFps();
      foreach (var owner in new List<string>(_loopCursors.Keys)) // snapshot: StopLoopPlayback mutates
      {
          var c = _data.FindByOwner(owner);
          if (c == null || !c.Loop) { StopLoopPlayback(owner); continue; }
          float cursor = _loopCursors[owner] + Time.deltaTime * fps;
          if (cursor >= c.TotalFrames) cursor -= c.TotalFrames; // wrap
          if (cursor < 0f) cursor = 0f;
          _loopCursors[owner] = cursor;
          SampleContainerAt(c, _loopClips[owner], cursor / Mathf.Max(1f, fps));
      }
  }
  ```
  with a helper `SampleContainerAt(ActionContainer c, Dictionary<string,AnimationClip> clips, float t)`
  that mirrors `ApplyFrame`'s inner loop (resolve each track's node via `_sceneGraph`, `clip.SampleAnimation(go, t)`).
- **No double-sampling:** `ApplyFrame` (clock/scrub path for the active owner) skips an owner that is
  currently loop-playing — `if (_loopCursors.ContainsKey(_activeContainerOwner)) return;` at the top.
- `Dispose()` clears `_loopCursors`/`_loopClips`.

**Panel (`AnimatorPanel`).**
- Mode button now toggles the selected container's Loop:
  ```csharp
  private void OnToggleModeClicked()
  {
      if (string.IsNullOrEmpty(_activeOwner)) return;
      bool next = !_ctx.Authoring.IsLooping(_activeOwner);
      _ctx.Authoring.SetLoop(_activeOwner, next);
      _transport?.SetMode(next); // loop sprite when looping, once sprite otherwise
  }
  ```
  In `Refresh`/`ApplyContainerToClock`, set `_transport.SetMode(_ctx.Authoring.IsLooping(_activeOwner))`
  and the play/pause icon to the right state (loop-playing for a looped owner, else clock state).
- Play/Pause branches on the selected container's Loop:
  ```csharp
  private void OnPlayPauseClicked()
  {
      if (_ctx.Clock == null) return;
      if (!string.IsNullOrEmpty(_activeOwner) && _ctx.Authoring.IsLooping(_activeOwner))
      {
          if (_ctx.Authoring.IsLoopPlaying(_activeOwner)) _ctx.Authoring.StopLoopPlayback(_activeOwner);
          else _ctx.Authoring.StartLoopPlayback(_activeOwner, _ctx.Clock.CurrentFrame);
          _transport?.SetPlaying(_ctx.Authoring.IsLoopPlaying(_activeOwner));
          return;
      }
      if (_ctx.Clock.IsPlaying) _ctx.Clock.Pause(); else _ctx.Clock.Play();
  }
  ```
- Selecting a different object does **not** stop loops. `OnToggleModeClicked` no longer touches the clock.

**Quirk (documented).** While a looping object is selected and its background loop runs, the clock's
scrub/Play does not move it (the loop owns its sampling) and the panel playhead does not follow it. To
scrub/edit such an object, pause its loop first.

## Components touched

| File / asset | Change |
|---|---|
| `Scripts/Animation/InterpolationMode.cs` | **new** enum `{ Linear, Stepped }` |
| `Scripts/Animation/ActionContainer.cs` | add `InterpolationMode Interpolation = Linear`; add `bool Loop = false` |
| `Scripts/Animation/ContainerChange.cs` | add `InterpolationChanged`, `LoopChanged` |
| `Scripts/Animation/AnimationClock.cs` | **remove** `PlayMode`/`SetPlayMode` and the Loop branch in `AdvanceFrame` (always single-shot; keep `Completed`) |
| `Scripts/Animation/AnimationPlayMode.cs` | **delete** (enum no longer used) |
| `Scripts/Animation/AnimationAuthoring.cs` | `GetInterpolation`/`SetInterpolation`; `RebuildClip` tangent pass; `DeleteKey` track-removal `TracksChanged`; `OnFrameChanged` guard drop; **`ITickable` + background loop runtime** (`IsLooping`/`SetLoop`/`IsLoopPlaying`/`StartLoopPlayback`/`StopLoopPlayback`/`Tick`/`SampleContainerAt`); `ApplyFrame` skip for loop-playing owner |
| `Scripts/SpatialUi/Panels/AnimatorSubToolbar.cs` | interpolation button + label + action + setter |
| `Scripts/SpatialUi/Panels/AnimatorPanel.cs` | rig bug fix; selected-track set/delete; interpolation wiring + label; mode button → per-object Loop; Play/Pause loop branch |
| `Content/Prefabs/UI/Panels/UserPanel/AnimatorPanelModule.prefab` | add the interpolation button (Button + TMP_Text label) to the toolbar; wire `_interpolationButton`/`_interpolationLabel` |
| `Tests/Animation/AnimationClockTests.cs` | update: remove Loop-mode tests; keep once-end-resets as the only end behavior |

## Verification

**EditMode unit tests:**
- `ActionContainer` defaults `Interpolation == Linear`; JSON round-trips it; `schemaVersion == 2`.
- `AnimationAuthoring.SetInterpolation` changes `GetInterpolation` and publishes `InterpolationChanged`.
- Stepped vs Linear sampling: build a track with two keys (frame 0 value A, frame 10 value B), set
  Stepped → sampling at the midpoint frame returns ≈A (held); set Linear → midpoint returns ≈(A+B)/2.
  (Drive through `AnimationAuthoring` with a fake `ISceneGraph` + a real GameObject, sample via the
  completion/scrub path, assert the GameObject's transform.)
- `SetKey(selectedNode)` keys only that track (other tracks unchanged); `DeleteKey` emptying a track
  publishes `TracksChanged`.
- `ActionContainer` defaults `Loop == false`; `SetLoop` flips `IsLooping` and publishes `LoopChanged`;
  `SetLoop(owner, false)` stops loop playback (`IsLoopPlaying` becomes false).
- Background loop: with a 2-key track (frame 0 → A, frame `TotalFrames` → B) and `Loop == true`,
  `StartLoopPlayback(owner, 0)` then a `Tick()` (simulated by injecting a known `Time.deltaTime` is not
  possible in EditMode — instead test the cursor-wrap arithmetic via a small `internal` helper
  `AdvanceLoopCursor(float cursor, float deltaFrames, int total)` that returns the wrapped cursor, and
  assert it wraps past `total`). `StopLoopPlayback` removes the owner from the loop set.
- `AnimationClock` always single-shot: `AdvanceFrame(TotalFrames)` stops, sets `CurrentFrame == 0`, and
  flags `Completed` (the prior Loop-mode test is removed; no `PlayMode` member remains).

**In-headset:**
1. Add animation on a **rig** → an owner track row appears immediately (plus bones as you key them).
2. Select one bone/track → "+ key" keys only that track; "− key" removes only that track's key;
   emptying a track removes its row live.
3. Toggle the interpolation button → label flips Linear/Stepped; with Stepped, scrubbing between keys
   holds the previous pose; with Linear, it blends.
4. Scrub the timeline between keys (not playing) → the object shows the interpolated intermediate pose.
5. Mark object A's animation **Loop** (mode button) → Play → A loops. Select object B → A keeps looping;
   edit/key B independently. Re-select A → Pause stops A's loop (or toggle Loop off to stop it).

## Out of scope (deferred)

- Per-key interpolation (this is per-container only).
- Removing the now-unused `SetKeyForFrame`/`DeleteAllKeysAtFrame`.
- The scene exporter (separate scaffold + its prefab/navbar wiring handoff).
- Ease/bezier curves; only Linear and Stepped.

## Risks

- Runtime tangent math: `float.PositiveInfinity` out-tangent for Stepped and numeric slopes for Linear
  must be validated by the sampling unit test (and in-headset), since `AnimationUtility` cannot be used.
- Dropping the `IsPlaying` guard means any `FrameChanged` re-poses the active animation's objects; this
  is intended for scrub preview but means manual posing must be followed by "+ key" before scrubbing
  away (otherwise the manual pose is overwritten by the sampled one) — standard, but note it.
- Prefab toolbar edit (new button) is verified in headset; anchors aren't compile-checked.
- **Background loop reworks prior delivered code** (clock `PlayMode`, mode-button wiring, `AnimationPlayMode`
  enum, three `AnimationClockTests`). All of it is pre-headset, but the plan must update those tests and
  remove the enum cleanly so nothing references the deleted members.
- **Double-sampling:** a loop-playing owner must be sampled only by `Tick`, never also by the
  clock/`ApplyFrame` path — the `_loopCursors.ContainsKey` guard in `ApplyFrame` is load-bearing.
- `Tick` uses `Time.deltaTime`, which is 0 in EditMode; the cursor-advance/wrap is unit-tested through a
  pure `internal AdvanceLoopCursor(...)` helper, and the end-to-end loop is verified in headset.
