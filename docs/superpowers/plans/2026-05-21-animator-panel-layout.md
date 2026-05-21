# Animator Panel Layout Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **User memory rule:** `feedback_no_git_during_dev` + `feedback_no_auto_commits` — no git commands anywhere. The user manages git themselves. All "save" steps refer to saving the prefab stage via MCP, not `git commit`.

**Goal:** Fix the broken `AnimationModule.prefab` layout (anchors, sizes, LayoutGroup config, ScrollRect refs, TMP_InputField completion) so it visually matches the mockup `docs/developer-notes/animator_panel_vr_mockup_v4.html`. No hierarchy changes — only RectTransform/Layout values.

**Architecture:** Subagents open `AnimationModule.prefab` directly in its own Prefab Stage via Unity MCP (`mcp__unityMCP__manage_prefabs action=open_prefab_stage`). They apply RectTransform/LayoutGroup/LayoutElement values on a section-by-section basis, save the stage, and close. Each task is one section so the subagent context stays manageable.

**Tech Stack:** Unity 6000.3.7f1 UGUI (RectTransform, Image, TMP, ScrollRect, LayoutGroups), Unity MCP tools (`manage_prefabs`, `manage_gameobject`, `manage_components`, `refresh_unity`, `read_console`).

**Spec:** `docs/superpowers/specs/2026-05-21-animator-panel-layout-design.md`

---

## Common rules for all tasks

1. **Target file:** `Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/AnimationModule.prefab`
2. **Per-task workflow:**
   - Open prefab stage with `action=open_prefab_stage`
   - Use `find_gameobjects` (search_method=by_path, scoping to AnimationModule subtree) or by name within the stage to locate targets
   - Apply changes via `manage_components action=set_property` for RectTransform / LayoutGroup / LayoutElement, or `manage_gameobject action=modify` for `set_active`
   - For new child GameObjects (TMP_InputField sub-tree), use `manage_gameobject action=create parent="<parent-name>"`
   - Save: `action=save_prefab_stage`
   - Close: `action=close_prefab_stage`
3. **After each task:** `refresh_unity` (compile=request, wait_for_ready=true) → `read_console types=["error"]` → must be zero project errors. MCP infrastructure noise (Client handler exited / disposed) is OK.
4. **No git commands.** No "commit" steps. If a subagent's default flow includes commits, it must skip them.
5. **Anchor convention:** `(x, y)` for both anchorMin and anchorMax. For RectTransform properties via MCP, set `m_AnchorMin` (Vector2), `m_AnchorMax`, `m_Pivot`, `m_AnchoredPosition`, `m_SizeDelta`.
6. **LayoutElement is added as a component** if not already present. Many GameObjects don't have it yet — `manage_components action=add component_type=LayoutElement`, then set properties.

---

## Task 1: Root + EmptyState_NoSelection + EmptyState_NoContainer

**Files:**
- Modify: `Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/AnimationModule.prefab`

**Targets (3 GameObjects + nested children of EmptyState_NoContainer):**

### Step 1: Open the prefab stage

```
mcp__unityMCP__manage_prefabs
  action: open_prefab_stage
  prefab_path: Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/AnimationModule.prefab
```

- [ ] **Step 2: Apply Root RectTransform + Image background**

Target: `AnimationModule` (root GO inside the stage).

RectTransform properties:
- `m_AnchorMin`: `{x: 0, y: 0}`
- `m_AnchorMax`: `{x: 1, y: 1}`
- `m_Pivot`: `{x: 0.5, y: 0.5}`
- `m_AnchoredPosition`: `{x: 0, y: 0}`
- `m_SizeDelta`: `{x: 0, y: 0}`

Image properties on root:
- `m_Color`: `{r: 0.12, g: 0.12, b: 0.14, a: 1}`
- `m_RaycastTarget`: true (default ok)

- [ ] **Step 3: Apply EmptyState_NoSelection**

Target: `EmptyState_NoSelection` (child of AnimationModule).

RectTransform:
- `m_AnchorMin`: `{x: 0, y: 0}`
- `m_AnchorMax`: `{x: 1, y: 1}`
- `m_Pivot`: `{x: 0.5, y: 0.5}`
- `m_AnchoredPosition`: `{x: 0, y: 0}`
- `m_SizeDelta`: `{x: 0, y: 0}`

GameObject:
- `set_active`: false (use `manage_gameobject action=modify set_active=false`)

HintText child of EmptyState_NoSelection RectTransform:
- `m_AnchorMin`: `{x: 0, y: 0}`
- `m_AnchorMax`: `{x: 1, y: 1}`
- `m_Pivot`: `{x: 0.5, y: 0.5}`
- `m_AnchoredPosition`: `{x: 0, y: 0}`
- `m_SizeDelta`: `{x: 0, y: 0}`

HintText TextMeshProUGUI:
- `m_text`: "select an object to animate"
- `m_fontSize`: 14
- `m_color`: `{r: 0.7, g: 0.7, b: 0.7, a: 1}`
- `m_HorizontalAlignment`: 2 (Center)
- `m_VerticalAlignment`: 256 (Middle)

- [ ] **Step 4: Apply EmptyState_NoContainer root**

Target: `EmptyState_NoContainer`.

RectTransform:
- `m_AnchorMin`: `{x: 0, y: 0}`
- `m_AnchorMax`: `{x: 1, y: 1}`
- `m_Pivot`: `{x: 0.5, y: 0.5}`
- `m_AnchoredPosition`: `{x: 0, y: 0}`
- `m_SizeDelta`: `{x: 0, y: 0}`

