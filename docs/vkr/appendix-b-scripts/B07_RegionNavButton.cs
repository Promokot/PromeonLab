using UnityEngine;
using UnityEngine.UI;
using VContainer;

// Thin nav button. It only forwards clicks to the router (Toggle). Visibility per mode
// and active-highlight are DRIVEN BY PanelRegionRouter via SetVisible / SetActiveHighlight –
// the button no longer subscribes to mode/region events itself.
// Setup is lifecycle-safe: the host UserPanel starts inactive, so Construct may run long
// before Awake/OnEnable. Colors + click listener are wired lazily and idempotently.
public class RegionNavButton : MonoBehaviour
{
    [SerializeField] private string _moduleId;
    [SerializeField] private Button _button;
    [SerializeField] [Range(0f, 2f)] private float _activeBrightness = 0.6f;

    private PanelRegionRouter _router;

    private ColorBlock _inactiveColors;
    private ColorBlock _activeColors;
    private bool       _colorsReady;
    private bool       _listenerAttached;
    private bool       _highlight;

    public string ModuleId => _moduleId;

    [Inject]
    public void Construct(PanelRegionRouter router) => _router = router;

    private void Awake()    => EnsureSetup();
    private void OnEnable()  => EnsureSetup();

    private void OnDestroy()
    {
        if (_button != null && _listenerAttached)
            _button.onClick.RemoveListener(OnClick);
    }

    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible)
            gameObject.SetActive(visible);
    }

    public void SetActiveHighlight(bool active)
    {
        _highlight = active;
        ApplyColors();
    }

    private void EnsureSetup()
    {
        if (_button == null) return;

        if (!_colorsReady)
        {
            var baseColor = _button.colors.normalColor;
            var block     = _button.colors;

            var activeBase = Brighten(baseColor, _activeBrightness);

            _inactiveColors                  = block;
            _inactiveColors.normalColor      = baseColor;
            _inactiveColors.highlightedColor = Hover(baseColor);
            _inactiveColors.selectedColor    = baseColor;

            _activeColors                  = block;
            _activeColors.normalColor      = activeBase;
            _activeColors.highlightedColor = Hover(activeBase);
            _activeColors.selectedColor    = activeBase;

            _colorsReady = true;
        }

        if (!_listenerAttached)
        {
            _button.onClick.AddListener(OnClick);
            _listenerAttached = true;
        }

        ApplyColors();
    }

    private void OnClick()
    {
        _router?.Toggle(_moduleId);
    }

    private void ApplyColors()
    {
        if (_colorsReady && _button != null)
            _button.colors = _highlight ? _activeColors : _inactiveColors;
    }

    // Hover tint that is always visibly distinct: shifts brightness toward whichever end has the
    // most headroom, so near-white buttons darken and dark buttons brighten instead of the old
    // brighten-only path that no-oped on already-light colors (the "no visible hover" symptom).
    private static Color Hover(Color c)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        float vNew   = v > 0.5f ? v - 0.22f : v + 0.22f;
        var   result = Color.HSVToRGB(h, s, Mathf.Clamp01(vNew));
        result.a = c.a;
        return result;
    }

    private static Color Brighten(Color c, float mult)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        var vNew   = mult >= 1f ? Mathf.Clamp01(v + (mult - 1f) * (1f - v + 0.05f)) : v * mult;
        var result = Color.HSVToRGB(h, s, vNew);
        result.a = c.a;
        return result;
    }
}
