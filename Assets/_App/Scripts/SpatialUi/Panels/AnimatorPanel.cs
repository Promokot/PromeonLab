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
    [SerializeField] private AnimatorSubPlayhead   _playhead;
    [SerializeField] private TimelineScrubInput    _timelineInput;
    [SerializeField] private TimelineScrubInput    _rulerInput;
    [SerializeField] private TimelineRow_Item           _rowPrefab;
    [SerializeField] private RectTransform         _rowsContent;

    private EventBus           _bus;
    private AnimationClipboard _clipboard;
    private SceneContext       _ctx;

    private string                       _activeOwner;
    private string                       _boneModeRig; // rig whose bone-edit mode is ON; keeps the timeline up when no bone is selected
    private readonly List<TimelineRow_Item> _rowPool = new();

    [Inject]
    public void Construct(EventBus bus, AnimationClipboard clipboard, SceneContext ctx)
    {
        _bus       = bus;
        _clipboard = clipboard;
        _ctx       = ctx;

        // Bone-edit mode must be tracked even while this panel is hidden: the Show Bones toggle lives
        // in the Inspector, so the user can enter bone mode with the Animator tab closed. A subscription
        // scoped to OnEnable/OnDisable would miss that event and reopen showing "select something" with
        // no bone selected. The panel is injected once at root (persistent on the UserPanel), so this
        // durable subscription is safe and single. Redraw is deferred to OnEnable when hidden.
        _bus.Subscribe<BonesVisibilityChangedEvent>(OnBonesVisibilityChanged);
    }

    private void OnDestroy()
    {
        if (_bus != null) _bus.Unsubscribe<BonesVisibilityChangedEvent>(OnBonesVisibilityChanged);
    }

    private void OnEnable()
    {
        if (_bus == null) return;
        _bus.Subscribe<SceneContextChangedEvent>(OnSceneContextChanged);
        _bus.Subscribe<SelectionChangedEvent>(OnSelectionChanged);
        _bus.Subscribe<FrameChangedEvent>(OnFrameChanged);
        _bus.Subscribe<LoopFrameChangedEvent>(OnLoopFrameChanged);
        _bus.Subscribe<PlaybackStateChangedEvent>(OnPlaybackStateChanged);
        _bus.Subscribe<AnimationContainerChangedEvent>(OnContainerChanged);
        _bus.Subscribe<AnimationKeyframeChangedEvent>(OnKeyframeChanged);
        _bus.Subscribe<NodeRenamedEvent>(OnNodeRenamed);
        // NOTE: BonesVisibilityChangedEvent is subscribed durably in Construct (see there), not here.

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
        _bus.Unsubscribe<LoopFrameChangedEvent>(OnLoopFrameChanged);
        _bus.Unsubscribe<PlaybackStateChangedEvent>(OnPlaybackStateChanged);
        _bus.Unsubscribe<AnimationContainerChangedEvent>(OnContainerChanged);
        _bus.Unsubscribe<AnimationKeyframeChangedEvent>(OnKeyframeChanged);
        _bus.Unsubscribe<NodeRenamedEvent>(OnNodeRenamed);
    }

    private void WireToolbar()
    {
        if (_toolbar == null) return;
        _toolbar.OnCurrentFrameSubmitted = f => _ctx.Clock?.Seek(Mathf.Clamp(f, 0, CurrentTotal()));
        _toolbar.OnTotalFramesSubmitted  = f => { if (_activeOwner != null) _ctx.Authoring.SetTotalFrames(_activeOwner, f); };
        _toolbar.OnFpsSubmitted          = f => _ctx.Authoring?.SetSceneFps(f);
        _toolbar.OnSetKey                = OnSetKeyClicked;
        _toolbar.OnDeleteKey             = OnDeleteKeyClicked;
        _toolbar.OnCopy                  = OnCopyClicked;
        _toolbar.OnPaste                 = OnPasteClicked;
        _toolbar.OnRemoveAnimation       = OnRemoveAnimationClicked;
        _toolbar.OnToggleInterpolation   = OnToggleInterpolationClicked;
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
        _transport.OnToggleMode = OnToggleModeClicked;
        _transport.SetMode(!string.IsNullOrEmpty(_activeOwner) && _ctx.Authoring != null && _ctx.Authoring.IsLooping(_activeOwner));
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
        if (_rulerInput != null) _rulerInput.OnFrameRequested = frame => _ctx.Clock?.Seek(frame);
    }

    private void OnSceneContextChanged(SceneContextChangedEvent e)
    {
        _boneModeRig = null; // bone mode does not survive a scene swap
        Refresh();
    }

    private void OnSelectionChanged(SelectionChangedEvent _) => Refresh();

    private void OnBonesVisibilityChanged(BonesVisibilityChangedEvent e)
    {
        _boneModeRig = e.Visible ? e.RigNodeId : null;
        // Hidden panel: state is stored above; the redraw happens on the next OnEnable's Refresh.
        if (isActiveAndEnabled) Refresh();
    }

    private void OnNodeRenamed(NodeRenamedEvent _)
    {
        if (!string.IsNullOrEmpty(_activeOwner)) RebuildTimeline();
    }

    private void OnFrameChanged(FrameChangedEvent e)
    {
        if (_playhead != null) _playhead.SetFrame(e.Frame);
        if (_toolbar  != null) _toolbar.SetCurrentFrame(e.Frame);
        RefreshKeyButtonStates();
        RefreshRowKeys();
    }

    private void OnLoopFrameChanged(LoopFrameChangedEvent e)
    {
        if (e.OwnerNodeId != _activeOwner) return;   // playhead follows only the selected owner
        if (_playhead != null) _playhead.SetFrame(e.Frame);
        if (_toolbar  != null) _toolbar.SetCurrentFrame(e.Frame);
    }

    private void OnPlaybackStateChanged(PlaybackStateChangedEvent e)
    {
        if (_transport != null) _transport.SetPlaying(e.IsPlaying);
    }

    private void OnContainerChanged(AnimationContainerChangedEvent e)
    {
        // A newly-Added container for the current selection must refresh even when _activeOwner is
        // still null (no container existed a moment ago, so Refresh cleared it). Without this, the
        // first "Add animation" looks like a no-op until the next reselect. (audit H5)
        if (e.Change == ContainerChange.Added)
        {
            var selectedOwner = AnimationAuthoring.OwnerOf(_ctx.Selection?.SelectedNodeId);
            if (e.OwnerNodeId == _activeOwner || e.OwnerNodeId == selectedOwner) Refresh();
            return;
        }

        if (e.OwnerNodeId != _activeOwner) return;

        switch (e.Change)
        {
            case ContainerChange.Removed:
                Refresh();
                break;

            case ContainerChange.TracksChanged:
                RebuildTimeline();
                break;

            case ContainerChange.LengthChanged:
            case ContainerChange.FpsChanged:
                ApplyContainerToClock();
                RebuildTimeline();
                break;
        }
    }

    private void OnKeyframeChanged(AnimationKeyframeChangedEvent e)
    {
        if (e.OwnerNodeId != _activeOwner) return;
        RefreshRowKeys();
        RefreshKeyButtonStates();
    }

    private void OnAddAnimationClicked()
    {
        if (_ctx.Authoring == null) return;
        var selected = _ctx.Selection?.SelectedNodeId;
        var owner    = AnimationAuthoring.OwnerOf(selected);
        if (string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(_boneModeRig))
            owner = _boneModeRig;                         // bone mode, nothing selected → target the rig
        if (string.IsNullOrEmpty(owner)) return;

        _ctx.Authoring.CreateContainer(owner, _config.DefaultTotalFrames, _config.DefaultFps);
        _ctx.Authoring.EnsureTrack(owner, owner);         // owner track ALWAYS — object/rig's own transform
    }

    private void OnRemoveAnimationClicked()
    {
        if (string.IsNullOrEmpty(_activeOwner)) return;
        _ctx.Authoring.RemoveContainer(_activeOwner);
    }

    private void OnToggleInterpolationClicked()
    {
        if (string.IsNullOrEmpty(_activeOwner)) return;
        var cur  = _ctx.Authoring.GetInterpolation(_activeOwner);
        var next = cur == InterpolationMode.Stepped ? InterpolationMode.Linear : InterpolationMode.Stepped;
        _ctx.Authoring.SetInterpolation(_activeOwner, next);
        _toolbar.SetInterpolationLabel(next.ToString());
        _ctx.Clock.Seek(_ctx.Clock.CurrentFrame); // re-fire FrameChanged → re-sample with new tangents
    }

    private void OnSetKeyClicked()
    {
        if (string.IsNullOrEmpty(_activeOwner)) return;
        var target = _ctx.Selection?.SelectedNodeId;
        if (string.IsNullOrEmpty(target)) return; // nothing selected (e.g. bone mode, no bone) → no key
        _ctx.Authoring.SetKey(target, _ctx.Clock.CurrentFrame); // keys only the selected track
        RefreshKeyButtonStates();
    }

    private void OnDeleteKeyClicked()
    {
        if (string.IsNullOrEmpty(_activeOwner)) return;
        var target = _ctx.Selection?.SelectedNodeId;
        if (string.IsNullOrEmpty(target)) return;
        _ctx.Authoring.DeleteKey(target, _ctx.Clock.CurrentFrame); // removes only the selected track's key
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
        if (!string.IsNullOrEmpty(_activeOwner) && _ctx.Authoring != null && _ctx.Authoring.IsLooping(_activeOwner))
        {
            if (_ctx.Authoring.IsLoopPlaying(_activeOwner)) _ctx.Authoring.StopLoopPlayback(_activeOwner);
            else                                            _ctx.Authoring.StartLoopPlayback(_activeOwner, _ctx.Clock.CurrentFrame);
            _transport?.SetPlaying(_ctx.Authoring.IsLoopPlaying(_activeOwner));
            return;
        }
        if (_ctx.Clock.IsPlaying) _ctx.Clock.Pause(); else _ctx.Clock.Play();
    }

    private void OnToggleModeClicked()
    {
        if (string.IsNullOrEmpty(_activeOwner) || _ctx.Authoring == null) return;
        bool next = !_ctx.Authoring.IsLooping(_activeOwner);
        _ctx.Authoring.SetLoop(_activeOwner, next);
        _transport?.SetMode(next);
    }

    private void Refresh()
    {
        // The animation services are absent in scenes without an animation system (e.g. Sandbox),
        // where SceneContext binds Graph but leaves Authoring/Clock null. Guard on what Refresh
        // actually dereferences, not just HasScene (which only tracks Graph).
        if (_ctx.Authoring == null || _ctx.Clock == null) { ShowEmpty(AnimatorSubEmptyState.State.NoSelection); return; }

        var selected = _ctx.Selection?.SelectedNodeId;
        var owner    = AnimationAuthoring.OwnerOf(selected);
        // Bone-edit mode: with no bone selected we are still focused on the rig, so keep its timeline
        // visible (keying is disabled because no specific track is selected).
        if (string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(_boneModeRig))
            owner = _boneModeRig;
        var has      = !string.IsNullOrEmpty(owner) && _ctx.Authoring.HasContainer(owner);

        if (string.IsNullOrEmpty(owner))
        {
            _activeOwner = null;
            ShowEmpty(AnimatorSubEmptyState.State.NoSelection);
            _ctx.Authoring.SetActiveContainerOwner(null);
            _ctx.Clock.Configure(_config.DefaultTotalFrames, _ctx.Authoring.GetSceneFps());
            return;
        }

        if (!has)
        {
            _activeOwner = null;
            ShowEmpty(AnimatorSubEmptyState.State.NoContainer);
            _ctx.Authoring.SetActiveContainerOwner(null);
            _ctx.Clock.Configure(_config.DefaultTotalFrames, _ctx.Authoring.GetSceneFps());
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
        int sceneFps = _ctx.Authoring.GetSceneFps();
        _ctx.Clock.Configure(c.TotalFrames, sceneFps);
        if (_toolbar != null)
        {
            _toolbar.SetTotalFrames(c.TotalFrames);
            _toolbar.SetFps(sceneFps);
            _toolbar.SetCurrentFrame(_ctx.Clock.CurrentFrame);
            _toolbar.SetInterpolationLabel(_ctx.Authoring.GetInterpolation(_activeOwner).ToString());
        }
        _transport?.SetMode(_ctx.Authoring.IsLooping(_activeOwner));
        _transport?.SetPlaying(_ctx.Authoring.IsLoopPlaying(_activeOwner) || _ctx.Clock.IsPlaying);
    }

    private void RebuildTimeline()
    {
        var c = _ctx.Authoring.GetContainer(_activeOwner);
        if (c == null) return;

        float off = _config != null ? _config.TrackNameWidth : 0f;
        float px  = _config != null ? _config.FramePx : 30f;

        if (_timelineContent != null)
        {
            var size = _timelineContent.sizeDelta;
            size.x = off + (c.TotalFrames + 1) * px;
            _timelineContent.sizeDelta = size;
        }

        if (_timelineInput != null) { _timelineInput.MaxFrame = c.TotalFrames; _timelineInput.LeftOffset = off; }
        if (_rulerInput != null) { _rulerInput.MaxFrame = c.TotalFrames; _rulerInput.LeftOffset = off; }

        _ruler?.Rebuild(c.TotalFrames);
        RebuildRows(c);

        if (_playhead != null) { _playhead.LeftOffset = off; _playhead.SetFrame(_ctx.Clock.CurrentFrame); }
    }

    private void RebuildRows(ActionContainer c)
    {
        foreach (var r in _rowPool) if (r != null) r.gameObject.SetActive(false);
        if (_rowsContent == null || _rowPrefab == null) return;

        for (int i = 0; i < c.Tracks.Count; i++)
        {
            var t       = c.Tracks[i];
            var go      = _ctx.Graph?.GetNode(t.NodeId);
            var display = go != null ? go.DisplayName : t.NodeId;
            bool isBone = t.NodeId.StartsWith("bone:");

            var row = GetOrCreateRow(i);
            row.gameObject.SetActive(true);
            row.Bind(t.NodeId, display, isBone, () => _ctx.Selection.Select(t.NodeId));
            row.SetActive(t.NodeId == _ctx.Selection.SelectedNodeId);
            row.SetKeys(_ctx.Authoring.GetKeyFrames(t.NodeId), _ctx.Clock.CurrentFrame);
        }
    }

    private TimelineRow_Item GetOrCreateRow(int idx)
    {
        while (_rowPool.Count <= idx)
        {
            var r = Instantiate(_rowPrefab, _rowsContent);
            r.gameObject.SetActive(false);
            _rowPool.Add(r);
        }
        return _rowPool[idx];
    }

    private void RefreshRowKeys()
    {
        if (string.IsNullOrEmpty(_activeOwner)) return;
        var c = _ctx.Authoring.GetContainer(_activeOwner);
        if (c == null) return;
        foreach (var row in _rowPool)
        {
            if (row == null || !row.gameObject.activeSelf) continue;
            row.SetKeys(_ctx.Authoring.GetKeyFrames(row.TrackNodeId), _ctx.Clock.CurrentFrame);
        }
    }

    private void RefreshKeyButtonStates()
    {
        if (_toolbar == null) return;
        bool hasContainer = !string.IsNullOrEmpty(_activeOwner);
        bool hasSelection = !string.IsNullOrEmpty(_ctx.Selection?.SelectedNodeId);
        bool hasKey = hasContainer && (_ctx.Authoring.GetContainer(_activeOwner)?.HasAnyKeyAtFrame(_ctx.Clock.CurrentFrame) ?? false);
        _toolbar.SetSetKeyInteractable   (hasContainer && hasSelection);
        _toolbar.SetDeleteKeyInteractable(hasKey && hasSelection);
        _toolbar.SetPasteInteractable    (!_clipboard.IsEmpty);
    }

    private int CurrentTotal()
    {
        if (string.IsNullOrEmpty(_activeOwner)) return _config?.DefaultTotalFrames ?? 60;
        return _ctx.Authoring.GetContainer(_activeOwner)?.TotalFrames ?? _config?.DefaultTotalFrames ?? 60;
    }
}
