# Сессия 2026-05-17 — XR Interaction, UI Bugs, NavBar Design

## Что обсуждали

### XRRayInteractor → NearFarInteractor

`WorldClickCatcher` был написан под устаревший `XRRayInteractor`, но в `User XR Origin (XR Rig).prefab`
основная интеракция — `NearFarInteractor` (XRI 3.x). `XRRayInteractor` есть только на `Teleport Interactor`
(для телепортации), использовать его для выделения объектов нельзя.

**Изменение:** `WorldClickCatcher.cs` переписан:
- Поля `XRRayInteractor _leftRay/_rightRay` + `InputActionReference` × 2 → `NearFarInteractor _leftInteractor/_rightInteractor`
- Trigger detection через `isSelectActive` + edge detection в `Update()` (wasActive tracking)
- Hit check через `interactablesHovered` вместо `TryGetCurrent3DRaycastHit`

**Manual task:** В Inspector на `WorldClickCatcher` (XR Origin root) назначить:
- `_leftInteractor` → `Camera Offset/Left Controller/Near-Far Interactor`
- `_rightInteractor` → `Camera Offset/Right Controller/Near-Far Interactor`

### SelectionInteractorFactory — краш при спавне без коллайдера

**Проблема:** `SelectionInteractor` имеет `[RequireComponent(typeof(Collider))]`. Фабрика проверяла
`GetComponentInChildren<Collider>()` — если коллайдер был только на дочернем меше (например, Toilet FBX),
Unity блокировал `AddComponent<SelectionInteractor>()` на root GO → NullReferenceException.

**Фикс:** `GetComponentInChildren` → `GetComponent` в `SelectionInteractorFactory.cs:17`.
Теперь BoxCollider добавляется на root если у него нет прямого коллайдера.

### SceneAutoSaver — race condition при выгрузке сцены

**Проблема:** `SaveCurrentAsync()` делал `await LoadSceneAsync()` перед `CaptureSnapshot()`.
Если данные не в кэше, `await` делал yield, и к моменту `CaptureSnapshot` Unity-сцена могла
выгрузиться → `SceneNode.transform` destroyed → пустой/сломанный снапшот.

**Фикс:** Добавлен `AppStorage.GetCachedScene(sceneId)` (синхронный). `SceneAutoSaver` теперь
берёт данные из кэша синхронно ДО любого `await`, и snapshot захватывается сразу.

**Важно:** `SceneAutoSaver` сохраняет только при переходе из VrEditing в другой режим (через
`ModeChangedEvent`). Если Play Mode остановлен в Editor без перехода — изменения теряются. Это
ожидаемое поведение для dev-сессий. `XRInteractionManager` в bootstrap-сцене — норма, XRSimpleInteractable
регистрируется в нём автоматически.

---

## NavBar Panel System Design

Написан спек: `docs/superpowers/specs/2026-05-17-navbar-panel-system-design.md`

### Суть редизайна

**Удаляем:** `ContextSlot` + `ContextMenuEntry[]` в UserPanel, пустые `UserPanel_ContextMenu_*.cs` файлы.

**Добавляем:**
- `NavBarConfig` ScriptableObject — маппинг `EntryId → AppMode[]` (какие кнопки видны в каких режимах)
- `NavBarBinding[]` в UserPanel — связывает EntryId с реальными Button и Panel объектами
- `DetachablePanel` компонент — три состояния: Linked (в UserPanel), Unlinked (floating), Closed
- Три события: `PanelDetachedEvent`, `PanelLinkedEvent`, `PanelClosedEvent`

### Маппинг кнопок по режимам

| Кнопка | MainMenu | VrEditing | Sandbox |
|--------|----------|-----------|---------|
| Settings | ✓ | ✓ | ✓ |
| Assets | ✓ | ✓ | ✓ |
| Outliner | — | ✓ | ✓ |
| Inspector | — | ✓ | ✓ |
| Timeline | — | ✓ | — |
| Rigging Tools | — | stub | stub |
| Gizmo Tools | — | stub | stub |

### DetachablePanel механика

- **Linked:** живёт внутри UserPanel, наследует SmartFollow, drag выключен
- **Unlinked:** reparent → scene root, world position сохраняется, drag включён, появляются Lock + Close
- **Кнопки в nav bar:** остаются "нажатыми" пока linked-панель открыта
- Несколько unlinked экземпляров одной панели одновременно — разрешено
- При смене режима — все панели уничтожаются (VContainer scope dispose)

---

## Известные нерешённые задачи

### Manual (в Unity Editor)
1. `WorldClickCatcher` на XR Origin — назначить `Near-Far Interactor` с каждого контроллера
2. `OutlinerObject_ItemUI.prefab` — wire `_highlight`, `_button`, `_label` в OutlinerItem component
3. `ContextMenu_VrEditing.prefab` — wire SceneOutlinerView + добавить SceneInspectorView иерархию

### Code (следующие сессии)
1. **Реализовать NavBar Panel System** — по спеку выше (writing-plans → implementation)
2. **Заставить работать редактирование сцен:**
   - Wire WorldClickCatcher (manual, см. выше)
   - Проверить что SceneGraph корректно reload-ит сцену при повторном открытии
   - Убедиться что SceneAutoSaver фактически сохраняет (добавить Debug.Log при тестировании)
3. **Баги из ошибок консоли:**
   - `SceneSerializer migrating v1→v2` — старые сцены, при необходимости пересоздать
