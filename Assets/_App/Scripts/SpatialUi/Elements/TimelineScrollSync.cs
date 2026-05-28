using UnityEngine;
using UnityEngine.UI;

public class TimelineScrollSync : MonoBehaviour
{
    [SerializeField] private ScrollRect _leftTracks;
    [SerializeField] private ScrollRect _rightTimeline;

    private bool _syncing;

    private void OnEnable()
    {
        if (_leftTracks    != null) _leftTracks   .onValueChanged.AddListener(OnLeftChanged);
        if (_rightTimeline != null) _rightTimeline.onValueChanged.AddListener(OnRightChanged);
    }

    private void OnDisable()
    {
        if (_leftTracks    != null) _leftTracks   .onValueChanged.RemoveListener(OnLeftChanged);
        if (_rightTimeline != null) _rightTimeline.onValueChanged.RemoveListener(OnRightChanged);
    }

    private void OnLeftChanged(Vector2 v)
    {
        if (_syncing || _rightTimeline == null) return;
        _syncing = true;
        _rightTimeline.verticalNormalizedPosition = v.y;
        _syncing = false;
    }

    private void OnRightChanged(Vector2 v)
    {
        if (_syncing || _leftTracks == null) return;
        _syncing = true;
        _leftTracks.verticalNormalizedPosition = v.y;
        _syncing = false;
    }
}
