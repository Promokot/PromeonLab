using UnityEngine;

[CreateAssetMenu(fileName = "GizmoConfig", menuName = "PromeonLab/Gizmo Config")]
public class GizmoConfig : ScriptableObject
{
    [SerializeField] private GameObject _gizmoPrefab;
    [SerializeField, Range(0.5f, 5f)]   private float _boundsCoefficient = 1.5f;
    [SerializeField, Range(0.01f, 1f)]  private float _minSize           = 0.1f;
    [SerializeField, Range(1f,    20f)] private float _maxSize           = 5f;

    [Header("Handle appearance")]
    [Tooltip("Solid fill material for gizmo handle meshes. Reuse the bone material so the gizmo reads " +
             "consistently with the rig bones. Instanced per-part at runtime and tinted to each axis color.")]
    [SerializeField] private Material _handleMaterial;

    public GameObject GizmoPrefab       => _gizmoPrefab;
    public float      BoundsCoefficient => _boundsCoefficient;
    public float      MinSize           => _minSize;
    public float      MaxSize           => _maxSize;
    public Material   HandleMaterial    => _handleMaterial;
}
