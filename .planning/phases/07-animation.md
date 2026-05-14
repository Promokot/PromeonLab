# Phase 7: Animation Authoring + Playback — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Select a bone proxy → press "Set Key" at current frame → scrub timeline → press Play → bone animates between keyframes.

**Architecture:** `AnimationClock` (Singleton, `ITickable`) advances the current frame during playback and publishes `FrameChangedEvent`. `TrackRecorder` listens and writes the selected bone's transform into `ActionData`. `AnimationEvaluator` interpolates `ActionData` at a given frame. `PropertyApplicator` writes evaluated values back to bone `Transform`s. `PlaybackController` toggles play/pause/stop and controls clock speed.

**Tech Stack:** VContainer `ITickable` for the update loop, `UnityEngine.Transform`, pure C# interpolation (no `AnimationClip`)

---

## File Map

**Create:**
- `Assets/Subsystems/AnimationAuthoring/Data/Keyframe.cs`
- `Assets/Subsystems/AnimationAuthoring/Data/AnimTrack.cs`
- `Assets/Subsystems/AnimationAuthoring/Data/ActionData.cs`
- `Assets/Subsystems/AnimationAuthoring/AnimationClock.cs`
- `Assets/Subsystems/AnimationAuthoring/TrackRecorder.cs`
- `Assets/Subsystems/AnimationPlayback/AnimationEvaluator.cs`
- `Assets/Subsystems/AnimationPlayback/PropertyApplicator.cs`
- `Assets/Subsystems/AnimationPlayback/PlaybackController.cs`
- `Assets/Subsystems/AnimationAuthoring/UI/KeyframeEditorPanel.cs`
- `Assets/Subsystems/AnimationAuthoring/Tests/AnimationEvaluatorTests.cs`

**Modify:**
- `Assets/_App/Bootstrap/RootLifetimeScope.cs` — register AnimationClock
- `Assets/_App/Bootstrap/VrEditingSceneScope.cs` — register TrackRecorder, PlaybackController
- `Assets/Subsystems/SpatialUi/UI/ToolbarPanel.cs` — add transport controls

---

## Task 1: AnimationClock

**Files:** `AnimationAuthoring/AnimationClock.cs`

- [ ] Create `Assets/Subsystems/AnimationAuthoring/AnimationClock.cs`:
  ```csharp
  using VContainer.Unity;

  public class AnimationClock : ITickable
  {
      public int CurrentFrame { get; private set; }
      public int FrameStart   { get; private set; } = 0;
      public int FrameEnd     { get; private set; } = 60;
      public int Fps          { get; private set; } = 30;
      public float SpeedMultiplier { get; set; } = 1f;
      public bool IsPlaying   { get; private set; }
      public bool IsLooping   { get; set; } = true;

      private readonly EventBus _bus;
      private float _fractionalFrame;

      public AnimationClock(EventBus bus) => _bus = bus;

      public void SetRange(int start, int end)
      {
          FrameStart = start;
          FrameEnd   = end;
      }

      public void SetFrame(int frame)
      {
          CurrentFrame    = UnityEngine.Mathf.Clamp(frame, FrameStart, FrameEnd);
          _fractionalFrame = CurrentFrame;
          _bus.Publish(new FrameChangedEvent { Frame = CurrentFrame });
      }

      public void Play()  { IsPlaying = true;  _fractionalFrame = CurrentFrame; }
      public void Pause() { IsPlaying = false; }
      public void Stop()  { IsPlaying = false; SetFrame(FrameStart); }

      public void Tick()
      {
          if (!IsPlaying) return;

          _fractionalFrame += SpeedMultiplier * UnityEngine.Time.deltaTime * Fps;

          if (_fractionalFrame >= FrameEnd)
          {
              if (IsLooping) _fractionalFrame = FrameStart;
              else { IsPlaying = false; _fractionalFrame = FrameEnd; }
          }

          var newFrame = (int)_fractionalFrame;
          if (newFrame == CurrentFrame) return;

          CurrentFrame = newFrame;
          _bus.Publish(new FrameChangedEvent { Frame = CurrentFrame });
      }
  }
  ```

- [ ] Register in `RootLifetimeScope.cs`:
  ```csharp
  builder.Register<AnimationClock>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
  ```

- [ ] Commit:
  ```bash
  git add Assets/Subsystems/AnimationAuthoring/AnimationClock.cs Assets/_App/Bootstrap/RootLifetimeScope.cs
  git commit -m "feat: add AnimationClock — ITickable frame advance with FrameChangedEvent"
  ```

---

## Task 2: ActionData + Keyframe

