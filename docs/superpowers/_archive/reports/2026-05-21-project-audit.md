# Project Audit — 2026-05-21

Read-only review of `Assets/_App`. Findings ordered by actionability.

## TL;DR

- **1 bug found:** `ConstraintFreezePosition.cs` содержит пустой класс `Акуу` (артефакт от прошлой грязной правки).
- **5 placeholder subsystems** живут как scaffolds + .asmdef — ожидаемо по архитектурному плану, но `AnimationPlayback` уже отмечен как merged into AnimationAuthoring и теперь чисто scaffold.
- **Дубликатов/orphan-скриптов нет** среди топ-уровня subsystem'ов. Старый `VrGizmoHierarchyController` удалён, `IDragStrategy` (legacy) живёт корректно — он принадлежит регулярному grab'у, а не гизмо.
- **Direction:** последние 3 недели вся активность концентрировалась в `VrInteraction/Gizmo/*`, `SpatialUi/Prefabs/UserPanel/*`, `RigBuilder/PromeonProxyRigBuilder.cs`. Constraints subsystem только инициирован (один файл, и тот пустой).

## Dead code / candidates for cleanup

### Bug: `ConstraintFreezePosition.cs` содержит чужой класс

`Assets/_App/Subsystems/SceneComposition/Constraints/ConstraintFreezePosition.cs`:
```csharp
using UnityEngine;

public class Акуу
{
}
```

Файл создан в последнем коммите (`0ae72db 12321`), скорее всего как scaffold для constraints. Содержимое перенесли из мусорного `Акуу.cs` (который был удалён рядом в коммите `358c1a8`), но забыли переименовать класс. Никто `Акуу` не использует, `ConstraintFreezePosition` тоже не упоминается ни в одном .cs.

**Действие:** либо реализуй `public class ConstraintFreezePosition` с настоящим content'ом, либо удали файл с .meta.

### Placeholder subsystems (5)

| Файл | Класс | Статус |
|---|---|---|
| `AnimationPlayback/AnimationPlayback.cs` | `AnimationPlaybackPlaceholder` | Comment: "merged into AnimationAuthoring + AnimationClock" — больше не subsystem. |
| `EnvironmentMapping/EnvironmentMapping.cs` | `EnvironmentMappingPlaceholder` | Не начат. |
| `ErrorHandling/ErrorHandling.cs` | `ErrorHandlingPlaceholder` | Не начат. |
| `ExportPipeline/ExportPipeline.cs` | `ExportPipelinePlaceholder` | Не начат. |
| `InputBindings/InputBindings.cs` | `InputBindingsPlaceholder` | Не начат. |

Каждый имеет свой `.asmdef`. По CLAUDE.md это intentional — все 13 subsystems зарезервированы как scaffolds. Не deadcode формально, но `AnimationPlaybackPlaceholder` сигналит, что subsystem был отменён — может стоит либо удалить целиком (включая .asmdef и папку), либо переименовать `AnimationPlayback` подкаталог в `AnimationClock` (где реально живёт код).

### Empty Scene Scopes

- `Bootstrap/ArMappingSceneScope.cs` — `Configure(...)` пустой.
- `Bootstrap/ArPreviewSceneScope.cs` — `Configure(...)` пустой.

Скоупы существуют для будущих регистраций (EnvironmentMapping и AR Preview). Можно оставить как scaffold, либо удалить до начала работы над AR-режимами.

### Empty UI module

- `SpatialUi/Scripts/Panels/SettingsModule.cs` — `// Settings UI content goes here` и больше ничего.

## Direction analysis (last ~3 weeks)

Сгруппировал по volume of changes:

### 1. VR Gizmo system (just shipped, this session)
Файлы: `VrInteraction/Gizmo/*` (новый поддиректорий), `_Shared/Models/GizmoMode.cs`, `SpatialUi/.../GizmoToolsModule.prefab`, `Resources/Prefabs/Gizmos/Vr3D_Gizmos.prefab`.

Имплементация: 13 С# файлов (4 стратегии + Activator + Hierarchy + Handle + Config + BoundsFitter + Tools panel + enums + events), 5 EditMode test files. **Поведенческая модель отличается от spec'а** — гизмо primary, target follows. Detail в `memory/project_gizmo_system.md`.

### 2. Spawn + safety (player anchor + fall guard)
Файлы: `Bootstrap/PlayerSpawnApplier.cs`, `Bootstrap/FallGuard.cs`, `_Shared/Models/PlayerSpawnAnchor.cs`, prefab `User XR Origin (XR Rig)`.

