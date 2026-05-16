# Double UserPanel + Cursor Fix: Отчёт

**Дата:** 2026-05-16  
**Статус:** Реализовано, требует ручного тестирования в Play Mode

---

## Что было сломано и почему

### Баг 1: Двойной UserPanel при переходе в VrEditing / Sandbox

`DefaultPanelRegistry.asset` содержал одну запись — `UserPanel.prefab` (PanelId = 6).  
При каждом переходе в VrEditing или Sandbox `UiPanelManager.SpawnPanels()` вызывал  
`_resolver.Instantiate(UserPanel.prefab)` — создавался **второй** экземпляр UserPanel  
в мировых координатах без родителя.

Одновременно оригинальный UserPanel живёт как дочерний объект XR Rig в Bootstrap-сцене  
и зарегистрирован в `RootLifetimeScope` через `FindAnyObjectByType<UserPanel>()`.  
Итог: два UserPanel в иерархии одновременно.

### Баг 2: Курсор мыши виден и интерактивен в Play Mode

`XRUIInputModule` в `EventSystem.prefab` имел флаг `m_EnableMouseInput: 1`.  
Мышь генерировала pointer events и взаимодействовала с UI-элементами (hover, click)  
даже при скрытом курсоре — критично для VR-тестирования.

---

## Что было сделано

### Fix 1 — `Assets/_App/Subsystems/SpatialUi/Data/DefaultPanelRegistry.asset`

Очищен список `_panels`:

```yaml
# было:
_panels:
- Id: 6
  Prefab: {fileID: 8573857923733603883, guid: 7a4de75d919ab50449b093180517b28c, type: 3}
  VisibleInModes: 000000000100000004000000020000000300000005000000

# стало:
_panels: []
```

`UiPanelManager.SpawnPanels()` теперь не порождает второй экземпляр.  
Единственный UserPanel — на XR Rig в Bootstrap, он же управляется `UserPanelOpener`.

### Fix 2 — `Assets/_App/Bootstrap/AppBootstrap.cs`

Добавлено скрытие курсора в `Start()`:

```csharp
Cursor.visible   = false;
Cursor.lockState = CursorLockMode.Locked;
```

Применяется один раз при старте. На Quest — нет эффекта (курсор и так не рендерится).  
В Play Mode и PC-сборках курсор скрыт и зафиксирован.

### Fix 3 — `Assets/Resources/Prefabs/User/EventSystem.prefab`

Отключён mouse input в `XRUIInputModule`:

```yaml
# было:
m_EnableMouseInput: 1

# стало:
m_EnableMouseInput: 0
```

Мышь больше не генерирует pointer events. XR controller raycasting (VR контроллеры) работает штатно.

---

## Ручное тестирование (необходимо)

| Шаг | Ожидаемый результат |
|---|---|
| Войти в VrEditing или Sandbox | В Hierarchy → "UserPanel": **ровно один объект** |
| Переключиться между режимами несколько раз | Дубликатов UserPanel не появляется |
| Навести мышь на кнопки в Game View | Никакого hover/click эффекта |
| Использовать VR контроллеры для UI | Работает штатно |
| Выйти из Play Mode | Курсор возвращается в норму автоматически |

---

## Файлы изменены

| Файл | Изменение |
|---|---|
| `Assets/_App/Subsystems/SpatialUi/Data/DefaultPanelRegistry.asset` | `_panels: []` |
| `Assets/_App/Bootstrap/AppBootstrap.cs` | Cursor.visible/lockState |
| `Assets/Resources/Prefabs/User/EventSystem.prefab` | `m_EnableMouseInput: 0` |
