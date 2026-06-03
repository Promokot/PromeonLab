using UnityEngine;
using UnityEngine.UI;

public class OutlinerNode_Rig_Item : OutlinerNode_Item
{
    [SerializeField] private Image _backgroundTint;
    [SerializeField] private Image _iconImage;
    [SerializeField] private Color _bonesOnColor  = new Color(0.31f, 0.51f, 0.94f, 1f);
    [SerializeField] private Color _bonesOffColor = Color.white;

    public void SetBonesMode(bool active)
    {
        var col = active ? _bonesOnColor : _bonesOffColor;
        if (_backgroundTint != null) _backgroundTint.color = col;
        if (_iconImage      != null) _iconImage     .color = col;
    }
}
