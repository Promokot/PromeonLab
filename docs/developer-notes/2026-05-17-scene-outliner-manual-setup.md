# Scene-Bound Objects + Outliner — Manual Setup Checklist

**Date:** 2026-05-17
**Контекст:** Spec A реализован полностью. Ниже — ручные шаги в Unity Editor, которые код не может сделать сам (prefabs, SO-ассеты, scene-объекты).

После выполнения всех шагов: запустить Test Runner → все unit-тесты зелёные → playmode-валидация (см. конец документа).

---

## Pre-flight

1. **Откройте Unity Editor** на проекте PromeonLab. Дождитесь domain reload.
2. **Window → General → Console → Clear.** Ожидаемо: 0 errors (если есть — стоп, диагностируем).
3. **Window → General → Test Runner → EditMode → Run All.** Ожидаемо: все зелёные. В частности:
   - `SelectionManagerTests` (7 тестов)
   - `AssetRegistryTests` (2 теста)
   - `SceneGraphTests` (1 тест — `CaptureSnapshot_EmptyGraph_ReturnsV2WithEmptyNodes`)
   - `SceneSerializerTests` (3 теста — round-trip v2, null, v1→v2 миграция)
   - Существующие тесты: `CommandStackTests`, `PathProviderTests` — должны быть зелёными.

Если что-то красное — стоп. Дальше не идём.

---

## Manual Task A — Capabilities на BuiltinAssetLibrary entries

**Зачем:** При спавне `IInteractableFactory.MakeInteractable(go, capabilities)` смотрит на `capabilities & AssetCapabilities.Selectable` и без этого флага ничего не вешает. Дефолт у `BuiltinLabAsset._capabilities` = `None` (потому что это struct), поэтому ВСЕ существующие демо-ассеты сейчас невыделяемые.

**Шаги:**

1. В Project view: Search → `t:BuiltinAssetLibrary` → найти ассет (`Assets/_App/DemoAssets/BuiltinAssetLibrary.asset` или похожее).
2. Кликнуть на него → Inspector покажет список `Entries`.
3. У КАЖДОГО entry в массиве: поле `Capabilities` — поставить галки `Selectable` и `Movable`.
4. Ctrl+S сохранить ассет.

**Suggested commit:**
```
chore(demo-assets): set Selectable|Movable capabilities on all builtin entries
```

---

## Manual Task B — Префаб SceneOutlinerRow

**Зачем:** `SceneOutlinerView._rowPrefab` ждёт готовый префаб с компонентом `SceneOutlinerRow`.

**Шаги:**

1. В пустой сцене (или временной): GameObject → UI → Button. Переименовать root в `SceneOutlinerRow`.
2. Структура внутри Button:
   ```
   SceneOutlinerRow (Button + Image background)
   ├── Indent (Empty + LayoutElement) — preferredWidth = 0 (default; будет переписываться кодом)
   ├── Highlight (Image) — enabled = false, color = Clear (overlay для подсветки)
   └── Label (TMP Text - TextMeshPro UGUI) — текст пустой, alignment Left
   ```
3. На root повесить `Horizontal Layout Group` (Child Force Expand: Width=true; Child Alignment Middle Left).
4. Add Component → `SceneOutlinerRow` (свежий компонент). Заполнить поля:
   - `_label` → Label TMP_Text
   - `_highlight` → Highlight Image
   - `_indentSpacer` → Indent's LayoutElement
   - `_button` → root Button
5. Создать папку `Assets/_App/Subsystems/SpatialUi/Prefabs/Rows/` если её нет.
6. Drag root в эту папку → создастся `SceneOutlinerRow.prefab`. Удалить временный объект из сцены.

**Suggested commit:**
```
feat(spatial-ui): SceneOutlinerRow prefab
```

---

## Manual Task C — Outliner + Inspector в ContextMenu prefabs

**Где:** `Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/UserPanel_ContextMenu_VrEditing.prefab` и `UserPanel_ContextMenu_Sandbox.prefab`.

**Если `_Sandbox.prefab` не существует:** Project view → правый клик на `UserPanel_ContextMenu_VrEditing.prefab` → Duplicate → переименовать в `UserPanel_ContextMenu_Sandbox`. Затем убедиться, что в `UserPanel.prefab` массив `_contextMenus` имеет entry для `AppMode.Sandbox` → этот новый prefab.

**Структура (одинаковая для обоих):**

