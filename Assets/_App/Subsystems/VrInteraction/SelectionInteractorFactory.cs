using UnityEngine;

public class SelectionInteractorFactory : IInteractableFactory
{
    private readonly ISelectionManager _selectionManager;

    public SelectionInteractorFactory(ISelectionManager selectionManager)
    {
        _selectionManager = selectionManager;
    }

    public void MakeInteractable(GameObject go, AssetCapabilities capabilities)
    {
        if ((capabilities & AssetCapabilities.Selectable) == 0)
            return;

        if (go.GetComponent<Collider>() == null)
            go.AddComponent<BoxCollider>();

        var sn = go.GetComponent<SceneNode>();
        var sel = go.GetComponent<Selectable>() ?? go.AddComponent<Selectable>();
        if (sn != null) sel.Init(sn);

        var si = go.GetComponent<SelectionInteractor>() ?? go.AddComponent<SelectionInteractor>();
        si.Construct(_selectionManager);
    }
}
