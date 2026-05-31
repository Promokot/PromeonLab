using UnityEngine;

[CreateAssetMenu(fileName = "GizmoConfig", menuName = "PromeonLab/Gizmo Config")]
public class GizmoConfig : ScriptableObject
{
    [SerializeField] private GameObject _gizmoPrefab;
    [SerializeField, Range(0.5f, 5f)]   private float _boundsCoefficient = 1.5f;
    [SerializeField, Range(0.01f, 1f)]  private float _minSize           = 0.1f;
    [SerializeField, Range(1f,    20f)] private float _maxSize           = 5f;

    [Header("Fixed size (bounds-fit frozen)")]
    [Tooltip("Stable world size of the spawned gizmo. The per-object bounds-fit is frozen for now; " +
             "every gizmo spawns at this size regardless of the target's bounds.")]
    [SerializeField, Range(0.05f, 10f)] private float _fixedSize = 1f;

    [Header("Active handle material")]
    [Tooltip("Material whose look (emission + base color) the grabbed handle adopts. Assign " +
             "Gizmo_EmissiveSelected. Read once at spawn; not instanced onto the renderers.")]
    [SerializeField] private Material _activeMaterial;

    public GameObject GizmoPrefab       => _gizmoPrefab;
    public float      BoundsCoefficient => _boundsCoefficient;
    public float      MinSize           => _minSize;
    public float      MaxSize           => _maxSize;
    public float      FixedSize         => _fixedSize;
    public Material   ActiveMaterial    => _activeMaterial;
}
