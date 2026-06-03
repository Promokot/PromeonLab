using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// DEAD / LEGACY — flatscreen pointer-drag handle for the UserPanel. Superseded by PanelGrabHandle
// (XR grip-grab via NearFarInteractor, the one actually on UserPanel.prefab). This component is on no
// prefab or scene. All operational code is commented out so it is INERT if ever re-attached; the class
// shell, serialized fields, and interface method shells (no-ops) are kept so it still compiles and
// satisfies the EventSystem handler interfaces. Revive by uncommenting. See docs/BACKLOG.md / memory.
#pragma warning disable 0414, 0649 // serialized/private fields kept for revival but currently unread
[RequireComponent(typeof(Image))]
public class PanelDragHandle : MonoBehaviour,
    IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private UserPanel _panel;
    [SerializeField] private Color _normalColor = new Color(1f,    1f,    1f,    0.25f);
    [SerializeField] private Color _hoverColor  = new Color(0.80f, 0.80f, 0.85f, 0.45f);
    [SerializeField] private Color _dragColor   = new Color(0.45f, 0.50f, 0.55f, 0.70f);

    private const float MaxFrameDelta = 0.4f;

    private Image _image;
    private bool  _isDragging;

    // private void Awake()
    // {
    //     _image       = GetComponent<Image>();
    //     _image.color = _normalColor;
    // }

    public void OnPointerDown(PointerEventData eventData) { /* dead feature — no-op */ }

    public void OnPointerEnter(PointerEventData eventData) { /* dead feature — no-op */ }
    // {
    //     if (!_isDragging)
    //         _image.color = _hoverColor;
    // }

    public void OnPointerExit(PointerEventData eventData) { /* dead feature — no-op */ }
    // {
    //     if (!_isDragging)
    //         _image.color = _normalColor;
    // }

    public void OnBeginDrag(PointerEventData eventData) { /* dead feature — no-op */ }
    // {
    //     _isDragging  = true;
    //     _image.color = _dragColor;
    //     _panel.SetDragging(true);
    // }

    public void OnDrag(PointerEventData eventData) { /* dead feature — no-op */ }
    // {
    //     if (eventData.delta.sqrMagnitude < 0.01f) return;
    //
    //     // Screen-space delta projected to world at panel depth avoids the
    //     // canvas-movement feedback loop that occurs with consecutive world hit comparisons.
    //     var cam = eventData.enterEventCamera != null
    //         ? eventData.enterEventCamera
    //         : Camera.main;
    //     if (cam == null) return;
    //
    //     var screenZ = cam.WorldToScreenPoint(_panel.transform.position).z;
    //     if (screenZ <= 0.01f) return;
    //
    //     var prev      = eventData.position - eventData.delta;
    //     var worldPrev = cam.ScreenToWorldPoint(new Vector3(prev.x,               prev.y,               screenZ));
    //     var worldCurr = cam.ScreenToWorldPoint(new Vector3(eventData.position.x, eventData.position.y, screenZ));
    //
    //     var delta = worldCurr - worldPrev;
    //     if (delta.magnitude > MaxFrameDelta) return;
    //
    //     _panel.MoveTo(_panel.transform.position + delta);
    // }

    public void OnEndDrag(PointerEventData eventData) { /* dead feature — no-op */ }
    // {
    //     _isDragging  = false;
    //     _image.color = _normalColor;
    //     _panel.SetDragging(false);
    // }
}
#pragma warning restore 0414, 0649