GameObject:
- `set_active`: false

Add VerticalLayoutGroup (if not present):
- `manage_components action=add component_type=VerticalLayoutGroup target=EmptyState_NoContainer`

VerticalLayoutGroup properties:
- `m_Padding`: `{m_Left: 40, m_Right: 40, m_Top: 40, m_Bottom: 40}`
- `m_Spacing`: 16
- `m_ChildAlignment`: 4 (MiddleCenter)
- `m_ChildControlWidth`: true
- `m_ChildControlHeight`: true
- `m_ChildForceExpandWidth`: false
- `m_ChildForceExpandHeight`: false

- [ ] **Step 5: Apply EmptyState_NoContainer children**

For each child of `EmptyState_NoContainer`, ensure it has a LayoutElement component (add if missing) and set values:

**Icon:**
- LayoutElement: `m_PreferredWidth`: 48, `m_PreferredHeight`: 48, `m_FlexibleWidth`: 0, `m_FlexibleHeight`: 0
- Image: `m_Color`: `{r: 0.5, g: 0.5, b: 0.5, a: 1}`

**HintText:**
- LayoutElement: `m_PreferredWidth`: 400, `m_PreferredHeight`: 24
- TextMeshProUGUI: `m_text`: "this object has no animation container yet", `m_fontSize`: 15, `m_color`: `{r: 0.7, g: 0.7, b: 0.7, a: 1}`, alignment Center+Middle

**AddAnimationButton:**
- LayoutElement: `m_PreferredWidth`: 220, `m_PreferredHeight`: 52
- Image: `m_Color`: `{r: 0.18, g: 0.50, b: 0.95, a: 1}`
- Its Label child RectTransform: anchor stretch full `(0,0)`-`(1,1)`, sizeDelta `(0,0)`
- Label TextMeshProUGUI: `m_text`: "+ Add animation", `m_fontSize`: 15, `m_color`: `{r: 1, g: 1, b: 1, a: 1}`, alignment Center+Middle

**HintSubtext:**
- LayoutElement: `m_PreferredWidth`: 400, `m_PreferredHeight`: 18
- TextMeshProUGUI: `m_text`: "creates an action container with default 60 frames @ 24 fps", `m_fontSize`: 12, `m_color`: `{r: 0.5, g: 0.5, b: 0.5, a: 1}`, alignment Center+Middle

- [ ] **Step 6: Save + close prefab stage**

```
mcp__unityMCP__manage_prefabs action=save_prefab_stage
mcp__unityMCP__manage_prefabs action=close_prefab_stage
```

- [ ] **Step 7: Refresh + verify**

```
mcp__unityMCP__refresh_unity mode=force scope=all compile=request wait_for_ready=true
mcp__unityMCP__read_console types=["error"] count=20
```

Expected: zero project errors.

---

## Task 2: ActiveState VLG + LayoutElements on 3 children

**Files:**
- Modify: `Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/AnimationModule.prefab`

- [ ] **Step 1: Open prefab stage**

```
mcp__unityMCP__manage_prefabs action=open_prefab_stage prefab_path=Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/AnimationModule.prefab
```

- [ ] **Step 2: Apply ActiveState root**

Target: `ActiveState`.

RectTransform:
- `m_AnchorMin`: `{x: 0, y: 0}`
- `m_AnchorMax`: `{x: 1, y: 1}`
- `m_Pivot`: `{x: 0.5, y: 0.5}`
- `m_AnchoredPosition`: `{x: 0, y: 0}`
- `m_SizeDelta`: `{x: 0, y: 0}`

GameObject:
- `set_active`: false

VerticalLayoutGroup (already present per Phase 11c) properties:
- `m_Padding`: `{m_Left: 0, m_Right: 0, m_Top: 0, m_Bottom: 0}`
- `m_Spacing`: 0
- `m_ChildAlignment`: 0 (UpperLeft)
- `m_ChildControlWidth`: true
- `m_ChildControlHeight`: false
- `m_ChildForceExpandWidth`: true
- `m_ChildForceExpandHeight`: false

- [ ] **Step 3: LayoutElement on ActiveState children**

For each of `ToolbarTop`, `Body`, `ToolbarBottom` — ensure LayoutElement present (add via `manage_components action=add component_type=LayoutElement` if not), then:

**ToolbarTop:**
- `m_PreferredHeight`: 50
- `m_FlexibleHeight`: 0
- `m_MinHeight`: 50

**Body:**
- `m_FlexibleHeight`: 1
- `m_PreferredHeight`: -1 (means "no preference")
- `m_MinHeight`: 0

**ToolbarBottom:**
- `m_PreferredHeight`: 52
- `m_FlexibleHeight`: 0
- `m_MinHeight`: 52

- [ ] **Step 4: Save + close**

```
mcp__unityMCP__manage_prefabs action=save_prefab_stage
mcp__unityMCP__manage_prefabs action=close_prefab_stage
```

- [ ] **Step 5: Refresh + verify**

```
mcp__unityMCP__refresh_unity mode=force scope=all compile=request wait_for_ready=true
mcp__unityMCP__read_console types=["error"]
```

---

## Task 3: ToolbarTop HLG + 12 children LayoutElements

**Files:**
- Modify: `Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/AnimationModule.prefab`

- [ ] **Step 1: Open prefab stage**

```
mcp__unityMCP__manage_prefabs action=open_prefab_stage prefab_path=Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/AnimationModule.prefab
```

