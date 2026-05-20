using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using VContainer;

public class RigRuntime : MonoBehaviour, IRigRuntime
{
    [SerializeField] private GameObject _boneProxyPrefab;

    private ISelectionManager     _selectionManager;
    private readonly List<BoneProxy> _proxies = new();

    [Inject]
    public void Construct(ISelectionManager selectionManager)
    {
        _selectionManager = selectionManager;
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
        ClearProxies();

        var animator = smr.GetComponentInParent<Animator>();
        if (animator == null) animator = smr.gameObject.AddComponent<Animator>();

        var rigGo = new GameObject("_Rig");
        rigGo.transform.SetParent(smr.transform, worldPositionStays: false);
        var rig = rigGo.AddComponent<Rig>();

        var rigBuilder = animator.gameObject.GetComponent<RigBuilder>();
        if (rigBuilder == null) rigBuilder = animator.gameObject.AddComponent<RigBuilder>();
        rigBuilder.layers.Add(new RigLayer(rig));

        var boneRenderer = animator.gameObject.GetComponent<PromeonBoneRenderer>();
        if (boneRenderer == null) boneRenderer = animator.gameObject.AddComponent<PromeonBoneRenderer>();
        var transforms = new List<Transform>();
        foreach (var bone in definition.Bones)
        {
            var t = FindBone(smr, bone.BoneName);
            if (t != null) transforms.Add(t);
        }
        var field = typeof(BoneRenderer).GetField("m_Transforms",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null) field.SetValue(boneRenderer, transforms.ToArray());
        boneRenderer.Rebuild();

        foreach (var bone in definition.Bones)
        {
            var boneTr = FindBone(smr, bone.BoneName);
            if (boneTr == null || _boneProxyPrefab == null) continue;

            var proxyGo = Instantiate(_boneProxyPrefab, boneTr.position, boneTr.rotation);
            var proxy   = proxyGo.GetComponent<BoneProxy>();
            var nodeId  = $"bone_{bone.BoneName}";
            proxy.Construct(_selectionManager);
            proxy.Init(bone.BoneName, boneTr, nodeId);
            _proxies.Add(proxy);
        }

        foreach (var chain in definition.IkChains)
            AddTwoBoneIK(rigGo.transform, smr, chain);

        rigBuilder.Build();
    }

    private void AddTwoBoneIK(Transform rigTransform, SkinnedMeshRenderer smr, IkChainRecord chain)
    {
        var ikGo = new GameObject($"IK_{chain.RootBone}_{chain.EndBone}");
        ikGo.transform.SetParent(rigTransform, false);

        var constraint      = ikGo.AddComponent<TwoBoneIKConstraint>();
        constraint.data.root = FindBone(smr, chain.RootBone);
        constraint.data.mid  = FindMidBone(smr, chain.RootBone, chain.EndBone);
        constraint.data.tip  = FindBone(smr, chain.EndBone);
        constraint.weight    = chain.Weight;

        var target = new GameObject($"Target_{chain.EndBone}");
        target.transform.SetParent(rigTransform, false);
        if (constraint.data.tip != null)
            target.transform.SetPositionAndRotation(constraint.data.tip.position, constraint.data.tip.rotation);
        constraint.data.target = target.transform;
    }

    private Transform FindBone(SkinnedMeshRenderer smr, string boneName)
    {
        foreach (var b in smr.bones)
            if (b.name == boneName) return b;
        return null;
    }

    private Transform FindMidBone(SkinnedMeshRenderer smr, string root, string end)
    {
        bool inChain = false;
        foreach (var b in smr.bones)
        {
            if (b.name == root) { inChain = true; continue; }
            if (inChain && b.name != end) return b;
            if (b.name == end) break;
        }
        return null;
    }

    private void ClearProxies()
    {
        foreach (var p in _proxies)
            if (p != null) Destroy(p.gameObject);
        _proxies.Clear();
    }
}
