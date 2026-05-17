# NavBar Panel System — Design Spec

**Date:** 2026-05-17

---

## Goal

Replace the broken `ContextSlot` / `SwapContext` system in `UserPanel` with a declarative, SO-driven nav bar that shows mode-specific buttons, and introduce a `DetachablePanel` component for panels that can be unpinned from the UserPanel into free-floating world-space windows.

---

## Context & Motivation

Current state:
- `UserPanel._contextMenus` instantiates/destroys a prefab per `AppMode` — the VrEditing context prefab is an empty stub, so the Outliner never appears.
- Nav buttons (Settings, Assets) are hardcoded fields; no active-state feedback.
- No way to make panels independent of the UserPanel.

---

## Architecture

### Three Panel Interaction Models (unchanged contracts)

| Type | Behaviour | Examples |
|---|---|---|
| **Module** | Slide in/out from UserPanel body | Settings, AssetBrowser |
| **Overlay** | Replace UserPanel content area | VR Keyboard |
| **Detachable** | Linked inside UserPanel OR unlinked as free-floating world panel | Outliner, Inspector |

---

## New: NavBarConfig ScriptableObject

**File:** `Assets/_App/Subsystems/SpatialUi/Data/NavBarConfig.cs`

```csharp
[CreateAssetMenu(menuName = "VrAnimApp/NavBarConfig")]
public class NavBarConfig : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public string    Id;
        public AppMode[] VisibleModes;
        public bool      StartsEnabled; // false = disabled placeholder (future tools)
    }
    public Entry[] Entries;
}
```

One asset lives at `Assets/_App/Subsystems/SpatialUi/Data/NavBarConfig.asset`.

**Entry definitions:**

| Id | VisibleModes | StartsEnabled |
|---|---|---|
| `settings` | MainMenu, VrEditing, Sandbox, ArMapping | true |
| `assets` | MainMenu, VrEditing, Sandbox | true |
| `outliner` | VrEditing, Sandbox | true |
| `inspector` | VrEditing, Sandbox | true |
| `timeline` | VrEditing | true |
| `rigging` | VrEditing, Sandbox | false |
| `gizmo` | VrEditing, Sandbox | false |

---

## Changes to UserPanel

**File:** `Assets/_App/Subsystems/SpatialUi/UI_Scripts/UserPanel.cs`

### Remove
- `ContextMenuEntry` struct
- `_contextMenus`, `_contextSlot` fields
- `SwapContext()` method
- `_currentContext` field + Destroy/Instantiate logic

### Add

```csharp
[Serializable]
public struct NavBarBinding
{
    public string        EntryId;   // matches NavBarConfig.Entry.Id
    public Button        NavButton; // button in HLG
    public MonoBehaviour Panel;     // Module or DetachablePanel to open/close
    [HideInInspector]
    public Image         ActiveIndicator; // child Image on NavButton for active state
}

[Header("Nav Bar")]
[SerializeField] private NavBarConfig    _navBarConfig;
[SerializeField] private NavBarBinding[] _bindings;
```

### OnModeChanged logic

```
foreach binding in _bindings:
    entry = _navBarConfig.Find(binding.EntryId)
    visible = entry.VisibleModes.Contains(currentMode)
    binding.NavButton.gameObject.SetActive(visible)
    binding.NavButton.interactable = entry.StartsEnabled
    if (!visible && panel is open):
        panel.Hide()
        SetActiveState(binding, false)
```

### Active-state

Each `NavButton` has a child `Image` (ActiveIndicator). `UserPanel` calls `SetActiveState(binding, bool)` which sets `alpha` / `color` of that Image. Called when a panel opens or closes — Modules via their `Toggle()`, DetachablePanels via events.

### Button wiring

`NavButton.onClick` → `UserPanel.OnNavButtonClicked(entryId)`:
- Finds the binding
- Calls `Panel.Toggle()` (Modules) or `DetachablePanel.ToggleLinked()` (Detachable)
- Updates active indicator

---

## New: DetachablePanel Component

**File:** `Assets/_App/Subsystems/SpatialUi/DetachablePanel.cs`

