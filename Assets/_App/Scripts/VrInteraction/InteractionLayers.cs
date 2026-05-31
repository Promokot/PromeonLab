using UnityEngine;

/// Maps the InteractionLayer enum to Unity physics layer indices (cached by name).
/// Layers are looked up by NAME, never by hardcoded index, so reshuffling layer slots in
/// ProjectSettings does not break anything.
public static class InteractionLayers
{
    private const int NotCached = -2; // -1 = "layer not found"; -2 = not yet looked up.
    private static readonly int[] _unityLayers = { NotCached, NotCached, NotCached };

    /// Unity physics layer index for the given interaction layer (cached). -1 if the
    /// named layer does not exist in ProjectSettings.
    public static int UnityLayer(InteractionLayer layer)
    {
        int i = (int)layer;
        if (_unityLayers[i] == NotCached)
            _unityLayers[i] = LayerMask.NameToLayer(layer.ToString());
        return _unityLayers[i];
    }
}
