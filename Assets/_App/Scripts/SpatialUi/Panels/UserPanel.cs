using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class UserPanel : SpatialPanel
{
    public enum LockMode { Follow, LockPosition, LockPositionRotation }

    [Header("Navigation")]
    [SerializeField] private Button _mainMenuButton;
    [SerializeField] private Button _exitButton;

    [Header("Lock")]
    [SerializeField] private Button _lockButton;
    [SerializeField] private Image  _lockButtonImage;

    [Header("Smart Follow")]
    [SerializeField] private float _recenterAngle     = 45f;
    [SerializeField] private float _smoothTime        = 0.5f;
    [SerializeField] private float _minDistance       = 0.25f;
    [SerializeField] private float _preferredDistance = 0.7f;
    [SerializeField] private float _maxDistance       = 1.25f;
    [SerializeField] private float _yOffset           = -0.15f;
    [Range(0f, 0.5f)]
    [SerializeField] private float _faceBelowOffset   = 0.15f;

    private ModeOrchestrator _orchestrator;

    private LockMode _lockMode = LockMode.Follow;
    private bool     _initialized;
    private bool     _isDragging;
    private Vector3  _followVelocity;
    private Vector3? _activeTarget;

    public LockMode CurrentLockMode => _lockMode;

    private static readonly Color ColorFollow       = new Color(0.62f, 1.00f, 0.77f, 0.90f); // green
    private static readonly Color ColorLockPosition = new Color(1.00f, 0.78f, 0.35f, 0.90f); // amber
    private static readonly Color ColorLockPosRot   = new Color(1.00f, 0.42f, 0.42f, 0.90f); // red

    [Inject]
    public void Construct(ModeOrchestrator orchestrator) => _orchestrator = orchestrator;

    private void Start()
    {
        _mainMenuButton?.onClick.AddListener(OnMainMenu);
        _exitButton?.onClick.AddListener(OnExit);
        _lockButton?.onClick.AddListener(CycleLockMode);
        ApplyLockVisual();
        DetachToWorld();
    }

    protected override void LateUpdate()
    {
        if (_cameraTransform == null) return;

        // Position: smart-follow only in Follow mode, and never while being grabbed
        // (the grab drives position directly — position-only).
        if (!_isDragging && _lockMode == LockMode.Follow)
            UpdateSmartFollow();

        // Rotation: auto-face the user in every mode except full lock. This also runs while
        // grabbing, so a position-only grab keeps the panel readable as it is repositioned.
        if (_lockMode != LockMode.LockPositionRotation)
            FaceCameraBelow();
    }

    private void UpdateSmartFollow()
    {
        if (!_initialized)
        {
            var fwd = GetCameraYawForward();
            transform.position = new Vector3(
                _cameraTransform.position.x + fwd.x * _preferredDistance,
                _cameraTransform.position.y + _yOffset,
                _cameraTransform.position.z + fwd.z * _preferredDistance);
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
                var targetXZ = camXZ + yaw * _preferredDistance;
                _activeTarget = new Vector3(targetXZ.x, _cameraTransform.position.y + _yOffset, targetXZ.z);
            }
            else if (xzDist < _minDistance || xzDist > _maxDistance)
            {
                var targetXZ = camXZ + delta.normalized * _preferredDistance;
                _activeTarget = new Vector3(targetXZ.x, _cameraTransform.position.y + _yOffset, targetXZ.z);
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
                _activeTarget      = null;
                _followVelocity    = Vector3.zero;
            }
        }
    }

    private Vector3 GetCameraYawForward()
    {
        var f = _cameraTransform.forward;
        f.y = 0f;
        return f.sqrMagnitude > 0.001f ? f.normalized : Vector3.forward;
    }

    private void FaceCameraBelow()
    {
        var target = _cameraTransform.position + Vector3.down * _faceBelowOffset;
        var dir    = transform.position - target;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir);
    }

    public void ResetPosition()
    {
        _initialized = false;
        _lockMode    = LockMode.Follow;
        _activeTarget   = null;
        _followVelocity = Vector3.zero;
        ApplyLockVisual();
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

    // Absolute world-space move used by the grip grab (position only).
    public void MoveTo(Vector3 worldPosition)
    {
        if (_isDragging)
            transform.position = worldPosition;
    }

    public void CycleLockMode()
    {
        _lockMode = (LockMode)(((int)_lockMode + 1) % 3);
        // Clear any in-flight follow target on every transition. It is only consumed in Follow
        // mode, so clearing unconditionally is a no-op for the lock modes and keeps the state
        // robust against future LateUpdate changes.
        _activeTarget   = null;
        _followVelocity = Vector3.zero;
        ApplyLockVisual();
    }

    private void ApplyLockVisual()
    {
        if (_lockButtonImage == null) return;
        _lockButtonImage.color = _lockMode switch
        {
            LockMode.Follow       => ColorFollow,
            LockMode.LockPosition => ColorLockPosition,
            _                     => ColorLockPosRot,
        };
    }

    private void DetachToWorld()
    {
        // The panel ships parented under the persistent XR Rig. Locking only the follow script
        // cannot stop the rig's transform from carrying the panel when the player locomotes, so
        // detach to a top-level persistent object. Smart-follow is script-driven against the
        // world-space camera, so it keeps working with no parent; both lock modes then hold
        // world position. Start runs on the panel's first activation (it ships inactive), which
        // is after UserPanelOpener.Awake has cached its reference and after RootLifetimeScope
        // registered the instance — so detaching here breaks no existing reference holder.
        transform.SetParent(null, worldPositionStays: true);
        DontDestroyOnLoad(gameObject);
    }

    private void OnMainMenu() => _orchestrator?.TransitionTo(AppMode.MainMenu);
    private void OnExit()     => Application.Quit();
}
