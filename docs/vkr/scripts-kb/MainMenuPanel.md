> [!info] Назначение
> `MainMenuPanel` — главная панель режима `MainMenu` (`SpatialUi/Panels/`). Подписана на `SceneSelectedEvent` от `ScenePickerPanel`, активирует кнопку «Open Scene» при выборе, открывает сцену VR-редактора тремя шагами (`LoadSceneAsync` → `SetActiveScene` → `SceneOpenedEvent` → `TransitionTo`), запускает сеанс песочницы через `BeginSandboxSession`. Листинг 3.28 (фрагмент) + Листинг Б.11 (полный текст).

### Обзор

##### Роль и место
Панель живёт в сцене `MainMenu`, получает зависимости (`AppStorage`, `EventBus`, `ModeOrchestrator`) через `[Inject] Construct`. Про `ScenePickerPanel` не знает — координация только через шину. Выход из главного меню происходит через `ModeOrchestrator.TransitionTo`, который запускает цепочку `HeadFade → LoadSceneMode.Single → ModeChangedEvent`.

##### Ключевые методы
- `Start` — подключает кнопки, деактивирует «Open Scene», подписывается на `SceneSelectedEvent`.
- `OnDestroy` — явно отписывается от `SceneSelectedEvent` (панель — `MonoBehaviour`, уничтожается при выгрузке сцены).
- `OnSceneSelected` — обновляет `_selectedSceneId`, интерактивность кнопки и её подпись.
- `OpenSceneAsync` — три шага открытия сцены; `async Task` (не `async void`), запускается из лямбды через `_ =`.
- `OnOpenSandbox` — синхронный: `BeginSandboxSession` возвращает `SceneData` без IO.

### Разбор кода

##### OnSceneSelected — реакция на пустой SceneId

```csharp
private void OnSceneSelected(SceneSelectedEvent e)
{
    _selectedSceneId = e.SceneId;
    var hasScene = !string.IsNullOrEmpty(e.SceneId);
    _openSceneButton.interactable = hasScene;
    _openSceneLabel.text = hasScene ? $"Open  {e.DisplayName}" : "Open Scene";
}
```

> `!string.IsNullOrEmpty(e.SceneId)` — единственная ветка на два состояния события: пустой `SceneId` (сброс выбора в `ScenePickerPanel.RefreshAsync`) и заполненный (нажатие строки). Панель не различает «список перестроен» и «ничего не выбрано» — оба случая приходят одним типом события, поэтому достаточно проверить пустоту строки.
>
> Двойной пробел в `$"Open  {e.DisplayName}"` — намеренный визуальный отступ в VR-интерфейсе, не опечатка.

##### OpenSceneAsync — порядок трёх шагов

```csharp
private async Task OpenSceneAsync()
{
    if (string.IsNullOrEmpty(_selectedSceneId)) return;
    var data = await _storage.LoadSceneAsync(_selectedSceneId, CancellationToken.None);
    if (data == null) return;
    _storage.SetActiveScene(data);
    _bus.Publish(new SceneOpenedEvent { SceneId = data.SceneId });
    _orchestrator.TransitionTo(AppMode.VrEditing);
}
```

> Порядок критичен:
> 1. `LoadSceneAsync` — читает `scene.json` с диска; может вернуть `null` (файл повреждён или удалён). Проверка `data == null` предотвращает переход в режим без данных.
> 2. `SetActiveScene(data)` — запоминает загруженную `SceneData` в `AppStorage` как «текущую активную». Это не открытие файла, а запись ссылки в памяти. Граф сцены (`SceneGraph.Start`) прочитает `ActiveSceneId` при инициализации и начнёт восстановление.
> 3. `Publish(SceneOpenedEvent)` — уведомление о смене сцены **до** `TransitionTo`. Этот порядок нужен: `TransitionTo` запустит загрузку Unity-сцены (`LoadSceneMode.Single`), которая уничтожит текущую область жизни. Все подписчики текущего `EventBus` уже получат событие.
> 4. `TransitionTo(AppMode.VrEditing)` — передаёт управление `ModeOrchestrator`; метод возвращается немедленно (переход асинхронный внутри оркестратора).
>
> Почему `SceneOpenedEvent` публикуется здесь, а не в новой сцене? Потому что граф новой сцены читает `ActiveSceneId` из `AppStorage` при `Start`, а не ждёт событие — событие предназначено для других подписчиков текущей сцены, если они есть.

##### OnOpenSandbox — синхронный путь без LoadSceneAsync

