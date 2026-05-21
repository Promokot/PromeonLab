# Animator Panel Layout Fix — Design Spec

**Дата:** 2026-05-21
**Статус:** Draft — pending user review
**Скоуп:** RectTransform anchors/sizes/LayoutGroup config внутри `AnimationModule.prefab` (вложенного в `UserPanel.prefab`). Code touch-up: 1 строка в `AnimatorPanelView.RebuildTimeline`.

---

## Goal

Превратить нынешний `AnimationModule.prefab` (структура верна, но layout «разъехавшийся») в визуально работающую панель, повторяющую `docs/developer-notes/animator_panel_vr_mockup_v4.html`.

**Принцип:** хирархия и SerializeFields НЕ трогаются (42 поля уже привязаны). Меняются только:
- RectTransform anchor/sizeDelta/pivot/anchoredPosition на каждом GameObject
- LayoutGroup конфиг (padding, spacing, childControl/Force)
- LayoutElement (preferredWidth/Height, flexibleWidth/Height) на тех детях, что управляются LayoutGroup'ом
- ScrollRect viewport/content refs (не были привязаны программно)
- TMP_InputField completion (добавить Text + Placeholder children + textComponent/placeholder refs + contentType=Integer)
- Default `activeSelf` для EmptyState/ActiveState

**Контекст работы:** subagent открывает `AnimationModule.prefab` напрямую в его собственном Prefab Stage. Все правки идут в основной prefab asset — никаких override-headaches с UserPanel. Поскольку UserPanel содержит nested prefab reference (не клон), все изменения в `AnimationModule.prefab` автоматически отражаются на UserPanel-instance при следующем открытии. GUID `AnimationModule.prefab` сохраняется. После сохранения subagent открывает `UserPanel.prefab` чтобы verify визуальный контекст (это удовлетворяет «работаешь внутри UserPanel» по смыслу).

---

## Architecture

**Изменяется:**
- `Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/AnimationModule.prefab` — RectTransform/Layout values на 59 GameObjects
- `Assets/_App/Subsystems/SpatialUi/Scripts/Views/AnimatorPanelView.cs` — 1-line removal (SetHeight call)

**НЕ изменяется:**
- AnimationAuthoring и подсистема (full API + tests pass)
- UserPanel.prefab само (только overrides на nested AnimationModule)
- Leaf prefabs (TimelineTick/Label/Diamond/Lane/TrackRow) — структурно ок
- Outliner prefabs
- AnimatorPanelConfig.asset

**Tech:** Unity 6000.3.7f1 UGUI (RectTransform, Image, TMP, ScrollRect, LayoutGroups), via Unity MCP tools.

---

## Глобальные принципы layout

1. **Root anchor stretch fill parent.** Везде где элемент должен занимать всё доступное место родителя — `anchorMin (0, 0)`, `anchorMax (1, 1)`, `sizeDelta (0, 0)`, `anchoredPosition (0, 0)`.

2. **LayoutElement определяет размер** в детях LayoutGroup. Когда `HorizontalLayoutGroup.childControlWidth = false`, использует `LayoutElement.preferredWidth`. Аналогично для height.

3. **Pivot и anchor для top-down контента (Ruler, Lanes, Playhead):** anchor top-left, pivot `(0, 1)` или `(0.5, 1)` — координаты Y отсчитываются вниз от верха.

4. **ScrollRect's Viewport:** anchor stretch full, Image + Mask с `showMaskGraphic = false`.

5. **ScrollRect.content:** anchor top-left или top-stretch, pivot top (Y=1) — содержимое растёт вниз.

6. **Default activeSelf:** state-machine GO ставим в `false` (UI код активирует).

---

## RectTransform spec — пожизненно по каждому GameObject

Format: `path | anchorMin | anchorMax | pivot | anchoredPos | sizeDelta | notes`

### Root

```
AnimationModule | (0,0) | (1,1) | (0.5,0.5) | (0,0) | (0,0) |
  Image color rgba(0.12, 0.12, 0.14, 1)
  CanvasGroup alpha 1, interactable true, blocksRaycasts true
```

### EmptyState_NoSelection branch

```
EmptyState_NoSelection | (0,0) | (1,1) | (0.5,0.5) | (0,0) | (0,0) | activeSelf=false
  HintText | (0,0) | (1,1) | (0.5,0.5) | (0,0) | (0,0)
    TMP_Text "select an object to animate", fontSize 14, alignment Center+Middle
    color rgba(0.7, 0.7, 0.7, 1)
```

