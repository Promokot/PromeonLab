---
note_type: script
subsystem: SpatialUi / ExportPipeline
listings: "3.63, Б.34"
---

> [!info] Назначение
> `ExportPanel` — VR-панель экспорта сцены. Публикует `SceneExportRequestedEvent` по нажатию кнопки, подписывается на `SceneExportedEvent` для отображения результата, обновляет путь назначения в реальном времени через `SceneExporter.BuildTargetPath`. Относится к подсистеме `SpatialUi`, открывается с вкладки навигации «exporter». Листинги 3.63, Б.34.

### Обзор

##### Роль и место

Панель живёт на `UserPanel` (корневая область), инжектируется через `[Inject] Construct`. Зависимости: `EventBus`, `SceneContext`, `SceneExporter` (app-lifetime), `AppStorage`. Подписки — в `OnEnable`/`OnDisable`, включая `TMP_InputField.onValueChanged` и `Button.onClick`. Это событийная модель: панель **не вызывает** `SceneExporter` напрямую — публикует `SceneExportRequestedEvent`.

##### Ключевые методы

| Метод | Назначение |
|---|---|
| `OnExportClicked` | Публикует запрос, блокирует кнопку, пишет «Exporting…» |
| `OnExported` | Отображает результат из `SceneExportedEvent.Message`, разблокирует кнопку |
| `RefreshSceneInfo` | Заполняет имя сцены; если поле имени пустое — подставляет `DisplayName` |
| `RefreshPathLabel` | Пересчитывает путь через `_exporter.BuildTargetPath(name)` |

### Разбор кода

##### OnExportClicked — публикация вместо прямого вызова

```csharp
private void OnExportClicked()
{
    if (_bus == null) return;
    var name = _fileNameInput != null ? _fileNameInput.text : string.Empty;
    _bus.Publish(new SceneExportRequestedEvent { FileName = name });

    if (_statusLabel != null)
        _statusLabel.text = "Exporting…";
    if (_exportButton != null)
        _exportButton.interactable = false;
}
```

> Панель не держит логику экспорта — только публикует событие. `SceneExporter` подписан на `SceneExportRequestedEvent` и выполнит работу. Разделение по паттерну [[Паттерн Publish-Subscribe]]: панель не знает, кто обработает запрос.
>
> `_exportButton.interactable = false` — блокировка на время операции: защита от двойного нажатия. Разблокировка — в `OnExported`, который гарантированно придёт по завершении (и при ошибке тоже).
>
> Guard `if (_bus == null) return` — на случай, если панель активируется до инъекции (Unity может вызвать `OnEnable` раньше `[Inject]` в некоторых конфигурациях).

##### OnExported — обработка результата

```csharp
private void OnExported(SceneExportedEvent e)
{
    if (_statusLabel != null)
        _statusLabel.text = e.Message;
    if (_exportButton != null)
        _exportButton.interactable = true;
}
```

> `e.Message` содержит путь сохранения и счётчик нод без геометрии (или сообщение об ошибке). Панель не различает Success/Failure для логики UI — просто показывает строку. Это намеренно: разбор статуса не нужен, достаточно читаемого текста.

##### RefreshSceneInfo — заполнение имени однократно

```csharp
if (_fileNameInput != null && string.IsNullOrEmpty(_fileNameInput.text) && scene != null)
    _fileNameInput.text = scene.DisplayName;
```

> Имя файла автозаполняется только если поле **пустое**. Если пользователь уже ввёл своё имя и переключился на другую вкладку, при возврате `RefreshSceneInfo` вызовется снова (через `OnSceneContextChanged`), но не перезапишет введённое.

##### RefreshPathLabel — live preview пути

```csharp
private void RefreshPathLabel()
{
    if (_pathLabel == null || _exporter == null) return;
    var name = _fileNameInput != null ? _fileNameInput.text : string.Empty;
    _pathLabel.text = _exporter.BuildTargetPath(name);
}
```

> Вызывается из `_fileNameInput.onValueChanged` — при каждом символе. `BuildTargetPath` — чистая функция (санитизация + Path.Combine), вызов дешёвый. Пользователь видит итоговый путь в реальном времени.

##### OnEnable/OnDisable — регистрация Unity-событий

```csharp
if (_fileNameInput != null)
    _fileNameInput.onValueChanged.AddListener(OnFileNameChanged);
if (_exportButton != null)
    _exportButton.onClick.AddListener(OnExportClicked);
```

> Unity UI-события (`UnityEvent`) не управляются `EventBus` — добавляются через `AddListener`/`RemoveListener`. Это отличается от `_bus.Subscribe/Unsubscribe`: Unity-события — C# делегаты на компонентах, `EventBus` — кастомный type-keyed dispatch. Обе системы должны быть симметрично очищены в `OnDisable`.

### К защите

##### Вероятные вопросы

> [!question]
> **В:** Почему панель публикует событие, а не вызывает `SceneExporter.Export()` напрямую?
> **О:** Прямые вызовы через границы подсистем запрещены правилами CLAUDE.md («Direct method calls across subsystem boundaries are forbidden»). `ExportPanel` — в подсистеме `SpatialUi`, `SceneExporter` — в `ExportPipeline`. Связь через `SceneExportRequestedEvent`/`SceneExportedEvent` — развязка: панель не зависит от конкретной реализации экспортёра.

> [!question]
> **В:** Как панель показывает путь до нажатия кнопки?
> **О:** `_fileNameInput.onValueChanged` подписан на `RefreshPathLabel`. При каждом вводе символа вызывается `_exporter.BuildTargetPath(text)` — чистая функция, результат выводится в `_pathLabel`. Это даёт live-preview без каких-либо запросов на диск.

> [!question]
> **В:** Что происходит с кнопкой при ошибке экспорта?
> **О:** `SceneExporter` публикует `SceneExportedEvent` и при ошибке (`Success = false`), сообщение содержит текст исключения. `OnExported` всегда разблокирует кнопку (`interactable = true`) и показывает сообщение — независимо от Success. Кнопка не остаётся заблокированной.

> [!question]
> **В:** Зачем автозаполнение имени файла только при пустом поле?
> **О:** Защита пользовательского ввода: если пользователь открыл панель, ввёл имя, переключился на другую вкладку и вернулся — `OnSceneContextChanged` снова вызовет `RefreshSceneInfo`. Условие `string.IsNullOrEmpty(_fileNameInput.text)` предотвращает перезапись введённого пользователем имени.

### Связи

[[SceneExporter]] · [[SceneBundle]] · [[SceneContext]] · [[Паттерн Publish-Subscribe]] · [[Внедрение зависимостей (VContainer)]] · [[SceneExportRequestedEvent]] · [[SceneExportedEvent]] · [[SceneContextChangedEvent]]
