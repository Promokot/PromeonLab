using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(UnityEngine.UI.Image))]
public class PanelDragHandle : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private UserPanel _panel;

    private Vector3 _grabOffset;

    public void OnPointerDown(PointerEventData eventData) { }

    public void OnBeginDrag(PointerEventData eventData)
    {
        var hitPos = eventData.pointerPressRaycast.worldPosition;
        _grabOffset = hitPos != Vector3.zero
            ? _panel.transform.position - hitPos
            : Vector3.zero;
        _panel.SetDragging(true);
    }

    public void OnDrag(PointerEventData eventData)
    {
        var hitPos = eventData.pointerCurrentRaycast.worldPosition;
        if (hitPos != Vector3.zero)
            _panel.SetDragWorldPosition(hitPos + _grabOffset);
    }

    public void OnEndDrag(PointerEventData eventData) =>
        _panel.SetDragging(false);
}