**Files:** `Data/Keyframe.cs`, `Data/AnimTrack.cs`, `Data/ActionData.cs`

- [ ] Create `Assets/Subsystems/AnimationAuthoring/Data/Keyframe.cs`:
  ```csharp
  using System;

  public enum Interpolation { Linear, Stepped }

  [Serializable]
  public struct Keyframe
  {
      public int Frame;
      public float Value;
      public Interpolation Interpolation;
  }
  ```

- [ ] Create `Assets/Subsystems/AnimationAuthoring/Data/AnimTrack.cs`:
  ```csharp
  using System;
  using System.Collections.Generic;

  [Serializable]
  public class AnimTrack
  {
      public string ChannelPath;   // e.g. "bone:Hips/position.x"
      public List<Keyframe> Keys = new();

      public void SetKey(int frame, float value, Interpolation interp = Interpolation.Linear)
      {
          Keys.RemoveAll(k => k.Frame == frame);
          Keys.Add(new Keyframe { Frame = frame, Value = value, Interpolation = interp });
          Keys.Sort((a, b) => a.Frame.CompareTo(b.Frame));
      }
  }
  ```

- [ ] Create `Assets/Subsystems/AnimationAuthoring/Data/ActionData.cs`:
  ```csharp
  using System;
  using System.Collections.Generic;

  [Serializable]
  public class ActionData
  {
      public string ActionId;
      public string TargetNodeId;
      public List<AnimTrack> Tracks = new();

      public AnimTrack GetOrCreateTrack(string channelPath)
      {
          foreach (var t in Tracks)
              if (t.ChannelPath == channelPath) return t;
          var track = new AnimTrack { ChannelPath = channelPath };
          Tracks.Add(track);
          return track;
      }
  }
  ```

- [ ] Commit:
  ```bash
  git add Assets/Subsystems/AnimationAuthoring/Data/
  git commit -m "feat: add Keyframe, AnimTrack, ActionData data structures"
  ```

---

## Task 3: AnimationEvaluator + Tests

**Files:** `AnimationPlayback/AnimationEvaluator.cs`, `AnimationAuthoring/Tests/AnimationEvaluatorTests.cs`

- [ ] Write failing tests first — `Assets/Subsystems/AnimationAuthoring/Tests/AnimationEvaluatorTests.cs`:
  ```csharp
  using NUnit.Framework;
  using System.Collections.Generic;

  public class AnimationEvaluatorTests
  {
      [Test]
      public void Linear_BetweenTwoKeys_Interpolates()
      {
          var track = new AnimTrack { ChannelPath = "test" };
          track.SetKey(0, 0f);
          track.SetKey(10, 10f);

          var result = AnimationEvaluator.EvaluateTrack(track, 5);
          Assert.AreEqual(5f, result, 0.001f);
      }

      [Test]
      public void Stepped_BetweenTwoKeys_ReturnsFromValue()
      {
          var track = new AnimTrack { ChannelPath = "test" };
          track.SetKey(0, 0f, Interpolation.Stepped);
          track.SetKey(10, 10f, Interpolation.Stepped);

          var result = AnimationEvaluator.EvaluateTrack(track, 5);
          Assert.AreEqual(0f, result, 0.001f);
      }

      [Test]
      public void BeforeFirstKey_ReturnsFirstKeyValue()
      {
          var track = new AnimTrack { ChannelPath = "test" };
          track.SetKey(5, 3f);

          Assert.AreEqual(3f, AnimationEvaluator.EvaluateTrack(track, 0), 0.001f);
      }

      [Test]
      public void AfterLastKey_ReturnsLastKeyValue()
      {
          var track = new AnimTrack { ChannelPath = "test" };
          track.SetKey(0, 1f);
          track.SetKey(5, 9f);

          Assert.AreEqual(9f, AnimationEvaluator.EvaluateTrack(track, 10), 0.001f);
      }

      [Test]
      public void EmptyTrack_ReturnsZero()
      {
          var track = new AnimTrack { ChannelPath = "test" };
          Assert.AreEqual(0f, AnimationEvaluator.EvaluateTrack(track, 5), 0.001f);
      }
  }
  ```

- [ ] Run tests → 5 failures

