using System.Collections.Generic;
using UnityEngine;

/// Central mapping between the InteractionLayer enum, Unity physics layers, and
/// raycast priority. Priority = enum declaration order (GizmoHandles = 0 = highest).
public static class InteractionLayers
{
    private const int NotCached = -2; // -1 is "layer not found"; -2 = not yet looked up.

    private static readonly int[] _unityLayers = { NotCached, NotCached, NotCached };
    private static int _mask = NotCached;

    /// Raycast priority of a layer: smaller wins. Equals the enum's integer value.
    public static int Priority(InteractionLayer layer) => (int)layer;

    /// Unity physics layer index for the given interaction layer (cached). -1 if the
    /// named layer was never created in ProjectSettings.
    public static int UnityLayer(InteractionLayer layer)
    {
        int i = (int)layer;
        if (_unityLayers[i] == NotCached)
            _unityLayers[i] = LayerMask.NameToLayer(layer.ToString());
        return _unityLayers[i];
    }

    /// Combined raycast LayerMask of all interaction layers that exist (cached).
    public static int Mask
    {
        get
        {
            if (_mask == NotCached)
            {
                _mask = 0;
                foreach (InteractionLayer l in System.Enum.GetValues(typeof(InteractionLayer)))
                {
                    int unity = UnityLayer(l);
                    if (unity >= 0) _mask |= 1 << unity;
                }
            }
            return _mask;
        }
    }

    /// Maps a Unity physics layer index back to an interaction-layer priority.
    /// Returns false if the layer is not one of the interaction layers.
    public static bool TryGetPriority(int unityLayer, out int priority)
    {
        foreach (InteractionLayer l in System.Enum.GetValues(typeof(InteractionLayer)))
        {
            if (UnityLayer(l) == unityLayer && unityLayer >= 0)
            {
                priority = Priority(l);
                return true;
            }
        }
        priority = 0;
        return false;
    }

    /// Pure selection: given parallel lists of priorities (smaller = higher) and
    /// distances, return the index of the winner — highest priority, nearest within
    /// ties. Returns -1 when the lists are empty. The lists must be the same length.
    public static int PickWinnerIndex(IReadOnlyList<int> priorities, IReadOnlyList<float> distances)
    {
        int best = -1;
        for (int i = 0; i < priorities.Count; i++)
        {
            if (best < 0
                || priorities[i] < priorities[best]
                || (priorities[i] == priorities[best] && distances[i] < distances[best]))
            {
                best = i;
            }
        }
        return best;
    }
}
