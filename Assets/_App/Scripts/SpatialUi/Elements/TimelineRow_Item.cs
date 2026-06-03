using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TimelineRow_Item : MonoBehaviour
{
    [SerializeField] private RectTransform       _nameSegment;
    [SerializeField] private TMP_Text            _nameLabel;
    [SerializeField] private Image               _activeBackground;
    [SerializeField] private RectTransform       _keyStrip;
    [SerializeField] private RectTransform       _keyPrefab;
    [SerializeField] private AnimatorPanelConfig _config;

    private readonly List<RectTransform> _keyPool = new();
    private string _trackNodeId;
    private bool   _isBone;

    public string TrackNodeId => _trackNodeId;

    public void Bind(string trackNodeId, string displayName, bool isBone, Action onClick)
    {
        _trackNodeId = trackNodeId;
        _isBone      = isBone;

        if (_nameLabel != null)
        {
            _nameLabel.text         = displayName;
            _nameLabel.overflowMode = TextOverflowModes.Ellipsis;
        }

        if (_config != null)
        {
            if (_nameSegment != null)
            {
                var sd = _nameSegment.sizeDelta; sd.x = _config.TrackNameWidth; _nameSegment.sizeDelta = sd;
            }
            if (_keyStrip != null)
            {
                var om = _keyStrip.offsetMin; om.x = _config.TrackNameWidth; _keyStrip.offsetMin = om;
            }
            var le = GetComponent<LayoutElement>();
            if (le != null) { le.minHeight = _config.RowHeight; le.preferredHeight = _config.RowHeight; }
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

    public void SetKeys(IReadOnlyList<int> frames, int currentFrame)
    {
        DeactivateAll();
        if (_keyStrip == null || _keyPrefab == null || _config == null) return;

        for (int i = 0; i < frames.Count; i++)
        {
            int f   = frames[i];
            var key = GetOrCreateKey(i);
            key.anchoredPosition = new Vector2(f * _config.FramePx, 0f);

            bool isSel = f == currentFrame;
            var img = key.GetComponent<Image>();
            if (img != null)
                img.color = isSel
                    ? _config.KeyColor_Selected
                    : (_isBone ? _config.KeyColor_Bone : _config.KeyColor_Object);

            float size = isSel ? _config.KeySizeSelected : _config.KeySize;
            key.sizeDelta = new Vector2(size, size);
            key.gameObject.SetActive(true);
        }
    }

    private RectTransform GetOrCreateKey(int idx)
    {
        while (_keyPool.Count <= idx)
        {
            var k = Instantiate(_keyPrefab, _keyStrip);
            k.gameObject.SetActive(false);
            _keyPool.Add(k);
        }
        return _keyPool[idx];
    }

    private void DeactivateAll()
    {
        foreach (var k in _keyPool) if (k != null) k.gameObject.SetActive(false);
    }
}