- [ ] **Step 2: Apply ToolbarTop HLG**

Target: `ToolbarTop`.

HorizontalLayoutGroup (already present per Phase 11c):
- `m_Padding`: `{m_Left: 10, m_Right: 14, m_Top: 8, m_Bottom: 8}`
- `m_Spacing`: 12
- `m_ChildAlignment`: 3 (MiddleLeft)
- `m_ChildControlWidth`: false
- `m_ChildControlHeight`: true
- `m_ChildForceExpandWidth`: false
- `m_ChildForceExpandHeight`: true

- [ ] **Step 3: LayoutElement on each ToolbarTop child**

For each child, add LayoutElement if missing, then set:

| Child | preferredWidth | preferredHeight | flexibleWidth |
|---|---|---|---|
| `CurrentFrameInput` | 72 | 44 | 0 |
| `SlashLabel` | 16 | 44 | 0 |
| `TotalFramesInput` | 72 | 44 | 0 |
| `FpsLabel` | 30 | 44 | 0 |
| `FpsInput` | 60 | 44 | 0 |
| `Divider` | 1 | 32 | 0 |
| `SetKeyButton` | 90 | 44 | 0 |
| `DeleteKeyButton` | 90 | 44 | 0 |
| `CopyButton` | 44 | 44 | 0 |
| `PasteButton` | 44 | 44 | 0 |
| `Spacer` | -1 | -1 | 1 |
| `RemoveAnimationButton` | 180 | 44 | 0 |

- [ ] **Step 4: Apply label/text content for non-button children**

**SlashLabel** TextMeshProUGUI:
- `m_text`: "/", `m_fontSize`: 13, `m_color`: `{r: 0.7, g: 0.7, b: 0.7, a: 1}`, alignment Center+Middle

**FpsLabel** TextMeshProUGUI:
- `m_text`: "fps", `m_fontSize`: 13, `m_color`: `{r: 0.7, g: 0.7, b: 0.7, a: 1}`, alignment Center+Middle

**Divider** Image:
- `m_Color`: `{r: 0.4, g: 0.4, b: 0.4, a: 1}`

- [ ] **Step 5: Apply button label texts and colors**

For each button's Label (TMP_Text child), set `m_text` and styling:

**SetKeyButton/Label:**
- `m_text`: "+ key", `m_fontSize`: 14, `m_color`: `{r: 1, g: 1, b: 1, a: 1}` (white), alignment Center+Middle
- Parent button Image `m_Color`: `{r: 0.18, g: 0.50, b: 0.95, a: 1}` (primary blue)

**DeleteKeyButton/Label:**
- `m_text`: "− key", `m_fontSize`: 14, `m_color`: `{r: 0.85, g: 0.30, b: 0.30, a: 1}` (red), alignment Center+Middle
- Parent button Image `m_Color`: `{r: 0.20, g: 0.20, b: 0.22, a: 1}` (dark)

**CopyButton/Label:**
- `m_text`: "copy", `m_fontSize`: 12, `m_color`: `{r: 0.9, g: 0.9, b: 0.9, a: 1}`, alignment Center+Middle
- Parent button Image `m_Color`: `{r: 0.20, g: 0.20, b: 0.22, a: 1}`

**PasteButton/Label:**
- `m_text`: "paste", `m_fontSize`: 12, `m_color`: `{r: 0.9, g: 0.9, b: 0.9, a: 1}`, alignment Center+Middle
- Parent button Image `m_Color`: `{r: 0.20, g: 0.20, b: 0.22, a: 1}`

**RemoveAnimationButton/Label:**
- `m_text`: "remove animation", `m_fontSize`: 14, `m_color`: `{r: 0.85, g: 0.30, b: 0.30, a: 1}` (red), alignment Center+Middle
- Parent button Image `m_Color`: `{r: 0.20, g: 0.20, b: 0.22, a: 1}`

Each Button Label's RectTransform: anchor stretch full `(0,0)-(1,1)`, sizeDelta `(0,0)`.

- [ ] **Step 6: Save + close + verify**

```
mcp__unityMCP__manage_prefabs action=save_prefab_stage
mcp__unityMCP__manage_prefabs action=close_prefab_stage
mcp__unityMCP__refresh_unity mode=force scope=all compile=request wait_for_ready=true
mcp__unityMCP__read_console types=["error"]
```

---

## Task 4: TMP_InputField completion (3 inputs)

**Files:**
- Modify: `Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/AnimationModule.prefab`

Each TMP_InputField (`CurrentFrameInput`, `TotalFramesInput`, `FpsInput`) needs a `Text Area` child with `Placeholder` + `Text` grandchildren, plus wiring of `TMP_InputField.textComponent` and `placeholder`.

- [ ] **Step 1: Open prefab stage**

```
mcp__unityMCP__manage_prefabs action=open_prefab_stage prefab_path=Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/AnimationModule.prefab
```

- [ ] **Step 2: For CurrentFrameInput — add Text Area + Placeholder + Text**

Create children using `manage_gameobject`:

```
mcp__unityMCP__manage_gameobject
  action: create
  name: "Text Area"
  parent: "CurrentFrameInput"
  components_to_add: ["RectTransform", "RectMask2D"]
```

Set Text Area RectTransform:
- `m_AnchorMin`: `{x: 0, y: 0}`, `m_AnchorMax`: `{x: 1, y: 1}`, `m_Pivot`: `{x: 0.5, y: 0.5}`
- `m_AnchoredPosition`: `{x: 0, y: 0}`, `m_SizeDelta`: `{x: -16, y: -8}`

