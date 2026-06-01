using System.Linq;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public class RigRuntime : MonoBehaviour, IRigRuntime
{
    private IObjectResolver _resolver;
    private RigEntityFactory _factory;

    [Inject]
    public void Construct(IObjectResolver resolver, RigEntityFactory factory)
    {
        _resolver = resolver;
        _factory  = factory;
    }

    public RigDefinition BuildFromSkinnedMesh(SkinnedMeshRenderer smr)
        => RigDefinitionExtractor.FromSkinnedMesh(smr)
           ?? new RigDefinition { AssetId = smr != null ? smr.gameObject.name : "" };

    public void ApplyDefinition(RigDefinition definition, SkinnedMeshRenderer smr)
    {
        if (smr == null) return;
        var rigRoot   = smr.transform.root.gameObject;
        var boneNames = definition?.Bones != null && definition.Bones.Count > 0
            ? definition.Bones.Select(b => b.BoneName).ToList()
            : null;

        _factory.BuildProxyRig(rigRoot, boneNames,
            definition != null ? definition.TerminalBonesAxis : TerminalBoneAxis.Auto,
            definition != null && definition.InvertTerminalBonesAxis);

        // Proxies get programmatic Selectable/XRPromeonInteractable/SceneNode + ProxyRigRuntime;
        // wire their [Inject] deps now (recursive over children).
        _resolver?.InjectGameObject(rigRoot);
    }
}
