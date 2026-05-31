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
    [Header("Sticky highlight (mirrors the panel nav buttons)")]
    [SerializeField, Range(0f, 2f)] private float _inactiveHoverBrightness = 1.2f;
    [SerializeField, Range(0f, 2f)] private float _activeBrightness        = 0.6f;
    [SerializeField, Range(0f, 2f)] private float _activeHoverBrightness   = 0.8f;

    private EventBus  _bus;
    private GizmoMode _current = GizmoMode.Move;

    // Per-button color pairs (built once from each button's authored normalColor). The active pair
    // brightens normalColor too, so the picked tool button HOLDS its color even without focus.
    private bool       _colorsReady;
    private ColorBlock _moveInactive,   _moveActive;
    private ColorBlock _rotateInactive, _rotateActive;
    private ColorBlock _scaleInactive,  _scaleActive;

    [Inject]
    public void Construct(EventBus bus) => _bus = bus;

    private void Awake()
    {
        EnsureColors();
        if (_moveButton   != null) _moveButton  .onClick.AddListener(() => SelectMode(GizmoMode.Move));
        if (_rotateButton != null) _rotateButton.onClick.AddListener(() => SelectMode(GizmoMode.Rotate));
        if (_scaleButton  != null) _scaleButton .onClick.AddListener(() => SelectMode(GizmoMode.Scale));
    }

    private void OnEnable()
    {
        EnsureColors();
        _current = GizmoMode.Move; // default tool — Move
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

        if (!_colorsReady) return;
        if (_moveButton   != null) _moveButton  .colors = _current == GizmoMode.Move   ? _moveActive   : _moveInactive;
        if (_rotateButton != null) _rotateButton.colors = _current == GizmoMode.Rotate ? _rotateActive : _rotateInactive;
        if (_scaleButton  != null) _scaleButton .colors = _current == GizmoMode.Scale  ? _scaleActive  : _scaleInactive;
    }

    private void EnsureColors()
    {
        if (_colorsReady) return;
        bool any = false;
        if (_moveButton   != null) { BuildPair(_moveButton,   out _moveInactive,   out _moveActive);   any = true; }
        if (_rotateButton != null) { BuildPair(_rotateButton, out _rotateInactive, out _rotateActive); any = true; }
        if (_scaleButton  != null) { BuildPair(_scaleButton,  out _scaleInactive,  out _scaleActive);  any = true; }
        _colorsReady = any;
    }

    private void BuildPair(Button b, out ColorBlock inactive, out ColorBlock active)
    {
        var baseColor = b.colors.normalColor;

        inactive                  = b.colors;
        inactive.normalColor      = baseColor;
        inactive.highlightedColor = Brighten(baseColor, _inactiveHoverBrightness);
        inactive.selectedColor    = baseColor;

        active                  = b.colors;
        active.normalColor      = Brighten(baseColor, _activeBrightness);
        active.highlightedColor = Brighten(baseColor, _activeHoverBrightness);
        active.selectedColor    = Brighten(baseColor, _activeBrightness);
    }

    private static Color Brighten(Color c, float mult)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        var vNew   = mult >= 1f ? Mathf.Clamp01(v + (mult - 1f) * (1f - v + 0.05f)) : v * mult;
        var result = Color.HSVToRGB(h, s, vNew);
        result.a = c.a;
        return result;
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
