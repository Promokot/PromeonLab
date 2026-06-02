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
    [SerializeField] private Color _selectedColor = new Color(0.20f, 1f, 0.35f, 1f);

    private ILabAsset _asset;
    private Graphic   _background;
    private Color     _backgroundDefault;
    private bool      _hasBackground;

    public event Action<LabAssetCard> Selected;

    public ILabAsset Asset => _asset;

    private void Awake() => CacheBackground();

    private void CacheBackground()
    {
        if (_hasBackground || _button == null) return;
        _background = _button.targetGraphic;
        if (_background != null)
        {
            _backgroundDefault = _background.color;
            _hasBackground     = true;
        }
    }

    public void Bind(ILabAsset asset, Sprite icon)
    {
        _asset         = asset;
        _nameText.text = asset.DisplayName;

        if (icon != null)
            _iconImage.sprite = icon;

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => Selected?.Invoke(this));

        SetSelected(false);
    }

    // Persistent selection tint, driven by AssetBrowserPanel. We set the background Graphic's own
    // color directly (NOT the button's ColorBlock): the button's ColorTint transition multiplies
    // its state color by this graphic color, so driving selection through the ColorBlock would get
    // multiplied down by a non-white background and render dark/invisible. Setting the graphic color
    // and leaving the (≈white) button states alone renders the exact picked color; deselecting
    // restores the prefab's original background color.
    public void SetSelected(bool selected)
    {
        CacheBackground();
        if (!_hasBackground) return;

        _background.color = selected ? _selectedColor : _backgroundDefault;
    }
}
