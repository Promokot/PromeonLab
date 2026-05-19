# Animation System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement SceneNode transform animation — keyframe recording, timeline scrubbing, and playback — stored as `animation.json` in the scene directory.

**Architecture:** `AnimationClock` (pure C# `ITickable`) drives a frame counter and publishes `FrameChangedEvent`. `AnimationAuthoring` (pure C# `IStartable`) owns `SceneAnimationData`, builds `AnimationClip` objects from track keyframes, and calls `clip.SampleAnimation()` on each frame event. `AnimationModule` (MonoBehaviour) is a UserPanel tab that wires the UI to both services.

**Tech Stack:** Unity 6 AnimationClip/AnimationCurve (legacy mode), JsonUtility, VContainer ITickable/IStartable, MessagePipe EventBus, TMP, Unity UI Slider.

---

## File Map

| Action | Path |
|---|---|
| Create | `Assets/_App/Subsystems/AnimationAuthoring/Data/AnimKeyData.cs` |
| Create | `Assets/_App/Subsystems/AnimationAuthoring/Data/AnimTrackData.cs` |
| Create | `Assets/_App/Subsystems/AnimationAuthoring/Data/SceneAnimationData.cs` |
| Create | `Assets/_App/Subsystems/AnimationAuthoring/AnimationClock.cs` |
| Create | `Assets/_App/Subsystems/AnimationAuthoring/AnimationAuthoring.cs` |
| Create | `Assets/_App/Subsystems/AnimationAuthoring/UI/AnimationModule.cs` |
| Create | `Assets/_App/Subsystems/AnimationAuthoring/Tests/AnimationDataTests.cs` |
| Create | `Assets/_App/Subsystems/AnimationAuthoring/Tests/AnimationClockTests.cs` |
| Modify | `Assets/_App/Subsystems/AnimationAuthoring/AnimationAuthoring.cs` ← replaces 2-line placeholder |
| Modify | `Assets/_App/Subsystems/AnimationPlayback/AnimationPlayback.cs` ← delete placeholder |
| Modify | `Assets/_App/_Shared/Events/AppEvents.cs` ← add AnimationKeyframeChangedEvent |
| Modify | `Assets/_App/Subsystems/StorageCore/PathProvider.cs` ← add AnimationJson() |
| Modify | `Assets/_App/Bootstrap/VrEditingSceneScope.cs` ← register clock + authoring + module |

---

## Task 1: Data types + event

**Files:**
- Create: `Assets/_App/Subsystems/AnimationAuthoring/Data/AnimKeyData.cs`
- Create: `Assets/_App/Subsystems/AnimationAuthoring/Data/AnimTrackData.cs`
- Create: `Assets/_App/Subsystems/AnimationAuthoring/Data/SceneAnimationData.cs`
- Modify: `Assets/_App/_Shared/Events/AppEvents.cs`
- Create: `Assets/_App/Subsystems/AnimationAuthoring/Tests/AnimationDataTests.cs`

- [ ] **Write failing tests**

Create `Assets/_App/Subsystems/AnimationAuthoring/Tests/AnimationDataTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

public class AnimationDataTests
{
    // AnimTrackData.UpsertKey
    [Test]
    public void UpsertKey_AddsNewKeyframe()
    {
        var track = new AnimTrackData { NodeId = "n1" };
        track.UpsertKey(10, Vector3.one, Quaternion.identity, Vector3.one);
        Assert.AreEqual(1, track.Keys.Count);
        Assert.AreEqual(10, track.Keys[0].Frame);
    }

    [Test]
    public void UpsertKey_OverwritesExistingFrame()
    {
        var track = new AnimTrackData { NodeId = "n1" };
        track.UpsertKey(10, Vector3.zero,    Quaternion.identity, Vector3.one);
        track.UpsertKey(10, Vector3.forward, Quaternion.identity, Vector3.one);
        Assert.AreEqual(1,           track.Keys.Count);
        Assert.AreEqual(Vector3.forward, track.Keys[0].Position);
    }

    [Test]
    public void UpsertKey_KeysAreSortedByFrame()
    {
        var track = new AnimTrackData { NodeId = "n1" };
        track.UpsertKey(30, Vector3.zero, Quaternion.identity, Vector3.one);
        track.UpsertKey(10, Vector3.zero, Quaternion.identity, Vector3.one);
        track.UpsertKey(20, Vector3.zero, Quaternion.identity, Vector3.one);
        Assert.AreEqual(10, track.Keys[0].Frame);
        Assert.AreEqual(20, track.Keys[1].Frame);
        Assert.AreEqual(30, track.Keys[2].Frame);
    }

    [Test]
    public void RemoveKey_RemovesCorrectFrame()
    {
        var track = new AnimTrackData { NodeId = "n1" };
        track.UpsertKey(10, Vector3.zero, Quaternion.identity, Vector3.one);
        track.UpsertKey(20, Vector3.zero, Quaternion.identity, Vector3.one);
        track.RemoveKey(10);
        Assert.AreEqual(1,  track.Keys.Count);
        Assert.AreEqual(20, track.Keys[0].Frame);
    }

    [Test]
    public void HasKey_ReturnsTrueForExistingFrame()
    {
        var track = new AnimTrackData { NodeId = "n1" };
        track.UpsertKey(15, Vector3.zero, Quaternion.identity, Vector3.one);
        Assert.IsTrue(track.HasKey(15));
        Assert.IsFalse(track.HasKey(99));
    }

    // SceneAnimationData.GetOrCreateTrack
    [Test]
    public void GetOrCreateTrack_CreatesNewTrack()
    {
        var data = new SceneAnimationData();
        var track = data.GetOrCreateTrack("abc");
        Assert.AreEqual("abc", track.NodeId);
        Assert.AreEqual(1, data.Tracks.Count);
    }

    [Test]
    public void GetOrCreateTrack_ReturnsExistingTrack()
    {
        var data  = new SceneAnimationData();
        var first  = data.GetOrCreateTrack("abc");
        var second = data.GetOrCreateTrack("abc");
        Assert.AreSame(first, second);
        Assert.AreEqual(1, data.Tracks.Count);
    }

    // JSON round-trip
    [Test]
    public void SceneAnimationData_JsonRoundTrip()
    {
        var data  = new SceneAnimationData { Fps = 24, TotalFrames = 60 };
        var track = data.GetOrCreateTrack("node1");
        track.UpsertKey(5, new Vector3(1, 2, 3), Quaternion.Euler(10, 20, 30), Vector3.one);

        var json    = UnityEngine.JsonUtility.ToJson(data);
        var loaded  = UnityEngine.JsonUtility.FromJson<SceneAnimationData>(json);

        Assert.AreEqual(24,    loaded.Fps);
        Assert.AreEqual(60,    loaded.TotalFrames);
        Assert.AreEqual(1,     loaded.Tracks.Count);
        Assert.AreEqual("node1", loaded.Tracks[0].NodeId);
        Assert.AreEqual(5,     loaded.Tracks[0].Keys[0].Frame);
        Assert.AreEqual(1f,    loaded.Tracks[0].Keys[0].Position.x, 0.001f);
    }
}
```

- [ ] **Open Unity Test Runner** (Window > General > Test Runner) and run `AnimationDataTests` — expect compile errors because classes don't exist yet.

- [ ] **Create `AnimKeyData.cs`**

```csharp
using System;
using UnityEngine;

[Serializable]
public class AnimKeyData
{
    public int        Frame;
    public Vector3    Position;
    public Quaternion Rotation;
    public Vector3    Scale;
}
```

- [ ] **Create `AnimTrackData.cs`**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class AnimTrackData
{
    public string          NodeId;
    public List<AnimKeyData> Keys = new();

    public void UpsertKey(int frame, Vector3 pos, Quaternion rot, Vector3 scale)
    {
        for (int i = 0; i < Keys.Count; i++)
        {
            if (Keys[i].Frame != frame) continue;
            Keys[i] = new AnimKeyData { Frame = frame, Position = pos, Rotation = rot, Scale = scale };
            return;
        }
        Keys.Add(new AnimKeyData { Frame = frame, Position = pos, Rotation = rot, Scale = scale });
        Keys.Sort((a, b) => a.Frame.CompareTo(b.Frame));
    }

    public void RemoveKey(int frame)
    {
        for (int i = Keys.Count - 1; i >= 0; i--)
            if (Keys[i].Frame == frame) Keys.RemoveAt(i);
    }

    public bool HasKey(int frame)
    {
        foreach (var k in Keys)
            if (k.Frame == frame) return true;
        return false;
    }
}
```

- [ ] **Create `SceneAnimationData.cs`**

```csharp
using System;
using System.Collections.Generic;

[Serializable]
public class SceneAnimationData
{
    public int                schemaVersion = 1;
    public int                Fps           = 30;
    public int                TotalFrames   = 120;
    public List<AnimTrackData> Tracks        = new();

    public AnimTrackData GetOrCreateTrack(string nodeId)
    {
        foreach (var t in Tracks)
            if (t.NodeId == nodeId) return t;
        var track = new AnimTrackData { NodeId = nodeId };
        Tracks.Add(track);
        return track;
    }

    public AnimTrackData FindTrack(string nodeId)
    {
        foreach (var t in Tracks)
            if (t.NodeId == nodeId) return t;
        return null;
    }
}
```

- [ ] **Add event to `AppEvents.cs`** — append one line:

```csharp
public struct AnimationKeyframeChangedEvent { public string NodeId; }
```

- [ ] **Run `AnimationDataTests` in Test Runner** — all 8 tests should pass.

- [ ] **Commit**

```
git add Assets/_App/Subsystems/AnimationAuthoring/Data/ \
        Assets/_App/Subsystems/AnimationAuthoring/Tests/AnimationDataTests.cs \
        Assets/_App/_Shared/Events/AppEvents.cs
git commit -m "feat: animation data types + AnimationKeyframeChangedEvent"
```

---

## Task 2: PathProvider.AnimationJson

**Files:**
- Modify: `Assets/_App/Subsystems/StorageCore/PathProvider.cs`
- Modify: `Assets/_App/Subsystems/StorageCore/Tests/PathProviderTests.cs`

- [ ] **Add failing test** to `PathProviderTests.cs`:

```csharp
[Test]
public void AnimationJson_ReturnsExpectedPath()
{
    Assert.AreEqual("/data/scenes/scene-01/animation.json",
        _sut.AnimationJson("scene-01"));
}
```

- [ ] **Run test** — expect fail: `AnimationJson` does not exist.

- [ ] **Add method to `PathProvider.cs`** after `AssetCatalogJson`:

```csharp
public string AnimationJson(string sceneId) =>
    Path.Combine(SceneRoot(sceneId), "animation.json");
```

- [ ] **Run test** — expect pass.

- [ ] **Commit**

```
git add Assets/_App/Subsystems/StorageCore/PathProvider.cs \
        Assets/_App/Subsystems/StorageCore/Tests/PathProviderTests.cs
git commit -m "feat: PathProvider.AnimationJson"
```

---

## Task 3: AnimationClock

**Files:**
- Create: `Assets/_App/Subsystems/AnimationAuthoring/AnimationClock.cs`
- Create: `Assets/_App/Subsystems/AnimationAuthoring/Tests/AnimationClockTests.cs`

- [ ] **Write failing tests**

Create `Assets/_App/Subsystems/AnimationAuthoring/Tests/AnimationClockTests.cs`:

```csharp
using NUnit.Framework;

public class AnimationClockTests
{
    private EventBus       _bus;
    private AnimationClock _sut;

    [SetUp]
    public void SetUp()
    {
        _bus = new EventBus();
        _sut = new AnimationClock(_bus);
    }

    [Test]
    public void InitialState_IsNotPlaying_FrameZero()
    {
        Assert.IsFalse(_sut.IsPlaying);
        Assert.AreEqual(0, _sut.CurrentFrame);
    }

    [Test]
    public void Play_SetsIsPlayingTrue()
    {
        _sut.Play();
        Assert.IsTrue(_sut.IsPlaying);
    }

    [Test]
    public void Pause_SetsIsPlayingFalse_PreservesFrame()
    {
        _sut.Seek(10);
        _sut.Play();
        _sut.Pause();
        Assert.IsFalse(_sut.IsPlaying);
        Assert.AreEqual(10, _sut.CurrentFrame);
    }

    [Test]
    public void Stop_ResetsFrameToZero()
    {
        _sut.Seek(30);
        _sut.Play();
        _sut.Stop();
        Assert.IsFalse(_sut.IsPlaying);
        Assert.AreEqual(0, _sut.CurrentFrame);
    }

    [Test]
    public void Seek_ClampsToRange()
    {
        _sut.Seek(-5);
        Assert.AreEqual(0, _sut.CurrentFrame);

        _sut.Seek(9999);
        Assert.AreEqual(_sut.TotalFrames, _sut.CurrentFrame);
    }

    [Test]
    public void Seek_PublishesFrameChangedEvent()
    {
        int received = -1;
        _bus.Subscribe<FrameChangedEvent>(e => received = e.Frame);
        _sut.Seek(42);
        Assert.AreEqual(42, received);
    }

    [Test]
    public void Play_AtEndFrame_RewindsToZero()
    {
        _sut.Seek(_sut.TotalFrames);
        _sut.Play();
        Assert.AreEqual(0, _sut.CurrentFrame);
        Assert.IsTrue(_sut.IsPlaying);
    }
}
```

- [ ] **Run tests** — expect compile errors (AnimationClock missing).

- [ ] **Create `AnimationClock.cs`**

```csharp
using UnityEngine;
using VContainer.Unity;

public class AnimationClock : ITickable
{
    public int  CurrentFrame { get; private set; }
    public int  TotalFrames  { get; private set; } = 120;
    public int  Fps          { get; private set; } = 30;
    public bool IsPlaying    { get; private set; }

    private float    _accumulated;
    private readonly EventBus _bus;

    public AnimationClock(EventBus bus) => _bus = bus;

    public void Tick()
    {
        if (!IsPlaying) return;
        _accumulated += Time.deltaTime * Fps;
        var next = Mathf.FloorToInt(_accumulated);
        if (next == CurrentFrame) return;

        if (next >= TotalFrames)
        {
            CurrentFrame = TotalFrames;
            IsPlaying    = false;
            _accumulated = TotalFrames;
        }
        else
        {
            CurrentFrame = next;
        }
        _bus.Publish(new FrameChangedEvent        { Frame = CurrentFrame });
        _bus.Publish(new PlaybackStateChangedEvent { IsPlaying = IsPlaying, Frame = CurrentFrame });
    }

    public void Play()
    {
        if (CurrentFrame >= TotalFrames) Seek(0);
        IsPlaying = true;
        _bus.Publish(new PlaybackStateChangedEvent { IsPlaying = true, Frame = CurrentFrame });
    }

    public void Pause()
    {
        IsPlaying = false;
        _bus.Publish(new PlaybackStateChangedEvent { IsPlaying = false, Frame = CurrentFrame });
    }

    public void Stop()
    {
        IsPlaying    = false;
        _accumulated = 0f;
        CurrentFrame = 0;
        _bus.Publish(new FrameChangedEvent         { Frame    = 0 });
        _bus.Publish(new PlaybackStateChangedEvent { IsPlaying = false, Frame = 0 });
    }

    public void Seek(int frame)
    {
        CurrentFrame = Mathf.Clamp(frame, 0, TotalFrames);
        _accumulated = CurrentFrame;
        _bus.Publish(new FrameChangedEvent { Frame = CurrentFrame });
    }
}
```

- [ ] **Run `AnimationClockTests`** — all 7 tests should pass.

- [ ] **Commit**

```
git add Assets/_App/Subsystems/AnimationAuthoring/AnimationClock.cs \
        Assets/_App/Subsystems/AnimationAuthoring/Tests/AnimationClockTests.cs
git commit -m "feat: AnimationClock (ITickable) with play/pause/stop/seek"
```

---

## Task 4: AnimationAuthoring

**Files:**
- Modify: `Assets/_App/Subsystems/AnimationAuthoring/AnimationAuthoring.cs` (replaces 2-line placeholder)
- Modify: `Assets/_App/Subsystems/AnimationPlayback/AnimationPlayback.cs` (delete placeholder)

- [ ] **Replace `AnimationAuthoring.cs`** with the full implementation:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

public class AnimationAuthoring : IStartable, IDisposable
{
    private readonly AnimationClock _clock;
    private readonly ISceneGraph    _sceneGraph;
    private readonly PathProvider   _paths;
    private readonly AppStorage     _storage;
    private readonly EventBus       _bus;

    private SceneAnimationData                    _data;
    private readonly Dictionary<string, AnimationClip> _clips = new();
    private string _sceneId;

    public AnimationAuthoring(AnimationClock clock, ISceneGraph sceneGraph,
                               PathProvider paths, AppStorage storage, EventBus bus)
    {
        _clock      = clock;
        _sceneGraph = sceneGraph;
        _paths      = paths;
        _storage    = storage;
        _bus        = bus;
    }

    public void Start()
    {
        _bus.Subscribe<SceneOpenedEvent>(OnSceneOpened);
        _bus.Subscribe<FrameChangedEvent>(OnFrameChanged);

        var activeId = _storage.ActiveSceneId;
        if (!string.IsNullOrEmpty(activeId))
            _ = LoadAsync(activeId, CancellationToken.None);
    }

    public void Dispose()
    {
        _bus.Unsubscribe<SceneOpenedEvent>(OnSceneOpened);
        _bus.Unsubscribe<FrameChangedEvent>(OnFrameChanged);
    }

    public void SetKey(string nodeId, int frame)
    {
        var go = _sceneGraph.GetNode(nodeId);
        if (go == null) return;

        EnsureData();
        var track = _data.GetOrCreateTrack(nodeId);
        track.UpsertKey(frame, go.transform.localPosition,
                                go.transform.localRotation,
                                go.transform.localScale);
        RebuildClip(track);
        _ = SaveAsync(CancellationToken.None);
        _bus.Publish(new AnimationKeyframeChangedEvent { NodeId = nodeId });
    }

    public void DeleteKey(string nodeId, int frame)
    {
        var track = _data?.FindTrack(nodeId);
        if (track == null) return;

        track.RemoveKey(frame);
        if (track.Keys.Count == 0)
        {
            _data.Tracks.Remove(track);
            _clips.Remove(nodeId);
        }
        else
        {
            RebuildClip(track);
        }
        _ = SaveAsync(CancellationToken.None);
        _bus.Publish(new AnimationKeyframeChangedEvent { NodeId = nodeId });
    }

    public bool HasKey(string nodeId, int frame) =>
        _data?.FindTrack(nodeId)?.HasKey(frame) ?? false;

    public IReadOnlyList<int> GetKeyFrames(string nodeId)
    {
        var track = _data?.FindTrack(nodeId);
        if (track == null) return Array.Empty<int>();
        var frames = new int[track.Keys.Count];
        for (int i = 0; i < track.Keys.Count; i++)
            frames[i] = track.Keys[i].Frame;
        return frames;
    }

    private void OnSceneOpened(SceneOpenedEvent e) =>
        _ = LoadAsync(e.SceneId, CancellationToken.None);

    private void OnFrameChanged(FrameChangedEvent e)
    {
        if (_data == null || !_clock.IsPlaying) return;
        ApplyFrame(e.Frame);
    }

    private void ApplyFrame(int frame)
    {
        float t = (float)frame / (_data.Fps > 0 ? _data.Fps : 30);
        foreach (var track in _data.Tracks)
        {
            if (!_clips.TryGetValue(track.NodeId, out var clip)) continue;
            var go = _sceneGraph.GetNode(track.NodeId);
            if (go == null) continue;
            clip.SampleAnimation(go, t);
        }
    }

    private async Task LoadAsync(string sceneId, CancellationToken ct)
    {
        _sceneId = sceneId;
        _clips.Clear();
        var path = _paths.AnimationJson(sceneId);
        if (!File.Exists(path))
        {
            _data = new SceneAnimationData();
            return;
        }
        try
        {
            var json = await File.ReadAllTextAsync(path, ct);
            _data = JsonUtility.FromJson<SceneAnimationData>(json) ?? new SceneAnimationData();
            foreach (var track in _data.Tracks)
                RebuildClip(track);
        }
        catch (Exception ex)
        {
            Debug.LogError($"AnimationAuthoring: load failed '{path}': {ex.Message}");
            _data = new SceneAnimationData();
        }
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        if (_data == null || string.IsNullOrEmpty(_sceneId)) return;
        try
        {
            var path = _paths.AnimationJson(_sceneId);
            var json = JsonUtility.ToJson(_data, prettyPrint: true);
            await File.WriteAllTextAsync(path, json, ct);
        }
        catch (Exception ex)
        {
            Debug.LogError($"AnimationAuthoring: save failed: {ex.Message}");
        }
    }

    private void EnsureData() => _data ??= new SceneAnimationData();

    private void RebuildClip(AnimTrackData track)
    {
        var clip = new AnimationClip { legacy = true };
        int fps  = _data?.Fps > 0 ? _data.Fps : 30;

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

        clip.SetCurve("", typeof(Transform), "localPosition.x",  px);
        clip.SetCurve("", typeof(Transform), "localPosition.y",  py);
        clip.SetCurve("", typeof(Transform), "localPosition.z",  pz);
        clip.SetCurve("", typeof(Transform), "m_LocalRotation.x", rx);
        clip.SetCurve("", typeof(Transform), "m_LocalRotation.y", ry);
        clip.SetCurve("", typeof(Transform), "m_LocalRotation.z", rz);
        clip.SetCurve("", typeof(Transform), "m_LocalRotation.w", rw);
        clip.SetCurve("", typeof(Transform), "localScale.x",  sx);
        clip.SetCurve("", typeof(Transform), "localScale.y",  sy);
        clip.SetCurve("", typeof(Transform), "localScale.z",  sz);

        _clips[track.NodeId] = clip;
    }
}
```

- [ ] **Replace `AnimationPlayback.cs`** with a comment redirect (keep file to avoid `.meta` orphan):

```csharp
// Playback logic merged into AnimationAuthoring + AnimationClock (Phase 7).
// This file is intentionally empty.
public static class AnimationPlaybackPlaceholder { }
```

- [ ] **Open Unity** — verify no compile errors in Console.

- [ ] **Commit**

```
git add Assets/_App/Subsystems/AnimationAuthoring/AnimationAuthoring.cs \
        Assets/_App/Subsystems/AnimationPlayback/AnimationPlayback.cs
git commit -m "feat: AnimationAuthoring — SetKey/DeleteKey/ApplyFrame/Load/Save"
```

---

## Task 5: AnimationModule UI (code only)

**Files:**
- Create: `Assets/_App/Subsystems/AnimationAuthoring/UI/AnimationModule.cs`

- [ ] **Create `AnimationModule.cs`**

```csharp
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class AnimationModule : MonoBehaviour
{
    [Header("Transport")]
    [SerializeField] private Button   _playButton;
    [SerializeField] private Button   _stopButton;
    [SerializeField] private Button   _rewindButton;

    [Header("Scrubber")]
    [SerializeField] private Slider       _scrubber;
    [SerializeField] private TMP_Text     _frameLabel;

    [Header("Keyframe markers")]
    [SerializeField] private RectTransform _markersRoot;
    [SerializeField] private Image         _markerPrefab;

    [Header("Keyframe actions")]
    [SerializeField] private Button _setKeyButton;
    [SerializeField] private Button _deleteKeyButton;

    private AnimationClock    _clock;
    private AnimationAuthoring _authoring;
    private ISelectionManager _selection;
    private EventBus          _bus;

    private readonly List<Image> _markerPool = new();
    private bool   _suppressScrub;
    private string _activeNodeId;

    [Inject]
    public void Construct(AnimationClock clock, AnimationAuthoring authoring,
                          ISelectionManager selection, EventBus bus)
    {
        _clock     = clock;
        _authoring = authoring;
        _selection = selection;
        _bus       = bus;
    }

    private void Awake()
    {
        _playButton?.onClick.AddListener(OnPlay);
        _stopButton?.onClick.AddListener(OnStop);
        _rewindButton?.onClick.AddListener(OnRewind);
        _setKeyButton?.onClick.AddListener(OnSetKey);
        _deleteKeyButton?.onClick.AddListener(OnDeleteKey);
        _scrubber?.onValueChanged.AddListener(OnScrub);
    }

    private void OnEnable()
    {
        _bus?.Subscribe<FrameChangedEvent>(OnFrameChanged);
        _bus?.Subscribe<PlaybackStateChangedEvent>(OnPlaybackStateChanged);
        _bus?.Subscribe<SelectionChangedEvent>(OnSelectionChanged);
        _bus?.Subscribe<AnimationKeyframeChangedEvent>(OnKeyframeChanged);

        if (_clock != null && _scrubber != null)
        {
            _scrubber.minValue     = 0;
            _scrubber.maxValue     = _clock.TotalFrames;
            _scrubber.wholeNumbers = true;
        }
        RefreshScrubber(_clock?.CurrentFrame ?? 0);
        RefreshActiveNode();
        RefreshButtons();
    }

    private void OnDisable()
    {
        _bus?.Unsubscribe<FrameChangedEvent>(OnFrameChanged);
        _bus?.Unsubscribe<PlaybackStateChangedEvent>(OnPlaybackStateChanged);
        _bus?.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);
        _bus?.Unsubscribe<AnimationKeyframeChangedEvent>(OnKeyframeChanged);
    }

    private void OnPlay()  => _clock?.Play();
    private void OnStop()  => _clock?.Stop();
    private void OnRewind() => _clock?.Seek(0);

    private void OnSetKey()
    {
        if (string.IsNullOrEmpty(_activeNodeId) || _clock == null) return;
        _authoring?.SetKey(_activeNodeId, _clock.CurrentFrame);
    }

    private void OnDeleteKey()
    {
        if (string.IsNullOrEmpty(_activeNodeId) || _clock == null) return;
        _authoring?.DeleteKey(_activeNodeId, _clock.CurrentFrame);
    }

    private void OnScrub(float value)
    {
        if (_suppressScrub) return;
        _clock?.Seek(Mathf.RoundToInt(value));
    }

    private void OnFrameChanged(FrameChangedEvent e)    => RefreshScrubber(e.Frame);
    private void OnPlaybackStateChanged(PlaybackStateChangedEvent _) => RefreshButtons();

    private void OnSelectionChanged(SelectionChangedEvent _)
    {
        RefreshActiveNode();
        RefreshButtons();
    }

    private void OnKeyframeChanged(AnimationKeyframeChangedEvent e)
    {
        if (e.NodeId == _activeNodeId) RefreshMarkers();
        RefreshButtons();
    }

    private void RefreshActiveNode()
    {
        _activeNodeId = _selection?.ActiveId;
        RefreshMarkers();
    }

    private void RefreshScrubber(int frame)
    {
        if (_scrubber != null)
        {
            _suppressScrub  = true;
            _scrubber.value = frame;
            _suppressScrub  = false;
        }
        if (_frameLabel != null) _frameLabel.text = $"Fr: {frame}";
    }

    private void RefreshButtons()
    {
        bool hasNode = !string.IsNullOrEmpty(_activeNodeId);
        bool hasKeyAtFrame = hasNode && _clock != null
                             && (_authoring?.HasKey(_activeNodeId, _clock.CurrentFrame) ?? false);

        if (_setKeyButton    != null) _setKeyButton.interactable    = hasNode;
        if (_deleteKeyButton != null) _deleteKeyButton.interactable = hasKeyAtFrame;
    }

    private void RefreshMarkers()
    {
        foreach (var m in _markerPool) m.gameObject.SetActive(false);
        if (_markersRoot == null || _markerPrefab == null) return;
        if (string.IsNullOrEmpty(_activeNodeId) || _authoring == null || _clock == null) return;

        var frames = _authoring.GetKeyFrames(_activeNodeId);
        float width = _markersRoot.rect.width;
        int total   = _clock.TotalFrames;

        for (int i = 0; i < frames.Count; i++)
        {
            var marker = GetOrCreateMarker(i);
            marker.gameObject.SetActive(true);
            float x = total > 0 ? (float)frames[i] / total * width - width * 0.5f : 0f;
            ((RectTransform)marker.transform).anchoredPosition = new Vector2(x, 0f);
        }
    }

    private Image GetOrCreateMarker(int idx)
    {
        while (_markerPool.Count <= idx)
        {
            var go = Instantiate(_markerPrefab, _markersRoot);
            go.gameObject.SetActive(false);
            _markerPool.Add(go);
        }
        return _markerPool[idx];
    }
}
```

- [ ] **Open Unity** — verify no compile errors.

- [ ] **Commit**

```
git add Assets/_App/Subsystems/AnimationAuthoring/UI/AnimationModule.cs
git commit -m "feat: AnimationModule UI — scrubber, transport, set/delete key, keyframe markers"
```

---

## Task 6: VrEditingSceneScope registration

**Files:**
- Modify: `Assets/_App/Bootstrap/VrEditingSceneScope.cs`
- Modify: `Assets/_App/Bootstrap/RootLifetimeScope.cs`

- [ ] **In `VrEditingSceneScope.Configure`** — replace the Phase 7 comment:

Find:
```csharp
        // Phase 7: TrackRecorder, PropertyApplicator, PlaybackController
```
Replace with:
```csharp
        builder.RegisterEntryPoint<AnimationClock>(Lifetime.Scoped).AsSelf();
        builder.RegisterEntryPoint<AnimationAuthoring>(Lifetime.Scoped).AsSelf();

        var animModule = Object.FindAnyObjectByType<AnimationModule>(FindObjectsInactive.Include);
        if (animModule != null)
            builder.RegisterBuildCallback(c => c.Inject(animModule));
```

- [ ] **In `RootLifetimeScope.cs`** — remove the stale placeholder comment:

Find:
```csharp
        // AssetImporter registered in VrEditingSceneScope (needs SceneGraph)
        // AnimationClock — Phase 7
```
Replace with:
```csharp
        // AssetImporter registered in VrEditingSceneScope (needs SceneGraph)
```

- [ ] **Open Unity** — verify no compile errors in Console.

- [ ] **Commit**

```
git add Assets/_App/Bootstrap/VrEditingSceneScope.cs \
        Assets/_App/Bootstrap/RootLifetimeScope.cs
git commit -m "feat: register AnimationClock + AnimationAuthoring in VrEditingSceneScope"
```

---

## Task 7: Prefab wiring (manual in Unity Editor)

**This task is done entirely in the Unity Editor. No code changes.**

### 7a — NavBarConfig SO

- [ ] Open `Assets/_App/Subsystems/SpatialUi/Data/NavBarConfig.asset` in Inspector.
- [ ] Add a new entry in `_entries`:
  - `Id`: `animation`
  - `VisibleModes`: `[VrEditing]`
  - `ExclusiveGroup`: `tools`
- [ ] Save the asset (`Ctrl+S`).

### 7b — UserPanel prefab: Animation panel

- [ ] Open the `UserPanel` prefab (double-click to enter prefab mode).
- [ ] Duplicate the existing `AssetsPanel` child — rename the copy to `AnimationPanel`.
- [ ] Remove the `AssetBrowserModule` component from `AnimationPanel`.
- [ ] Add `AnimationModule` component to `AnimationPanel`.
- [ ] Build the layout inside `AnimationPanel` to match the spec (see below). **All RectTransform Z must be 0.**
- [ ] Wire `AnimationModule` SerializeField references in Inspector.

**AnimationPanel layout:**

```
AnimationPanel (DetachablePanel)
└── Content
    ├── TransportRow (HorizontalLayoutGroup)
    │   ├── RewindButton   (Button + TMP_Text "◀◀")
    │   ├── PlayButton     (Button + TMP_Text "▶")
    │   ├── StopButton     (Button + TMP_Text "■")
    │   └── FrameLabel     (TMP_Text "Fr: 0")
    ├── ScrubberRow
    │   ├── Scrubber       (Slider — minValue=0, maxValue=120, wholeNumbers=true)
    │   └── MarkersRoot    (RectTransform — same width/pos as Scrubber fill area)
    └── KeyframeRow (HorizontalLayoutGroup)
        ├── SetKeyButton    (Button + TMP_Text "Set Key")
        └── DeleteKeyButton (Button + TMP_Text "Delete Key")
```

**MarkerPrefab** — create a small `Image` GameObject (12×12 px, rotated 45° to look like a diamond), save as prefab in `Assets/_App/Subsystems/AnimationAuthoring/UI/KeyframeMarker.prefab`. Assign to `AnimationModule._markerPrefab`.

### 7c — UserPanel prefab: NavBarBinding

- [ ] In the `UserPanel` component on the prefab root, expand `_bindings` array.
- [ ] Add new binding:
  - `EntryId`: `animation`
  - `NavButton`: drag the new NavBar button for Animation
  - `Panel`: drag `AnimationPanel`
- [ ] Add a NavBar button for Animation (copy an existing NavBar button, change label/icon).
- [ ] Exit prefab mode and save.

### 7d — Smoke test

- [ ] Enter Play Mode in Unity Editor (XR Simulator).
- [ ] Open a scene from Main Menu.
- [ ] Open UserPanel → click Animation tab → `AnimationPanel` appears.
- [ ] Spawn an object. Select it.
- [ ] Scrub to frame 10 → click **Set Key**. Frame counter shows 10. Marker appears on timeline.
- [ ] Move the object. Scrub to frame 30 → click **Set Key**. Second marker appears.
- [ ] Click **Rewind** → scrub to 0. Click **Play** — object moves between the two positions.
- [ ] Click **Stop** — object snaps back to frame 0 pose.
- [ ] Check `Application.persistentDataPath/scenes/{id}/animation.json` exists and contains two keyframes.
- [ ] Exit Play Mode, re-enter Play Mode, open same scene — keyframes still present.

- [ ] **Commit after wiring**

```
git add Assets/_App/Subsystems/SpatialUi/Data/NavBarConfig.asset \
        Assets/_App/Subsystems/AnimationAuthoring/UI/KeyframeMarker.prefab \
        Assets/...  (UserPanel prefab changes)
git commit -m "feat: wire AnimationModule into UserPanel + NavBarConfig"
```