### EmptyState_NoContainer branch

```
EmptyState_NoContainer | (0,0) | (1,1) | (0.5,0.5) | (0,0) | (0,0) | activeSelf=false
  VerticalLayoutGroup: padding 40/40/40/40, spacing 16,
                       childAlignment MiddleCenter,
                       childControlWidth=true, childControlHeight=true,
                       childForceExpandWidth=false, childForceExpandHeight=false
  Icon | n/a (LayoutGroup-managed) | LayoutElement: preferredWidth=48, preferredHeight=48
    Image color rgba(0.5, 0.5, 0.5, 1)
  HintText | n/a | LayoutElement: preferredHeight=24, preferredWidth=400
    TMP_Text "this object has no animation container yet", fontSize 15, color rgba(0.7, 0.7, 0.7, 1), align Center+Middle
  AddAnimationButton | n/a | LayoutElement: preferredWidth=220, preferredHeight=52
    Image background rgba(0.18, 0.50, 0.95, 1)
    Button (transition ColorTint defaults)
    Label child | (0,0) | (1,1) | (0.5,0.5) | (0,0) | (0,0)
      TMP_Text "+ Add animation", fontSize 15, color white, align Center+Middle
  HintSubtext | n/a | LayoutElement: preferredHeight=18, preferredWidth=400
    TMP_Text "creates an action container with default 60 frames @ 24 fps",
              fontSize 12, color rgba(0.5, 0.5, 0.5, 1), align Center+Middle
```

### ActiveState

```
ActiveState | (0,0) | (1,1) | (0.5,0.5) | (0,0) | (0,0) | activeSelf=false
  VerticalLayoutGroup: padding 0, spacing 0,
                       childAlignment UpperLeft,
                       childControlWidth=true, childControlHeight=false,
                       childForceExpandWidth=true, childForceExpandHeight=false
```

#### ToolbarTop (child 0)

```
ToolbarTop | n/a | LayoutElement: preferredHeight=50, flexibleHeight=0
  HorizontalLayoutGroup: padding 10/14/8/8, spacing 12,
                         childAlignment MiddleLeft,
                         childControlWidth=false, childControlHeight=true,
                         childForceExpandWidth=false, childForceExpandHeight=true
  Children (in order):
    CurrentFrameInput  | LayoutElement: preferredWidth=72, preferredHeight=44
    SlashLabel         | LayoutElement: preferredWidth=16, preferredHeight=44
    TotalFramesInput   | LayoutElement: preferredWidth=72, preferredHeight=44
    FpsLabel           | LayoutElement: preferredWidth=30, preferredHeight=44
    FpsInput           | LayoutElement: preferredWidth=60, preferredHeight=44
    Divider            | LayoutElement: preferredWidth=1,  preferredHeight=32
    SetKeyButton       | LayoutElement: preferredWidth=90, preferredHeight=44
    DeleteKeyButton    | LayoutElement: preferredWidth=90, preferredHeight=44
    CopyButton         | LayoutElement: preferredWidth=44, preferredHeight=44
    PasteButton        | LayoutElement: preferredWidth=44, preferredHeight=44
    Spacer             | LayoutElement: flexibleWidth=1
    RemoveAnimationButton | LayoutElement: preferredWidth=180, preferredHeight=44
```

`SlashLabel` content: TMP_Text "/", fontSize 13, color text-secondary, align Center+Middle.
`FpsLabel`: TMP_Text "fps", fontSize 13.
`Divider`: Image color rgba(0.4, 0.4, 0.4, 1).

#### Input field internals (CurrentFrameInput / TotalFramesInput / FpsInput)

Каждый InputField root:
- RectTransform: pos/size управляются LayoutElement выше
- Image background: rgba(0.18, 0.18, 0.20, 1), border via Image alpha-edge или nested Image
- TMP_InputField:
  - `textComponent` → Text child
  - `placeholder` → Placeholder child
  - `contentType` = Integer
  - `lineType` = SingleLine
  - `text` = initial value ("0" or per-context)
  - `caretWidth` = 1, `customCaretColor` = false

