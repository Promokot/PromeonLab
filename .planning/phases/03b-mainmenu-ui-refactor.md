# MainMenu UI Refactor — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Разделить Canvas и логику главного меню на две изолированные панели (ScenePickerPanel + MainMenuPanel), добавить выделение/удаление сцен, кнопки Open Sandbox / Open Scene, упорядочить папки с префабами.

**Architecture:** Две SpatialPanel-компоненты, каждая на своём Canvas. Общаются только через EventBus: ScenePickerPanel публикует `SceneSelectedEvent`, MainMenuPanel подписывается. AppStorage получает `BeginSandboxSession()` (временная сессия без записи) и `GetAllScenesAsync()` для загрузки имён.

**Tech Stack:** Unity 6, C#, VContainer, MessagePipe-style EventBus, TextMeshPro, Unity UI

---

### Task 1: Добавить `SceneSelectedEvent` в AppEvents и методы в AppStorage

**Files:**
- Modify: `Assets/_App/_Shared/Events/AppEvents.cs`
- Modify: `Assets/_App/Subsystems/StorageCore/AppStorage.cs`

- [ ] **Step 1: Добавить событие в AppEvents.cs**

```csharp
public struct SceneOpenedEvent       { public string SceneId; }
public struct SceneModifiedEvent     { }
public struct SceneClosedEvent       { }
public struct AssetImportedEvent     { public string AssetId; }
public struct SelectionChangedEvent  { public string SelectedNodeId; }
public struct ModeChangedEvent       { public AppMode PreviousMode; public AppMode CurrentMode; }
public struct FrameChangedEvent      { public int Frame; }
public struct PlaybackStateChangedEvent { public bool IsPlaying; public int Frame; }
public struct ErrorOccurredEvent     { public ErrorLevel Level; public string Message; }
public struct SceneSelectedEvent     { public string SceneId; public string DisplayName; }
```

- [ ] **Step 2: Добавить `GetAllScenesAsync` и `BeginSandboxSession` в AppStorage.cs**

Добавить в конец класса, после `DeleteScene`:

```csharp
public async Task<IReadOnlyList<(string SceneId, string DisplayName)>> GetAllScenesAsync(
    CancellationToken ct = default)
{
    var result = new List<(string, string)>();
    foreach (var sceneId in GetAllSceneIds())
    {
        var data = await LoadSceneAsync(sceneId, ct);
        if (data != null) result.Add((data.SceneId, data.DisplayName));
    }
    return result;
}

public SceneData BeginSandboxSession()
{
    var data = new SceneData
    {
        SceneId     = "__sandbox__",
        DisplayName = "Sandbox",
        CreatedAt   = DateTime.UtcNow.ToString("yyyy-MM-dd")
    };
    _activeSceneId = data.SceneId;
    return data;
}
```

- [ ] **Step 3: Проверить — открыть Unity, убедиться, что компиляция чистая**

---

### Task 2: Обновить `Subsystems.SpatialUi.asmdef`

**Files:**
- Modify: `Assets/_App/Subsystems/SpatialUi/Subsystems.SpatialUi.asmdef`

- [ ] **Step 1: Добавить `Subsystems.StorageCore` в references**

```json
{
  "name": "Subsystems.SpatialUi",
  "references": [
    "_Shared",
    "VContainer",
    "Unity.XR.Interaction.Toolkit",
    "Subsystems.ModeOrchestrator",
    "Subsystems.StorageCore",
    "Unity.TextMeshPro"
  ],
  "autoReferenced": false
}
```

---

### Task 3: Создать `SceneItem.cs`

**Files:**
- Create: `Assets/_App/Subsystems/SpatialUi/UI/SceneItem.cs`

- [ ] **Step 1: Написать SceneItem.cs**

```csharp
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SceneItem : MonoBehaviour
{
    [SerializeField] private TMP_Text _label;
    [SerializeField] private Image _background;
    [SerializeField] private Button _button;

    [SerializeField] private Color _normalColor  = new Color(0f, 0f, 0f, 0f);
    [SerializeField] private Color _selectedColor = new Color(0.3f, 0.6f, 1f, 0.4f);

    public string SceneId      { get; private set; }
    public string DisplayName  { get; private set; }

    public event Action<SceneItem> Clicked;

    public void Init(string sceneId, string displayName)
    {
        SceneId     = sceneId;
        DisplayName = displayName;
        _label.text = displayName;
        _button.onClick.AddListener(() => Clicked?.Invoke(this));
        SetSelected(false);
    }

    public void SetSelected(bool selected) =>
        _background.color = selected ? _selectedColor : _normalColor;
}
```

---

### Task 4: Создать `ScenePickerPanel.cs`

**Files:**
- Create: `Assets/_App/Subsystems/SpatialUi/UI/ScenePickerPanel.cs`

- [ ] **Step 1: Написать ScenePickerPanel.cs**

