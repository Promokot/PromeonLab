using UnityEngine;
using VContainer;

public class BoneInteractableFactory : IBoneInteractableFactory
{
    private readonly ISelectionManager _selectionManager;
    private readonly IObjectResolver   _resolver;
    private GizmoController            _gizmoCached;

    public BoneInteractableFactory(ISelectionManager selectionManager, IObjectResolver resolver)
    {
        _selectionManager = selectionManager;
        _resolver         = resolver;
    }

    public void MakeBoneInteractable(GameObject proxyGo)
    {
        if (proxyGo == null) return;

        var sn = proxyGo.GetComponent<SceneNode>();
        // Proxies are nested (proxy_spine is child of proxy_pelvis). Use GetComponents (not InChildren)
        // so each bone owns only its own collider — otherwise child colliders get registered on the parent.
        var existing = proxyGo.GetComponents<Collider>();

        var sel = proxyGo.GetComponent<Selectable>() ?? proxyGo.AddComponent<Selectable>();
        if (sn != null) sel.Init(sn);

        _gizmoCached ??= _resolver.Resolve<GizmoController>();

        var xri = proxyGo.GetComponent<XRPromeonInteractable>() ?? proxyGo.AddComponent<XRPromeonInteractable>();
        xri.RegisterColliders(existing);
        xri.Construct(_selectionManager, _gizmoCached);
    }
}
