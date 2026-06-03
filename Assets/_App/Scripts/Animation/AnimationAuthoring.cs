using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

public class AnimationAuthoring : IStartable, ITickable, IDisposable
{
    private readonly AnimationClock   _clock;
    private readonly ISceneGraph      _sceneGraph;
    private readonly AnimationStorage _animStorage;
    private readonly AppStorage       _storage;
    private readonly EventBus         _bus;

    private SceneAnimationData                     _data;
    private readonly Dictionary<string, AnimationClip> _clips = new();
    private readonly Dictionary<string, float> _loopCursors = new();
    private readonly Dictionary<string, Dictionary<string, AnimationClip>> _loopClips = new();
    private readonly Dictionary<string, int> _loopLastFrame = new();
    private string _sceneId;
    private string _activeContainerOwner;

    public AnimationAuthoring(AnimationClock clock, ISceneGraph sceneGraph,
                               AnimationStorage animStorage, AppStorage storage, EventBus bus)
    {
        _clock       = clock;
        _sceneGraph  = sceneGraph;
        _animStorage = animStorage;
        _storage     = storage;
        _bus         = bus;
    }

    public static string OwnerOf(string nodeId)
    {
        if (nodeId == null) return null;
        if (!nodeId.StartsWith("bone:")) return nodeId;
        var parts = nodeId.Split(':');
        return parts.Length >= 2 ? parts[1] : nodeId;
    }

    public bool HasContainer(string ownerNodeId) =>
        _data?.FindByOwner(ownerNodeId) != null;

    public ActionContainer GetContainer(string ownerNodeId) =>
        _data?.FindByOwner(ownerNodeId);

    /// <summary>
    /// Read-only access to the live scene animation data for export. Returns null when no
    /// animation data has been created/loaded yet (e.g. Sandbox, or an untouched scene).
    /// </summary>
    public SceneAnimationData CaptureForExport() => _data;

    public ActionContainer CreateContainer(string ownerNodeId)
    {
        EnsureData();
        return FinishCreate(ownerNodeId, _data.CreateContainer(ownerNodeId));
    }

    public ActionContainer CreateContainer(string ownerNodeId, int totalFrames, int fps)
    {
        EnsureData();
        return FinishCreate(ownerNodeId,
            _data.CreateContainer(ownerNodeId, Mathf.Max(1, totalFrames), Mathf.Max(1, fps)));
    }

    private ActionContainer FinishCreate(string ownerNodeId, ActionContainer c)
    {
        _bus.Publish(new AnimationContainerChangedEvent
        {
            OwnerNodeId = ownerNodeId,
            Change      = ContainerChange.Added
        });
        RequestSave();
        RebuildActiveClips();
        return c;
    }

    public void EnsureTrack(string ownerNodeId, string trackNodeId)
    {
        var c = _data?.FindByOwner(ownerNodeId);
        if (c == null) return;
        if (c.FindTrack(trackNodeId) != null) return;
        c.GetOrCreateTrack(trackNodeId);
        _bus.Publish(new AnimationContainerChangedEvent
        {
            OwnerNodeId = ownerNodeId,
            Change      = ContainerChange.TracksChanged
        });
        RequestSave();
        RebuildActiveClips();
    }

    public void RemoveContainer(string ownerNodeId)
    {
        if (_data == null) return;
        if (_data.FindByOwner(ownerNodeId) == null) return;
        _data.RemoveContainer(ownerNodeId);
        _bus.Publish(new AnimationContainerChangedEvent
        {
            OwnerNodeId = ownerNodeId,
            Change      = ContainerChange.Removed
        });
        RequestSave();
        RebuildActiveClips();
    }

    public void SetActiveContainerOwner(string ownerNodeId)
    {
        _activeContainerOwner = ownerNodeId;
        RebuildActiveClips();
    }

    private void RebuildActiveClips()
    {
        _clips.Clear();
        if (string.IsNullOrEmpty(_activeContainerOwner)) return;
        var c = _data?.FindByOwner(_activeContainerOwner);
        if (c == null) return;
        foreach (var t in c.Tracks) _clips[t.NodeId] = AnimationClipBaker.BuildClip(t, GetSceneFps(), c.Interpolation);
    }