Then create Placeholder:

```
mcp__unityMCP__manage_gameobject
  action: create
  name: "Placeholder"
  parent: "Text Area"
  components_to_add: ["RectTransform", "TextMeshProUGUI"]
```

Placeholder RectTransform: anchor stretch full `(0,0)-(1,1)`, sizeDelta `(0,0)`, pivot `(0.5, 0.5)`, anchored `(0,0)`.
Placeholder TextMeshProUGUI:
- `m_text`: "0"
- `m_fontSize`: 14
- `m_color`: `{r: 0.5, g: 0.5, b: 0.5, a: 1}`
- alignment Center+Middle
- `m_RaycastTarget`: false

Then create Text:

```
mcp__unityMCP__manage_gameobject
  action: create
  name: "Text"
  parent: "Text Area"
  components_to_add: ["RectTransform", "TextMeshProUGUI"]
```

Text RectTransform: same as Placeholder (anchor stretch, sizeDelta 0).
Text TextMeshProUGUI:
- `m_text`: "0" (default; code overrides via SetCurrentFrame)
- `m_fontSize`: 14
- `m_color`: `{r: 0.9, g: 0.9, b: 0.9, a: 1}`
- alignment Center+Middle
- `m_RaycastTarget`: false

- [ ] **Step 3: Wire TMP_InputField refs for CurrentFrameInput**

Use `manage_components action=set_property` on the TMP_InputField component:

- `m_TextComponent`: reference to the Text child (by instanceID or path within prefab stage)
- `m_Placeholder`: reference to the Placeholder child
- `m_ContentType`: 2 (Integer)
- `m_LineType`: 0 (SingleLine)
- `m_Text`: "0"

The InputField root's Image background:
- `m_Color`: `{r: 0.18, g: 0.18, b: 0.20, a: 1}`

- [ ] **Step 4: Repeat for TotalFramesInput**

Same children structure (Text Area → Placeholder + Text). Same RectTransform/TMP values. Same TMP_InputField wiring.

Use `manage_gameobject action=create` 3 times (Text Area, Placeholder, Text) with `parent="TotalFramesInput"`.

- [ ] **Step 5: Repeat for FpsInput**

Same structure. `parent="FpsInput"`.

- [ ] **Step 6: Save + close + verify**

```
mcp__unityMCP__manage_prefabs action=save_prefab_stage
mcp__unityMCP__manage_prefabs action=close_prefab_stage
mcp__unityMCP__refresh_unity mode=force scope=all compile=request wait_for_ready=true
mcp__unityMCP__read_console types=["error"]
```

---

## Task 5: Body HLG + TracksColumn (header + scroll + viewport + content + ScrollRect refs)

**Files:**
- Modify: `Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/AnimationModule.prefab`

- [ ] **Step 1: Open prefab stage**

```
mcp__unityMCP__manage_prefabs action=open_prefab_stage prefab_path=Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/AnimationModule.prefab
```

- [ ] **Step 2: Apply Body HLG**

Target: `Body`.

HorizontalLayoutGroup (existing):
- `m_Padding`: 0/0/0/0
- `m_Spacing`: 0
- `m_ChildAlignment`: 0 (UpperLeft)
- `m_ChildControlWidth`: true
- `m_ChildControlHeight`: true
- `m_ChildForceExpandWidth`: false
- `m_ChildForceExpandHeight`: true

- [ ] **Step 3: LayoutElement on TracksColumn + TimelineColumn**

**TracksColumn:**
- Add LayoutElement if missing
- `m_PreferredWidth`: 220
- `m_FlexibleWidth`: 0
- `m_MinWidth`: 220

**TimelineColumn:**
- Add LayoutElement if missing
- `m_FlexibleWidth`: 1
- `m_PreferredWidth`: -1
- `m_MinWidth`: 0

- [ ] **Step 4: TracksColumn VLG**

VerticalLayoutGroup (existing):
- `m_Padding`: 0/0/0/0, `m_Spacing`: 0
- `m_ChildAlignment`: 0 (UpperLeft)
- `m_ChildControlWidth`: true, `m_ChildControlHeight`: false
- `m_ChildForceExpandWidth`: true, `m_ChildForceExpandHeight`: false

- [ ] **Step 5: TracksColumnHeader**

LayoutElement (add if missing):
- `m_PreferredHeight`: 36, `m_FlexibleHeight`: 0

Background: this GO needs an Image component if it doesn't have one. Add via `manage_components action=add component_type=Image`. Then:
- Image `m_Color`: `{r: 0.16, g: 0.16, b: 0.18, a: 1}`

TextMeshProUGUI on TracksColumnHeader:
- `m_text`: "objects · bones"
- `m_fontSize`: 13
- `m_color`: `{r: 0.7, g: 0.7, b: 0.7, a: 1}`
- `m_HorizontalAlignment`: 1 (Left)
- `m_VerticalAlignment`: 256 (Middle)
- RectTransform anchor stretch full, but offset left 14 via `m_OffsetMin.x = 14`

To set offsetMin: instead of `m_SizeDelta`, set anchor stretch full + use anchoredPosition adjustment. Simpler: leave anchored at default, set the TMP `m_margin.x = 14` (left padding via text margin):
- `m_margin`: `{x: 14, y: 0, z: 0, w: 0}` (left margin 14px)

- [ ] **Step 6: TracksColumnScroll**

