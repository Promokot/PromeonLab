# UserPanel — кнопка «Main Menu» не работает: экспресс-план исправления

**Дата:** 2026-05-16

---

## Проблема

Кнопка `_mainMenuButton` в `UserPanel` при нажатии не производит никакого эффекта (тишина в Console) из всех сцен кроме самого `MainMenu`.

---

## Диагноз

**Симптом:** тишина при клике → `_orchestrator?.TransitionTo(...)` с `_orchestrator == null`.

**Корень:** `UserPanel.Construct()` (`[Inject]`) никогда не вызывается.

В `VrEditingSceneScope.Configure()` используется:
```csharp
var userPanel = Object.FindAnyObjectByType<UserPanel>(FindObjectsInactive.Include);
if (userPanel != null)
    builder.RegisterInstance(userPanel);
```

`RegisterInstance` для MonoBehaviour в VContainer регистрирует объект для разрешения другими,
но **не вызывает `[Inject]`-методы на самом объекте**. Для этого требуется явный
`container.Inject(instance)` через `RegisterBuildCallback`.

Дополнительно: переход `ArMapping → MainMenu` отсутствует в `DefaultModeTransitionGraph.asset`
(есть только `ArMapping → VrEditing`), что заблокировало бы кнопку даже при корректной инъекции.

---

## Решение

### Шаг 1 — Перенести регистрацию UserPanel в `RootLifetimeScope`

`UserPanel` физически находится на Bootstrap XR Rig (`Bootstrap.unity`, всегда загружен).
Архитектурно правильное место — Root scope, где живут `ModeOrchestrator` и `EventBus`.

В `RootLifetimeScope.Configure()` добавить:
```csharp
var userPanel = Object.FindAnyObjectByType<UserPanel>(FindObjectsInactive.Include);
if (userPanel != null)
{
    builder.RegisterInstance(userPanel);
    builder.RegisterBuildCallback(c => c.Inject(userPanel));
}
```

`RegisterBuildCallback` вызывается после построения контейнера и гарантирует вызов `Construct()`.

Из `VrEditingSceneScope.Configure()` соответствующий блок (`var userPanel = ...`) удалить.

### Шаг 2 — Добавить переход `ArMapping → MainMenu` в граф

В `DefaultModeTransitionGraph.asset` добавить запись:
```yaml
- From: 2   # ArMapping
  To: 0     # MainMenu
```

Это позволяет корректно вернуться в меню из AR-режима.

---

## Затронутые файлы

| Файл | Изменение |
|------|-----------|
| `Assets/_App/Bootstrap/RootLifetimeScope.cs` | Добавить FindAnyObjectByType + RegisterInstance + RegisterBuildCallback |
| `Assets/_App/Bootstrap/VrEditingSceneScope.cs` | Удалить блок регистрации UserPanel |
| `Assets/_App/Subsystems/ModeOrchestrator/Data/DefaultModeTransitionGraph.asset` | Добавить ArMapping → MainMenu |

---

## Ограничения

- `RegisterBuildCallback` поддерживается VContainer начиная с версии 2.x. Если проект использует более раннюю версию — нужен альтернативный подход (IStartable в Root scope).
- `ArPreview → MainMenu` перехода также нет в графе; при необходимости добавить аналогично (From: 3 → To: 0).
