using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public class RigRuntime : MonoBehaviour, IRigRuntime
{
    [SerializeField] private Material _boneMaterial;

    private IObjectResolver _resolver;

    [Inject]
    public void Construct(IObjectResolver resolver) => _resolver = resolver;

    public RigDefinition BuildFromSkinnedMesh(SkinnedMeshRenderer smr)
    {
        var def = new RigDefinition { AssetId = smr.gameObject.name };
        foreach (var bone in smr.bones)
            def.Bones.Add(new BoneRecord { BoneName = bone.name });
        return def;
    }

    public void ApplyDefinition(RigDefinition definition, SkinnedMeshRenderer smr)
    {
        var boneRenderer = smr.GetComponentInParent<PromeonProxyRigBuilder>();
        if (boneRenderer == null)
            boneRenderer = smr.gameObject.AddComponent<PromeonProxyRigBuilder>();

        if (_boneMaterial != null) boneRenderer.SetMaterial(_boneMaterial);

        var bones = new List<Transform>();
        foreach (var bone in definition.Bones)
        {
            var t = FindBone(smr, bone.BoneName);
            if (t != null) bones.Add(t);
        }
        boneRenderer.SetTransforms(bones.ToArray());
        boneRenderer.Rebuild();

        // Rebuild may have created brand-new proxy GameObjects (Selectable + XRPromeonInteractable +
        // BoneSceneNodeMarker + SceneNode are added programmatically by the builder). Their [Inject]
        // methods have not fired yet — wire DI deps now so they are functional immediately.
        if (_resolver != null) _resolver.InjectGameObject(boneRenderer.gameObject);
    }

    private Transform FindBone(SkinnedMeshRenderer smr, string boneName)
    {
        foreach (var b in smr.bones)
            if (b.name == boneName) return b;
        return null;
    }
}
