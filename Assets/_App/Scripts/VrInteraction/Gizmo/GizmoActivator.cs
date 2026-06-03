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
    private GizmoDragSession      _drag;

    private bool       _panelOpen;
    private GizmoMode  _mode = GizmoMode.Move;
    private Transform  _target;
    private string     _targetNodeId;

    private GameObject     _instance;
    private GizmoHierarchy _hierarchy;

    [Inject]
    public void Construct(EventBus bus, SceneGraph graph, ISelectionManager selection, OutlineConfig outlineConfig)
    {
        _bus             = bus;
        _graph           = graph;
        _selection       = selection;
        _outlineConfig   = outlineConfig;
        _painter         = new GizmoHighlightPainter(_outlineConfig, _config);
        _drag            = new GizmoDragSession(_config, _bus, _painter);

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
        if (_target == null) { if (!_drag.IsActive) Despawn(); return; }
        // Во время drag гизмо — primary source-of-truth (strategy мутирует instance напрямую,
        // target подтягивается за ним в OnHandleDragged). Не переписываем instance из target.
        if (_drag.IsActive) return;
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
        if (_drag.IsActive) return;
        _panelOpen = false;
        RefreshVisibility();
    }

    private void OnModeChanged(GizmoModeChangedEvent e)
    {
        if (_drag.IsActive) return;
        _mode = e.Mode;
        if (_instance != null && _hierarchy != null) _hierarchy.ShowMode(_mode);
    }

    private void OnSelectionChanged(SelectionChangedEvent e)
    {
        if (_drag.IsActive) return;
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
        if (handle == null || _painter == null) return;
        if (_drag != null && _drag.IsGrabbing(handle)) return; // grab color wins
        if (hovering) _painter.Darken(handle);
        else          _painter.Restore(handle);
    }

    private void Despawn()
    {
        _drag?.Abort();
        if (_instance != null) Destroy(_instance);
        _instance  = null;
        _hierarchy = null;
        _painter.Clear();
    }

    public void OnHandleGrabbed(GizmoHandle handle, Vector3 handPos, Quaternion handRot)
    {
        if (_drag == null || _target == null || handle == null || _instance == null) return;
        _drag.Begin(_instance.transform, _target, _targetNodeId, _hierarchy, _mode, CurrentSize(),
                    handle, handPos, handRot);
    }

    public void OnHandleDragged(Vector3 handPos, Quaternion handRot)
    {
        _drag?.Update(handPos, handRot);
    }

    public void OnHandleReleased()
    {
        _drag?.End();
    }

    public void OnHandleAborted()
    {
        Debug.LogWarning($"[GizmoActivator] OnHandleAborted called, dragActive={_drag != null && _drag.IsActive}");
        _drag?.Abort();
    }
}
