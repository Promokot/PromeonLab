# UserPanel Y-drift Fix + FileBrowserVrAnchor Dynamic Tracking

**Date:** 2026-05-16
**Scope:** Two focused fixes in SpatialUi subsystem. Issue 1 (double UserPanel) handled separately.

---

## Fix 1 — UserPanel Y-drift + FaceCamera below camera

### Root cause

`UpdateSmartFollow()` sets `_activeTarget.y = transform.position.y` (current panel Y), then at snap adds `_yOffsetTarget = -0.4f`. Each re-center drops the panel 0.4 m further. After 3–4 re-centers the panel is on the floor.

### Changes — `Assets/_App/Subsystems/SpatialUi/UI_Scripts/UserPanel.cs`

**Remove field:**
```
[SerializeField] private float _yOffsetTarget = -0.4f;
```

**Both branches where `_activeTarget` is set** — change Y from `transform.position.y` to camera-relative:
```csharp
_activeTarget = new Vector3(targetXZ.x, _cameraTransform.position.y + _yOffset, targetXZ.z);
```

**Snap section** — remove `_yOffsetTarget` offset:
```csharp
// Before:
transform.position = _activeTarget.Value + new Vector3(0, _yOffsetTarget, 0);
// After:
transform.position = _activeTarget.Value;
```

Result: panel Y is always `camera.y + _yOffset` (-0.15 m), never drifts.

### Feature — FaceCamera slightly below camera Y

**Add field:**
```csharp
[SerializeField] private float _faceBelowOffset = 0.15f;
```

**In `LateUpdate()`** — replace `FaceCamera()` call with:
```csharp
FaceCameraBelow();
```

**Add private method** (does not touch `SpatialPanel.FaceCamera()`):
```csharp
private void FaceCameraBelow()
{
    var target = _cameraTransform.position + Vector3.down * _faceBelowOffset;
    var dir    = transform.position - target;
    if (dir.sqrMagnitude > 0.001f)
        transform.rotation = Quaternion.LookRotation(dir);
}
```

`_faceBelowOffset = 0.15f` makes the panel face a point 15 cm below camera eye level — tunable in Inspector.

---

## Fix 2 — FileBrowserVrAnchor: dynamic tracking + auto-close

### Root cause — wrong position ("at nose level")

`Start()` runs before UserPanel's first `LateUpdate()`. At that moment AssetBrowserModule is at its prefab default world position (near world origin / player spawn) — SFB canvas appears right in front of the user's face.

### Root cause — "stays in the middle of the map"

Position is captured once in `Start()`. UserPanel smart-follows the camera; SFB canvas stays at the old position.

### Changes — `Assets/_App/Subsystems/SpatialUi/UI_Scripts/FileBrowserVrAnchor.cs`

Full replacement:

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

        RepositionToTarget(); // best-effort initial placement; LateUpdate corrects each frame
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
        transform.position = t.position - t.forward * _forwardOffset; // 2 cm in front to avoid z-fight
        transform.rotation = t.rotation;
    }
}
```

**Behaviour:**
- Every `LateUpdate` the canvas snaps to AssetBrowserModule's current world position — follows UserPanel as it moves.
- If AssetBrowserModule becomes inactive (panel hidden) while SFB is open → `FileBrowser.HideDialog()` is called — dialog closes cleanly via SFB's own shutdown path.
- `_forwardOffset = 0.02f` keeps SFB canvas physically 2 cm closer to camera than the panel, ensuring it renders on top without sort-order changes.

### No changes to `AssetBrowserModule.cs`

Cleanup is self-contained in `FileBrowserVrAnchor`. SFB's own success/cancel callbacks handle the normal close path.

---

## Files touched

| File | Change |
|---|---|
| `Assets/_App/Subsystems/SpatialUi/UI_Scripts/UserPanel.cs` | Remove `_yOffsetTarget`, fix target Y, add `_faceBelowOffset` + `FaceCameraBelow()` |
| `Assets/_App/Subsystems/SpatialUi/UI_Scripts/FileBrowserVrAnchor.cs` | Full rewrite — dynamic tracking via LateUpdate, auto-close on panel hide |

## Out of scope

- Issue 1 (double UserPanel instance) — handled separately.
- SFB canvas sort order — `_forwardOffset` is sufficient for correct render order.
- AssetBrowserModule changes on mode transition — not needed; `FileBrowserVrAnchor` handles cleanup.
