# NavBar Panel System — Руководство по ручной настройке в Unity Inspector

Выполнять **после** завершения всех code задач (Tasks 1–7 плана).

---

## Шаг 1: Создать NavBarConfig.asset

1. В Project window перейти в `Assets/_App/Subsystems/SpatialUi/Data/`
2. Right-click → `Create > VrAnimApp > NavBarConfig`
3. Назвать файл `NavBarConfig`
4. В Inspector настроить массив `Entries` (7 элементов):

| Index | Id | VisibleModes | StartsEnabled |
|---|---|---|---|
| 0 | `settings` | MainMenu, VrEditing, Sandbox, ArMapping | ✓ |
| 1 | `assets` | MainMenu, VrEditing, Sandbox | ✓ |
| 2 | `outliner` | VrEditing, Sandbox | ✓ |
| 3 | `inspector` | VrEditing, Sandbox | ✓ |
| 4 | `timeline` | VrEditing | ✓ |
| 5 | `rigging` | VrEditing, Sandbox | ✗ |
| 6 | `gizmo` | VrEditing, Sandbox | ✗ |

---

## Шаг 2: Добавить кнопки в UserPanel prefab

> Открыть `UserPanel.prefab` (double-click → Prefab Mode)

В горизонтальной группе кнопок нужно убедиться что есть следующие **Button** объекты. Те которых нет — добавить по аналогии с существующими (`Settings`, `Assets`):

| Кнопка | Назначение |
|---|---|
| Settings | открывает SettingsModule |
| Assets | открывает AssetBrowserModule |
| Outliner | открывает OutlinerPanel |
| Inspector | открывает InspectorPanel |
| Timeline | заглушка (disabled, реализация позже) |
| Rigging Tools | заглушка (disabled) |
| Gizmo Tools | заглушка (disabled) |

**Для каждой кнопки:** убедиться что у неё есть дочерний `Image`-объект под названием `ActiveIndicator` (Raycast Target = off, начальный цвет — прозрачно-белый `FFFFFF с alpha ~50`). Этот image используется как индикатор активного состояния.

---

## Шаг 3: Добавить Outliner как дочерний объект UserPanel

В иерархии UserPanel prefab создать дочерний GameObject `OutlinerPanel`:

1. `GameObject > UI > Canvas` (или добавить `Canvas` компонент вручную на новый GO)
   - Render Mode: **World Space**
   - Layer: UI
   - Sorting Layer: такой же как у UserPanel
2. Добавить компонент `DetachablePanel`
3. Создать иерархию содержимого:
   ```
   OutlinerPanel/
   ├── Header/
   │   ├── DragStrip (Image + DetachablePanelDragHandle)
   │   ├── TitleText (TMP_Text: "Outliner")
   │   ├── LinkButton (Button с иконкой link/unlink)
   │   ├── LockButton (Button с иконкой замка)
   │   └── CloseButton (Button с иконкой ×)
   └── Content/
       └── ScrollView/
           └── Viewport/Content/ ← сюда SceneOutlinerView._rowsRoot
   ```
4. Добавить `SceneOutlinerView` компонент на `OutlinerPanel` или на `Content`
   - `_rowsRoot` → Content Transform (из ScrollView)
   - `_rowPrefab` → `OutlinerObject_ItemUI` prefab (перетащить из Project window)
5. В `DetachablePanel` компоненте назначить:
   - `_linkButton` → `Header/LinkButton`
   - `_lockButton` → `Header/LockButton`
   - `_closeButton` → `Header/CloseButton`
   - `_dragHandle` → `Header/DragStrip` (DetachablePanelDragHandle компонент)
6. В `DetachablePanelDragHandle` (на DragStrip):
   - `_panel` → `DetachablePanel` компонент на `OutlinerPanel`

---

## Шаг 4: Добавить Inspector как дочерний объект UserPanel

Аналогично Outliner, создать `InspectorPanel`:

1. Canvas (World Space)
2. Добавить `DetachablePanel`
3. Иерархия:
   ```
   InspectorPanel/
   ├── Header/
   │   ├── DragStrip (DetachablePanelDragHandle)
   │   ├── TitleText
   │   ├── LinkButton
   │   ├── LockButton
   │   └── CloseButton
   └── Content/
       ├── EmptyState (текст "Ничего не выбрано")
       └── NodeContent/
           ├── NameField (TMP_InputField)
           ├── TypeLabel (TMP_Text)
           ├── PosX/Y/Z (TMP_Text ×3)
           ├── RotX/Y/Z (TMP_Text ×3)
           └── ScaleX/Y/Z (TMP_Text ×3)
   ```
4. Добавить `SceneInspectorView` на `InspectorPanel` и назначить **13 полей**:
   - `_emptyState` → `Content/EmptyState`
   - `_content` → `Content/NodeContent`
   - `_nameField` → `NameField`
   - `_typeLabel` → `TypeLabel`
   - `_posX/Y/Z`, `_rotX/Y/Z`, `_scaleX/Y/Z` → соответствующие TMP_Text
5. В `DetachablePanel`:
   - `_linkButton`, `_lockButton`, `_closeButton`, `_dragHandle` — как в Outliner

---

