using UnityEngine;
using UnityEngine.EventSystems;

public class TimelineScrubInput : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    [SerializeField] private RectTransform        _content;
    [SerializeField] private AnimatorPanelConfig  _config;

    public System.Action<int> OnFrameRequested;
    public int                MaxFrame    { get; set; } = 60;
    public float              LeftOffset  { get; set; } = 0f;

    public void OnPointerDown(PointerEventData e) => HandleEvent(e);
    public void OnDrag       (PointerEventData e) => HandleEvent(e);

    private void HandleEvent(PointerEventData e)
    {
        if (_content == null || _config == null || OnFrameRequested == null) return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _content, e.position, e.pressEventCamera, out var local)) return;

        int frame = Mathf.RoundToInt((local.x - LeftOffset) / _config.FramePx);
        frame = Mathf.Clamp(frame, 0, MaxFrame);
        OnFrameRequested(frame);
    }
}
