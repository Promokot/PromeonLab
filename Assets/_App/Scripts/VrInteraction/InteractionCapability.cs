using System.Collections.Generic;
using UnityEngine;

// The single definition of "make this GameObject a selectable scene entity".
// Pure application from the recipe's collider kind. Idempotent: skips if an XRPromeonInteractable is
// already present (a baked built-in prefab), so it can never disturb working assets.
//   Box        → one BoxCollider on the root (center/size from the recipe).
//   ConvexMesh → one convex MeshCollider per mesh-bearing renderer, registered to the interactable.
//   BoneBoxes  → built on the rig side (RigEntityFabricator) and registered via
//                ProxyRigRuntime.RegisterSelectorColliders(); nothing is built here.
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

        // 2) Build the collider shape for this kind. Box sits on the root (auto-discovered by the
        //    interactable's Awake); ConvexMesh colliders sit on child renderers and are registered
        //    explicitly below. BoneBoxes builds nothing here.
        List<Collider> childColliders = null;
        if (colliderKind == ColliderKind.Box)
        {
            var box    = root.AddComponent<BoxCollider>();
            box.center = colliderCenter;
            box.size   = colliderSize;
            box.gameObject.SetInteractionLayer(layer);
        }
        else if (colliderKind == ColliderKind.ConvexMesh)
        {
            // Layer is tagged at build (like Box, before the selectable gate) so the colliders are
            // never left on the default layer even when selectable == false.
            childColliders = BuildConvexColliders(root, layer);
            if (childColliders.Count == 0)
                Debug.LogWarning($"InteractionCapability: '{root.name}' produced no convex colliders (no readable mesh renderers).");
        }

        if (!selectable) return;

        // 3) Outline driver + input-driven select/move/rotate. DI (Construct) wired later by
        //    IObjectResolver.InjectGameObject at the call site.
        root.AddComponent<Selectable>();
        var interactable = root.AddComponent<XRPromeonInteractable>();

        // 4) Child colliders (ConvexMesh) aren't found by the interactable's root-only Awake scan —
        //    register them so a hit on any of them counts as a hit on the root entity.
        if (childColliders != null && childColliders.Count > 0)
            interactable.RegisterColliders(childColliders);

        // One re-tag covers the auto-discovered root Box and any registered child colliders.
        interactable.SetInteractionLayer(layer);
    }

    private static List<Collider> BuildConvexColliders(GameObject root, InteractionLayer layer)
    {
        var result = new List<Collider>();
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(includeInactive: true))
            AddConvex(mf.gameObject, mf.sharedMesh, layer, result);
        foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true))
            AddConvex(smr.gameObject, smr.sharedMesh, layer, result);
        return result;
    }

    private static void AddConvex(GameObject go, Mesh mesh, InteractionLayer layer, List<Collider> result)
    {
        if (mesh == null || go.GetComponent<MeshCollider>() != null) return;
        // Convex cooking needs a CPU-readable mesh; glTFast imports can be GPU-only. Skip (don't throw)
        // so one unreadable mesh never aborts the whole entity build on Quest.
        if (!mesh.isReadable)
        {
            Debug.LogWarning($"InteractionCapability: mesh '{mesh.name}' is not readable — skipping convex collider on '{go.name}'.");
            return;
        }
        var mc = go.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh;
        mc.convex     = true;
        go.SetInteractionLayer(layer);
        result.Add(mc);
    }
}
