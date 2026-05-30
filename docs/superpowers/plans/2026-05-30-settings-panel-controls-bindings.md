# Settings Panel + Rig Locomotion Fix + Controls Map — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Починить локомоцию рига (левый стик — движение, правый — continuous turn, snap off), зафиксировать карту управления как `ControlsProfile` (ScriptableObject) + markdown-зеркало, и перестроить пустую Settings-панель в master-detail UI (General плейсхолдер / Bindings из профиля).

**Architecture:** `ControlsProfile.asset` — единый источник правды; `SettingsPanel` рендерит строки `BindingRow` из него в рантайме; editor-экспортёр пишет `docs/controls-bindings.md` из того же ассета. Локомоция чинится через флаги `ControllerInputActionManager` на левом/правом контроллере варианта рига `User XR Origin (XR Rig)`.

**Tech Stack:** Unity 6000.3.7f1, C# (no namespaces, `_App.Runtime`), VContainer, TMP/uGUI, XR Interaction Toolkit 3.0.7 Starter Assets, Unity MCP для правок ассетов/префабов.

---

## ✅ Status: COMPLETED (2026-05-30)

Все 8 задач выполнены и проверены пользователем в гарнитуре. Реализовано subagent-driven (двухстадийное ревью на задачу). Итог по факту:

- **Локомоция (Task 3):** фикс через флаги `ControllerInputActionManager` (а не очистку per-hand input, как предполагала исходная спека) — левый `SmoothMotion=true/SmoothTurn=false`, правый `SmoothMotion=false/SmoothTurn=true` + очищены teleport-рефы. Подтверждено в гарнитуре.
- **Данные:** `ControlsProfile.asset` заполнен **фактическими** биндами приложения (Movement: Move L / Turn R; Selection: Select/Rotate/Move object/Deselect; System: User panel/Undo) — §7 визуального референса использован только как layout, НЕ как данные.
- **UI:** карточки-секции (`BindingSection.prefab` + `BindingSectionCard`) + строки с pill и L/R-бейджем (`BindingRow.prefab`), master-detail в `SettingsModule.prefab` (560×300, region-swap в UserPanel/Center_top). Иконок нет (v1). Углы острые (rounded-спрайт через MCP не назначился).
- **Экспортёр:** `Tools/Promeon/Export Controls Doc` → `docs/controls-bindings.md` (зеркало ассета).

Отклонения/доп-правки по ходу:
- Прерванный фоновый агент создал дубли (`BindingSectionCard.prefab`, `BindingGroupHeader.prefab`) и мусор (`fix_prefab*.py`) — удалены; `SettingsModule.prefab` пересобран начисто детерминированно.
- По запросу пользователя: дефолтная вкладка — **General**; заголовки контента и секций — PascalCase.
- Память: [[project_locomotion_scheme]], [[project_controls_settings_system]].

---

## Project-specific execution notes (read before starting)

- **Все правки сцен/префабов/ассетов — через Unity MCP** (запрос пользователя). Перед началом: `mcp__unityMCP__set_active_instance` (или передавать `unity_instance` в каждый вызов), затем `mcp__unityMCP__read_console` для базовой проверки.
- **Нет pytest/TDD-цикла** — это Unity-проект. «Verify» = `read_console` на ошибки компиляции после изменения скриптов + ручная проверка в Play Mode / гарнитуре. Юнит-тестов не пишем (UI/конфиг-данные).
- **Git не трогаем.** Пользователь коммитит сам (`feedback_no_auto_commits`, `feedback_no_git_during_dev`). Вместо шагов "Commit" — **CHECKPOINT** для ревью/проверки пользователем.
- **MCP-квирки** (`project_unity_mcp_quirks`): `manage_asset` move возвращает false-но-успешно (проверять через Glob); `execute_code` ненадёжен на длинных путях — предпочитать специализированные tools (`manage_gameobject`, `manage_components`, `manage_prefabs`, `manage_scriptable_object`, `manage_ui`). После каждого изменения скрипта ждать окончания компиляции (поле `isCompiling` в `editor_state`).
- **Конвенции CLAUDE.md:** `[SerializeField] private` (не public), один публичный тип на файл, имя файла = имя типа, нет `#if UNITY_EDITOR` в рантайме (editor-код в `Editor/`), нет `Resources.Load`/`FindObjectOfType`/синглтонов, нет запрещённых суффиксов (`Manager/Handler/Controller/...`).

