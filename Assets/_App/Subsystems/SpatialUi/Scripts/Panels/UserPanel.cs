using System;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class UserPanel : SpatialPanel
{
    [Serializable]
    public struct NavBarBinding
    {
        public string        EntryId;
        public Button        NavButton;
        public MonoBehaviour Panel;    // SettingsModule, AssetBrowserModule, or DetachablePanel
    }

    [Header("Navigation")]
    [SerializeField] private Button _mainMenuButton;
    [SerializeField] private Button _exitButton;

    [Header("Nav Bar")]
    [SerializeField] private NavBarConfig    _navBarConfig;
    [SerializeField] private NavBarBinding[] _bindings;

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

    private Image[] _activeIndicators;

    private bool    _locked;
    private bool    _initialized;
    private bool    _isDragging;
    private Vector3  _followVelocity;
    private Vector3? _activeTarget;

    private static readonly Color ColorUnlocked  = new Color(0.62f, 1.00f, 0.77f, 0.90f);
    private static readonly Color ColorLocked    = new Color(1.00f, 0.42f, 0.42f, 0.90f);
    private static readonly Color IndicatorOn    = new Color(1.00f, 1.00f, 1.00f, 0.90f);
    private static readonly Color IndicatorOff   = new Color(1.00f, 1.00f, 1.00f, 0.20f);

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

        _activeIndicators = new Image[_bindings?.Length ?? 0];

        for (int i = 0; i < (_bindings?.Length ?? 0); i++)
        {
            var b       = _bindings[i];
            var entryId = b.EntryId;
            var idx     = i;

            if (b.Panel is DetachablePanel dp)
                dp.EntryId = entryId;

            _activeIndicators[idx] = b.NavButton?.GetComponentInChildren<Image>();
            SetActiveState(idx, false);

            b.NavButton?.onClick.AddListener(() => OnNavButtonClicked(entryId));
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
            var enabled = false;
            if (_navBarConfig != null && _navBarConfig.TryGetEntry(b.EntryId, out var entry))
                enabled = entry.StartsEnabled;

            b.NavButton.gameObject.SetActive(visible);
            b.NavButton.interactable = enabled;

            if (!visible)
                HidePanel(b, i);
        }
    }

    private void OnNavButtonClicked(string entryId)
    {
        var idx = FindBindingIndex(entryId);
        if (idx < 0) return;

        var b = _bindings[idx];

        if (b.Panel is DetachablePanel dp)
        {
            dp.ToggleLinked();
            SetActiveState(idx, dp.IsVisible && dp.IsLinked);
            return;
        }

        if (b.Panel is SettingsModule || b.Panel is AssetBrowserModule)
            HideAllModules(exceptEntryId: entryId);

        if (b.Panel is SettingsModule sm)
        {
            sm.Toggle();
            SetActiveState(idx, sm.IsVisible);
        }
        else if (b.Panel is AssetBrowserModule abm)
        {
            abm.Toggle();
            SetActiveState(idx, abm.IsVisible);
        }
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

    private void HideAllModules(string exceptEntryId = null)
    {
        for (int i = 0; i < (_bindings?.Length ?? 0); i++)
        {
            var b = _bindings[i];
            if (b.EntryId == exceptEntryId) continue;
            if (b.Panel is SettingsModule sm && sm.IsVisible)
            {
                sm.Hide();
                SetActiveState(i, false);
            }
            else if (b.Panel is AssetBrowserModule abm && abm.IsVisible)
            {
                abm.Hide();
                SetActiveState(i, false);
            }
        }
    }

    private void HidePanel(NavBarBinding b, int idx)
    {
        switch (b.Panel)
        {
            case DetachablePanel dp when dp.IsVisible: dp.Hide(); break;
            case SettingsModule sm   when sm.IsVisible: sm.Hide(); break;
            case AssetBrowserModule abm when abm.IsVisible: abm.Hide(); break;
        }
        SetActiveState(idx, false);
    }

    private int FindBindingIndex(string entryId)
    {
        for (int i = 0; i < (_bindings?.Length ?? 0); i++)
            if (_bindings[i].EntryId == entryId) return i;
        return -1;
    }

    private void SetActiveState(int bindingIndex, bool active)
    {
        if (_activeIndicators == null || bindingIndex >= _activeIndicators.Length) return;
        var img = _activeIndicators[bindingIndex];
        if (img != null)
            img.color = active ? IndicatorOn : IndicatorOff;
    }
}
