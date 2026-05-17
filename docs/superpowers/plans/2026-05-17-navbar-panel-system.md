# NavBar Panel System — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the broken ContextSlot/SwapContext system in UserPanel with a NavBarConfig-driven nav bar that shows mode-specific buttons, and add a DetachablePanel component for panels that can be unpinned into free-floating world windows.

**Architecture:** `NavBarConfig` ScriptableObject declares which buttons are visible per `AppMode`. `UserPanel` replaces hardcoded Settings/Assets buttons with a `NavBarBinding[]` array that maps entry IDs to nav buttons and panel MonoBehaviours. `DetachablePanel` manages Linked↔Unlinked↔Closed state for panels like Outliner and Inspector.

**Tech Stack:** Unity 6000.3.7f1, C# (no namespaces), VContainer DI, MessagePipe EventBus, UnityEngine.UI, TextMeshPro

---

## File Map

| File | Action | Responsibility |
|---|---|---|
| `Assets/_App/_Shared/Events/AppEvents.cs` | Modify | Add PanelDetachedEvent, PanelLinkedEvent, PanelClosedEvent |
| `Assets/_App/Subsystems/SpatialUi/Data/NavBarConfig.cs` | Create | SO declaring entry IDs, visible modes, enabled flag |
| `Assets/_App/Subsystems/SpatialUi/UI_Scripts/SettingsModule.cs` | Modify | Add `IsVisible` property |
| `Assets/_App/Subsystems/SpatialUi/UI_Scripts/AssetBrowserModule.cs` | Modify | Add `IsVisible` property |
| `Assets/_App/Subsystems/SpatialUi/UI_Scripts/DetachablePanelDragHandle.cs` | Create | Drag-to-move for floating DetachablePanel windows |
| `Assets/_App/Subsystems/SpatialUi/DetachablePanel.cs` | Create | Linked/Unlinked/Closed state machine for detachable panels |
| `Assets/_App/Subsystems/SpatialUi/UI_Scripts/UserPanel.cs` | Modify | Remove ContextSlot/SwapContext; add NavBarBinding[] system |
| `Assets/_App/Subsystems/SpatialUi/UI_Scripts/UserPanel_ContextMenu_VrEditing.cs` | Delete | Empty stub, replaced |
| `Assets/_App/Subsystems/SpatialUi/UI_Scripts/UserPanel_ContextMenu_Sandbox.cs` | Delete | Empty stub, replaced |
| `Assets/_App/Subsystems/SpatialUi/UI_Scripts/UserPanel_ContextMenu_ArMapping.cs` | Delete | Empty stub, replaced |

---

## Task 1: Panel Events

**Files:**
- Modify: `Assets/_App/_Shared/Events/AppEvents.cs`

These three events are published by `DetachablePanel` and consumed by `UserPanel` to update nav button active states.

- [ ] **Step 1: Add three event structs to AppEvents.cs**

Open `Assets/_App/_Shared/Events/AppEvents.cs`. Append at the end of the file:

```csharp
public struct PanelDetachedEvent { public string EntryId; }
public struct PanelLinkedEvent   { public string EntryId; }
public struct PanelClosedEvent   { public string EntryId; }
```

The file should look like this in full after the edit:

```csharp
public struct SceneOpenedEvent       { public string SceneId; }
public struct SceneModifiedEvent     { }
public struct SceneClosedEvent       { }
public struct AssetImportedEvent     { public string AssetId; }
public struct SelectionChangedEvent  { public string SelectedNodeId; public string[] SelectedNodeIds; }
public struct ModeChangedEvent       { public AppMode PreviousMode; public AppMode CurrentMode; }
public struct FrameChangedEvent      { public int Frame; }
public struct PlaybackStateChangedEvent { public bool IsPlaying; public int Frame; }
public struct ErrorOccurredEvent     { public ErrorLevel Level; public string Message; }
public struct SceneSelectedEvent          { public string SceneId; public string DisplayName; }
public struct PlayerSpawnRequestedEvent   { public UnityEngine.Vector3 Position; public UnityEngine.Quaternion Rotation; }
public struct AssetSpawnRequestedEvent    { public ILabAsset Asset; public UnityEngine.Vector3 Position; public UnityEngine.Quaternion Rotation; }
public struct KeyboardFocusEvent          { public TMPro.TMP_InputField Target; }
public struct PanelDetachedEvent { public string EntryId; }
public struct PanelLinkedEvent   { public string EntryId; }
public struct PanelClosedEvent   { public string EntryId; }
```

