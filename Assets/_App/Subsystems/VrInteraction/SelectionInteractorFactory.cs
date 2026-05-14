using UnityEngine;

public class SelectionInteractorFactory : IInteractableFactory
{
    private readonly ISelectionManager _selectionManager;

    public SelectionInteractorFactory(ISelectionManager selectionManager)
    {
        _selectionManager = selectionManager;
    }

    public void MakeInteractable(GameObject go)
    {
        if (go.GetComponentInChildren<Collider>() == null)
            go.AddComponent<BoxCollider>();

        var si = go.AddComponent<SelectionInteractor>();
        si.Construct(_selectionManager);
    }
}
