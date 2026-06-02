# Settings Panel + Rig Locomotion Fix + Controls Map — Design

**Date:** 2026-05-30
**Status:** Approved (design)
**Author:** brainstorming session

## Problem

1. **Локомоция сломана:** оба контроллера отвечают за ходьбу. На базовом XRI-риге
   `Dynamic Move Provider` слушает обе руки, и активны одновременно Snap + Continuous
   Turn провайдеры на обеих руках. Нужна чёткая схема: **левый стик — движение,
   правый стик — поворот (continuous)**.
2. **Нет карты управления:** биндинги нигде не зафиксированы как данные. Нужен
   «фиксированный файл», к которому можно вернуться и на его основе переделать настройки.
3. **Пустая Settings-панель:** `SettingsPanel.cs` — заглушка (`// Settings UI content
   goes here`). Префаб панели существует в region-системе, но контента нет. Нужен
   master-detail UI (левый rail с разделами, справа содержимое), разделы **General** и
   **Bindings**.

## Goals

- Левый стик = smooth move; правый стик = continuous turn; snap turn выключен.
- Единый источник правды для биндов — `ControlsProfile` (ScriptableObject).
- Settings-панель с двумя вкладками; Bindings рендерится из профиля в рантайме,
  General — плейсхолдер.
- Markdown-зеркало карты биндов, генерируемое из профиля (editor tool).

## Non-Goals (YAGNI)

- Рантайм-ребайндинг (изменение биндов пользователем).
- Сохранение пользовательских настроек на диск.
- Vertical-fly (вертикальное движение стиком), comfort-винетка, наполнение General.
- Графические настройки.

## Constraints

- Менять параметры рига **только как prefab overrides на варианте**
  `Assets/_App/Content/Prefabs/XR/User XR Origin (XR Rig).prefab` — базовый XRI-сэмпл
  не трогаем (его перезатрёт реимпорт пакета).
- Соблюдать конвенции CLAUDE.md: SO только для config/profile (suffix `Profile`),
  один публичный тип на файл, нет `#if UNITY_EDITOR` в рантайме (editor-код в `Editor/`),
  `[SerializeField] private`, кросс-субсистемные границы.
- Карта биндов отражает **фактические** бинды приложения (инвентаризация из кода),
  а не пример-мокап из задачи (мокап — только layout-референс).
- Реализация — через Unity MCP (по запросу пользователя).

## Architecture

```
ControlsProfile.asset  ──serialized ref──▶  SettingsPanel ──▶ BindingRow × N (runtime UI)
        │
        └──editor exporter──▶ docs/controls-bindings.md (reference doc)
```

### Часть 1 — Фикс рига (локомоция)

Правки как prefab overrides на `User XR Origin (XR Rig)` (там уже есть override
`m_SmoothTurnEnabled`):

| Провайдер | Было | Станет |
|---|---|---|
| Dynamic Move Provider | обе руки (`m_LeftHandMoveInput` + `m_RightHandMoveInput`) | **только левая** (правый move-инпут отключён/очищен) |
| Continuous Turn Provider | обе руки / выкл | **только правая**, включён |
| Snap Turn Provider | активен | **выключен** |

**Механизм (определён чтением `ControllerInputActionManager`):** бинды move/turn
управляются не только провайдерами, а флагами CIAM на каждом контроллере
(`UpdateLocomotionActions`: `Turn` включается только при `!SmoothMotion && SmoothTurn`).
Поэтому primary-фикс — флаги CIAM: **левый** `SmoothMotion=true, SmoothTurn=false`
(move on, turn off); **правый** `SmoothMotion=false, SmoothTurn=true` (move off, continuous
turn on) + очистка `m_TeleportMode`/`m_TeleportModeCancel` на правом (чтобы teleport-режим
не активировался). Fallback — очистка per-hand input source mode на провайдерах + disable
SnapTurnProvider. Детали — в плане (Task 3).

### Часть 2 — `ControlsProfile` (источник правды)

Наполняем субсистему `InputBindings/` (сейчас только placeholder):

- **`Assets/_App/Scripts/InputBindings/Data/ControlBindingCategory.cs`** — enum:
  `Movement`, `Selection`, `System`.
- **`Assets/_App/Scripts/InputBindings/Data/ControlHand.cs`** — enum:
  `Left`, `Right`, `Both`, `Any`.
- **`Assets/_App/Scripts/InputBindings/Data/ControlBinding.cs`** — `[Serializable]`
  struct: `Category`, `Action` (string), `Description` (string), `Hand`,
  `InputLabel` (string, напр. «stick», «trigger», «grip + trigger»),
  `IconId` (string, опц. — id иконки Tabler для UI).
- **`Assets/_App/Scripts/InputBindings/ControlsProfile.cs`** — `ScriptableObject`,
  поля: `int schemaVersion`, `ControlBinding[] bindings`. Только чтение в рантайме.