- [ ] **Step 2: Verify compilation**

In Unity Editor, wait for the compilation spinner to finish. Check the Console window (`Window > General > Console`) — no errors expected.

---

## Task 2: NavBarConfig ScriptableObject

**Files:**
- Create: `Assets/_App/Subsystems/SpatialUi/Data/NavBarConfig.cs`

This SO declares all nav bar entries. One asset instance will be created in the Inspector and wired into UserPanel.

- [ ] **Step 1: Create NavBarConfig.cs**

Create new file `Assets/_App/Subsystems/SpatialUi/Data/NavBarConfig.cs`:

```csharp
using System;
using UnityEngine;

[CreateAssetMenu(menuName = "VrAnimApp/NavBarConfig")]
public class NavBarConfig : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public string    Id;
        public AppMode[] VisibleModes;
        public bool      StartsEnabled;
    }

    public Entry[] Entries;

    public bool TryGetEntry(string id, out Entry entry)
    {
        if (Entries != null)
            foreach (var e in Entries)
                if (e.Id == id) { entry = e; return true; }
        entry = default;
        return false;
    }

    public bool IsVisibleInMode(string id, AppMode mode)
    {
        if (!TryGetEntry(id, out var e)) return false;
        if (e.VisibleModes == null) return false;
        foreach (var m in e.VisibleModes)
            if (m == mode) return true;
        return false;
    }
}
```

- [ ] **Step 2: Verify compilation**

Check Console — no errors. `NavBarConfig` type should now resolve.

- [ ] **Step 3: Create NavBarConfig.asset (Manual — Unity Inspector)**

In the Unity Project window, navigate to `Assets/_App/Subsystems/SpatialUi/Data/`.
Right-click → `Create > VrAnimApp > NavBarConfig`. Name it `NavBarConfig`.

Configure `Entries` array with 7 elements:

| Index | Id | VisibleModes | StartsEnabled |
|---|---|---|---|
| 0 | `settings` | MainMenu, VrEditing, Sandbox, ArMapping | ✓ |
| 1 | `assets` | MainMenu, VrEditing, Sandbox | ✓ |
| 2 | `outliner` | VrEditing, Sandbox | ✓ |
| 3 | `inspector` | VrEditing, Sandbox | ✓ |
| 4 | `timeline` | VrEditing | ✓ |
| 5 | `rigging` | VrEditing, Sandbox | ✗ |
| 6 | `gizmo` | VrEditing, Sandbox | ✗ |

- [ ] **Step 4: Commit**

```
git add Assets/_App/Subsystems/SpatialUi/Data/NavBarConfig.cs
git add Assets/_App/Subsystems/SpatialUi/Data/NavBarConfig.asset
git add Assets/_App/Subsystems/SpatialUi/Data/NavBarConfig.asset.meta
git commit -m "feat: add NavBarConfig SO for mode-aware nav bar entries"
```

---

## Task 3: Add IsVisible to Modules

**Files:**
- Modify: `Assets/_App/Subsystems/SpatialUi/UI_Scripts/SettingsModule.cs`
- Modify: `Assets/_App/Subsystems/SpatialUi/UI_Scripts/AssetBrowserModule.cs`

`UserPanel` needs to read module visibility to update active indicators and enforce mutual exclusion.

- [ ] **Step 1: Add IsVisible to SettingsModule**

In `SettingsModule.cs`, add the property immediately after the `_anim` field declaration (line 13, before `private void Awake()`):

```csharp
    public bool IsVisible => _visible;
```

- [ ] **Step 2: Add IsVisible to AssetBrowserModule**

In `AssetBrowserModule.cs`, add the property after `_isEditableMode` field (before `private void Awake()`):

```csharp
    public bool IsVisible => _visible;
```

- [ ] **Step 3: Verify compilation**

Check Console — no errors.

- [ ] **Step 4: Commit**

```
git add Assets/_App/Subsystems/SpatialUi/UI_Scripts/SettingsModule.cs
git add Assets/_App/Subsystems/SpatialUi/UI_Scripts/AssetBrowserModule.cs
git commit -m "feat: expose IsVisible on SettingsModule and AssetBrowserModule"
```