```
ContextMenu_* (root Panel — твоя текущая структура)
├── Outliner (новый child)
│    ├── Header (TMP Text "Outliner")
│    ├── ScrollView (Scroll Rect)
│    │    └── Viewport
│    │         └── Content (Transform — VerticalLayoutGroup) ← _rowsRoot
│    └── (опционально: scrollbar)
│    [Add Component] SceneOutlinerView
│      _rowsRoot → Content
│      _rowPrefab → SceneOutlinerRow.prefab (из Task B)
│      _indentPx → 16
└── Inspector (новый child)
     ├── Header (TMP Text "Inspector")
     ├── EmptyState (Text "Nothing selected") — disabled by default
     └── Content (Vertical Layout Group)
          ├── NameField (TMP_InputField)         + Add Component: VrInputFieldProxy
          ├── TypeLabel (TMP Text)
          ├── PositionLabel (TMP Text)
          ├── RotationLabel (TMP Text)
          └── ScaleLabel (TMP Text)
     [Add Component] SceneInspectorView
       _emptyState     → EmptyState GameObject
       _content        → Content GameObject
       _nameField      → NameField TMP_InputField
       _typeLabel      → TypeLabel TMP_Text
       _positionLabel  → PositionLabel TMP_Text
       _rotationLabel  → RotationLabel TMP_Text
       _scaleLabel     → ScaleLabel TMP_Text
```

**Замечания:**
- `VrInputFieldProxy` уже существует в проекте (см. `docs/developer-notes/vr-keyboard.md`). Без него клавиатура не активируется при тапе по NameField.
- `EmptyState` и `Content` — два разных GameObject; их активность переключается через `SetActive` из `SceneInspectorView.Refresh()`. По умолчанию EmptyState enabled, Content disabled.
- Можно использовать Layout Groups (Vertical) для всего ContextMenu чтобы Outliner и Inspector делили высоту, или Grid.

**Suggested commit:**
```
feat(spatial-ui): Outliner + Inspector inside UserPanel_ContextMenu_{VrEditing,Sandbox} prefabs
```

---

## Manual Task D — WorldClickCatcher на XR Origin

**Зачем:** Реагирует на trigger-press в пустоту → `SelectionManager.Clear()`.

**Шаги (для каждой scene — `VrEditing` и `Sandbox`):**

1. Открыть сцену (или scene-prefab).
2. Найти `XR Origin` GameObject (обычно в корне сцены или внутри XR rig).
3. Add Component → `WorldClickCatcher`.
4. Wire поля:
   - `_leftRay` → XRRayInteractor компонент с **левого** контроллера (drag из children XR Origin)
   - `_rightRay` → то же с правого контроллера
   - `_leftSelectAction` → InputActionReference на trigger select левого. Найти в Input Actions asset (обычно `XRI Default Input Actions` или подобное). Action типа `Select` с binding `<XRController>{LeftHand}/triggerPressed` или подобный.
   - `_rightSelectAction` → то же для right.
5. Сохранить сцену (Ctrl+S).

**Где искать InputActionReference:** обычно создаётся drag-and-drop'ом из `XRI Default Input Actions.inputactions` ассета (внутри project). Открываем actions asset, переходим к нужному action, кнопка `+` создаёт reference в Project, drag его в поле компонента.

**Suggested commit:**
```
feat(vr-interaction): WorldClickCatcher wired on XR Origin in VrEditing + Sandbox scenes
```

---

## Validation: Manual Playmode Test Sweep

После всех Manual Tasks A-D — финальная VR-валидация. В Quest 3 (или XR Simulator).

| # | Действие | Ожидание |
|---|---|---|
| 1 | Play Mode → VrEditing → новая сцена → спавн 3 ассетов | Все 3 видны под `[Spawned]` в Hierarchy. Outliner показывает 3 строки с именами. |
| 2 | Trigger по 1-му ассету | Жёлтый outline на 3D + жёлтая подсветка строки. Inspector показывает имя/тип/transform. |
| 3 | Trigger по 2-му ассету | Оба выделены: 1-й оранжевый, 2-й жёлтый (active). Inspector — данные 2-го. |
| 4 | Trigger по 1-му ещё раз | 1-й убран, остался 2-й (жёлтый). Inspector — 2-й. |
| 5 | Trigger в пустоту | Selection clear. Inspector → "Nothing selected". |
| 6 | Trigger по кнопке UserPanel (например, Settings) | **Selection НЕ сбрасывается.** Кнопка активируется. |
| 7 | Click в NameField → клавиатура → "MyChair" → Submit | Имя обновляется в Outliner и `gameObject.name`. |
| 8 | Exit в MainMenu → снова открыть ту же сцену | Все 3 ассета на месте с теми же positions; "MyChair" сохранилось. |
| 9 | Sandbox: спавн 2 ассетов → exit → снова в Sandbox | **Пусто (in-memory).** |
| 10 | Создать новую сцену → спавн → exit → открыть СТАРУЮ сцену | Старая показывает СВОИ ноды, не новой. |

