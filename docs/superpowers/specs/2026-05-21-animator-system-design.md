# Animator System v2 — Design Spec

**Дата:** 2026-05-21
**Статус:** Draft — pending user review
**Скоуп:** Animation authoring data model, AnimatorPanel UI, Outliner rig/object differentiation, Bones-mode event wiring

---

## Goal

Переработать систему анимации (data model + AnimatorPanel UI + связку с outliner и bones-mode) так, чтобы:

- Каждый объект сцены имеет свой ActionContainer с собственными TotalFrames/FPS
- Таймлайн рисуется с фиксированным шагом в пикселях на кадр, проматывается, не растягивается под ширину окна
- Ползунок (playhead) визуально выровнен с ключами, проходит вертикально через все дорожки, snap к кадрам
- Дорожки нестятся: для рига — root + lazy bone tracks, переключающиеся по bones-mode
- Outliner различает rig vs object префабом, рига становится синей в bones-mode
- Длина анимации и FPS редактируются input-полями, длина может уменьшаться (drop keys за пределами)
- Empty states обрабатываются: "no selection" и "no animation container" (с Add animation)
- Copy/Paste frame через clipboard сервис

---

## Architecture

Изменения локализованы в трёх подсистемах: `AnimationAuthoring`, `SpatialUi`, `RigBuilder`. Связь между ними — через MessagePipe события на SceneLifetimeScope. Никаких прямых cross-subsystem вызовов.

**Tech stack:** Unity 6000.3.7f1, VContainer DI, MessagePipe events, UGUI ScrollRect, JsonUtility persistence, schemaVersion=2 для миграции.

---

## Data Model

### `SceneAnimationData` (v2)

```csharp
[Serializable]
public class SceneAnimationData
{
    public int                    schemaVersion = 2;
    public List<ActionContainer>  Containers    = new();

    public ActionContainer FindByOwner(string ownerNodeId);
    public ActionContainer CreateContainer(string ownerNodeId, int totalFrames = 60, int fps = 24);
    public void            RemoveContainer(string ownerNodeId);
}
```

### `ActionContainer` (новый)

```csharp
[Serializable]
public class ActionContainer
{
    public string             OwnerNodeId;
    public int                Fps         = 24;
    public int                TotalFrames = 60;
    public List<AnimTrackData> Tracks     = new();

    public AnimTrackData FindTrack(string nodeId);
    public AnimTrackData GetOrCreateTrack(string nodeId);    // lazy
    public bool          HasAnyKeyAtFrame(int frame);
    public IReadOnlyList<string> ExistingTrackNodeIds();
    public void          TruncateToTotalFrames();            // drop keys with Frame > TotalFrames
}
```

### `AnimTrackData` / `AnimKeyData` — без изменений

Сохраняем существующий формат `(NodeId, Keys[])` и `(Frame, Pos, Rot, Scale)`.

### `FrameClipboard` (новый POCO)

```csharp
public class FrameClipboard
{
    public string  OwnerNodeId;      // diag/source container
    public int     SourceFrame;
    public List<FrameClipboardEntry> Entries;
}

[Serializable]
public struct FrameClipboardEntry
{
    public string      TrackNodeId;
    public Vector3     Position;
    public Quaternion  Rotation;
    public Vector3     Scale;
}
```

### Migration (schemaVersion 1 → 2)

Per user decision: миграция не выполняется. При `LoadAsync`:

- если `schemaVersion < 2` → `Debug.LogWarning` с путём и старой версией, **`File.Delete(path)`** (физическое удаление файла), создаётся пустой `SceneAnimationData { schemaVersion = 2 }` в памяти. Следующий Save (на первой мутации) создаст новый v2 файл.
- если `schemaVersion == 2` → загрузка как есть
- если `schemaVersion > 2` → `Debug.LogError`, сцена открывается с пустым in-memory data, файл **не трогается** (forward-compat защита от случайной перезаписи новой схемы старым кодом)