---

## Task 4: DetachablePanelDragHandle

**Files:**
- Create: `Assets/_App/Subsystems/SpatialUi/UI_Scripts/DetachablePanelDragHandle.cs`

The existing `PanelDragHandle` is typed to `UserPanel`. This new component is the drag handle for unlinked `DetachablePanel` windows. Mirrors the same drag math.

- [ ] **Step 1: Create DetachablePanelDragHandle.cs**

```csharp
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class DetachablePanelDragHandle : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private DetachablePanel _panel;
    [SerializeField] private Color _normalColor = new Color(1f,    1f,    1f,    0.25f);
    [SerializeField] private Color _hoverColor  = new Color(0.80f, 0.80f, 0.85f, 0.45f);
    [SerializeField] private Color _dragColor   = new Color(0.45f, 0.50f, 0.55f, 0.70f);

    private const float MaxFrameDelta = 0.4f;

    private Image _image;
    private bool  _isDragging;

    private void Awake()
    {
        _image       = GetComponent<Image>();
        _image.color = _normalColor;
    }

    public void OnPointerEnter(PointerEventData e) { if (!_isDragging) _image.color = _hoverColor; }
    public void OnPointerExit(PointerEventData e)  { if (!_isDragging) _image.color = _normalColor; }

    public void OnBeginDrag(PointerEventData e)
    {
        _isDragging  = true;
        _image.color = _dragColor;
    }

    public void OnDrag(PointerEventData e)
    {
        if (e.delta.sqrMagnitude < 0.01f || _panel == null) return;

        var cam = e.enterEventCamera != null ? e.enterEventCamera : Camera.main;
        if (cam == null) return;

        var screenZ = cam.WorldToScreenPoint(_panel.transform.position).z;
        if (screenZ <= 0.01f) return;

        var prev      = e.position - e.delta;
        var worldPrev = cam.ScreenToWorldPoint(new Vector3(prev.x,       prev.y,       screenZ));
        var worldCurr = cam.ScreenToWorldPoint(new Vector3(e.position.x, e.position.y, screenZ));
        var delta     = worldCurr - worldPrev;

        if (delta.magnitude > MaxFrameDelta) return;
        _panel.MoveDelta(delta);
    }

    public void OnEndDrag(PointerEventData e)
    {
        _isDragging  = false;
        _image.color = _normalColor;
    }
}
```

- [ ] **Step 2: Verify compilation**

Check Console — no errors. `DetachablePanelDragHandle` references `DetachablePanel` which doesn't exist yet; compilation will fail until Task 5 is complete. If the compiler errors only reference missing `DetachablePanel` type, proceed to Task 5 immediately.

---

## Task 5: DetachablePanel

**Files:**
- Create: `Assets/_App/Subsystems/SpatialUi/DetachablePanel.cs`

State machine: Linked+Hidden → Linked+Visible ↔ Unlinked+Visible → Destroyed. Panels are pre-placed inside the UserPanel prefab hierarchy; VContainer auto-injects them.

- [ ] **Step 1: Create DetachablePanel.cs**

