using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class AnimatorRulerView : MonoBehaviour
{
    [SerializeField] private RectTransform       _content;
    [SerializeField] private RectTransform       _tickPrefab;
    [SerializeField] private TMP_Text            _labelPrefab;
    [SerializeField] private AnimatorPanelConfig _config;

    private readonly List<RectTransform> _tickPool  = new();
    private readonly List<TMP_Text>      _labelPool = new();

    public void Rebuild(int totalFrames)
    {
        if (_content == null || _tickPrefab == null || _config == null) return;

        DeactivateAll();

        int needed = totalFrames + 1;

        for (int f = 0; f < needed; f++)
        {
            var tick = GetOrCreateTick(f);
            tick.anchoredPosition = new Vector2(f * _config.FramePx, 0f);
            bool major = f % _config.MajorTickInterval == 0;
            var sz = tick.sizeDelta;
            sz.y = major ? _config.MajorTickHeight : _config.MinorTickHeight;
            tick.sizeDelta = sz;
            tick.gameObject.SetActive(true);

            if (major)
            {
                var lbl = GetOrCreateLabel(f);
                ((RectTransform)lbl.transform).anchoredPosition = new Vector2(f * _config.FramePx, 0f);
                lbl.text = f.ToString();
                lbl.gameObject.SetActive(true);
            }
        }
    }

    private RectTransform GetOrCreateTick(int idx)
    {
        while (_tickPool.Count <= idx)
        {
            var t = Instantiate(_tickPrefab, _content);
            t.gameObject.SetActive(false);
            _tickPool.Add(t);
        }
        return _tickPool[idx];
    }

    private TMP_Text GetOrCreateLabel(int idx)
    {
        while (_labelPool.Count <= idx)
        {
            var l = Instantiate(_labelPrefab, _content);
            l.gameObject.SetActive(false);
            _labelPool.Add(l);
        }
        return _labelPool[idx];
    }

    private void DeactivateAll()
    {
        foreach (var t in _tickPool)  if (t != null) t.gameObject.SetActive(false);
        foreach (var l in _labelPool) if (l != null) l.gameObject.SetActive(false);
    }
}
