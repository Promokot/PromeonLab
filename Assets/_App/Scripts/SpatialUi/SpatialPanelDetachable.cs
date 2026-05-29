using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class SpatialPanelDetachable : MonoBehaviour
{
    [SerializeField] private Button                    _linkButton;
    [SerializeField] private Button                    _lockButton;
    [SerializeField] private Button                    _closeButton;
    [SerializeField] private DetachablePanelDragHandle _dragHandle;

    private EventBus _bus;
    private bool     _locked;
    private bool     _closedPublished;

    public bool   IsLinked  { get; private set; } = true;
    public bool   IsVisible { get; private set; }
    public string EntryId   { get; set; }

    [Inject]
    public void Construct(EventBus bus) => _bus = bus;

    private void Awake()
    {
        if (_linkButton  != null) _linkButton.onClick.AddListener(OnLinkButtonClicked);
        if (_lockButton  != null) _lockButton.onClick.AddListener(OnLockClicked);
        if (_closeButton != null) _closeButton.onClick.AddListener(OnCloseClicked);

        SetUnlinkedControlsVisible(false);
        if (_dragHandle != null) _dragHandle.enabled = false;

        gameObject.SetActive(false);
    }

    public void ToggleLinked()
    {
        if (IsVisible) Hide();
        else Show();
    }

    public void Show()
    {
        IsVisible = true;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (!IsVisible) return;
        IsVisible = false;
        if (IsLinked)
            gameObject.SetActive(false);
        else
            DestroyUnlinked();
    }

    public void Unlink()
    {
        if (!IsLinked) return;
        IsLinked = false;
        transform.SetParent(null, worldPositionStays: true);
        SetUnlinkedControlsVisible(true);
        if (_dragHandle != null) _dragHandle.enabled = true;
        _bus?.Publish(new PanelDetachedEvent { EntryId = EntryId });
    }

    public void LinkBack()
    {
        _closedPublished = true;
        _bus?.Publish(new PanelLinkedEvent { EntryId = EntryId });
        Destroy(gameObject);
    }

    public void MoveDelta(Vector3 delta)
    {
        if (!_locked)
            transform.position += delta;
    }

    private void OnLinkButtonClicked()
    {
        if (IsLinked) Unlink();
        else LinkBack();
    }

    private void OnLockClicked() => _locked = !_locked;

    private void OnCloseClicked()
    {
        _closedPublished = true;
        _bus?.Publish(new PanelClosedEvent { EntryId = EntryId });
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (!IsLinked && !_closedPublished)
            _bus?.Publish(new PanelClosedEvent { EntryId = EntryId });
    }

    private void DestroyUnlinked()
    {
        _closedPublished = true;
        _bus?.Publish(new PanelClosedEvent { EntryId = EntryId });
        Destroy(gameObject);
    }

    private void SetUnlinkedControlsVisible(bool visible)
    {
        if (_lockButton  != null) _lockButton.gameObject.SetActive(visible);
        if (_closeButton != null) _closeButton.gameObject.SetActive(visible);
    }
}
