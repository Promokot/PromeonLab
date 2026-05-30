using UnityEngine;

/// Holds the two outline material assets (mask + fill) used by the QuickOutline `Outline` component.
/// Referenced directly (no Resources.Load / Shader.Find) and handed to each `Outline` via its
/// `OutlineConfig` setter at creation. The materials point at the forked `PromeonLab/Outline*` shaders.
[CreateAssetMenu(fileName = "OutlineConfig", menuName = "PromeonLab/Outline Config")]
public class OutlineConfig : ScriptableObject
{
    [Header("Materials (forked PromeonLab/Outline* shaders)")]
    [SerializeField] private Material maskMaterial;
    [SerializeField] private Material fillMaterial;

    [Header("Gizmo axis colors")]
    [SerializeField] private Color axisColorX = new Color(0.93f, 0.58f, 0.58f);
    [SerializeField] private Color axisColorY = new Color(0.60f, 0.84f, 0.62f);
    [SerializeField] private Color axisColorZ = new Color(0.60f, 0.72f, 0.93f);

    [Header("Selection")]
    [SerializeField] private Color selectColor = new Color(1f, 0.95f, 0.15f);

    [Header("Bones")]
    [SerializeField] private Color boneColor         = Color.white;
    [SerializeField] private Color boneSelectedColor = new Color(1f, 0.5f, 0f);

    public Material MaskMaterial => maskMaterial;
    public Material FillMaterial => fillMaterial;

    public Color AxisColorX => axisColorX;
    public Color AxisColorY => axisColorY;
    public Color AxisColorZ => axisColorZ;
    public Color SelectColor => selectColor;
    public Color BoneColor => boneColor;
    public Color BoneSelectedColor => boneSelectedColor;
}
