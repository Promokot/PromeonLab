using UnityEngine;

// Build-time parameters for runtime proxy-rig construction (RigEntityFactory.BuildProxyRig).
// Outline COLORS are not here — they come from OutlineConfig (BoneColor/BoneSelectedColor).
[CreateAssetMenu(menuName = "PromeonLab/ProxyRigConfig", fileName = "ProxyRigConfig")]
public class ProxyRigConfig : ScriptableObject
{
    [SerializeField] private Material _boneMaterial;
    [SerializeField] private float    _boneWidth = 0.06f;
    [SerializeField] private bool     _useConvexCollider = true;

    public Material BoneMaterial      => _boneMaterial;
    public float    BoneWidth         => _boneWidth;
    public bool     UseConvexCollider => _useConvexCollider;
}