Контекст: rig телепортируется на (0,0,0) при sceneLoaded, FallGuard Y<-20 → Respawn. Старый PlayerSpawnAnchor prefab удалён. Spec: `docs/superpowers/specs/2026-05-21-player-anchor-fall-guard-design.md`.

### 3. RigBuilder / PromeonProxyRigBuilder (continuous tweaking)
`PromeonProxyRigBuilder.cs` менялся практически в каждом коммите — продолжающаяся доработка proxy-rig фабрики для импортируемых рагдоллов. Открытый bug: `interactable-conflict` (см. memory).

### 4. Asset library tweaks
Множественные правки prefab'ов в `BuiltinLab_ObjectPrefabs/` (Crush Dummy, Potted Plant N, Street Tree N) + асет-каталоги. Часть QuickOutline patch (см. memory).

### 5. Constraints subsystem (только инициирован)
Один файл `ConstraintFreezePosition.cs`, и тот пустой. Похоже, начало работы — может оформиться в следующий phase.

## Hotkeys / Input bindings (current state)

Собрано из live кода для записи в memory.

### Клавиатура
| Хоткей | Действие | Источник |
|---|---|---|
| `Ctrl-Z` (Left или Right) | Undo через `CommandStack.Undo()` | `Bootstrap/UndoKeyHandler.cs` |

`UndoKeyHandler` подписан на `GizmoDragStartedEvent`/`Ended` и no-op'ит Ctrl-Z пока drag активен. Redo не реализован.

### Quest controllers (XRI InputActions)
| Кнопка | Действие | Источник |
|---|---|---|
| `primaryButton` (X на L / A на R) | Toggle UserPanel | `SpatialUi/Scripts/Elements/UserPanelOpener.cs` |
| `trigger` tap | Select 3D object | `XRPromeonInteractable` (см. [[interaction-input-model]]) |
| `trigger` hold | Rotation drag selected | same |
| `grip` hold | Position drag selected (regular grab) | same |
| `grip` hold на GizmoHandle | Axis-constrained drag через гизмо | `Gizmo/GizmoHandle.cs` |
| `trigger` tap в пустоту | Deselect | `WorldClickCatcher` |

### Открытые пробелы
- Redo (Ctrl-Y / Ctrl-Shift-Z) — нет
- Save scene (Ctrl-S) — нет (есть auto-save через `SceneAutoSaver`)
- Cancel drag (Escape или secondary button) — нет

## Stale / outdated в memory

Просканировал memory index:
- `[[interaction-input-model]]` упоминает `Toggle(NodeId)` в "ключевых инвариантах" — фактически уже `Select(NodeId)` после single-select cleanup (см. [[selection-single-only]]). **Пользователь явно попросил не трогать этот файл** — оставляем как есть, но факт зафиксирован.
- `[[gizmo-system]]` обновлён в этой сессии (post-shipping inversion).
- `[[bone-outline-needs-click]]` — open bug, не закрыт.
- `[[ik-interactable-conflict]]` — open bug, не закрыт.

## Tests health

EditMode test files (находятся через `*Tests.cs` под `Tests/` папками):
- `SceneComposition/Tests/SelectionManagerTests.cs`
- `VrInteraction/Tests/AxisMoveStrategyTests.cs`
- `VrInteraction/Tests/AxisScaleStrategyTests.cs`
- `VrInteraction/Tests/UniformScaleStrategyTests.cs`
- `VrInteraction/Tests/RingRotateStrategyTests.cs`
- `VrInteraction/Tests/BoundsFitterTests.cs`
- `VrInteraction/Tests/GizmoActivatorStateTests.cs`
- `RigBuilder/Tests/PromeonProxyRigBuilderTests.cs`
- `StorageCore/Tests/*` (не открывал)
- `AnimationAuthoring/Tests/*` (не открывал)

`GizmoActivatorStateTests` — smoke-тест на state machine; **может быть устаревшим** после inversion: тесты писались когда strategy мутировала `_target`, а сейчас мутирует `_instance.transform`. Стоит запустить TestRunner и посмотреть.

## Рекомендации

1. **Срочно:** удалить или починить `ConstraintFreezePosition.cs` (класс `Акуу`).
2. **Скоро:** прогнать TestRunner на `Subsystems.VrInteraction.Tests.asmdef` — проверить что gizmo тесты не сломались inversion'ом.
3. **Позже:** определиться с `AnimationPlayback/` — оставить scaffold или удалить subsystem целиком (placeholder признаётся отменённым).
4. **Позже:** добавить Redo + Save хоткеи если планируется.
