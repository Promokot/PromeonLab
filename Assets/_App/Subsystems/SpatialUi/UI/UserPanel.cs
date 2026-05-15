using System;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class UserPanel : SpatialPanel
{
    [Serializable]
    public struct ContextMenuEntry
    {
        public AppMode    Mode;
        public GameObject Prefab;
    }

    [Header("Navigation")]
    [SerializeField] private Button _mainMenuButton;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private Button _exitButton;

    [Header("Modules")]
    [SerializeField] private SettingsModule _settingsModule;
    [SerializeField] private Transform      _contextSlot;

    [Header("Context Menus")]
    [SerializeField] private ContextMenuEntry[] _contextMenus;

    [Header("Lock")]
    [SerializeField] private Button _lockButton;
    [SerializeField] private Image  _lockButtonImage;

    [Header("Smart Follow")]
    [SerializeField] private float _recenterAngle     = 45f;
    [SerializeField] private float _smoothTime        = 0.5f;
    [SerializeField] private float _minDistance       = 0.35f;
    [SerializeField] private float _preferredDistance = 0.8f;
    [SerializeField] private float _maxDistance       = 1.35f;

    private ModeOrchestrator _orchestrator;
    private EventBus         _bus;
    private GameObject       _currentContext;

    private bool    _locked;
    private bool    _initialized;
    private bool    _isDragging;

    private Vector3  _followVelocity;
    private Vector3? _activeTarget;

    private static readonly Color ColorUnlocked = new Color(0.30f, 0.30f, 0.35f, 0.90f);
    private static readonly Color ColorLocked   = new Color(0.80f, 0.50f, 0.10f, 0.95f);

    [Inject]
    public void Construct(ModeOrchestrator orchestrator, EventBus bus)
    {
        _orchestrator = orchestrator;
        _bus          = bus;
    }

    private void Start()
    {
        _mainMenuButton.onClick.AddListener(OnMainMenu);
        _settingsButton.onClick.AddListener(OnSettings);
        _exitButton.onClick.AddListener(OnExit);
        _lockButton?.onClick.AddListener(OnLockToggle);
        _bus?.Subscribe<ModeChangedEvent>(OnModeChanged);
    }

    private void OnDestroy() =>
        _bus?.Unsubscribe<ModeChangedEvent>(OnModeChanged);

    protected override void LateUpdate()
    {
        if (_cameraTransform == null) return;

        if (!_isDragging && !_locked)
            UpdateSmartFollow();

        FaceCamera();
    }

    private void UpdateSmartFollow()
    {
        if (!_initialized)
        {
            transform.position = _cameraTransform.position + GetCameraYawForward() * _preferredDistance;
            _initialized    = true;
            _followVelocity = Vector3.zero;
            return;
        }

        var camXZ   = new Vector3(_cameraTransform.position.x, 0f, _cameraTransform.position.z);
        var panelXZ = new Vector3(transform.position.x,        0f, transform.position.z);
        var delta   = panelXZ - camXZ;
        var xzDist  = delta.magnitude;

        if (xzDist > 0.001f)
        {
            var yaw   = GetCameraYawForward();
            var angle = Vector3.Angle(yaw, delta.normalized);

            if (angle > _recenterAngle)
            {
                // Camera turned: re-center in front
                var targetXZ = camXZ + yaw * _preferredDistance;
                _activeTarget = new Vector3(targetXZ.x, transform.position.y, targetXZ.z);
            }
            else if (xzDist < _minDistance || xzDist > _maxDistance)
            {
                // Too close or too far: move to preferred distance along same direction
                var targetXZ = camXZ + delta.normalized * _preferredDistance;
                _activeTarget = new Vector3(targetXZ.x, transform.position.y, targetXZ.z);
            }
        }

        if (_activeTarget.HasValue)
        {
            transform.position = Vector3.SmoothDamp(
                transform.position, _activeTarget.Value,
                ref _followVelocity, _smoothTime);

            if (Vector3.Distance(transform.position, _activeTarget.Value) < 0.015f)
            {
                transform.position = _activeTarget.Value;
                _activeTarget       = null;
                _followVelocity     = Vector3.zero;
            }
        }
    }

    private Vector3 GetCameraYawForward()
    {
        var f = _cameraTransform.forward;
        f.y = 0f;
        return f.sqrMagnitude > 0.001f ? f.normalized : Vector3.forward;
    }

    public void SetDragging(bool active)
    {
        _isDragging = active;
        if (!active)
        {
            _activeTarget   = null;
            _followVelocity = Vector3.zero;
        }
    }

    public void MoveDelta(Vector3 delta)
    {
        if (_isDragging)
            transform.position += delta;
    }

    private void OnLockToggle()
    {
        _locked = !_locked;
        if (_lockButtonImage != null)
            _lockButtonImage.color = _locked ? ColorLocked : ColorUnlocked;
    }

    private void OnMainMenu() => _orchestrator?.TransitionTo(AppMode.MainMenu);
    private void OnSettings() => _settingsModule?.Toggle();
    private void OnExit()     => Application.Quit();

    private void OnModeChanged(ModeChangedEvent e) => SwapContext(e.CurrentMode);

    private void SwapContext(AppMode mode)
    {
        if (_currentContext != null)
        {
            Destroy(_currentContext);
            _currentContext = null;
        }

        if (_contextSlot == null) return;

        foreach (var entry in _contextMenus)
        {
            if (entry.Mode == mode && entry.Prefab != null)
            {
                _currentContext = Instantiate(entry.Prefab, _contextSlot);
                _currentContext.transform.localPosition = Vector3.zero;
                _currentContext.transform.localRotation = Quaternion.identity;
                break;
            }
        }
    }
}
