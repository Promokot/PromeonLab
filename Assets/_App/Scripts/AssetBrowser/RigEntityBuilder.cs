// Slice 1: a Rig import behaves exactly like a static Object (selectable static skinned mesh).
// Slice 2 will replace this with runtime proxy-rig building + a bone descriptor in the recipe.
public class RigEntityBuilder : ObjectEntityBuilder
{
    public RigEntityBuilder(AssetSourceStore store, GltfModelLoader loader, IColliderStrategy collider)
        : base(store, loader, collider) { }

    public override AssetType HandledType => AssetType.Rig;
}
