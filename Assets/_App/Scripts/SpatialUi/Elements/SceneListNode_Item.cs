using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SceneListNode_Item : MonoBehaviour
{
    [SerializeField] private TMP_Text _label;
    [SerializeField] private Image    _background;
    [SerializeField] private Button   _button;

    [SerializeField] private Color _normalColor   = new Color(0f, 0f, 0f, 0f);
    [SerializeField] private Color _selectedColor = new Color(0.3f, 0.6f, 1f, 0.4f);

    public string SceneId     { get; private set; }
    public string DisplayName { get; private set; }

    public event Action<SceneListNode_Item> Clicked;

    public void Init(string sceneId, string displayName)
    {
        SceneId     = sceneId;
        DisplayName = displayName;
        _label.text = displayName;
        _button.onClick.AddListener(() => Clicked?.Invoke(this));
        SetSelected(false);
    }

    public void SetSelected(bool selected) =>
        _background.color = selected ? _selectedColor : _normalColor;
}
