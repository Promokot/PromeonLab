using UnityEngine;

public interface IBoneInteractableFactory
{
    void MakeBoneInteractable(GameObject proxyGo);
    Transform GetBoneTransform(string nodeId);
}
