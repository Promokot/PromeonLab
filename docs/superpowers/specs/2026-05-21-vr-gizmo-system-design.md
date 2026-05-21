# VR 3D Gizmo System — Design

**Дата:** 2026-05-21
**Статус:** Draft, ждёт user-review перед переходом к implementation plan
**Связано:** `Assets/Resources/Prefabs/Gizmos/Vr3D_Gizmos.prefab`, `Subsystems/VrInteraction/`, `Subsystems/SceneComposition/SelectionManager.cs`

---

## 1. Цели и не-цели

### Цели

- Дать пользователю манипулировать выделенным объектом через 3D-гизмос (Move / Rotate / Scale) с **жёсткими axis-constraint** (стрелка X двигает только по X, ring Y крутит только вокруг Y, и т.д.).
- Гизмос **подгоняется под bounding box** выделенного объекта.
- Гизмос **сосуществует** с прямой манипуляцией через `XRPromeonInteractable` (но во время видимости гизмоса collider target отключён).
- Каждая операция — **атомарный commit** через `CommandStack` (rollback одним undo).
- Hard-cleanup: `SelectionManager` приводится к **single-select API** (multi-select вырезается).
- Phase 1: **input swap** в `XRPromeonInteractable` — tap trigger = select, hold trigger = rotate, hold grip = move.

### Не-цели

- Box-select / lasso-select.
- Snap to grid (можно добавить позже).
- Local↔World axis toggle (фиксируем Local).
- Гизмос для нескольких выделенных объектов (multi-select вырезается).
- Анимация появления/исчезновения гизмоса.

---

## 2. UX-модель

### Видимость гизмоса

Гизмос виден ⟺ **`GizmoToolsPanel` открыта** И **есть выделенный объект**.

- В `UserPanel` — кнопка **«Gizmo Tools»** (toggle). Открывает/закрывает sub-panel `GizmoToolsPanel`.
- `GizmoToolsPanel` содержит 3 кнопки: **Move / Rotate / Scale**. Без кнопки Off — закрытие самой sub-panel = выключение.
- Открытие sub-panel: default mode = **Move**.
- Перевыбор объекта при открытой sub-panel → гизмос пересоздаётся на новом target, mode сохраняется.
- Закрытие sub-panel → гизмос исчезает.

### Оси и pivot

