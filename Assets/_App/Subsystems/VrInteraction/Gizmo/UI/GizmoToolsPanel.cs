using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class GizmoToolsPanel : MonoBehaviour
{
    [SerializeField] private Button _moveButton;
    [SerializeField] private Button _rotateButton;
    [SerializeField] private Button _scaleButton;
    [Header("Optional visual feedback")]
    [SerializeField] private GameObject _moveActiveIndicator;
    [SerializeField] private GameObject _rotateActiveIndicator;
    [SerializeField] private GameObject _scaleActiveIndicator;

    private EventBus  _bus;
    private GizmoMode _current = GizmoMode.Move;

    [Inject]
    public void Construct(EventBus bus) => _bus = bus;

    private void Awake()
    {
        if (_moveButton   != null) _moveButton  .onClick.AddListener(() => SelectMode(GizmoMode.Move));
        if (_rotateButton != null) _rotateButton.onClick.AddListener(() => SelectMode(GizmoMode.Rotate));
        if (_scaleButton  != null) _scaleButton .onClick.AddListener(() => SelectMode(GizmoMode.Scale));
    }

    private void OnEnable()
    {
        _current = GizmoMode.Move;
        UpdateIndicators();
        if (_bus != null)
        {
            _bus.Subscribe<GizmoDragStartedEvent>(OnDragStarted);
            _bus.Subscribe<GizmoDragEndedEvent>(OnDragEnded);
            _bus.Publish(new GizmoToolsPanelOpenedEvent());
        }
    }

    private void OnDisable()
    {
        if (_bus != null)
        {
            _bus.Unsubscribe<GizmoDragStartedEvent>(OnDragStarted);
            _bus.Unsubscribe<GizmoDragEndedEvent>(OnDragEnded);
            _bus.Publish(new GizmoToolsPanelClosedEvent());
        }
    }

    private void SelectMode(GizmoMode mode)
    {
        _current = mode;
        UpdateIndicators();
        _bus?.Publish(new GizmoModeChangedEvent { Mode = mode });
    }

    private void UpdateIndicators()
    {
        if (_moveActiveIndicator   != null) _moveActiveIndicator  .SetActive(_current == GizmoMode.Move);
        if (_rotateActiveIndicator != null) _rotateActiveIndicator.SetActive(_current == GizmoMode.Rotate);
        if (_scaleActiveIndicator  != null) _scaleActiveIndicator .SetActive(_current == GizmoMode.Scale);
    }

    private void OnDragStarted(GizmoDragStartedEvent _) => SetButtonsInteractable(false);
    private void OnDragEnded  (GizmoDragEndedEvent   _) => SetButtonsInteractable(true);

    private void SetButtonsInteractable(bool value)
    {
        if (_moveButton   != null) _moveButton  .interactable = value;
        if (_rotateButton != null) _rotateButton.interactable = value;
        if (_scaleButton  != null) _scaleButton .interactable = value;
    }
}