Children of each InputField:
```
Text Area | (0,0) | (1,1) | (0.5,0.5) | (0,0) | (-16, -8) | RectMask2D
  Placeholder | (0,0) | (1,1) | (0.5,0.5) | (0,0) | (0,0)
    TMP_Text "", fontSize 14, color rgba(0.5,0.5,0.5,1), align Center+Middle, raycastTarget=false
  Text         | (0,0) | (1,1) | (0.5,0.5) | (0,0) | (0,0)
    TMP_Text "", fontSize 14, color rgba(0.9,0.9,0.9,1), align Center+Middle, raycastTarget=false
```

(sizeDelta `(-16, -8)` на TextArea = padding 8/8/4/4 относительно InputField rect)

#### Button style spec

Default button (CopyButton, PasteButton, transport buttons):
- Image background rgba(0.20, 0.20, 0.22, 1)
- Button.transition = ColorTint, normalColor white, highlightedColor rgba(0.30, 0.30, 0.34, 1)
- Label child anchor stretch full, TMP_Text color rgba(0.9,0.9,0.9,1), align Center+Middle, fontSize 14

Primary button (SetKeyButton, PlayPauseButton, AddAnimationButton):
- Image background rgba(0.18, 0.50, 0.95, 1)
- Label color white

Danger button (DeleteKeyButton, RemoveAnimationButton):
- Image background rgba(0.20, 0.20, 0.22, 1) (как default)
- Border tint: можно через nested Image, либо просто
- Label color rgba(0.85, 0.30, 0.30, 1) red

#### Body (child 1 of ActiveState)

```
Body | n/a | LayoutElement: flexibleHeight=1
  HorizontalLayoutGroup: padding 0, spacing 0,
                         childAlignment UpperLeft,
                         childControlWidth=true, childControlHeight=true,
                         childForceExpandWidth=false, childForceExpandHeight=true
```

##### TracksColumn (child 0 of Body)

```
TracksColumn | n/a | LayoutElement: preferredWidth=220, flexibleWidth=0
  VerticalLayoutGroup: padding 0, spacing 0,
                       childControlWidth=true, childControlHeight=false,
                       childForceExpandWidth=true, childForceExpandHeight=false
  Background Image: optional border-right (rgba 0.3,0.3,0.3,1, width 0.5px) — пропускаем для простоты, либо через дочерний Image
```

###### TracksColumnHeader (child 0)

```
TracksColumnHeader | n/a | LayoutElement: preferredHeight=36, flexibleHeight=0
  Image background rgba(0.16, 0.16, 0.18, 1)
  TMP_Text "objects · bones", fontSize 13, color rgba(0.7,0.7,0.7,1),
           align Left+Middle, padding left 14
```

(Padding left реализуется либо через дочерний RectTransform с offset, либо через text margins. Простейшее: anchor stretch с offsetMin.x = 14.)

###### TracksColumnScroll (child 1)

```
TracksColumnScroll | n/a | LayoutElement: flexibleHeight=1
  ScrollRect: horizontal=false, vertical=true, movementType=Clamped,
              viewport=Viewport, content=TracksColumnContent,
              scrollSensitivity=20
  Viewport | (0,0) | (1,1) | (0.5,0.5) | (0,0) | (0,0)
    Image white alpha 0.001 (нужен для Mask), Mask showMaskGraphic=false
  TracksColumnContent | (0,1) | (1,1) | (0.5,1) | (0,0) | (0, 0)
    VerticalLayoutGroup: padding 0, spacing 0,
                         childControlWidth=true, childControlHeight=false,
                         childForceExpandWidth=true, childForceExpandHeight=false
    ContentSizeFitter: horizontalFit=Unconstrained, verticalFit=PreferredSize
```

ScrollRect.viewport → Viewport (object ref).
ScrollRect.content → TracksColumnContent (object ref).

(`Viewport` живёт в иерархии как дочерний `TracksColumnScroll/Viewport`; `TracksColumnContent` — `TracksColumnScroll/Viewport/TracksColumnContent`.)

##### TimelineColumn (child 1 of Body)

```
TimelineColumn | n/a | LayoutElement: flexibleWidth=1
  (только RectTransform, без layout components)
```

###### TimelineScroll (child 0)

