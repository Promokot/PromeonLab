using UnityEngine;

/// The single funnel for assigning an interaction layer to the GameObject that
/// carries the collider. Maps the enum to its Unity layer via InteractionLayers.
public static class InteractionLayerExtensions
{
    public static void SetInteractionLayer(this GameObject go, InteractionLayer layer)
    {
        if (go == null) return;
        int unity = InteractionLayers.UnityLayer(layer);
        if (unity < 0)
        {
            Debug.LogError($"SetInteractionLayer: Unity layer '{layer}' is missing – create it in " +
                           $"ProjectSettings > Tags and Layers. Leaving '{go.name}' on its current layer.");
            return;
        }
        go.layer = unity;
    }
}
