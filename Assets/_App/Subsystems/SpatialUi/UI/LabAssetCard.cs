using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LabAssetCard : MonoBehaviour
{
    [SerializeField] private Image    _iconImage;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private Button   _button;

    private ILabAsset _asset;

    public event Action<LabAssetCard> Selected;

    public ILabAsset Asset => _asset;

    public void Bind(ILabAsset asset)
    {
        _asset         = asset;
        _nameText.text = asset.DisplayName;

        if (asset.Icon != null)
            _iconImage.sprite = asset.Icon;

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => Selected?.Invoke(this));
    }
}