LayoutElement:
- `m_FlexibleHeight`: 1, `m_PreferredHeight`: -1, `m_MinHeight`: 0

ScrollRect properties:
- `m_Horizontal`: false
- `m_Vertical`: true
- `m_MovementType`: 2 (Clamped)
- `m_ScrollSensitivity`: 20
- `m_Inertia`: true

ScrollRect refs (wire in next step after Viewport/Content RT are set).

- [ ] **Step 7: TracksColumnScroll.Viewport**

Viewport RectTransform:
- anchor stretch full, sizeDelta (0, 0), pivot (0.5, 0.5), anchored (0, 0)

Viewport Image (existing):
- `m_Color`: `{r: 1, g: 1, b: 1, a: 0.001}` (effectively invisible but exists for Mask)
- `m_RaycastTarget`: true (needed for Mask drag)

Viewport Mask (existing):
- `m_ShowMaskGraphic`: false

- [ ] **Step 8: TracksColumnContent**

TracksColumnContent RectTransform:
- `m_AnchorMin`: `{x: 0, y: 1}`
- `m_AnchorMax`: `{x: 1, y: 1}`
- `m_Pivot`: `{x: 0.5, y: 1}`
- `m_AnchoredPosition`: `{x: 0, y: 0}`
- `m_SizeDelta`: `{x: 0, y: 0}`

VerticalLayoutGroup (existing):
- `m_Padding`: 0, `m_Spacing`: 0
- `m_ChildControlWidth`: true, `m_ChildControlHeight`: false
- `m_ChildForceExpandWidth`: true, `m_ChildForceExpandHeight`: false

Add ContentSizeFitter:
- `manage_components action=add component_type=ContentSizeFitter target=TracksColumnContent`
- `m_HorizontalFit`: 0 (Unconstrained)
- `m_VerticalFit`: 2 (PreferredSize)

- [ ] **Step 9: Wire ScrollRect refs**

On TracksColumnScroll's ScrollRect:
- `m_Viewport`: reference to the Viewport child (search_method=by_path within stage)
- `m_Content`: reference to the TracksColumnContent grandchild

If MCP's `set_property` cannot resolve in-prefab object references reliably, the subagent must report this as a partial-failure and the user wires manually in Inspector. The fallback applies only here — try first.

- [ ] **Step 10: Save + close + verify**

```
mcp__unityMCP__manage_prefabs action=save_prefab_stage
mcp__unityMCP__manage_prefabs action=close_prefab_stage
mcp__unityMCP__refresh_unity mode=force scope=all compile=request wait_for_ready=true
mcp__unityMCP__read_console types=["error"]
```

---

## Task 6: TimelineColumn + TimelineScroll + Viewport + ScrollRect refs

**Files:**
- Modify: `Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/AnimationModule.prefab`

- [ ] **Step 1: Open prefab stage**

```
mcp__unityMCP__manage_prefabs action=open_prefab_stage prefab_path=Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/AnimationModule.prefab
```

- [ ] **Step 2: TimelineColumn root RectTransform**

Target: `TimelineColumn`.

RectTransform (LayoutElement already set in Task 5):
- `m_AnchorMin`: `{x: 0, y: 0}`, `m_AnchorMax`: `{x: 0, y: 0}` (these will be overridden by HLG parent)
- Actually, when a child is under HorizontalLayoutGroup with `childControlWidth=true`, the anchor doesn't matter much — HLG positions and sizes the child. Leave default `(0, 0)`/`(0, 0)` if not already set, or `(0.5, 0.5)`/`(0.5, 0.5)`. The HLG overrides anyway.

- [ ] **Step 3: TimelineScroll RectTransform**

Target: `TimelineScroll`.

RectTransform:
- `m_AnchorMin`: `{x: 0, y: 0}`, `m_AnchorMax`: `{x: 1, y: 1}`
- `m_Pivot`: `{x: 0.5, y: 0.5}`, `m_AnchoredPosition`: `{x: 0, y: 0}`, `m_SizeDelta`: `{x: 0, y: 0}`

ScrollRect properties:
- `m_Horizontal`: true
- `m_Vertical`: true
- `m_MovementType`: 2 (Clamped)
- `m_ScrollSensitivity`: 30
- `m_Inertia`: false

(TimelineScrollSync wiring already done in Phase 11c per spec.)

- [ ] **Step 4: TimelineScroll.Viewport**

RectTransform: anchor stretch full, sizeDelta 0, pivot (0.5, 0.5), anchored (0, 0).

Image: color `{r: 1, g: 1, b: 1, a: 0.001}`, raycastTarget true.

Mask: showMaskGraphic false.

- [ ] **Step 5: Wire ScrollRect refs for TimelineScroll**

On TimelineScroll's ScrollRect:
- `m_Viewport`: TimelineScroll/Viewport
- `m_Content`: TimelineScroll/Viewport/TimelineContent

If reference assignment fails via MCP, report as partial-failure (user wires in Inspector).

- [ ] **Step 6: Save + close + verify**

```
mcp__unityMCP__manage_prefabs action=save_prefab_stage
mcp__unityMCP__manage_prefabs action=close_prefab_stage
mcp__unityMCP__refresh_unity mode=force scope=all compile=request wait_for_ready=true
mcp__unityMCP__read_console types=["error"]
```

---

## Task 7: TimelineContent + Ruler + LanesContent + Playhead (with sibling order)

**Files:**
- Modify: `Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/AnimationModule.prefab`

