# Scene Loading Isolation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stop the bootstrap scene's lights, skybox, fog, and ambient settings from leaking into additively-loaded mode scenes — each mode scene must render with its own environment.

**Architecture:** Keep additive scene loading (preserves the persistent VContainer root scope and XR rig). After every additive load, transfer Unity's Active Scene status to the loaded scene so its `RenderSettings` drive rendering. Strip all visual content from the bootstrap scene (Lights, skybox, ambient, fog) so nothing remains to bleed.

**Tech Stack:** Unity 6, `UnityEngine.SceneManagement`. No new dependencies, no new tests (config + SceneManager integration, verified manually in Editor).

> **No git commits** — the user manages version control manually. Skip all commit steps.

---

## File Map

| File | Change |
|---|---|
| `Assets/_App/Bootstrap/AppBootstrap.cs` | **Replace** — subscribe to `sceneLoaded`, call `SetActiveScene` after MainMenu loads |
| `Assets/_App/Subsystems/ModeOrchestrator/ModeOrchestrator.cs` | **Modify** — add one line to `OnSceneLoadedForSpawn` to call `SetActiveScene` |
| Bootstrap scene asset (Editor, manual) | Strip Lights, neutralize RenderSettings |
| Mode-scene assets (Editor, manual) | Verify each owns its Light/Skybox/Fog |

---

## Task 1: Update Code (Both Files)

**Files:**
- Modify: `Assets/_App/Bootstrap/AppBootstrap.cs`
- Modify: `Assets/_App/Subsystems/ModeOrchestrator/ModeOrchestrator.cs`

Both edits are small and tightly coupled (both add `SetActiveScene` after additive load), so they ship together.

- [ ] **Step 1: Replace the entire content of `AppBootstrap.cs`**

Path: `S:\[02] Projects\[02] Study\[00] Repositories\PromeonLab\Assets\_App\Bootstrap\AppBootstrap.cs`

Replace the whole file with:

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;
using Scene = UnityEngine.SceneManagement.Scene;

public class AppBootstrap : MonoBehaviour
{
    private const string MAIN_MENU_SCENE = "MainMenu";

    private void Start()
    {
        Cursor.visible   = false;
        Cursor.lockState = CursorLockMode.Locked;
        SceneManager.sceneLoaded += OnMainMenuLoaded;
        SceneManager.LoadScene(MAIN_MENU_SCENE, LoadSceneMode.Additive);
    }

    private void OnMainMenuLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != MAIN_MENU_SCENE) return;
        SceneManager.sceneLoaded -= OnMainMenuLoaded;
        SceneManager.SetActiveScene(scene);
    }
}
```

Differences from the previous version:
- Adds `MAIN_MENU_SCENE` constant for the scene name
- Adds `SceneManager.sceneLoaded` subscription before the load call
- Adds `OnMainMenuLoaded` handler that calls `SetActiveScene` and unsubscribes
- Adds `using Scene = UnityEngine.SceneManagement.Scene;` alias (the codebase uses this pattern in `ModeOrchestrator.cs`)

- [ ] **Step 2: Modify `ModeOrchestrator.OnSceneLoadedForSpawn`**

Path: `S:\[02] Projects\[02] Study\[00] Repositories\PromeonLab\Assets\_App\Subsystems\ModeOrchestrator\ModeOrchestrator.cs`

Find the existing `OnSceneLoadedForSpawn` method (around lines 38–52):

```csharp
private void OnSceneLoadedForSpawn(Scene scene, LoadSceneMode mode)
{
    SceneManager.sceneLoaded -= OnSceneLoadedForSpawn;
    foreach (var root in scene.GetRootGameObjects())
    {
        var anchor = root.GetComponentInChildren<PlayerSpawnAnchor>(true);
        if (anchor == null) continue;
        _bus.Publish(new PlayerSpawnRequestedEvent
        {
            Position = anchor.transform.position,
            Rotation = anchor.transform.rotation
        });
        return;
    }
}
```

Insert a single new line — `SceneManager.SetActiveScene(scene);` — immediately after the unsubscribe. Resulting method:

```csharp
private void OnSceneLoadedForSpawn(Scene scene, LoadSceneMode mode)
{
    SceneManager.sceneLoaded -= OnSceneLoadedForSpawn;
    SceneManager.SetActiveScene(scene);
    foreach (var root in scene.GetRootGameObjects())
    {
        var anchor = root.GetComponentInChildren<PlayerSpawnAnchor>(true);
        if (anchor == null) continue;
        _bus.Publish(new PlayerSpawnRequestedEvent
        {
            Position = anchor.transform.position,
            Rotation = anchor.transform.rotation
        });
        return;
    }
}
```

Everything else in `ModeOrchestrator.cs` is unchanged.

- [ ] **Step 3: Switch to Unity Editor and wait for recompile**

Switch focus to the Unity Editor. Wait for the spinner to finish recompiling.

Open the **Console** window.

Expected: zero compile errors.

If you see `error CS0246: type or namespace 'Scene' could not be found` in `AppBootstrap.cs` — the `using Scene = UnityEngine.SceneManagement.Scene;` alias is missing. Verify Step 1 was applied verbatim.

---

## Task 2: Manual Editor Cleanup (Scene Assets)

**Files (modified through the Editor UI, not directly):**
- Bootstrap scene (the scene at Build Settings index 0 — the one containing `AppBootstrap` and `RootLifetimeScope`)
- Each mode scene: `MainMenu`, `VrEditing`, `ArMapping`, `ArPreview`, `Sandbox`

This task cannot be done from code. Follow the steps in Unity Editor.

- [ ] **Step 1: Open the bootstrap scene by itself**

`File → Open Scene` → select the bootstrap scene (index 0 in Build Settings). Make sure no other scene is open additively in the Editor while you do this.

- [ ] **Step 2: Delete every Light in the bootstrap scene**

In the Hierarchy:
- Search the hierarchy for any GameObject with a `Light` component (Directional Light, Point Light, Spot Light, Area Light)
- Delete each one (right-click → Delete, or select and press `Delete`)
- Verify: the Hierarchy has no Lights left

- [ ] **Step 3: Neutralize Environment lighting in the bootstrap scene**

Open `Window → Rendering → Lighting`. Select the **Environment** tab. Set:
- **Skybox Material:** None (drag-clear or click the target icon → None)
- **Ambient Source:** Color
- **Ambient Color:** black (`#000000`)
- **Reflection Source:** Skybox (with no skybox set above, this contributes nothing)

