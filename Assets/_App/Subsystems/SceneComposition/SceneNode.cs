using UnityEngine;

public class SceneNode : MonoBehaviour
{
    [SerializeField] private string   _nodeId;
    [SerializeField] private AssetRef _assetRef;
    [SerializeField] private string   _displayName;
    [SerializeField] private bool     _isVisible = true;
    [SerializeField] private bool     _isLocked;

    public string   NodeId      => _nodeId;
    public AssetRef AssetRef    => _assetRef;
    public string   DisplayName => _displayName;
    public bool     IsVisible   => _isVisible;
    public bool     IsLocked    => _isLocked;

    public void Init(string nodeId, AssetRef assetRef, string displayName)
    {
        _nodeId      = nodeId;
        _assetRef    = assetRef;
        _displayName = displayName;
    }

    public void SetNodeId(string newId) => _nodeId = newId;

    public void SetDisplayName(string name)
    {
        _displayName = name;
        gameObject.name = name;
    }

    public void SetVisible(bool visible)
    {
        _isVisible = visible;
        gameObject.SetActive(visible);
    }

    public void SetLocked(bool locked) => _isLocked = locked;
}
