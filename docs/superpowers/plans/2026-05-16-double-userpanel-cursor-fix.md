# Double UserPanel Fix + Mouse Cursor Hide — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Important:** Do NOT run `git commit` — the user commits manually.

**Goal:** Eliminate the duplicate UserPanel that appears on every mode transition and hide the mouse cursor in Play Mode and builds.

**Architecture:** Two independent one-file changes. Task 1 edits a YAML asset file (no code). Task 2 adds two lines to `AppBootstrap.Start()`. No new types or dependencies. No unit tests are feasible for these Unity lifecycle components; verification is manual Play Mode.

**Tech Stack:** Unity 6000.3.7f1, C#, Unity SceneManager, UnityEngine.Cursor.

---

## File Map

| File | Action |
|---|---|
| `Assets/_App/Subsystems/SpatialUi/Data/DefaultPanelRegistry.asset` | Modify — clear `_panels` list |
| `Assets/_App/Bootstrap/AppBootstrap.cs` | Modify — add Cursor hide before scene load |

---

## Task 1: Remove UserPanel from DefaultPanelRegistry

**Files:**
- Modify: `Assets/_App/Subsystems/SpatialUi/Data/DefaultPanelRegistry.asset`

**Root cause:** `UiPanelManager.SpawnPanels()` reads `PanelRegistry._panels` and calls
`_resolver.Instantiate(entry.Prefab)` for every entry. The registry currently contains one
entry — `UserPanel.prefab`. This spawns a second UserPanel each time VrEditing or Sandbox
loads, on top of the original that lives as a child of XR Rig in the Bootstrap scene.

- [ ] **Open** `Assets/_App/Subsystems/SpatialUi/Data/DefaultPanelRegistry.asset` in a text
  editor. The relevant section currently reads:

```yaml
  _panels:
  - Id: 6
    Prefab: {fileID: 8573857923733603883, guid: 7a4de75d919ab50449b093180517b28c, type: 3}
    VisibleInModes: 000000000100000004000000020000000300000005000000
```

- [ ] **Replace** the `_panels` block with an empty list:

```yaml
  _panels: []
```

  The full file after the edit should look like this (everything else unchanged):

```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: cf920f1c0606a6c4ca8cb6082d5abf0f, type: 3}
  m_Name: DefaultPanelRegistry
  m_EditorClassIdentifier: Subsystems.SpatialUi::PanelRegistry
  _panels: []
```

- [ ] **Let Unity reimport the asset.** No compilation step needed (it's data, not code).
  Console should show no errors.

- [ ] **Verify in Play Mode:**
  1. Enter Play Mode, transition to VrEditing or Sandbox.
  2. Open the Hierarchy window. Search for "UserPanel".
  3. Expected: **exactly one** UserPanel object in the hierarchy (the one parented under
     XR Rig / Player in the Bootstrap scene).
  4. Previously: two UserPanel objects were visible — one parented, one floating at
     world origin.

---

## Task 2: Hide Mouse Cursor in Play Mode and Builds

**Files:**
- Modify: `Assets/_App/Bootstrap/AppBootstrap.cs`

**Why here:** `AppBootstrap` is the first MonoBehaviour that runs (it's in the Bootstrap
scene which is always loaded). Setting `Cursor.visible = false` here applies for the entire
session. On Meta Quest, the cursor is never rendered regardless; this fix targets Play Mode
in the Unity Editor and any PC builds.

- [ ] **Open** `Assets/_App/Bootstrap/AppBootstrap.cs`. Current content:

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;

public class AppBootstrap : MonoBehaviour
{
    private void Start()
    {
        SceneManager.LoadScene("MainMenu", LoadSceneMode.Additive);
    }
}
```

- [ ] **Replace the entire file** with:

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;

public class AppBootstrap : MonoBehaviour
{
    private void Start()
    {
        Cursor.visible   = false;
        Cursor.lockState = CursorLockMode.Locked;
        SceneManager.LoadScene("MainMenu", LoadSceneMode.Additive);
    }
}
```

- [ ] **Wait for Unity to compile.** Console must show 0 errors.

- [ ] **Verify in Play Mode:**
  1. Enter Play Mode.
  2. Move the mouse over the Game view.
  3. Expected: no OS cursor visible. The cursor is locked to the center of the window
     (standard Unity cursor lock behavior).
  4. Exit Play Mode — cursor returns to normal (Unity restores it automatically on exit).
