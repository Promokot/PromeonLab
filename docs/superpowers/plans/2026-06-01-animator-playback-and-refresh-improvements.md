# Animator — Playback Modes, Live Refresh, Scene FPS & Config-Driven Metrics — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make animator tracks refresh live, input fields apply in VR, FPS scene-wide, timeline metrics config-driven, and add Once/Loop playback with a first-keyframe reset.

**Architecture:** All changes live in the `Animation` subsystem (`AnimationClock`, `AnimationAuthoring`, `SceneAnimationData`, events) and the `SpatialUi` animator UI (`AnimatorPanel`, `AnimatorSubToolbar`, `AnimatorSubTransport`, `AnimatorSubRuler`, `TimelineRow`, `AnimatorPanelConfig`) plus the shared `VrKeyboard` commit path. No new cross-subsystem events except one struct field and one enum.

**Tech Stack:** Unity 6000.3.7f1, C# (no namespaces for runtime), VContainer, custom `EventBus`, NUnit EditMode tests in `_App.Tests`, Unity-MCP for compile/test verification.

---

## Conventions for every task (READ FIRST)

- **NO GIT.** The user commits manually. Never run any `git` command. Where a normal plan says "commit", this plan says **Checkpoint** instead.
- **Checkpoint (run after each task's code is written):**
  1. `refresh_unity(mode="force", scope="all", compile="request", wait_for_ready=true)`
  2. `read_console(action="get", types=["error"], count="30")` → must show **no `CS####`** errors. Acceptable noise: `MCP-FOR-UNITY: Client handler exited`, and (only after closing a prefab stage) `SerializedObjectNotCreatableException` / `MissingReferenceException`.
  3. `run_tests(mode="EditMode")` then poll `get_test_job` → the task's new tests pass; the only pre-existing failures allowed are `PathProviderTests` ×4 and `RingRotateStrategyTests` ×2 (unrelated, documented). **No other failures.**
- **Tests** are plain NUnit classes in `Assets/_App/Tests/Animation/` (one class per file, no namespace). Construct services directly: `new AnimationClock(new EventBus())`, `new AnimationAuthoring(null, null, null, null, new EventBus())` + `authoring.InitForTest()` (the debounced save is a no-op when `_sceneId` is null, so null `paths`/`storage` are safe).
- **One public type per file**, file name == type name.
- Private fields `_camelCase`; `[SerializeField] private` for inspector fields; plain `[Serializable]` JSON data classes may use public fields.

---

## File Structure

| File | Responsibility | Task |
|---|---|---|
| `Scripts/Animation/AnimationPlayMode.cs` | **new** enum `{ Once, Loop }` | 1 |
| `Scripts/Animation/Events/PlaybackStateChangedEvent.cs` | add `bool Completed` | 1 |
| `Scripts/Animation/AnimationClock.cs` | `PlayMode`, `SetPlayMode`, `AdvanceFrame` with loop/once | 1 |
| `Scripts/Animation/SceneAnimationData.cs` | add scene-wide `int Fps` | 2 |
| `Scripts/Animation/AnimationAuthoring.cs` | scene fps API + use; new-track event; frame-0 on completion | 3,4,5 |
| `Scripts/SpatialUi/AnimatorPanelConfig.cs` | new metric fields | 6 |
| `Scripts/SpatialUi/Elements/TimelineRow.cs` | key size + row height from config | 6 |
| `Scripts/SpatialUi/Panels/AnimatorSubRuler.cs` | tick heights from config | 6 |
| `Scripts/SpatialUi/VrKeyboard.cs` | fire `onEndEdit` on submit/focus-switch | 7 |
| `Scripts/SpatialUi/Panels/AnimatorSubTransport.cs` | mode button + icon swap | 8 |
| `Scripts/SpatialUi/Panels/AnimatorPanel.cs` | scene-fps wiring, toggle-mode, rename rebuild | 8 |
| `Content/Prefabs/UI/Panels/UserPanel/AnimatorPanelModule.prefab` | add+wire mode button; verify field proxies | 9 |
| `Tests/Animation/*` | unit tests per task | 1–6 |

---

## Task 1: Once/Loop playback in AnimationClock

**Files:**
- Create: `Assets/_App/Scripts/Animation/AnimationPlayMode.cs`
- Modify: `Assets/_App/Scripts/Animation/Events/PlaybackStateChangedEvent.cs`
- Modify: `Assets/_App/Scripts/Animation/AnimationClock.cs`
- Test: `Assets/_App/Tests/Animation/AnimationClockTests.cs`

- [ ] **Step 1: Add the enum file**

`Assets/_App/Scripts/Animation/AnimationPlayMode.cs`:
```csharp
public enum AnimationPlayMode { Once, Loop }
```

- [ ] **Step 2: Add `Completed` to the playback event**

`Assets/_App/Scripts/Animation/Events/PlaybackStateChangedEvent.cs` (full file):
```csharp
public struct PlaybackStateChangedEvent { public bool IsPlaying; public int Frame; public bool Completed; }
```

- [ ] **Step 3: Write failing tests** — append to `AnimationClockTests.cs` (inside the class):
```csharp
    [Test]
    public void DefaultPlayMode_IsOnce()
    {
        Assert.AreEqual(AnimationPlayMode.Once, _sut.PlayMode);
    }

    [Test]
    public void AdvanceFrame_Once_AtEnd_StopsAndRewindsToZero_AndFlagsCompleted()
    {
        bool completed = false;
        _bus.Subscribe<PlaybackStateChangedEvent>(e => { if (e.Completed) completed = true; });
        _sut.Play();
        _sut.AdvanceFrame(_sut.TotalFrames);
        Assert.IsFalse(_sut.IsPlaying, "Once mode stops at the end");
        Assert.AreEqual(0, _sut.CurrentFrame, "playhead rewinds to 0");
        Assert.IsTrue(completed, "a Completed event is published");
    }

    [Test]
    public void AdvanceFrame_Loop_AtEnd_WrapsToZero_AndKeepsPlaying()
    {
        _sut.SetPlayMode(AnimationPlayMode.Loop);
        _sut.Play();
        _sut.AdvanceFrame(_sut.TotalFrames);
        Assert.IsTrue(_sut.IsPlaying, "Loop keeps playing");
        Assert.AreEqual(0, _sut.CurrentFrame, "wraps to 0");
    }

    [Test]
    public void AdvanceFrame_MidRange_SetsCurrentFrame()
    {
        _sut.Play();
        _sut.AdvanceFrame(7);
        Assert.AreEqual(7, _sut.CurrentFrame);
        Assert.IsTrue(_sut.IsPlaying);
    }
```

- [ ] **Step 4: Run tests, verify they fail** — `run_tests(mode="EditMode")`; expect compile error / missing `PlayMode`,`SetPlayMode`,`AdvanceFrame`.

- [ ] **Step 5: Implement** — edit `AnimationClock.cs`. Add fields/props after `IsPlaying`:
```csharp
    public AnimationPlayMode PlayMode { get; private set; } = AnimationPlayMode.Once;
    public void SetPlayMode(AnimationPlayMode mode) => PlayMode = mode;
```
Replace `Tick()` body so it delegates to `AdvanceFrame`:
```csharp
    public void Tick()
    {
        if (!IsPlaying) return;
        _accumulated += Time.deltaTime * Fps;
        var next = Mathf.FloorToInt(_accumulated);
        if (next == CurrentFrame) return;
        AdvanceFrame(next);
    }

    internal void AdvanceFrame(int next)
    {
        if (next >= TotalFrames)
        {
            if (PlayMode == AnimationPlayMode.Loop)
            {
                CurrentFrame = 0;
                _accumulated = 0f;
                _bus.Publish(new FrameChangedEvent { Frame = 0 });
                return;
            }

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
(Leave `Play`/`Pause`/`Stop`/`Seek`/`Configure` unchanged. `Play` still rewinds when `CurrentFrame >= TotalFrames`, which now also covers re-playing after an Once completion.)

- [ ] **Step 6: Run tests, verify pass** — `run_tests(mode="EditMode")`; the 4 new clock tests pass.

- [ ] **Step 7: Checkpoint** (refresh → console clean → tests). **No git.**

---

## Task 2: Scene-wide FPS field on SceneAnimationData

**Files:**
- Modify: `Assets/_App/Scripts/Animation/SceneAnimationData.cs`
- Test: `Assets/_App/Tests/Animation/AnimationDataTests.cs`

- [ ] **Step 1: Write failing test** — append inside `AnimationDataTests`:
```csharp
    [Test]
    public void SceneAnimationData_DefaultFps_Is24()
    {
        Assert.AreEqual(24, new SceneAnimationData().Fps);
    }

    [Test]
    public void SceneAnimationData_Fps_RoundTrips_AndKeepsSchemaV2()
    {
        var data = new SceneAnimationData { Fps = 48 };
        var json   = UnityEngine.JsonUtility.ToJson(data);
        var loaded = UnityEngine.JsonUtility.FromJson<SceneAnimationData>(json);
        Assert.AreEqual(48, loaded.Fps);
        Assert.AreEqual(2,  loaded.schemaVersion);
    }
```

- [ ] **Step 2: Run, verify fail** — `run_tests(mode="EditMode")`; expect `Fps` not found.

- [ ] **Step 3: Implement** — add the field to `SceneAnimationData.cs` after `schemaVersion`:
```csharp
    public int                   schemaVersion = 2;
    public int                   Fps           = 24;
    public List<ActionContainer> Containers    = new();
```

- [ ] **Step 4: Run, verify pass.**

- [ ] **Step 5: Checkpoint.** No git.

---

## Task 3: Scene FPS used everywhere in AnimationAuthoring

**Files:**
- Modify: `Assets/_App/Scripts/Animation/AnimationAuthoring.cs`
- Test: `Assets/_App/Tests/Animation/AnimationAuthoringSceneFpsTests.cs` (new)

- [ ] **Step 1: Write failing tests** — `Assets/_App/Tests/Animation/AnimationAuthoringSceneFpsTests.cs`:
```csharp
using NUnit.Framework;

public class AnimationAuthoringSceneFpsTests
{
    private AnimationAuthoring NewAuthoring(out EventBus bus)
    {
        bus = new EventBus();
        var a = new AnimationAuthoring(null, null, null, null, bus);
        a.InitForTest();
        return a;
    }

    [Test]
    public void GetSceneFps_DefaultsTo24()
    {
        var a = NewAuthoring(out _);
        Assert.AreEqual(24, a.GetSceneFps());
    }

    [Test]
    public void SetSceneFps_UpdatesValue_AndClampsMinimum()
    {
        var a = NewAuthoring(out _);
        a.SetSceneFps(48);
        Assert.AreEqual(48, a.GetSceneFps());
        a.SetSceneFps(0);
        Assert.AreEqual(1, a.GetSceneFps());
    }

    [Test]
    public void SetSceneFps_PublishesFpsChanged_ForActiveOwner()
    {
        var a = NewAuthoring(out var bus);
        a.CreateContainer("obj1", 60, 24);
        a.SetActiveContainerOwner("obj1");

        ContainerChange? change = null;
        bus.Subscribe<AnimationContainerChangedEvent>(e => change = e.Change);

        a.SetSceneFps(30);
        Assert.AreEqual(ContainerChange.FpsChanged, change);
    }
}
```

- [ ] **Step 2: Run, verify fail** — missing `GetSceneFps`/`SetSceneFps`.

- [ ] **Step 3: Implement** — in `AnimationAuthoring.cs`:

(a) Add the public API (place near `SetFps`):
```csharp
    public int GetSceneFps() => _data?.Fps ?? 24;

    public void SetSceneFps(int fps)
    {
        EnsureData();
        _data.Fps = Mathf.Max(1, fps);
        if (!string.IsNullOrEmpty(_activeContainerOwner))
            _bus.Publish(new AnimationContainerChangedEvent
            {
                OwnerNodeId = _activeContainerOwner,
                Change      = ContainerChange.FpsChanged
            });
        RequestSave();
        RebuildActiveClips();
    }
```

(b) Use scene fps when building clips — change `RebuildActiveClips`:
```csharp
        foreach (var t in c.Tracks) RebuildClip(t, GetSceneFps());
```

(c) Use scene fps when sampling — change `ApplyFrame` so `t` uses scene fps:
```csharp
    private void ApplyFrame(int frame)
    {
        if (string.IsNullOrEmpty(_activeContainerOwner)) return;
        var c = _data?.FindByOwner(_activeContainerOwner);
        if (c == null) return;
        int fps = GetSceneFps();
        if (fps <= 0) return;

        float t = (float)frame / fps;
        foreach (var track in c.Tracks)
        {
            if (!_clips.TryGetValue(track.NodeId, out var clip)) continue;
            var go = _sceneGraph?.GetNode(track.NodeId);
            if (go == null) continue;
            clip.SampleAnimation(go, t);
        }
    }
```

(d) Load-normalize — in `LoadAsync`, right before the final `_data = loaded;` (the success path, after the `schemaVersion > 2` guard) make fps sane:
```csharp
            if (loaded.Fps <= 0)
                loaded.Fps = loaded.Containers.Count > 0 ? Mathf.Max(1, loaded.Containers[0].Fps) : 24;

            _data = loaded;
```

- [ ] **Step 4: Run, verify pass.**

- [ ] **Step 5: Checkpoint.** No git.

---

## Task 4: New track appears live (no reopen)

**Files:**
- Modify: `Assets/_App/Scripts/Animation/AnimationAuthoring.cs`
- Test: `Assets/_App/Tests/Animation/AnimationAuthoringLiveTrackTests.cs` (new)

- [ ] **Step 1: Write failing tests** — `Assets/_App/Tests/Animation/AnimationAuthoringLiveTrackTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;

public class AnimationAuthoringLiveTrackTests
{
    private AnimationAuthoring NewAuthoring(out EventBus bus)
    {
        bus = new EventBus();
        var a = new AnimationAuthoring(null, null, null, null, bus);
        a.InitForTest();
        return a;
    }

    [Test]
    public void SetKey_OnNewTrack_PublishesTracksChanged()
    {
        var a = NewAuthoring(out var bus);
        a.CreateContainer("obj1", 60, 24);

        bool tracksChanged = false;
        bus.Subscribe<AnimationContainerChangedEvent>(e =>
        { if (e.Change == ContainerChange.TracksChanged) tracksChanged = true; });

        a.SetKey("obj1", 5, Vector3.zero, Quaternion.identity, Vector3.one);
        Assert.IsTrue(tracksChanged, "first key on a new track must announce TracksChanged");
    }

    [Test]
    public void SetKey_OnExistingTrack_DoesNotRepublishTracksChanged()
    {
        var a = NewAuthoring(out var bus);
        a.CreateContainer("obj1", 60, 24);
        a.SetKey("obj1", 5, Vector3.zero, Quaternion.identity, Vector3.one);

        int tracksChangedCount = 0;
        bus.Subscribe<AnimationContainerChangedEvent>(e =>
        { if (e.Change == ContainerChange.TracksChanged) tracksChangedCount++; });

        a.SetKey("obj1", 10, Vector3.one, Quaternion.identity, Vector3.one);
        Assert.AreEqual(0, tracksChangedCount, "adding a key to an existing track must not announce TracksChanged");
    }
}
```

- [ ] **Step 2: Run, verify fail** — both expectations fail (no `TracksChanged` currently published by `SetKey`).

- [ ] **Step 3: Implement** — in `AnimationAuthoring.SetKey(string nodeId, int frame, Vector3 pos, Quaternion rot, Vector3 scale)`, detect a brand-new track before `GetOrCreateTrack`:
```csharp
        var c = _data.FindByOwner(owner);
        if (c == null) return;

        bool trackIsNew = c.FindTrack(nodeId) == null;
        var track       = c.GetOrCreateTrack(nodeId);
        bool existed    = track.HasKey(frame);
        track.UpsertKey(frame, pos, rot, scale);

        if (trackIsNew)
            _bus.Publish(new AnimationContainerChangedEvent
            {
                OwnerNodeId = owner,
                Change      = ContainerChange.TracksChanged
            });

        _bus.Publish(new AnimationKeyframeChangedEvent
        {
            NodeId      = nodeId,
            OwnerNodeId = owner,
            Frame       = frame,
            Change      = existed ? KeyframeChange.Overwritten : KeyframeChange.Added
        });
        RequestSave();
        RebuildActiveClips();
```

Apply the same new-track detection in `PasteFrame` (it also calls `GetOrCreateTrack`): inside the `foreach (var e in clip.Entries)` loop, before `GetOrCreateTrack`:
```csharp
        foreach (var e in clip.Entries)
        {
            bool trackIsNew = c.FindTrack(e.TrackNodeId) == null;
            var track       = c.GetOrCreateTrack(e.TrackNodeId);
            bool existed    = track.HasKey(frame);
            track.UpsertKey(frame, e.Position, e.Rotation, e.Scale);
            if (trackIsNew)
                _bus.Publish(new AnimationContainerChangedEvent
                    { OwnerNodeId = ownerNodeId, Change = ContainerChange.TracksChanged });
            _bus.Publish(new AnimationKeyframeChangedEvent
            {
                NodeId      = e.TrackNodeId,
                OwnerNodeId = ownerNodeId,
                Frame       = frame,
                Change      = existed ? KeyframeChange.Overwritten : KeyframeChange.Added
            });
        }
```

- [ ] **Step 4: Run, verify pass.**

- [ ] **Step 5: Checkpoint.** No git. (The panel already maps `TracksChanged → RebuildTimeline`, so no panel change is needed for the row to appear.)

---

## Task 5: First-keyframe pose on Once completion (4.1)

**Files:**
- Modify: `Assets/_App/Scripts/Animation/AnimationAuthoring.cs`
- Test: `Assets/_App/Tests/Animation/AnimationAuthoringCompletionTests.cs` (new)

- [ ] **Step 1: Write failing test** — `Assets/_App/Tests/Animation/AnimationAuthoringCompletionTests.cs`. Uses a fake `ISceneGraph` returning a real GameObject; on `Completed` the authoring must sample frame 0, and because the AnimationCurve holds the first key value before it, the GameObject ends at the first key's pose:
```csharp
using NUnit.Framework;
using UnityEngine;

public class AnimationAuthoringCompletionTests
{
    private class FakeGraph : ISceneGraph
    {
        private readonly GameObject _go;
        public FakeGraph(GameObject go) => _go = go;
        public GameObject GetNode(string nodeId) => _go;
    }

    [Test]
    public void Completed_SamplesFrameZero_PosesObjectAtFirstKey()
    {
        var go  = new GameObject("obj1");
        go.transform.localPosition = new Vector3(99, 99, 99); // arbitrary "current" pose

        var bus = new EventBus();
        var a   = new AnimationAuthoring(new AnimationClock(bus), new FakeGraph(go), null, null, bus);
        a.InitForTest();
        a.CreateContainer("obj1", 60, 24);
        // first (and only) key sits at frame 30 with a known pose
        a.SetKey("obj1", 30, new Vector3(1, 2, 3), Quaternion.identity, Vector3.one);
        a.SetActiveContainerOwner("obj1");

        bus.Publish(new PlaybackStateChangedEvent { IsPlaying = false, Frame = 0, Completed = true });

        Assert.AreEqual(1f, go.transform.localPosition.x, 0.001f);
        Assert.AreEqual(2f, go.transform.localPosition.y, 0.001f);
        Assert.AreEqual(3f, go.transform.localPosition.z, 0.001f);

        Object.DestroyImmediate(go);
    }
}
```
> Note: this exercises `AnimationClip.SampleAnimation` on a legacy clip in EditMode. If it proves flaky in the batch test runner, keep the code and demote this single assertion to the in-headset check (Task 9) — but attempt it first.

- [ ] **Step 2: Run, verify fail** — pose unchanged (no Completed handler yet).

- [ ] **Step 3: Implement** — subscribe in `Start()` and add the handler:

In `Start()` add a subscription (next to the existing `FrameChanged` one):
```csharp
        _bus.Subscribe<PlaybackStateChangedEvent>(OnPlaybackState);
```
In `Dispose()` add the matching unsubscribe:
```csharp
        _bus.Unsubscribe<PlaybackStateChangedEvent>(OnPlaybackState);
```
Add the handler near `OnFrameChanged`:
```csharp
    private void OnPlaybackState(PlaybackStateChangedEvent e)
    {
        if (e.Completed) ApplyFrame(0);
    }
```
> The test constructs authoring **without** calling `Start()` (no scene load). To make the handler reachable in the test, also subscribe in a way the test can trigger: the test publishes the event on the same `bus`, and `OnPlaybackState` must be subscribed. Since `Start()` isn't called in EditMode, **subscribe in the constructor instead** is wrong (other tests construct with a bus too, harmless but changes behavior). Resolution: keep the subscribe in `Start()` **and** have the test call the handler path via the bus by subscribing in `InitForTest()`:
```csharp
    internal void InitForTest()
    {
        _data = new SceneAnimationData();
        _bus.Subscribe<PlaybackStateChangedEvent>(OnPlaybackState);
    }
```
This keeps production wiring in `Start()` and gives tests the same subscription via `InitForTest()`. (Runtime never calls `InitForTest()`, so no double-subscribe in production.)

- [ ] **Step 4: Run, verify pass.**

- [ ] **Step 5: Checkpoint.** No git.

---

## Task 6: Config-driven timeline metrics

**Files:**
- Modify: `Assets/_App/Scripts/SpatialUi/AnimatorPanelConfig.cs`
- Modify: `Assets/_App/Scripts/SpatialUi/Elements/TimelineRow.cs`
- Modify: `Assets/_App/Scripts/SpatialUi/Panels/AnimatorSubRuler.cs`
- Test: `Assets/_App/Tests/Animation/AnimatorPanelConfigTests.cs` (new)

- [ ] **Step 1: Write failing test** — `Assets/_App/Tests/Animation/AnimatorPanelConfigTests.cs` (asserts the config exposes the new fields with sane defaults):
```csharp
using NUnit.Framework;
using UnityEngine;

public class AnimatorPanelConfigTests
{
    [Test]
    public void Config_ExposesMetricDefaults()
    {
        var c = ScriptableObject.CreateInstance<AnimatorPanelConfig>();
        Assert.AreEqual(22f, c.KeySize,         0.001f);
        Assert.AreEqual(26f, c.KeySizeSelected, 0.001f);
        Assert.AreEqual(24f, c.MajorTickHeight, 0.001f);
        Assert.AreEqual(16f, c.MinorTickHeight, 0.001f);
        Assert.AreEqual(52f, c.RowHeight,       0.001f);
        Object.DestroyImmediate(c);
    }
}
```

- [ ] **Step 2: Run, verify fail** — fields missing.

- [ ] **Step 3: Implement config** — add to `AnimatorPanelConfig.cs`:
```csharp
    [Header("Key marker sizes")]
    public float KeySize         = 22f;
    public float KeySizeSelected = 26f;

    [Header("Ruler tick sizes")]
    public float MajorTickHeight = 24f;
    public float MinorTickHeight = 16f;

    [Header("Track row height")]
    public float RowHeight = 52f;
```

- [ ] **Step 4: Wire `TimelineRow`** — in `TimelineRow.SetKeys`, replace the hardcoded sizes:
```csharp
            float size = isSel ? _config.KeySizeSelected : _config.KeySize;
            key.sizeDelta = new Vector2(size, size);
```
In `TimelineRow.Bind`, set row height from config (after the `_config != null` block that sets name/keystrip):
```csharp
            var le = GetComponent<LayoutElement>();
            if (le != null) { le.minHeight = _config.RowHeight; le.preferredHeight = _config.RowHeight; }
```
(`UnityEngine.UI.LayoutElement` is already available via `using UnityEngine.UI;` at the top of the file.)

- [ ] **Step 5: Wire `AnimatorSubRuler`** — in `Rebuild`, replace tick heights:
```csharp
            sz.y = major ? _config.MajorTickHeight : _config.MinorTickHeight;
```

- [ ] **Step 6: Run, verify pass** (the config test; the row/ruler changes are compile-checked and verified visually in Task 9).

- [ ] **Step 7: Checkpoint.** No git.

---

## Task 7: VR keyboard commits onEndEdit (fixes input fields, #2)

**Files:**
- Modify: `Assets/_App/Scripts/SpatialUi/VrKeyboard.cs`

**Why:** `VrInputFieldProxy.OnPointerDown` publishes `KeyboardFocusEvent{Target}`; `VrKeyboard.AddLetter` mutates `_target.text` (raises only `onValueChanged`). `onEndEdit` never fires, so the toolbar's `onEndEdit` callbacks never run. Firing `onEndEdit` on submit **and** on focus-switch makes the existing toolbar wiring work, for all numeric fields, without per-field changes. (Live `onValueChanged` apply is deliberately avoided: `SetTotalFrames` calls `TruncateToTotalFrames`, so applying a half-typed "1" while typing "100" would delete keyframes.)

- [ ] **Step 1: Implement** — replace the focus/submit members in `VrKeyboard.cs`:
```csharp
    private void OnFocus(KeyboardFocusEvent e)
    {
        if (_target != null && _target != e.Target)
            _target.onEndEdit?.Invoke(_target.text); // commit the field we are leaving
        _target = e.Target;
    }

    public void SubmitWord()
    {
        if (_target == null) return;
        _target.onEndEdit?.Invoke(_target.text);
        _target = null;
    }
```
(Leave `AddLetter`/`DeleteLetter`/`Start`/`OnDestroy`/`Construct` unchanged.)

- [ ] **Step 2: Manual reasoning check (no unit test — MonoBehaviour + TMP focus is not EditMode-testable here):** `InspectorPanel._nameField` listens to both `onValueChanged` (live rename) and `onEndEdit` (commit). The new focus-switch commit will call `onEndEdit` → `OnNameCommit` once more when leaving the field; renaming to the same name is idempotent, so this is safe.

- [ ] **Step 3: Checkpoint** (compile + console clean). Functional verification is in-headset (Task 9). **No git.**

---

## Task 8: AnimatorPanel & Transport wiring (mode toggle, scene fps, live names)

**Files:**
- Modify: `Assets/_App/Scripts/SpatialUi/Panels/AnimatorSubTransport.cs`
- Modify: `Assets/_App/Scripts/SpatialUi/Panels/AnimatorPanel.cs`

- [ ] **Step 1: Extend `AnimatorSubTransport`** — add fields, action, wiring, and a `SetMode` that swaps the icon (mirrors the existing `SetPlaying`):

Add serialized fields (next to the play/pause ones):
```csharp
    [SerializeField] private Button _modeButton;
    [SerializeField] private Image  _modeIcon;
    [SerializeField] private Sprite _onceSprite;
    [SerializeField] private Sprite _loopSprite;
```
Add the action (next to `OnPlayPause`):
```csharp
    public Action OnToggleMode;
```
In `Awake`, wire it:
```csharp
        _modeButton?.onClick.AddListener(() => OnToggleMode?.Invoke());
```
Add the setter (next to `SetPlaying`):
```csharp
    public void SetMode(bool loop)
    {
        if (_modeIcon == null) return;
        _modeIcon.sprite = loop ? _loopSprite : _onceSprite;
    }
```

- [ ] **Step 2: Wire the toggle in `AnimatorPanel`** — in `WireTransport`, add:
```csharp
        _transport.OnToggleMode = OnToggleModeClicked;
        _transport.SetMode(_ctx.Clock != null && _ctx.Clock.PlayMode == AnimationPlayMode.Loop);
```
Add the handler (next to `OnPlayPauseClicked`):
```csharp
    private void OnToggleModeClicked()
    {
        if (_ctx.Clock == null) return;
        var next = _ctx.Clock.PlayMode == AnimationPlayMode.Loop
            ? AnimationPlayMode.Once
            : AnimationPlayMode.Loop;
        _ctx.Clock.SetPlayMode(next);
        _transport?.SetMode(next == AnimationPlayMode.Loop);
    }
```

- [ ] **Step 3: Route the fps input to scene fps** — in `WireToolbar`, change the fps line:
```csharp
        _toolbar.OnFpsSubmitted = f => _ctx.Authoring?.SetSceneFps(f);
```
In `ApplyContainerToClock`, configure the clock and the fps field from scene fps:
```csharp
    private void ApplyContainerToClock()
    {
        var c = _ctx.Authoring.GetContainer(_activeOwner);
        if (c == null) return;
        int sceneFps = _ctx.Authoring.GetSceneFps();
        _ctx.Clock.Configure(c.TotalFrames, sceneFps);
        if (_toolbar != null)
        {
            _toolbar.SetTotalFrames(c.TotalFrames);
            _toolbar.SetFps(sceneFps);
            _toolbar.SetCurrentFrame(_ctx.Clock.CurrentFrame);
        }
    }
```
Also in `Refresh`, the two `Configure(DefaultTotalFrames, DefaultFps)` calls for the empty/no-container states should use scene fps so the field shows the real scene value. Replace both occurrences of:
```csharp
            _ctx.Clock.Configure(_config.DefaultTotalFrames, _config.DefaultFps);
```
with:
```csharp
            _ctx.Clock.Configure(_config.DefaultTotalFrames, _ctx.Authoring.GetSceneFps());
```

- [ ] **Step 4: Live track-name refresh on rename** — subscribe to `NodeRenamedEvent` and rebuild rows. In `OnEnable` add:
```csharp
        _bus.Subscribe<NodeRenamedEvent>(OnNodeRenamed);
```
In `OnDisable` add:
```csharp
        _bus.Unsubscribe<NodeRenamedEvent>(OnNodeRenamed);
```
Add the handler (next to `OnSelectionChanged`):
```csharp
    private void OnNodeRenamed(NodeRenamedEvent _)
    {
        if (!string.IsNullOrEmpty(_activeOwner)) RebuildTimeline();
    }
```
> Confirm the field name on `NodeRenamedEvent` is not needed here (we rebuild unconditionally for the active owner). If `RebuildTimeline` is cheap enough this is fine; it pools rows.

- [ ] **Step 5: Checkpoint** (compile + console clean; existing EditMode tests still green). Visual wiring verified in Task 9. **No git.**

---

## Task 9: Prefab — add the mode button, wire icons, verify field proxies (in-headset)

**Files:**
- Modify: `Assets/_App/Content/Prefabs/UI/Panels/UserPanel/AnimatorPanelModule.prefab`

This task is prefab work via Unity-MCP (`manage_prefabs` / `manage_components`) or careful YAML edits, then in-headset verification. It cannot be unit-tested.

- [ ] **Step 1: Add the mode button** — in the transport bar (sibling of the play/pause button in `AnimatorPanelModule.prefab`), add a `Button` GameObject with a child `Image` (the icon). Wire on the `AnimatorSubTransport` component:
  - `_modeButton` → the new button
  - `_modeIcon` → the button's child Image
  - leave `_onceSprite` / `_loopSprite` **empty** (the user fills them, exactly like play/pause).

- [ ] **Step 2: Verify the toolbar input fields can be typed into in VR** — each of the three `TMP_InputField`s (`_currentFrameInput`, `_totalFramesInput`, `_fpsInput`) must have a `VrInputFieldProxy` component (it publishes `KeyboardFocusEvent` on pointer-down). If any is missing, add it. Without it the VR keyboard never targets the field.

- [ ] **Step 3: Checkpoint** — `refresh_unity` + `read_console` (no errors). Then **hand to the user for in-headset verification** (cannot be automated):
  1. Keyframe a **bone** on a rig (no reopen) → its row appears immediately. Keyframe a new **object** track → row appears immediately.
  2. Edit total-frames / fps / current-frame via the VR keyboard → the value applies (timeline length changes; playback speed changes; playhead moves). Long ranges are not truncated mid-typing.
  3. Change fps → **all** animations in the scene play at the new rate (scene-wide).
  4. Toggle the mode button to **Loop** → playback wraps and repeats. Toggle to **Once** → after the end the object snaps to **frame 0 in its first-keyframe pose**; the mode icon swaps (once you assign the two sprites).
  5. Tune the config (`KeySize`, `KeySizeSelected`, `MajorTickHeight`, `MinorTickHeight`, `RowHeight`, key colors) → the timeline reflects every change.
  6. Rename a node → its track row name updates live (no reopen).
  7. Select a rig → no crash.

- [ ] **Step 4:** Report results. **No git** — the user commits manually.

---

## Self-Review (done by the author)

**Spec coverage:**
- #1 live tracks → Task 4 (new-track `TracksChanged`) + Task 8 step 4 (names on rename). ✓
- #2 inputs apply → Task 7 (VR keyboard commit) + Task 9 step 2 (field proxies). ✓
- #2.1 scene fps → Task 2 (field) + Task 3 (use everywhere) + Task 8 step 3 (toolbar routing). ✓
- config-driven metrics → Task 6. ✓
- #4 Once/Loop + switch → Task 1 (clock) + Task 8 steps 1–2 (button) + Task 9 (prefab). ✓
- #4.1 first-key pose → Task 5. ✓

**Placeholder scan:** No TBD/"handle edge cases"/"similar to". The only conditional is Task 5's note to demote one assertion to in-headset *if* `SampleAnimation` is flaky — the code still ships either way. ✓

**Type consistency:** `AnimationPlayMode {Once, Loop}`, `AnimationClock.PlayMode`/`SetPlayMode`/`AdvanceFrame`, `PlaybackStateChangedEvent.Completed`, `SceneAnimationData.Fps`, `AnimationAuthoring.GetSceneFps`/`SetSceneFps`, `AnimatorSubTransport._modeButton`/`_modeIcon`/`_onceSprite`/`_loopSprite`/`OnToggleMode`/`SetMode`, `AnimatorPanel.OnToggleModeClicked`/`OnNodeRenamed`, config `KeySize`/`KeySizeSelected`/`MajorTickHeight`/`MinorTickHeight`/`RowHeight` — names are consistent across tasks. ✓