```
TimelineScroll | (0,0) | (1,1) | (0.5,0.5) | (0,0) | (0,0)
  ScrollRect: horizontal=true, vertical=true, movementType=Clamped,
              inertia=false, scrollSensitivity=30,
              viewport=Viewport, content=TimelineContent
  TimelineScrollSync: _leftTracks=TracksColumnScroll, _rightTimeline=self (already wired)
  Viewport | (0,0) | (1,1) | (0.5,0.5) | (0,0) | (0,0)
    Image white alpha 0.001, Mask showMaskGraphic=false
  TimelineContent | (0,1) | (0,1) | (0,1) | (0,0) | (1830, 520) initial
    (sizeDelta устанавливается кодом — AnimatorPanelView.RebuildTimeline)
    TimelineInputHandler (existing wiring intact)
```

###### TimelineContent children

```
LanesContent | (0,1) | (1,1) | (0.5,1) | (0,-36) | (0, -36)
  (вертикальный stretch внутри content, начинается с Y=-36 — под ruler)
  VerticalLayoutGroup: padding 0, spacing 0,
                       childControlWidth=true, childControlHeight=false,
                       childForceExpandWidth=true, childForceExpandHeight=false

Ruler | (0,1) | (1,1) | (0.5,1) | (0,0) | (0, 36)
  Image background rgba(0.16, 0.16, 0.18, 1)
  TimelineRulerView (existing wiring)
  RulerContent | (0,0) | (1,1) | (0.5,0.5) | (0,0) | (0,0)
    (RulerView вставляет ticks/labels пулом)

Playhead | (0,0) | (0,1) | (0.5,1) | (0,0) | (20, 0)
  (Vertical stretch — высота parent, ширина 20px;
   X из кода = currentFrame * FRAME_PX)
  TimelinePlayheadView (existing wiring)
  Children:
    Line | (0.5,0) | (0.5,1) | (0.5,0.5) | (0,0) | (2, 0)
      Image rgba(0.85, 0.25, 0.25, 1) red
    FrameLabel | (0.5,1) | (0.5,1) | (0.5,0) | (0,2) | (40, 24)
      Image background rgba(0.18, 0.18, 0.20, 1) + Image border red
      TMP_Text "0", fontSize 13, monospace, color red, align Center+Middle
```

**Sibling order внутри TimelineContent (важно для z-order):**

| Index | Child |
|---|---|
| 0 | `LanesContent` |
| 1 | `Ruler` |
| 2 | `Playhead` |

(Render порядок: Lanes снизу, Ruler сверху lanes, Playhead на самом верху.)

#### ToolbarBottom (child 2 of ActiveState)

```
ToolbarBottom | n/a | LayoutElement: preferredHeight=52, flexibleHeight=0
  HorizontalLayoutGroup: padding 12/12/8/8, spacing 8,
                         childAlignment MiddleCenter,
                         childControlWidth=false, childControlHeight=true,
                         childForceExpandWidth=false, childForceExpandHeight=true
  Children (in order):
    PrevKeyButton    | LayoutElement: preferredWidth=52, preferredHeight=52 | default style
    PrevFrameButton  | LayoutElement: preferredWidth=52, preferredHeight=52 | default style
    StartButton      | LayoutElement: preferredWidth=52, preferredHeight=52 | default style
    PlayPauseButton  | LayoutElement: preferredWidth=60, preferredHeight=52 | primary style
    EndButton        | LayoutElement: preferredWidth=52, preferredHeight=52 | default style
    NextFrameButton  | LayoutElement: preferredWidth=52, preferredHeight=52 | default style
    NextKeyButton    | LayoutElement: preferredWidth=52, preferredHeight=52 | default style
```

Каждая кнопка имеет `Label` (TMP_Text) child anchor stretch full. PlayPauseButton имеет дополнительно `PlayPauseIcon` (Image, sizeDelta 24×24, center anchor) — sprite swap делает `AnimatorTransportView.SetPlaying`.

Text labels (placeholder, пока sprites не привязаны):
- PrevKeyButton: "<<"
- PrevFrameButton: "<"
- StartButton: "|<"
- PlayPauseButton: ">" (или иконка)
- EndButton: ">|"
- NextFrameButton: ">"
- NextKeyButton: ">>"

(Можно заменить на Unicode-стрелки ⏮ ◀ ⏪ ▶ ⏩ ▶ ⏭ если шрифт поддерживает.)

---

## Code touch-up (1 строка)

