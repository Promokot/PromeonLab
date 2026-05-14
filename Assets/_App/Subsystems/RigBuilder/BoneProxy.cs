using UnityEngine;
using VContainer;

public class BoneProxy : MonoBehaviour
{
    public string    BoneName      { get; private set; }
    public Transform BoneTransform { get; private set; }

    private ISelectionManager _selectionManager;
    private string            _nodeId;

    [Inject]
    public void Construct(ISelectionManager selectionManager)
    {
        _selectionManager = selectionManager;
    }

    public void Init(string boneName, Transform boneTransform, string nodeId)
    {
        BoneName       = boneName;
        BoneTransform  = boneTransform;
        _nodeId        = nodeId;
        gameObject.name = $"Proxy_{boneName}";
    }

    private void LateUpdate()
    {
        if (BoneTransform != null)
            transform.SetPositionAndRotation(BoneTransform.position, BoneTransform.rotation);
    }

    public void OnSelected() => _selectionManager?.Select(_nodeId);
}
