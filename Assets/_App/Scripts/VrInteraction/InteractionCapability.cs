using UnityEngine;

// The single definition of "make this GameObject a selectable scene entity".
// Pure application from primitive recipe values — makes no decisions. Used by runtime Restore now
// and editor bake later. Idempotent: skips if an XRPromeonInteractable is already present
// (a baked built-in prefab), so it can never disturb working assets.
public static class InteractionCapability
{
    public static void Apply(
        GameObject root,
        InteractionLayer layer,
        ColliderKind colliderKind,
        Vector3 colliderCenter,
        Vector3 colliderSize,
        bool selectable)
    {
        if (root == null) return;
        if (root.GetComponent<XRPromeonInteractable>() != null) return; // already a complete entity

        // 1) Identity FIRST so Selectable/XRPromeonInteractable Awake-time lookups resolve.
        //    NodeId is stamped later by SceneGraph.AddNode (re-uses this SceneNode).
        if (root.GetComponent<SceneNode>() == null) root.AddComponent<SceneNode>();

        // 2) Collider on the root (so _includeChildColliders can stay false).
        if (colliderKind == ColliderKind.Box)
        {
            var box    = root.AddComponent<BoxCollider>();
            box.center = colliderCenter;
            box.size   = colliderSize;
            box.gameObject.SetInteractionLayer(layer);
        }

        if (!selectable) return;

        // 3) Outline driver + input-driven select/move/rotate. DI (Construct) wired later by
        //    IObjectResolver.InjectGameObject at the call site.
        root.AddComponent<Selectable>();
        root.AddComponent<XRPromeonInteractable>().SetInteractionLayer(layer);
    }
}
