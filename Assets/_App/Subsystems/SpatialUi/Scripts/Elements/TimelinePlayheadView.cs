using TMPro;
using UnityEngine;

public class TimelinePlayheadView : MonoBehaviour
{
    [SerializeField] private RectTransform        _root;
    [SerializeField] private TMP_Text             _frameLabel;
    [SerializeField] private AnimatorPanelConfig  _config;

    public void SetFrame(int frame)
    {
        if (_root == null || _config == null) return;
        _root.anchoredPosition = new Vector2(frame * _config.FramePx, _root.anchoredPosition.y);
        if (_frameLabel != null) _frameLabel.text = frame.ToString();
    }

    public void SetHeight(float height)
    {
        if (_root == null) return;
        var size = _root.sizeDelta;
        size.y = height;
        _root.sizeDelta = size;
    }
}