- [ ] Create `Assets/Subsystems/AnimationPlayback/AnimationEvaluator.cs`:
  ```csharp
  using System.Collections.Generic;
  using UnityEngine;

  public static class AnimationEvaluator
  {
      public static float EvaluateTrack(AnimTrack track, int frame)
      {
          if (track.Keys == null || track.Keys.Count == 0) return 0f;
          if (frame <= track.Keys[0].Frame) return track.Keys[0].Value;
          if (frame >= track.Keys[^1].Frame) return track.Keys[^1].Value;

          for (int i = 0; i < track.Keys.Count - 1; i++)
          {
              var a = track.Keys[i];
              var b = track.Keys[i + 1];
              if (frame < a.Frame || frame > b.Frame) continue;

              if (a.Interpolation == Interpolation.Stepped)
                  return a.Value;

              var t = (float)(frame - a.Frame) / (b.Frame - a.Frame);
              return Mathf.Lerp(a.Value, b.Value, t);
          }
          return 0f;
      }

      public static Dictionary<string, float> EvaluateAction(ActionData action, int frame)
      {
          var result = new Dictionary<string, float>();
          foreach (var track in action.Tracks)
              result[track.ChannelPath] = EvaluateTrack(track, frame);
          return result;
      }
  }
  ```

- [ ] Run tests → 5 passes

- [ ] Commit:
  ```bash
  git add Assets/Subsystems/AnimationPlayback/AnimationEvaluator.cs Assets/Subsystems/AnimationAuthoring/Tests/
  git commit -m "feat: add AnimationEvaluator with 5 passing tests"
  ```

---

## Task 4: PropertyApplicator

**Files:** `AnimationPlayback/PropertyApplicator.cs`

> `ChannelPath` format: `"bone:{BoneName}/position.x"`, `"bone:{BoneName}/rotation.x"`, etc.

- [ ] Create `Assets/Subsystems/AnimationPlayback/PropertyApplicator.cs`:
  ```csharp
  using System.Collections.Generic;
  using UnityEngine;

  public class PropertyApplicator
  {
      private readonly SceneGraph _sceneGraph;

      public PropertyApplicator(SceneGraph sceneGraph) => _sceneGraph = sceneGraph;

      public void Apply(ActionData action, Dictionary<string, float> values)
      {
          foreach (var (path, value) in values)
              ApplyChannel(action.TargetNodeId, path, value);
      }

      private void ApplyChannel(string nodeId, string channelPath, float value)
      {
          if (!channelPath.StartsWith("bone:")) return;

          // Parse "bone:{BoneName}/position.x"
          var withoutPrefix = channelPath["bone:".Length..];
          var slash = withoutPrefix.IndexOf('/');
          if (slash < 0) return;

          var boneName = withoutPrefix[..slash];
          var property = withoutPrefix[(slash + 1)..];

          var node = _sceneGraph.GetNode(nodeId);
          if (node == null) return;

          var smr = node.GetComponentInChildren<SkinnedMeshRenderer>();
          if (smr == null) return;

          Transform bone = null;
          foreach (var b in smr.bones)
              if (b.name == boneName) { bone = b; break; }
          if (bone == null) return;

          var pos = bone.localPosition;
          var rot = bone.localEulerAngles;

          switch (property)
          {
              case "position.x": pos.x = value; bone.localPosition = pos; break;
              case "position.y": pos.y = value; bone.localPosition = pos; break;
              case "position.z": pos.z = value; bone.localPosition = pos; break;
              case "rotation.x": rot.x = value; bone.localEulerAngles = rot; break;
              case "rotation.y": rot.y = value; bone.localEulerAngles = rot; break;
              case "rotation.z": rot.z = value; bone.localEulerAngles = rot; break;
          }
      }
  }
  ```

- [ ] Commit:
  ```bash
  git add Assets/Subsystems/AnimationPlayback/PropertyApplicator.cs
  git commit -m "feat: add PropertyApplicator — applies evaluated channel values to bone transforms"
  ```

---

## Task 5: TrackRecorder

**Files:** `AnimationAuthoring/TrackRecorder.cs`