- **Asset:** `Assets/_App/Content/ScriptableObjects/ControlsProfile.asset`, заполнен
  фактическими биндами (инвентаризация из XRPromeonInteractable, hotkey-обработчиков,
  локомоция-провайдеров на этапе реализации).

Панель получает профиль через `[SerializeField]`-ссылку (статичные display-данные,
DI не требуется).

#### Предварительная карта биндов (verify-against-code на этапе реализации)

Из памяти проекта (`project_interaction_input_model`, `project_hotkeys`) — подлежит
сверке с кодом:

| Category | Action | Hand | Input |
|---|---|---|---|
| Movement | Smooth move | Left | stick |
| Movement | Turn (continuous) | Right | stick ⟷ |
| Selection | Select | Right | tap trigger |
| Selection | Rotate selected | Right | hold trigger |
| Selection | Move selected | Right | hold grip |
| System | Undo | — | Ctrl+Z |
| System | User panel | Left/Right | primaryButton (X/A) |

### Часть 3 — Settings UI (master-detail)

Перестраиваем `SettingsPanel.cs` + префаб (размер панели текущий; цвета/токены — как у
`AssetBrowserPanel`):

```
SettingsPanel
├── Rail (левая колонка, ~220px)   →  [General] [Bindings]
└── Content (правая)
    ├── General  →  плейсхолдер ("Coming soon")
    └── Bindings →  ScrollView; группы-секции (Movement / Selection / System);
                    строки из ControlsProfile через row-prefab
```

- **`SettingsPanel.cs`** — `[SerializeField] private`: rail-кнопки (`Button`),
  два content-контейнера (`GameObject`), `ControlsProfile`, `_bindingRowPrefab`,
  `_groupHeaderPrefab`, `_bindingsRoot` (Transform). На `Start()`/`Awake()` строит
  список биндов сгруппированно по `Category`; переключение вкладок — toggle видимости
  двух контейнеров + active-стиль кнопок rail.
- **`Assets/_App/Scripts/SpatialUi/Elements/BindingRow.cs`** — мелкий элемент:
  иконка + name + description + бейдж (рука + input-label); `Bind(ControlBinding)`.
- Стиль кнопок rail — как `RegionNavButton`/существующие кнопки SpatialUi.

### Часть 4 — Markdown-экспортёр

- **`Assets/_App/Editor/Tooling/ControlsProfileExporter.cs`** — editor menu item
  (`Tools/Promeon/Export Controls Doc`): читает `ControlsProfile.asset`, пишет
  `docs/controls-bindings.md` (таблица по категориям). Держит «файл, к которому
  возвращаемся» синхронным с SO; editor-код в `Editor/` (нет рантайм-`#if`).

## Data Flow

1. `ControlsProfile.asset` заполнен дизайнером/разработчиком в инспекторе.
2. Рантайм: `SettingsPanel` читает массив `bindings`, группирует по `Category`,
   инстанцирует `BindingRow` под `_bindingsRoot`.
3. Editor: `ControlsProfileExporter` экспортирует тот же профиль в markdown.

## Error Handling

- `SettingsPanel`: если `ControlsProfile` не назначен — лог-warning, секция Bindings
  пустая (без краша). Null-guard на row-prefab/контейнеры (паттерн `?.` как в
  существующих панелях).
- Exporter: если asset не найден — `EditorUtility.DisplayDialog` с сообщением, без
  записи файла.

## Testing

- Ручная проверка в редакторе/гарнитуре: левый стик двигает, правый поворачивает
  (continuous), snap не срабатывает.
- Визуальная проверка панели: переключение General/Bindings, корректные группы и строки.
- Сверка `docs/controls-bindings.md` с UI после экспорта (должны совпадать — общий
  источник).
- Юнит-тесты не предусмотрены (UI/конфиг-данные; логики для изоляции нет).

## Files

**New:**
- `Assets/_App/Scripts/InputBindings/Data/ControlBindingCategory.cs`
- `Assets/_App/Scripts/InputBindings/Data/ControlHand.cs`
- `Assets/_App/Scripts/InputBindings/Data/ControlBinding.cs`
- `Assets/_App/Scripts/InputBindings/ControlsProfile.cs`
- `Assets/_App/Content/ScriptableObjects/ControlsProfile.asset`
- `Assets/_App/Scripts/SpatialUi/Elements/BindingRow.cs`
- `Assets/_App/Editor/Tooling/ControlsProfileExporter.cs`
- `docs/controls-bindings.md` (generated)

**Modified:**
- `Assets/_App/Scripts/SpatialUi/Panels/SettingsPanel.cs` (placeholder → master-detail)
- `Assets/_App/Content/Prefabs/XR/User XR Origin (XR Rig).prefab` (locomotion overrides)
- Settings-панель префаб (UI rebuild)
- `Assets/_App/Scripts/InputBindings/InputBindings.cs` (удалить placeholder, если мешает)
