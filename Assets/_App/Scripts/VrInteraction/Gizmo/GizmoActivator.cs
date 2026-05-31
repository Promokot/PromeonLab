using System.Collections.Generic;
using UnityEngine;
using VContainer;

public class GizmoActivator : MonoBehaviour
{
    // One per gizmo mesh renderer. The gizmo ships its own per-axis materials (now the emissive
    // Gizmo_Emissive* set) — including the scale center, whose single mesh carries 4 submeshes
    // (body + 3 axis legs). We keep those native materials (instanced so tinting never touches the
    // shared assets) and capture BOTH the base AND emission color of every submesh so hover/grab can
    // recolor and exactly restore them. The visible color of an emissive material is its emission, so
    // touching only _BaseColor (the old bug) left the highlight broken — we now handle both.
    private class GizmoPart
    {
        public Material[]   Materials;    // instanced native materials (one per submesh)
        public bool[]       HasBase;      // submesh has a _BaseColor/_Color slot
        public Color[]      BaseColor;    // captured base color per submesh
        public bool[]       HasEmis;      // submesh has an _EmissionColor slot
        public Color[]      EmisColor;    // captured emission color per submesh
        public Outline      Outline;
        public Color        OutlineBase;
        public GizmoHandle  Handle;       // null for centers (move-center has no handle)
    }

    private static readonly int BaseColorId     = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId         = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private const float HOVER_DARKEN = 0.75f;

    // The grabbed-handle look, read once from GizmoConfig.ActiveMaterial (Gizmo_EmissiveSelected) at spawn.
    private bool  _hasActiveBase, _hasActiveEmis;
    private Color _activeBase, _activeEmis;

    [SerializeField] private GizmoConfig _config;

    private EventBus          _bus;
    private SceneGraph        _graph;
    private ISelectionManager _selection;
    private GizmoController   _gizmoController;
    private OutlineConfig     _outlineConfig;

    private bool       _panelOpen;
    private GizmoMode  _mode = GizmoMode.Move;
    private Transform  _target;
    private string     _targetNodeId;

    private GameObject     _instance;
    private GizmoHierarchy _hierarchy;
    private GizmoHandle    _grabbedHandle;

    private readonly List<GizmoPart>                          _parts         = new();
    private readonly Dictionary<GizmoHandle, List<GizmoPart>> _partsByHandle = new();

    private bool                _dragActive;
    private IGizmoDragStrategy  _activeStrategy;
    private Vector3             _originalPos;
    private Quaternion          _originalRot;
    private Vector3             _originalScale;
    // Scale-inversion baselines: при scale-drag гизмо мутирует свою localScale, а target
    // получает пропорциональный фактор (instance.scale / instanceAtGrab → target = targetAtGrab * factor).
    private Vector3             _instanceScaleAtGrab;
    private Vector3             _targetScaleAtGrab;

    [Inject]
    public void Construct(EventBus bus, SceneGraph graph, ISelectionManager selection, GizmoController gizmoController, OutlineConfig outlineConfig)
    {
        _bus             = bus;
        _graph           = graph;
        _selection       = selection;
        _gizmoController = gizmoController;
        _outlineConfig   = outlineConfig;

        // Subscribe immediately. Doing this in OnEnable would race with LifetimeScope.Awake's
        // BuildCallback — if Activator's OnEnable ran first, _bus would be null and the bail-out
        // would silently skip all subscriptions, causing panel events to go unheard.
        _bus.Subscribe<GizmoToolsPanelOpenedEvent>(OnPanelOpened);
        _bus.Subscribe<GizmoToolsPanelClosedEvent>(OnPanelClosed);
        _bus.Subscribe<GizmoModeChangedEvent>(OnModeChanged);
        _bus.Subscribe<SelectionChangedEvent>(OnSelectionChanged);

        Debug.Log("[GizmoActivator] Construct() called — subscriptions registered.");
    }

