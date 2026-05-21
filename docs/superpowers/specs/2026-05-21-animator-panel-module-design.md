# AnimatorPanelModule — Design Spec

> Build a brand-new `AnimatorPanelModule.prefab` from scratch via a single C# Editor builder script (no path-based prefab YAML mutations). Previous attempts via `manage_prefabs modify_contents` failed visually — this approach uses Unity's native APIs and exits the path-based limitation.

**Source mockup:** `S:\[01] Files\default\Downloads\animator_panel_vr_mockup_v4.html`

**Code surface preserved:** `AnimatorPanelView`, `AnimatorToolbarView`, `AnimatorTransportView`, `AnimatorEmptyStateView`, `TimelinePlayheadView`, `TimelineRulerView`, `TimelineLanesView`, `TimelineLaneView`, `TimelineInputHandler`, `TimelineScrollSync`, `TrackRowView`, `AnimationAuthoring`, `AnimationClock`, `AnimationClipboard` and `AnimatorPanelConfig` remain unchanged. 73 existing tests must continue to pass.

---

## 1. Architecture

- **Output:** `Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/AnimatorPanelModule.prefab`
- **Builder:** `Assets/_App/Editor/AnimatorPanelModuleBuilder.cs` — single Editor class with `[MenuItem("PromeonLab/Build/Animator Panel Module")]` and a static `BuildAndSave()` method callable from `execute_code`. The script idempotently destroys any pre-existing prefab at the target path, builds the new hierarchy in a temporary scene root, calls `PrefabUtility.SaveAsPrefabAsset(...)`, and cleans up the temp object.
- **Isolation:** Root GameObject carries its own `Canvas` (nested, sorting follows parent), `GraphicRaycaster`, and a fixed-size `RectTransform` (1200×680) — so when instantiated as a child of `UserPanel.prefab` the inner layout cannot be squashed by any parent `LayoutGroup`. Render Mode inherits from the parent root Canvas (which is World-Space on UserPanel).
- **NavBar wiring:** A separate task adds the new module to `UserPanel.prefab._bindings` (EntryId `animator`) and adds a corresponding NavButton + `NavBarConfig` entry. Done in code via the builder's helper method (`WireUserPanel()`).
- **Config asset:** Reuse existing `AnimatorPanelConfig.asset` at `Assets/_App/Subsystems/SpatialUi/Data/AnimatorPanelConfig.asset` (GUID `4b710848b9de3b74b97536367c823ac8`). The `DefaultAnimatorPanelConfig.asset` duplicate in `Subsystems/AnimationAuthoring/Data/` will NOT be touched — both still resolve identically.

## 2. Prefab Root Specification

| Property | Value |
|---|---|
| GameObject name | `AnimatorPanelModule` |
| RectTransform pivot | (0.5, 0.5) |
| RectTransform anchorMin/Max | (0.5, 0.5) both (centered, no stretch) |
| RectTransform sizeDelta | (1200, 680) |
| Components | `Canvas` (nested, plane distance default), `GraphicRaycaster`, `Image` (panel background, color `#1A1A1AFF`), `DetachablePanel` |
| `DetachablePanel.EntryId` | left empty — set by UserPanel.Start() from binding |
| Initial activeSelf | **false** (hidden until navbar opens it) |

Children appear in declared order; declared order matches sibling index at runtime.

## 3. Visual States

The root contains exactly two state subtrees that swap via SetActive:

### 3.1 `EmptyStateRoot` (anchor stretch, sizeDelta 0,0)
Hosts `AnimatorEmptyStateView` component on this GO.

- `NoSelectionPanel` (anchor stretch) — centered VLG with:
  - `Icon` (Image, 48×48, gray) — placeholder icon, sprite TBD by user later
  - `Label` (TMP_Text, 16pt, "Select an object to view its animator")
- `NoContainerPanel` (anchor stretch) — centered VLG with:
  - `Icon` (Image, 48×48, gray)
  - `Label` (TMP_Text, 15pt, "This object has no animation container yet")
  - `AddAnimationButton` (Button, primary blue, 180×52, text "+ add animation")
  - `Hint` (TMP_Text, 12pt, gray, "creates an action container with default 60 frames @ 24 fps")