```csharp
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;

public class ScenePickerPanel : MonoBehaviour
{
    [SerializeField] private Transform      _listRoot;
    [SerializeField] private GameObject     _sceneItemPrefab;
    [SerializeField] private TMP_InputField _nameInput;
    [SerializeField] private Button         _createButton;
    [SerializeField] private Button         _deleteButton;

    private AppStorage _storage;
    private EventBus   _bus;
    private SceneItem  _selectedItem;

    [Inject]
    public void Construct(AppStorage storage, EventBus bus)
    {
        _storage = storage;
        _bus     = bus;
    }

    private async void Start()
    {
        _createButton.onClick.AddListener(() => { _ = OnCreateClickedAsync(); });
        _deleteButton.onClick.AddListener(OnDeleteClicked);
        _deleteButton.interactable = false;
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        foreach (Transform child in _listRoot)
            Destroy(child.gameObject);

        _selectedItem = null;
        _deleteButton.interactable = false;
        _bus.Publish(new SceneSelectedEvent { SceneId = string.Empty, DisplayName = string.Empty });

        var scenes = await _storage.GetAllScenesAsync(CancellationToken.None);
        foreach (var (sceneId, displayName) in scenes)
            SpawnItem(sceneId, displayName);
    }

    private void SpawnItem(string sceneId, string displayName)
    {
        var go   = Instantiate(_sceneItemPrefab, _listRoot);
        var item = go.GetComponent<SceneItem>();
        item.Init(sceneId, displayName);
        item.Clicked += OnItemClicked;
    }

    private void OnItemClicked(SceneItem item)
    {
        _selectedItem?.SetSelected(false);
        _selectedItem = item;
        item.SetSelected(true);
        _deleteButton.interactable = true;
        _bus.Publish(new SceneSelectedEvent { SceneId = item.SceneId, DisplayName = item.DisplayName });
    }

    private async Task OnCreateClickedAsync()
    {
        var name = _nameInput.text;
        if (string.IsNullOrWhiteSpace(name)) name = "New Scene";
        _nameInput.text = string.Empty;
        await _storage.CreateSceneAsync(name, CancellationToken.None);
        await RefreshAsync();
    }

    private void OnDeleteClicked()
    {
        if (_selectedItem == null) return;
        _storage.DeleteScene(_selectedItem.SceneId);
        _ = RefreshAsync();
    }
}
```

---

### Task 5: Переписать `MainMenuPanel.cs`

**Files:**
- Modify: `Assets/_App/Subsystems/SpatialUi/UI/MainMenuPanel.cs`

- [ ] **Step 1: Заменить содержимое MainMenuPanel.cs**

```csharp
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;

public class MainMenuPanel : MonoBehaviour
{
    [SerializeField] private Button   _openSandboxButton;
    [SerializeField] private Button   _openSceneButton;
    [SerializeField] private TMP_Text _openSceneLabel;

    private AppStorage       _storage;
    private EventBus         _bus;
    private ModeOrchestrator _orchestrator;
    private string           _selectedSceneId;

    [Inject]
    public void Construct(AppStorage storage, EventBus bus, ModeOrchestrator orchestrator)
    {
        _storage      = storage;
        _bus          = bus;
        _orchestrator = orchestrator;
    }

    private void Start()
    {
        _openSandboxButton.onClick.AddListener(OnOpenSandbox);
        _openSceneButton.onClick.AddListener(() => { _ = OpenSceneAsync(); });
        _openSceneButton.interactable = false;
        _bus.Subscribe<SceneSelectedEvent>(OnSceneSelected);
    }

    private void OnDestroy() =>
        _bus.Unsubscribe<SceneSelectedEvent>(OnSceneSelected);

    private void OnSceneSelected(SceneSelectedEvent e)
    {
        _selectedSceneId = e.SceneId;
        var hasScene = !string.IsNullOrEmpty(e.SceneId);
        _openSceneButton.interactable = hasScene;
        _openSceneLabel.text = hasScene ? $"Open  {e.DisplayName}" : "Open Scene";
    }

    private void OnOpenSandbox()
    {
        var data = _storage.BeginSandboxSession();
        _bus.Publish(new SceneOpenedEvent { SceneId = data.SceneId });
        _orchestrator.TransitionTo(AppMode.VrEditing);
    }

    private async System.Threading.Tasks.Task OpenSceneAsync()
    {
        if (string.IsNullOrEmpty(_selectedSceneId)) return;
        var data = await _storage.LoadSceneAsync(_selectedSceneId, CancellationToken.None);
        if (data == null) return;
        _storage.SetActiveScene(data);
        _bus.Publish(new SceneOpenedEvent { SceneId = data.SceneId });
        _orchestrator.TransitionTo(AppMode.VrEditing);
    }
}
```

---

### Task 6: Обновить `MainMenuSceneScope` и удалить `ScenePickerView`

**Files:**
- Modify: `Assets/_App/Bootstrap/MainMenuSceneScope.cs`
- Delete: `Assets/_App/Subsystems/AssetBrowser/UI/ScenePickerView.cs`