---

## File Structure

**New (runtime — субсистема InputBindings):**
- `Assets/_App/Scripts/InputBindings/Data/ControlBindingCategory.cs` — enum категорий.
- `Assets/_App/Scripts/InputBindings/Data/ControlHand.cs` — enum руки.
- `Assets/_App/Scripts/InputBindings/Data/ControlBinding.cs` — `[Serializable]` запись бинда.
- `Assets/_App/Scripts/InputBindings/ControlsProfile.cs` — ScriptableObject-контейнер.

**New (runtime — UI элемент):**
- `Assets/_App/Scripts/SpatialUi/Elements/BindingRow.cs` — строка бинда.

**New (editor):**
- `Assets/_App/Editor/Tooling/ControlsProfileExporter.cs` — menu item экспорта в markdown.

**New (assets):**
- `Assets/_App/Content/ScriptableObjects/ControlsProfile.asset` — заполненный профиль.
- `Assets/_App/Content/Prefabs/UI/BindingRow.prefab` — префаб строки.
- `docs/controls-bindings.md` — генерируемое markdown-зеркало.

**Modified:**
- `Assets/_App/Scripts/SpatialUi/Panels/SettingsPanel.cs` — placeholder → master-detail логика.
- `Assets/_App/Content/Prefabs/UI/Panels/UserPanel/SettingsModule.prefab` — перестройка UI (rail + content + bindings list), проводка ссылок.
- `Assets/_App/Content/Prefabs/XR/User XR Origin (XR Rig).prefab` — флаги локомоции (overrides).
- `Assets/_App/Scripts/InputBindings/InputBindings.cs` — удалить placeholder-тип.

**Inventory фактических биндов (зафиксировано чтением кода — использовать как данные для Task 4):**

| Category | Action | Hand | InputLabel | Description | Источник |
|---|---|---|---|---|---|
| Movement | Move | Left | `stick` | Ходьба: вперёд/назад + стрейф | DynamicMoveProvider + Left CIAM |
| Movement | Turn | Right | `stick ⟷` | Плавный поворот вида | ContinuousTurnProvider + Right CIAM |
| Selection | Select | Any | `tap trigger` | Навести луч и тапнуть по объекту/кости | `XRPromeonInteractable` (activateInput tap < 0.5s) |
| Selection | Rotate | Any | `hold trigger` | Удерживать триггер на выбранном — вращение | `XRPromeonInteractable` (TriggerRotate) |
| Selection | Move object | Any | `hold grip` | Удерживать grip на выбранном — перетаскивание | `XRPromeonInteractable` (GripMove, selectInput) |
| Selection | Deselect | Any | `tap trigger (empty)` | Тап по пустому месту — снять выделение | `WorldClickCatcher` |
| System | User panel | Both | `X / A` | Открыть/закрыть пользовательскую панель | `UserPanelOpener` (primaryButton) |
| System | Undo | None | `Ctrl + Z` | Отменить последнее действие (клавиатура) | `UndoKeyHandler` |

---

## Task 1: ControlsProfile data types + ScriptableObject

**Files:**
- Create: `Assets/_App/Scripts/InputBindings/Data/ControlBindingCategory.cs`
- Create: `Assets/_App/Scripts/InputBindings/Data/ControlHand.cs`
- Create: `Assets/_App/Scripts/InputBindings/Data/ControlBinding.cs`
- Create: `Assets/_App/Scripts/InputBindings/ControlsProfile.cs`
- Modify: `Assets/_App/Scripts/InputBindings/InputBindings.cs` (удалить placeholder)

