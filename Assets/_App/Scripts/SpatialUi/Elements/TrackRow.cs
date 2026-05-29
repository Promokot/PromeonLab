using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum TrackRowKind { Object, Rig, Bone }

public class TrackRow : MonoBehaviour
{
    [SerializeField] private TMP_Text             _label;
    [SerializeField] private Image                _icon;
    [SerializeField] private Image                _activeBackground;
    [SerializeField] private GameObject           _hasKeyDot;
    [SerializeField] private LayoutElement        _indent;
    [SerializeField] private AnimatorPanelConfig  _config;

    public string NodeId { get; private set; }

    public void Bind(string nodeId, string displayName, TrackRowKind kind, bool hasKeys, int indentLevel, Action onClick)
    {
        NodeId         = nodeId;
        _label.text    = displayName;
        if (_hasKeyDot != null) _hasKeyDot.SetActive(hasKeys);
        if (_indent    != null) _indent.preferredWidth = indentLevel * 18f;

        if (_icon != null && _config != null)
        {
            _icon.color = kind switch
            {
                TrackRowKind.Bone => _config.KeyColor_Bone,
                _                 => _config.KeyColor_Object
            };
        }

        var btn = GetComponentInChildren<Button>(true);
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => onClick());
        }
    }

    public void SetActive(bool active)
    {
        if (_activeBackground == null || _config == null) return;
        _activeBackground.color = active ? _config.TrackRow_Active : _config.TrackRow_Inactive;
    }
}