- [ ] **Step 1: Обновить регистрации в MainMenuSceneScope.cs**

```csharp
using VContainer;
using VContainer.Unity;
using UnityEngine;

public class MainMenuSceneScope : LifetimeScope
{
    [SerializeField] private ModeTransitionGraph _transitionGraph;

    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<EventBus>(Lifetime.Scoped);
        builder.RegisterInstance(_transitionGraph);
        builder.Register<ModeOrchestrator>(Lifetime.Scoped);
        builder.Register<UnsavedChangesGuard>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.RegisterComponentInHierarchy<ScenePickerPanel>();
        builder.RegisterComponentInHierarchy<MainMenuPanel>();
    }
}
```

- [ ] **Step 2: Удалить ScenePickerView.cs**

Удали файл `Assets/_App/Subsystems/AssetBrowser/UI/ScenePickerView.cs` и его `.meta` через файловый менеджер Unity (ПКМ → Delete).

---

### Task 7: Инструкции по Unity Editor

**Папки префабов — реорганизовать:**

- [ ] **Step 1: Создать папки**
  - `Assets/_App/Subsystems/SpatialUi/UI/Prefabs/Panels/`
  - `Assets/_App/Subsystems/SpatialUi/UI/Prefabs/Items/`

- [ ] **Step 2: Перенести префабы через Project-окно Unity (drag & drop)**
  - `MainMenuPanel.prefab` → `Panels/`
  - `AssetBrowserPanel.prefab` → `Panels/`
  - `SceneItemPrefab.prefab` → `Items/`
  - `AssetItemPrefab.prefab` → `Items/`

**Обновить `SceneItemPrefab`:**

- [ ] **Step 3: Открыть SceneItemPrefab в режиме Prefab**
  - Добавить компонент `SceneItem.cs` на корневой объект
  - Привязать поля: `_label` → TMP_Text, `_background` → Image (фон кнопки), `_button` → Button

**Создать `ScenePickerPanel.prefab`:**

- [ ] **Step 4: В сцене MainMenu создать новый World Space Canvas**
  - `GameObject → UI → Canvas` → режим **World Space**
  - Убрать `Graphic Raycaster`, добавить `Tracked Device Graphic Raycaster`
  - Event Camera → Main Camera
  - RectTransform: Width `600`, Height `700`, Scale `0.001, 0.001, 0.001`
  - Position: `(-0.4, 1.5, 1.8)` — слева

- [ ] **Step 5: Добавить дочерние элементы ScenePickerPanel**
  ```
  Canvas [ScenePickerPanel]
  ├── Header (TMP_Text, текст "Scenes")
  ├── ScrollView
  │     └── Viewport → Content   ← это _listRoot
  ├── HorizontalLayout
  │     ├── TMP_InputField "NewSceneName"   ← _nameInput
  │     └── Button "Create"                 ← _createButton
  └── Button "Delete"                       ← _deleteButton
  ```

- [ ] **Step 6: Добавить компонент `ScenePickerPanel.cs` на корневой Canvas-объект**
  - Привязать все SerializeField поля

- [ ] **Step 7: Сохранить как префаб** в `Panels/ScenePickerPanel.prefab`

**Обновить/создать `MainMenuPanel`:**

- [ ] **Step 8: Создать второй World Space Canvas**
  - RectTransform: Width `400`, Height `300`, Scale `0.001, 0.001, 0.001`
  - Position: `(0.35, 1.5, 1.8)` — справа
  - Добавить `Tracked Device Graphic Raycaster`, Event Camera → Main Camera

- [ ] **Step 9: Добавить дочерние элементы MainMenuPanel**
  ```
  Canvas [MainMenuPanel]
  ├── Button "OpenSandboxButton"   ← _openSandboxButton (текст "Open Sandbox")
  └── Button "OpenSceneButton"     ← _openSceneButton
        └── TMP_Text "Label"       ← _openSceneLabel (текст "Open Scene")
  ```

- [ ] **Step 10: Добавить компонент `MainMenuPanel.cs`, привязать поля**

- [ ] **Step 11: Сохранить как префаб** в `Panels/MainMenuPanel.prefab`

**Верифицировать сцену MainMenu:**

- [ ] **Step 12: Убедиться, что в иерархии MainMenu.unity есть:**
  ```
  [MainMenuSceneScope]
  XR Origin (XR Rig)
  EventSystem (XR UI Input Module)
  [ScenePickerPanel]  ← Canvas + ScenePickerPanel.cs
  [MainMenuPanel]     ← Canvas + MainMenuPanel.cs
  ```

- [ ] **Step 13: Запустить из Bootstrap сцены, проверить:**
  - Список сцен отображается (или пуст при первом запуске)
  - "Create" создаёт новую сцену, она появляется в списке
  - Клик на сцену выделяет её, кнопка "Open [Имя]" становится активной
  - "Delete" удаляет выделенную сцену
  - "Open Sandbox" переходит в VrEditing без сохранения
  - "Open [Имя]" переходит в VrEditing с выбранной сценой
