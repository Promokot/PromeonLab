using UnityEngine;

// Build-time parameters for runtime proxy-rig construction (RigEntityFabricator.BuildProxyRig).
// Outline COLORS are not here — they come from OutlineConfig (BoneColor/BoneSelectedColor).
[CreateAssetMenu(menuName = "PromeonLab/ProxyRigConfig", fileName = "ProxyRigConfig")]
public class ProxyRigConfig : ScriptableObject
{
    [SerializeField] private Material _boneMaterial;
    [Tooltip("Material the bone's primary submesh swaps to while it is the selected bone " +
             "(assign an emissive 'warm orange'). Falls back to BoneMaterial if unassigned.")]
    [SerializeField] private Material _boneSelectedMaterial;
    [SerializeField] private float    _boneWidth = 0.06f;
    [Tooltip("Multiplier applied to BoneWidth to size the SELECTION colliders only (RigEntityFabricator's " +
             "min box thickness). The visible bone mesh stays on raw BoneWidth, so this fattens the " +
             "hitboxes — making bones easier to click — without changing how they look.")]
    [SerializeField] private float    _selectorThicknessMultiplier = 3f;
    [SerializeField] private bool     _useConvexCollider = true;

    public Material BoneMaterial                 => _boneMaterial;
    public Material BoneSelectedMaterial         => _boneSelectedMaterial;
    public float    BoneWidth                    => _boneWidth;
    public float    SelectorThicknessMultiplier  => _selectorThicknessMultiplier;
    public bool     UseConvexCollider            => _useConvexCollider;
}
