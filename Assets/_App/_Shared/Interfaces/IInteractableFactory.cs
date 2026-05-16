using UnityEngine;

public interface IInteractableFactory
{
    void MakeInteractable(GameObject go, AssetCapabilities capabilities);
}
