using UnityEngine;

public class SceneNode : MonoBehaviour
{
    public string NodeId { get; private set; }
    public bool IsVisible { get; private set; } = true;
    public bool IsLocked  { get; private set; }

    public void Init(string nodeId) => NodeId = nodeId;

    public void SetVisible(bool visible)
    {
        IsVisible = visible;
        gameObject.SetActive(visible);
    }

    public void SetLocked(bool locked) => IsLocked = locked;
}