Logic живёт в `AnimationAuthoring.LoadAsync`, не в отдельном Migrator.

---

## AnimationAuthoring API

```csharp
public class AnimationAuthoring : IStartable, IDisposable
{
    // === Container CRUD ===
    public bool             HasContainer(string ownerNodeId);
    public ActionContainer  GetContainer(string ownerNodeId);
    public ActionContainer  CreateContainer(string ownerNodeId);   // defaults 60/24
    public void             RemoveContainer(string ownerNodeId);

    // === Length / FPS ===
    public void  SetTotalFrames(string ownerNodeId, int frames);   // drops keys beyond
    public void  SetFps        (string ownerNodeId, int fps);

    // === Per-track keys (low-level) ===
    public void  SetKey   (string nodeId, int frame);
    public void  DeleteKey(string nodeId, int frame);
    public bool  HasKey   (string nodeId, int frame);
    public IReadOnlyList<int> GetKeyFrames(string nodeId);

    // === Whole-frame operations ===
    public void            SetKeyForFrame      (string ownerNodeId, string activeNodeId, int frame);
    public void            DeleteAllKeysAtFrame(string ownerNodeId, int frame);
    public FrameClipboard  CopyFrame           (string ownerNodeId, int frame);
    public void            PasteFrame          (string ownerNodeId, int frame, FrameClipboard clip);

    // === Navigation helpers ===
    public int?  NearestKeyBefore(string ownerNodeId, int frame);
    public int?  NearestKeyAfter (string ownerNodeId, int frame);

    // === Helpers ===
    public string OwnerOf(string nodeId);  // bone: prefix → parent rig id; else nodeId
}
```

### Семантика `SetKeyForFrame`

**Параметры:** `ownerNodeId` (container), `activeNodeId` (текущая selection), `frame`.

**Поведение:**

1. Если активная нода (`activeNodeId`) не имеет трека в container — lazy create.
2. Для всех треков container (включая только что созданный) — захват `localPosition`/`localRotation`/`localScale` соответствующей ноды + UpsertKey на `frame`.

Дорожки, которые ни разу не получали ключи И не являются активными — не трогаются (нет «мусорных» треков). Удалённые ноды (GO null) — skip с warning.

### Семантика `DeleteAllKeysAtFrame`

Для всех существующих треков container → `RemoveKey(frame)`. Если трек становится пустым — удаляется из container.

### Семантика `CopyFrame` / `PasteFrame`

`CopyFrame`: захватывает текущее значение всех существующих треков container на frame (или интерполированное, если ключа нет) — упрощение: берём только реальные ключи на frame, треки без ключа на этом frame не попадают в snapshot. **Если ключей нет совсем — clipboard остаётся пустым, копирование игнорируется.**

`PasteFrame`: для каждой entry в snapshot → `track = GetOrCreateTrack(entry.TrackNodeId)` → `UpsertKey(frame, entry.Pos, entry.Rot, entry.Scale)`. Lazy-create треков допустим, потому что эти треки уже существовали в исходном frame.

### TotalFrames change (drop)

Если новое `TotalFrames < currentMaxKeyFrame`:
1. Все keys с `Frame > newTotal` удаляются.
2. Save.
3. Publish `AnimationContainerChangedEvent.LengthChanged`.

Undo не предусмотрен в этой версии (отдельный Command — out of scope; пользователь должен быть аккуратен).

### FPS change

Просто перезаписать `container.Fps`. Frame-индексы не пересчитываются (frame — целочисленный, FPS — только playback-time).

---

## AnimationClock — per-container configurable

Текущий `AnimationClock` — singleton с фиксированными `TotalFrames=120` / `Fps=30`. Расширяем:

```csharp
public class AnimationClock : ITickable
{
    public int  CurrentFrame { get; private set; }
    public int  TotalFrames  { get; private set; } = 60;
    public int  Fps          { get; private set; } = 24;
    public bool IsPlaying    { get; private set; }

    public void Configure(int totalFrames, int fps);   // НОВЫЙ
    public void Play(); public void Pause(); public void Stop();
    public void Seek(int frame);
}
```