- [ ] **Step 1: Создать enum категорий**

`Assets/_App/Scripts/InputBindings/Data/ControlBindingCategory.cs`:
```csharp
public enum ControlBindingCategory
{
    Movement,
    Selection,
    System,
}
```

- [ ] **Step 2: Создать enum руки**

`Assets/_App/Scripts/InputBindings/Data/ControlHand.cs`:
```csharp
public enum ControlHand
{
    None,   // клавиатура / без контроллера (бейдж руки скрыт)
    Left,
    Right,
    Both,
    Any,    // любая рука (луч)
}
```

- [ ] **Step 3: Создать запись бинда**

`Assets/_App/Scripts/InputBindings/Data/ControlBinding.cs`:
```csharp
using System;

[Serializable]
public struct ControlBinding
{
    public ControlBindingCategory Category;
    public string                 Action;       // короткое название, напр. "Move"
    public string                 Description;  // пояснение для строки
    public ControlHand            Hand;
    public string                 InputLabel;   // напр. "stick", "tap trigger", "X / A"
    public string                 IconId;       // опц. id иконки (для md/будущего UI); может быть пустым
}
```

- [ ] **Step 4: Создать ScriptableObject-контейнер**

`Assets/_App/Scripts/InputBindings/ControlsProfile.cs`:
```csharp
using UnityEngine;

[CreateAssetMenu(menuName = "PromeonLab/ControlsProfile")]
public class ControlsProfile : ScriptableObject
{
    [SerializeField] private int             _schemaVersion = 1;
    [SerializeField] private ControlBinding[] _bindings;

    public int             SchemaVersion => _schemaVersion;
    public ControlBinding[] Bindings     => _bindings;
}
```

- [ ] **Step 5: Удалить placeholder InputBindings**

Заменить всё содержимое `Assets/_App/Scripts/InputBindings/InputBindings.cs` на пустой комментарий-маркер (файл оставляем, чтобы не трогать .meta-GUID; placeholder-тип убираем, т.к. субсистема теперь наполнена):
```csharp
// InputBindings subsystem — data types live in Data/; см. ControlsProfile.cs
```

- [ ] **Step 6: Verify — компиляция**

MCP: `mcp__unityMCP__refresh_unity`, дождаться `editor_state.isCompiling == false`, затем `mcp__unityMCP__read_console` (filter Error).
Expected: ноль ошибок компиляции, тип `ControlsProfile` доступен.

- [ ] **Step 7: CHECKPOINT** — показать пользователю созданные типы, дождаться ОК.

---

## Task 2: ControlsProfile.asset + наполнение биндами

**Files:**
- Create: `Assets/_App/Content/ScriptableObjects/ControlsProfile.asset`

- [ ] **Step 1: Создать ассет**

MCP: `mcp__unityMCP__manage_scriptable_object` — create экземпляр `ControlsProfile` по пути `Assets/_App/Content/ScriptableObjects/ControlsProfile.asset`.
Expected: ассет создан (проверить `Glob` по пути — учитывая квирк false-но-успешно).

- [ ] **Step 2: Заполнить массив `_bindings`**

Записать 8 записей строго по таблице Inventory выше (порядок: Movement ×2, Selection ×4, System ×2). `_schemaVersion = 1`. Значения полей:

```
[0] Movement | "Move"        | "Ходьба: вперёд/назад + стрейф"           | Left  | "stick"             | ""
[1] Movement | "Turn"        | "Плавный поворот вида"                    | Right | "stick ⟷"           | ""
[2] Selection| "Select"      | "Навести луч и тапнуть по объекту/кости"  | Any   | "tap trigger"       | ""
[3] Selection| "Rotate"      | "Удерживать триггер на выбранном — вращение" | Any | "hold trigger"      | ""
[4] Selection| "Move object" | "Удерживать grip на выбранном — перетаскивание" | Any | "hold grip"      | ""
[5] Selection| "Deselect"    | "Тап по пустому месту — снять выделение"  | Any   | "tap trigger (empty)" | ""
[6] System   | "User panel"  | "Открыть/закрыть пользовательскую панель" | Both  | "X / A"             | ""
[7] System   | "Undo"        | "Отменить последнее действие (клавиатура)"| None  | "Ctrl + Z"          | ""
```

