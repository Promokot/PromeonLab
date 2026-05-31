/// Interaction layers in priority order. Declaration order IS the priority:
/// index 0 (GizmoHandles) is highest. Each name MUST match an existing Unity layer
/// ("GizmoHandles"/"BoneProxies"/"SceneObjects") so layer.ToString() resolves directly.
public enum InteractionLayer
{
    GizmoHandles,
    BoneProxies,
    SceneObjects,
}
