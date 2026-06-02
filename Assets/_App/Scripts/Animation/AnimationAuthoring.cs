using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

public class AnimationAuthoring : IStartable, ITickable, IDisposable
{
    private readonly AnimationClock _clock;
    private readonly ISceneGraph    _sceneGraph;
    private readonly PathProvider   _paths;
    private readonly AppStorage     _storage;
    private readonly EventBus       _bus;

    private SceneAnimationData                     _data;
    private readonly Dictionary<string, AnimationClip> _clips = new();
    private readonly Dictionary<string, float> _loopCursors = new();
    private readonly Dictionary<string, Dictionary<string, AnimationClip>> _loopClips = new();
    private string _sceneId;
    private string _activeContainerOwner;

    private CancellationTokenSource _saveCts;
    private const int SAVE_DEBOUNCE_MS = 200;

    public AnimationAuthoring(AnimationClock clock, ISceneGraph sceneGraph,
                               PathProvider paths, AppStorage storage, EventBus bus)
    {
        _clock      = clock;
        _sceneGraph = sceneGraph;
        _paths      = paths;
        _storage    = storage;
        _bus        = bus;
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
        foreach (var t in c.Tracks) _clips[t.NodeId] = BuildClip(t, GetSceneFps(), c.Interpolation);
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
        foreach (var t in c.Tracks) clips[t.NodeId] = BuildClip(t, GetSceneFps(), c.Interpolation);
        _loopClips[ownerNodeId]   = clips;
        _loopCursors[ownerNodeId] = Mathf.Clamp(startFrame, 0, c.TotalFrames);
    }

    public void StopLoopPlayback(string ownerNodeId)
    {
        _loopCursors.Remove(ownerNodeId);
        _loopClips.Remove(ownerNodeId);
    }

    private void RebuildLoopClips(string owner)
    {
        if (!_loopClips.ContainsKey(owner)) return;
        var c = _data?.FindByOwner(owner);
        if (c == null) return;
        var clips = new Dictionary<string, AnimationClip>();
        foreach (var t in c.Tracks) clips[t.NodeId] = BuildClip(t, GetSceneFps(), c.Interpolation);
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

    private void RequestSave()
    {
        _saveCts?.Cancel();
        _saveCts = new CancellationTokenSource();
        _ = DebouncedSave(_saveCts.Token);
    }

    private async Task DebouncedSave(CancellationToken ct)
    {
        try
        {
            await Task.Delay(SAVE_DEBOUNCE_MS, ct);
            if (ct.IsCancellationRequested) return;
            await SaveAsync(ct);
        }
        catch (TaskCanceledException) { }
    }

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
        _saveCts?.Cancel();
        _saveCts?.Dispose();
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

    private void ApplyFrame(int frame)
    {
        if (string.IsNullOrEmpty(_activeContainerOwner)) return;
        if (_loopCursors.ContainsKey(_activeContainerOwner)) return; // background loop owns sampling
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
        var path = _paths.AnimationJson(sceneId);

        if (!File.Exists(path))
        {
            _data = new SceneAnimationData();
            return;
        }

        try
        {
            var json   = await File.ReadAllTextAsync(path, ct);
            var loaded = JsonUtility.FromJson<SceneAnimationData>(json);

            if (loaded == null || loaded.schemaVersion < 2)
            {
                Debug.LogWarning(
                    $"AnimationAuthoring: discarding old animation data at '{path}' (schemaVersion={loaded?.schemaVersion ?? 0}). Starting fresh.");
                try { File.Delete(path); }
                catch (Exception delEx)
                {
                    Debug.LogError($"AnimationAuthoring: failed to delete v1 file '{path}': {delEx.Message}");
                }
                _data = new SceneAnimationData();
                return;
            }

            if (loaded.schemaVersion > 2)
            {
                Debug.LogError(
                    $"AnimationAuthoring: animation file '{path}' has schemaVersion={loaded.schemaVersion} (newer than supported 2). Opening empty in-memory data; file NOT touched.");
                _data = new SceneAnimationData();
                return;
            }

            if (loaded.Fps <= 0)
                loaded.Fps = loaded.Containers.Count > 0 ? Mathf.Max(1, loaded.Containers[0].Fps) : 24;

            _data = loaded;
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
}
