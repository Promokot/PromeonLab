using UnityEngine;

// Pure build-time decision: a SkinnedMeshRenderer's skeleton → a serializable RigDefinition (bone
// names only for Slice A). Returns null when there is no usable skeleton, so the rig gracefully
// degrades to a static object. Builds no GameObjects.
public static class RigDefinitionExtractor
{
    public static RigDefinition FromSkinnedMesh(SkinnedMeshRenderer smr)
    {
        if (smr == null || smr.bones == null || smr.bones.Length == 0) return null;

        var def = new RigDefinition { AssetId = smr.gameObject.name };
        foreach (var bone in smr.bones)
            if (bone != null)
                def.Bones.Add(new BoneRecord { BoneName = bone.name });

        return def.Bones.Count > 0 ? def : null;
    }
}
