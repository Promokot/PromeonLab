// How the entity's selection collider is shaped. Int values are serialized in recipes — append only,
// never reorder. Box = single AABB; ConvexMesh = per-renderer convex hull; BoneBoxes = boxes along
// the skeleton (see BoneSelectorBoxPlanner).
public enum ColliderKind
{
    None       = 0,
    Box        = 1,
    ConvexMesh = 2,
    BoneBoxes  = 3,
}
