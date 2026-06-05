using UnityEngine;
using UnityEngine.UI;
using VContainer;

// DEAD FEATURE – detachable / floating panels (never built; see docs/BACKLOG.md).
// All operational code is commented out so the component is INERT wherever it lingers on a panel
// prefab (currently only AnimatorPanelModule). Its live behaviour fought the region router that owns
// panel visibility – most visibly, Awake's SetActive(false) ate the first router Open (two-press-to-
// open bug). Commented (not deleted) so the feature can be revived by uncommenting; the class shell +
// serialized fields are kept so prefab references stay valid. While dead, this MUST NOT touch
// activeSelf, wire button listeners, reparent, destroy, or publish events.
#pragma warning disable 0414, 0649 // serialized/DI fields kept for revival but currently unread
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

    // private void Awake()
    // {
    //     if (_linkButton  != null) _linkButton.onClick.AddListener(OnLinkButtonClicked);
    //     if (_lockButton  != null) _lockButton.onClick.AddListener(OnLockClicked);
    //     if (_closeButton != null) _closeButton.onClick.AddListener(OnCloseClicked);
    //
    //     SetUnlinkedControlsVisible(false);
    //     if (_dragHandle != null) _dragHandle.enabled = false;
    //
    //     // Region router owns visibility – self-deactivating here ate the first Open (two-press bug).
    //     gameObject.SetActive(false);
    // }

    public void ToggleLinked() { /* dead feature – no-op */ }
    // {
    //     if (IsVisible) Hide();
    //     else Show();
    // }

    public void Show() { /* dead feature – no-op */ }
    // {
    //     IsVisible = true;
    //     gameObject.SetActive(true);
    // }

    public void Hide() { /* dead feature – no-op */ }
    // {
    //     if (!IsVisible) return;
    //     IsVisible = false;
    //     if (IsLinked)
    //         gameObject.SetActive(false);
    //     else
    //         DestroyUnlinked();
    // }

    public void Unlink() { /* dead feature – no-op */ }
    // {
    //     if (!IsLinked) return;
    //     IsLinked = false;
    //     transform.SetParent(null, worldPositionStays: true);
    //     SetUnlinkedControlsVisible(true);
    //     if (_dragHandle != null) _dragHandle.enabled = true;
    //     _bus?.Publish(new PanelDetachedEvent { EntryId = EntryId });
    // }

    public void LinkBack() { /* dead feature – no-op */ }
    // {
    //     _closedPublished = true;
    //     _bus?.Publish(new PanelLinkedEvent { EntryId = EntryId });
    //     Destroy(gameObject);
    // }

    public void MoveDelta(Vector3 delta) { /* dead feature – no-op */ }
    // {
    //     if (!_locked)
    //         transform.position += delta;
    // }

    // private void OnLinkButtonClicked()
    // {
    //     if (IsLinked) Unlink();
    //     else LinkBack();
    // }
    //
    // private void OnLockClicked() => _locked = !_locked;
    //
    // private void OnCloseClicked()
    // {
    //     _closedPublished = true;
    //     _bus?.Publish(new PanelClosedEvent { EntryId = EntryId });
    //     Destroy(gameObject);
    // }
    //
    // private void OnDestroy()
    // {
    //     if (!IsLinked && !_closedPublished)
    //         _bus?.Publish(new PanelClosedEvent { EntryId = EntryId });
    // }
    //
    // private void DestroyUnlinked()
    // {
    //     _closedPublished = true;
    //     _bus?.Publish(new PanelClosedEvent { EntryId = EntryId });
    //     Destroy(gameObject);
    // }
    //
    // private void SetUnlinkedControlsVisible(bool visible)
    // {
    //     if (_lockButton  != null) _lockButton.gameObject.SetActive(visible);
    //     if (_closeButton != null) _closeButton.gameObject.SetActive(visible);
    // }
}
#pragma warning restore 0414, 0649