```csharp
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class DetachablePanel : MonoBehaviour
{
    [SerializeField] private Button                  _linkButton;
    [SerializeField] private Button                  _lockButton;
    [SerializeField] private Button                  _closeButton;
    [SerializeField] private DetachablePanelDragHandle _dragHandle;

    private EventBus _bus;
    private bool     _locked;
    private bool     _closedPublished;

    public bool   IsLinked  { get; private set; } = true;
    public bool   IsVisible { get; private set; }
    public string EntryId   { get; set; }

    [Inject]
    public void Construct(EventBus bus) => _bus = bus;

    private void Awake()
    {
        if (_linkButton  != null) _linkButton.onClick.AddListener(OnLinkButtonClicked);
        if (_lockButton  != null) _lockButton.onClick.AddListener(OnLockClicked);
        if (_closeButton != null) _closeButton.onClick.AddListener(OnCloseClicked);

        // Only lock/close/drag are available when unlinked — hide them by default.
        SetUnlinkedControlsVisible(false);
        if (_dragHandle != null) _dragHandle.enabled = false;

        gameObject.SetActive(false);
    }

    // Called by UserPanel nav button to open/close while linked.
    public void ToggleLinked()
    {
        if (IsVisible) Hide();
        else Show();
    }

    public void Show()
    {
        IsVisible = true;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (!IsVisible) return;
        IsVisible = false;
        if (IsLinked)
            gameObject.SetActive(false);
        else
            DestroyUnlinked();
    }

    // Detach from UserPanel: reparent to scene root, enable drag.
    public void Unlink()
    {
        if (!IsLinked) return;
        IsLinked = false;
        transform.SetParent(null, worldPositionStays: true);
        SetUnlinkedControlsVisible(true);
        if (_dragHandle != null) _dragHandle.enabled = true;
        _bus?.Publish(new PanelDetachedEvent { EntryId = EntryId });
    }

    // Re-link: destroy self; UserPanel will re-show its linked placeholder.
    public void LinkBack()
    {
        _bus?.Publish(new PanelLinkedEvent { EntryId = EntryId });
        Destroy(gameObject);
    }

    public void MoveDelta(Vector3 delta)
    {
        if (!_locked)
            transform.position += delta;
    }

    private void OnLinkButtonClicked()
    {
        if (IsLinked) Unlink();
        else LinkBack();
    }

    private void OnLockClicked() => _locked = !_locked;

    private void OnCloseClicked()
    {
        _closedPublished = true;
        _bus?.Publish(new PanelClosedEvent { EntryId = EntryId });
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        // If unlinked and Close() was not explicitly called, publish closed event.
        if (!IsLinked && !_closedPublished)
            _bus?.Publish(new PanelClosedEvent { EntryId = EntryId });
    }

    private void DestroyUnlinked()
    {
        _closedPublished = true;
        _bus?.Publish(new PanelClosedEvent { EntryId = EntryId });
        Destroy(gameObject);
    }

    private void SetUnlinkedControlsVisible(bool visible)
    {
        if (_lockButton  != null) _lockButton.gameObject.SetActive(visible);
        if (_closeButton != null) _closeButton.gameObject.SetActive(visible);
    }
}
```

- [ ] **Step 2: Verify compilation**

Check Console. Both `DetachablePanel` and `DetachablePanelDragHandle` should now compile cleanly together.

- [ ] **Step 3: Commit**

```
git add Assets/_App/Subsystems/SpatialUi/DetachablePanel.cs
git add Assets/_App/Subsystems/SpatialUi/UI_Scripts/DetachablePanelDragHandle.cs
git commit -m "feat: add DetachablePanel and DetachablePanelDragHandle components"
```

---

## Task 6: Refactor UserPanel

**Files:**
- Modify: `Assets/_App/Subsystems/SpatialUi/UI_Scripts/UserPanel.cs`

Replace the ContextSlot/SwapContext/hardcoded-buttons system with NavBarBinding[]. All SmartFollow, lock, drag, FaceCameraBelow logic remains unchanged.

**Removals:**
- `ContextMenuEntry` struct
- `_mainMenuButton` (kept — still needed for back navigation)
- `_settingsButton`, `_assetsButton` (replaced by _bindings)
- `_settingsModule`, `_assetBrowserModule` (now in _bindings)
- `_contextSlot`, `_contextMenus`, `_currentContext`
- `SwapContext()`, `OnSettings()`, `OnAssetsToggle()`, `ToggleAssetsModule()`
- `IObjectResolver _resolver` (no longer needed)

**Additions:**
- `NavBarBinding` struct
- `_navBarConfig`, `_bindings`, `_activeIndicators[]`
- `ApplyMode()`, `OnNavButtonClicked()`, `SetActiveState()`, `HideAllModules()`
- Subscribe/unsubscribe `PanelDetachedEvent`, `PanelClosedEvent`

- [ ] **Step 1: Replace UserPanel.cs with the new version**

Full new content of `Assets/_App/Subsystems/SpatialUi/UI_Scripts/UserPanel.cs`:

```csharp
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

    private static readonly Color ColorUnlocked  = new Color(0.30f, 0.30f, 0.35f, 0.90f);
    private static readonly Color ColorLocked    = new Color(0.80f, 0.50f, 0.10f, 0.95f);
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

        // Module panels are mutually exclusive — hide all other modules first.
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
```

- [ ] **Step 2: Verify compilation**

Check Console. Expect errors only if prefab references to removed fields are stale (runtime warnings, not compile errors). Compilation itself should succeed.

