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

    [Header("Drag feel (displacement-driven)")]
    [Tooltip("Controller travel (metres) before a drag direction locks; also the baseline so there is no pop at lock.")]
    [SerializeField, Range(0.001f, 0.1f)] private float _deadzoneMeters = 0.02f;
    [Tooltip("Scale gain: factor = exp(gain × metres). ~ln2/0.15 ≈ 4.62 → ×2 per 15 cm.")]
    [SerializeField, Range(0.5f, 20f)]    private float _scaleGain      = 4.62f;
    [Tooltip("Rotation gain: degrees per metre of controller displacement. 1200 → a full turn per 30 cm.")]
    [SerializeField, Range(60f, 3600f)]   private float _rotGain        = 1200f;

    public GameObject GizmoPrefab       => _gizmoPrefab;
    public float      BoundsCoefficient => _boundsCoefficient;
    public float      MinSize           => _minSize;
    public float      MaxSize           => _maxSize;
    public float      FixedSize         => _fixedSize;
    public Material   ActiveMaterial    => _activeMaterial;
    public float      DeadzoneMeters    => _deadzoneMeters;
    public float      ScaleGain         => _scaleGain;
    public float      RotGain           => _rotGain;
}