- [ ] **Step 1: Open prefab stage**

```
mcp__unityMCP__manage_prefabs action=open_prefab_stage prefab_path=Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/AnimationModule.prefab
```

- [ ] **Step 2: TimelineContent RectTransform**

Target: `TimelineContent`.

RectTransform:
- `m_AnchorMin`: `{x: 0, y: 1}`
- `m_AnchorMax`: `{x: 0, y: 1}`
- `m_Pivot`: `{x: 0, y: 1}`
- `m_AnchoredPosition`: `{x: 0, y: 0}`
- `m_SizeDelta`: `{x: 1830, y: 520}` (initial; AnimatorPanelView.RebuildTimeline overrides x at runtime)

- [ ] **Step 3: LanesContent**

Target: `LanesContent` (child of TimelineContent).

RectTransform:
- `m_AnchorMin`: `{x: 0, y: 1}`
- `m_AnchorMax`: `{x: 1, y: 1}`
- `m_Pivot`: `{x: 0.5, y: 1}`
- `m_AnchoredPosition`: `{x: 0, y: -36}`
- `m_SizeDelta`: `{x: 0, y: -36}` (height = parent.height - 36, fills under ruler)

VerticalLayoutGroup (existing):
- `m_Padding`: 0, `m_Spacing`: 0
- `m_ChildControlWidth`: true, `m_ChildControlHeight`: false
- `m_ChildForceExpandWidth`: true, `m_ChildForceExpandHeight`: false

- [ ] **Step 4: Ruler**

Target: `Ruler` (child of TimelineContent).

RectTransform:
- `m_AnchorMin`: `{x: 0, y: 1}`
- `m_AnchorMax`: `{x: 1, y: 1}`
- `m_Pivot`: `{x: 0.5, y: 1}`
- `m_AnchoredPosition`: `{x: 0, y: 0}`
- `m_SizeDelta`: `{x: 0, y: 36}`

Add Image component if missing:
- `manage_components action=add component_type=Image target=Ruler` (if not present)
- Image `m_Color`: `{r: 0.16, g: 0.16, b: 0.18, a: 1}`

RulerContent RectTransform:
- anchor stretch full `(0,0)-(1,1)`, sizeDelta (0,0), pivot (0.5, 0.5)

- [ ] **Step 5: Playhead**

Target: `Playhead` (child of TimelineContent).

RectTransform:
- `m_AnchorMin`: `{x: 0, y: 0}`
- `m_AnchorMax`: `{x: 0, y: 1}`
- `m_Pivot`: `{x: 0.5, y: 1}`
- `m_AnchoredPosition`: `{x: 0, y: 0}`
- `m_SizeDelta`: `{x: 20, y: 0}` (width 20, height = parent through stretch on Y)

- [ ] **Step 6: Playhead/Line**

Target: `Playhead/Line`.

RectTransform:
- `m_AnchorMin`: `{x: 0.5, y: 0}`
- `m_AnchorMax`: `{x: 0.5, y: 1}`
- `m_Pivot`: `{x: 0.5, y: 0.5}`
- `m_AnchoredPosition`: `{x: 0, y: 0}`
- `m_SizeDelta`: `{x: 2, y: 0}` (2px wide, full height through stretch)

Image:
- `m_Color`: `{r: 0.85, g: 0.25, b: 0.25, a: 1}`

- [ ] **Step 7: Playhead/FrameLabel**

Target: `Playhead/FrameLabel`.

RectTransform:
- `m_AnchorMin`: `{x: 0.5, y: 1}`
- `m_AnchorMax`: `{x: 0.5, y: 1}`
- `m_Pivot`: `{x: 0.5, y: 0}`
- `m_AnchoredPosition`: `{x: 0, y: 2}`
- `m_SizeDelta`: `{x: 40, y: 24}`

Add Image component if missing (for badge background). Image:
- `m_Color`: `{r: 0.18, g: 0.18, b: 0.20, a: 1}`

TextMeshProUGUI:
- `m_text`: "0"
- `m_fontSize`: 13
- `m_color`: `{r: 0.85, g: 0.25, b: 0.25, a: 1}` (red)
- alignment Center+Middle
- `m_margin`: `{x: 0, y: 0, z: 0, w: 0}`

- [ ] **Step 8: Sibling order in TimelineContent**

Sibling order matters for Canvas z-rendering. Use `manage_gameobject action=modify` with `set_sibling_index` if available, or manipulate transform sibling order via direct YAML editing if MCP doesn't expose it.

Alternative method via MCP: re-parent children in correct order. Use `manage_gameobject action=modify` with parent change (effectively re-attach to same parent at end of children).

Target order under `TimelineContent`:
- Index 0: `LanesContent` (drawn first, bottom layer)
- Index 1: `Ruler` (drawn over lanes)
- Index 2: `Playhead` (top layer)

If MCP can't directly set sibling index: simplest workaround is to call `manage_gameobject action=modify parent=TimelineContent` on each child in the desired order; this typically re-appends to the end. Apply in order LanesContent → Ruler → Playhead so each becomes last (highest index) sequentially.

If that doesn't work either, report as PARTIAL and user fixes order in Hierarchy panel manually.

- [ ] **Step 9: Save + close + verify**

```
mcp__unityMCP__manage_prefabs action=save_prefab_stage
mcp__unityMCP__manage_prefabs action=close_prefab_stage
mcp__unityMCP__refresh_unity mode=force scope=all compile=request wait_for_ready=true
mcp__unityMCP__read_console types=["error"]
```