- [ ] **Step 3: Verify**

MCP: `mcp__unityMCP__manage_scriptable_object` read по пути → проверить, что `_bindings.Length == 8` и первая запись `Category=Movement, Action="Move", Hand=Left`.
Expected: массив из 8 записей, значения совпадают.

- [ ] **Step 4: CHECKPOINT** — показать содержимое ассета, дождаться ОК.

---

## Task 3: Фикс локомоции рига (CIAM-флаги)

**Files:**
- Modify: `Assets/_App/Content/Prefabs/XR/User XR Origin (XR Rig).prefab` (prefab overrides)

**Целевая схема** (через `ControllerInputActionManager`, логика `UpdateLocomotionActions`):

| Контроллер | `m_SmoothMotionEnabled` | `m_SmoothTurnEnabled` | Результат |
|---|---|---|---|
| Левый  | `true`  | `false` | Move on; Turn off (smoothMotion подавляет turn); Snap off |
| Правый | `false` | `true`  | Move off; Turn (continuous) on; Snap off; teleport нейтрализуем |

Правый `m_SmoothMotionEnabled=false` включил бы teleport-режим — нейтрализуем, очистив `m_TeleportMode` и `m_TeleportModeCancel` (InputActionReference → None) на правом CIAM, чтобы teleport-луч не активировался и не глушил selection-луч.

- [ ] **Step 1: Live-inspect рига**

Открыть префаб (`mcp__unityMCP__manage_prefabs` open / `manage_gameobject` find) `User XR Origin (XR Rig)`. Найти оба `ControllerInputActionManager` (по одному на Left/Right контроллере). Прочитать их компоненты (`mcp__unityMCP__manage_components` get): зафиксировать, **который под левым, который под правым** контроллером, и текущие значения `m_SmoothMotionEnabled` / `m_SmoothTurnEnabled` / `m_TeleportMode` / `m_TeleportModeCancel`.
Expected: понятно соответствие fileID → рука (ранее в YAML: `4778211696441940833` и `5663893676086941514` — определить какой какой через родительский GameObject).

- [ ] **Step 2: Настроить ЛЕВЫЙ CIAM**

`manage_components` set на левом `ControllerInputActionManager`:
- `m_SmoothMotionEnabled = true`
- `m_SmoothTurnEnabled = false`

- [ ] **Step 3: Настроить ПРАВЫЙ CIAM**

`manage_components` set на правом `ControllerInputActionManager`:
- `m_SmoothMotionEnabled = false`
- `m_SmoothTurnEnabled = true`
- `m_TeleportMode = None` (очистить InputActionReference)
- `m_TeleportModeCancel = None` (очистить InputActionReference)

- [ ] **Step 4: Сохранить префаб**

`mcp__unityMCP__manage_prefabs` save/apply. Убедиться, что изменения записаны как overrides на варианте (не в базовый XRI-сэмпл).

- [ ] **Step 5: Verify — Play Mode**

`mcp__unityMCP__manage_editor` enter Play Mode (или ручная проверка в гарнитуре). Проверить:
- Левый стик — движется (вперёд/назад/стрейф), НЕ поворачивает.
- Правый стик влево/вправо — плавный поворот; правый стик НЕ двигает игрока.
- Snap-рывков нет; teleport-луч не появляется на правом.
- `read_console`: нет ошибок/варнингов про teleport interactor.
Expected: схема «левый — ходьба, правый — поворот» работает.

- [ ] **Step 6: Fallback (если turn не включился)**

