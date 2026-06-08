> [!info] Назначение
> `ScenePickerPanel` — панель списка сохранённых сцен в режиме главного меню (`SpatialUi/Panels/`). Запрашивает перечень сцен у `AppStorage`, строит список строк-элементов `SceneListNode_Item`, создаёт и удаляет сцены, публикует `SceneSelectedEvent` при каждом изменении выбора. Листинг 3.26 (фрагмент) + Листинг Б.10 (полный текст).

### Обзор

##### Роль и место
Панель принадлежит сцене режима `MainMenu` и входит в её внутрисценную область жизни VContainer. Зависимости (`AppStorage`, `EventBus`) получает методом-инжектором `Construct`. Друг о `MainMenuPanel` не знает — взаимодействует только через `SceneSelectedEvent`. Клавиатурный ввод имени согласуется через `VrInputFieldFocusBridge` (прямой связи с `VrKeyboard` нет).

##### Ключевые методы
- `Start` — `async void`; подключает кнопки и запускает первый `RefreshAsync`.
- `RefreshAsync` — полная перестройка списка: уничтожает старые строки, сбрасывает выбор, публикует «пустой» `SceneSelectedEvent`, загружает перечень из хранилища, порождает новые элементы.
- `SpawnItem` — `Instantiate` + `GetComponent<SceneListNode_Item>` + подписка на `Clicked`.
- `OnItemClicked` — переключает визуальный выбор и публикует «заполненный» `SceneSelectedEvent`.
- `OnCreateClickedAsync` — создаёт сцену, затем вызывает `RefreshAsync`.
- `OnDeleteClicked` — удаляет сцену, запускает `RefreshAsync` через `_ = RefreshAsync()`.

### Разбор кода

##### Start (async void)

```csharp
private async void Start()
{
    _createButton.onClick.AddListener(() => { _ = OnCreateClickedAsync(); });
    _deleteButton.onClick.AddListener(OnDeleteClicked);
    _deleteButton.interactable = false;
    await RefreshAsync();
}
```

> `async void Start()` — единственный легальный случай `async void` по конвенции проекта: Unity вызывает `Start` без возможности `await`, поэтому исключение из тела будет проглочено движком, если не обёрнуто в `try/catch`. Здесь `RefreshAsync` исключений не бросает (только `Debug.Log`), поэтому допустимо.
>
> `() => { _ = OnCreateClickedAsync(); }` — дискард `_` подавляет предупреждение CS4014 «await не применён». Это намеренно: нажатие кнопки — fire-and-forget. Однако повторное нажатие до завершения первого `await` запустит второй параллельный `OnCreateClickedAsync` — защиты от реентерабельности нет.

##### RefreshAsync — сброс выбора до загрузки

```csharp
_selectedItem = null;
_deleteButton.interactable = false;
_bus.Publish(new SceneSelectedEvent { SceneId = string.Empty, DisplayName = string.Empty });

var scenes = await _storage.GetAllScenesAsync(CancellationToken.None);
```

> Публикация «пустого» события происходит **до** `await`, то есть синхронно, пока `MainMenuPanel` ещё обрабатывает тот же кадр. Это гарантирует, что кнопка «Open Scene» деактивируется раньше, чем начнётся асинхронная загрузка списка. Если бы событие публиковалось после `await`, существовало бы окно, когда `_selectedItem == null`, но кнопка ещё интерактивна.
>
> `CancellationToken.None` — отмена не предусмотрена; если панель уничтожится во время `await`, Unity уничтожит `_listRoot`, и цикл `foreach` ниже упадёт с `MissingReferenceException`. Реального краша нет: `OnDeleteClicked` использует `_ = RefreshAsync()`, а не `await`, поэтому уничтожение до завершения маловероятно в нормальном сценарии.

##### SpawnItem — coupling через C# event

```csharp
private void SpawnItem(string sceneId, string displayName)
{
    var go   = Instantiate(_sceneItemPrefab, _listRoot);
    var item = go.GetComponent<SceneListNode_Item>();
    item.Init(sceneId, displayName);
    item.Clicked += OnItemClicked;
}
```

> `item.Clicked += OnItemClicked` — подписка не отписывается явно. Это безопасно: элемент уничтожается в начале следующего `RefreshAsync` (`Destroy(child.gameObject)`), Unity при `Destroy` не вызывает `OnDestroy` на всех подписчиках автоматически, но `ScenePickerPanel` — единственный подписчик, и он переживает элемент. Утечки нет, потому что при `Destroy` ссылки на лямбды живут только в мёртвом объекте.

