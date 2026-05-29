using UnityEngine;

public class RegionMember : MonoBehaviour, IRegionSurface
{
    [SerializeField] private string _moduleId;

    private IRegionSurface _custom;
    private bool _resolved;

    public string ModuleId => _moduleId;

    private IRegionSurface Custom
    {
        get
        {
            if (!_resolved)
            {
                _resolved = true;
                foreach (var s in GetComponents<IRegionSurface>())
                    if (!ReferenceEquals(s, this)) { _custom = s; break; }
            }
            return _custom;
        }
    }

    public bool IsOpen => Custom != null ? Custom.IsOpen : gameObject.activeSelf;

    public void Show()
    {
        if (Custom != null) Custom.Show();
        else gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (Custom != null) Custom.Hide();
        else gameObject.SetActive(false);
    }
}