`AnimatorEmptyStateView` SerializeFields wired: `_noSelectionPanel`, `_noContainerPanel`, `_addAnimationButton`.

### 3.2 `ActiveStateRoot` (anchor stretch, sizeDelta 0,0)
VLG `top→bottom`, padding 0, spacing 0. Contains three children:

#### 3.2.1 `ToolbarTop` — `AnimatorToolbarView`
- `LayoutElement` preferredHeight 60
- `HorizontalLayoutGroup`: padding L/R 14, T/B 10, spacing 8, childAlignment MiddleLeft, child controls width=false, height=false, force expand width=false, height=false
- `Image` background tint matches inner panel (`#0F0F0FFF`)
- Children, in order:
  1. `FrameLabel` — TMP_Text "frame", 13pt, secondary color, LE w=40
  2. `CurrentFrameInput` — TMP_InputField, mono font, 16pt, content-type Integer, 72w×44h
  3. `SlashLabel` — TMP_Text "/", 16pt, LE w=10
  4. `TotalFramesInput` — TMP_InputField, 72w×44h
  5. `FpsLabel` — TMP_Text "fps", 13pt, secondary, LE w=30
  6. `FpsInput` — TMP_InputField, 60w×44h
  7. `Divider` — Image (1×32, border-tertiary color)
  8. `SetKeyButton` — Button primary blue, label "+ key", 80×44
  9. `DeleteKeyButton` — Button danger red, label "− key", 80×44
  10. `CopyButton` — Button neutral, label "copy", 60×44
  11. `PasteButton` — Button neutral, label "paste", 60×44
  12. `Spacer` — RT, LE flexibleWidth=1
  13. `RemoveAnimationButton` — Button danger, label "remove animation", min-w 200, 44h

`AnimatorToolbarView` SerializeFields wired: `_currentFrameInput`, `_totalFramesInput`, `_fpsInput`, `_setKeyButton`, `_deleteKeyButton`, `_copyButton`, `_pasteButton`, `_removeAnimationButton`.

Each TMP_InputField is fully constructed with `TextArea`, `Placeholder`, `Text` children + Image background (per Unity `TMP_DefaultControls.CreateInputField` pattern).

#### 3.2.2 `Body` — `HorizontalLayoutGroup`
- `LayoutElement` flexibleHeight 1
- HLG: padding 0, spacing 0, child controls width=true height=true, force expand both=true

Two children:

**`TracksColumn`** (LE preferredWidth 220, no flex)
- VLG top→bottom, child controls width=true height=false
- Image background `#0F0F0FFF`
- Children:
  - `HeaderRow` — LE h=36, contains background Image (own GameObject) + child `Label` (TMP_Text "objects · bones", 13pt secondary)
  - `TracksScroll` — RectTransform LE flexibleHeight=1, components: `ScrollRect` (horizontal=false, vertical=true), `Image` (transparent)
    - `Viewport` (anchor stretch, `Image` placeholder, `Mask` showMaskGraphic=false)
      - `TracksColumnContent` — RT, anchor (0,1)-(1,1) pivot (0.5,1) sizeDelta (0,0); `VerticalLayoutGroup` (childControls width=true height=false, childForceExpand width=true height=false), `ContentSizeFitter` (verticalFit=PreferredSize)
- ScrollRect references: `viewport` → Viewport, `content` → TracksColumnContent, `verticalScrollbar` left null for now

