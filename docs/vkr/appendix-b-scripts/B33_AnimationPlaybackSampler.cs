using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

// Owns all runtime sampling extracted from AnimationAuthoring (A1): background per-object loops and
// transport playback of the selected container, plus paused scrub. Reads the live SceneAnimationData
// through a data source bound by the authoring façade, so it never holds a stale copy.
public class AnimationPlaybackSampler : ITickable, IDisposable
{
    private readonly AnimationClock _clock;
    private readonly ISceneGraph    _graph;
    private readonly EventBus        _bus;

    private Func<SceneAnimationData> _dataSource = () => null;

    private readonly Dictionary<string, AnimationClip> _clips = new();
    private readonly Dictionary<string, float> _loopCursors = new();
    private readonly Dictionary<string, Dictionary<string, AnimationClip>> _loopClips = new();
    private readonly Dictionary<string, int> _loopLastFrame = new();
    private string _activeContainerOwner;

    public AnimationPlaybackSampler(AnimationClock clock, ISceneGraph graph, EventBus bus)
    {
        _clock = clock;
        _graph = graph;
        _bus   = bus;
        _bus.Subscribe<FrameChangedEvent>(OnFrameChanged);
        _bus.Subscribe<PlaybackStateChangedEvent>(OnPlaybackState);
    }

    // The façade calls this once with () => _data so the sampler always reads the live document.
    public void Bind(Func<SceneAnimationData> dataSource) => _dataSource = dataSource ?? (() => null);

    private SceneAnimationData Data => _dataSource();
    private int Fps => Data?.Fps ?? 24;

    public bool IsLoopPlaying(string ownerNodeId) => _loopCursors.ContainsKey(ownerNodeId);

    public void SetActiveContainerOwner(string ownerNodeId)
    {
        _activeContainerOwner = ownerNodeId;
        RebuildActiveClips();
    }

    // Rebuild the active clips (and, if it is looping, that owner's loop clips) after a data mutation.
    public void OnDataChanged(string ownerNodeId)
    {
        RebuildActiveClips();
        if (!string.IsNullOrEmpty(ownerNodeId)) RebuildLoopClips(ownerNodeId);
    }

    public void RebuildAllLoopClips()
    {
        foreach (var owner in new List<string>(_loopClips.Keys)) RebuildLoopClips(owner);
    }

    private void RebuildActiveClips()
    {
        _clips.Clear();
        if (string.IsNullOrEmpty(_activeContainerOwner)) return;
        var c = Data?.FindByOwner(_activeContainerOwner);
        if (c == null) return;
        foreach (var t in c.Tracks) _clips[t.NodeId] = AnimationClipBaker.BuildClip(t, Fps, c.Interpolation);
    }

    public void StartLoopPlayback(string ownerNodeId, int startFrame)
    {
        var c = Data?.FindByOwner(ownerNodeId);
        if (c == null || !c.Loop) return;
        var clips = new Dictionary<string, AnimationClip>();
        foreach (var t in c.Tracks) clips[t.NodeId] = AnimationClipBaker.BuildClip(t, Fps, c.Interpolation);
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
        var c = Data?.FindByOwner(owner);
        if (c == null) return;
        var clips = new Dictionary<string, AnimationClip>();
        foreach (var t in c.Tracks) clips[t.NodeId] = AnimationClipBaker.BuildClip(t, Fps, c.Interpolation);
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

    public void Tick()
    {
        var data = Data;
        if (data == null) return;
        float fps = Fps;

        // Background loops: each looping owner advances on its own cursor and samples per render frame.
        if (_loopCursors.Count > 0)
        {
            foreach (var owner in new List<string>(_loopCursors.Keys)) // snapshot: StopLoopPlayback mutates
            {
                var c = data.FindByOwner(owner);
                if (c == null || !c.Loop) { StopLoopPlayback(owner); continue; }
                float cursor = AdvanceLoopCursor(_loopCursors[owner], Time.deltaTime * fps, c.TotalFrames);
                _loopCursors[owner] = cursor;
                if (_loopClips.TryGetValue(owner, out var clips))
                    Sample(c, clips, cursor / Mathf.Max(1f, fps));
                PublishLoopFrameIfChanged(owner, cursor);
            }
        }

        // Direct transport playback of the SELECTED non-looping container: sample at the clock's
        // FRACTIONAL position every render frame, so it is as smooth as loop playback.
        if (_clock != null && _clock.IsPlaying
            && !string.IsNullOrEmpty(_activeContainerOwner)
            && !_loopCursors.ContainsKey(_activeContainerOwner)
            && fps > 0f)
        {
            var c = data.FindByOwner(_activeContainerOwner);
            if (c != null) Sample(c, _clips, _clock.CurrentFrameContinuous / fps);
        }
    }

    // B3: the single sampling body shared by playback, background loops, and paused scrub. Scrub passes
    // the integer frame as seconds (frame/fps); playback passes the continuous position. No divergence.
    private void Sample(ActionContainer c, Dictionary<string, AnimationClip> clips, float seconds)
    {
        foreach (var track in c.Tracks)
        {
            if (!clips.TryGetValue(track.NodeId, out var clip)) continue;
            var go = _graph?.GetNode(track.NodeId);
            if (go == null) continue;
            clip.SampleAnimation(go, seconds);
        }
    }

    // Publishes a LoopFrameChangedEvent for an owner only when its integer frame changes, so the
    // playhead steps once per frame rather than every tick. Internal for EditMode testing.
    internal void PublishLoopFrameIfChanged(string owner, float cursor)
    {
        int frame = Mathf.FloorToInt(cursor);
        if (_loopLastFrame.TryGetValue(owner, out var last) && last == frame) return;
        _loopLastFrame[owner] = frame;
        _bus.Publish(new LoopFrameChangedEvent { OwnerNodeId = owner, Frame = frame });
    }

    private void OnFrameChanged(FrameChangedEvent e)
    {
        if (Data == null) return;
        ApplyFrame(e.Frame);
    }

    private void OnPlaybackState(PlaybackStateChangedEvent e)
    {
        if (e.Completed) ApplyFrame(0);
    }

    // Integer-frame path: scrub/seek/stop while the clock is not playing. During playback Tick samples
    // continuously (smooth) and this early-returns so the pose is not quantized to the fps.
    private void ApplyFrame(int frame)
    {
        if (string.IsNullOrEmpty(_activeContainerOwner)) return;
        if (_loopCursors.ContainsKey(_activeContainerOwner)) return; // background loop owns sampling
        if (_clock != null && _clock.IsPlaying) return;
        var c = Data?.FindByOwner(_activeContainerOwner);
        if (c == null) return;
        int fps = Fps;
        if (fps <= 0) return;
        Sample(c, _clips, (float)frame / fps);
    }

    public void Dispose()
    {
        _bus.Unsubscribe<FrameChangedEvent>(OnFrameChanged);
        _bus.Unsubscribe<PlaybackStateChangedEvent>(OnPlaybackState);
        _loopCursors.Clear();
        _loopClips.Clear();
    }
}
