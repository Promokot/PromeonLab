using UnityEngine;

// Build-time seam: measures a freshly-built entity and returns a LOCAL-SPACE collider descriptor
// to bake into the recipe. NOT used at restore (restore applies the stored descriptor verbatim).
public interface IColliderStrategy
{
    void Measure(GameObject root, out ColliderKind kind, out Vector3 center, out Vector3 size);
}