Если правый не поворачивает (напр. strafe/иная логика перебивает): дополнительно на провайдерах варианта —
- DynamicMoveProvider (`Move` GO, fileID `153982007679157697`): `m_RightHandMoveInput.m_InputSourceMode = 0` (Unused).
- ContinuousTurnProvider (`Turn` GO, fileID `6480925242510836759`): `m_LeftHandTurnInput.m_InputSourceMode = 0` (Unused).
- SnapTurnProvider (`Turn` GO, fileID `7347985736721345035`): `m_Enabled = 0`.
Повторить Step 5.

- [ ] **Step 7: CHECKPOINT** — пользователь подтверждает в гарнитуре, что движение/поворот корректны.

---

## Task 4: BindingRow — скрипт + префаб

**Files:**
- Create: `Assets/_App/Scripts/SpatialUi/Elements/BindingRow.cs`
- Create: `Assets/_App/Content/Prefabs/UI/BindingRow.prefab`

- [ ] **Step 1: Создать скрипт BindingRow**

`Assets/_App/Scripts/SpatialUi/Elements/BindingRow.cs`:
```csharp
using TMPro;
using UnityEngine;

// Одна строка в списке биндов: название + описание + бейдж (рука + input-label).
// Иконка опциональна и в v1 не используется (поле IconId хранится в данных для md/будущего).
public class BindingRow : MonoBehaviour
{
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _descriptionText;
    [SerializeField] private TMP_Text _handText;    // L / R / L+R / "" 
    [SerializeField] private TMP_Text _inputText;   // напр. "tap trigger"

    public void Bind(ControlBinding binding)
    {
        if (_nameText != null)        _nameText.text        = binding.Action;
        if (_descriptionText != null) _descriptionText.text = binding.Description;
        if (_handText != null)        _handText.text        = HandLabel(binding.Hand);
        if (_inputText != null)       _inputText.text       = binding.InputLabel;
    }

    private static string HandLabel(ControlHand hand) => hand switch
    {
        ControlHand.Left  => "L",
        ControlHand.Right => "R",
        ControlHand.Both  => "L+R",
        ControlHand.Any   => "•",
        _                 => string.Empty, // None
    };
}
```

- [ ] **Step 2: Verify — компиляция**

`refresh_unity` → дождаться компиляции → `read_console` (Error).
Expected: ноль ошибок.

- [ ] **Step 3: Построить префаб BindingRow**

MCP (`manage_gameobject` + `manage_ui`): создать UI-строку под `Assets/_App/Content/Prefabs/UI/BindingRow.prefab`:
- Root: RectTransform + `HorizontalLayoutGroup` + `LayoutElement` (min height ~40), компонент `BindingRow`.
- Дети (TMP_Text, шрифт/цвет как в `AssetBrowserPanel`/других панелях SpatialUi — взять токены оттуда):
  - `Name` (flexible width), `Description` (под именем — допускается вертикальный под-контейнер: Name сверху, Description снизу мелким вторичным цветом),
  - `Hand` (бейдж, фикс. ширина ~28), `Input` (фикс. ширина ~130, выравнивание по правому краю, цвет-акцент как у info-кнопок).
- Проставить ссылки `_nameText/_descriptionText/_handText/_inputText` на компоненте `BindingRow`.

- [ ] **Step 4: Verify** — визуально открыть префаб, убедиться, что layout читаемый и ссылки заполнены (`manage_components` get на `BindingRow`).

- [ ] **Step 5: CHECKPOINT** — показать префаб строки, дождаться ОК.

---

## Task 5: SettingsPanel — master-detail логика

**Files:**
- Modify: `Assets/_App/Scripts/SpatialUi/Panels/SettingsPanel.cs`

- [ ] **Step 1: Переписать SettingsPanel.cs**