- **Local axes** — стрелки и кольца следуют за rotation выделенного объекта.
- Pivot — `target.position` (origin transform'а).
- Snapshot осей делается на момент `BeginDrag` каждого handle'а (защита от self-reference в rotate).

### Хват handle'а

- **Hold grip** на handle (стрелке / кольце / кубе) = drag.
- **Release grip** = commit через `CommandStack`.
- Trigger на handle игнорируется.

### Прямая манипуляция (Phase 1 — input swap в XRPromeonInteractable)

- **Tap trigger (<0.5s)** = select (как сейчас).
- **Hold trigger (>0.5s)** = rotate (был move).
- **Hold grip** = move (был rotate).

---

## 3. Архитектура

### Subsystem ownership

Вся gizmo-логика живёт в `Subsystems/VrInteraction/Gizmo/`.

Текущий файл `Subsystems/SceneComposition/VrGizmoHierarchyController.cs` (содержит класс `TransformGizmoHierarchyController`) **переименовывается** в `GizmoHierarchy.cs` и **перемещается** в `VrInteraction/Gizmo/`. Файл/класс не совпадали по имени — нарушение CLAUDE.md «One public type per file; file name matches type name exactly».

### Компоненты

| Компонент | Тип | Lifetime | Назначение |
|---|---|---|---|
| `GizmoActivator` | `MonoBehaviour` на отдельной scene-managed GO `_Gizmo` | Scene | Подписан на selection/panel/mode events; spawn/despawn префаба; держит current target и active strategy |
| `GizmoHierarchy` | `MonoBehaviour` на корне `Vr3D_Gizmos.prefab` | Prefab instance | Переключение parent/child handles по mode; кеширование default state |
| `GizmoHandle` | `MonoBehaviour : XRBaseInteractable` на каждой ручке prefab'а | Prefab instance | Reads `HandleKind`/`AxisKind` из SerializedField; репортит grip BeginDrag/UpdateDrag/EndDrag в Activator |
| `GizmoConfig` | `ScriptableObject` | Asset | `_gizmoPrefab`, `_boundsCoefficient` (1.5), `_minSize` (0.1), `_maxSize` (5.0) |
| `GizmoToolsPanel` | `MonoBehaviour` на корне sub-panel | Scene | В `OnEnable`/`OnDisable` публикует `GizmoToolsPanelOpenedEvent`/`Closed`; 3 кнопки публикуют `GizmoModeChangedEvent` |
| `GizmoToolsPanelOpener` | `MonoBehaviour` на кнопке UserPanel | Scene | Toggle активности sub-panel |
| `IGizmoDragStrategy` + 4 реализации | Plain C# | Per-drag | Чистая axis-math, тестируется без Unity runtime |

### Strategies

| Strategy | Применяется к handle | Что делает |
|---|---|---|
| `AxisMoveStrategy` | `HandleKind.MoveAxis` (X/Y/Z) | Project hand-pos на infinite line вдоль local axis, target.position скользит по линии |
| `AxisScaleStrategy` | `HandleKind.ScaleAxis` (X/Y/Z) | Signed distance hand→target вдоль axis, ratio к dist-at-grab → scale factor для оси |
| `UniformScaleStrategy` | `HandleKind.ScaleUniform` (центральный куб) | Distance hand→target / dist-at-grab → uniform scale factor |
| `RingRotateStrategy` | `HandleKind.RotateRing` (X/Y/Z) | Project hand-pos на plane (normal = axis), signed angle от dir-at-grab → quaternion rotation |

### События (struct-based, через `EventBus`)

| Event | Поля | Publisher → Subscriber |
|---|---|---|
| `GizmoToolsPanelOpenedEvent` | — | `GizmoToolsPanel.OnEnable` → `GizmoActivator` |
| `GizmoToolsPanelClosedEvent` | — | `GizmoToolsPanel.OnDisable` → `GizmoActivator` |
| `GizmoModeChangedEvent` | `GizmoMode Mode` | `GizmoToolsPanel` (button click) → `GizmoActivator` |
| `GizmoDragStartedEvent` | `string TargetNodeId` | `GizmoActivator.OnHandleGrabbed` → `UndoKeyHandler` (block undo), `GizmoToolsPanel` (disable mode buttons) |
| `GizmoDragEndedEvent` | `string TargetNodeId` | `GizmoActivator.OnHandleReleased` → те же подписчики |

---

## 4. Жизненный цикл

### State в Activator

```
_panelOpen   : bool                (default false)
_mode        : GizmoMode           (Move / Rotate / Scale; default Move)
_target      : Transform?          (текущий выделенный)
_targetNodeId: string?
_instance    : GameObject?         (current spawned gizmo)
_originalTargetCollider : Collider?  (cached для restore)
_dragActive  : bool
_activeHandle: GizmoHandle?
_activeStrategy : IGizmoDragStrategy?
_originalPos : Vector3
_originalRot : Quaternion
_originalScale : Vector3
```

### Event handlers

```
OnGizmoToolsPanelOpened:
    _panelOpen = true
    _mode      = GizmoMode.Move           // default at open
    RefreshVisibility()

OnGizmoToolsPanelClosed:
    if (_dragActive) return                // ignore mid-drag
    _panelOpen = false
    RefreshVisibility()

OnGizmoModeChanged(mode):
    if (_dragActive) return                // ignore mid-drag (UI button также disabled)
    _mode = mode
    if (_instance != null) _hierarchy.ShowMode(_mode)

OnSelectionChanged(id):
    if (_dragActive) return                // selection changes blocked mid-drag at UX level
    _target       = (id != null) ? sceneGraph.GetNode(id)?.transform : null
    _targetNodeId = id
    RefreshVisibility()

RefreshVisibility():
    shouldShow = _panelOpen && _target != null
    if (shouldShow && _instance == null)             Spawn()
    else if (!shouldShow && _instance != null)       Despawn()
    else if (shouldShow && _instance != null)        { Despawn(); Spawn(); }
```

### Spawn

1. `_instance = Instantiate(gizmoConfig.GizmoPrefab)` под scene root (не под target).
2. Cache `_originalTargetCollider = _target.GetComponent<Collider>()`, set `enabled = false`.
3. `_instance.position = _target.position`; `_instance.rotation = _target.rotation`.
4. `FitToBounds()`.
5. Cache references to `_hierarchy = _instance.GetComponent<GizmoHierarchy>()` и handles (через `GetComponentsInChildren<GizmoHandle>(true)`, inject Activator ref в каждый).
6. `_hierarchy.ShowMode(_mode)`.

### Despawn

1. If `_dragActive` — abort current drag (rollback to original, no commit).
2. Restore `_originalTargetCollider.enabled = true` if not null.
3. `Destroy(_instance)`.
4. Clear cached refs.

### Follow (LateUpdate)

```
if (_instance == null || _dragActive) return
if (_target == null) { Despawn(); return }
_instance.position = _target.position
_instance.rotation = _target.rotation
// scale управляется FitToBounds, не следует за target.localScale
```

### FitToBounds

```
Bounds combined = encapsulate of all Renderer.bounds in target hierarchy
                  (MeshRenderer + SkinnedMeshRenderer, includeInactive=false)
if (no renderers found)
    gizmoSize = config.MinSize
else
    float maxExtent = max(combined.extents.x, .y, .z)
    gizmoSize = clamp(maxExtent * config.BoundsCoefficient, config.MinSize, config.MaxSize)
_instance.localScale = Vector3.one * gizmoSize
```

Refit вызывается на: Spawn, OnHandleReleased (после Scale commit).

---

## 5. Drag flow

### Handle behavior

`GizmoHandle : XRBaseInteractable`:

```csharp
[SerializeField] private HandleKind _kind;
[SerializeField] private AxisKind   _axis;

private GizmoActivator     _activator;       // GetComponentInParent в Awake
private NearFarInteractor  _locked;
private enum State { Idle, Dragging }
private State _state;

public override bool IsSelectableBy(IXRSelectInteractor _) => false;

public override void ProcessInteractable(UpdatePhase phase)
{
    base.ProcessInteractable(phase);
    if (phase != Dynamic) return;

    if (_state == Dragging && (_locked == null || !_locked.isActiveAndEnabled))
    {
        _activator.OnHandleAborted();
        _state = Idle; _locked = null;
        return;
    }

    switch (_state)
    {
        case Idle:
            var ni = CurrentHoverer();        // copy of XRPromeonInteractable.CurrentHoverer
            if (ni == null || !IsPrimaryFor(ni)) break;
            if (ni.selectInput.ReadWasPerformedThisFrame())   // grip down
            {
                _locked = ni; _state = Dragging;
                var attach = ni.GetAttachTransform(this);
                _activator.OnHandleGrabbed(this, attach.position, attach.rotation);
            }
            break;

        case Dragging:
            if (_locked.selectInput.ReadWasCompletedThisFrame())   // grip up
            {
                _activator.OnHandleReleased();
                _locked = null; _state = Idle;
                break;
            }
            var attach2 = _locked.GetAttachTransform(this);
            _activator.OnHandleDragged(attach2.position, attach2.rotation);
            break;
    }
}
```

### Activator drag callbacks

```
OnHandleGrabbed(handle, handPos, handRot):
    if (_dragActive) return                          // single-handle lock
    _dragActive     = true
    _activeHandle   = handle
    _originalPos    = _target.position
    _originalRot    = _target.rotation
    _originalScale  = _target.localScale
    _activeStrategy = ResolveStrategy(handle)
    _hierarchy.OnHandleGrabbed(handle)               // re-parent for Move/Rotate
    _activeStrategy.BeginDrag(_target, handle, handPos, handRot)
    bus.Publish(new GizmoDragStartedEvent(_targetNodeId))

OnHandleDragged(handPos, handRot):
    if (!_dragActive || _target == null) { OnHandleAborted(); return }
    _activeStrategy.UpdateDrag(handPos, handRot)
    // strategy writes _target.position/rotation/localScale directly

OnHandleReleased():
    if (!_dragActive) return
    _activeStrategy.EndDrag()
    _hierarchy.OnHandleReleased()
    var finalPos   = _target.position
    var finalRot   = _target.rotation
    var finalScale = _target.localScale
    // Restore to original so TransformCommand.ctor captures correct _old snapshot
    _target.position   = _originalPos
    _target.rotation   = _originalRot
    _target.localScale = _originalScale
    gizmoController.CommitTransform(_target, finalPos, finalRot, finalScale)
    FitToBounds()   // bounds могли измениться после Scale
    _activeStrategy = null; _activeHandle = null; _dragActive = false
    bus.Publish(new GizmoDragEndedEvent(_targetNodeId))

OnHandleAborted():
    // controller died mid-drag, target deleted, etc.
    _target?.position   = _originalPos
    _target?.rotation   = _originalRot
    _target?.localScale = _originalScale
    _hierarchy.OnHandleReleased()
    _activeStrategy = null; _activeHandle = null; _dragActive = false
    bus.Publish(new GizmoDragEndedEvent(_targetNodeId))
```

`ResolveStrategy(handle)` — dispatcher по `(handle.Kind, handle.Axis)`. Single-handle lock защищает от двуручных конфликтов.

### Strategy formulas

`axisIndex` — индекс `AxisKind.X/Y/Z` (0/1/2).

Snapshot оси берётся из **target.rotation на момент BeginDrag**, не из handle.transform (handle может быть re-parented в hierarchy switch'е, target — нет):
- `AxisKind.X` → `target.right`
- `AxisKind.Y` → `target.up`
- `AxisKind.Z` → `target.forward`

**AxisMoveStrategy:**
```
BeginDrag(target, handle, handPos, handRot):
    _target   = target
    _axis     = LocalAxisFromTarget(target, handle.Axis)   // snapshot, LOCAL
    _originDistAtGrab = Dot(handPos - target.position, _axis)
    _originalTargetPos = target.position

UpdateDrag(handPos, handRot):
    float dist = Dot(handPos - _originalTargetPos, _axis)
    float delta = dist - _originDistAtGrab
    _target.position = _originalTargetPos + _axis * delta
```

**AxisScaleStrategy:**
```
BeginDrag:
    _axis      = LocalAxisFromTarget(target, handle.Axis)
    _axisIndex = (int)handle.Axis
    _originalScale = target.localScale
    _distAtGrab = max(0.01, Dot(handPos - target.position, _axis))

UpdateDrag(handPos, handRot):
    float distNow = Dot(handPos - target.position, _axis)
    float factor  = max(0.01, distNow / _distAtGrab)
    var scl = _originalScale
    scl[_axisIndex] = _originalScale[_axisIndex] * factor
    target.localScale = scl
```

**UniformScaleStrategy:**
```
BeginDrag:
    _originalScale = target.localScale
    _distAtGrab = max(0.01, (handPos - target.position).magnitude)

UpdateDrag(handPos, handRot):
    float distNow = (handPos - target.position).magnitude
    float factor  = max(0.01, distNow / _distAtGrab)
    target.localScale = _originalScale * factor
```

**RingRotateStrategy:**
```
BeginDrag:
    _normal   = LocalAxisFromTarget(target, handle.Axis)
    _grabDir  = ProjectOnPlane(handPos - target.position, _normal).normalized
    _originalRot = target.rotation

UpdateDrag(handPos, handRot):
    var nowDir = ProjectOnPlane(handPos - target.position, _normal).normalized
    float angle = Vector3.SignedAngle(_grabDir, nowDir, _normal)
    target.rotation = Quaternion.AngleAxis(angle, _normal) * _originalRot
```

### Mid-drag edge cases

| Случай | Поведение |
|---|---|
| Hand-pos coincides with target.position (rotate: nowDir == 0) | Skip frame, не пишем rotation |
| `_distAtGrab` нулевая (scale: пользователь схватил handle в pivot) | clamp до 0.01 в `BeginDrag`, factor никогда не делит на ноль |
| Selection меняется (event приходит) | Activator `OnSelectionChanged` no-op'ит пока `_dragActive` (UX не должен этого допускать) |
| target удалён | `OnHandleDragged` детектит `_target == null` → `OnHandleAborted` |

---

## 6. Hierarchy switching (GizmoHierarchy)

Существующий `TransformGizmoHierarchyController` уже почти готов. Изменения:

### Переименование

- File: `Subsystems/SceneComposition/VrGizmoHierarchyController.cs` → `Subsystems/VrInteraction/Gizmo/GizmoHierarchy.cs`
- Class: `TransformGizmoHierarchyController` → `GizmoHierarchy`
- Enum: nested `GizmoMode` → top-level `Subsystems/VrInteraction/Gizmo/GizmoMode.cs` (values: `Move`, `Rotate`, `Scale`)

### API

```csharp
public void ShowMode(GizmoMode mode);          // SetActive groups; hierarchy не меняется
public void OnHandleGrabbed(GizmoHandle handle);  // re-parent group items под activeHandle
public void OnHandleReleased();                // ResetHierarchy() + ShowMode(currentMode)
public void ResetHierarchy();                  // unchanged from current
```

`ShowMode`:
- `Move`: `moveRoot.SetActive(true); rotateRoot.SetActive(false); scaleRoot.SetActive(false);`
- `Rotate`: opposite for rotate.
- `Scale`: opposite for scale.

`OnHandleGrabbed`:
- `handle.Kind == MoveAxis`: re-parent (`moveCenter, moveX, moveY, moveZ` except active) под `handle.transform`.
- `handle.Kind == RotateRing`: re-parent (`rotateX, rotateY, rotateZ` except active) под `handle.transform`.
- `handle.Kind == ScaleAxis | ScaleUniform`: no-op (scale handles не требуют re-parenting; центр уже в корне).

---

## 7. Integration с существующим кодом

### `GizmoController` (Subsystems/VrInteraction/GizmoController.cs)

Уже существует, остаётся как DI service. Добавляется только seam для read-only флага «drag active» — нужен ли он `CommandStack`/`UndoKeyHandler`? Решено: **`UndoKeyHandler` подписывается напрямую на `GizmoDragStartedEvent`/`Ended`**, держит свой флаг. `GizmoController` не расширяется.

### `XRPromeonInteractable` (Phase 1 — input swap)

State machine:
```
enum State { Idle, TriggerPressed, TriggerRotate, GripMove }
```

```
Idle:
    trigger.WasPerformed → Lock; _pressTime = Time.time; _state = TriggerPressed
    grip.WasPerformed && IsObjectSelected → Lock; CapturePositionOffset(); _state = GripMove

TriggerPressed:
    trigger.WasCompleted → tap-select; EndInteraction
    Time.time - _pressTime > tapWindow && IsObjectSelected → CaptureRotationOffset(); _state = TriggerRotate

TriggerRotate:
    trigger.WasCompleted → CommitTransform(rot snapshot); EndInteraction
    else → ApplyRotate()

GripMove:
    grip.WasCompleted → CommitTransform(pos snapshot); EndInteraction
    else → ApplyMove()
```

`ApplyMove`/`ApplyRotate`/`CapturePositionOffset`/`CaptureRotationOffset` методы не трогаются — это чистый label swap + перенос capture в правильный transition.

Memory entry `project_interaction_input_model` обновляется по окончании Phase 1.

### `SelectionManager` (Phase 0 — single-select)

**API после:**
```csharp
public interface ISelectionManager
{
    string SelectedId { get; }
    void Select(string nodeId);    // null = clear
}
```

Удаляются: `SelectedIds`, `Toggle`, `Clear` (заменяется на `Select(null)`).

**`SelectionChangedEvent`:**
```csharp
public readonly struct SelectionChangedEvent
{
    public readonly string SelectedNodeId;     // null = ничего не выделено
    public SelectionChangedEvent(string id) { SelectedNodeId = id; }
}
```

Удаляется поле `SelectedNodeIds`.

**`enum SelectionVisual`:** `None`, `Selected`. Удаляется `InSet`.

**`Selectable.SetVisualState`:** удаляется ветка `case InSet:`. `Selected` остаётся (бывший `Active`, yellow).

**`SelectionVisualSync`:**
```csharp
private void OnSelectionChanged(SelectionChangedEvent e)
{
    foreach (var pair in _graph.Nodes)
    {
        var sel = pair.Value.GetComponent<Selectable>();
        if (sel == null) continue;
        sel.SetVisualState(pair.Key == e.SelectedNodeId
            ? SelectionVisual.Selected
            : SelectionVisual.None);
    }
}
```

**Сайты вызова под обновление (поиск grep'ом):**
- `XRPromeonInteractable.IsObjectSelected()` — `selectionManager.SelectedId == _node.NodeId`.
- `SceneOutlinerView`, `OutlinerItem` — переписать под single-select.
- `SceneInspectorView`, `PropertyPanel`, `BoneInspectorPanel`, `IkSetupWizard` — то же.
- Любые `SelectionVisual.InSet` references — удалить.

### `Vr3D_Gizmos.prefab` (manual prefab work — на стороне пользователя)

1. Заменить `TransformGizmoHierarchyController` на `GizmoHierarchy` (переподцепить script reference после rename — Unity не обновит автоматически).
2. На каждый handle добавить `GizmoHandle` компонент с правильными `_kind` и `_axis`.
3. Убедиться что каждый handle имеет Collider для XR raycast.

### Новые prefab'ы (manual prefab work)

- **`GizmoToolsPanel.prefab`** — sub-panel с 3 кнопками Move/Rotate/Scale + компонент `GizmoToolsPanel`.
- **`UserPanel.prefab`** — добавить кнопку «Gizmo Tools» с `GizmoToolsPanelOpener`.

### Bootstrap (`VrEditingSceneScope` + `SandboxSceneScope`)

```csharp
builder.RegisterInstance(_gizmoConfig);

var gizmoActivator = Object.FindAnyObjectByType<GizmoActivator>(FindObjectsInactive.Include);
if (gizmoActivator != null)
    builder.RegisterBuildCallback(c => c.Inject(gizmoActivator));

var gizmoToolsPanel = Object.FindAnyObjectByType<GizmoToolsPanel>(FindObjectsInactive.Include);
if (gizmoToolsPanel != null)
    builder.RegisterBuildCallback(c => c.Inject(gizmoToolsPanel));
```

GO `_Gizmo` с `GizmoActivator` создаётся в сцене (manual — на стороне пользователя), как и `GizmoToolsPanel` инстанс под UserPanel.

---

## 8. Edge cases

| Сценарий | Поведение |
|---|---|
| Target удалён во время drag | `OnHandleDragged` детектит → `OnHandleAborted` (rollback, no commit, Despawn) |
| Target удалён вне drag | `OnSelectionChanged(null)` → `_target = null` → `RefreshVisibility` → Despawn |
| Scene mode сменился (VrEditing → MainMenu) | `GizmoActivator` живёт в SceneScope, dispose при выгрузке сцены — стандартная VContainer cleanup |
| Sub-panel закрылась во время drag | `OnGizmoToolsPanelClosed` no-op'ит пока `_dragActive`; despawn после `OnHandleReleased` |
| Mode-кнопка нажата во время drag | UI button `interactable = false` пока `GizmoDragStartedEvent` (UI-layer guard); Activator также no-op'ит mid-drag |
| Двуручный grab | Activator держит single `_activeHandle` — второй `OnHandleGrabbed` отбрасывается пока `_activeHandle != null` |
| `target.localScale = (0,0,0)` на момент Scale grab | `_distAtGrab = max(0.01, ...)` защищает; original.localScale кешируется |
| Bounds пустые (target без Renderer) | fallback `gizmoSize = config.MinSize` |
| GizmoConfig prefab null | Activator логирует error, отключает себя; mode buttons no-op |
| QuickOutline на target когда collider отключён | Outline не зависит от collider — продолжает работать |
| Undo нажат во время drag | `UndoKeyHandler` подписан на `GizmoDragStartedEvent`/`Ended`, держит флаг `_dragActive`, no-op'ит undo/redo пока true |
| Renderer.bounds на skinned mesh | Берём текущий world-AABB после skinning; пересчитываем на каждом spawn и на Scale commit |

---

## 9. Testing

### Unit tests (EditMode, без Unity runtime)

`Subsystems/VrInteraction/Tests/`:

| Файл | Покрытие |
|---|---|
| `AxisMoveStrategyTests.cs` | Drag вдоль X → только x меняется. Hand движется сбоку → projection отбрасывает offset. Multiple frames монотонны |
| `AxisScaleStrategyTests.cs` | Drag наружу → scale > 1. К центру → < 1. distAtGrab клампится |
| `UniformScaleStrategyTests.cs` | Все 3 оси scale пропорционально. Min clamp = 0.01 |
| `RingRotateStrategyTests.cs` | Поворот на 90° → quaternion соответствует. Reverse → отрицательный angle. nowDir ~= 0 → no-op |
| `GizmoActivatorStateTests.cs` | State table (panelOpen × target × mode) → expected (visible, currentMode). Single-handle lock |
| `BoundsFitterTests.cs` | Combined bounds от нескольких Renderer; clamp min/max; empty fallback |

`Subsystems/SceneComposition/Tests/SelectionManagerTests.cs` — обновить под single-select API, удалить multi-select тесты.

### Manual smoke test (Quest 3, на стороне пользователя)

1. Открыть `VrEditing` сцену, выбрать объект, открыть Gizmo Tools → виден Move-гизмос в bounds объекта.
2. Drag X-arrow вправо grip'ом → объект едет по локальной X, не Y/Z.
3. Switch to Rotate → Move-стрелки исчезли, ring'и появились.
4. Drag Y-ring grip'ом → объект крутится вокруг локальной Y.
5. Switch to Scale → drag axis cube → один axis scale. Drag center → uniform.
6. Перевыбрать другой объект → гизмос переехал. Mode сохранился.
7. Закрыть Gizmo Tools → гизмос исчез. Open снова → появился (default Move).
8. Undo после серии commit'ов → корректный rollback каждой операции.
9. Open Sandbox scene → повторить п.1-5.
10. Hold trigger на объекте без gizmo (после tap window) → объект крутится (Phase 1 swap).
11. Hold grip на объекте → объект движется.
12. Tap trigger → объект выделяется.

---

## 10. File layout

```
Assets/_App/Subsystems/VrInteraction/Gizmo/
├── GizmoActivator.cs
├── GizmoHierarchy.cs                (rename + move из SceneComposition)
├── GizmoHandle.cs                   (rewrite старого VrInteraction/GizmoHandle.cs)
├── GizmoConfig.cs                   (ScriptableObject)
├── HandleKind.cs                    (enum)
├── AxisKind.cs                      (enum)
├── GizmoMode.cs                     (enum: Move/Rotate/Scale)
├── Strategies/
│   ├── IGizmoDragStrategy.cs
│   ├── AxisMoveStrategy.cs
│   ├── AxisScaleStrategy.cs
│   ├── UniformScaleStrategy.cs
│   └── RingRotateStrategy.cs
└── UI/
    ├── GizmoToolsPanel.cs
    └── GizmoToolsPanelOpener.cs

Assets/_App/Subsystems/VrInteraction/Tests/
├── AxisMoveStrategyTests.cs
├── AxisScaleStrategyTests.cs
├── UniformScaleStrategyTests.cs
├── RingRotateStrategyTests.cs
├── GizmoActivatorStateTests.cs
└── BoundsFitterTests.cs

Assets/_App/_Shared/Events/
├── GizmoModeChangedEvent.cs                (новый)
├── GizmoToolsPanelOpenedEvent.cs           (новый)
├── GizmoToolsPanelClosedEvent.cs           (новый)
├── GizmoDragStartedEvent.cs                (новый)
└── GizmoDragEndedEvent.cs                  (новый)

УДАЛИТЬ:
└── Assets/_App/Subsystems/SceneComposition/VrGizmoHierarchyController.cs
└── Assets/_App/Subsystems/VrInteraction/GizmoHandle.cs (старая версия)

ОБНОВИТЬ:
└── Assets/_App/Subsystems/SceneComposition/SelectionManager.cs (single-select API)
└── Assets/_App/_Shared/Events/SelectionChangedEvent.cs (убрать SelectedNodeIds)
└── Assets/_App/_Shared/Interfaces/ISelectionManager.cs
└── Assets/_App/Subsystems/VrInteraction/Selectable.cs (убрать InSet)
└── Assets/_App/Subsystems/VrInteraction/SelectionVisualSync.cs (упростить)
└── Assets/_App/Subsystems/VrInteraction/XRPromeonInteractable.cs (input swap)
└── Assets/_App/Subsystems/SpatialUi/Scripts/Views/SceneOutlinerView.cs
└── Assets/_App/Subsystems/SpatialUi/Scripts/Views/SceneInspectorView.cs
└── Assets/_App/Subsystems/SpatialUi/Scripts/Elements/OutlinerItem.cs
└── Assets/_App/Subsystems/SpatialUi/Scripts/Panels/PropertyPanel.cs
└── Assets/_App/Subsystems/SpatialUi/Scripts/Panels/BoneInspectorPanel.cs
└── Assets/_App/Subsystems/SpatialUi/Scripts/Panels/IkSetupWizard.cs
└── Assets/_App/Bootstrap/UndoKeyHandler.cs (подписка на drag events)
└── Assets/_App/Bootstrap/VrEditingSceneScope.cs (DI регистрация Activator + Panel + Config)
└── Assets/_App/Bootstrap/SandboxSceneScope.cs (то же)

MANUAL PREFAB WORK (на стороне пользователя):
└── Assets/Resources/Prefabs/Gizmos/Vr3D_Gizmos.prefab — назначить GizmoHandle на ручки, переподцепить GizmoHierarchy
└── Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/UserPanel.prefab — кнопка Gizmo Tools
└── Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/GizmoToolsPanel.prefab — новый prefab с 3 кнопками
└── В сцене (VrEditing, Sandbox): создать GO `_Gizmo` с компонентом GizmoActivator
└── Создать GizmoConfig.asset в Assets/_App/Subsystems/VrInteraction/Gizmo/
```

---

## 11. Phasing

| Phase | Содержание |
|---|---|
| **Phase 0** | SelectionManager hard cleanup до single-select. Все call-sites обновлены. Тесты обновлены. Никаких регрессий выделения. |
| **Phase 1** | Input swap в `XRPromeonInteractable` (label rename + capture move). Memory entry обновлён. |
| **Phase 2** | Gizmo system: enums, events, GizmoMode/Hierarchy rename и move, GizmoConfig, GizmoHandle rewrite, strategies, GizmoActivator, GizmoToolsPanel + Opener, DI регистрации, тесты strategies/activator. |
| **Phase 3** | Manual prefab work (на стороне пользователя): handles, sub-panel, opener button, scene GO `_Gizmo`. Smoke test по п.1-12. |

Phase 0 и Phase 1 — независимы и могут быть merged в любом порядке. Phase 2 опирается на Phase 0 (single-select). Phase 3 опирается на Phase 2.

---

## 12. Conventions compliance

- ✅ No public fields — `[SerializeField] private` везде.
- ✅ Events — `struct` с суффиксом `Event`.
- ✅ Enum / Class / Interface naming per CLAUDE.md.
- ✅ Один тип в файле, file name = type name.
- ✅ Без forbidden generic suffixes (Manager/Handler/Controller/Service/...) — нейминг: `GizmoActivator`, `GizmoHierarchy`, `GizmoHandle`, `GizmoToolsPanel`, `GizmoToolsPanelOpener`. (`GizmoController` остаётся как был — существующий код.)
- ✅ Cross-subsystem messaging через struct events на EventBus, никаких прямых method calls.
- ✅ Без `FindObjectOfType` в runtime hot-path (только в bootstrap для DI scan).
- ✅ Без `Resources.Load` — gizmo prefab через `GizmoConfig` ScriptableObject reference.
- ✅ `CommandStack` — единственный путь для user-reversible изменений; drag finalises через `TransformCommand`.
- ✅ Без `#if UNITY_EDITOR` в runtime коде.
- ✅ Без `MonoBehaviour` как data container — GizmoConfig это SO.

## 13. Open questions для подтверждения

Все ключевые UX и архитектурные решения подтверждены через clarifying questions (см. session transcript). Открытых вопросов нет.
