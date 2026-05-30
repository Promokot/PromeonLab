using UnityEngine;

/// Holds the two outline material assets (mask + fill) used by the QuickOutline `Outline` component.
/// Referenced directly (no Resources.Load / Shader.Find) and handed to each `Outline` via its
/// `OutlineConfig` setter at creation. The materials point at the forked `PromeonLab/Outline*` shaders.
[CreateAssetMenu(fileName = "OutlineConfig", menuName = "PromeonLab/Outline Config")]
public class OutlineConfig : ScriptableObject
{
    [SerializeField] private Material maskMaterial;
    [SerializeField] private Material fillMaterial;

    public Material MaskMaterial => maskMaterial;
    public Material FillMaterial => fillMaterial;
}
