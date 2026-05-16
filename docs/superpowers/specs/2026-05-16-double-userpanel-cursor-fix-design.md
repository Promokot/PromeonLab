# Double UserPanel Fix + Mouse Cursor Hide

**Date:** 2026-05-16
**Scope:** Two focused fixes. No new types, no new dependencies.

---

## Fix 1 — Remove UserPanel from DefaultPanelRegistry

### Root cause

`DefaultPanelRegistry.asset` contains one entry: `UserPanel.prefab`.

On every transition to VrEditing or Sandbox, `UiPanelManager.SpawnPanels()` calls
`_resolver.Instantiate(entry.Prefab)` — instantiating a **second** UserPanel at world origin.

Meanwhile the original UserPanel lives as a child of XR Rig in the Bootstrap scene and is
registered in `RootLifetimeScope` via `FindAnyObjectByType<UserPanel>`. The result: two
UserPanel GameObjects coexist.

### Fix

Edit `DefaultPanelRegistry.asset` — remove the UserPanel entry so `_panels` is empty.

```yaml
_panels: []
```

### Why safe

- `UserPanel` receives `_cameraTransform` through `SpatialPanel.Awake()` fallback:
  `_cameraTransform = Camera.main?.transform`. No `Init()` required.
- Visibility is controlled exclusively by `UserPanelOpener` (A/X button toggle).
  Mode-based hiding is not needed — panel is accessible in all modes.
- `RootLifetimeScope` will continue to find and inject the single Bootstrap instance.

### Files touched

| File | Change |
|---|---|
| `Assets/_App/Subsystems/SpatialUi/Data/DefaultPanelRegistry.asset` | Clear `_panels` list |

---

## Fix 2 — Hide mouse cursor in Play Mode and builds

### Change

`Assets/_App/Bootstrap/AppBootstrap.cs` — add two lines in `Start()` before scene load:

```csharp
private void Start()
{
    Cursor.visible   = false;
    Cursor.lockState = CursorLockMode.Locked;
    SceneManager.LoadScene("MainMenu", LoadSceneMode.Additive);
}
```

`CursorLockMode.Locked` centers and hides the OS cursor for the application window.
Applied once at app start; persists for the entire session.

On Meta Quest the cursor is never rendered regardless, so this only affects Play Mode in
the Unity Editor and PC builds — both desired.

### Files touched

| File | Change |
|---|---|
| `Assets/_App/Bootstrap/AppBootstrap.cs` | Add `Cursor.visible = false; Cursor.lockState = CursorLockMode.Locked;` |

---

## Out of scope

- `UiPanelManager` is not deleted — it still manages other panels added to the registry in future.
- No changes to `UserPanel.cs`, `UserPanelOpener.cs`, `RootLifetimeScope.cs`, or any scene files.
- Cursor state is not restored on app pause/focus-loss — unnecessary for a VR-only target.