`AnimationClock` остаётся в RootLifetimeScope как singleton. Конфигурируется через `Configure(...)` со стороны `AnimatorPanelView` при смене активного container'а (по `SelectionChangedEvent` / `AnimationContainerChangedEvent`).

Если активного container нет → `Configure(60, 24)` (defaults).

При `Configure` если `CurrentFrame > newTotal` → clamp к `newTotal`, publish `FrameChangedEvent`.

---

## Events

### Новые

```csharp
public struct AnimationContainerChangedEvent
{
    public string           OwnerNodeId;
    public ContainerChange  Change;
}

public enum ContainerChange { Added, Removed, LengthChanged, FpsChanged }

public struct BonesVisibilityChangedEvent
{
    public string  RigNodeId;
    public bool    Visible;
}
```

### Расширенные

```csharp
public struct AnimationKeyframeChangedEvent
{
    public string  NodeId;
    public string  OwnerNodeId;
    public int     Frame;
    public KeyframeChange Change;
}

public enum KeyframeChange { Added, Removed, Overwritten }
```

### Без изменений

`FrameChangedEvent`, `PlaybackStateChangedEvent`, `SelectionChangedEvent`, `SceneModifiedEvent`, `NodeRenamedEvent`, `SceneOpenedEvent`.

### Publication points

- `AnimationContainerChangedEvent` — публикует `AnimationAuthoring` при Create/Remove/SetTotalFrames/SetFps.
- `BonesVisibilityChangedEvent` — публикует `SceneInspectorView.OnShowBonesToggleChanged` (использует уже известный `rigNodeId`).
- `AnimationKeyframeChangedEvent` — `AnimationAuthoring` при SetKey/DeleteKey/PasteFrame/SetKeyForFrame.

---

## AnimatorPanel UI

### Prefab structure

```
AnimatorPanel (RectTransform + AnimatorPanelView)
├── EmptyState_NoSelection
│   └── Text "select an object to animate"
├── EmptyState_NoContainer
│   ├── Icon (movie)
│   ├── Text "this object has no animation container yet"
│   ├── Button "Add animation"
│   └── Hint "creates an action container with default 60 frames @ 24 fps"
└── ActiveState
    ├── ToolbarTop (AnimatorToolbarView)
    │   ├── Frame input (current)
    │   ├── "/"
    │   ├── Total frames input
    │   ├── "fps" + FPS input
    │   ├── Divider
    │   ├── [+ key] button
    │   ├── [- key] button
    │   ├── [copy] button
    │   ├── [paste] button
    │   └── [remove animation] button (right-aligned)
    ├── Body
    │   ├── TracksColumn (width=220, fixed)
    │   │   ├── TracksColumnHeader ("objects" / "objects · bones")
    │   │   └── TracksColumnScroll (ScrollRect, vertical only)
    │   │       └── TracksColumnContent
    │   │           └── TrackRow[]
    │   └── TimelineColumn (flex)
    │       └── TimelineScroll (ScrollRect, horizontal + vertical)
    │           └── TimelineContent (width = TotalFrames * FRAME_PX + FRAME_PX)
    │               ├── Ruler (top, 36px) — TimelineRulerView
    │               ├── LanesContent (vertical stack of TrackLane)
    │               └── Playhead — TimelinePlayheadView (overlay, top→bottom)
    └── ToolbarBottom (AnimatorTransportView)
        ├── [<<key] prev key
        ├── [<frame] prev frame
        ├── [|<] go to start
        ├── [▶/❚❚] play/pause (primary style when playing)
        ├── [>|] go to end
        ├── [frame>] next frame
        └── [key>>] next key
```

### Component breakdown (one type per file)