**`TimelineColumn`** (LE flexibleWidth 1)
- Components: `RectMask2D` (clips ruler/lanes/playhead)
- Children:
  - `TimelineScroll` — anchor stretch (0,0)-(1,1) sizeDelta (0,0); `ScrollRect` (horizontal=true, vertical=true), `Image` transparent
    - `Viewport` (anchor stretch, `Image` transparent, `Mask` showMaskGraphic=false)
      - `TimelineContent` — RT, anchor (0,1)-(0,1) pivot (0,1) sizeDelta initial (1830,520); positioned at (0,0) anchored. Three children IN THIS SIBLING ORDER:
        - `LanesContent` — anchor (0,1)-(1,1) pivot (0.5,1) anchoredPos (0,-36) sizeDelta (0,484); container for lanes spawned by `TimelineLanesView`
        - `Ruler` — anchor (0,1)-(1,1) pivot (0.5,1) anchoredPos (0,0) sizeDelta (0,36); Image bg `#1A1A1AFF`; `TimelineRulerView` component; child `Content` (RT, anchor stretch, sizeDelta 0,0) hosts ticks/labels
        - `Playhead` — anchor (0,0)-(0,1) pivot (0.5,0.5) sizeDelta (20,0); `TimelinePlayheadView` component; children:
          - `Line` (RT, anchor stretch, Image red `#FF3232CC`, width strip 4w sized via anchor offsets L=8 R=8)
          - `FrameLabel` (anchor (0.5,1) pivot (0.5,0)-style badge, TMP_Text 13pt mono, Image background red border) anchoredPos (0,4)

Sibling order: LanesContent(0), Ruler(1), Playhead(2) — lanes under ruler under playhead (playhead on top so its red line is visible above all).

ScrollRect references: `viewport` → Viewport, `content` → TimelineContent.

#### 3.2.3 `ToolbarBottom` — `AnimatorTransportView`
- `LayoutElement` preferredHeight 70
- `HorizontalLayoutGroup`: padding 0, spacing 8, childAlignment MiddleCenter, child controls/force-expand all=false
- `Image` background `#0F0F0FFF`
- Seven Buttons, each 52×52, in order:
  1. `PrevKeyButton`     (label "<<")
  2. `PrevFrameButton`   (label "<")
  3. `StartButton`       (label "|<")
  4. `PlayPauseButton`   (primary blue, contains child `PlayPauseIcon` Image 24×24 sprite=PlaySprite)
  5. `EndButton`         (label ">|")
  6. `NextFrameButton`   (label ">")
  7. `NextKeyButton`     (label ">>")

`AnimatorTransportView` SerializeFields wired: all 7 buttons, plus `_playPauseIcon` → the icon Image; `_playSprite`/`_pauseSprite` left null for now (user assigns later).

## 4. Scroll Sync

Add `TimelineScrollSync` component on `Body` GameObject. SerializeFields:
- `_leftTracks` → `TracksScroll.ScrollRect`
- `_rightTimeline` → `TimelineScroll.ScrollRect`

## 5. Wiring `AnimatorPanelView`

The root GameObject hosts `AnimatorPanelView`. SerializeFields wired by the builder:
- `_config` → `AnimatorPanelConfig.asset` (loaded via AssetDatabase)
- `_timelineContent` → TimelineContent RT
- `_toolbar` → ToolbarTop view
- `_transport` → ToolbarBottom view
- `_emptyState` → EmptyStateRoot view
- `_activeStateRoot` → ActiveStateRoot GO
- `_ruler` → Ruler view
- `_lanes` → TimelineLanesView (placed on `LanesContent` GO — combine root+component)
- `_playhead` → Playhead view
- `_timelineInput` → `TimelineInputHandler` placed on `LanesContent` (it needs RT of LanesContent + listens to pointer events). `_content` ref of input handler → LanesContent RT itself
- `_tracksColumnContent` → TracksColumnContent RT
- `_trackRowPrefab` → `Assets/_App/Subsystems/SpatialUi/Prefabs/Items/TrackRow.prefab` (loaded via AssetDatabase)

For `TimelineRulerView`:
- `_content` → Ruler/Content RT
- `_tickPrefab` → `TimelineTick.prefab`
- `_labelPrefab` → `TimelineTickLabel.prefab`
- `_config` → AnimatorPanelConfig

For `TimelineLanesView` (on LanesContent):
- `_root` → LanesContent RT itself
- `_lanePrefab` → `TimelineLane.prefab`

