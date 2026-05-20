using System.Collections.Generic;
using UnityEngine;
using VContainer;

public class BoneInteractableFactory : IBoneInteractableFactory
{
    private readonly ISelectionManager                _selectionManager;
    private readonly IObjectResolver                  _resolver;
    private readonly Dictionary<string, SceneNode>    _bonesByNodeId = new();
    private GizmoController                           _gizmoCached;

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
        if (sn != null)
        {
            sel.Init(sn);
            _bonesByNodeId[sn.NodeId] = sn;
        }

        _gizmoCached ??= _resolver.Resolve<GizmoController>();

        var xri = proxyGo.GetComponent<XRPromeonInteractable>() ?? proxyGo.AddComponent<XRPromeonInteractable>();
        xri.RegisterColliders(existing);
        xri.Construct(_selectionManager, _gizmoCached);
    }

    public Transform GetBoneTransform(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId)) return null;
        if (!_bonesByNodeId.TryGetValue(nodeId, out var sn) || sn == null)
        {
            // Stale entry (proxy destroyed on Rebuild) — clean up lazily.
            _bonesByNodeId.Remove(nodeId);
            return null;
        }
        return sn.transform;
    }
}
