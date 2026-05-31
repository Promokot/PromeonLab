using UnityEngine;

/// The single funnel for assigning an interaction layer to the GameObject that
/// carries the collider. Maps the enum to its Unity layer via InteractionLayers.
public static class GameObjectInteractionLayerExtensions
{
    public static void SetInteractionLayer(this GameObject go, InteractionLayer layer)
    {
        if (go == null) return;
        int unity = InteractionLayers.UnityLayer(layer);
        if (unity < 0)
        {
            Debug.LogError($"SetInteractionLayer: Unity layer '{layer}' is missing — create it in " +
                           $"ProjectSettings > Tags and Layers. Leaving '{go.name}' on its current layer.");
            return;
        }
        go.layer = unity;
    }

    /// Sets the interaction layer on every GameObject in this hierarchy that carries a Collider —
    /// the objects the ray actually hits. Use for spawned multi-part assets whose colliders may
    /// live on child meshes (e.g. an imported FBX where the root holds XRPromeonInteractable but the
    /// child mesh holds the BoxColliders). The resolver only raycasts the interaction-layer mask, so
    /// a collider left on the Default layer would be invisible to it.
    public static void SetInteractionLayerOnColliders(this GameObject root, InteractionLayer layer)
    {
        if (root == null) return;
        foreach (var c in root.GetComponentsInChildren<Collider>(includeInactive: true))
            if (c != null) c.gameObject.SetInteractionLayer(layer);
    }
}