    private void OnDestroy()
    {
        if (_bus == null) return;
        _bus.Unsubscribe<GizmoToolsPanelOpenedEvent>(OnPanelOpened);
        _bus.Unsubscribe<GizmoToolsPanelClosedEvent>(OnPanelClosed);
        _bus.Unsubscribe<GizmoModeChangedEvent>(OnModeChanged);
        _bus.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);
        if (_instance != null) Despawn();
    }

    private void Start()
    {
        var id = _selection?.SelectedNodeId;
        if (id != null) _target = _graph?.GetNode(id)?.transform;
        _targetNodeId = id;
    }

    private void LateUpdate()
    {
        if (_instance == null) return;
        if (_target == null) { if (!_dragActive) Despawn(); return; }
        // Во время drag гизмо — primary source-of-truth (strategy мутирует instance напрямую,
        // target подтягивается за ним в OnHandleDragged). Не переписываем instance из target.
        if (_dragActive) return;
        _instance.transform.position = _target.position;
        _instance.transform.rotation = _target.rotation;
    }

    private void OnPanelOpened(GizmoToolsPanelOpenedEvent _)
    {
        Debug.Log($"[GizmoActivator] OnPanelOpened received. _target={(_target != null ? _target.name : "null")}");
        _panelOpen = true;
        _mode      = GizmoMode.Move;
        RefreshVisibility();
    }

    private void OnPanelClosed(GizmoToolsPanelClosedEvent _)
    {
        if (_dragActive) return;
        _panelOpen = false;
        RefreshVisibility();
    }

    private void OnModeChanged(GizmoModeChangedEvent e)
    {
        if (_dragActive) return;
        _mode = e.Mode;
        if (_instance != null && _hierarchy != null) _hierarchy.ShowMode(_mode);
    }

    private void OnSelectionChanged(SelectionChangedEvent e)
    {
        if (_dragActive) return;
        _target       = (e.SelectedNodeId != null) ? _graph?.GetNode(e.SelectedNodeId)?.transform : null;
        _targetNodeId = e.SelectedNodeId;
        Debug.Log($"[GizmoActivator] OnSelectionChanged: id={e.SelectedNodeId}, target={(_target != null ? _target.name : "null")}");
        RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        bool shouldShow = _panelOpen && _target != null;
        Debug.Log($"[GizmoActivator] RefreshVisibility: panelOpen={_panelOpen}, target={(_target != null ? _target.name : "null")}, shouldShow={shouldShow}, instance={(_instance != null ? "exists" : "null")}");
        if (shouldShow && _instance == null)       Spawn();
        else if (!shouldShow && _instance != null) Despawn();
        else if (shouldShow && _instance != null)  { Despawn(); Spawn(); }
    }

    private void Spawn()
    {
        if (_config == null || _config.GizmoPrefab == null)
        {
            Debug.LogError("GizmoActivator: GizmoConfig missing or prefab null — gizmo disabled.");
            return;
        }
        _instance = Instantiate(_config.GizmoPrefab);
        _instance.transform.position = _target.position;
        _instance.transform.rotation = _target.rotation;

        // Bounds-fit frozen: spawn at one stable size from config (see _fixedSize).
        // var size = BoundsFitter.ComputeSize(_target, _config.BoundsCoefficient, _config.MinSize, _config.MaxSize);
        _instance.transform.localScale = Vector3.one * _config.FixedSize;

        _hierarchy = _instance.GetComponent<GizmoHierarchy>();
        if (_hierarchy != null) _hierarchy.ShowMode(_mode);

        // Spawned instance lives in scene root (not under this transform), so handles can't reach
        // us via GetComponentInParent. Bind the reference explicitly.
        foreach (var handle in _instance.GetComponentsInChildren<GizmoHandle>(includeInactive: true))
            handle.Bind(this);

        CacheActiveColors();
        BuildParts();
    }

    // Read the grabbed-handle look once from the configured active material (Gizmo_EmissiveSelected).
    private void CacheActiveColors()
    {
        _hasActiveBase = _hasActiveEmis = false;
        var fallback = _outlineConfig != null ? _outlineConfig.GizmoActiveColor : Color.cyan;
        var m = _config != null ? _config.ActiveMaterial : null;
        if (m != null)
        {
            if (m.HasProperty(BaseColorId))     { _hasActiveBase = true; _activeBase = m.GetColor(BaseColorId); }
            else if (m.HasProperty(ColorId))    { _hasActiveBase = true; _activeBase = m.GetColor(ColorId); }
            if (m.HasProperty(EmissionColorId)) { _hasActiveEmis = true; _activeEmis = m.GetColor(EmissionColorId); }
        }
        if (!_hasActiveBase) { _hasActiveBase = true; _activeBase = fallback; }
        if (!_hasActiveEmis) { _hasActiveEmis = true; _activeEmis = fallback; }
    }

    // One GizmoPart per mesh renderer. We keep the gizmo's native per-axis materials (instanced so
    // tinting never touches the shared assets) and add a SilhouetteOnly outline — see-through silhouette
    // behind occluders, like the rig bones, while the native solid mesh is the always-visible front
    // highlight (depth-tested, so overlapping handles never flicker as the OutlineAll mode did).
    // Multi-submesh renderers (the scale center: body + 3 axis legs) keep every submesh's own color.
    private void BuildParts()
    {
        _parts.Clear();
        _partsByHandle.Clear();

        foreach (var mr in _instance.GetComponentsInChildren<MeshRenderer>(includeInactive: true))
        {
            // includeInactive is REQUIRED: at spawn only the active-mode group is enabled (the others are
            // hidden by ShowMode). Without it, GetComponentInParent skips inactive GOs → handle resolves
            // null for the hidden Rotate/Scale groups → their outlines go white and never map into
            // _partsByHandle (so hover/grab never reaches them). Move worked only because it's active.
            var handle = mr.GetComponentInParent<GizmoHandle>(includeInactive: true);

            // Accessing .materials instantiates per-renderer copies of the native gizmo materials and
            // assigns them back — capture BEFORE installing the Outline (which appends mask/fill passes).
            var mats    = mr.materials;
            var hasBase = new bool[mats.Length];
            var baseCol = new Color[mats.Length];
            var hasEmis = new bool[mats.Length];
            var emisCol = new Color[mats.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null) continue;
                if (m.HasProperty(BaseColorId))     { hasBase[i] = true; baseCol[i] = m.GetColor(BaseColorId); }
                else if (m.HasProperty(ColorId))    { hasBase[i] = true; baseCol[i] = m.GetColor(ColorId); }
                if (m.HasProperty(EmissionColorId)) { hasEmis[i] = true; emisCol[i] = m.GetColor(EmissionColorId); }
            }

            var outlineColor = PartColor(handle);
            var outline      = InstallHandleOutline(mr.gameObject, outlineColor);

            var part = new GizmoPart
            {
                Materials   = mats,
                HasBase     = hasBase,
                BaseColor   = baseCol,
                HasEmis     = hasEmis,
                EmisColor   = emisCol,
                Outline     = outline,
                OutlineBase = outlineColor,
                Handle      = handle,
            };
            _parts.Add(part);
            if (handle != null)
            {
                if (!_partsByHandle.TryGetValue(handle, out var list))
                    _partsByHandle[handle] = list = new List<GizmoPart>();
                list.Add(part);
            }
        }
    }

    // Outline tint: per-axis handles get their axis color; centers/uniform-scale and handle-less meshes
    // get white. The mesh itself keeps its authored per-submesh colors; this only colors the silhouette.
    private Color PartColor(GizmoHandle handle)
    {
        if (handle == null) return Color.white;
        if (handle.Kind == HandleKind.ScaleUniform) return Color.white;
        return AxisColor(handle.Axis);
    }

    // SilhouetteOnly = see-through silhouette behind occluders only, like the rig bones.
    private Outline InstallHandleOutline(GameObject go, Color color)
    {
        var outline = go.GetComponent<Outline>();
        if (outline == null) outline = go.AddComponent<Outline>();
        if (_outlineConfig != null)
            outline.SetOutlineMaterials(_outlineConfig.MaskMaterial, _outlineConfig.FillMaterial);
        outline.OutlineMode    = Outline.Mode.SilhouetteOnly;
        outline.OutlineColor   = color;
        outline.OutlineWidth   = 3f;
        outline.RenderPriority = 2; // over selection (0) and bones (1)
        return outline;
    }

    private Color AxisColor(AxisKind axis)
    {
        if (_outlineConfig == null) return Color.white;
        switch (axis)
        {
            case AxisKind.X: return _outlineConfig.AxisColorX;
            case AxisKind.Y: return _outlineConfig.AxisColorY;
            case AxisKind.Z: return _outlineConfig.AxisColorZ;
            default:         return Color.white;
        }
    }

    private static void SetBase(Material m, Color c)
    {
        if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
        if (m.HasProperty(ColorId))     m.SetColor(ColorId, c);
    }

    private static void SetEmis(Material m, Color c)
    {
        m.EnableKeyword("_EMISSION");
        m.SetColor(EmissionColorId, c);
    }

    private static Color Scale(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, c.a);

    // Grab: every submesh of the handle adopts the active material's look (base + emission); outline too.
    private void RecolorHandle(GizmoHandle handle, Color outlineColor)
    {
        if (handle == null || !_partsByHandle.TryGetValue(handle, out var list)) return;
        foreach (var part in list)
        {
            for (int i = 0; i < part.Materials.Length; i++)
            {
                var m = part.Materials[i];
                if (m == null) continue;
                if (part.HasBase[i] && _hasActiveBase) SetBase(m, _activeBase);
                if (part.HasEmis[i] && _hasActiveEmis) SetEmis(m, _activeEmis);
            }
            if (part.Outline != null) part.Outline.OutlineColor = outlineColor;
        }
    }

    // Hover: darken each submesh toward its own captured base + emission (keeps per-axis distinction).
    private void DarkenHandle(GizmoHandle handle)
    {
        if (handle == null || !_partsByHandle.TryGetValue(handle, out var list)) return;
        foreach (var part in list)
        {
            for (int i = 0; i < part.Materials.Length; i++)
            {
                var m = part.Materials[i];
                if (m == null) continue;
                if (part.HasBase[i]) SetBase(m, Scale(part.BaseColor[i], HOVER_DARKEN));
                if (part.HasEmis[i]) SetEmis(m, Scale(part.EmisColor[i], HOVER_DARKEN));
            }
            if (part.Outline != null) part.Outline.OutlineColor = Scale(part.OutlineBase, HOVER_DARKEN);
        }
    }

    private void RestoreHandle(GizmoHandle handle)
    {
        if (handle == null || !_partsByHandle.TryGetValue(handle, out var list)) return;
        foreach (var part in list)
        {
            for (int i = 0; i < part.Materials.Length; i++)
            {
                var m = part.Materials[i];
                if (m == null) continue;
                if (part.HasBase[i]) SetBase(m, part.BaseColor[i]);
                if (part.HasEmis[i]) SetEmis(m, part.EmisColor[i]);
            }
            if (part.Outline != null) part.Outline.OutlineColor = part.OutlineBase;
        }
    }

    // Fired by GizmoHandle when its primary-hover state flips (skipped while that handle is grabbed).
    public void OnHandleHoverChanged(GizmoHandle handle, bool hovering)
    {
        if (handle == null) return;
        if (_dragActive && _grabbedHandle == handle) return; // grab color wins
        if (hovering) DarkenHandle(handle);
        else          RestoreHandle(handle);
    }

    private void Despawn()
    {
        if (_dragActive) AbortDrag();
        if (_instance != null) Destroy(_instance);
        _instance  = null;
        _hierarchy = null;
        _parts.Clear();
        _partsByHandle.Clear();
    }

    public void OnHandleGrabbed(GizmoHandle handle, Vector3 handPos, Quaternion handRot)
    {
        Debug.Log($"[GizmoActivator] OnHandleGrabbed: handle={(handle != null ? handle.name : "null")}, dragActive={_dragActive}, target={(_target != null ? _target.name : "null")}");
        if (_dragActive || _target == null || handle == null || _instance == null) return;
        _dragActive          = true;
        _originalPos         = _target.position;
        _originalRot         = _target.rotation;
        _originalScale       = _target.localScale;
        _instanceScaleAtGrab = _instance.transform.localScale;
        _targetScaleAtGrab   = _target.localScale;
        _activeStrategy      = ResolveStrategy(handle);
        _hierarchy?.OnHandleGrabbed(handle);

        // Highlight the grabbed handle (mesh material + outline) in the distinct active color;
        // restored in EndDragInternal. Uses the handle→parts map, so it lands on the renderer even
        // when it sits on a child of the handle GO (the scaler).
        _grabbedHandle = handle;
        RecolorHandle(handle, _outlineConfig != null ? _outlineConfig.GizmoActiveColor : Color.cyan);
        // Гизмо — primary: strategy мутирует _instance.transform во всех режимах.
        // Target подтягивается за гизмо в OnHandleDragged в зависимости от типа стратегии.
        _activeStrategy.BeginDrag(_instance.transform, handle.Axis, handPos, handRot);
        _bus?.Publish(new GizmoDragStartedEvent { TargetNodeId = _targetNodeId });
    }

    public void OnHandleDragged(Vector3 handPos, Quaternion handRot)
    {
        if (!_dragActive) return;
        if (_target == null || _instance == null) { OnHandleAborted(); return; }
        _activeStrategy?.UpdateDrag(handPos, handRot);
        // Target follows гизмо. Тип синка зависит от стратегии.
        switch (_activeStrategy)
        {
            case AxisMoveStrategy:
                _target.position = _instance.transform.position;
                break;
            case RingRotateStrategy:
                _target.rotation = _instance.transform.rotation;
                break;
            case AxisScaleStrategy:
            case UniformScaleStrategy:
                // Гизмо сама масштабируется визуально → target получает тот же фактор изменения,
                // применённый к собственной исходной шкале (не к гизмовой bounds-fit шкале).
                var inst = _instance.transform.localScale;
                var fX = SafeRatio(inst.x, _instanceScaleAtGrab.x);
                var fY = SafeRatio(inst.y, _instanceScaleAtGrab.y);
                var fZ = SafeRatio(inst.z, _instanceScaleAtGrab.z);
                _target.localScale = new Vector3(
                    _targetScaleAtGrab.x * fX,
                    _targetScaleAtGrab.y * fY,
                    _targetScaleAtGrab.z * fZ);
                break;
        }
    }

    private static float SafeRatio(float num, float den) => Mathf.Abs(den) < 1e-6f ? 1f : num / den;

    public void OnHandleReleased()
    {
        if (!_dragActive) return;
        _activeStrategy?.EndDrag();
        var currentMode = _mode;
        _hierarchy?.OnHandleReleased(currentMode);
        if (_target == null) { EndDragInternal(); return; }
        var finalPos   = _target.position;
        var finalRot   = _target.rotation;
        var finalScale = _target.localScale;
        // Restore to original so TransformCommand.ctor captures the correct _old snapshot.
        _target.position   = _originalPos;
        _target.rotation   = _originalRot;
        _target.localScale = _originalScale;
        _gizmoController?.CommitTransform(_target, finalPos, finalRot, finalScale);
        // Bounds-fit frozen: a scale drag mutated the instance scale, so reset it back to the fixed size.
        if (_instance != null && _config != null)
            _instance.transform.localScale = Vector3.one * _config.FixedSize;
        EndDragInternal();
    }

    public void OnHandleAborted()
    {
        Debug.LogWarning($"[GizmoActivator] OnHandleAborted called, dragActive={_dragActive}");
        if (!_dragActive) return;
        AbortDrag();
    }

    private void AbortDrag()
    {
        if (_target != null)
        {
            _target.position   = _originalPos;
            _target.rotation   = _originalRot;
            _target.localScale = _originalScale;
        }
        _activeStrategy?.EndDrag();
        if (_hierarchy != null) _hierarchy.OnHandleReleased(_mode);
        EndDragInternal();
    }

    private void EndDragInternal()
    {
        // Restore the grabbed handle (mesh material + outline) from the active color to its base.
        if (_grabbedHandle != null)
        {
            RestoreHandle(_grabbedHandle);
            _grabbedHandle = null;
        }

        var id = _targetNodeId;
        _activeStrategy = null;
        _dragActive     = false;
        _bus?.Publish(new GizmoDragEndedEvent { TargetNodeId = id });
    }

    private IGizmoDragStrategy ResolveStrategy(GizmoHandle handle)
    {
        switch (handle.Kind)
        {
            case HandleKind.MoveAxis:     return new AxisMoveStrategy();
            case HandleKind.ScaleAxis:    return new AxisScaleStrategy();
            case HandleKind.ScaleUniform: return new UniformScaleStrategy();
            case HandleKind.RotateRing:   return new RingRotateStrategy();
            default:                      return new AxisMoveStrategy();
        }
    }
}
