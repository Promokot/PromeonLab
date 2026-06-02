# Animator — Per-Object Track, Selected-Track Keying, Interpolation & Per-Object Loop — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the rig owner-track bug and add selected-track keying, per-block Linear/Stepped interpolation, scrub preview, and per-object Loop with background playback.

**Architecture:** All changes are in the `Animation` subsystem (`ActionContainer`, `AnimationClock`, `AnimationAuthoring`, events) and the `AnimatorPanel`/`AnimatorSubToolbar` UI. Loop becomes a per-`ActionContainer` flag driven by a background `ITickable` loop runtime inside `AnimationAuthoring`; the scene-wide clock `PlayMode` from the prior round is removed (clock is now always single-shot).

**Tech Stack:** Unity 6000.3.7f1, C# (no namespaces for runtime), VContainer, custom `EventBus`, NUnit EditMode tests in `_App.Tests`, Unity-MCP for compile/test verification.

**Spec:** `docs/superpowers/specs/2026-06-02-animator-selected-track-keying-and-interpolation-design.md`

---

## Conventions for every task (READ FIRST)

- **NO GIT.** Never run any `git` command. Where a normal plan says "commit", this plan says **Checkpoint**.
- **Checkpoint (after each task's code is written):**
  1. `refresh_unity(mode="force", scope="all", compile="request", wait_for_ready=true)`
  2. `read_console(action="get", types=["error"], count="30")` → no `CS####` errors. Acceptable noise: `MCP-FOR-UNITY: Client handler exited`; after closing a prefab stage also `SerializedObjectNotCreatableException`/`MissingReferenceException`.
  3. `run_tests(mode="EditMode")` → poll `get_test_job`. The task's new tests pass; the only allowed pre-existing failures are `PathProviderTests` ×4 and `RingRotateStrategyTests` ×2. No other failures.
- **Tests:** plain NUnit, no namespace, in `Assets/_App/Tests/Animation/`. Construct services directly: `new AnimationClock(new EventBus())`; `new AnimationAuthoring(null, null, null, null, new EventBus())` + `InitForTest()` (debounced save is a no-op with null `_sceneId`, so null `paths`/`storage` are safe). `[assembly: InternalsVisibleTo("_App.Tests")]` exists, so `internal` members are testable.
- **One public type per file**; file name == type name. Private fields `_camelCase`. `[Serializable]` JSON data classes may use public fields.

---

## File Structure

| File | Responsibility | Task |
|---|---|---|
| `Scripts/Animation/InterpolationMode.cs` | **new** enum `{ Linear, Stepped }` | 1 |
| `Scripts/Animation/ActionContainer.cs` | add `Interpolation`, `Loop` | 1 |
| `Scripts/Animation/ContainerChange.cs` | add `InterpolationChanged`, `LoopChanged` | 1 |
| `Scripts/Animation/AnimationAuthoring.cs` | interpolation API + tangent clip build; DeleteKey live; scrub preview; loop runtime | 2,3,4 |
| `Scripts/Animation/AnimationClock.cs` | remove `PlayMode`; always single-shot | 7 |
| `Scripts/Animation/AnimationPlayMode.cs` | **delete** | 7 |
| `Scripts/SpatialUi/Panels/AnimatorSubToolbar.cs` | interpolation button + label | 5 |
| `Scripts/SpatialUi/Panels/AnimatorPanel.cs` | rig fix; selected-track keys; interpolation wiring; loop rewire | 6,7 |
| `Content/Prefabs/UI/Panels/UserPanel/AnimatorPanelModule.prefab` | interpolation button | 8 |
| `Tests/Animation/*` | unit tests | 1–4,7 |

---

## Task 1: Data — Interpolation & Loop on the container

**Files:**
- Create: `Assets/_App/Scripts/Animation/InterpolationMode.cs`
- Modify: `Assets/_App/Scripts/Animation/ActionContainer.cs`
- Modify: `Assets/_App/Scripts/Animation/ContainerChange.cs`
- Test: `Assets/_App/Tests/Animation/AnimationDataTests.cs`

- [ ] **Step 1: New enum file** — `Assets/_App/Scripts/Animation/InterpolationMode.cs`:
```csharp
public enum InterpolationMode { Linear, Stepped }
```

- [ ] **Step 2: Failing tests** — append inside `AnimationDataTests`:
```csharp
    [Test]
    public void ActionContainer_Defaults_LinearAndNotLooping()
    {
        var c = new ActionContainer();
        Assert.AreEqual(InterpolationMode.Linear, c.Interpolation);
        Assert.IsFalse(c.Loop);
    }

    [Test]
    public void ActionContainer_InterpolationAndLoop_RoundTrip_SchemaV2()
    {
        var data = new SceneAnimationData();
        var c    = data.CreateContainer("obj", 60, 24);
        c.Interpolation = InterpolationMode.Stepped;
        c.Loop          = true;

        var loaded = UnityEngine.JsonUtility.FromJson<SceneAnimationData>(UnityEngine.JsonUtility.ToJson(data));
        Assert.AreEqual(InterpolationMode.Stepped, loaded.Containers[0].Interpolation);
        Assert.IsTrue(loaded.Containers[0].Loop);
        Assert.AreEqual(2, loaded.schemaVersion);
    }
```

- [ ] **Step 3: Run, verify FAIL** — `run_tests(mode="EditMode")`; missing `Interpolation`/`Loop`.

- [ ] **Step 4: Implement** — in `ActionContainer.cs`, add fields after `Fps`:
```csharp
    public string             OwnerNodeId;
    public int                Fps          = 24;
    public int                TotalFrames  = 60;
    public InterpolationMode  Interpolation = InterpolationMode.Linear;
    public bool               Loop         = false;
    public List<AnimTrackData> Tracks      = new();
```
In `ContainerChange.cs`, add the two values:
```csharp
public enum ContainerChange { Added, Removed, LengthChanged, FpsChanged, TracksChanged, InterpolationChanged, LoopChanged }
```

- [ ] **Step 5: Run, verify PASS.**

- [ ] **Step 6: Checkpoint.** No git.

---

## Task 2: Authoring — interpolation API + tangent-aware clip build

**Files:**
- Modify: `Assets/_App/Scripts/Animation/AnimationAuthoring.cs`
- Test: `Assets/_App/Tests/Animation/AnimationAuthoringInterpolationTests.cs` (new)

**Context:** `AnimationAuthoring.RebuildClip(AnimTrackData track, int fps)` currently builds a legacy clip with `AddKey` and stores it in `_clips[track.NodeId]`. `RebuildActiveClips` calls `RebuildClip(t, GetSceneFps())` per track. We refactor `RebuildClip` into `BuildClip(track, fps, mode)` that **returns** the clip (so the loop runtime in Task 4 can reuse it) and applies tangents per `InterpolationMode`.

- [ ] **Step 1: Failing tests** — `Assets/_App/Tests/Animation/AnimationAuthoringInterpolationTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;

public class AnimationAuthoringInterpolationTests
{
    [Test]
    public void ApplyInterpolation_Stepped_HoldsLeftValue()
    {
        var curve = new AnimationCurve();
        curve.AddKey(0f, 0f);
        curve.AddKey(1f, 10f);
        AnimationAuthoring.ApplyInterpolation(curve, InterpolationMode.Stepped);
        Assert.AreEqual(0f, curve.Evaluate(0.5f), 0.01f, "stepped holds the previous key");
    }

    [Test]
    public void ApplyInterpolation_Linear_BlendsBetweenKeys()
    {
        var curve = new AnimationCurve();
        curve.AddKey(0f, 0f);
        curve.AddKey(1f, 10f);
        AnimationAuthoring.ApplyInterpolation(curve, InterpolationMode.Linear);
        Assert.AreEqual(5f, curve.Evaluate(0.5f), 0.01f, "linear blends to the midpoint");
    }

    private AnimationAuthoring NewAuthoring(out EventBus bus)
    {
        bus = new EventBus();
        var a = new AnimationAuthoring(null, null, null, null, bus);
        a.InitForTest();
        return a;
    }

    [Test]
    public void SetInterpolation_UpdatesValue_AndPublishesInterpolationChanged()
    {
        var a = NewAuthoring(out var bus);
        a.CreateContainer("obj", 60, 24);
        a.SetActiveContainerOwner("obj");

        ContainerChange? change = null;
        bus.Subscribe<AnimationContainerChangedEvent>(e => change = e.Change);

        a.SetInterpolation("obj", InterpolationMode.Stepped);
        Assert.AreEqual(InterpolationMode.Stepped, a.GetInterpolation("obj"));
        Assert.AreEqual(ContainerChange.InterpolationChanged, change);
    }
```

- [ ] **Step 2: Run, verify FAIL** — missing `ApplyInterpolation`/`GetInterpolation`/`SetInterpolation`.

- [ ] **Step 3: Implement** in `AnimationAuthoring.cs`:

(a) Add the interpolation API (near `SetFps`):
```csharp
    public InterpolationMode GetInterpolation(string ownerNodeId) =>
        _data?.FindByOwner(ownerNodeId)?.Interpolation ?? InterpolationMode.Linear;

    public void SetInterpolation(string ownerNodeId, InterpolationMode mode)
    {
        var c = _data?.FindByOwner(ownerNodeId);
        if (c == null) return;
        c.Interpolation = mode;
        _bus.Publish(new AnimationContainerChangedEvent
        {
            OwnerNodeId = ownerNodeId,
            Change      = ContainerChange.InterpolationChanged
        });
        RequestSave();
        RebuildActiveClips();
    }
```

(b) Refactor the clip builder. Replace the private `RebuildClip(AnimTrackData track, int fps)` method with a returning `BuildClip` that applies tangents, and add the tangent helper:
```csharp
    private AnimationClip BuildClip(AnimTrackData track, int fps, InterpolationMode mode)
    {
        var clip = new AnimationClip { legacy = true };
        var px = new AnimationCurve(); var py = new AnimationCurve(); var pz = new AnimationCurve();
        var rx = new AnimationCurve(); var ry = new AnimationCurve();
        var rz = new AnimationCurve(); var rw = new AnimationCurve();
        var sx = new AnimationCurve(); var sy = new AnimationCurve(); var sz = new AnimationCurve();

        foreach (var k in track.Keys)
        {
            float t = (float)k.Frame / fps;
            px.AddKey(t, k.Position.x); py.AddKey(t, k.Position.y); pz.AddKey(t, k.Position.z);
            rx.AddKey(t, k.Rotation.x); ry.AddKey(t, k.Rotation.y);
            rz.AddKey(t, k.Rotation.z); rw.AddKey(t, k.Rotation.w);
            sx.AddKey(t, k.Scale.x);    sy.AddKey(t, k.Scale.y);    sz.AddKey(t, k.Scale.z);
        }

        foreach (var curve in new[] { px, py, pz, rx, ry, rz, rw, sx, sy, sz })
            ApplyInterpolation(curve, mode);

        clip.SetCurve("", typeof(Transform), "localPosition.x",   px);
        clip.SetCurve("", typeof(Transform), "localPosition.y",   py);
        clip.SetCurve("", typeof(Transform), "localPosition.z",   pz);
        clip.SetCurve("", typeof(Transform), "m_LocalRotation.x", rx);
        clip.SetCurve("", typeof(Transform), "m_LocalRotation.y", ry);
        clip.SetCurve("", typeof(Transform), "m_LocalRotation.z", rz);
        clip.SetCurve("", typeof(Transform), "m_LocalRotation.w", rw);
        clip.SetCurve("", typeof(Transform), "localScale.x",      sx);
        clip.SetCurve("", typeof(Transform), "localScale.y",      sy);
        clip.SetCurve("", typeof(Transform), "localScale.z",      sz);
        return clip;
    }

    internal static void ApplyInterpolation(AnimationCurve curve, InterpolationMode mode)
    {
        var keys = curve.keys;
        for (int i = 0; i < keys.Length; i++)
        {
            if (mode == InterpolationMode.Stepped)
            {
                keys[i].inTangent  = float.PositiveInfinity;
                keys[i].outTangent = float.PositiveInfinity;
            }
            else
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

(c) Update `RebuildActiveClips` to use `BuildClip` with the container's interpolation:
```csharp
    private void RebuildActiveClips()
    {
        _clips.Clear();
        if (string.IsNullOrEmpty(_activeContainerOwner)) return;
        var c = _data?.FindByOwner(_activeContainerOwner);
        if (c == null) return;
        foreach (var t in c.Tracks) _clips[t.NodeId] = BuildClip(t, GetSceneFps(), c.Interpolation);
    }
```

- [ ] **Step 4: Run, verify PASS.**

- [ ] **Step 5: Checkpoint.** No git.

---

## Task 3: Authoring — live track removal + scrub preview

**Files:**
- Modify: `Assets/_App/Scripts/Animation/AnimationAuthoring.cs`
- Test: `Assets/_App/Tests/Animation/AnimationAuthoringDeleteKeyTests.cs` (new)

- [ ] **Step 1: Failing test** — `Assets/_App/Tests/Animation/AnimationAuthoringDeleteKeyTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;

public class AnimationAuthoringDeleteKeyTests
{
    private AnimationAuthoring NewAuthoring(out EventBus bus)
    {
        bus = new EventBus();
        var a = new AnimationAuthoring(null, null, null, null, bus);
        a.InitForTest();
        return a;
    }

    [Test]
    public void DeleteKey_EmptyingTrack_PublishesTracksChanged()
    {
        var a = NewAuthoring(out var bus);
        a.CreateContainer("obj", 60, 24);
        a.SetKey("obj", 5, Vector3.zero, Quaternion.identity, Vector3.one); // single key on the "obj" track

        bool tracksChanged = false;
        bus.Subscribe<AnimationContainerChangedEvent>(e =>
        { if (e.Change == ContainerChange.TracksChanged) tracksChanged = true; });

        a.DeleteKey("obj", 5); // removes the last key → track drops
        Assert.IsTrue(tracksChanged, "emptying a track announces TracksChanged so its row disappears");
        Assert.IsNull(a.GetContainer("obj").FindTrack("obj"));
    }
```

- [ ] **Step 2: Run, verify FAIL** — `DeleteKey` does not publish `TracksChanged` today.

- [ ] **Step 3: Implement** — in `AnimationAuthoring.DeleteKey`, detect track removal and announce it. Replace the body after the `if (!track.HasKey(frame)) return;` guard:
```csharp
        track.RemoveKey(frame);
        bool trackRemoved = false;
        if (track.Keys.Count == 0) { c.Tracks.Remove(track); trackRemoved = true; }

        _bus.Publish(new AnimationKeyframeChangedEvent
        {
            NodeId      = nodeId,
            OwnerNodeId = owner,
            Frame       = frame,
            Change      = KeyframeChange.Removed
        });
        if (trackRemoved)
            _bus.Publish(new AnimationContainerChangedEvent
                { OwnerNodeId = owner, Change = ContainerChange.TracksChanged });
        RequestSave();
        RebuildActiveClips();
```

- [ ] **Step 4: Scrub preview** — drop the `IsPlaying` guard in `OnFrameChanged` so the pose updates on scrub/seek too. Replace:
```csharp
    private void OnFrameChanged(FrameChangedEvent e)
    {
        if (_data == null) return;
        ApplyFrame(e.Frame);
    }
```
(`ApplyFrame` already returns early when there is no active owner.)

- [ ] **Step 5: Run, verify PASS.**

- [ ] **Step 6: Checkpoint.** No git.

---

## Task 4: Authoring — background loop runtime (ITickable)

**Files:**
- Modify: `Assets/_App/Scripts/Animation/AnimationAuthoring.cs`
- Test: `Assets/_App/Tests/Animation/AnimationAuthoringLoopTests.cs` (new)

**Context:** `AnimationAuthoring` is registered via `RegisterEntryPoint` (VContainer wires every lifecycle interface), so simply adding `ITickable` makes VContainer call `Tick()` — no DI change. The clip builder is `BuildClip(track, fps, mode)` (Task 2). `ApplyFrame` and `Dispose` exist.

- [ ] **Step 1: Failing tests** — `Assets/_App/Tests/Animation/AnimationAuthoringLoopTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;

public class AnimationAuthoringLoopTests
{
    private AnimationAuthoring NewAuthoring(out EventBus bus)
    {
        bus = new EventBus();
        var a = new AnimationAuthoring(null, null, null, null, bus);
        a.InitForTest();
        return a;
    }

    [Test]
    public void SetLoop_TogglesFlag_AndPublishesLoopChanged()
    {
        var a = NewAuthoring(out var bus);
        a.CreateContainer("obj", 60, 24);

        ContainerChange? change = null;
        bus.Subscribe<AnimationContainerChangedEvent>(e => change = e.Change);

        a.SetLoop("obj", true);
        Assert.IsTrue(a.IsLooping("obj"));
        Assert.AreEqual(ContainerChange.LoopChanged, change);
    }

    [Test]
    public void StartLoopPlayback_RequiresLoopFlag_AndStopStopsIt()
    {
        var a = NewAuthoring(out _);
        a.CreateContainer("obj", 60, 24);

        a.StartLoopPlayback("obj", 0);
        Assert.IsFalse(a.IsLoopPlaying("obj"), "no playback without the Loop flag");

        a.SetLoop("obj", true);
        a.StartLoopPlayback("obj", 0);
        Assert.IsTrue(a.IsLoopPlaying("obj"));

        a.StopLoopPlayback("obj");
        Assert.IsFalse(a.IsLoopPlaying("obj"));
    }

    [Test]
    public void SetLoop_False_StopsPlayback()
    {
        var a = NewAuthoring(out _);
        a.CreateContainer("obj", 60, 24);
        a.SetLoop("obj", true);
        a.StartLoopPlayback("obj", 0);
        Assert.IsTrue(a.IsLoopPlaying("obj"));

        a.SetLoop("obj", false);
        Assert.IsFalse(a.IsLoopPlaying("obj"));
    }

    [Test]
    public void AdvanceLoopCursor_WrapsPastTotal()
    {
        Assert.AreEqual(2f, AnimationAuthoring.AdvanceLoopCursor(58f, 4f, 60), 0.001f);
        Assert.AreEqual(0f, AnimationAuthoring.AdvanceLoopCursor(0f, 0f, 0),  0.001f);
    }
```

- [ ] **Step 2: Run, verify FAIL** — missing loop API.

- [ ] **Step 3: Implement** in `AnimationAuthoring.cs`:

(a) Add `ITickable` to the class declaration:
```csharp
public class AnimationAuthoring : IStartable, ITickable, IDisposable
```
(`VContainer.Unity` is already imported.)

(b) Add loop state next to `_clips`:
```csharp
    private readonly Dictionary<string, float> _loopCursors = new();
    private readonly Dictionary<string, Dictionary<string, AnimationClip>> _loopClips = new();
```

(c) Add the loop API (place near `SetInterpolation`):
```csharp
    public bool IsLooping(string ownerNodeId) => _data?.FindByOwner(ownerNodeId)?.Loop ?? false;

    public bool IsLoopPlaying(string ownerNodeId) => _loopCursors.ContainsKey(ownerNodeId);

    public void SetLoop(string ownerNodeId, bool loop)
    {
        var c = _data?.FindByOwner(ownerNodeId);
        if (c == null) return;
        c.Loop = loop;
        if (!loop) StopLoopPlayback(ownerNodeId);
        _bus.Publish(new AnimationContainerChangedEvent
        {
            OwnerNodeId = ownerNodeId,
            Change      = ContainerChange.LoopChanged
        });
        RequestSave();
    }

    public void StartLoopPlayback(string ownerNodeId, int startFrame)
    {
        var c = _data?.FindByOwner(ownerNodeId);
        if (c == null || !c.Loop) return;
        var clips = new Dictionary<string, AnimationClip>();
        foreach (var t in c.Tracks) clips[t.NodeId] = BuildClip(t, GetSceneFps(), c.Interpolation);
        _loopClips[ownerNodeId]   = clips;
        _loopCursors[ownerNodeId] = Mathf.Clamp(startFrame, 0, c.TotalFrames);
    }

    public void StopLoopPlayback(string ownerNodeId)
    {
        _loopCursors.Remove(ownerNodeId);
        _loopClips.Remove(ownerNodeId);
    }

    internal static float AdvanceLoopCursor(float cursor, float deltaFrames, int total)
    {
        if (total <= 0) return 0f;
        float c = cursor + deltaFrames;
        while (c >= total) c -= total;
        if (c < 0f) c = 0f;
        return c;
    }
```

(d) Add `Tick()` and the sampling helper (place near `ApplyFrame`):
```csharp
    public void Tick()
    {
        if (_data == null || _loopCursors.Count == 0) return;
        float fps = GetSceneFps();
        foreach (var owner in new List<string>(_loopCursors.Keys)) // snapshot: StopLoopPlayback mutates
        {
            var c = _data.FindByOwner(owner);
            if (c == null || !c.Loop) { StopLoopPlayback(owner); continue; }
            float cursor = AdvanceLoopCursor(_loopCursors[owner], Time.deltaTime * fps, c.TotalFrames);
            _loopCursors[owner] = cursor;
            if (_loopClips.TryGetValue(owner, out var clips))
                SampleContainerAt(c, clips, cursor / Mathf.Max(1f, fps));
        }
    }

    private void SampleContainerAt(ActionContainer c, Dictionary<string, AnimationClip> clips, float t)
    {
        foreach (var track in c.Tracks)
        {
            if (!clips.TryGetValue(track.NodeId, out var clip)) continue;
            var go = _sceneGraph?.GetNode(track.NodeId);
            if (go == null) continue;
            clip.SampleAnimation(go, t);
        }
    }
```

(e) In `ApplyFrame`, skip an owner that is currently loop-playing (add at the very top, after the empty-owner guard):
```csharp
    private void ApplyFrame(int frame)
    {
        if (string.IsNullOrEmpty(_activeContainerOwner)) return;
        if (_loopCursors.ContainsKey(_activeContainerOwner)) return; // background loop owns sampling
        var c = _data?.FindByOwner(_activeContainerOwner);
        ...
    }
```

(f) In `Dispose()`, clear the loop state (add before the `_saveCts` lines):
```csharp
        _loopCursors.Clear();
        _loopClips.Clear();
```

- [ ] **Step 4: Run, verify PASS.**

- [ ] **Step 5: Checkpoint.** No git.

---

## Task 5: Toolbar — interpolation text button

**Files:**
- Modify: `Assets/_App/Scripts/SpatialUi/Panels/AnimatorSubToolbar.cs`

- [ ] **Step 1: Implement** — add the serialized field pair, the action, the Awake wiring, and the setter. In `AnimatorSubToolbar.cs`:

Add fields (next to `_removeAnimationButton`):
```csharp
    [SerializeField] private Button   _interpolationButton;
    [SerializeField] private TMP_Text _interpolationLabel;
```
Add the action (next to `OnRemoveAnimation`):
```csharp
    public Action OnToggleInterpolation;
```
In `Awake`, wire it (next to the other AddListener calls):
```csharp
        _interpolationButton?.onClick.AddListener(() => OnToggleInterpolation?.Invoke());
```
Add the setter (next to `SetPasteInteractable`):
```csharp
    public void SetInterpolationLabel(string text)
    {
        if (_interpolationLabel != null) _interpolationLabel.text = text;
    }
```

- [ ] **Step 2: Checkpoint** — compile clean (no test; this is additive UI plumbing). No git.

---

## Task 6: Panel — rig fix, selected-track keying, interpolation wiring

**Files:**
- Modify: `Assets/_App/Scripts/SpatialUi/Panels/AnimatorPanel.cs`

(Do NOT touch the clock or the mode button here — that is Task 7. The panel must stay compiling: `OnToggleModeClicked`/`OnPlayPauseClicked` keep their current clock-based bodies until Task 7.)

- [ ] **Step 1: Rig owner-track fix** — replace `OnAddAnimationClicked`:
```csharp
    private void OnAddAnimationClicked()
    {
        if (_ctx.Authoring == null) return;
        var selected = _ctx.Selection?.SelectedNodeId;
        var owner = AnimationAuthoring.OwnerOf(selected);
        if (string.IsNullOrEmpty(owner)) return;
        _ctx.Authoring.CreateContainer(owner, _config.DefaultTotalFrames, _config.DefaultFps);

        bool isBone = selected != null && selected.StartsWith("bone:");
        if (!isBone && owner == selected)
            _ctx.Authoring.EnsureTrack(owner, owner); // owner track for ANY non-bone type, incl. rigs
    }
```

- [ ] **Step 2: Selected-track set/delete** — replace `OnSetKeyClicked` and `OnDeleteKeyClicked`:
```csharp
    private void OnSetKeyClicked()
    {
        if (string.IsNullOrEmpty(_activeOwner)) return;
        var target = _ctx.Selection?.SelectedNodeId ?? _activeOwner;
        _ctx.Authoring.SetKey(target, _ctx.Clock.CurrentFrame); // keys only the selected track
        RefreshKeyButtonStates();
    }

    private void OnDeleteKeyClicked()
    {
        if (string.IsNullOrEmpty(_activeOwner)) return;
        var target = _ctx.Selection?.SelectedNodeId ?? _activeOwner;
        _ctx.Authoring.DeleteKey(target, _ctx.Clock.CurrentFrame); // removes only the selected track's key
    }
```

- [ ] **Step 3: Interpolation wiring** — in `WireToolbar`, append:
```csharp
        _toolbar.OnToggleInterpolation = OnToggleInterpolationClicked;
```
Add the handler (next to `OnRemoveAnimationClicked`):
```csharp
    private void OnToggleInterpolationClicked()
    {
        if (string.IsNullOrEmpty(_activeOwner)) return;
        var cur  = _ctx.Authoring.GetInterpolation(_activeOwner);
        var next = cur == InterpolationMode.Stepped ? InterpolationMode.Linear : InterpolationMode.Stepped;
        _ctx.Authoring.SetInterpolation(_activeOwner, next);
        _toolbar.SetInterpolationLabel(next.ToString());
        _ctx.Clock.Seek(_ctx.Clock.CurrentFrame); // re-fire FrameChanged → re-sample with new tangents
    }
```

- [ ] **Step 4: Show current interpolation on (re)bind** — in `ApplyContainerToClock`, after the existing `_toolbar` block, add:
```csharp
        _toolbar?.SetInterpolationLabel(_ctx.Authoring.GetInterpolation(_activeOwner).ToString());
```

- [ ] **Step 5: Checkpoint** — compile clean; existing EditMode tests still green (allowed pre-existing failures only). No git.

---

## Task 7: Clock single-shot + panel loop rewire

**Files:**
- Modify: `Assets/_App/Scripts/Animation/AnimationClock.cs`
- Delete: `Assets/_App/Scripts/Animation/AnimationPlayMode.cs` (+ its `.meta`)
- Modify: `Assets/_App/Tests/Animation/AnimationClockTests.cs`
- Modify: `Assets/_App/Scripts/SpatialUi/Panels/AnimatorPanel.cs`

These changes are compile-coupled (removing `PlayMode` breaks the panel's current mode-button wiring), so they land together.

- [ ] **Step 1: Update the clock tests first** — in `AnimationClockTests.cs`, **delete** `DefaultPlayMode_IsOnce` and `AdvanceFrame_Loop_AtEnd_WrapsToZero_AndKeepsPlaying`. Keep `AdvanceFrame_Once_AtEnd_StopsAndRewindsToZero_AndFlagsCompleted` and `AdvanceFrame_MidRange_SetsCurrentFrame` unchanged (Once is now the only behavior; neither calls `SetPlayMode`).

- [ ] **Step 2: Run, verify FAIL** — the file still compiles only after Step 3 (the two kept tests reference no removed members, but the deleted ones must be gone). Run to confirm the suite still builds and the two kept clock tests are present.

- [ ] **Step 3: Simplify the clock** — in `AnimationClock.cs`, remove the `PlayMode` property and `SetPlayMode`, and make `AdvanceFrame` always single-shot:
```csharp
    internal void AdvanceFrame(int next)
    {
        if (next >= TotalFrames)
        {
            IsPlaying    = false;
            CurrentFrame = 0;
            _accumulated = 0f;
            _bus.Publish(new FrameChangedEvent         { Frame = 0 });
            _bus.Publish(new PlaybackStateChangedEvent { IsPlaying = false, Frame = 0, Completed = true });
            return;
        }

        CurrentFrame = next;
        _bus.Publish(new FrameChangedEvent         { Frame     = CurrentFrame });
        _bus.Publish(new PlaybackStateChangedEvent { IsPlaying = IsPlaying, Frame = CurrentFrame });
    }
```
Delete the two lines:
```csharp
    public AnimationPlayMode PlayMode { get; private set; } = AnimationPlayMode.Once;
    public void SetPlayMode(AnimationPlayMode mode) => PlayMode = mode;
```
Leave `Tick`/`Play`/`Pause`/`Stop`/`Seek`/`Configure` otherwise unchanged.

- [ ] **Step 4: Delete the enum** — remove `Assets/_App/Scripts/Animation/AnimationPlayMode.cs` and its `.meta` (use the Unity MCP `delete_script` tool or delete both files).

- [ ] **Step 5: Rewire the panel for per-object loop** — in `AnimatorPanel.cs`:

`WireTransport` — change the `SetMode` initialiser line from the clock-based one to the loop-flag one:
```csharp
        _transport.OnToggleMode = OnToggleModeClicked;
        _transport.SetMode(!string.IsNullOrEmpty(_activeOwner) && _ctx.Authoring != null && _ctx.Authoring.IsLooping(_activeOwner));
```
Replace `OnToggleModeClicked`:
```csharp
    private void OnToggleModeClicked()
    {
        if (string.IsNullOrEmpty(_activeOwner) || _ctx.Authoring == null) return;
        bool next = !_ctx.Authoring.IsLooping(_activeOwner);
        _ctx.Authoring.SetLoop(_activeOwner, next);
        _transport?.SetMode(next);
    }
```
Replace `OnPlayPauseClicked`:
```csharp
    private void OnPlayPauseClicked()
    {
        if (_ctx.Clock == null) return;
        if (!string.IsNullOrEmpty(_activeOwner) && _ctx.Authoring != null && _ctx.Authoring.IsLooping(_activeOwner))
        {
            if (_ctx.Authoring.IsLoopPlaying(_activeOwner)) _ctx.Authoring.StopLoopPlayback(_activeOwner);
            else                                            _ctx.Authoring.StartLoopPlayback(_activeOwner, _ctx.Clock.CurrentFrame);
            _transport?.SetPlaying(_ctx.Authoring.IsLoopPlaying(_activeOwner));
            return;
        }
        if (_ctx.Clock.IsPlaying) _ctx.Clock.Pause(); else _ctx.Clock.Play();
    }
```
In `ApplyContainerToClock`, reflect the loop state on the mode + play icons (add after the interpolation-label line from Task 6):
```csharp
        _transport?.SetMode(_ctx.Authoring.IsLooping(_activeOwner));
        _transport?.SetPlaying(_ctx.Authoring.IsLoopPlaying(_activeOwner) || _ctx.Clock.IsPlaying);
```

- [ ] **Step 6: Run, verify** — full EditMode suite: the two kept clock tests pass, all Task 1–4 tests pass, only the allowed pre-existing failures remain.

- [ ] **Step 7: Checkpoint.** No git.

---

## Task 8: Prefab — interpolation button (in-headset)

**Files:**
- Modify: `Assets/_App/Content/Prefabs/UI/Panels/UserPanel/AnimatorPanelModule.prefab`

Prefab work via Unity-MCP, then in-headset verification. (The Loop mode button already exists from the prior round; this task adds only the interpolation button.)

- [ ] **Step 1: Add the interpolation button** — in the animator **toolbar** row (the `HorizontalLayoutGroup` that holds "+ key" / "− key" / fps), add a `Button` with a child `TMP_Text` label (duplicate an existing toolbar text button so it inherits layout). On the `AnimatorSubToolbar` component, wire `_interpolationButton` → the new Button and `_interpolationLabel` → its child `TMP_Text`. Set the label's default text to `Linear`.

- [ ] **Step 2: Verify wiring by reading the prefab YAML** — confirm on the `AnimatorSubToolbar` component block that `_interpolationButton` and `_interpolationLabel` reference non-zero fileIDs, and that those fileIDs resolve to a `Button` (guid `4e29b1a8efbd4b44bb3f3716e73f07ff`) and a `TextMeshProUGUI` respectively. Quote the lines.

- [ ] **Step 3: Compile check** — `refresh_unity` + `read_console` (no `CS####`). Then hand to the user for in-headset verification:
  1. Add animation on a **rig** → an owner track row appears immediately; key bones → their rows appear live.
  2. Select one track → "+ key" keys only it; "− key" removes only its key; emptying a track removes its row live.
  3. Interpolation button flips **Linear/Stepped**; Stepped holds the previous pose between keys, Linear blends.
  4. Scrub between keys (not playing) → the object shows the interpolated pose.
  5. Mode (loop) button → mark object A's animation **Loop** → Play → A loops; select B and edit it → A keeps looping; re-select A and Pause (or toggle Loop off) → A stops.

- [ ] **Step 4:** Report results. No git.

---

## Self-Review (by the author)

**Spec coverage:**
- Rig bug (owner track on any type) → Task 6 Step 1. ✓
- F1 selected-track + "+ key"/"− key" → Task 6 Step 2 (+ Task 3 live track-removal). ✓
- F2 interpolation per block + text button → Task 1 (data) + Task 2 (API/tangents) + Task 5 (toolbar) + Task 6 (wiring) + Task 8 (prefab). ✓
- F3 scrub preview → Task 3 Step 4. ✓
- Per-object Loop + background playback → Task 1 (Loop field) + Task 4 (runtime) + Task 7 (clock simplification + panel rewire). ✓

**Placeholder scan:** No TBD/"handle edge cases"/"similar to". All code is shown. ✓

**Type consistency:** `InterpolationMode {Linear,Stepped}`; `ContainerChange.InterpolationChanged/LoopChanged`; `ActionContainer.Interpolation/Loop`; `AnimationAuthoring.GetInterpolation/SetInterpolation/ApplyInterpolation/BuildClip/IsLooping/SetLoop/IsLoopPlaying/StartLoopPlayback/StopLoopPlayback/AdvanceLoopCursor/SampleContainerAt/Tick`; `AnimatorSubToolbar._interpolationButton/_interpolationLabel/OnToggleInterpolation/SetInterpolationLabel`; `AnimatorPanel.OnToggleInterpolationClicked`; clock `AdvanceFrame` (single-shot), `PlayMode`/`SetPlayMode`/`AnimationPlayMode` removed — consistent across tasks. ✓

**Compile-coupling:** the only removal that breaks an existing caller (`AnimationClock.PlayMode` used by the panel mode wiring) is bundled with that panel rewire in Task 7. Tasks 1–6 are additive and each compiles. ✓
