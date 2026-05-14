using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AssetBrowserItem : MonoBehaviour
{
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private Button   _button;

    public string NodeId { get; private set; }

    public event Action<AssetBrowserItem> Clicked;

    public void Init(string nodeId, string displayName)
    {
        NodeId         = nodeId;
        _nameText.text = displayName;
        _button.onClick.AddListener(() => Clicked?.Invoke(this));
    }
}
