using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(UnityEngine.UI.Image))]
public class PanelDragHandle : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private UserPanel _panel;

    // Max world-space movement allowed in a single frame — safety against ray jumps
    private const float MaxFrameDelta = 0.4f;

    public void OnPointerDown(PointerEventData eventData) { }

    public void OnBeginDrag(PointerEventData eventData) =>
        _panel.SetDragging(true);

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.delta.sqrMagnitude < 0.01f) return;

        // Use screen-space delta projected to world space at the panel's depth.
        // This avoids the canvas-movement feedback loop that occurs when comparing
        // consecutive world hit positions on a moving canvas plane.
        var cam = eventData.enterEventCamera != null
            ? eventData.enterEventCamera
            : Camera.main;
        if (cam == null) return;

        var screenZ = cam.WorldToScreenPoint(_panel.transform.position).z;
        if (screenZ <= 0.01f) return;

        var prev = eventData.position - eventData.delta;
        var worldPrev = cam.ScreenToWorldPoint(new Vector3(prev.x,                 prev.y,                 screenZ));
        var worldCurr = cam.ScreenToWorldPoint(new Vector3(eventData.position.x,   eventData.position.y,   screenZ));

        var delta = worldCurr - worldPrev;
        if (delta.magnitude > MaxFrameDelta) return;

        _panel.MoveDelta(delta);
    }

    public void OnEndDrag(PointerEventData eventData) =>
        _panel.SetDragging(false);
}
