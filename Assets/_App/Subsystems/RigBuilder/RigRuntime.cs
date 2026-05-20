using System.Collections.Generic;
using UnityEngine;

public class RigRuntime : MonoBehaviour, IRigRuntime
{
    [SerializeField] private Material _boneMaterial;

    public RigDefinition BuildFromSkinnedMesh(SkinnedMeshRenderer smr)
    {
        var def = new RigDefinition { AssetId = smr.gameObject.name };
        foreach (var bone in smr.bones)
            def.Bones.Add(new BoneRecord { BoneName = bone.name });
        return def;
    }

    public void ApplyDefinition(RigDefinition definition, SkinnedMeshRenderer smr)
    {
        var boneRenderer = smr.GetComponentInParent<PromeonInteractableRigBuilder>();
        if (boneRenderer == null)
            boneRenderer = smr.gameObject.AddComponent<PromeonInteractableRigBuilder>();

        if (_boneMaterial != null) boneRenderer.SetMaterial(_boneMaterial);

        var bones = new List<Transform>();
        foreach (var bone in definition.Bones)
        {
            var t = FindBone(smr, bone.BoneName);
            if (t != null) bones.Add(t);
        }
        boneRenderer.SetTransforms(bones.ToArray());
        boneRenderer.Rebuild();
    }

    private Transform FindBone(SkinnedMeshRenderer smr, string boneName)
    {
        foreach (var b in smr.bones)
            if (b.name == boneName) return b;
        return null;
    }
}
