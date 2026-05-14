using UnityEngine;

public class SelectionInteractorFactory : IInteractableFactory
{
    private readonly SelectionManager _selectionManager;

    public SelectionInteractorFactory(SelectionManager selectionManager)
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