- [ ] **Step 3: Commit**

```
git add Assets/_App/Subsystems/SpatialUi/UI_Scripts/UserPanel.cs
git commit -m "feat: replace ContextSlot/SwapContext with NavBarBinding[] in UserPanel"
```

---

## Task 7: Delete Obsolete ContextMenu Files

**Files:**
- Delete: `Assets/_App/Subsystems/SpatialUi/UI_Scripts/UserPanel_ContextMenu_VrEditing.cs`
- Delete: `Assets/_App/Subsystems/SpatialUi/UI_Scripts/UserPanel_ContextMenu_Sandbox.cs`
- Delete: `Assets/_App/Subsystems/SpatialUi/UI_Scripts/UserPanel_ContextMenu_ArMapping.cs`

- [ ] **Step 1: Delete the three files**

In Unity's Project window, select and delete each file (Delete key or right-click → Delete). Unity will also delete the corresponding `.meta` files.

Alternatively via git:

```bash
git rm "Assets/_App/Subsystems/SpatialUi/UI_Scripts/UserPanel_ContextMenu_VrEditing.cs"
git rm "Assets/_App/Subsystems/SpatialUi/UI_Scripts/UserPanel_ContextMenu_VrEditing.cs.meta"
git rm "Assets/_App/Subsystems/SpatialUi/UI_Scripts/UserPanel_ContextMenu_Sandbox.cs"
git rm "Assets/_App/Subsystems/SpatialUi/UI_Scripts/UserPanel_ContextMenu_Sandbox.cs.meta"
git rm "Assets/_App/Subsystems/SpatialUi/UI_Scripts/UserPanel_ContextMenu_ArMapping.cs"
git rm "Assets/_App/Subsystems/SpatialUi/UI_Scripts/UserPanel_ContextMenu_ArMapping.cs.meta"
```

- [ ] **Step 2: Verify compilation**

Check Console — no errors. These were empty stubs with no references.

- [ ] **Step 3: Commit**

```
git commit -m "chore: delete obsolete UserPanel_ContextMenu_* stub files"
```

---

## Task 8: Inspector Wiring (Manual — Unity Editor)

**Files:** UserPanel prefab, Outliner prefab, Inspector prefab

This task has no code changes — it wires the new NavBarBinding[] system in the Inspector. All steps are in Unity Editor.

### 8a. Wire UserPanel prefab

- [ ] **Step 1: Open UserPanel prefab**

In Project window, find `Assets/_App/Subsystems/SpatialUi/` (or `UI/`) — locate `UserPanel.prefab`. Open it in Prefab Mode (double-click).

- [ ] **Step 2: Set NavBarConfig reference**

Select the UserPanel root GameObject. In Inspector, find the `UserPanel` component.
- `Nav Bar Config` → drag in `Assets/_App/Subsystems/SpatialUi/Data/NavBarConfig.asset`

- [ ] **Step 3: Wire _bindings array**

Set the `Bindings` array size to match the number of nav buttons you have in the prefab hierarchy.

For each binding, set:
- `Entry Id` = string ID from NavBarConfig (e.g., `"settings"`, `"assets"`, `"outliner"`, `"inspector"`, `"timeline"`)
- `Nav Button` = the Button component in the horizontal group for that function
- `Panel` = the corresponding MonoBehaviour:
  - `"settings"` → SettingsModule component (pre-placed child)
  - `"assets"` → AssetBrowserModule component (pre-placed child)
  - `"outliner"` → DetachablePanel component on the Outliner child GameObject
  - `"inspector"` → DetachablePanel component on the Inspector child GameObject
  - `"timeline"` → set to `None` for now (stub button, disabled by NavBarConfig)
  - `"rigging"` → set to `None` (disabled stub)
  - `"gizmo"` → set to `None` (disabled stub)

- [ ] **Step 4: Wire Navigation buttons**

- `Main Menu Button` → the Main Menu back-button in the nav area
- `Exit Button` → the Exit/Quit button
- `Lock Button` → the lock/unlock button
- `Lock Button Image` → the Image component on the lock button

- [ ] **Step 5: Verify active indicators**

Each `NavButton` must have at least one `Image` component in its children (for the active indicator). The code uses `GetComponentInChildren<Image>()` to find the first one. If the button's own image is the only one, add a child `Image` GameObject named `ActiveIndicator` with `Raycast Target` off. Set its color to transparent white initially.

