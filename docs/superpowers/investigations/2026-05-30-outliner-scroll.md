# Outliner scroll "flies up" investigation (task 4)

Date: 2026-05-30
Mode: READ-ONLY (Read / Grep / Glob only). No files were edited.

## Summary

When the Outliner panel opens/populates, its `ScrollRect` content is not at the top —
the list starts scrolled down so the first items are hidden. The panel script
(`OutlinerPanel`) rebuilds the row list on enable and on scene events but **never touches
the `ScrollRect` scroll position**. The script does not even hold a reference to the
`ScrollRect`. Because the content `RectTransform` in the prefab is saved with a non-zero
scrolled offset (`m_AnchoredPosition.y ≈ 103.3`) and the vertical scrollbar is saved at
`m_Value ≈ 0.836` (not `1` = top), the panel inherits that stale mid-scroll state on
load and nothing ever resets it. Adding a "scroll to top after rebuild" step is the fix.

## Root cause

### The script that runs on the prefab

`SceneOutlinerModule.prefab` has a MonoBehaviour whose script GUID is
`6f91ee46bdd1c2f41bac3aeaacb9336f`
(`SceneOutlinerModule.prefab:104`). That GUID resolves to `OutlinerPanel.cs`
(`Assets/_App/Scripts/SpatialUi/Panels/OutlinerPanel.cs.meta:2`).

> Note: the prefab's `m_EditorClassIdentifier` reads `Subsystems.SpatialUi::SceneOutlinerView`
> (`SceneOutlinerModule.prefab:106`). That string is a stale serialized hint only — no
> `SceneOutlinerView` type exists anywhere in `Assets/_App/Scripts` (Glob found none). The
> live binding is the GUID, so the active component is `OutlinerPanel`.

The serialized `_rowsRoot` points to fileID `3118048587844026723`
(`SceneOutlinerModule.prefab:107`), which is the `Content` RectTransform
(`SceneOutlinerModule.prefab:699-717`) that carries the `VerticalLayoutGroup`
(`SceneOutlinerModule.prefab:718-743`).

### The script never sets scroll position

`OutlinerPanel` populates rows in `Rebuild()`:

- `OnEnable()` subscribes to events and calls `Rebuild()` — `OutlinerPanel.cs:27-35`.
- `OnModified(SceneModifiedEvent)` calls `Rebuild()` — `OutlinerPanel.cs:46`.
- `Rebuild()` destroys all existing rows and re-instantiates them under `_rowsRoot`
  via `AddRowsRecursive` — `OutlinerPanel.cs:64-83`, `OutlinerPanel.cs:93-114`.

Grep across `Assets/_App/Scripts` for `verticalNormalizedPosition`, `normalizedPosition`,
and `ScrollRect` shows **no match inside `OutlinerPanel.cs`** (and no scroll-reset
anywhere related to the outliner). The panel has no `ScrollRect` field at all
(`OutlinerPanel.cs:8-17`). So after rows are rebuilt, whatever scroll offset the
`ScrollRect` currently holds is left untouched.

### The prefab is saved in a scrolled-down state

The `Content` RectTransform (`SceneOutlinerModule.prefab:699-717`):
- `m_AnchorMin {0,1}`, `m_AnchorMax {1,1}`, `m_Pivot {0,1}` — top-anchored/top-pivot,
  which is the correct setup for a top-down vertical list. So anchoring is NOT the bug.
- `m_AnchoredPosition: {x: -0.008, y: 103.29545}` — a **non-zero positive Y offset**, i.e.
  the content is already pushed down (scrolled away from top) in the saved asset.
- `m_SizeDelta: {x: 0, y: 1000}` — fixed 1000px tall content (see ContentSizeFitter note).

The vertical `Scrollbar` is saved at `m_Value: 0.83575916` with `m_Size: 0.371`
(`SceneOutlinerModule.prefab:990-991`). A value of `1` means top; `0.836` is mid-list.
The `ScrollRect` (`SceneOutlinerModule.prefab:466-495`) is `m_MovementType: 1` (Elastic),
with both horizontal and vertical movement enabled and the content/viewport wired
correctly (`m_Content: 3118048587844026723`, `m_Viewport: 2554700069621550276`).

When the panel GameObject is enabled, Unity restores this serialized offset/scrollbar
value, `OnEnable → Rebuild()` repopulates rows, and nothing snaps the view back to the
top — so the user sees the list "flown up."

### No ContentSizeFitter (timing nuance)

The `Content` object has a `VerticalLayoutGroup` (`SceneOutlinerModule.prefab:718-743`,
`m_ChildControlHeight: 0`) but **no `ContentSizeFitter`** — content height is a fixed
`1000` (`SizeDelta.y`). This means the classic "ContentSizeFitter recalculates a frame
late and overwrites my scroll set" race is less of a concern here than usual, but the
`VerticalLayoutGroup` still repositions children during the layout pass, so the robust
fix should still force a layout rebuild before setting the scroll position (see below).