| Class | Responsibility |
|---|---|
| `AnimatorPanelView` | State machine (NoSelection/NoContainer/Active); subscribes to events; owns refs to subviews |
| `AnimatorToolbarView` | Top toolbar (input fields + key actions + remove anim) |
| `AnimatorTransportView` | Bottom toolbar (transport buttons) |
| `AnimatorEmptyStateView` | Handles both empty placeholders, owns `Add animation` button |
| `TimelineRulerView` | Ticks + labels (pool); rebuild on TotalFrames change |
| `TimelineLanesView` | Lanes (one per track); rebuild on Tracks change; pools lanes |
| `TimelineLaneView` | Per-lane: grid lines + key markers (pool); active highlight |
| `TimelinePlayheadView` | Position playhead by `currentFrame * FRAME_PX`; height = full content; label shows frame |
| `TimelineInputHandler` | XR ray pointer-down on ruler/lanes → `frame = round(localX / FRAME_PX)` → `clock.Seek` |
| `TrackRowView` | One row in TracksColumn; active highlight; icon (object/rig/bone); blue-color for rig in bones-mode |
| `TimelineScrollSync` | Mirrors vertical scroll Y between TracksColumnScroll and TimelineScroll |
| `AnimationClipboard` | Service (RootScope); holds current FrameClipboard; thread-safe |
| `AnimatorPanelConfig` | ScriptableObject — FRAME_PX, MAJOR_TICK_INTERVAL, defaults, colors |

### `AnimatorPanelConfig`

```csharp
[CreateAssetMenu(menuName="VrAnimApp/Animator Panel Config")]
public class AnimatorPanelConfig : ScriptableObject
{
    public float FRAME_PX             = 30f;
    public int   MAJOR_TICK_INTERVAL  = 5;
    public int   DEFAULT_TOTAL_FRAMES = 60;
    public int   DEFAULT_FPS          = 24;
    public Color KeyColor_Object;
    public Color KeyColor_Rig;
    public Color KeyColor_Bone;
    public Color KeyColor_Selected;
    public Color TrackRow_Active;
    public Color TrackRow_Inactive;
    public Color RigRow_BonesOn;     // blue
    public Color RigRow_BonesOff;
}
```

### State machine — `AnimatorPanelView`

| State | Condition | Visible UI |
|---|---|---|
| NoSelection | `SelectionManager.SelectedNodeId == null` | `EmptyState_NoSelection` |
| NoContainer | selected, `authoring.HasContainer(owner) == false` | `EmptyState_NoContainer` |
| Active | selected, container exists | `ActiveState` |

Where `owner = authoring.OwnerOf(selectedId)`.

Transitions:
- `SelectionChangedEvent` → recompute state
- `AnimationContainerChangedEvent.Added` matching current selection's owner → → Active
- `AnimationContainerChangedEvent.Removed` matching current owner → → NoContainer
- `AnimationContainerChangedEvent.LengthChanged` / `FpsChanged` → если `event.OwnerNodeId == currentOwner` → rebuild ruler + lanes + `clock.Configure(container.TotalFrames, container.Fps)`. Иначе — игнор (другой контейнер).

### Timeline rendering

**Coordinate system:** all positions in `TimelineContent` local space.

- `TimelineContent.sizeDelta.x = (totalFrames + 1) * FRAME_PX` (extra frame для последнего tick)
- Tick `i`: `anchoredPosition.x = i * FRAME_PX`
- Major tick if `i % MAJOR_TICK_INTERVAL == 0`
- Tick label: shown only on major; format `i.ToString()`
- Lane `j`: vertically stacked, height=52px, anchored top-left
- Grid line in lane: every `MAJOR_TICK_INTERVAL` frames, opacity 0.7 (major) or 0.35
- Key marker `k`: `anchoredPosition = (k.Frame * FRAME_PX, 0)`, rotated 45°, size 22×22 (selected: 26×26)
- Playhead: anchored top, `anchoredPosition.x = currentFrame * FRAME_PX`, height = full content height (stretch via anchor); width 20px; pivot center-top; triangle indicator on top using nested image