Полное содержимое:
```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Master-detail Settings панель: левый rail (General / Bindings), справа контент.
// General — плейсхолдер. Bindings рендерится из ControlsProfile в рантайме.
public class SettingsPanel : MonoBehaviour
{
    [Header("Rail")]
    [SerializeField] private Button _generalTabButton;
    [SerializeField] private Button _bindingsTabButton;

    [Header("Content")]
    [SerializeField] private GameObject _generalContent;
    [SerializeField] private GameObject _bindingsContent;

    [Header("Bindings")]
    [SerializeField] private ControlsProfile _profile;
    [SerializeField] private Transform       _bindingsRoot;     // контейнер с VerticalLayoutGroup
    [SerializeField] private BindingRow      _rowPrefab;
    [SerializeField] private GameObject      _groupHeaderPrefab; // опц.: заголовок группы (TMP_Text внутри)

    [Header("Tab Highlight")]
    [SerializeField] [Range(0f, 2f)] private float _activeBrightness = 0.6f;

    private void Awake()
    {
        _generalTabButton?.onClick.AddListener(ShowGeneral);
        _bindingsTabButton?.onClick.AddListener(ShowBindings);
    }

    private void Start()
    {
        BuildBindings();
        ShowBindings(); // по умолчанию открыта вкладка Bindings
    }

    private void OnDestroy()
    {
        _generalTabButton?.onClick.RemoveListener(ShowGeneral);
        _bindingsTabButton?.onClick.RemoveListener(ShowBindings);
    }

    private void ShowGeneral()
    {
        SetActiveTab(general: true);
    }

    private void ShowBindings()
    {
        SetActiveTab(general: false);
    }

    private void SetActiveTab(bool general)
    {
        if (_generalContent != null)  _generalContent.SetActive(general);
        if (_bindingsContent != null) _bindingsContent.SetActive(!general);
        HighlightTab(_generalTabButton, general);
        HighlightTab(_bindingsTabButton, !general);
    }

    private static void HighlightTab(Button button, bool active)
    {
        if (button == null) return;
        var image = button.targetGraphic as Graphic;
        if (image == null) return;
        var c = image.color;
        c.a = active ? 1f : 0.5f;
        image.color = c;
    }

    private void BuildBindings()
    {
        if (_bindingsRoot == null || _rowPrefab == null) return;

        foreach (Transform child in _bindingsRoot)
            Destroy(child.gameObject);

        if (_profile == null)
        {
            Debug.LogWarning("[SettingsPanel] ControlsProfile not assigned — bindings list empty.", this);
            return;
        }

        ControlBindingCategory? currentGroup = null;
        foreach (var binding in _profile.Bindings)
        {
            if (currentGroup != binding.Category)
            {
                currentGroup = binding.Category;
                SpawnGroupHeader(binding.Category);
            }

            var row = Instantiate(_rowPrefab, _bindingsRoot);
            row.Bind(binding);
        }
    }

    private void SpawnGroupHeader(ControlBindingCategory category)
    {
        if (_groupHeaderPrefab == null) return;
        var header = Instantiate(_groupHeaderPrefab, _bindingsRoot);
        var label  = header.GetComponentInChildren<TMPro.TMP_Text>();
        if (label != null) label.text = category.ToString();
    }
}
```

- [ ] **Step 2: Verify — компиляция**

`refresh_unity` → дождаться компиляции → `read_console` (Error).
Expected: ноль ошибок.

- [ ] **Step 3: CHECKPOINT** — показать скрипт, дождаться ОК.

---

## Task 6: Перестройка SettingsModule.prefab (UI)

**Files:**
- Modify: `Assets/_App/Content/Prefabs/UI/Panels/UserPanel/SettingsModule.prefab`

Размер панели — текущий (не менять). Цвета/токены — как у других модулей UserPanel/AssetBrowser.

- [ ] **Step 1: Live-inspect текущего префаба**

`manage_gameobject` / `manage_prefabs` open `SettingsModule.prefab`. Зафиксировать корневой RectTransform, наличие компонента `SettingsPanel`, текущую иерархию (вероятно почти пустая).

- [ ] **Step 2: Построить master-detail иерархию**

