using UnityEngine;

// Default strategy: one Box covering every child Renderer, expressed in root-local space so the
// stored center/size stay valid regardless of the root's spawn rotation.
public class BoundsBoxColliderStrategy : IColliderStrategy
{
    public void Measure(GameObject root, out ColliderKind kind, out Vector3 center, out Vector3 size)
    {
        kind = ColliderKind.Box;

        var renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
        if (renderers.Length == 0)
        {
            center = Vector3.zero;
            size   = Vector3.one * 0.1f;   // tiny fallback so the object is still hittable
            return;
        }

        bool has = false;
        var min  = Vector3.positiveInfinity;
        var max  = Vector3.negativeInfinity;
        var toLocal = root.transform.worldToLocalMatrix;

        foreach (var r in renderers)
        {
            var b = r.bounds; // world AABB
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? b.min.x : b.max.x,
                    (i & 2) == 0 ? b.min.y : b.max.y,
                    (i & 4) == 0 ? b.min.z : b.max.z);
                var local = toLocal.MultiplyPoint3x4(corner);
                min = Vector3.Min(min, local);
                max = Vector3.Max(max, local);
                has = true;
            }
        }

        if (!has) { center = Vector3.zero; size = Vector3.one * 0.1f; return; }
        center = (min + max) * 0.5f;
        size   = max - min;
    }
}