### Snap

Pointer interaction в `TimelineInputHandler`:
- subscribe `XRRayInteractor.selectEntered` / `selectExited` events на `TimelineContent`'s collider/raycast region
- on hover-and-drag: convert world pointer → local point in TimelineContent
- `frame = Mathf.RoundToInt(localPoint.x / FRAME_PX)`, clamp `[0, container.TotalFrames]`
- `clock.Seek(frame)`

Snap-by-design: dragging produces только целые frame значения, никаких subframes.

### Pools

`List<T>` pool как в существующем `AnimationModule._markerPool` (SetActive(false) для лишних). Apply for: ticks, key markers per lane, lanes themselves, track rows.

### Vertical scroll synchronization

`TimelineScrollSync` — отдельный MonoBehaviour, ссылается на ScrollRect двух колонок:
```csharp
public class TimelineScrollSync : MonoBehaviour
{
    [SerializeField] private ScrollRect _leftTracks;
    [SerializeField] private ScrollRect _rightTimeline;
    private bool _syncing;
    // OnEnable: subscribe both onValueChanged
    // Mirror Y, guard with _syncing flag
}
```

---

## Bones-mode → Outliner visual

### Publication

`SceneInspectorView.OnShowBonesToggleChanged(bool value)` уже знает `rigNodeId`. После `rig.SetBonesInteractive(value)` дополнительно:

```csharp
_bus.Publish(new BonesVisibilityChangedEvent { RigNodeId = rigNodeId, Visible = value });
```

### Subscription (Outliner)

`SceneOutlinerView`:
- хранит `Dictionary<string, bool> _bonesActiveByRig`
- subscribe `BonesVisibilityChangedEvent` в `OnEnable`, unsubscribe в `OnDisable`
- on event → update dict → find `OutlinerItem` with matching NodeId → call `RigOutlinerItem.SetBonesMode(visible)`
- on Rebuild → восстанавливает состояние из dict (важно, чтобы переключение не сбрасывалось при rebuild)

### Subscription (AnimatorPanel)

`AnimatorPanelView` подписывается на `BonesVisibilityChangedEvent` тоже:
- on event matching current container's owner → no UI change directly, но это сигнал к review селекции (если current selection — бывшая «активная кость», и теперь bones hidden → инспектор уже перебросит selection на rig, мы реагируем через `SelectionChangedEvent`).

---

## Outliner — two prefabs

### Current state

Один префаб `OutlinerObject-Object_ItemUI.prefab` + `OutlinerObject-Rig_ItemUI.prefab` уже существуют, но `SceneOutlinerView` использует ОДИН префаб и переключает иконки внутри `OutlinerItem`.

### New approach

- `OutlinerItem` (базовый) — без знания о bones; работает для object префаба.
- `RigOutlinerItem` (наследник OutlinerItem) — добавляет `SetBonesMode(bool)` который меняет цвет фона/иконки строки.

```csharp
public class RigOutlinerItem : OutlinerItem
{
    [SerializeField] private Image _backgroundImage;
    [SerializeField] private Image _iconImage;

    public void SetBonesMode(bool active)
    {
        _backgroundImage.color = active ? config.RigRow_BonesOn : config.RigRow_BonesOff;
        _iconImage.color = active ? config.RigRow_BonesOn : config.RigRow_BonesOff;
    }
}
```

`SceneOutlinerView`:

```csharp
[SerializeField] private OutlinerItem    _objectRowPrefab;
[SerializeField] private RigOutlinerItem _rigRowPrefab;

private void AddRowsRecursive(...)
{
    var isRig = node.GetComponentInChildren<PromeonProxyRigBuilder>(true) != null;
    OutlinerItem row = isRig
        ? Instantiate(_rigRowPrefab, _rowsRoot)
        : Instantiate(_objectRowPrefab, _rowsRoot);
    row.Bind(node, depth * _indentPx, () => _selection.Select(node.NodeId));

    if (row is RigOutlinerItem rigRow
        && _bonesActiveByRig.TryGetValue(node.NodeId, out var bonesOn))
    {
        rigRow.SetBonesMode(bonesOn);
    }
}
```