Под корнем панели (`HorizontalLayoutGroup` на контейнере):
- `Rail` (левая колонка, ширина ~220, вертикальный layout): две кнопки `GeneralTab`, `BindingsTab` (стиль/цвета как у nav-кнопок SpatialUi; TMP-лейблы "General"/"Bindings").
- `Content` (flexible):
  - `GeneralContent` (GameObject): заголовок "General" + TMP-текст "Coming soon".
  - `BindingsContent` (GameObject): заголовок "Bindings" + `ScrollRect` (vertical) с `Viewport/Content`, где `Content` имеет `VerticalLayoutGroup` + `ContentSizeFitter` (vertical preferred). Этот `Content` = `_bindingsRoot`.

- [ ] **Step 3: (опц.) Группа-заголовок префаб**

Создать простой `GroupHeader` (TMP_Text жирным, вторичный цвет) — либо отдельным мини-префабом `Assets/_App/Content/Prefabs/UI/BindingGroupHeader.prefab`, либо оставить `_groupHeaderPrefab` пустым (тогда группы не показываются — допустимо для v1). Рекомендуется создать — улучшает читаемость.

- [ ] **Step 4: Проводка ссылок SettingsPanel**

На компоненте `SettingsPanel` (`manage_components` set) проставить:
- `_generalTabButton` → Rail/GeneralTab
- `_bindingsTabButton` → Rail/BindingsTab
- `_generalContent` → Content/GeneralContent
- `_bindingsContent` → Content/BindingsContent
- `_profile` → `Assets/_App/Content/ScriptableObjects/ControlsProfile.asset`
- `_bindingsRoot` → BindingsContent/ScrollRect/Viewport/Content
- `_rowPrefab` → `Assets/_App/Content/Prefabs/UI/BindingRow.prefab`
- `_groupHeaderPrefab` → BindingGroupHeader.prefab (или None)

- [ ] **Step 5: Сохранить префаб** (`manage_prefabs` save).

- [ ] **Step 6: Verify — Play Mode**

Войти в Play Mode, открыть UserPanel (X/A), открыть Settings-модуль. Проверить:
- Видны две вкладки; по умолчанию активна Bindings со списком из 8 строк, сгруппированных Movement/Selection/System.
- Клик по General → показывает "Coming soon", подсветка вкладок переключается.
- Клик обратно по Bindings → список снова виден; скролл работает.
- `read_console`: нет ошибок, нет варнинга "ControlsProfile not assigned".
Expected: панель функциональна, строки совпадают с ассетом.

- [ ] **Step 7: CHECKPOINT** — пользователь подтверждает вид/поведение панели.

---

## Task 7: Editor-экспортёр в markdown

**Files:**
- Create: `Assets/_App/Editor/Tooling/ControlsProfileExporter.cs`
- Create (генерируется): `docs/controls-bindings.md`

- [ ] **Step 1: Создать экспортёр**

`Assets/_App/Editor/Tooling/ControlsProfileExporter.cs`:
```csharp
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Editor-only: пишет docs/controls-bindings.md из ControlsProfile.asset.
// Держит «файл, к которому возвращаемся» синхронным с единым источником правды.
public static class ControlsProfileExporter
{
    private const string ProfilePath = "Assets/_App/Content/ScriptableObjects/ControlsProfile.asset";
    private const string OutputRelative = "../docs/controls-bindings.md"; // относительно Assets/

    [MenuItem("Tools/Promeon/Export Controls Doc")]
    public static void Export()
    {
        var profile = AssetDatabase.LoadAssetAtPath<ControlsProfile>(ProfilePath);
        if (profile == null)
        {
            EditorUtility.DisplayDialog("Export Controls Doc",
                $"ControlsProfile not found at:\n{ProfilePath}", "OK");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("# Controls — Bindings Map");
        sb.AppendLine();
        sb.AppendLine($"_Generated from `{ProfilePath}` (schemaVersion {profile.SchemaVersion}). " +
                      "Do not edit by hand — edit the ControlsProfile asset and re-run " +
                      "`Tools/Promeon/Export Controls Doc`._");
        sb.AppendLine();

        ControlBindingCategory? group = null;
        foreach (var b in profile.Bindings)
        {
            if (group != b.Category)
            {
                group = b.Category;
                sb.AppendLine();
                sb.AppendLine($"## {b.Category}");
                sb.AppendLine();
                sb.AppendLine("| Action | Hand | Input | Description |");
                sb.AppendLine("|---|---|---|---|");
            }
            sb.AppendLine($"| {b.Action} | {b.Hand} | {b.InputLabel} | {b.Description} |");
        }

        var outputPath = Path.GetFullPath(Path.Combine(Application.dataPath, OutputRelative));
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        File.WriteAllText(outputPath, sb.ToString());
        Debug.Log($"[ControlsProfileExporter] Wrote {outputPath}");
        EditorUtility.RevealInFinder(outputPath);
    }
}
```