```csharp
private void OnOpenSandbox()
{
    var data = _storage.BeginSandboxSession();
    _bus.Publish(new SceneOpenedEvent { SceneId = data.SceneId });
    _orchestrator.TransitionTo(AppMode.Sandbox);
}
```

> `BeginSandboxSession` — синхронный: возвращает in-memory `SceneData` с заранее сгенерированным `SceneId`, не обращается к диску. Поэтому `OnOpenSandbox` — обычный `void`, без `async`. Сцена песочницы не персистентна: данные существуют только в памяти, при выходе не сохраняются на диск.

##### OnDestroy — явная отписка

```csharp
private void OnDestroy() =>
    _bus.Unsubscribe<SceneSelectedEvent>(OnSceneSelected);
```

> `MonoBehaviour` живёт в сцене `MainMenu`; при `TransitionTo` Unity выгрузит сцену (`LoadSceneMode.Single`), вызовет `OnDestroy`. Без явной отписки `EventBus` хранил бы мёртвую ссылку: в `_handlers` остался бы `Action<SceneSelectedEvent>` с замыканием на уничтоженный объект. Следующий `Publish<SceneSelectedEvent>` не вызовет NPE (объект уже `null`), но это утечка памяти и потенциальная `MissingReferenceException`.

##### Закомментированная кнопка Exit

```csharp
/*[SerializeField] private Button   _exitButton;*/
/*_exitButton.onClick.AddListener(OnExit);*/
/*private void OnExit() => Application.Quit();*/
```

> Код закомментирован, а не удалён — сигнал, что выход из приложения запланирован, но не реализован. На Quest кнопка Home OS возвращает пользователя в лончер; прямой `Application.Quit` не нужен для базового UX.

### К защите

##### Вероятные вопросы

> [!question]
> **В:** Почему `SceneOpenedEvent` публикуется в `MainMenuPanel`, а не в новой сцене после её загрузки?
> **О:** К моменту загрузки Unity-сцены (`LoadSceneMode.Single`) текущая область жизни уничтожена. `EventBus` текущей сцены недоступен. Поэтому событие публикуется сразу после `SetActiveScene` — пока ещё живёт сцена меню. Граф новой сцены восстанавливается не по событию, а по `ActiveSceneId` из `AppStorage` в методе `Start`.

> [!question]
> **В:** Что произойдёт, если `LoadSceneAsync` вернёт `null`?
> **О:** Метод `OpenSceneAsync` завершится ранним `return`: `SetActiveScene` не вызовется, `SceneOpenedEvent` не опубликуется, `TransitionTo` не выполнится. Пользователь останется в главном меню. Кнопка «Open Scene» останется активной — повторное нажатие снова попробует загрузить ту же сцену. Ошибка нигде не логируется (уточнить: `AppStorage.LoadSceneAsync` может логировать внутри).

> [!question]
> **В:** Чем отличается открытие сцены редактора от открытия песочницы?
> **О:** Редактор: `LoadSceneAsync` (чтение с диска) → `SetActiveScene` (запись ссылки) → `TransitionTo(VrEditing)`. Песочница: `BeginSandboxSession` (in-memory, без диска) → `TransitionTo(Sandbox)`. Результат один — `SceneGraph` новой сцены получает `ActiveSceneId` и восстанавливает граф, но в случае песочницы граф пустой (нет сохранённых нод).

> [!question]
> **В:** Почему кнопка «Open Scene» деактивирована по умолчанию, а не скрыта?
> **О:** Деактивированная кнопка остаётся видимой — пользователь понимает, что действие возможно, но требует предварительного выбора. Скрытая кнопка создаёт эффект «исчезновения» элемента интерфейса, что в VR дезориентирует. `Button.interactable = false` также не требует перестройки лейаута.

> [!question]
> **В:** Почему `TransitionTo` вызывается после `Publish`, а не до?
> **О:** `TransitionTo` запускает асинхронную выгрузку сцены. Если вызвать его первым, область жизни начнёт разрушаться, и `_bus.Publish` может обратиться к уже частично уничтоженному `EventBus`. Порядок «Publish → TransitionTo» гарантирует, что событие доставлено всем подписчикам текущей сцены до начала выгрузки.

### Связи

[[AppStorage]] · [[ModeOrchestrator]] · [[SceneContext]] · [[ScenePickerPanel]] · [[SceneGraph]] · [[SceneSelectedEvent]] · [[SceneOpenedEvent]] · [[ModeChangedEvent]] · [[EventBus]] · [[Паттерн Publish-Subscribe]] · [[Внедрение зависимостей (VContainer)]]
