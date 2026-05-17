# NavBar Exclusive Groups & Button State Fixes — Design Spec

**Date:** 2026-05-18

---

## Goal

1. Allow multiple nav bar panels to be open simultaneously when they don't occupy the same visual space (e.g. Outliner + Inspector). Currently all panels are mutually exclusive.
2. Fix two button-state visual bugs introduced in the previous session.

---

## Context

`UserPanel` manages a nav bar with 7 buttons. Each button toggles a child panel. The current `HideAllPanels` call closes every open panel whenever any button is clicked, making it impossible to have more than one panel open at a time.

The layout has two distinct panel zones:
- **Center zone**: Settings, AssetBrowser — share the same visual space, must be exclusive
- **Side zones**: Outliner (left), Inspector (right) — independent, can coexist with each other and with center-zone panels

---

## Design

### 1. Data Model — `NavBarConfig.Entry.ExclusiveGroup`

Add one optional string field to `NavBarConfig.Entry`:

```csharp
[Serializable]
public struct Entry
{
    public string    Id;
    public AppMode[] VisibleModes;
    public string    ExclusiveGroup; // empty/null = no conflict with any panel
}
```

**Semantics:** panels sharing the same non-empty `ExclusiveGroup` value are mutually exclusive. Opening one closes all others in the group. Panels with an empty or null group open independently alongside anything.

**`DefaultNavBarConfig.asset` configuration:**

| Id        | ExclusiveGroup |
|-----------|----------------|
| settings  | center         |
| assets    | center         |
| outliner  | *(empty)*      |
| inspector | *(empty)*      |
| timeline  | *(empty)*      |
| rigging   | *(empty)*      |
| gizmo     | *(empty)*      |

---

### 2. UserPanel Logic

`HideAllPanels(int exceptIdx)` is removed and replaced with `HidePanelsInGroup(string group, int exceptIdx)`.

`OnNavButtonClicked(int idx)` updated:

```
if panel == null → return (placeholder)
willShow = !panel.activeSelf
if willShow:
    group = GetGroup(idx)              // reads ExclusiveGroup from NavBarConfig
    if group is not empty:
        HidePanelsInGroup(group, idx)  // closes only same-group panels
panel.SetActive(willShow)
SetActiveState(idx, willShow)
```

`HidePanelsInGroup` iterates all bindings, skips `exceptIdx`, skips panels whose group doesn't match, and calls `SetActive(false)` + `SetActiveState(i, false)` on matching open panels.

`ApplyMode` is unchanged — it already hides panels whose button becomes invisible in the new mode, which is correct regardless of groups.

---

### 3. Bug Fix — Highlight Color Invisible on Bright Buttons (1.0)

`Brighten()` multiplies the HSV V channel. When V is near 1.0 (light button color), `V × 1.2` is clamped and produces no visible change.

Fix: for `mult > 1`, use an additive delta proportional to remaining headroom:

```csharp
private static Color Brighten(Color c, float mult)
{
    Color.RGBToHSV(c, out float h, out float s, out float v);
    var vNew = mult >= 1f
        ? Mathf.Clamp01(v + (mult - 1f) * (1f - v + 0.05f))
        : v * mult;
    var result = Color.HSVToRGB(h, s, vNew);
    result.a = c.a;
    return result;
}
```

For `mult < 1` (dimming), multiplicative is kept — it always has room to darken.

---

### 4. Bug Fix — White Flash After Button Click (1.1)

After clicking a button, Unity's EventSystem keeps it in "selected" state. If `ColorBlock.selectedColor` is white (Unity default), the button shows white until focus moves elsewhere.

Fix: explicitly set `selectedColor = baseColor` in `_inactiveColors` during `Start()`:

```csharp
var inactive              = block;
inactive.normalColor      = baseColor;
inactive.highlightedColor = Brighten(baseColor, _inactiveHoverBrightness);
inactive.selectedColor    = baseColor;   // ← added
_inactiveColors[i]        = inactive;
```

---

## Files Changed

| File | Change |
|------|--------|
| `NavBarConfig.cs` | Add `ExclusiveGroup` string to `Entry` struct |
| `DefaultNavBarConfig.asset` | Set `ExclusiveGroup` values in inspector |
| `UserPanel.cs` | Replace `HideAllPanels` with `HidePanelsInGroup`; fix `Brighten`; fix `selectedColor` |

No other files change.

---

## Out of Scope

- DetachablePanel wiring (Outliner, Inspector) — separate task per wiring guide
- Timeline, Rigging, Gizmo panel implementation — future phases
