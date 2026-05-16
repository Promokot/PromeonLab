# VR Keyboard — Design Spec
Date: 2026-05-16

## Problem

1. `KeyboardButton` in UserPanel has no toggle logic — Default and Keyboard objects are not swapped.
2. `KeyboardButtonController` delegates to `GameManager.Instance` (demo singleton from keyboard package)
   which is always null at runtime → keystrokes only log to console, nothing written to any field.

## Solution Overview

Three new scripts + one event struct + minimal modification to the keyboard package controller.
No hierarchy changes to UserPanel prefab.

## Components

### `KeyboardFocusEvent` (in `AppEvents.cs`)
```
struct KeyboardFocusEvent { TMP_InputField Target; }
```
Published by any input field when the user taps it. Consumed by `VrKeyboard`.

### `VrKeyboard` — on `OverlaysSlot/Keyboard` root
- Subscribes to `KeyboardFocusEvent` via injected `EventBus`
- Stores `_target: TMP_InputField`
- Exposes `AddLetter(string)`, `DeleteLetter()`, `SubmitWord()`

### `UserPanelKeyboardToggle` — on UserPanel root
- Wires `KeyboardButton.onClick` → `OnToggle()`
- `OnToggle()` swaps `Default.SetActive` / `Keyboard.SetActive`
- Fields: `_keyboardButton`, `_defaultContent` (Default GO), `_keyboardContent` (Keyboard GO)

### `VrInputFieldProxy` — added alongside each `TMP_InputField` that needs keyboard
- `IPointerDownHandler` → publishes `KeyboardFocusEvent`
- Resolves `EventBus` from `SceneLifetimeScope` in `Awake` (no VContainer registration needed)

### `KeyboardButtonController` (modified, keyboard package)
- Caches `VrKeyboard` via `GetComponentInParent<VrKeyboard>()` in `Awake`
- `AddLetter / DeleteLetter / SubmitWord` delegate to `_keyboard` via null-conditional

## Flow

```
User taps TMP_InputField
  → VrInputFieldProxy.OnPointerDown → Publish(KeyboardFocusEvent { target })
    → VrKeyboard.OnFocus → _target = target

User presses KeyboardButton
  → UserPanelKeyboardToggle.OnToggle → Default off, Keyboard on

User presses key
  → KeyboardButtonController.AddLetter → VrKeyboard.AddLetter → _target.text += letter

User presses KeyboardButton again
  → UserPanelKeyboardToggle.OnToggle → Keyboard off, Default on
```

## Constraints
- No singleton pattern, no FindObjectOfType, no static mutable state
- No changes to UserPanel prefab hierarchy
- Developer must manually add `VrInputFieldProxy` to each new input field (documented in `docs/developer-notes/vr-keyboard.md`)
