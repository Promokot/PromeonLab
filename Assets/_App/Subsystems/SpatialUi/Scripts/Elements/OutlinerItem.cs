using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OutlinerItem : MonoBehaviour
{
    [SerializeField] private TMP_Text      _label;
    [SerializeField] private Image         _highlight;
    [SerializeField] private LayoutElement _indentSpacer;
    [SerializeField] private Button        _button;

    public string NodeId { get; private set; }

    public virtual void Bind(SceneNode node, float indentPx, Action onClick)
    {
        NodeId      = node.NodeId;
        _label.text = node.DisplayName;
        if (_indentSpacer != null) _indentSpacer.preferredWidth = indentPx;
        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => onClick());
    }

    public void SetVisualState(SelectionVisual state)
    {
        if (_highlight == null) return;
        _highlight.enabled = state != SelectionVisual.None;
        _highlight.color = state == SelectionVisual.Selected
            ? new Color(1f, 0.95f, 0.15f, 0.35f)
            : Color.clear;
    }

    public void SetLabel(string newName)
    {
        if (_label != null) _label.text = newName;
    }
}