Старая логика `_iconObject`/`_iconRig` внутри `OutlinerItem` удаляется (теперь у каждого префаба свой статичный icon).

---

## Persistence

- Каждая мутация (`SetKey`, `DeleteKey`, `CreateContainer`, `RemoveContainer`, `SetTotalFrames`, `SetFps`, `SetKeyForFrame`, `DeleteAllKeysAtFrame`, `PasteFrame`) триггерит `RequestSave()`.
- `RequestSave()` — debounce 200ms: cancel previous timer, start new. По окончании — `SaveAsync(CancellationToken)`.
- Любая `SetKey` (и derived) дополнительно публикует `SceneModifiedEvent` для UnsavedChangesGuard (если оно работает с animation.json — see existing pattern).

JsonUtility serializes `SceneAnimationData` целиком (PathProvider.AnimationJson(sceneId)).

---

## VContainer registration

`SceneLifetimeScope.Configure(...)`:
- `AnimationAuthoring` (уже зарегистрирован)

`RootLifetimeScope.Configure(...)`:
- `AnimationClipboard` — singleton, не привязан к сцене

`AnimatorPanelConfig` — `[SerializeField]` на `AnimatorPanelView` (через инспектор привязывается SO-ассет). НЕ регистрируется в DI.

Все view-классы — UI компоненты на префабе AnimatorPanel, инжектятся через `[Inject]` `Construct(...)` как сейчас в `AnimationModule`.

---

## Out of scope (явно)

- `AnimationPlayback` (sampling/ApplyFrame во время Play) — остаётся в текущем виде. Per-container TotalFrames не ломает: `AnimationAuthoring.ApplyFrame` уже игнорирует пустые/несуществующие треки.
- `ExportPipeline` — без изменений.
- Pose save (TODO из VKR-документа) — без изменений.
- Undo для `SetTotalFrames`/`SetFps`/`Remove Animation` — нет. Только базовое предупреждение в логе при drop.
- Sandbox mode interactions — поведение наследуется от обычного scene mode без специальных правок (`Sandbox` использует тот же AppStorage container).
- Старые animation.json — discard at load time (без миграции).

---

## Testing strategy

### Edit-mode tests (must)

- `SceneAnimationDataTests`
  - FindByOwner returns null/existing
  - CreateContainer applies defaults
  - RemoveContainer removes by owner
- `ActionContainerTests`
  - GetOrCreateTrack returns same instance for same NodeId
  - TruncateToTotalFrames drops keys with Frame > TotalFrames; trim does not touch ≤
  - HasAnyKeyAtFrame
- `AnimationAuthoringTests`
  - OwnerOf("bone:R:n") == "R"; OwnerOf("regular") == "regular"
  - SetKey lazy-creates track in container
  - SetKeyForFrame: writes to active node lazy-create + all existing tracks
  - DeleteAllKeysAtFrame: removes from all existing tracks, drops empty tracks
  - CopyFrame returns empty entries when no keys at frame
  - PasteFrame: restores keys, creates missing tracks
  - NearestKeyBefore/After: edge cases (empty, single, exactly-on, before-all, after-all)
  - SetTotalFrames drops keys beyond newTotal
  - SetFps doesn't change Frame indices
- `AnimationClipboardTests`
  - Set → Get returns same; survives multiple selection changes
- `AnimationClockTests` (extend existing)
  - Configure changes TotalFrames/Fps; CurrentFrame clamps if > newTotal
  - publishes FrameChangedEvent on clamp

### Play-mode tests (optional / smoke)

- Не требуются для логики; UI/integration ручной smoke-test после сборки префаба.

