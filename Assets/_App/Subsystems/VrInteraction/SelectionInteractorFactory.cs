using UnityEngine;
using VContainer;

public class SelectionInteractorFactory : IInteractableFactory
{
    private readonly ISelectionManager _selectionManager;
    private readonly IObjectResolver   _resolver;
    private GizmoController            _gizmoCached;

    public SelectionInteractorFactory(ISelectionManager selectionManager, IObjectResolver resolver)
    {
        _selectionManager = selectionManager;
        _resolver         = resolver;
    }

    public void MakeInteractable(GameObject go, AssetCapabilities capabilities)
    {
        if ((capabilities & AssetCapabilities.Selectable) == 0)
            return;

        // Use existing colliders (root or children). Only fall back to a default BoxCollider if none exist.
        var existing = go.GetComponentsInChildren<Collider>(includeInactive: true);
        if (existing.Length == 0)
        {
            go.AddComponent<BoxCollider>();
            existing = go.GetComponentsInChildren<Collider>(includeInactive: true);
        }

        var sn  = go.GetComponent<SceneNode>();
        var sel = go.GetComponent<Selectable>() ?? go.AddComponent<Selectable>();
        if (sn != null) sel.Init(sn);

        _gizmoCached ??= _resolver.Resolve<GizmoController>();

        var xri = go.GetComponent<XRPromeonInteractable>() ?? go.AddComponent<XRPromeonInteractable>();
        xri.RegisterColliders(existing);
        xri.Construct(_selectionManager, _gizmoCached);
    }
}