## Шаг 5: Назначить NavBarBinding[] в UserPanel

Выбрать **UserPanel root GameObject**. В компоненте `UserPanel`:

1. Перетащить `NavBarConfig.asset` в поле `Nav Bar Config`
2. Задать массив `Bindings` (7 элементов):

| EntryId | NavButton | Panel |
|---|---|---|
| `settings` | Settings Button | SettingsModule компонент |
| `assets` | Assets Button | AssetBrowserModule компонент |
| `outliner` | Outliner Button | DetachablePanel компонент на OutlinerPanel |
| `inspector` | Inspector Button | DetachablePanel компонент на InspectorPanel |
| `timeline` | Timeline Button | None (заглушка) |
| `rigging` | Rigging Button | None (заглушка) |
| `gizmo` | Gizmo Button | None (заглушка) |

3. Назначить кнопки навигации:
   - `Main Menu Button` → кнопка "Назад в главное меню"
   - `Exit Button` → кнопка выхода из приложения
   - `Lock Button` → кнопка блокировки UserPanel
   - `Lock Button Image` → Image компонент на Lock Button

4. Сохранить prefab (Ctrl+S)

---

## Шаг 6: Проверить что OutlinerObject_ItemUI prefab настроен

В `OutlinerObject_ItemUI.prefab` должен быть компонент `OutlinerItem` с назначенными:
- `_highlight` → Background Image
- `_button` → Button компонент
- `_label` → TMP_Text

Если не назначены — сделать это сейчас.

---

## Чеклист тестирования (Play Mode)

### Базовая видимость кнопок

- [ ] Запустить Play Mode
- [ ] В **MainMenu** режиме видны только: `Settings`, `Assets`
- [ ] После перехода в **VrEditing** появляются: `Outliner`, `Inspector`, `Timeline`
- [ ] `Rigging Tools`, `Gizmo Tools` видны но не кликабельны (interactable = false)
- [ ] В **Sandbox** режиме нет `Timeline`, остальные как в VrEditing

### Кнопки-модули (Settings, Assets)

- [ ] Клик `Settings` → SettingsModule открывается, активный индикатор светлый
- [ ] Клик `Assets` → AssetBrowserModule открывается, **Settings закрывается автоматически**
- [ ] Повторный клик `Assets` → закрывается
- [ ] Индикатор гаснет при закрытии

### Outliner (DetachablePanel, Linked)

- [ ] Клик `Outliner` → OutlinerPanel появляется внутри UserPanel
- [ ] В OutlinerPanel виден список объектов сцены (если они есть)
- [ ] Повторный клик `Outliner` → панель скрывается
- [ ] Индикатор активен пока панель открыта

### Outliner (Unlink → Float)

- [ ] Открыть Outliner
- [ ] Нажать кнопку `Link/Unlink` на панели → панель отсоединяется от UserPanel, становится отдельным окном в мировом пространстве
- [ ] Индикатор `Outliner` на UserPanel **гаснет** (панель больше не "принадлежит" nav bar)
- [ ] Появляются `Lock` и `Close` кнопки на отсоединённой панели
- [ ] Перетащить панель за DragStrip → перемещается в мировом пространстве
- [ ] Нажать Lock → панель фиксируется, drag не работает
- [ ] Повторный клик `Outliner` на UserPanel → открывается **новый** linked Outliner

### Закрытие floating панели

- [ ] Нажать `Close` на floating панели → уничтожается
- [ ] Или нажать `Link Back` → уничтожается, linked-слот снова доступен через nav bar

### Сцена: сохранение и загрузка

- [ ] Войти в **VrEditing**, открыть сцену
- [ ] Заспавнить объект через Assets
- [ ] Выйти в **MainMenu** (через кнопку Main Menu)
- [ ] Снова открыть ту же сцену в VrEditing
- [ ] Объект **присутствует** — SceneAutoSaver отработал корректно

### Выделение объектов (требует wiring WorldClickCatcher)

> **Предварительно:** на XR Origin назначить `_leftInteractor` и `_rightInteractor` в `WorldClickCatcher`.

- [ ] В VrEditing нажать триггер на пустой области → выделение снимается
- [ ] Нажать триггер на объекте сцены → объект **не** снимает выделение (WorldClickCatcher видит hoveredInteractable)
- [ ] Объект отображается выделенным в SceneOutlinerView
- [ ] SceneInspectorView показывает его имя и трансформ

---

## Если что-то не работает

| Симптом | Вероятная причина |
|---|---|
| Кнопки не скрываются при смене режима | `_navBarConfig` не назначен в UserPanel Inspector |
| OutlinerPanel не инжектится (NullRef на EventBus) | VContainer не видит DetachablePanel в иерархии — проверить что UserPanel instantiated через container |
| Индикатор не меняет цвет | `GetComponentInChildren<Image>()` нашёл не тот Image — добавить отдельный `ActiveIndicator` дочерний объект |
| Drag не работает на floating панели | `DetachablePanelDragHandle.enabled` не становится `true` — проверить что `_dragHandle` назначен в DetachablePanel |
| `SceneOutlinerView._bus` null | [Inject] не вызывается — убедиться что VContainer auto-inject включён для иерархии или добавить ручной InjectGameObject вызов |