### 8b. Add DetachablePanel prefab children

If Outliner and Inspector are not yet child GameObjects of UserPanel, add them:

- [ ] **Step 6: Add Outliner as child**

In UserPanel prefab hierarchy, create a child GameObject named `OutlinerPanel`. Add components:
1. `Canvas` (same settings as UserPanel canvas — World Space, layer UI)
2. `DetachablePanel` component
3. Wire `SceneOutlinerView` somewhere in the hierarchy:
   - Add child `Content` → add `SceneOutlinerView` component
   - Wire `_rowsRoot` → Content's ScrollView content Transform
   - Wire `_rowPrefab` → `OutlinerObject_ItemUI` prefab (drag from Project window)
4. Add `_linkButton` (Button) — always visible, unlink/link icon
5. Add `_lockButton` (Button) — hidden by default
6. Add `_closeButton` (Button) — hidden by default
7. Add `DetachablePanelDragHandle` on a drag strip Image child — wire `_panel` → DetachablePanel

- [ ] **Step 7: Add Inspector as child**

Same as Step 6 but for `InspectorPanel`:
1. Add `DetachablePanel` component
2. Add `SceneInspectorView` component in a child, wire all 13 `[SerializeField]` fields:
   - `_emptyState` → empty-state GameObject
   - `_content` → content GameObject
   - `_nameField` → TMP_InputField
   - `_typeLabel`, `_posX/Y/Z`, `_rotX/Y/Z`, `_scaleX/Y/Z` → TMP_Text components
3. Wire link/lock/close buttons and drag handle on DetachablePanel

- [ ] **Step 8: Save prefab and enter Play Mode**

Save the prefab (Ctrl+S in Prefab Mode). Exit prefab mode. Enter Play Mode.

**Verify:**
- In MainMenu mode: only Settings and Assets buttons are visible
- Switching to VrEditing mode: Outliner, Inspector, Timeline buttons appear
- Clicking Settings: Settings module slides up, active indicator turns bright; clicking again hides it
- Clicking Assets: Assets module slides up (Settings hides if open)
- Clicking Outliner: OutlinerPanel appears; clicking Unlink button detaches it to world space; drag moves it; Lock button locks position; Close button destroys it
- Clicking Outliner again after detaching: a new linked Outliner opens (multiple instances allowed)

---

## Self-Review Checklist

- [x] **Spec coverage:** All spec requirements covered: NavBarConfig SO ✓, NavBarBinding[] ✓, DetachablePanel state machine ✓, three events ✓, module mutual exclusion ✓, OnDestroy cleanup ✓, disabled stubs ✓, delete obsolete files ✓
- [x] **No placeholders:** All code steps include complete implementations
- [x] **Type consistency:** `NavBarBinding.Panel` is `MonoBehaviour` throughout; `DetachablePanel.EntryId` is `string` set in UserPanel.Start(); `IsVisible`/`IsLinked` are `bool` properties used consistently
- [x] **Mutual exclusion:** Task 6 OnNavButtonClicked calls `HideAllModules()` before toggling a Module panel; DetachablePanels do not participate in mutual exclusion

---

## Notes for the Implementer

**VContainer injection of pre-placed panel children:** `SettingsModule` and `AssetBrowserModule` already have `[Inject]` and work as pre-placed hierarchy children. `DetachablePanel` with `[Inject] public void Construct(EventBus bus)` will be auto-injected the same way since UserPanel's root is injected by VContainer, cascading to all children.

**`_activeIndicators` array:** `GetComponentInChildren<Image>()` on a Button returns the Button's own background image. If you want a separate "glow" indicator, add a dedicated child Image named `ActiveIndicator` to each NavButton in the prefab. The code will find whichever Image is first in depth-first order.

**Disabled stub buttons** (`rigging`, `gizmo`): `NavBarConfig.StartsEnabled = false` makes them non-interactable. Wire their `Panel` as `None` — `HidePanel()` does a null check before accessing the panel.

**Timeline button:** Visible in VrEditing per the config, enabled = true, but `Panel = None` (no implementation yet). Clicking it calls `OnNavButtonClicked` → `FindBindingIndex` → `b.Panel` is null → no switch case matches → nothing happens. This is correct stub behavior.