### Migration test

- `AnimationAuthoringTests.LoadAsync_v1_file_is_deleted_and_empty_v2_created`

---

## Open questions / risks

1. **TrackRow / TimelineLane row sync** — если число дорожек большое (10+ костей), vertical pooling необходим в обоих скроллах, и их высоты должны совпадать byte-by-byte. Решается фиксированной высотой 52px и единым `LanesContent` LayoutGroup с identical settings.
2. **XR pointer hit-testing на TimelineContent** — нужно убедиться, что `RaycastTarget = true` на правильных слоях. Может потребоваться невидимый Image сверху lanes для ловли клика. Решение: добавить `TimelineInputCatcher` — раздёргатый прозрачный Image на весь TimelineContent.
3. **bones-mode и SelectionChanged race** — когда юзер выключает bones mode и selected была кость, инспектор переключает selection на rig. AnimatorPanel реагирует на оба события (`BonesVisibilityChangedEvent` + `SelectionChangedEvent`) — обработчик в `AnimatorPanelView` должен быть идемпотентным.
4. **AnimationClock global vs per-container** — пока кратное переключение clock через Configure при смене selection. Если в будущем добавится "play all containers simultaneously", потребуется multi-clock или master timeline.

---

## Acceptance criteria

- [ ] Можно создать новый контейнер кнопкой Add animation, удалить через Remove animation
- [ ] Total frames input меняет длину таймлайна, ключи за пределами удаляются
- [ ] FPS input меняет скорость воспроизведения (frame indices не двигаются)
- [ ] Playhead визуально выровнен с ключами (под маркерами кадров)
- [ ] Playhead проходит вертикально через все дорожки сверху донизу
- [ ] Таймлайн скроллится горизонтально с фиксированным FRAME_PX, не сжимается под ширину окна
- [ ] Snap при перетаскивании playhead'а к ближайшему кадру
- [ ] Set Key пишет ключ под playhead в active track + все существующие треки контейнера
- [ ] Delete Key удаляет ключи на текущем кадре во всех существующих треках
- [ ] Copy/Paste Frame работает (буфер сохраняется между сменами selection)
- [ ] Prev/Next key/frame работают
- [ ] Empty state с Add animation показывается при выделении объекта без container'а
- [ ] No-selection placeholder показывается, когда ничего не выделено
- [ ] У рига отдельный outliner-row префаб; rig-row становится blue при включении bones-mode
- [ ] Старые animation.json v1 удаляются с warning при открытии сцены
- [ ] Все Edit-mode tests проходят

---

## File structure

### Create

