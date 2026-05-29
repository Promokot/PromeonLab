using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class RegionNavButton : MonoBehaviour
{
    [SerializeField] private string _moduleId;
    [SerializeField] private Button _button;
    [SerializeField] [Range(0f, 2f)] private float _inactiveHoverBrightness = 1.2f;
    [SerializeField] [Range(0f, 2f)] private float _activeBrightness        = 0.6f;
    [SerializeField] [Range(0f, 2f)] private float _activeHoverBrightness   = 0.8f;

    private PanelRegionRouter _router;
    private IRegionConfig     _config;
    private ModeOrchestrator  _orchestrator;
    private EventBus          _bus;

    private ColorBlock _inactiveColors;
    private ColorBlock _activeColors;
    private string     _region;

    [Inject]
    public void Construct(PanelRegionRouter router, IRegionConfig config, ModeOrchestrator orchestrator, EventBus bus)
    {
        _router       = router;
        _config       = config;
        _orchestrator = orchestrator;
        _bus          = bus;
    }

    private void Start()
    {
        Debug.Log($"[RegionDBG] Start id={_moduleId} buttonNull={_button == null} routerNull={_router == null} go={gameObject.name}");
        if (_button != null)
        {
            var baseColor = _button.colors.normalColor;
            var block     = _button.colors;

            _inactiveColors                  = block;
            _inactiveColors.normalColor      = baseColor;
            _inactiveColors.highlightedColor = Brighten(baseColor, _inactiveHoverBrightness);
            _inactiveColors.selectedColor    = baseColor;

            _activeColors                  = block;
            _activeColors.normalColor      = Brighten(baseColor, _activeBrightness);
            _activeColors.highlightedColor = Brighten(baseColor, _activeHoverBrightness);
            _activeColors.selectedColor    = Brighten(baseColor, _activeBrightness);

            _button.colors = _inactiveColors;
            _button.onClick.AddListener(OnClick);
        }

        if (_config != null) _config.TryGetRegion(_moduleId, out _region);
        _bus?.Subscribe<RegionChangedEvent>(OnRegionChanged);
        _bus?.Subscribe<ModeChangedEvent>(OnModeChanged);
        if (_orchestrator != null) ApplyMode(_orchestrator.CurrentMode);
        SetActiveColors(_router != null && _router.IsOpen(_moduleId));
    }

    private void OnDestroy()
    {
        if (_button != null) _button.onClick.RemoveListener(OnClick);
        _bus?.Unsubscribe<RegionChangedEvent>(OnRegionChanged);
        _bus?.Unsubscribe<ModeChangedEvent>(OnModeChanged);
    }

    private void OnClick()
    {
        Debug.Log($"[RegionDBG] OnClick id={_moduleId} routerNull={_router == null}");
        _router?.Toggle(_moduleId);
    }

    private void OnRegionChanged(RegionChangedEvent e)
    {
        if (e.RegionKey == _region)
            SetActiveColors(e.OpenModuleId == _moduleId);
    }

    private void OnModeChanged(ModeChangedEvent e) => ApplyMode(e.CurrentMode);

    private void ApplyMode(AppMode mode)
    {
        var visible = _config != null && _config.IsVisibleInMode(_moduleId, mode);
        gameObject.SetActive(visible);
    }

    private void SetActiveColors(bool active)
    {
        if (_button != null) _button.colors = active ? _activeColors : _inactiveColors;
    }

    private static Color Brighten(Color c, float mult)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        var vNew = mult >= 1f ? Mathf.Clamp01(v + (mult - 1f) * (1f - v + 0.05f)) : v * mult;
        var result = Color.HSVToRGB(h, s, vNew);
        result.a = c.a;
        return result;
    }
}
