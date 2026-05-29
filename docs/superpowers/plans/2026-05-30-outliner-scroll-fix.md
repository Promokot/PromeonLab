# Outliner Scroll-To-Top Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans (small; inline). Checkbox steps.
>
> **Root-cause reference:** `docs/superpowers/investigations/2026-05-30-outliner-scroll.md`.
>
> **Git note:** user commits manually. **Unity note:** controller compiles via MCP; one **[MANUAL EDITOR]** step assigns a field + fixes the saved scroll offset in the prefab; verified in-VR by the user.
>
> **Dependency:** `OutlinerPanel.cs` was migrated to `SceneContext` in Plan B — the `Rebuild()` method (`:64-83` pre-migration) still exists and is where the fix lands.

**Goal:** When the Outliner opens/rebuilds, the list shows from the top instead of "flying up" — caused by `Rebuild()` never resetting the `ScrollRect` and the prefab being saved mid-scroll (`Content.m_AnchoredPosition.y ≈ 103`, scrollbar `m_Value ≈ 0.836`).

**Architecture:** Add a serialized `ScrollRect` reference; after repopulating rows, force a layout rebuild and set `verticalNormalizedPosition = 1f` (top), with a one-frame-deferred fallback because rows are `Destroy()`d (destruction is deferred, so the content size isn't final the same frame).

---

## Task 1: Reset scroll position on Rebuild

**Files:** `Assets/_App/Scripts/SpatialUi/Panels/OutlinerPanel.cs`

- [ ] **Step 1:** Add `using UnityEngine.UI;` and a field `[SerializeField] private ScrollRect _scrollRect;`.
- [ ] **Step 2:** At the END of `Rebuild()` (after `AddRowsRecursive(...)` + `ApplyHighlight()`), reset the scroll to top:
```csharp
        ScrollToTop();
```
Add the helper (forces layout to settle first, then snaps to top; defers one frame as a fallback since destroyed rows + layout group settle late):
```csharp
    private void ScrollToTop()
    {
        if (_scrollRect == null) return;
        Canvas.ForceUpdateCanvases();
        if (_rowsRoot is RectTransform rt) LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        _scrollRect.verticalNormalizedPosition = 1f;
        // Fallback: rows are Destroy()d (deferred), so content height may finalize next frame.
        StartCoroutine(SnapTopNextFrame());
    }

    private System.Collections.IEnumerator SnapTopNextFrame()
    {
        yield return null;
        if (_scrollRect != null) _scrollRect.verticalNormalizedPosition = 1f;
    }
```
(If `_rowsRoot` is the ScrollRect content, the `LayoutRebuilder` call targets it; if the content is a different transform, target the ScrollRect's `content` instead — confirm against the prefab in Step 4.)
- [ ] **Step 3 (controller): compile.** No `CS`.
- [ ] **Step 4 [MANUAL EDITOR]:** In the Outliner prefab (`SceneOutlinerModule.prefab`): assign `OutlinerPanel._scrollRect` to the panel's `ScrollRect`. Reset the saved scroll: set `Content.m_AnchoredPosition.y = 0` (or top per pivot) and the vertical scrollbar `m_Value = 1`. Confirm `_rowsRoot` is (or is under) the ScrollRect `content`. **Optional (report open Q):** the ScrollRect has `m_Horizontal: 1` for a vertical list — set `m_Horizontal = 0` if horizontal scrolling is unintended.
- [ ] **Step 5 (user, VR): verify** open the Outliner (and after scene changes that rebuild it) → list starts at the top, first items visible; scrolling still works.
- [ ] **Checkpoint (user commits)** — `fix(outliner): reset ScrollRect to top on Rebuild`

---

## Self-Review

**Coverage:** root cause (Rebuild never sets scroll position; prefab saved scrolled) → serialized `ScrollRect` + `ScrollToTop` after repopulate + prefab reset (Task 1). **Placeholders:** none — exact code + the prefab fields to fix; the `_rowsRoot`-vs-`content` nuance is called out with a confirm step. **Risk:** deferred `Destroy()` of rows → handled by the next-frame fallback. Horizontal-scroll cleanup is optional per the report.
