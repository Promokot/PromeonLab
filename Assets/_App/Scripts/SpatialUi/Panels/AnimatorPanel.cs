using System.Collections.Generic;
using UnityEngine;
using VContainer;

public class AnimatorPanel : MonoBehaviour
{
    [SerializeField] private AnimatorPanelConfig   _config;
    [SerializeField] private RectTransform         _timelineContent;
    [SerializeField] private AnimatorSubToolbar    _toolbar;
    [SerializeField] private AnimatorSubTransport  _transport;
    [SerializeField] private AnimatorSubEmptyState _emptyState;
    [SerializeField] private GameObject            _activeStateRoot;
    [SerializeField] private AnimatorSubRuler      _ruler;
    [SerializeField] private AnimatorSubLanes      _lanes;
    [SerializeField] private AnimatorSubPlayhead   _playhead;
    [SerializeField] private TimelineScrubInput    _timelineInput;
    [SerializeField] private RectTransform         _tracksColumnContent;
    [SerializeField] private TrackRow              _trackRowPrefab;

    private EventBus           _bus;
    private AnimationClipboard _clipboard;
    private SceneContext       _ctx;

    private string                   _activeOwner;
    private readonly List<TrackRow> _rowPool = new();

    [Inject]
    public void Construct(EventBus bus, AnimationClipboard clipboard, SceneContext ctx)
    {
        _bus       = bus;
        _clipboard = clipboard;
        _ctx       = ctx;
    }

    private void OnEnable()
    {
        if (_bus == null) return;
        _bus.Subscribe<SceneContextChangedEvent>(OnSceneContextChanged);
        _bus.Subscribe<SelectionChangedEvent>(OnSelectionChanged);
        _bus.Subscribe<FrameChangedEvent>(OnFrameChanged);
        _bus.Subscribe<PlaybackStateChangedEvent>(OnPlaybackStateChanged);
        _bus.Subscribe<AnimationContainerChangedEvent>(OnContainerChanged);
        _bus.Subscribe<AnimationKeyframeChangedEvent>(OnKeyframeChanged);

        WireToolbar();
        WireTransport();
        WireEmptyState();
        WireTimelineInput();

        Refresh();
    }

