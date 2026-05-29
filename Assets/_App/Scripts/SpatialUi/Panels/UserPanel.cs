using System;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class UserPanel : SpatialPanel
{
    [Serializable]
    public struct NavBarBinding
    {
        public string     EntryId;
        public Button     NavButton;
        public GameObject Panel;
    }

    [Header("Navigation")]
    [SerializeField] private Button _mainMenuButton;
    [SerializeField] private Button _exitButton;

    [Header("Nav Bar")]
    [SerializeField] private NavBarConfig    _navBarConfig;
    [SerializeField] private NavBarBinding[] _bindings;

    [Header("Nav Bar Button Brightness")]
    [SerializeField] [Range(0f, 2f)] private float _inactiveHoverBrightness = 1.2f;
    [SerializeField] [Range(0f, 2f)] private float _activeBrightness        = 0.6f;
    [SerializeField] [Range(0f, 2f)] private float _activeHoverBrightness   = 0.8f;

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
    private EventBus         _bus;

    private ColorBlock[] _inactiveColors;
    private ColorBlock[] _activeColors;

    private bool     _locked;
    private bool     _initialized;
    private bool     _isDragging;
    private Vector3  _followVelocity;
    private Vector3? _activeTarget;

    private static readonly Color ColorUnlocked = new Color(0.62f, 1.00f, 0.77f, 0.90f);
    private static readonly Color ColorLocked   = new Color(1.00f, 0.42f, 0.42f, 0.90f);

    [Inject]
    public void Construct(ModeOrchestrator orchestrator, EventBus bus)
    {
        _orchestrator = orchestrator;
        _bus          = bus;
    }

    private void Start()
    {
        _mainMenuButton?.onClick.AddListener(OnMainMenu);
        _exitButton?.onClick.AddListener(OnExit);
        _lockButton?.onClick.AddListener(OnLockToggle);

        int count = _bindings?.Length ?? 0;
        _inactiveColors = new ColorBlock[count];
        _activeColors   = new ColorBlock[count];

        for (int i = 0; i < count; i++)
        {
            var b = _bindings[i];

            if (b.Panel != null && b.Panel.TryGetComponent<SpatialPanelDetachable>(out var dp))
                dp.EntryId = b.EntryId;

            if (b.NavButton != null)
            {
                var baseColor = b.NavButton.colors.normalColor;
                var block     = b.NavButton.colors;

                var inactive              = block;
                inactive.normalColor      = baseColor;
                inactive.highlightedColor = Brighten(baseColor, _inactiveHoverBrightness);
                inactive.selectedColor    = baseColor;
                _inactiveColors[i]        = inactive;

                var active              = block;
                active.normalColor      = Brighten(baseColor, _activeBrightness);
                active.highlightedColor = Brighten(baseColor, _activeHoverBrightness);
                active.selectedColor    = Brighten(baseColor, _activeBrightness);
                _activeColors[i]        = active;

                b.NavButton.colors = inactive;

                var idx = i;
                b.NavButton.onClick.AddListener(() => OnNavButtonClicked(idx));
            }
        }

        _bus?.Subscribe<ModeChangedEvent>(OnModeChanged);
        _bus?.Subscribe<PanelDetachedEvent>(OnPanelDetached);
        _bus?.Subscribe<PanelClosedEvent>(OnPanelClosed);

        if (_orchestrator != null)
            ApplyMode(_orchestrator.CurrentMode);
    }

    private void OnDestroy()
    {
        _bus?.Unsubscribe<ModeChangedEvent>(OnModeChanged);
        _bus?.Unsubscribe<PanelDetachedEvent>(OnPanelDetached);
        _bus?.Unsubscribe<PanelClosedEvent>(OnPanelClosed);
    }

    protected override void LateUpdate()
    {
        if (_cameraTransform == null) return;
        if (!_isDragging && !_locked)
            UpdateSmartFollow();
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
                transform.position  = _activeTarget.Value;
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
        _locked      = false;
        if (_lockButtonImage != null)
            _lockButtonImage.color = ColorUnlocked;
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
    private void OnExit()     => Application.Quit();

    private void OnModeChanged(ModeChangedEvent e) => ApplyMode(e.CurrentMode);

    private void ApplyMode(AppMode mode)
    {
        for (int i = 0; i < (_bindings?.Length ?? 0); i++)
        {
            var b = _bindings[i];
            if (b.NavButton == null) continue;

            var visible = _navBarConfig != null && _navBarConfig.IsVisibleInMode(b.EntryId, mode);
            b.NavButton.gameObject.SetActive(visible);

            if (!visible && b.Panel != null && b.Panel.activeSelf)
            {
                b.Panel.SetActive(false);
                SetActiveState(i, false);
            }
        }
    }

    private void OnNavButtonClicked(int idx)
    {
        var b = _bindings[idx];
        if (b.Panel == null) return;

        var willShow = !b.Panel.activeSelf;
        if (willShow)
        {
            var group = GetGroup(idx);
            if (!string.IsNullOrEmpty(group))
                HidePanelsInGroup(group, exceptIdx: idx);
        }
        b.Panel.SetActive(willShow);
        SetActiveState(idx, willShow);
    }

    private void OnPanelDetached(PanelDetachedEvent e)
    {
        var idx = FindBindingIndex(e.EntryId);
        if (idx >= 0)
            SetActiveState(idx, false);
    }

    private void OnPanelClosed(PanelClosedEvent e)
    {
        var idx = FindBindingIndex(e.EntryId);
        if (idx >= 0)
            SetActiveState(idx, false);
    }

    private string GetGroup(int idx)
    {
        if (_navBarConfig == null) return null;
        return _navBarConfig.TryGetEntry(_bindings[idx].EntryId, out var e) ? e.ExclusiveGroup : null;
    }

    private void HidePanelsInGroup(string group, int exceptIdx = -1)
    {
        for (int i = 0; i < (_bindings?.Length ?? 0); i++)
        {
            if (i == exceptIdx) continue;
            if (GetGroup(i) != group) continue;
            var p = _bindings[i].Panel;
            if (p != null && p.activeSelf)
            {
                p.SetActive(false);
                SetActiveState(i, false);
            }
        }
    }

    private int FindBindingIndex(string entryId)
    {
        for (int i = 0; i < (_bindings?.Length ?? 0); i++)
            if (_bindings[i].EntryId == entryId) return i;
        return -1;
    }

    private void SetActiveState(int idx, bool active)
    {
        if (_inactiveColors == null || idx >= _inactiveColors.Length) return;
        var btn = _bindings[idx].NavButton;
        if (btn == null) return;
        btn.colors = active ? _activeColors[idx] : _inactiveColors[idx];
    }

    private static Color Brighten(Color c, float mult)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        var vNew   = mult >= 1f
            ? Mathf.Clamp01(v + (mult - 1f) * (1f - v + 0.05f))
            : v * mult;
        var result = Color.HSVToRGB(h, s, vNew);
        result.a   = c.a;
        return result;
    }
}
