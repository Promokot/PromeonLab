using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TimelineLane : MonoBehaviour
{
    [SerializeField] private RectTransform       _content;
    [SerializeField] private RectTransform       _keyPrefab;
    [SerializeField] private Image               _activeBackground;
    [SerializeField] private AnimatorPanelConfig _config;

    private readonly List<RectTransform> _keyPool = new();
    private string _trackNodeId;
    private bool   _isBone;

    public string TrackNodeId => _trackNodeId;

    public void Bind(string trackNodeId, bool isBone)
    {
        _trackNodeId = trackNodeId;
        _isBone      = isBone;
    }

    public void SetActive(bool active)
    {
        if (_activeBackground == null || _config == null) return;
        _activeBackground.color = active ? _config.TrackRow_Active : _config.TrackRow_Inactive;
    }

    public void SetKeys(IReadOnlyList<int> frames, int currentFrame)
    {
        DeactivateAll();
        if (_content == null || _keyPrefab == null || _config == null) return;

        for (int i = 0; i < frames.Count; i++)
        {
            int f   = frames[i];
            var key = GetOrCreateKey(i);
            key.anchoredPosition = new Vector2(f * _config.FramePx, 0f);

            var img = key.GetComponent<Image>();
            bool isSel = f == currentFrame;
            if (img != null)
            {
                img.color = isSel
                    ? _config.KeyColor_Selected
                    : (_isBone ? _config.KeyColor_Bone : _config.KeyColor_Object);
            }
            float size = isSel ? 26f : 22f;
            key.sizeDelta = new Vector2(size, size);

            key.gameObject.SetActive(true);
        }
    }

    private RectTransform GetOrCreateKey(int idx)
    {
        while (_keyPool.Count <= idx)
        {
            var k = Instantiate(_keyPrefab, _content);
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
