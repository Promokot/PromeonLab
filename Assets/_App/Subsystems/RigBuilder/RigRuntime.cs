using System.Collections.Generic;
using UnityEngine;
using VContainer;

public class RigRuntime : MonoBehaviour, IRigRuntime
{
    [SerializeField] private Material _boneMaterial;

    private IBoneInteractableFactory _boneInteractableFactory;
    private EventBus                 _eventBus;

    [Inject]
    public void Construct(IBoneInteractableFactory boneInteractableFactory, EventBus bus)
    {
        _boneInteractableFactory = boneInteractableFactory;
        _eventBus                = bus;
    }

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

        var rigNode   = boneRenderer.GetComponentInParent<SceneNode>();
        var rigNodeId = rigNode != null ? rigNode.NodeId : null;
        boneRenderer.SetRigNodeId(rigNodeId);
        boneRenderer.SetEventBus(_eventBus);

        var bones = new List<Transform>();
        foreach (var bone in definition.Bones)
        {
            var t = FindBone(smr, bone.BoneName);
            if (t != null) bones.Add(t);
        }
        boneRenderer.SetTransforms(bones.ToArray());
        boneRenderer.Rebuild();

        if (_boneInteractableFactory != null)
        {
            foreach (var proxyGo in boneRenderer.ProxyGOs)
                _boneInteractableFactory.MakeBoneInteractable(proxyGo);
        }
    }

    private Transform FindBone(SkinnedMeshRenderer smr, string boneName)
    {
        foreach (var b in smr.bones)
            if (b.name == boneName) return b;
        return null;
    }
}