---

## Task 8: ToolbarBottom HLG + 7 buttons LayoutElements

**Files:**
- Modify: `Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/AnimationModule.prefab`

- [ ] **Step 1: Open prefab stage**

```
mcp__unityMCP__manage_prefabs action=open_prefab_stage prefab_path=Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/AnimationModule.prefab
```

- [ ] **Step 2: ToolbarBottom HLG**

Target: `ToolbarBottom`.

HorizontalLayoutGroup (existing):
- `m_Padding`: `{m_Left: 12, m_Right: 12, m_Top: 8, m_Bottom: 8}`
- `m_Spacing`: 8
- `m_ChildAlignment`: 4 (MiddleCenter)
- `m_ChildControlWidth`: false, `m_ChildControlHeight`: true
- `m_ChildForceExpandWidth`: false, `m_ChildForceExpandHeight`: true

- [ ] **Step 3: LayoutElement + style on each transport button**

For each child, add LayoutElement if missing, then set:

| Child | preferredWidth | preferredHeight | style |
|---|---|---|---|
| `PrevKeyButton` | 52 | 52 | default |
| `PrevFrameButton` | 52 | 52 | default |
| `StartButton` | 52 | 52 | default |
| `PlayPauseButton` | 60 | 52 | primary |
| `EndButton` | 52 | 52 | default |
| `NextFrameButton` | 52 | 52 | default |
| `NextKeyButton` | 52 | 52 | default |

Default button:
- Image `m_Color`: `{r: 0.20, g: 0.20, b: 0.22, a: 1}`

Primary button (PlayPauseButton):
- Image `m_Color`: `{r: 0.18, g: 0.50, b: 0.95, a: 1}`

- [ ] **Step 4: Button label texts**

For each button's Label child (TMP_Text), set anchor stretch full + sizeDelta 0 + alignment Center+Middle.

| Button | Label text | font size | color |
|---|---|---|---|
| `PrevKeyButton/Label` | "<<" | 14 | white |
| `PrevFrameButton/Label` | "<" | 14 | white |
| `StartButton/Label` | "\|<" | 14 | white |
| `PlayPauseButton/Label` | (no Label, has PlayPauseIcon instead — skip) | — | — |
| `EndButton/Label` | ">\|" | 14 | white |
| `NextFrameButton/Label` | ">" | 14 | white |
| `NextKeyButton/Label` | ">>" | 14 | white |

White color: `{r: 0.9, g: 0.9, b: 0.9, a: 1}`.

- [ ] **Step 5: PlayPauseIcon**

Target: `PlayPauseButton/PlayPauseIcon`.

RectTransform:
- `m_AnchorMin`: `{x: 0.5, y: 0.5}`, `m_AnchorMax`: `{x: 0.5, y: 0.5}`
- `m_Pivot`: `{x: 0.5, y: 0.5}`, `m_AnchoredPosition`: `{x: 0, y: 0}`
- `m_SizeDelta`: `{x: 24, y: 24}`

Image:
- `m_Color`: `{r: 1, g: 1, b: 1, a: 1}` (white tint; sprite is null for now)

- [ ] **Step 6: Save + close + verify**

```
mcp__unityMCP__manage_prefabs action=save_prefab_stage
mcp__unityMCP__manage_prefabs action=close_prefab_stage
mcp__unityMCP__refresh_unity mode=force scope=all compile=request wait_for_ready=true
mcp__unityMCP__read_console types=["error"]
```

---

## Task 9: Default activeSelf=false on state-machine GameObjects

**Files:**
- Modify: `Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/AnimationModule.prefab`

- [ ] **Step 1: Open prefab stage**

```
mcp__unityMCP__manage_prefabs action=open_prefab_stage prefab_path=Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/AnimationModule.prefab
```

- [ ] **Step 2: Set EmptyState_NoSelection inactive**

```
mcp__unityMCP__manage_gameobject action=modify target=EmptyState_NoSelection set_active=false
```

- [ ] **Step 3: Set EmptyState_NoContainer inactive**

```
mcp__unityMCP__manage_gameobject action=modify target=EmptyState_NoContainer set_active=false
```

- [ ] **Step 4: Set ActiveState inactive**

```
mcp__unityMCP__manage_gameobject action=modify target=ActiveState set_active=false
```

(Note: `AnimatorPanelView.OnEnable` → `Refresh()` activates the correct state at runtime.)

- [ ] **Step 5: Save + close + verify**

```
mcp__unityMCP__manage_prefabs action=save_prefab_stage
mcp__unityMCP__manage_prefabs action=close_prefab_stage
mcp__unityMCP__refresh_unity mode=force scope=all compile=request wait_for_ready=true
mcp__unityMCP__read_console types=["error"]
```

---

## Task 10: Code touch-up — remove SetHeight call in AnimatorPanelView

**Files:**
- Modify: `Assets/_App/Subsystems/SpatialUi/Scripts/Views/AnimatorPanelView.cs`

- [ ] **Step 1: Locate the call**

Open file. Find the block in `RebuildTimeline()`:

```csharp
if (_playhead != null)
{
    _playhead.SetFrame(_clock.CurrentFrame);
    _playhead.SetHeight((c.Tracks.Count + 1) * 52f);
}
```

- [ ] **Step 2: Edit — remove the SetHeight line**

Replace with:

```csharp
if (_playhead != null)
{
    _playhead.SetFrame(_clock.CurrentFrame);
}
```

