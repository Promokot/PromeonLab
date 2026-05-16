using UnityEngine;

public class SceneNode : MonoBehaviour
{
    public string   NodeId      { get; private set; }
    public AssetRef AssetRef    { get; private set; }
    public string   DisplayName { get; private set; }
    public bool     IsVisible   { get; private set; } = true;
    public bool     IsLocked    { get; private set; }

    public void Init(string nodeId, AssetRef assetRef, string displayName)
    {
        NodeId      = nodeId;
        AssetRef    = assetRef;
        DisplayName = displayName;
    }

    public void SetDisplayName(string name)
    {
        DisplayName = name;
        gameObject.name = name;
    }

    public void SetVisible(bool visible)
    {
        IsVisible = visible;
        gameObject.SetActive(visible);
    }

    public void SetLocked(bool locked) => IsLocked = locked;
}