- [ ] Create `Assets/Subsystems/AnimationAuthoring/TrackRecorder.cs`:
  ```csharp
  using System.Collections.Generic;
  using UnityEngine;
  using VContainer.Unity;

  public class TrackRecorder : IStartable, IDisposable
  {
      private readonly AnimationClock _clock;
      private readonly EventBus _bus;
      private readonly SelectionManager _selectionManager;
      private readonly SceneGraph _sceneGraph;

      private readonly Dictionary<string, ActionData> _actions = new();

      public IReadOnlyDictionary<string, ActionData> Actions => _actions;

      public TrackRecorder(AnimationClock clock, EventBus bus,
                            SelectionManager selectionManager, SceneGraph sceneGraph)
      {
          _clock            = clock;
          _bus              = bus;
          _selectionManager = selectionManager;
          _sceneGraph       = sceneGraph;
      }

      public void Start() { }
      public void Dispose() { }

      /// <summary>Called when user presses "Set Key" button.</summary>
      public void RecordKeyframe()
      {
          var nodeId = _selectionManager.SelectedNodeId;
          if (string.IsNullOrEmpty(nodeId)) return;

          // nodeId may be a bone proxy id like "bone_Hips"
          // Extract bone name and find it in scene
          if (!nodeId.StartsWith("bone_")) return;
          var boneName = nodeId["bone_".Length..];

          var parentNodeId = FindParentNodeId(boneName);
          if (parentNodeId == null) return;

          var node = _sceneGraph.GetNode(parentNodeId);
          var smr  = node?.GetComponentInChildren<SkinnedMeshRenderer>();
          if (smr == null) return;

          Transform bone = null;
          foreach (var b in smr.bones) if (b.name == boneName) { bone = b; break; }
          if (bone == null) return;

          var action = GetOrCreateAction(parentNodeId);
          var frame  = _clock.CurrentFrame;
          var pos    = bone.localPosition;
          var rot    = bone.localEulerAngles;

          action.GetOrCreateTrack($"bone:{boneName}/position.x").SetKey(frame, pos.x);
          action.GetOrCreateTrack($"bone:{boneName}/position.y").SetKey(frame, pos.y);
          action.GetOrCreateTrack($"bone:{boneName}/position.z").SetKey(frame, pos.z);
          action.GetOrCreateTrack($"bone:{boneName}/rotation.x").SetKey(frame, rot.x);
          action.GetOrCreateTrack($"bone:{boneName}/rotation.y").SetKey(frame, rot.y);
          action.GetOrCreateTrack($"bone:{boneName}/rotation.z").SetKey(frame, rot.z);

          _bus.Publish(new SceneModifiedEvent());
          Debug.Log($"Key set: {boneName} @ frame {frame}");
      }

      public ActionData GetOrCreateAction(string nodeId)
      {
          if (!_actions.ContainsKey(nodeId))
              _actions[nodeId] = new ActionData { ActionId = System.Guid.NewGuid().ToString("N")[..8], TargetNodeId = nodeId };
          return _actions[nodeId];
      }

      private string FindParentNodeId(string boneName)
      {
          // Walk scene graph nodes, find which one's SMR contains this bone
          foreach (var (id, node) in _sceneGraph.Nodes)
          {
              var smr = node.GetComponentInChildren<SkinnedMeshRenderer>();
              if (smr == null) continue;
              foreach (var b in smr.bones)
                  if (b.name == boneName) return id;
          }
          return null;
      }
  }
  ```

- [ ] Add to `VrEditingSceneScope`:
  ```csharp
  builder.Register<TrackRecorder>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
  builder.Register<PropertyApplicator>(Lifetime.Scoped);
  ```

- [ ] Commit:
  ```bash
  git add Assets/Subsystems/AnimationAuthoring/TrackRecorder.cs
  git commit -m "feat: add TrackRecorder — records bone transform keyframes at current frame"
  ```

---

## Task 6: PlaybackController

**Files:** `AnimationPlayback/PlaybackController.cs`

- [ ] Create `Assets/Subsystems/AnimationPlayback/PlaybackController.cs`:
  ```csharp
  using System.Collections.Generic;
  using VContainer.Unity;

  public class PlaybackController : IStartable, IDisposable
  {
      private readonly AnimationClock _clock;
      private readonly EventBus _bus;
      private readonly TrackRecorder _recorder;
      private readonly PropertyApplicator _applicator;

      public PlaybackController(AnimationClock clock, EventBus bus,
                                 TrackRecorder recorder, PropertyApplicator applicator)
      {
          _clock      = clock;
          _bus        = bus;
          _recorder   = recorder;
          _applicator = applicator;
      }

      public void Start() => _bus.Subscribe<FrameChangedEvent>(OnFrameChanged);
      public void Dispose() => _bus.Unsubscribe<FrameChangedEvent>(OnFrameChanged);

      public void Play()  { _clock.Play();  PublishState(); }
      public void Pause() { _clock.Pause(); PublishState(); }
      public void Stop()  { _clock.Stop();  PublishState(); }
      public void Scrub(int frame) { _clock.SetFrame(frame); PublishState(); }
      public void SetSpeed(float multiplier) => _clock.SpeedMultiplier = multiplier;
      public void SetLoop(bool loop) => _clock.IsLooping = loop;

      private void OnFrameChanged(FrameChangedEvent e)
      {
          if (!_clock.IsPlaying) return;

          foreach (var (nodeId, action) in _recorder.Actions)
          {
              var values = AnimationEvaluator.EvaluateAction(action, e.Frame);
              _applicator.Apply(action, values);
          }
      }

      private void PublishState() =>
          _bus.Publish(new PlaybackStateChangedEvent { IsPlaying = _clock.IsPlaying, Frame = _clock.CurrentFrame });
  }
  ```

