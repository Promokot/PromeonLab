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
    [SerializeField] private GameObject    _iconObject;
    [SerializeField] private GameObject    _iconRig;

    public string NodeId { get; private set; }

    public void Bind(SceneNode node, float indentPx, Action onClick)
    {
        NodeId = node.NodeId;
        _label.text = node.DisplayName;
        if (_indentSpacer != null) _indentSpacer.preferredWidth = indentPx;
        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => onClick());

        var isRig = node.GetComponentInChildren<PromeonProxyRigBuilder>(includeInactive: true) != null;
        if (_iconObject != null) _iconObject.SetActive(!isRig);
        if (_iconRig    != null) _iconRig   .SetActive( isRig);
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
