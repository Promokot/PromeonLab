using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class DetachablePanelDragHandle : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private DetachablePanel _panel;
    [SerializeField] private Color _normalColor = new Color(1f,    1f,    1f,    0.25f);
    [SerializeField] private Color _hoverColor  = new Color(0.80f, 0.80f, 0.85f, 0.45f);
    [SerializeField] private Color _dragColor   = new Color(0.45f, 0.50f, 0.55f, 0.70f);

    private const float MaxFrameDelta = 0.4f;

    private Image _image;
    private bool  _isDragging;

    private void Awake()
    {
        _image       = GetComponent<Image>();
        _image.color = _normalColor;
    }

    public void OnPointerEnter(PointerEventData e) { if (!_isDragging) _image.color = _hoverColor; }
    public void OnPointerExit(PointerEventData e)  { if (!_isDragging) _image.color = _normalColor; }

    public void OnBeginDrag(PointerEventData e)
    {
        _isDragging  = true;
        _image.color = _dragColor;
    }

    public void OnDrag(PointerEventData e)
    {
        if (e.delta.sqrMagnitude < 0.01f || _panel == null) return;

        var cam = e.enterEventCamera != null ? e.enterEventCamera : Camera.main;
        if (cam == null) return;

        var screenZ = cam.WorldToScreenPoint(_panel.transform.position).z;
        if (screenZ <= 0.01f) return;

        var prev      = e.position - e.delta;
        var worldPrev = cam.ScreenToWorldPoint(new Vector3(prev.x,       prev.y,       screenZ));
        var worldCurr = cam.ScreenToWorldPoint(new Vector3(e.position.x, e.position.y, screenZ));
        var delta     = worldCurr - worldPrev;

        if (delta.magnitude > MaxFrameDelta) return;
        _panel.MoveDelta(delta);
    }

    public void OnEndDrag(PointerEventData e)
    {
        _isDragging  = false;
        _image.color = _normalColor;
    }
}