##### OnItemClicked — идемпотентный сброс предыдущего

```csharp
private void OnItemClicked(SceneListNode_Item item)
{
    _selectedItem?.SetSelected(false);
    _selectedItem = item;
    item.SetSelected(true);
    _deleteButton.interactable = true;
    _bus.Publish(new SceneSelectedEvent { SceneId = item.SceneId, DisplayName = item.DisplayName });
}
```

> `_selectedItem?.SetSelected(false)` — null-conditional оператор: при первом нажатии (когда `_selectedItem == null`) сброс пропускается без NPE. Если нажать на уже выбранный элемент — `SetSelected(false)` и затем `SetSelected(true)` вызовутся на одном объекте: визуально нейтрально, событие публикуется повторно (дубль `SceneSelectedEvent`).

##### OnDeleteClicked — fire-and-forget с дискардом

```csharp
private void OnDeleteClicked()
{
    if (_selectedItem == null) return;
    _storage.DeleteScene(_selectedItem.SceneId);
    _ = RefreshAsync();
}
```

> `DeleteScene` — синхронный вызов (удаление файла); `RefreshAsync` запускается немедленно после него. Порядок гарантирован: файл удалён до первой точки `await` в `RefreshAsync` (цикл `foreach` над `_listRoot` — синхронный). Если `DeleteScene` бросит исключение, `RefreshAsync` не будет вызван — список останется в старом состоянии, но кнопка Delete уже задизейблена через `RefreshAsync` в других ветках.

### К защите

##### Вероятные вопросы

> [!question]
> **В:** Почему `SceneSelectedEvent` публикуется и с пустым `SceneId`, и с заполненным — в чём смысл двух состояний?
> **О:** Пустое событие — сигнал «выбор сброшен»; по нему `MainMenuPanel` деактивирует кнопку «Open Scene». Заполненное — сигнал «сцена выбрана»; по нему кнопка активируется и подпись меняется на имя сцены. Обе панели не знают друг о друге, координация идёт исключительно через `EventBus`.

> [!question]
> **В:** Почему `async void Start`, а не, например, запуск корутины?
> **О:** По конвенции проекта `async void` допускается в Unity-lifecycle методах (`Start`, `Awake`) как единственная точка входа без возможности `await`. Корутина потребовала бы преобразования всего асинхронного кода на `IEnumerator`; `Task`-цепочка через `async/await` согласована с остальным кодом (`AppStorage` возвращает `Task`).

> [!question]
> **В:** Что произойдёт, если пользователь нажмёт «Create» дважды быстро, не дожидаясь завершения первой операции?
> **О:** Запустятся два параллельных `OnCreateClickedAsync`. Оба считают `_nameInput.text` — второй прочтёт пустую строку (первый уже сбросил её), создаст сцену с именем `"New Scene"`. Оба вызовут `RefreshAsync`, список перестроится дважды. Защиты от реентерабельности нет — кнопку не блокируют на время async.

> [!question]
> **В:** Как панель узнаёт список сцен — читает файловую систему напрямую?
> **О:** Нет. Вызывается `_storage.GetAllScenesAsync` — метод `AppStorage`. Панель не знает о путях, форматах и файловой системе: всё это инкапсулировано в `StorageCore`. Принцип: UI-компонент обращается только к фасаду подсистемы, прямой доступ к файлам из панели запрещён соглашениями.

> [!question]
> **В:** Зачем `VrInputFieldFocusBridge` упомянут в тексте ВКР, но его нет в этом файле?
> **О:** `VrInputFieldFocusBridge` — отдельный компонент, прикреплённый к `TMP_InputField` на уровне префаба в редакторе. Он соединяет поле ввода с `VrKeyboard` (рут-скоп) без какого-либо кода в `ScenePickerPanel`. Панель работает с `_nameInput` как с обычным `TMP_InputField`; клавиатурная связь невидима на уровне C#.

### Связи

[[AppStorage]] · [[SceneListNode_Item]] · [[MainMenuPanel]] · [[SceneSelectedEvent]] · [[SceneOpenedEvent]] · [[EventBus]] · [[Паттерн Publish-Subscribe]] · [[Внедрение зависимостей (VContainer)]]