    private void OnDisable()
    {
        if (_bus == null) return;
        _bus.Unsubscribe<SceneContextChangedEvent>(OnSceneContextChanged);
        _bus.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);
        _bus.Unsubscribe<FrameChangedEvent>(OnFrameChanged);
        _bus.Unsubscribe<PlaybackStateChangedEvent>(OnPlaybackStateChanged);
        _bus.Unsubscribe<AnimationContainerChangedEvent>(OnContainerChanged);
        _bus.Unsubscribe<AnimationKeyframeChangedEvent>(OnKeyframeChanged);
    }

    private void WireToolbar()
    {
        if (_toolbar == null) return;
        _toolbar.OnCurrentFrameSubmitted = f => _ctx.Clock?.Seek(Mathf.Clamp(f, 0, CurrentTotal()));
        _toolbar.OnTotalFramesSubmitted  = f => { if (_activeOwner != null) _ctx.Authoring.SetTotalFrames(_activeOwner, f); };
        _toolbar.OnFpsSubmitted          = f => { if (_activeOwner != null) _ctx.Authoring.SetFps(_activeOwner, f); };
        _toolbar.OnSetKey                = OnSetKeyClicked;
        _toolbar.OnDeleteKey             = OnDeleteKeyClicked;
        _toolbar.OnCopy                  = OnCopyClicked;
        _toolbar.OnPaste                 = OnPasteClicked;
        _toolbar.OnRemoveAnimation       = OnRemoveAnimationClicked;
    }

    private void WireTransport()
    {
        if (_transport == null) return;
        _transport.OnPrevFrame  = () => _ctx.Clock?.Seek(Mathf.Max(0, _ctx.Clock.CurrentFrame - 1));
        _transport.OnNextFrame  = () => _ctx.Clock?.Seek(Mathf.Min(CurrentTotal(), _ctx.Clock.CurrentFrame + 1));
        _transport.OnStart      = () => _ctx.Clock?.Seek(0);
        _transport.OnEnd        = () => _ctx.Clock?.Seek(CurrentTotal());
        _transport.OnPlayPause  = OnPlayPauseClicked;
        _transport.OnPrevKey    = OnPrevKeyClicked;
        _transport.OnNextKey    = OnNextKeyClicked;
    }

    private void WireEmptyState()
    {
        if (_emptyState == null) return;
        _emptyState.OnAddAnimationClicked = OnAddAnimationClicked;
    }

    private void WireTimelineInput()
    {
        if (_timelineInput == null) return;
        _timelineInput.OnFrameRequested = frame => _ctx.Clock?.Seek(frame);
    }

    private void OnSceneContextChanged(SceneContextChangedEvent e) => Refresh();

    private void OnSelectionChanged(SelectionChangedEvent _) => Refresh();

    private void OnFrameChanged(FrameChangedEvent e)
    {
        if (_playhead != null) _playhead.SetFrame(e.Frame);
        if (_toolbar  != null) _toolbar.SetCurrentFrame(e.Frame);
        RefreshKeyButtonStates();
        RefreshLaneKeys();
    }

    private void OnPlaybackStateChanged(PlaybackStateChangedEvent e)
    {
        if (_transport != null) _transport.SetPlaying(e.IsPlaying);
    }

    private void OnContainerChanged(AnimationContainerChangedEvent e)
    {
        if (e.OwnerNodeId != _activeOwner) return;

        switch (e.Change)
        {
            case ContainerChange.Removed:
                Refresh();
                break;

            case ContainerChange.LengthChanged:
            case ContainerChange.FpsChanged:
                ApplyContainerToClock();
                RebuildTimeline();
                break;

            case ContainerChange.Added:
                Refresh();
                break;
        }
    }

    private void OnKeyframeChanged(AnimationKeyframeChangedEvent e)
    {
        if (e.OwnerNodeId != _activeOwner) return;
        RefreshLaneKeys();
        RefreshKeyButtonStates();
    }

    private void OnAddAnimationClicked()
    {
        var owner = AnimationAuthoring.OwnerOf(_ctx.Selection?.SelectedNodeId);
        if (string.IsNullOrEmpty(owner)) return;
        _ctx.Authoring.CreateContainer(owner);
    }

    private void OnRemoveAnimationClicked()
    {
        if (string.IsNullOrEmpty(_activeOwner)) return;
        _ctx.Authoring.RemoveContainer(_activeOwner);
    }

    private void OnSetKeyClicked()
    {
        if (string.IsNullOrEmpty(_activeOwner)) return;
        var active = _ctx.Selection?.SelectedNodeId ?? _activeOwner;
        _ctx.Authoring.SetKeyForFrame(_activeOwner, active, _ctx.Clock.CurrentFrame);
    }

    private void OnDeleteKeyClicked()
    {
        if (string.IsNullOrEmpty(_activeOwner)) return;
        _ctx.Authoring.DeleteAllKeysAtFrame(_activeOwner, _ctx.Clock.CurrentFrame);
    }

    private void OnCopyClicked()
    {
        if (string.IsNullOrEmpty(_activeOwner)) return;
        var clip = _ctx.Authoring.CopyFrame(_activeOwner, _ctx.Clock.CurrentFrame);
        _clipboard.Set(clip);
        RefreshKeyButtonStates();
    }

    private void OnPasteClicked()
    {
        if (string.IsNullOrEmpty(_activeOwner) || _clipboard.IsEmpty) return;
        _ctx.Authoring.PasteFrame(_activeOwner, _ctx.Clock.CurrentFrame, _clipboard.Current);
    }

    private void OnPrevKeyClicked()
    {
        var prev = _ctx.Authoring.NearestKeyBefore(_activeOwner, _ctx.Clock.CurrentFrame);
        if (prev.HasValue) _ctx.Clock.Seek(prev.Value);
    }

    private void OnNextKeyClicked()
    {
        var next = _ctx.Authoring.NearestKeyAfter(_activeOwner, _ctx.Clock.CurrentFrame);
        if (next.HasValue) _ctx.Clock.Seek(next.Value);
    }

    private void OnPlayPauseClicked()
    {
        if (_ctx.Clock == null) return;
        if (_ctx.Clock.IsPlaying) _ctx.Clock.Pause();
        else                      _ctx.Clock.Play();
    }

    private void Refresh()
    {
        if (!_ctx.HasScene) { ShowEmpty(AnimatorSubEmptyState.State.NoSelection); return; }

        var selected = _ctx.Selection?.SelectedNodeId;
        var owner    = AnimationAuthoring.OwnerOf(selected);
        var has      = !string.IsNullOrEmpty(owner) && _ctx.Authoring.HasContainer(owner);

        if (string.IsNullOrEmpty(selected))
        {
            _activeOwner = null;
            ShowEmpty(AnimatorSubEmptyState.State.NoSelection);
            _ctx.Authoring.SetActiveContainerOwner(null);
            _ctx.Clock.Configure(_config.DefaultTotalFrames, _config.DefaultFps);
            return;
        }

        if (!has)
        {
            _activeOwner = null;
            ShowEmpty(AnimatorSubEmptyState.State.NoContainer);
            _ctx.Authoring.SetActiveContainerOwner(null);
            _ctx.Clock.Configure(_config.DefaultTotalFrames, _config.DefaultFps);
            return;
        }

        _activeOwner = owner;
        ShowActive();
        _ctx.Authoring.SetActiveContainerOwner(_activeOwner);
        ApplyContainerToClock();
        RebuildTimeline();
        RefreshKeyButtonStates();
    }

    private void ShowEmpty(AnimatorSubEmptyState.State state)
    {
        if (_activeStateRoot != null) _activeStateRoot.SetActive(false);
        if (_emptyState != null) _emptyState.Show(state);
    }

    private void ShowActive()
    {
        if (_activeStateRoot != null) _activeStateRoot.SetActive(true);
        if (_emptyState != null) _emptyState.HideAll();
    }

    private void ApplyContainerToClock()
    {
        var c = _ctx.Authoring.GetContainer(_activeOwner);
        if (c == null) return;
        _ctx.Clock.Configure(c.TotalFrames, c.Fps);
        if (_toolbar != null)
        {
            _toolbar.SetTotalFrames(c.TotalFrames);
            _toolbar.SetFps(c.Fps);
            _toolbar.SetCurrentFrame(_ctx.Clock.CurrentFrame);
        }
    }

    private void RebuildTimeline()
    {
        var c = _ctx.Authoring.GetContainer(_activeOwner);
        if (c == null) return;

        if (_timelineContent != null && _config != null)
        {
            var size = _timelineContent.sizeDelta;
            size.x = (c.TotalFrames + 1) * _config.FramePx;
            _timelineContent.sizeDelta = size;
        }

        if (_timelineInput != null) _timelineInput.MaxFrame = c.TotalFrames;

        _ruler?.Rebuild(c.TotalFrames);
        RebuildTrackRows(c);
        RebuildLanes(c);
        RefreshLaneKeys();

        if (_playhead != null)
        {
            _playhead.SetFrame(_ctx.Clock.CurrentFrame);
        }
    }

    private void RebuildTrackRows(ActionContainer c)
    {
        if (_tracksColumnContent == null || _trackRowPrefab == null) return;
        foreach (var r in _rowPool) if (r != null) r.gameObject.SetActive(false);

        for (int i = 0; i < c.Tracks.Count; i++)
        {
            var t  = c.Tracks[i];
            var go = _ctx.Graph?.GetNode(t.NodeId);
            var display = go != null ? go.DisplayName : t.NodeId;
            bool isBone = t.NodeId.StartsWith("bone:");
            var kind    = isBone ? TrackRowKind.Bone : (c.OwnerNodeId == t.NodeId ? TrackRowKind.Rig : TrackRowKind.Object);
            int indent  = isBone ? 1 : 0;

            var row = GetOrCreateRow(i);
            row.gameObject.SetActive(true);
            row.Bind(t.NodeId, display, kind, t.Keys.Count > 0, indent,
                () => _ctx.Selection.Select(t.NodeId));

            row.SetActive(t.NodeId == _ctx.Selection.SelectedNodeId);
        }
    }

    private TrackRow GetOrCreateRow(int idx)
    {
        while (_rowPool.Count <= idx)
        {
            var r = Instantiate(_trackRowPrefab, _tracksColumnContent);
            r.gameObject.SetActive(false);
            _rowPool.Add(r);
        }
        return _rowPool[idx];
    }

    private void RebuildLanes(ActionContainer c)
    {
        if (_lanes == null) return;
        var list = new List<(string, bool)>(c.Tracks.Count);
        foreach (var t in c.Tracks)
            list.Add((t.NodeId, t.NodeId.StartsWith("bone:")));
        _lanes.Rebuild(list);

        foreach (var lane in _lanes.Lanes)
            if (lane != null && lane.gameObject.activeSelf)
                lane.SetActive(lane.TrackNodeId == _ctx.Selection.SelectedNodeId);
    }

    private void RefreshLaneKeys()
    {
        if (_lanes == null || string.IsNullOrEmpty(_activeOwner)) return;
        var c = _ctx.Authoring.GetContainer(_activeOwner);
        if (c == null) return;
        foreach (var t in c.Tracks)
        {
            var lane = _lanes.FindLane(t.NodeId);
            if (lane == null) continue;
            var frames = _ctx.Authoring.GetKeyFrames(t.NodeId);
            lane.SetKeys(frames, _ctx.Clock.CurrentFrame);
        }
    }

    private void RefreshKeyButtonStates()
    {
        if (_toolbar == null) return;
        bool hasContainer = !string.IsNullOrEmpty(_activeOwner);
        bool hasKey = hasContainer && (_ctx.Authoring.GetContainer(_activeOwner)?.HasAnyKeyAtFrame(_ctx.Clock.CurrentFrame) ?? false);
        _toolbar.SetSetKeyInteractable   (hasContainer);
        _toolbar.SetDeleteKeyInteractable(hasKey);
        _toolbar.SetPasteInteractable    (!_clipboard.IsEmpty);
    }

    private int CurrentTotal()
    {
        if (string.IsNullOrEmpty(_activeOwner)) return _config?.DefaultTotalFrames ?? 60;
        return _ctx.Authoring.GetContainer(_activeOwner)?.TotalFrames ?? _config?.DefaultTotalFrames ?? 60;
    }
}
