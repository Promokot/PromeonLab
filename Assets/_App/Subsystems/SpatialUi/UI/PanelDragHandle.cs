using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(UnityEngine.UI.Image))]
public class PanelDragHandle : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private UserPanel _panel;

    private Vector3 _prevHitPos;

    public void OnPointerDown(PointerEventData eventData) { }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _prevHitPos = eventData.pointerPressRaycast.worldPosition;
        if (_prevHitPos == Vector3.zero)
            _prevHitPos = eventData.pointerCurrentRaycast.worldPosition;
        _panel.SetDragging(true);
    }

    public void OnDrag(PointerEventData eventData)
    {
        var hitPos = eventData.pointerCurrentRaycast.worldPosition;
        if (hitPos == Vector3.zero) return;
        _panel.MoveDelta(hitPos - _prevHitPos);
        _prevHitPos = hitPos;
    }

    public void OnEndDrag(PointerEventData eventData) =>
        _panel.SetDragging(false);
}