Если какой-то шаг не сходится — Console обычно даёт подсказку (warning от `SceneGraph` про missing asset, или null reference от ненастроенного DI).

---

## Полный список изменений (для ревью)

### Code changes (Phases 1-4)

| Файл | Тип |
|---|---|
| `_Shared/Data/AssetCapabilities.cs` | new |
| `_Shared/Data/AssetSource.cs` | new |
| `_Shared/Data/AssetRef.cs` | new |
| `_Shared/Data/SelectionVisual.cs` | new |
| `_Shared/Data/NodeData.cs` (note: в `StorageCore/Data/`) | new |
| `_Shared/Events/AppEvents.cs` | edit |
| `_Shared/Interfaces/ILabAsset.cs` | edit |
| `_Shared/Interfaces/ISelectionManager.cs` | edit |
| `_Shared/Interfaces/ISceneGraph.cs` | edit |
| `_Shared/Interfaces/IInteractableFactory.cs` | edit |
| `_Shared/Interfaces/IAssetRegistry.cs` | new |
| `Subsystems/AssetBrowser/AssetRegistry.cs` | new |
| `Subsystems/AssetBrowser/AssetSpawner.cs` | edit |
| `Subsystems/AssetBrowser/AssetImporter.cs` | edit (legacy cleanup) |
| `Subsystems/AssetBrowser/Data/{Builtin,Imported,Saved}LabAsset.cs` | edit (Capabilities field) |
| `Subsystems/SceneComposition/Subsystems.SceneComposition.asmdef` | edit (+StorageCore) |
| `Subsystems/SceneComposition/SceneGraph.cs` | rewrite |
| `Subsystems/SceneComposition/SceneNode.cs` | edit |
| `Subsystems/SceneComposition/SelectionManager.cs` | rewrite (multi-select) |
| `Subsystems/SceneComposition/SceneAutoSaver.cs` | new |
| `Subsystems/SceneComposition/Tests/Subsystems.SceneComposition.Tests.asmdef` | edit (+AssetBrowser, +StorageCore) |
| `Subsystems/SceneComposition/Tests/SelectionManagerTests.cs` | new |
| `Subsystems/SceneComposition/Tests/SceneGraphTests.cs` | new |
| `Subsystems/SceneComposition/Tests/AssetRegistryTests.cs` | new |
| `Subsystems/StorageCore/Data/SceneData.cs` | edit (v2) |
| `Subsystems/StorageCore/Data/NodeData.cs` | new |
| `Subsystems/StorageCore/SceneSerializer.cs` | edit (migration) |
| `Subsystems/StorageCore/Tests/SceneSerializerTests.cs` | edit (v2 + migration tests) |
| `Subsystems/VrInteraction/Subsystems.VrInteraction.asmdef` | edit (+QuickOutline) |
| `Subsystems/VrInteraction/Selectable.cs` | new |
| `Subsystems/VrInteraction/SelectionInteractor.cs` | edit (Toggle) |
| `Subsystems/VrInteraction/SelectionInteractorFactory.cs` | edit (capabilities-aware) |
| `Subsystems/VrInteraction/SelectionVisualSync.cs` | new |
| `Subsystems/VrInteraction/WorldClickCatcher.cs` | new |
| `UnityPacks/QuickOutline/Scripts/QuickOutline.asmdef` | new |
| `Subsystems/SpatialUi/UI_Scripts/UserPanel.cs` | edit (SwapContext scope-aware) |
| `Subsystems/SpatialUi/UI_Scripts/SceneOutlinerRow.cs` | new |
| `Subsystems/SpatialUi/UI_Scripts/SceneOutlinerView.cs` | new |
| `Subsystems/SpatialUi/UI_Scripts/SceneInspectorView.cs` | new |
| `Bootstrap/RootLifetimeScope.cs` | edit (AssetRegistry) |
| `Bootstrap/VrEditingSceneScope.cs` | edit (+AutoSaver, +VisualSync, +ClickCatcher inject) |
| `Bootstrap/SandboxSceneScope.cs` | edit (+VisualSync, +ClickCatcher inject) |

### Manual asset/prefab/scene work (Tasks A-D)

| Где | Что |
|---|---|
| `BuiltinAssetLibrary.asset` entries | Set Capabilities = Selectable\|Movable |
| New: `SpatialUi/Prefabs/Rows/SceneOutlinerRow.prefab` | Create |
| `UserPanel_ContextMenu_VrEditing.prefab` | Add Outliner + Inspector child hierarchies |
| `UserPanel_ContextMenu_Sandbox.prefab` | Same (создать копией если не было) |
| XR Origin in VrEditing scene | Add WorldClickCatcher + wire fields |
| XR Origin in Sandbox scene | Same |