`TimelinePlayheadView.SetHeight` method stays in code (unused now but harmless — anchor stretch handles sizing).

- [ ] **Step 3: Refresh + console check**

```
mcp__unityMCP__refresh_unity mode=force scope=all compile=request wait_for_ready=true
mcp__unityMCP__read_console types=["error"]
```

Expected: no errors. `SetHeight` becomes unused — Unity does not warn about unused public methods on MonoBehaviours.

---

## Task 11: Run tests + final verification

**Files:** (no edits)

- [ ] **Step 1: Run Edit-mode tests (AnimationAuthoring)**

```
mcp__unityMCP__run_tests mode=EditMode assembly_names=["Subsystems.AnimationAuthoring.Tests"] include_failed_tests=true
```

Then poll:
```
mcp__unityMCP__get_test_job job_id=<from previous> wait_timeout=60
```

Expected: 73 passed, 0 failed.

- [ ] **Step 2: Read console for any new warnings**

```
mcp__unityMCP__read_console types=["warning", "error"] count=30
```

Note: missing-script warnings on prefab or "TMP_InputField has no textComponent assigned" warnings would indicate Task 4 didn't fully take. If any such warnings appear, flag for user.

- [ ] **Step 3: Open UserPanel.prefab to verify nested layout context**

```
mcp__unityMCP__manage_prefabs action=open_prefab_stage prefab_path=Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/UserPanel.prefab
```

Then close (verification was just to make sure the nested AnimationModule loads without errors):

```
mcp__unityMCP__manage_prefabs action=close_prefab_stage
mcp__unityMCP__read_console types=["error"]
```

Expected: no errors.

- [ ] **Step 4: Report**

Final status to user:
- Tasks 1-10 outcomes
- Any partial-failures (e.g., ScrollRect refs not wired automatically — user must wire in Inspector)
- Console state
- Test results
- Suggested manual smoke test sequence (Inspector toggle of EmptyState/ActiveState, play mode test)

---

## Spec coverage map

| Spec section | Task |
|---|---|
| Root anchor stretch + Image bg | T1 step 2 |
| EmptyState_NoSelection | T1 step 3 |
| EmptyState_NoContainer + VLG + 4 children | T1 steps 4-5 |
| ActiveState VLG split + LayoutElements | T2 |
| ToolbarTop HLG + 12 children LE | T3 steps 2-3 |
| Input/button labels + colors | T3 steps 4-5 |
| TMP_InputField completion | T4 |
| Body HLG + TracksColumn/TimelineColumn LE | T5 steps 2-3 |
| TracksColumn VLG + Header + Scroll | T5 steps 4-6 |
| TracksColumn Viewport + Content + size-fitter | T5 steps 7-8 |
| TracksColumnScroll viewport/content refs | T5 step 9 |
| TimelineColumn + TimelineScroll | T6 |
| TimelineScroll viewport/content refs | T6 step 5 |
| TimelineContent size | T7 step 2 |
| LanesContent layout | T7 step 3 |
| Ruler layout + bg | T7 step 4 |
| Playhead vertical-stretch + Line + FrameLabel | T7 steps 5-7 |
| Sibling order (Lanes, Ruler, Playhead) | T7 step 8 |
| ToolbarBottom HLG + 7 buttons LE | T8 |
| Button labels + PlayPauseIcon | T8 steps 4-5 |
| Default activeSelf=false | T9 |
| Code touch-up: remove SetHeight | T10 |
| Verification: tests + console | T11 |

All acceptance criteria from spec → covered.

---

## Risks / known partial-failure points

1. **ScrollRect.viewport / ScrollRect.content refs (T5 step 9, T6 step 5):** MCP's `set_property` may not resolve in-prefab GameObject references reliably for `m_Viewport`/`m_Content`. If it fails, subagent reports the failure and user wires manually in Inspector (drag Viewport → ScrollRect.Viewport slot, TracksColumnContent → ScrollRect.Content slot, similarly for TimelineScroll).

2. **TMP_InputField.textComponent / placeholder refs (T4 step 3):** Same risk as above for object references. Subagent tries set_property first; if fails, reports and user wires manually.

3. **Sibling order (T7 step 8):** MCP may not support direct sibling index control. The reparent-trick (re-append children in target order) usually works but is brittle. If neither works, subagent reports and user reorders in Hierarchy panel manually.

4. **Image components added at runtime:** Some GameObjects per the spec (Ruler, TracksColumnHeader, FrameLabel) need Image components added. If they were already created without Image in Phase 11c, the `manage_components action=add` should add them. Confirm via console after each Task.

5. **ContentSizeFitter on TracksColumnContent (T5 step 8):** May conflict with ScrollRect.content auto-sizing. If it causes "infinite layout" warnings in console, subagent removes the ContentSizeFitter and relies on VLG-driven heights via dynamic row instantiation.

---

## Implementation order summary

1. T1: Root + EmptyState branches
2. T2: ActiveState VLG split
3. T3: ToolbarTop HLG + children
4. T4: TMP_InputField completion (3 inputs)
5. T5: Body + TracksColumn (incl. ScrollRect refs)
6. T6: TimelineColumn + TimelineScroll (incl. ScrollRect refs)
7. T7: TimelineContent children (Ruler, Lanes, Playhead, sibling order)
8. T8: ToolbarBottom + transport buttons
9. T9: Default activeSelf=false
10. T10: Code touch-up
11. T11: Run tests, verify console, open UserPanel for visual context

End: user does manual play-mode smoke test.
