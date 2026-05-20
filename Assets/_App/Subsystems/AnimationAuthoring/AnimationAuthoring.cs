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

    private SceneAnimationData                     _data;
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

        _clips[track.NodeId] = clip;
    }
}
