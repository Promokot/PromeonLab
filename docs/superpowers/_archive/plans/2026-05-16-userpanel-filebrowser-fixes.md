# UserPanel Y-drift Fix + FileBrowserVrAnchor Dynamic Tracking — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Important:** Do NOT run `git commit` — the user commits manually.

**Goal:** Fix UserPanel from drifting to the floor on each smart-follow re-center, add tilt-below-camera facing, and make the SimpleFileBrowser canvas track AssetBrowserModule in world space while auto-closing when the panel hides.

**Architecture:** Two files, three focused changes. No new types or dependencies introduced. MonoBehaviours only — no unit tests feasible for these components; verification is manual Play Mode. UserPanel receives two independent edits (Y-fix, then FaceCamera); FileBrowserVrAnchor is fully rewritten with a `LateUpdate` tracking loop replacing the one-shot `Start()`.

**Tech Stack:** Unity 6000.3.7f1, C#, SimpleFileBrowser plugin (`FileBrowser.IsOpen` / `FileBrowser.HideDialog()`), VContainer (no DI changes needed).

---

## File Map

| File | Action |
|---|---|
| `Assets/_App/Subsystems/SpatialUi/UI_Scripts/UserPanel.cs` | Modify — remove `_yOffsetTarget`, fix target Y in two branches, fix snap, add `_faceBelowOffset` + `FaceCameraBelow()` |
| `Assets/_App/Subsystems/SpatialUi/UI_Scripts/FileBrowserVrAnchor.cs` | Full rewrite — `LateUpdate` tracking loop, auto-close on panel hide |

---

## Task 1: Fix UserPanel Y-drift

**Files:**
- Modify: `Assets/_App/Subsystems/SpatialUi/UI_Scripts/UserPanel.cs`