- [ ] **Step 4: Disable Fog in the bootstrap scene**

Still in `Window → Rendering → Lighting`. Select the **Scene** tab → **Other Settings** section. Set:
- **Fog:** unchecked (off)

- [ ] **Step 5: Save the bootstrap scene**

`File → Save` (or `Ctrl+S`). The bootstrap scene now has no Lights, no skybox, no ambient contribution, no fog.

- [ ] **Step 6: For each mode scene — open and verify it owns its environment**

For each of: `MainMenu`, `VrEditing`, `ArMapping`, `ArPreview`, `Sandbox`:

1. `File → Open Scene` → select the mode scene.
2. Inspect Hierarchy + Lighting window.
3. Configure based on the scene's purpose:

**VR scenes (`MainMenu`, `VrEditing`, `Sandbox`):**
- At least one **Directional Light** in the Hierarchy (rotation/intensity tuned per scene)
- **Skybox Material:** assigned (the default URP procedural skybox is fine if you have no custom one)
- **Ambient Source:** Skybox (or Gradient/Color if you prefer)
- **Fog:** optional, off by default unless you want it

**AR scenes (`ArMapping`, `ArPreview`):**
- **No Skybox** — passthrough camera fills the background. Set Skybox Material to None.
- **Ambient Source:** Color (a subtle neutral tone, e.g. `#404040`) so spawned virtual content isn't pitch-black
- **Fog:** off
- Optional: a single dim Directional Light if virtual objects need sun-like shading on top of passthrough

4. Save each scene (`Ctrl+S`) before moving to the next.

- [ ] **Step 7: Smoke test the round trip**

In the Editor, open the bootstrap scene by itself and press Play.

1. **MainMenu loads:** the skybox/lights/fog you set in `MainMenu.unity` should be visible. In the Hierarchy, `MainMenu` should appear in **bold** (Unity's marker for Active Scene).
2. **Trigger a mode transition** to `VrEditing` (use the existing UI flow — main menu button, or trigger `ModeOrchestrator.TransitionTo(AppMode.VrEditing)` via debug). The visual environment should swap to VrEditing's settings. `VrEditing` is now bold in the Hierarchy; `MainMenu` is gone (unloaded).
3. **Transition back** to MainMenu. MainMenu's environment is restored.
4. **The bootstrap scene** stays loaded throughout but is **never** bold — it never becomes the Active Scene after Frame 1.

If any of the above doesn't hold:
- Bootstrap shows in bold → `SetActiveScene` not firing. Check `OnSceneLoadedForSpawn` modification from Task 1 Step 2.
- Mode scene visuals look wrong / mixed → that mode scene file is missing Light/Skybox/Fog setup. Repeat Step 6 for that scene.
- Light from bootstrap still visible → a Light component was missed in Step 2. Re-open bootstrap and double-check.

---

## Done

After Task 2 Step 7 passes:
- Bootstrap scene carries only infrastructure; contributes no lights, skybox, fog, or ambient
- Each mode scene renders with its own RenderSettings as Unity's Active Scene
- Transitions between modes swap the visual environment cleanly
- Persistent infrastructure (DI root, XR rig, panels) keeps working unchanged