- `Assets/_App/_Shared/Events/AnimationContainerChangedEvent.cs`
- `Assets/_App/_Shared/Events/BonesVisibilityChangedEvent.cs`
- `Assets/_App/_Shared/Events/KeyframeChange.cs` (enum)
- `Assets/_App/_Shared/Events/ContainerChange.cs` (enum)
- `Assets/_App/Subsystems/AnimationAuthoring/Data/ActionContainer.cs`
- `Assets/_App/Subsystems/AnimationAuthoring/Data/FrameClipboard.cs`
- `Assets/_App/Subsystems/AnimationAuthoring/Data/FrameClipboardEntry.cs`
- `Assets/_App/Subsystems/AnimationAuthoring/AnimationClipboard.cs`
- `Assets/_App/Subsystems/SpatialUi/Scripts/Views/AnimatorPanelView.cs`
- `Assets/_App/Subsystems/SpatialUi/Scripts/Views/AnimatorToolbarView.cs`
- `Assets/_App/Subsystems/SpatialUi/Scripts/Views/AnimatorTransportView.cs`
- `Assets/_App/Subsystems/SpatialUi/Scripts/Views/AnimatorEmptyStateView.cs`
- `Assets/_App/Subsystems/SpatialUi/Scripts/Elements/TimelineRulerView.cs`
- `Assets/_App/Subsystems/SpatialUi/Scripts/Elements/TimelineLanesView.cs`
- `Assets/_App/Subsystems/SpatialUi/Scripts/Elements/TimelineLaneView.cs`
- `Assets/_App/Subsystems/SpatialUi/Scripts/Elements/TimelinePlayheadView.cs`
- `Assets/_App/Subsystems/SpatialUi/Scripts/Elements/TimelineInputHandler.cs`
- `Assets/_App/Subsystems/SpatialUi/Scripts/Elements/TimelineScrollSync.cs`
- `Assets/_App/Subsystems/SpatialUi/Scripts/Elements/TrackRowView.cs`
- `Assets/_App/Subsystems/SpatialUi/Scripts/Elements/RigOutlinerItem.cs`
- `Assets/_App/Subsystems/SpatialUi/Data/AnimatorPanelConfig.cs`
- `Assets/_App/Subsystems/AnimationAuthoring/Tests/ActionContainerTests.cs`
- `Assets/_App/Subsystems/AnimationAuthoring/Tests/AnimationAuthoringTests.cs` (новые case'ы)
- `Assets/_App/Subsystems/AnimationAuthoring/Tests/AnimationClipboardTests.cs`

### Modify

- `Assets/_App/Subsystems/AnimationAuthoring/Data/SceneAnimationData.cs` — переработать на `List<ActionContainer>`
- `Assets/_App/Subsystems/AnimationAuthoring/AnimationAuthoring.cs` — новый API
- `Assets/_App/Subsystems/AnimationAuthoring/AnimationClock.cs` — `Configure` method
- `Assets/_App/Subsystems/AnimationAuthoring/UI/AnimationModule.cs` — **удалить** (заменяется `AnimatorPanelView`); все prefab-ссылки на этот компонент пересоздаются вручную в фазе manual prefab work
- `Assets/_App/Subsystems/SpatialUi/Scripts/Views/SceneInspectorView.cs` — publish `BonesVisibilityChangedEvent`
- `Assets/_App/Subsystems/SpatialUi/Scripts/Views/SceneOutlinerView.cs` — two-prefab logic + bones-state dict
- `Assets/_App/Subsystems/SpatialUi/Scripts/Elements/OutlinerItem.cs` — удалить SerializeField'ы `_iconObject`/`_iconRig` и runtime swap; класс остаётся базовым (используется для object-префаба и наследуется `RigOutlinerItem`); метод `Bind(...)` упрощается (не определяет тип ноды)
- `Assets/_App/_Shared/Events/AnimationKeyframeChangedEvent.cs` — добавить `OwnerNodeId`, `Frame`, `Change`
- `Assets/_App/Bootstrap/RootLifetimeScope.cs` — register `AnimationClipboard`

### Delete

- (none — старые prefab keep)

### Prefab work (user-side, manual)

- `AnimatorPanel.prefab` — full restructure под mockup v4
- `OutlinerObject-Rig_ItemUI.prefab` — добавить `RigOutlinerItem` component, иконку статически (без runtime swap)
- `OutlinerObject-Object_ItemUI.prefab` — оставить иконку как есть, на root `OutlinerItem`

---

## Implementation order (high level)

1. Data layer: SceneAnimationData v2 + ActionContainer + FrameClipboard + tests
2. AnimationAuthoring API rewrite + tests + migration-discard
3. AnimationClock.Configure + tests
4. AnimationClipboard service + tests
5. Events: new + extended
6. AnimatorPanelView state machine (без UI пока)
7. Timeline UI components (ruler, lanes, playhead, input, sync)
8. Toolbar views (top/bottom)
9. Empty states
10. Bones-mode event wiring (SceneInspectorView publish + SceneOutlinerView subscribe)
11. Outliner two-prefab logic
12. Bootstrap registration
13. Manual prefab work (user-side)
14. Smoke test

Детальный пошаговый план — в следующей фазе через `writing-plans` skill.
