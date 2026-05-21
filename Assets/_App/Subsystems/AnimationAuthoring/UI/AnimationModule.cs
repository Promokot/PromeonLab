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
    [SerializeField] private Slider   _scrubber;
    [SerializeField] private TMP_Text _frameLabel;

    [Header("Keyframe markers")]
    [SerializeField] private RectTransform _markersRoot;
    [SerializeField] private Image         _markerPrefab;

    [Header("Keyframe actions")]
    [SerializeField] private Button _setKeyButton;
    [SerializeField] private Button _deleteKeyButton;

    private AnimationClock    _clock;
    private AnimationAuthoring _authoring;
    private ISelectionManager  _selection;
    private EventBus           _bus;

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

    private void OnPlay()   => _clock?.Play();
    private void OnStop()   => _clock?.Stop();
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

    private void OnFrameChanged(FrameChangedEvent e)              => RefreshScrubber(e.Frame);
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
        _activeNodeId = _selection?.SelectedNodeId;
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
        bool hasNode       = !string.IsNullOrEmpty(_activeNodeId);
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

        var   frames = _authoring.GetKeyFrames(_activeNodeId);
        float width  = _markersRoot.rect.width;
        int   total  = _clock.TotalFrames;

        for (int i = 0; i < frames.Count; i++)
        {
            var   marker = GetOrCreateMarker(i);
            marker.gameObject.SetActive(true);
            float x = total > 0 ? (float)frames[i] / total * width - width * 0.5f : 0f;
            ((RectTransform)marker.transform).anchoredPosition = new Vector2(x, 0f);
        }
    }

    private Image GetOrCreateMarker(int idx)
    {
        while (_markerPool.Count <= idx)
        {
            var img = Instantiate(_markerPrefab, _markersRoot);
            img.gameObject.SetActive(false);
            _markerPool.Add(img);
        }
        return _markerPool[idx];
    }
}
