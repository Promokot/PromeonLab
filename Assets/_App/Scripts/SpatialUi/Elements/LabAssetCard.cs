using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LabAssetCard : MonoBehaviour
{
    [SerializeField] private Image    _iconImage;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private Button   _button;

    [Header("Selection")]
    [Tooltip("Background tint applied while this card is the selected one. Persists across " +
             "hover/press until another card is selected.")]
    [SerializeField] private Color _selectedColor = new Color(0.30f, 0.80f, 0.40f, 1f);

    private ILabAsset  _asset;
    private ColorBlock _defaultColors;
    private bool       _hasDefaults;

    public event Action<LabAssetCard> Selected;

    public ILabAsset Asset => _asset;

    private void Awake()
    {
        if (_button != null)
        {
            _defaultColors = _button.colors;
            _hasDefaults   = true;
        }
    }

    public void Bind(ILabAsset asset)
    {
        _asset         = asset;
        _nameText.text = asset.DisplayName;

        if (asset.Icon != null)
            _iconImage.sprite = asset.Icon;

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => Selected?.Invoke(this));

        SetSelected(false);
    }

    // Persistent selection tint, driven by AssetBrowserPanel. We push the green through the
    // button's own ColorBlock (normal/highlighted/selected) so it survives the button's hover/
    // press transitions; deselecting restores the prefab's original palette.
    public void SetSelected(bool selected)
    {
        if (_button == null || !_hasDefaults) return;

        if (!selected)
        {
            _button.colors = _defaultColors;
            return;
        }

        var colors = _defaultColors;
        colors.normalColor      = _selectedColor;
        colors.highlightedColor = _selectedColor;
        colors.selectedColor    = _selectedColor;
        colors.pressedColor     = Color.Lerp(_selectedColor, Color.black, 0.15f);
        _button.colors = colors;
    }
}
