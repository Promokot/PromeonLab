using System.Collections.Generic;
using UnityEngine;

public class GizmoHierarchy : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private Transform _moveRoot;
    [SerializeField] private Transform _moveCenter;
    [SerializeField] private Transform _moveX;
    [SerializeField] private Transform _moveY;
    [SerializeField] private Transform _moveZ;

    [Header("Rotate")]
    [SerializeField] private Transform _rotateRoot;
    [SerializeField] private Transform _rotateX;
    [SerializeField] private Transform _rotateY;
    [SerializeField] private Transform _rotateZ;

    [Header("Scale")]
    [SerializeField] private Transform _scaleRoot;
    [SerializeField] private Transform _scaleCenter;
    [SerializeField] private Transform _scaleX;
    [SerializeField] private Transform _scaleY;
    [SerializeField] private Transform _scaleZ;

    [Header("Highlight")]
    [SerializeField] private Color _highlightColor = new Color(1f, 0.85f, 0.1f, 1f);

    private readonly Dictionary<Transform, TransformState> _defaultStates = new();

    // На GRIP DOWN сохраняем оригинальные sharedMaterials активной ручки, подменяем первый
    // (основной) на тонированный инстанс. Outline-проходы QuickOutline не трогаем — они идут
    // следующими в массиве. На release возвращаем оригиналы и уничтожаем созданные инстансы.
    private readonly List<HighlightedRenderer> _highlighted = new();
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP Lit
    private static readonly int ColorId     = Shader.PropertyToID("_Color");     // legacy / custom
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private struct HighlightedRenderer
    {
        public Renderer  Renderer;
        public Material[] OriginalSharedMaterials;
        public Material   TintedInstance;
    }

    private void Awake() => CacheInitialState();

    public void ShowMode(GizmoMode mode)
    {
        if (_moveRoot   != null) _moveRoot.gameObject  .SetActive(mode == GizmoMode.Move);
        if (_rotateRoot != null) _rotateRoot.gameObject.SetActive(mode == GizmoMode.Rotate);
        if (_scaleRoot  != null) _scaleRoot.gameObject .SetActive(mode == GizmoMode.Scale);
    }

    public void OnHandleGrabbed(GizmoHandle handle)
    {
        if (handle == null) return;
        // Highlight снимается с рендереров ДО reparenting — иначе после SetAsParent в дочерние
        // войдут другие оси и они тоже подсветятся жёлтым.
        ApplyHighlight(handle);
        switch (handle.Kind)
        {
            case HandleKind.MoveAxis:
                SetAsParent(handle.transform, new[] { _moveCenter, _moveX, _moveY, _moveZ });
                break;
            case HandleKind.RotateRing:
                SetAsParent(handle.transform, new[] { _rotateX, _rotateY, _rotateZ });
                break;
            // ScaleAxis / ScaleUniform: no re-parent
        }
    }

    public void OnHandleReleased(GizmoMode currentMode)
    {
        ClearHighlight();
        ResetHierarchy();
        ShowMode(currentMode);
    }

    private void ApplyHighlight(GizmoHandle handle)
    {
        var renderers = handle.GetComponentsInChildren<Renderer>(includeInactive: false);
        foreach (var r in renderers)
        {
            if (r == null) continue;
            var originals = r.sharedMaterials;
            if (originals == null || originals.Length == 0 || originals[0] == null) continue;

            var tinted = new Material(originals[0]) { name = originals[0].name + " (Highlight)" };
            // Перебрать известные имена color-property и оверрайднуть те, что есть у шейдера.
            // Покрывает URP Lit (_BaseColor), legacy (_Color), и кастомные шейдеры с _Color.
            if (tinted.HasProperty(BaseColorId))     tinted.SetColor(BaseColorId, _highlightColor);
            if (tinted.HasProperty(ColorId))         tinted.SetColor(ColorId,     _highlightColor);
            if (tinted.HasProperty(EmissionColorId))
            {
                tinted.EnableKeyword("_EMISSION");
                tinted.SetColor(EmissionColorId, _highlightColor * 0.5f);
            }

            var newArr = new Material[originals.Length];
            newArr[0] = tinted;
            for (int i = 1; i < originals.Length; i++) newArr[i] = originals[i];
            r.sharedMaterials = newArr;

            _highlighted.Add(new HighlightedRenderer
            {
                Renderer = r,
                OriginalSharedMaterials = originals,
                TintedInstance = tinted,
            });
        }
    }

    private void ClearHighlight()
    {
        foreach (var h in _highlighted)
        {
            if (h.Renderer != null) h.Renderer.sharedMaterials = h.OriginalSharedMaterials;
            if (h.TintedInstance != null) Destroy(h.TintedInstance);
        }
        _highlighted.Clear();
    }

    public void ResetHierarchy()
    {
        foreach (var pair in _defaultStates)
        {
            var t = pair.Key;
            var s = pair.Value;
            if (t == null) continue;
            t.SetParent(s.Parent, false);
            t.localPosition = s.LocalPosition;
            t.localRotation = s.LocalRotation;
            t.localScale    = s.LocalScale;
        }
    }

    private static void SetAsParent(Transform newParent, Transform[] group)
    {
        foreach (var element in group)
        {
            if (element == null || element == newParent) continue;
            element.SetParent(newParent, worldPositionStays: true);
        }
    }

    private void CacheInitialState()
    {
        _defaultStates.Clear();
        Cache(_moveRoot); Cache(_moveCenter); Cache(_moveX); Cache(_moveY); Cache(_moveZ);
        Cache(_rotateRoot); Cache(_rotateX); Cache(_rotateY); Cache(_rotateZ);
        Cache(_scaleRoot); Cache(_scaleCenter); Cache(_scaleX); Cache(_scaleY); Cache(_scaleZ);
    }

    private void Cache(Transform t)
    {
        if (t == null || _defaultStates.ContainsKey(t)) return;
        _defaultStates.Add(t, new TransformState
        {
            Parent         = t.parent,
            LocalPosition  = t.localPosition,
            LocalRotation  = t.localRotation,
            LocalScale     = t.localScale,
        });
    }

    private class TransformState
    {
        public Transform  Parent;
        public Vector3    LocalPosition;
        public Quaternion LocalRotation;
        public Vector3    LocalScale;
    }
}