Sits on the root GameObject of any detachable panel prefab (Outliner, Inspector, …).

```csharp
public class DetachablePanel : MonoBehaviour
{
    [SerializeField] private Button         _linkButton;    // always visible
    [SerializeField] private Button         _lockButton;    // visible only when unlinked
    [SerializeField] private Button         _closeButton;   // visible only when unlinked
    [SerializeField] private PanelDragHandle _dragHandle;   // enabled only when unlinked
    [SerializeField] private GameObject     _linkedRoot;    // slot inside UserPanel hierarchy

    public bool IsLinked  { get; private set; } = true;
    public bool IsVisible { get; private set; } = false;
    public string EntryId { get; set; }  // set by UserPanel after instantiation

    // Called by UserPanel nav button
    public void ToggleLinked() { if (IsVisible) Hide(); else Show(); }
    public void Show();
    public void Hide();

    // Unlink: reparent to scene root, keep world pose, enable drag
    public void Unlink();

    // Link: Destroy self, UserPanel will re-show the linked slot
    public void LinkBack();
}
```

### State transitions

```
[Linked + Hidden]  ──Show()──►  [Linked + Visible]
[Linked + Visible] ──Hide()──►  [Linked + Hidden]
[Linked + Visible] ──Unlink()─► [Unlinked + Visible]  publishes PanelDetachedEvent
[Unlinked]         ──LinkBack()► Destroy self          publishes PanelLinkedEvent
[Unlinked]         ──Close()───► Destroy self          publishes PanelClosedEvent
```

### Unlink sequence

1. Save `worldPosition`, `worldRotation`
2. `transform.SetParent(null, worldPositionStays: true)`
3. Show `_lockButton`, `_closeButton`
4. Enable `_dragHandle`
5. Change `_linkButton` icon to "link" state
6. Publish `PanelDetachedEvent { EntryId }`
7. UserPanel receives event → hides its linked slot placeholder, resets active indicator to "off" (panel is now independent)

### Multiple instances

Nav button always creates a new linked instance when no linked instance is open. Unlinked instances are independent — they do not block creating a new linked one. Multiple unlinked windows of the same type can coexist.

### Lifecycle

`DetachablePanel` lives inside the scene-scope lifetime. On mode change the VContainer scope disposes → all instances (linked and unlinked) are destroyed automatically. `OnDestroy` publishes `PanelClosedEvent` to reset nav bar active state.

---

## New Events

**File:** `Assets/_App/_Shared/Events/`

```csharp
public struct PanelDetachedEvent { public string EntryId; }
public struct PanelLinkedEvent   { public string EntryId; }
public struct PanelClosedEvent   { public string EntryId; }
```

---

## Files to Delete

- `UserPanel_ContextMenu_VrEditing.cs`
- `UserPanel_ContextMenu_Sandbox.cs`
- `UserPanel_ContextMenu_ArMapping.cs`
- Corresponding prefabs in `Assets/_App/Subsystems/SpatialUi/UI/` (если были созданы)

---

## Files Changed / Created

| File | Action |
|---|---|
| `SpatialUi/Data/NavBarConfig.cs` | Create |
| `SpatialUi/Data/NavBarConfig.asset` | Create (Inspector) |
| `SpatialUi/DetachablePanel.cs` | Create |
| `SpatialUi/UI_Scripts/UserPanel.cs` | Modify |
| `_Shared/Events/PanelDetachedEvent.cs` | Create |
| `_Shared/Events/PanelLinkedEvent.cs` | Create |
| `_Shared/Events/PanelClosedEvent.cs` | Create |
| `UI_Scripts/UserPanel_ContextMenu_*.cs` (×3) | Delete |

---

## Out of Scope (separate tasks)

- Bug: Outliner not appearing — resolved by new system wiring in Inspector
- Bug: Object selection not working — SelectionInteractor / WorldClickCatcher setup
- Bug: Scene save/load — SceneAutoSaver timing
- Timeline, Rigging Tools, Gizmo Tools implementation (buttons present as disabled stubs)
