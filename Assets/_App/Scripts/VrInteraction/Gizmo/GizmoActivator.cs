using UnityEngine;
using VContainer;

public class GizmoActivator : MonoBehaviour
{
    [SerializeField] private GizmoConfig _config;

    private EventBus          _bus;
    private SceneGraph        _graph;
    private ISelectionManager _selection;
    private OutlineConfig     _outlineConfig;

    private GizmoHighlightPainter _painter;

    private bool       _panelOpen;
    private GizmoMode  _mode = GizmoMode.Move;
    private Transform  _target;
    private string     _targetNodeId;

    private GameObject     _instance;
    private GizmoHierarchy _hierarchy;
    private GizmoHandle    _grabbedHandle;

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
    public void Construct(EventBus bus, SceneGraph graph, ISelectionManager selection, OutlineConfig outlineConfig)
    {
        _bus             = bus;
        _graph           = graph;
        _selection       = selection;
        _outlineConfig   = outlineConfig;
        _painter         = new GizmoHighlightPainter(_outlineConfig, _config);

        // Subscribe immediately. Doing this in OnEnable would race with LifetimeScope.Awake's
        // BuildCallback — if Activator's OnEnable ran first, _bus would be null and the bail-out
        // would silently skip all subscriptions, causing panel events to go unheard.
        _bus.Subscribe<GizmoToolsPanelOpenedEvent>(OnPanelOpened);
        _bus.Subscribe<GizmoToolsPanelClosedEvent>(OnPanelClosed);
        _bus.Subscribe<GizmoModeChangedEvent>(OnModeChanged);
        _bus.Subscribe<SelectionChangedEvent>(OnSelectionChanged);
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
        RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        bool shouldShow = _panelOpen && _target != null;
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

        // Bounds-fit frozen: spawn at one stable size from config (see _fixedSize), halved on bones.
        // var size = BoundsFitter.ComputeSize(_target, _config.BoundsCoefficient, _config.MinSize, _config.MaxSize);
        _instance.transform.localScale = Vector3.one * CurrentSize();

        _hierarchy = _instance.GetComponent<GizmoHierarchy>();
        if (_hierarchy != null) _hierarchy.ShowMode(_mode);

        // Spawned instance lives in scene root (not under this transform), so handles can't reach
        // us via GetComponentInParent. Bind the reference explicitly.
        foreach (var handle in _instance.GetComponentsInChildren<GizmoHandle>(includeInactive: true))
            handle.Bind(this);

        _painter.Build(_instance);
    }

    // Stable gizmo size; halved when the target is a bone proxy (BoneSceneNodeMarker) so it doesn't
    // swamp the smaller bone geometry.
    private float CurrentSize()
    {
        float size = _config != null ? _config.FixedSize : 1f;
        if (_target != null && _target.GetComponent<BoneSceneNodeMarker>() != null) size *= 0.5f;
        return size;
    }

    // Fired by GizmoHandle when its primary-hover state flips (skipped while that handle is grabbed).
    public void OnHandleHoverChanged(GizmoHandle handle, bool hovering)
    {
        if (handle == null) return;
        if (_dragActive && _grabbedHandle == handle) return; // grab color wins
        if (hovering) _painter.Darken(handle);
        else          _painter.Restore(handle);
    }

    private void Despawn()
    {
        if (_dragActive) AbortDrag();
        if (_instance != null) Destroy(_instance);
        _instance  = null;
        _hierarchy = null;
        _painter.Clear();
    }

    public void OnHandleGrabbed(GizmoHandle handle, Vector3 handPos, Quaternion handRot)
    {
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
        _painter.Recolor(handle, _painter.GrabOutlineColor);
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
        // Transform is already live from the drag; undo recording was removed, so keep the final pose.
        // Bounds-fit frozen: a scale drag mutated the instance scale, so reset it back to the fixed size.
        if (_instance != null && _config != null)
            _instance.transform.localScale = Vector3.one * CurrentSize();
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
            _painter.Restore(_grabbedHandle);
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
            case HandleKind.ScaleAxis:    return new AxisScaleStrategy(_config.ScaleGain, _config.DeadzoneMeters);
            case HandleKind.ScaleUniform: return new UniformScaleStrategy(_config.ScaleGain, _config.DeadzoneMeters);
            case HandleKind.RotateRing:   return new RingRotateStrategy(_config.RotGain, _config.DeadzoneMeters);
            default:                      return new AxisMoveStrategy();
        }
    }
}
