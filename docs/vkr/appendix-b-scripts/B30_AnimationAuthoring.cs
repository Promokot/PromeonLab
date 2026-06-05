using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

public class AnimationAuthoring : IStartable, IDisposable
{
    private readonly ISceneGraph              _sceneGraph;
    private readonly AnimationStorage         _animStorage;
    private readonly AppStorage               _storage;
    private readonly AnimationPlaybackSampler _sampler;
    private readonly EventBus                 _bus;

    private SceneAnimationData _data;
    private string _sceneId;
    private string _activeContainerOwner; 

    public AnimationAuthoring(ISceneGraph sceneGraph, AnimationStorage animStorage,
                               AppStorage storage, AnimationPlaybackSampler sampler, EventBus bus)
    {
        _sceneGraph  = sceneGraph;
        _animStorage = animStorage;
        _storage     = storage;
        _sampler     = sampler;
        _bus         = bus;
        _sampler?.Bind(() => _data);
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
        _sampler?.OnDataChanged(null);
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
        _sampler?.OnDataChanged(null);
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
        _sampler?.OnDataChanged(null);
    }

    public void SetActiveContainerOwner(string ownerNodeId)
    {
        _activeContainerOwner = ownerNodeId;
        _sampler?.SetActiveContainerOwner(ownerNodeId);
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
        _sampler?.OnDataChanged(ownerNodeId);
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
        _sampler?.OnDataChanged(null);
    }

    public int GetSceneFps() => _data?.Fps ?? 24;

    public InterpolationMode GetInterpolation(string ownerNodeId) =>
        _data?.FindByOwner(ownerNodeId)?.Interpolation ?? InterpolationMode.Linear;

    public bool IsLooping(string ownerNodeId) => _data?.FindByOwner(ownerNodeId)?.Loop ?? false;

    public bool IsLoopPlaying(string ownerNodeId) => _sampler?.IsLoopPlaying(ownerNodeId) ?? false;

    public void SetLoop(string ownerNodeId, bool loop)
    {
        var c = _data?.FindByOwner(ownerNodeId);
        if (c == null) return;
        c.Loop = loop;
        if (!loop) _sampler?.StopLoopPlayback(ownerNodeId);
        _bus.Publish(new AnimationContainerChangedEvent
        {
            OwnerNodeId = ownerNodeId,
            Change      = ContainerChange.LoopChanged
        });
        RequestSave();
    }

    public void StartLoopPlayback(string ownerNodeId, int startFrame) =>
        _sampler?.StartLoopPlayback(ownerNodeId, startFrame);

    public void StopLoopPlayback(string ownerNodeId) =>
        _sampler?.StopLoopPlayback(ownerNodeId);

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
        _sampler?.OnDataChanged(ownerNodeId);
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
        _sampler?.OnDataChanged(null);
        _sampler?.RebuildAllLoopClips();
    }

    internal void InitForTest() => _data = new SceneAnimationData();

    private void RequestSave() => _animStorage?.RequestSave(_data, _sceneId);

    public void Start()
    {
        _bus.Subscribe<SceneOpenedEvent>(OnSceneOpened);

        var activeId = _storage.ActiveSceneId;
        if (!string.IsNullOrEmpty(activeId))
            _ = LoadAsync(activeId, CancellationToken.None);
    }

    public void Dispose() => _bus.Unsubscribe<SceneOpenedEvent>(OnSceneOpened);

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
        _sampler?.OnDataChanged(owner);
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
        _sampler?.OnDataChanged(owner);
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
        _sampler?.OnDataChanged(ownerNodeId);
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

    private async Task LoadAsync(string sceneId, CancellationToken ct)
    {
        _sceneId = sceneId;
        _data = _animStorage != null
            ? await _animStorage.LoadAsync(sceneId, ct)
            : new SceneAnimationData();
    }

    private void EnsureData() => _data ??= new SceneAnimationData();
}