В `Assets/_App/Subsystems/SpatialUi/Scripts/Views/AnimatorPanelView.cs`, метод `RebuildTimeline()`:

Найти:
```csharp
if (_playhead != null)
{
    _playhead.SetFrame(_clock.CurrentFrame);
    _playhead.SetHeight((c.Tracks.Count + 1) * 52f);
}
```

Заменить на:
```csharp
if (_playhead != null)
{
    _playhead.SetFrame(_clock.CurrentFrame);
}
```

(Удаление `SetHeight` — playhead теперь vertical-stretch anchor, размер ставит anchor.)

`TimelinePlayheadView.SetHeight` method можно оставить в коде (он не вреден) — мы только убираем вызов.

---

## Workflow execution (для subagent)

1. Open prefab stage on `AnimationModule.prefab` (NOT UserPanel — see Architecture note).
2. Navigate by name within the AnimationModule hierarchy.
3. For each GameObject (use the spec above for path-by-path), set:
   - RectTransform anchorMin/Max/pivot/anchoredPos/sizeDelta via `manage_components action=set_property` or `manage_gameobject action=modify component_properties=...`
   - LayoutGroup props for parents that have them
   - LayoutElement (add if not present, set values) on children that are managed by parent LayoutGroup
4. Wire ScrollRect.viewport and ScrollRect.content (this was missed in Phase 11c).
5. For each `*Input` GameObject (CurrentFrameInput, TotalFramesInput, FpsInput): add `Text Area > Placeholder + Text` children, wire `TMP_InputField.textComponent` and `.placeholder` and `.contentType = Integer`.
6. Set default `activeSelf = false` on `EmptyState_NoSelection`, `EmptyState_NoContainer`, `ActiveState`.
7. Save prefab stage (`mcp__unityMCP__manage_prefabs action=save_prefab_stage`).
8. Close prefab stage (`mcp__unityMCP__manage_prefabs action=close_prefab_stage`).
9. Refresh Unity + read_console — no errors.
10. Apply code touch-up: remove `SetHeight` line in AnimatorPanelView.cs.
11. (Optional verify) Open `UserPanel.prefab` in stage to visually confirm nested AnimationModule layout sits correctly within UserPanel context.

---

## Verification

### Automated (subagent)
- `mcp__unityMCP__refresh_unity` after every batch
- `mcp__unityMCP__read_console types=["error"]` → must be empty
- `mcp__unityMCP__run_tests assembly_names=["Subsystems.AnimationAuthoring.Tests"]` → still 73/73 pass (no regressions)

### Manual (user-side smoke test)
1. Open Unity Editor.
2. Open `UserPanel.prefab` in Prefab Stage.
3. Toggle `AnimationModule.ActiveState.SetActive(true)` in Inspector — visual sanity check:
   - ToolbarTop sits at top 50px height, with input fields + buttons in row, RemoveAnimation right-aligned
   - Body fills middle: 220px TracksColumn (white area) + flex TimelineColumn (with horizontal scroll for content)
   - ToolbarBottom 52px tall, transport buttons centered
4. Toggle `EmptyState_NoContainer.SetActive(true)` — visual:
   - Centered "+ Add animation" button visible
   - Hint text above/below
5. Toggle `EmptyState_NoSelection.SetActive(true)` — visual:
   - Just centered "select an object to animate" text

### Play mode (user-side)
1. Bootstrap → MainMenu → open scene with rig.
2. Click Animator nav button — UserPanel detaches it; panel should look correct at floating size.
3. Select object → empty container state with Add button.
4. Add animation → ActiveState appears with proper layout.
5. Set Key — diamond appears at integer frame, under playhead.
6. Change Total Frames input to 30 — TimelineContent width shrinks accordingly.

---

## Acceptance criteria

