# VR Keyboard System

The floating keyboard lives inside `UserPanel` prefab under `OverlaysSlot/Keyboard`.
It is a reusable input mechanism for any `TMP_InputField` in the app.

## How it works

1. `UserPanelKeyboardToggle` (on UserPanel root) toggles between `OverlaysSlot/Default`
   and `OverlaysSlot/Keyboard` when the user presses KeyboardButton.
2. `VrKeyboard` (on the Keyboard root) subscribes to `KeyboardFocusEvent` and stores
   the active `TMP_InputField`. All key presses write to that field.
3. `KeyboardButtonController` (keyboard package, modified) delegates `AddLetter /
   DeleteLetter / SubmitWord` to the `VrKeyboard` parent found via `GetComponentInParent`.

## Adding keyboard support to a new input field

Add `VrInputFieldProxy` as a component alongside `TMP_InputField` on the target object.
No other configuration needed — it resolves `EventBus` from `SceneLifetimeScope` at runtime.

> If the panel containing the field lives in a different scene or a Feature scope,
> replace `SceneLifetimeScope` with the appropriate scope type in `VrInputFieldProxy.Awake`.

## Files

| File | Purpose |
|---|---|
| `_App/Subsystems/SpatialUi/UI_Scripts/VrKeyboard.cs` | Receives focus events, writes to field |
| `_App/Subsystems/SpatialUi/UI_Scripts/UserPanelKeyboardToggle.cs` | Default ↔ Keyboard toggle |
| `_App/Subsystems/SpatialUi/UI_Scripts/VrInputFieldProxy.cs` | Publishes focus event on pointer down |
| `_App/_Shared/Events/AppEvents.cs` | `KeyboardFocusEvent` struct |
| `UnityPacks/Keyboard Package/Scripts/KeyboardButtonController.cs` | Modified to route through VrKeyboard |