## Proposed fix

Make `OutlinerPanel` explicitly scroll to the top after each rebuild.

1. Add a serialized field and wire it in the prefab:
   `[SerializeField] private ScrollRect _scrollRect;` in
   `Assets/_App/Scripts/SpatialUi/Panels/OutlinerPanel.cs` (alongside `_rowsRoot`,
   ~`OutlinerPanel.cs:8`). Assign it to the `ScrollView "OunlinerList"` ScrollRect
   (`SceneOutlinerModule.prefab:466`).

2. At the end of `Rebuild()` (`OutlinerPanel.cs:81-82`, after `AddRowsRecursive` and
   `ApplyHighlight`), force a layout pass and snap to top:

   ```csharp
   if (_scrollRect != null)
   {
       Canvas.ForceUpdateCanvases();
       LayoutRebuilder.ForceRebuildLayoutImmediate(
           (RectTransform)_scrollRect.content);
       _scrollRect.verticalNormalizedPosition = 1f;   // 1 = top
       _scrollRect.horizontalNormalizedPosition = 0f; // optional: left edge
   }
   ```

   `LayoutRebuilder` lives in `UnityEngine.UI`, already imported in `OutlinerItem.cs`;
   add `using UnityEngine.UI;` to `OutlinerPanel.cs` (it currently only imports
   `System`, `System.Collections.Generic`, `UnityEngine`, `VContainer` —
   `OutlinerPanel.cs:1-4`).

   Rationale: forcing the canvas/layout update first ensures the `VerticalLayoutGroup`
   has positioned the freshly instantiated rows, so setting
   `verticalNormalizedPosition = 1f` is not overwritten by a later layout pass. Setting
   it to `1f` is the standard uGUI "scroll to top" for a top-pivot content.

3. Optional belt-and-suspenders for the destroyed-row timing: because `Rebuild()` uses
   `Destroy()` (`OutlinerPanel.cs:67`), the old rows are not gone until end of frame.
   If a residual jump persists, defer the snap one frame
   (`StartCoroutine`/`yield return null` then set `verticalNormalizedPosition = 1f`),
   following the convention of a `*Routine`-suffixed coroutine per CLAUDE.md naming. The
   `ForceRebuildLayoutImmediate` approach above usually makes this unnecessary.

4. Optional cleanup (not required for the fix): the prefab's baked
   `Content.m_AnchoredPosition.y = 103.3` (`SceneOutlinerModule.prefab:715`) and vertical
   scrollbar `m_Value = 0.836` (`SceneOutlinerModule.prefab:990`) could be reset to
   top (`y = 0`, value `1`) so the prefab opens correctly even before the first rebuild.

## Verification steps

1. Open `SceneOutlinerModule.prefab` (or a scene containing it) in the Editor; confirm
   the active component is `OutlinerPanel` and assign the new `_scrollRect` reference to
   the `ScrollView "OunlinerList"` object.
2. Enter Play mode with a scene that has more outliner rows than fit in the viewport.
   On open, the first item must be at the top and the vertical scrollbar handle at the
   top.
3. Trigger a `SceneModifiedEvent` (add/remove a scene node) so `Rebuild()` runs again;
   confirm the list snaps back to the top rather than retaining a mid-scroll position.
4. Toggle the panel off/on (`OnDisable`/`OnEnable`) and confirm it reopens at the top.
5. Regression: scroll down manually, then rename a node (`NodeRenamedEvent`, handled by
   `OnNodeRenamed` — `OutlinerPanel.cs:49-54`) which does NOT call `Rebuild()`; confirm
   the rename path does not force-scroll (only full rebuilds should reset to top).

## Open questions

- Desired behavior on `SceneModifiedEvent` rebuilds: always snap to top, or preserve the
  user's current scroll position? The proposed fix snaps to top on every `Rebuild()`. If
  preserving position during edits is preferred, gate the snap to first-open only (e.g. a
  flag set in `OnEnable`).
- Horizontal scroll: the `ScrollRect` has `m_Horizontal: 1` (`SceneOutlinerModule.prefab:479`)
  and a horizontal scrollbar saved at `m_Value: 1.426` (out of 0..1 range,
  `SceneOutlinerModule.prefab:864`). The outliner is a vertical list; horizontal scrolling
  may be unintended. Confirm whether `m_Horizontal` should be disabled.
- The stale `m_EditorClassIdentifier` "SceneOutlinerView" hint
  (`SceneOutlinerModule.prefab:106`) suggests the script was renamed from `SceneOutlinerView`
  to `OutlinerPanel` (or moved subsystems) without re-serializing. Harmless at runtime, but
  worth confirming there is no second outliner implementation expected.
