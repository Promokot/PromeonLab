using UnityEngine;
using VContainer;

public class GizmoActivator : MonoBehaviour
{
    [SerializeField] private GizmoConfig _config;

    private EventBus          _bus;
    private SceneGraph        _graph;
    private ISelectionManager _selection;
    private GizmoController   _gizmoController;

    private bool       _panelOpen;
    private GizmoMode  _mode = GizmoMode.Move;
    private Transform  _target;
    private string     _targetNodeId;

    private GameObject     _instance;
    private GizmoHierarchy _hierarchy;
    private Collider       _originalTargetCollider;

    private bool                _dragActive;
    private IGizmoDragStrategy  _activeStrategy;
    private Vector3             _originalPos;
    private Quaternion          _originalRot;
    private Vector3             _originalScale;

    [Inject]
    public void Construct(EventBus bus, SceneGraph graph, ISelectionManager selection, GizmoController gizmoController)
    {
        _bus             = bus;
        _graph           = graph;
        _selection       = selection;
        _gizmoController = gizmoController;

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
        if (_instance == null || _dragActive) return;
        if (_target == null) { Despawn(); return; }
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

        _originalTargetCollider = _target.GetComponent<Collider>();
        if (_originalTargetCollider != null) _originalTargetCollider.enabled = false;

        var size = BoundsFitter.ComputeSize(_target, _config.BoundsCoefficient, _config.MinSize, _config.MaxSize);
        _instance.transform.localScale = Vector3.one * size;

        _hierarchy = _instance.GetComponent<GizmoHierarchy>();
        if (_hierarchy != null) _hierarchy.ShowMode(_mode);

        // Spawned instance lives in scene root (not under this transform), so handles can't reach
        // us via GetComponentInParent. Bind the reference explicitly.
        foreach (var handle in _instance.GetComponentsInChildren<GizmoHandle>(includeInactive: true))
            handle.Bind(this);
    }

    private void Despawn()
    {
        if (_dragActive) AbortDrag();
        if (_originalTargetCollider != null) _originalTargetCollider.enabled = true;
        _originalTargetCollider = null;
        if (_instance != null) Destroy(_instance);
        _instance  = null;
        _hierarchy = null;
    }

    public void OnHandleGrabbed(GizmoHandle handle, Vector3 handPos, Quaternion handRot)
    {
        Debug.Log($"[GizmoActivator] OnHandleGrabbed: handle={(handle != null ? handle.name : "null")}, dragActive={_dragActive}, target={(_target != null ? _target.name : "null")}");
        if (_dragActive || _target == null || handle == null) return;
        _dragActive     = true;
        _originalPos    = _target.position;
        _originalRot    = _target.rotation;
        _originalScale  = _target.localScale;
        _activeStrategy = ResolveStrategy(handle);
        _hierarchy?.OnHandleGrabbed(handle);
        _activeStrategy.BeginDrag(_target, handle.Axis, handPos, handRot);
        _bus?.Publish(new GizmoDragStartedEvent { TargetNodeId = _targetNodeId });
    }

    public void OnHandleDragged(Vector3 handPos, Quaternion handRot)
    {
        if (!_dragActive) return;
        if (_target == null) { OnHandleAborted(); return; }
        _activeStrategy?.UpdateDrag(handPos, handRot);
    }

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
        // Refit after scale commits — bounds may have changed.
        if (_instance != null && _config != null)
        {
            var size = BoundsFitter.ComputeSize(_target, _config.BoundsCoefficient, _config.MinSize, _config.MaxSize);
            _instance.transform.localScale = Vector3.one * size;
        }
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