**Root cause:** `_activeTarget.y` is set to `transform.position.y` (the panel's current Y, which already includes the previous offset). On snap, `_yOffsetTarget = -0.4f` is added on top. Each re-center = panel sinks 0.4 m. After 3–4 re-centers it's on the floor.

**Fix:** Set `_activeTarget.y` to `_cameraTransform.position.y + _yOffset` (camera-relative, always consistent). Remove the `_yOffsetTarget` snap offset entirely.

- [ ] **Open** `Assets/_App/Subsystems/SpatialUi/UI_Scripts/UserPanel.cs`.

- [ ] **Remove the `_yOffsetTarget` field.** Find and delete this line (it is inside the `[Header("Smart Follow")]` block):

```csharp
[SerializeField] private float _yOffsetTarget     = -0.4f;
```

- [ ] **Fix the "camera turned" branch in `UpdateSmartFollow()`.** Find:

```csharp
if (angle > _recenterAngle)
{
    // Camera turned: re-center in front
    var targetXZ = camXZ + yaw * _preferredDistance;
    _activeTarget = new Vector3(targetXZ.x, transform.position.y, targetXZ.z);
}
```

Replace with:

```csharp
if (angle > _recenterAngle)
{
    // Camera turned: re-center in front
    var targetXZ = camXZ + yaw * _preferredDistance;
    _activeTarget = new Vector3(targetXZ.x, _cameraTransform.position.y + _yOffset, targetXZ.z);
}
```

- [ ] **Fix the "too close / too far" branch.** Find:

```csharp
else if (xzDist < _minDistance || xzDist > _maxDistance)
{
    // Too close or too far: move to preferred distance along same direction
    var targetXZ = camXZ + delta.normalized * _preferredDistance;
    _activeTarget = new Vector3(targetXZ.x, transform.position.y, targetXZ.z);
}
```

Replace with:

```csharp
else if (xzDist < _minDistance || xzDist > _maxDistance)
{
    // Too close or too far: move to preferred distance along same direction
    var targetXZ = camXZ + delta.normalized * _preferredDistance;
    _activeTarget = new Vector3(targetXZ.x, _cameraTransform.position.y + _yOffset, targetXZ.z);
}
```

- [ ] **Fix the snap section.** Find:

```csharp
if (Vector3.Distance(transform.position, _activeTarget.Value) < 0.015f)
{
    transform.position = _activeTarget.Value + new Vector3(0, _yOffsetTarget, 0);
    _activeTarget       = null;
    _followVelocity     = Vector3.zero;
}
```

Replace with:

```csharp
if (Vector3.Distance(transform.position, _activeTarget.Value) < 0.015f)
{
    transform.position = _activeTarget.Value;
    _activeTarget       = null;
    _followVelocity     = Vector3.zero;
}
```

- [ ] **Wait for Unity to compile.** Console must show 0 errors. Unity may log one yellow warning about a missing serialized field (`_yOffsetTarget`) — this is harmless; click OK to clear it.

- [ ] **Verify in Play Mode:**
  1. Enter VrEditing or Sandbox
  2. Turn head / walk so UserPanel re-centers at least 3–4 times
  3. Expected: panel height stays consistent (camera Y − 0.15 m). It must NOT sink with each re-center.

---

## Task 2: Add FaceCameraBelow to UserPanel

**Files:**
- Modify: `Assets/_App/Subsystems/SpatialUi/UI_Scripts/UserPanel.cs`

**Why:** `SpatialPanel.FaceCamera()` aims at exact camera-eye Y. The user wants the panel to face a point ~15 cm below the eye so the panel tilts slightly toward the floor — more ergonomic for reading VR UI. `SpatialPanel.cs` is NOT touched; we add a private override only in `UserPanel`.

- [ ] **Add the `_faceBelowOffset` field.** In the `[Header("Smart Follow")]` block, find the last float field (now `_yOffset` after Task 1 removed `_yOffsetTarget`) and add `_faceBelowOffset` immediately after:

Find:
```csharp
[SerializeField] private float _yOffset           = -0.15f;
```

Replace with:
```csharp
[SerializeField] private float _yOffset           = -0.15f;
[SerializeField] private float _faceBelowOffset   = 0.15f;
```

- [ ] **Replace `FaceCamera()` with `FaceCameraBelow()` in `LateUpdate()`.** Find:

```csharp
protected override void LateUpdate()
{
    if (_cameraTransform == null) return;

    if (!_isDragging && !_locked)
        UpdateSmartFollow();

    FaceCamera();
}
```

Replace with:

```csharp
protected override void LateUpdate()
{
    if (_cameraTransform == null) return;

    if (!_isDragging && !_locked)
        UpdateSmartFollow();

    FaceCameraBelow();
}
```

- [ ] **Add the `FaceCameraBelow()` private method.** Place it directly after `GetCameraYawForward()`:

```csharp
private void FaceCameraBelow()
{
    var target = _cameraTransform.position + Vector3.down * _faceBelowOffset;
    var dir    = transform.position - target;
    if (dir.sqrMagnitude > 0.001f)
        transform.rotation = Quaternion.LookRotation(dir);
}
```

- [ ] **Wait for Unity to compile.** Console must show 0 errors.

- [ ] **Verify in Play Mode:**
  1. Open UserPanel (A / X button)
  2. The panel's top edge should be slightly closer to you than the bottom — a gentle forward tilt, not flat-on
  3. If tilt is too strong or too weak: exit Play Mode, open `UserPanel.prefab`, select the root object, adjust `_faceBelowOffset` in Inspector. Range: `0.05` (subtle) → `0.30` (pronounced). Default `0.15` is the starting point.

---

## Task 3: Rewrite FileBrowserVrAnchor — dynamic tracking + auto-close

**Files:**
- Modify: `Assets/_App/Subsystems/SpatialUi/UI_Scripts/FileBrowserVrAnchor.cs`

**Root cause — "canvas at nose level":** `Start()` captures `AssetBrowserModule` world position before UserPanel's first `LateUpdate()` executes. At that instant the panel is at its prefab default (near world origin → right in front of the player).

**Root cause — "canvas left behind on map":** Position set once in `Start()`; UserPanel smart-follows the camera and moves; canvas stays at stale world position.

**Fix:** Move positioning from `Start()` to `LateUpdate()` (runs every frame, tracks current position). `Start()` keeps one-time setup only: cache target, set scale, set `worldCamera`. Add auto-close: if target becomes inactive while SFB is open, call `FileBrowser.HideDialog()`.

- [ ] **Replace the entire contents of `Assets/_App/Subsystems/SpatialUi/UI_Scripts/FileBrowserVrAnchor.cs` with:**

```csharp
using SimpleFileBrowser;
using UnityEngine;

[RequireComponent(typeof(Canvas))]
public class FileBrowserVrAnchor : MonoBehaviour
{
    [SerializeField] private float _forwardOffset = 0.02f;
    [SerializeField] private float _scale         = 0.001f;

    private AssetBrowserModule _target;

    private void Start()
    {
        _target = Object.FindAnyObjectByType<AssetBrowserModule>(FindObjectsInactive.Include);

        transform.localScale = Vector3.one * _scale;

        var canvas  = GetComponent<Canvas>();
        var mainCam = Camera.main;
        if (canvas != null && mainCam != null)
            canvas.worldCamera = mainCam;

        RepositionToTarget();
    }

    private void LateUpdate()
    {
        if (_target == null || !_target.gameObject.activeInHierarchy)
        {
            if (FileBrowser.IsOpen)
                FileBrowser.HideDialog();
            return;
        }
        RepositionToTarget();
    }

    private void RepositionToTarget()
    {
        if (_target == null) return;
        var t = _target.transform;
        transform.position = t.position - t.forward * _forwardOffset;
        transform.rotation = t.rotation;
    }
}
```

**Why `t.forward` subtraction:** `AssetBrowserModule` inherits UserPanel's rotation. `FaceCamera` sets the panel's +Z to point away from the camera (`LookRotation(panel − camera)`). Subtracting `t.forward * 0.02f` moves the SFB canvas 2 cm toward the camera — physically in front of the panel, rendering on top without needing a higher sort order.

- [ ] **Wait for Unity to compile.** Console must show 0 errors.

- [ ] **Verify in Play Mode:**
  1. Enter VrEditing or Sandbox, open AssetBrowser
  2. Click **Add** — FileBrowser canvas should appear on top of AssetBrowserModule, not in front of the user's face
  3. Move head / walk so UserPanel re-centers — FileBrowser canvas must follow it, staying flush with AssetBrowserModule
  4. Hide AssetBrowser (Assets button again) — FileBrowser must close automatically; no zombie canvas should remain in scene
  5. If canvas is too large or too small: adjust `_scale` on `SimpleFileBrowserCanvas.prefab` → `FileBrowserVrAnchor` component. Working range: `0.0005`–`0.002`.
