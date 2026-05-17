using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AssetPropertiesView : MonoBehaviour
{
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _typeText;
    [SerializeField] private Image    _iconImage;

    public virtual void Bind(ILabAsset asset)
    {
        if (_nameText != null) _nameText.text = asset.DisplayName;
        if (_typeText != null) _typeText.text  = asset.Type.ToString();
        if (_iconImage != null && asset.Icon != null)
            _iconImage.sprite = asset.Icon;
    }
}
