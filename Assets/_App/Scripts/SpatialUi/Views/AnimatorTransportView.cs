using System;
using UnityEngine;
using UnityEngine.UI;

public class AnimatorTransportView : MonoBehaviour
{
    [SerializeField] private Button _prevKeyButton;
    [SerializeField] private Button _prevFrameButton;
    [SerializeField] private Button _startButton;
    [SerializeField] private Button _playPauseButton;
    [SerializeField] private Button _endButton;
    [SerializeField] private Button _nextFrameButton;
    [SerializeField] private Button _nextKeyButton;
    [SerializeField] private Image  _playPauseIcon;
    [SerializeField] private Sprite _playSprite;
    [SerializeField] private Sprite _pauseSprite;

    public Action OnPrevKey;
    public Action OnPrevFrame;
    public Action OnStart;
    public Action OnPlayPause;
    public Action OnEnd;
    public Action OnNextFrame;
    public Action OnNextKey;

    private void Awake()
    {
        _prevKeyButton  ?.onClick.AddListener(() => OnPrevKey?.Invoke());
        _prevFrameButton?.onClick.AddListener(() => OnPrevFrame?.Invoke());
        _startButton    ?.onClick.AddListener(() => OnStart?.Invoke());
        _playPauseButton?.onClick.AddListener(() => OnPlayPause?.Invoke());
        _endButton      ?.onClick.AddListener(() => OnEnd?.Invoke());
        _nextFrameButton?.onClick.AddListener(() => OnNextFrame?.Invoke());
        _nextKeyButton  ?.onClick.AddListener(() => OnNextKey?.Invoke());
    }

    public void SetPlaying(bool playing)
    {
        if (_playPauseIcon == null) return;
        _playPauseIcon.sprite = playing ? _pauseSprite : _playSprite;
    }
}
