using UnityEngine;

public static class BoundsFitter
{
    public static float ComputeSize(Transform target, float boundsCoefficient, float minSize, float maxSize)
    {
        var renderers = target.GetComponentsInChildren<Renderer>(includeInactive: false);
        if (renderers.Length == 0) return minSize;

        var combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            combined.Encapsulate(renderers[i].bounds);

        var e = combined.extents;
        var maxExtent = Mathf.Max(e.x, Mathf.Max(e.y, e.z));
        return Mathf.Clamp(maxExtent * boundsCoefficient, minSize, maxSize);
    }
}