- [ ] Root: anchor stretch fill parent, Image background dark
- [ ] EmptyState_NoSelection: anchor stretch, inactive by default, "select an object" text centered
- [ ] EmptyState_NoContainer: anchor stretch, inactive by default, VLG with icon + text + button + subtext
- [ ] ActiveState: anchor stretch, inactive by default, VLG splits into toolbar-50 / body-flex / toolbar-52
- [ ] ToolbarTop: HLG row, 12 children, RemoveAnimation right-aligned via Spacer flex
- [ ] All `*Input` fields have Text + Placeholder children + correct TMP_InputField refs
- [ ] Body: HLG, 220px TracksColumn + flex TimelineColumn
- [ ] TracksColumn: VLG header (36) + Scroll (flex), ScrollRect viewport+content wired
- [ ] TimelineColumn: ScrollRect viewport+content wired, both axes enabled
- [ ] TimelineContent: anchor top-left, pivot top-left, initial sizeDelta (1830, 520)
- [ ] Ruler: top-stretch, height 36, sibling index 1
- [ ] LanesContent: top-stretch under ruler, VLG, sibling index 0
- [ ] Playhead: vertical-stretch anchor, X-driven by code, sibling index 2, Line + FrameLabel children
- [ ] ToolbarBottom: HLG row centered, 7 transport buttons
- [ ] AnimatorPanelView.SetHeight call removed
- [ ] Refresh: compile clean
- [ ] Tests: 73/73 still pass
- [ ] Manual visual check: ToolbarTop, Body, ToolbarBottom proportions correct
- [ ] Play test: state transitions work, layout doesn't break in any state

---

## Out of scope

- Visual polish (icons, fonts, color tweaks) beyond what's in the table
- Refactor of Layout group spacing values (we pick reasonable defaults; user adjusts in Inspector)
- ScrollRect scrollbars (we go without them — viewport masking is enough)
- Play/Pause sprite assignment on AnimatorTransportView (out of layout scope)
- TrackRow.prefab or TimelineLane.prefab internal layout (those are external pool prefabs)

---

## File structure

### Create
- (none)

### Modify
- `Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/AnimationModule.prefab` (heavy — все RectTransform/LayoutGroup/LayoutElement)
- `Assets/_App/Subsystems/SpatialUi/Scripts/Views/AnimatorPanelView.cs` (1-line removal: `_playhead.SetHeight(...)` call in `RebuildTimeline`)

### Delete
- (none)

---

## Risks / open questions

1. **MCP ScrollRect viewport/content assignment** — Subagent Phase 11c уже отметил что эти 2 поля не были привязаны программно (это `Object`-ссылки на in-prefab GameObjects). Если `manage_components set_property` не сможет их установить (Unity validates type via SerializedProperty.objectReferenceValue), fall back: user wires them manually в Inspector после применения layout.

2. **`ContentSizeFitter` на TracksColumnContent** — может конфликтовать с anchor stretch parent ScrollRect.content. Альтернатива: убрать ContentSizeFitter, делать layout-driven height + AutoLayout. Решение: пробуем с ContentSizeFitter, если ScrollRect ругается на recursive layout, убираем и опираемся только на VLG.

3. **TMP_InputField sub-component creation via MCP** — Unity's `TMP_DefaultControls.CreateInputField` creates the proper hierarchy with all components. Через MCP это нужно делать вручную (create Text Area, Placeholder, Text + wire refs). Если subagent не сможет — fall back: user в Inspector right-click → UI → Input Field - TextMeshPro и переносит rect-настройки.

4. **(Resolved by Architecture note)** Изменения идут напрямую в `AnimationModule.prefab` (не как overrides на UserPanel). UserPanel-instance автоматически подхватывает изменения через nested reference.

---

## Implementation order

1. Open `AnimationModule.prefab` в prefab stage
2. Root anchor + image
3. EmptyState_NoSelection layout
4. EmptyState_NoContainer layout (VLG + children)
5. ActiveState layout (VLG split)
6. ToolbarTop (HLG + 12 children + LayoutElement)
7. TMP_InputField completion (3 inputs × add Text+Placeholder)
8. Body (HLG split)
9. TracksColumn + Header + Scroll + Viewport + Content
10. TimelineColumn + Scroll + Viewport
11. TimelineContent + Ruler + LanesContent + Playhead
12. ScrollRect.viewport/content refs (both Scrolls)
13. Playhead Line + FrameLabel layout
14. ToolbarBottom (HLG + 7 buttons + LayoutElement)
15. Set defaults activeSelf=false на EmptyState/ActiveState
16. Save prefab
17. Refresh + read_console
18. Run tests (AnimationAuthoring) — должны быть 73/73 still pass
19. Code touch-up в AnimatorPanelView.cs: remove SetHeight call
20. Refresh + read_console final
21. Open UserPanel.prefab в stage чтобы verify visual context
22. Hand off для manual play-mode smoke test