    public void SetTotalFrames(string ownerNodeId, int frames)
    {
        var c = _data?.FindByOwner(ownerNodeId);
        if (c == null) return;
        c.TotalFrames = Mathf.Max(1, frames);
        c.TruncateToTotalFrames();
        _bus.Publish(new AnimationContainerChangedEvent
        {
            OwnerNodeId = ownerNodeId,
            Change      = ContainerChange.LengthChanged
        });
        RequestSave();
        RebuildActiveClips();
        RebuildLoopClips(ownerNodeId);
    }

    public void SetFps(string ownerNodeId, int fps)
    {
        var c = _data?.FindByOwner(ownerNodeId);
        if (c == null) return;
        c.Fps = Mathf.Max(1, fps);
        _bus.Publish(new AnimationContainerChangedEvent
        {
            OwnerNodeId = ownerNodeId,
            Change      = ContainerChange.FpsChanged
        });
        RequestSave();
        RebuildActiveClips();
    }

    public int GetSceneFps() => _data?.Fps ?? 24;

    public InterpolationMode GetInterpolation(string ownerNodeId) =>
        _data?.FindByOwner(ownerNodeId)?.Interpolation ?? InterpolationMode.Linear;

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
        foreach (var t in c.Tracks) clips[t.NodeId] = AnimationClipBaker.BuildClip(t, GetSceneFps(), c.Interpolation);
        _loopClips[ownerNodeId]   = clips;
        _loopCursors[ownerNodeId] = Mathf.Clamp(startFrame, 0, c.TotalFrames);
    }

    public void StopLoopPlayback(string ownerNodeId)
    {
        _loopCursors.Remove(ownerNodeId);
        _loopClips.Remove(ownerNodeId);
        _loopLastFrame.Remove(ownerNodeId);
    }

    private void RebuildLoopClips(string owner)
    {
        if (!_loopClips.ContainsKey(owner)) return;
        var c = _data?.FindByOwner(owner);
        if (c == null) return;
        var clips = new Dictionary<string, AnimationClip>();
        foreach (var t in c.Tracks) clips[t.NodeId] = AnimationClipBaker.BuildClip(t, GetSceneFps(), c.Interpolation);
        _loopClips[owner] = clips;
    }

    internal static float AdvanceLoopCursor(float cursor, float deltaFrames, int total)
    {
        if (total <= 0) return 0f;
        float c = cursor + deltaFrames;
        while (c >= total) c -= total;
        if (c < 0f) c = 0f;
        return c;
    }

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
        RebuildLoopClips(ownerNodeId);
    }

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
        foreach (var owner in new System.Collections.Generic.List<string>(_loopClips.Keys))
            RebuildLoopClips(owner);
    }

    internal void InitForTest()
    {
        _data = new SceneAnimationData();
        _bus.Subscribe<PlaybackStateChangedEvent>(OnPlaybackState);
    }

    private void RequestSave() => _animStorage?.RequestSave(_data, _sceneId);

    public void Start()
    {
        _bus.Subscribe<SceneOpenedEvent>(OnSceneOpened);
        _bus.Subscribe<FrameChangedEvent>(OnFrameChanged);
        _bus.Subscribe<PlaybackStateChangedEvent>(OnPlaybackState);

        var activeId = _storage.ActiveSceneId;
        if (!string.IsNullOrEmpty(activeId))
            _ = LoadAsync(activeId, CancellationToken.None);
    }

    public void Dispose()
    {
        _bus.Unsubscribe<SceneOpenedEvent>(OnSceneOpened);
        _bus.Unsubscribe<FrameChangedEvent>(OnFrameChanged);
        _bus.Unsubscribe<PlaybackStateChangedEvent>(OnPlaybackState);
        _loopCursors.Clear();
        _loopClips.Clear();
    }

    public void SetKey(string nodeId, int frame)
    {
        var go = _sceneGraph?.GetNode(nodeId);
        if (go == null) return;
        SetKey(nodeId, frame, go.transform.localPosition, go.transform.localRotation, go.transform.localScale);
    }

    public void SetKey(string nodeId, int frame, Vector3 pos, Quaternion rot, Vector3 scale)
    {
        var owner = OwnerOf(nodeId);
        if (string.IsNullOrEmpty(owner)) return;
        EnsureData();
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
        RebuildLoopClips(owner);
    }

    public void DeleteKey(string nodeId, int frame)
    {
        var owner = OwnerOf(nodeId);
        var c     = _data?.FindByOwner(owner);
        var track = c?.FindTrack(nodeId);
        if (track == null) return;

        if (!track.HasKey(frame)) return;
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
        RebuildLoopClips(owner);
    }

    public bool HasKey(string nodeId, int frame)
    {
        var owner = OwnerOf(nodeId);
        return _data?.FindByOwner(owner)?.FindTrack(nodeId)?.HasKey(frame) ?? false;
    }

    public IReadOnlyList<int> GetKeyFrames(string nodeId)
    {
        var owner = OwnerOf(nodeId);
        var track = _data?.FindByOwner(owner)?.FindTrack(nodeId);
        if (track == null) return System.Array.Empty<int>();
        var frames = new int[track.Keys.Count];
        for (int i = 0; i < track.Keys.Count; i++) frames[i] = track.Keys[i].Frame;
        return frames;
    }

    public void SetKeyForFrame(string ownerNodeId, string activeNodeId, int frame)
    {
        var c = _data?.FindByOwner(ownerNodeId);
        if (c == null) return;

        var snapshots = new Dictionary<string, (Vector3, Quaternion, Vector3)>();

        if (!string.IsNullOrEmpty(activeNodeId) && _sceneGraph != null)
        {
            var go = _sceneGraph.GetNode(activeNodeId);
            if (go != null)
                snapshots[activeNodeId] = (go.transform.localPosition, go.transform.localRotation, go.transform.localScale);
        }

        foreach (var t in c.Tracks)
        {
            if (snapshots.ContainsKey(t.NodeId)) continue;
            var go = _sceneGraph?.GetNode(t.NodeId);
            if (go == null) continue;
            snapshots[t.NodeId] = (go.transform.localPosition, go.transform.localRotation, go.transform.localScale);
        }

        SetKeyForFrame_Test(ownerNodeId, activeNodeId, frame, snapshots);
    }

    internal void SetKeyForFrame_Test(
        string ownerNodeId, string activeNodeId, int frame,
        Dictionary<string, (Vector3 Pos, Quaternion Rot, Vector3 Scale)> snapshots)
    {
        var c = _data?.FindByOwner(ownerNodeId);
        if (c == null) return;

        if (!string.IsNullOrEmpty(activeNodeId) && snapshots.TryGetValue(activeNodeId, out var aSnap))
        {
            SetKey(activeNodeId, frame, aSnap.Pos, aSnap.Rot, aSnap.Scale);
        }

        var existingIds = new List<string>(c.ExistingTrackNodeIds());
        foreach (var tid in existingIds)
        {
            if (tid == activeNodeId) continue;
            if (!snapshots.TryGetValue(tid, out var snap)) continue;
            SetKey(tid, frame, snap.Pos, snap.Rot, snap.Scale);
        }
    }

    public void DeleteAllKeysAtFrame(string ownerNodeId, int frame)
    {
        var c = _data?.FindByOwner(ownerNodeId);
        if (c == null) return;

        var trackIds = new List<string>(c.ExistingTrackNodeIds());
        foreach (var id in trackIds)
            DeleteKey(id, frame);
    }

    public FrameClipboard CopyFrame(string ownerNodeId, int frame)
    {
        var clip = new FrameClipboard { OwnerNodeId = ownerNodeId, SourceFrame = frame };
        var c    = _data?.FindByOwner(ownerNodeId);
        if (c == null) return clip;

        foreach (var t in c.Tracks)
        {
            foreach (var k in t.Keys)
            {
                if (k.Frame != frame) continue;
                clip.Entries.Add(new FrameClipboardEntry
                {
                    TrackNodeId = t.NodeId,
                    Position    = k.Position,
                    Rotation    = k.Rotation,
                    Scale       = k.Scale
                });
                break;
            }
        }
        return clip;
    }

    public void PasteFrame(string ownerNodeId, int frame, FrameClipboard clip)
    {
        if (clip == null || clip.IsEmpty) return;
        var c = _data?.FindByOwner(ownerNodeId);
        if (c == null) return;

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
        RequestSave();
        RebuildActiveClips();
        RebuildLoopClips(ownerNodeId);
    }

    public int? NearestKeyBefore(string ownerNodeId, int frame)
    {
        var c = _data?.FindByOwner(ownerNodeId);
        if (c == null) return null;

        int? best = null;
        foreach (var t in c.Tracks)
            foreach (var k in t.Keys)
                if (k.Frame < frame && (!best.HasValue || k.Frame > best.Value))
                    best = k.Frame;
        return best;
    }

    public int? NearestKeyAfter(string ownerNodeId, int frame)
    {
        var c = _data?.FindByOwner(ownerNodeId);
        if (c == null) return null;

        int? best = null;
        foreach (var t in c.Tracks)
            foreach (var k in t.Keys)
                if (k.Frame > frame && (!best.HasValue || k.Frame < best.Value))
                    best = k.Frame;
        return best;
    }

    private void OnSceneOpened(SceneOpenedEvent e) =>
        _ = LoadAsync(e.SceneId, CancellationToken.None);

    private void OnFrameChanged(FrameChangedEvent e)
    {
        if (_data == null) return;
        ApplyFrame(e.Frame);
    }

    private void OnPlaybackState(PlaybackStateChangedEvent e)
    {
        if (e.Completed) ApplyFrame(0);
    }

    public void Tick()
    {
        if (_data == null) return;
        float fps = GetSceneFps();

        // Background loops: each looping owner advances on its own cursor and samples per render frame.
        if (_loopCursors.Count > 0)
        {
            foreach (var owner in new List<string>(_loopCursors.Keys)) // snapshot: StopLoopPlayback mutates
            {
                var c = _data.FindByOwner(owner);
                if (c == null || !c.Loop) { StopLoopPlayback(owner); continue; }
                float cursor = AdvanceLoopCursor(_loopCursors[owner], Time.deltaTime * fps, c.TotalFrames);
                _loopCursors[owner] = cursor;
                if (_loopClips.TryGetValue(owner, out var clips))
                    SampleContainerAt(c, clips, cursor / Mathf.Max(1f, fps));
                PublishLoopFrameIfChanged(owner, cursor);
            }
        }

        // Direct transport playback of the SELECTED non-looping container: sample at the clock's
        // FRACTIONAL position every render frame, so it is as smooth as loop playback. ApplyFrame's
        // integer-frame sampling (driven by FrameChangedEvent) only steps the playhead during play —
        // sampling the pose there would quantize motion to the animation fps. Scrubbing while paused
        // still goes through ApplyFrame (see its IsPlaying guard).
        if (_clock != null && _clock.IsPlaying
            && !string.IsNullOrEmpty(_activeContainerOwner)
            && !_loopCursors.ContainsKey(_activeContainerOwner)
            && fps > 0f)
        {
            var c = _data.FindByOwner(_activeContainerOwner);
            if (c != null) SampleContainerAt(c, _clips, _clock.CurrentFrameContinuous / fps);
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

    // Publishes a LoopFrameChangedEvent for an owner only when its integer frame changes, so the
    // playhead steps once per frame rather than every tick. Internal for EditMode testing.
    internal void PublishLoopFrameIfChanged(string owner, float cursor)
    {
        int frame = UnityEngine.Mathf.FloorToInt(cursor);
        if (_loopLastFrame.TryGetValue(owner, out var last) && last == frame) return;
        _loopLastFrame[owner] = frame;
        _bus.Publish(new LoopFrameChangedEvent { OwnerNodeId = owner, Frame = frame });
    }

    private void ApplyFrame(int frame)
    {
        if (string.IsNullOrEmpty(_activeContainerOwner)) return;
        if (_loopCursors.ContainsKey(_activeContainerOwner)) return; // background loop owns sampling
        // During transport playback Tick samples continuously (smooth); this integer path would quantize
        // the pose to the fps. It still runs for scrub/seek/stop (when the clock is not playing).
        if (_clock != null && _clock.IsPlaying) return;
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

    private async Task LoadAsync(string sceneId, CancellationToken ct)
    {
        _sceneId = sceneId;
        _data = _animStorage != null
            ? await _animStorage.LoadAsync(sceneId, ct)
            : new SceneAnimationData();
    }

    private void EnsureData() => _data ??= new SceneAnimationData();
}