For `TimelinePlayheadView`:
- `_root` → Playhead RT
- `_frameLabel` → Playhead/FrameLabel TMP_Text
- `_config` → AnimatorPanelConfig

For `TimelineInputHandler` (on LanesContent):
- `_content` → LanesContent RT
- `_config` → AnimatorPanelConfig

## 6. UserPanel Integration

Builder also has `WireUserPanel()` which:
1. Loads `UserPanel.prefab` via `PrefabUtility.LoadPrefabContents`
2. Searches for child named `Modules` (parent of all module instances) — falls back to UserPanel root if not found
3. If a child `AnimatorPanelModule` instance already exists, removes it
4. Instantiates `AnimatorPanelModule.prefab` as child, sets activeSelf=false
5. Locates `_bindings` SerializedObject of UserPanel via `SerializedProperty`, appends `{ EntryId="animator", NavButton=<existing or null>, Panel=<the instance> }`
6. Saves `PrefabUtility.SaveAsPrefabAsset` back
7. Loads `NavBarConfig.asset`, appends entry `(id="animator", ExclusiveGroup="modules", visibleInModes=[VrEditing, Sandbox])` if not present

NavButton creation is **out of scope** for this builder — user adds it to the navbar manually because navbar styling is panel-specific. Builder leaves `NavButton` ref null in the binding; the panel can still be toggled programmatically.

## 7. Sub-Agent Split

| Subagent | Files touched | Description |
|---|---|---|
| **A** | Create `Assets/_App/Editor/AnimatorPanelModuleBuilder.cs` | Write builder Editor class. Define `BuildAndSave()`, `BuildRoot()`, `BuildEmptyState()`, `BuildToolbarTop()`, `BuildBody()`, `BuildToolbarBottom()`, helper `CreateUiElement()`. |
| **B** | None new — runs builder | Run `BuildAndSave()` via `mcp__unityMCP__execute_code`. Verify via `read_console` (no errors), `manage_prefabs read_contents` (root structure exists, 12 children of ToolbarTop, 7 transport buttons, etc.). |
| **C** | Modify `UserPanel.prefab` | Call `WireUserPanel()` via `execute_code`. Verify instance appears in UserPanel hierarchy and binding entry exists. |
| **D** | None — runtime check | `manage_editor action=play_mode`, `read_console` for runtime errors, exit play mode. |

## 8. Verification Checklist

After Subagent D finishes:
- [ ] No console errors at edit-time and play-time
- [ ] 73/73 tests still pass (`Subsystems.AnimationAuthoring.Tests` + others)
- [ ] Prefab `AnimatorPanelModule.prefab` exists with expected hierarchy
- [ ] Nested Canvas on root prevents parent VLG/HLG from affecting layout (verified by parenting under a temp VLG in test scene)
- [ ] All `[SerializeField]` references on every `*View` component are non-null when inspecting prefab
- [ ] UserPanel binding includes new `animator` entry
- [ ] Empty state shows by default when prefab is first activated without selection

## 9. Out of Scope

- Pause/Play sprites — left null, user assigns later
- NavButton icon and styling — added manually to navbar
- IconFont icons (ti-plus, ti-trash etc.) — using text fallback labels in this iteration
- Sub-bone hierarchy (track tree indentation) — `TrackRow.prefab` already supports indent via existing implementation

## 10. Risks

| Risk | Mitigation |
|---|---|
| `execute_code` token output exceeds limit when running builder | Builder logs only summary lines; verbose path data only on errors. |
| Prefab GUID drift breaks UserPanel binding | Builder uses `AssetDatabase.LoadAssetAtPath` by path, not GUID. UserPanel binding stores GameObject ref to instance, not asset. |
| Nested Canvas causes raycast failures | GraphicRaycaster on root + standard raycaster on UserPanel parent root — confirmed safe per Unity docs. |
| TMP+Image conflict | All TMP_Text/Image lives on separate GameObjects per Unity Graphic singleton rule. Builder enforces this. |
| TMP font asset missing | Use `TMP_Settings.defaultFontAsset` everywhere, never explicit asset ref. |