- [ ] Add to `VrEditingSceneScope`:
  ```csharp
  builder.Register<PlaybackController>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
  ```

- [ ] Commit:
  ```bash
  git add Assets/Subsystems/AnimationPlayback/PlaybackController.cs
  git commit -m "feat: add PlaybackController — evaluates ActionData per frame during playback"
  ```

---

## Task 7: KeyframeEditorPanel + Transport UI

**Files:** `AnimationAuthoring/UI/KeyframeEditorPanel.cs`, ToolbarPanel transport buttons

- [ ] Create `Assets/Subsystems/AnimationAuthoring/UI/KeyframeEditorPanel.cs`:
  ```csharp
  using UnityEngine;
  using UnityEngine.UI;
  using VContainer;
  using TMPro;

  public class KeyframeEditorPanel : SpatialPanel
  {
      [SerializeField] private Slider _timelineSlider;
      [SerializeField] private TMP_Text _frameLabel;
      [SerializeField] private Button _setKeyButton;
      [SerializeField] private Button _playButton;
      [SerializeField] private Button _pauseButton;
      [SerializeField] private Button _stopButton;

      private PlaybackController _playback;
      private TrackRecorder _recorder;
      private AnimationClock _clock;
      private EventBus _bus;

      [Inject]
      public void Construct(PlaybackController playback, TrackRecorder recorder,
                             AnimationClock clock, EventBus bus)
      {
          _playback = playback;
          _recorder = recorder;
          _clock    = clock;
          _bus      = bus;
      }

      private void Start()
      {
          _setKeyButton.onClick.AddListener(() => _recorder.RecordKeyframe());
          _playButton.onClick.AddListener(() => _playback.Play());
          _pauseButton.onClick.AddListener(() => _playback.Pause());
          _stopButton.onClick.AddListener(() => _playback.Stop());

          _timelineSlider.minValue = _clock.FrameStart;
          _timelineSlider.maxValue = _clock.FrameEnd;
          _timelineSlider.onValueChanged.AddListener(v => _playback.Scrub((int)v));

          _bus.Subscribe<FrameChangedEvent>(OnFrameChanged);
          _bus.Subscribe<PlaybackStateChangedEvent>(OnPlaybackStateChanged);
      }

      private void OnDestroy()
      {
          _bus.Unsubscribe<FrameChangedEvent>(OnFrameChanged);
          _bus.Unsubscribe<PlaybackStateChangedEvent>(OnPlaybackStateChanged);
      }

      private void OnFrameChanged(FrameChangedEvent e)
      {
          _frameLabel.text = $"Frame: {e.Frame}";
          _timelineSlider.SetValueWithoutNotify(e.Frame);
      }

      private void OnPlaybackStateChanged(PlaybackStateChangedEvent e)
      {
          _playButton.interactable  = !e.IsPlaying;
          _pauseButton.interactable = e.IsPlaying;
      }
  }
  ```

  > Transport controls (Play/Pause/Stop) live in `KeyframeEditorPanel`, not in `ToolbarPanel`. ToolbarPanel's transport area is reserved for a future compact strip; for the demo, open KeyframeEditorPanel to access transport.

- [ ] **In Unity Editor:**
  1. Create `KeyframeEditorPanel` prefab (World Space Canvas): timeline Slider, frame TMP_Text, Set Key / Play / Pause / Stop buttons
  2. Add to `PanelRegistry.asset`: VisibleInModes = [VrEditing]
  3. Wire component fields

- [ ] Press Play → import model → build rig → select bone proxy → scrub to frame 0 → click "Set Key" → scrub to frame 30 → manually move bone → click "Set Key" → click "Play" → bone animates between the two keyframes

- [ ] Commit:
  ```bash
  git add Assets/Subsystems/AnimationAuthoring/UI/ Assets/Scenes/VrEditing.unity
  git commit -m "feat: KeyframeEditorPanel with timeline scrubber, Set Key, transport controls"
  ```

---

## Phase 7 Verification

- [ ] AnimationEvaluator tests: 5 passing
- [ ] Set keyframe at frame 0 → move bone → set keyframe at frame 30 → Play → bone interpolates between positions
- [ ] Stop → bone resets to frame 0
- [ ] Scrub slider updates bone position in real time
- [ ] `PlaybackStateChangedEvent` fires (Play/Pause buttons toggle interactability)
