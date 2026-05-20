# Scene Loading Isolation Design

> **Status:** Approved — ready for implementation

**Goal:** Stop the bootstrap scene's lights, skybox, fog, and ambient settings from bleeding into additively-loaded mode scenes. Each mode scene must render with its own environment.

**Approach:** Keep additive scene loading (preserves the persistent VContainer root scope and XR rig infrastructure). After every additive load, mark the newly-loaded scene as Unity's Active Scene so its `RenderSettings` drive rendering. Strip all visual content (Lights, skybox, ambient, fog) from the bootstrap scene so it has nothing left to bleed.

**Tech Stack:** Unity 6, `UnityEngine.SceneManagement`. No new dependencies.

---

## Why

The bootstrap scene is loaded first and never unloads — it carries persistent infrastructure (`RootLifetimeScope`, XR Rig, `UserPanel`, `AssetBrowserModule`, `VrKeyboard`, `PlayerSpawnApplier`). When `AppBootstrap.Start` and later `ModeOrchestrator.TransitionTo` load mode scenes via `LoadSceneMode.Additive`, Unity keeps bootstrap as the **Active Scene** — the active scene owns the live `RenderSettings` (skybox material, ambient mode/color/intensity, fog). Additionally, every `Light` in any loaded scene (including bootstrap) illuminates every other loaded scene; lights do not respect the active-scene boundary.

So today:
- Bootstrap's skybox is shown even when MainMenu has its own
- Bootstrap's fog drowns out mode-scene fog (or vice versa)
- A bootstrap Directional Light double-lights mode-scene geometry on top of mode-scene lights

The fix is two-part:
1. **Strip visuals from bootstrap.** Bootstrap is pure infrastructure — no Lights, no skybox, no fog, neutral ambient. With no visuals to bleed, nothing leaks.
2. **Transfer active-scene status to the loaded mode scene.** After every additive load, call `SceneManager.SetActiveScene(loadedScene)`. From that point on, the mode scene's `RenderSettings` drive rendering. Bootstrap's render settings become inert.

Alternatives rejected:
- *`LoadSceneMode.Single` + `DontDestroyOnLoad`* — breaks `RootLifetimeScope` because VContainer registers `FindAnyObjectByType` references (`UserPanel`, `AssetBrowserModule`, `PlayerSpawnApplier`, `VrKeyboard`) that would be destroyed and re-created with each transition. Significantly larger refactor.
- *Per-frame skybox/fog overrides in code* — masks the symptom; lights still bleed across scenes; brittle.

---

## Runtime Behavior

```
Frame 0:                Bootstrap loaded by Unity (Build Settings index 0).
                        Bootstrap is the Active Scene. AppBootstrap.Start runs.
                        AppBootstrap.Start → LoadSceneMode.Additive ("MainMenu")
                        + subscribe sceneLoaded.

Frame 1 (sceneLoaded):  MainMenu fully integrated. Callback fires:
                          SceneManager.SetActiveScene(MainMenu) → MainMenu becomes Active.
                          MainMenu's RenderSettings (skybox/fog/ambient) take effect.

Later, mode transition: ModeOrchestrator.TransitionTo(VrEditing)
                          → UnloadSceneAsync(MainMenu)
                          → LoadScene("VrEditing", Additive)
                          → sceneLoaded fires:
                              SetActiveScene(VrEditing) (NEW)
                              + existing player spawn anchor logic
```

At any given moment after Frame 1:
- Bootstrap scene is loaded but **not active** — its `RenderSettings` are not consulted by Unity's renderer
- Exactly one mode scene is loaded and is the Active Scene — its `RenderSettings` drive the frame
- No Lights live in bootstrap, so only the active mode scene's lights illuminate anything

---

## Code Changes

### `Assets/_App/Bootstrap/AppBootstrap.cs`

Replace the entire file with:

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

Difference: subscribe to `sceneLoaded`, set MainMenu as active scene once it finishes loading, unsubscribe.

### `Assets/_App/Subsystems/ModeOrchestrator/ModeOrchestrator.cs`

Extend the existing `OnSceneLoadedForSpawn` callback to also call `SetActiveScene`. Only the body of `OnSceneLoadedForSpawn` changes:

```csharp
private void OnSceneLoadedForSpawn(Scene scene, LoadSceneMode mode)
{
    SceneManager.sceneLoaded -= OnSceneLoadedForSpawn;
    SceneManager.SetActiveScene(scene);                  // NEW

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

Rest of `ModeOrchestrator.cs` is unchanged. The single new line is `SceneManager.SetActiveScene(scene);` placed immediately after the unsubscribe.

---

## Manual Scene-File Changes (Unity Editor)

These cannot be done from code — they require opening the scene in the Editor.

### Bootstrap scene

1. Open the bootstrap scene (the one at Build Settings index 0, containing `AppBootstrap` and `RootLifetimeScope`).
2. Delete every GameObject that has a `Light` component (Directional Light, Point Light, etc.).
3. Open **Window → Rendering → Lighting → Environment** while the bootstrap scene is the only one open:
   - Skybox Material: **None**
   - Ambient Source: **Color**
   - Ambient Color: **black** (`#000000`)
   - Reflection Source: **Skybox** (with no skybox = no reflection probe contribution)
4. Open **Window → Rendering → Lighting → Scene (Other Settings)**:
   - Fog: **off**
5. Save the bootstrap scene.

### Mode scenes (MainMenu, VrEditing, ArMapping, ArPreview, Sandbox)

For each mode scene, verify it owns its full environment. Required content depends on the scene's intent:

- **VR scenes (MainMenu, VrEditing, Sandbox):** at least one Directional Light, an appropriate Skybox material (or solid color), ambient lighting tuned to the scene, fog if desired.
- **AR scenes (ArMapping, ArPreview):** no Skybox (`Skybox Material = None`), minimal ambient (the camera shows passthrough so lighting only affects spawned virtual content), fog off unless visual intent calls for it.

There's no automated check — if a scene visually looks wrong after the code changes ship, this is where to fix it.

---

## Testing

This is configuration + integration with `SceneManager`; no EditMode unit tests added. Verify by hand in the Editor:

1. **First boot:** Play → MainMenu loads → its skybox/lights/fog are visible. The bootstrap scene shows in the Hierarchy panel but contributes no light.
2. **Mode transition:** From MainMenu, trigger a transition to VrEditing (existing flow). VrEditing's skybox/lights/fog replace MainMenu's. No bleed-through.
3. **Round trip:** Transition back to MainMenu. MainMenu's environment is restored.
4. **Active scene check:** During Play mode, the Hierarchy panel should show the current mode scene name in bold (Unity's marker for Active Scene). Bootstrap should never be bold after Frame 1.

---

## Known Limitations / Out of Scope

- **Async loading:** Sticking with sync `LoadScene` to match the existing pattern. If frame stutter on transitions becomes a problem, swap to `LoadSceneAsync` later — the `SetActiveScene` call already lives in the `sceneLoaded` callback, which fires for both sync and async loads.
- **`PlayerSpawnApplier`:** Already subscribes to `sceneLoaded` and teleports the XR rig to the spawn anchor in the loaded scene. Its behavior is unchanged by this work.
- **Reflection probes:** If mode scenes need baked reflection probes, that's a separate lightmapping concern. This spec doesn't address baked lighting.
- **Lighting settings asset:** Unity stores per-scene lighting in a `*.lighting` settings asset alongside the scene. If a mode scene doesn't have one explicitly, Unity uses defaults. Verifying or creating these is part of the manual scene-file pass above.