- [ ] **Step 2: Verify — компиляция**

`refresh_unity` → дождаться компиляции → `read_console` (Error).
Expected: ноль ошибок; пункт меню `Tools/Promeon/Export Controls Doc` доступен.

- [ ] **Step 3: Запустить экспорт**

MCP: `mcp__unityMCP__execute_menu_item` → `Tools/Promeon/Export Controls Doc`.
Expected: создан `docs/controls-bindings.md`.

- [ ] **Step 4: Verify — содержимое md**

`Read` `docs/controls-bindings.md`: три секции (Movement/Selection/System), всего 8 строк, значения совпадают с `ControlsProfile.asset`.
Expected: md = зеркало ассета.

- [ ] **Step 5: CHECKPOINT** — показать md, дождаться ОК.

---

## Task 8: Финальная сквозная проверка

- [ ] **Step 1:** В гарнитуре: левый стик ходит, правый плавно поворачивает, snap/teleport не мешают.
- [ ] **Step 2:** UserPanel (X/A) → Settings: вкладки General/Bindings переключаются; Bindings показывает актуальную карту; скролл ок.
- [ ] **Step 3:** `docs/controls-bindings.md` соответствует тому, что в панели.
- [ ] **Step 4:** `read_console` чистый (нет новых ошибок/варнингов от наших изменений).
- [ ] **Step 5: CHECKPOINT** — финальное подтверждение пользователя. Напомнить, что git-коммит — за пользователем.

---

## Self-Review

**Spec coverage:**
- Часть 1 (фикс рига, левый=move/правый=continuous turn/snap off) → Task 3 (механизм уточнён до CIAM-флагов на основе чтения `ControllerInputActionManager`).
- Часть 2 (ControlsProfile SO в InputBindings) → Task 1 + Task 2.
- Часть 3 (master-detail UI, General плейсхолдер / Bindings из профиля) → Task 4 (BindingRow) + Task 5 (логика) + Task 6 (префаб).
- Часть 4 (.md-зеркало, editor tool) → Task 7.
- Non-goals (ребайндинг, сохранение настроек, vertical-fly, comfort, наполнение General) — не реализуются. ✓

**Placeholder scan:** Кода-плейсхолдеров нет; все скрипты приведены целиком. MCP-шаги для префабов конкретны (пути, fileID, поля); live-inspect шаги — необходимая верификация перед правкой YAML, не «TODO».

**Type consistency:** `ControlsProfile.Bindings` / `SchemaVersion`, `ControlBinding` поля (`Category/Action/Description/Hand/InputLabel/IconId`), `ControlHand {None,Left,Right,Both,Any}`, `ControlBindingCategory {Movement,Selection,System}`, `BindingRow.Bind(ControlBinding)`, `SettingsPanel` поля — согласованы между Task 1/4/5/7.

**Расхождение со спекой (намеренное, зафиксировано):** спека предполагала фикс рига через очистку per-hand input source mode; чтение `ControllerInputActionManager.UpdateLocomotionActions` показало, что бинды move/turn управляются CIAM-флагами `m_SmoothMotionEnabled`/`m_SmoothTurnEnabled` — поэтому primary-механизм в Task 3 — флаги CIAM, а очистка input source mode оставлена как fallback (Step 6).
