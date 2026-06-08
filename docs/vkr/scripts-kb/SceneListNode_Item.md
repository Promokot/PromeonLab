> [!info] Назначение
> `SceneListNode_Item` — UI-элемент одной строки в списке сцен панели `ScenePickerPanel` (`SpatialUi/Elements/`). Хранит `SceneId` и `DisplayName`, отображает имя, переключает цвет подложки при выделении, уведомляет панель-владельца событием `Clicked`. Не имеет связей с `AppStorage` или `EventBus` — данные получает при инициализации, решения принимает владелец. Листинг 3.27.

### Обзор

##### Роль и место
Чистый UI-листовой элемент: один компонент на один строчный объект списка. `ScenePickerPanel` порождает его через `Instantiate`, вызывает `Init` и подписывается на `Clicked`. Принцип «тупого представления»: элемент не знает ни о шине событий, ни о хранилище — только рисует и сообщает о нажатии.

##### Ключевые методы
- `Init` — записывает данные, текст метки, регистрирует обработчик кнопки, сбрасывает визуал.
- `SetSelected` — меняет цвет `Image` фона: выбран / не выбран.
- `Clicked` — C# event `Action<SceneListNode_Item>`; аргумент — сам элемент, чтобы владелец мог прочитать `SceneId`/`DisplayName`.

### Разбор кода

##### Init — замыкание на this

```csharp
public void Init(string sceneId, string displayName)
{
    SceneId     = sceneId;
    DisplayName = displayName;
    _label.text = displayName;
    _button.onClick.AddListener(() => Clicked?.Invoke(this));
    SetSelected(false);
}
```

> `() => Clicked?.Invoke(this)` — лямбда замыкает `this` (объект `SceneListNode_Item`). При нажатии кнопки вызывает `Clicked` с собой как аргументом. Это позволяет `ScenePickerPanel.OnItemClicked` получить ссылку на нажатый элемент и прочитать его `SceneId`/`DisplayName` без словаря или поиска по списку.
>
> `Clicked?.Invoke(this)` — null-conditional: если никто не подписался на `Clicked`, вызов пропускается без NPE. Это важно: `Init` вызывается из `SpawnItem` **до** `item.Clicked += OnItemClicked`, то есть на момент `AddListener` подписчика ещё нет. Нажатие до подписки не сломает элемент.
>
> `_button.onClick.AddListener` вызывается каждый раз при `Init`. Если `Init` вызовут повторно на уже инициализированном элементе — обработчик добавится второй раз, и каждое нажатие сгенерирует два события `Clicked`. В реальном коде `Init` вызывается ровно один раз на жизненный цикл объекта, так что двойного вызова нет.

##### SetSelected — прямое присваивание цвета

```csharp
public void SetSelected(bool selected) =>
    _background.color = selected ? _selectedColor : _normalColor;
```

> Прямая запись в `Image.color` без `ColorBlock` или анимации — нет интерполяции, переключение мгновенное. `_normalColor = new Color(0, 0, 0, 0)` — полностью прозрачный фон (alpha=0), то есть в невыбранном состоянии подложка невидима. `_selectedColor = new Color(0.3f, 0.6f, 1f, 0.4f)` — голубой с alpha 0.4: полупрозрачная заливка. Оба цвета задаются как `[SerializeField]` — их можно переопределить в Inspector без правки кода.

##### SceneId / DisplayName — auto-property с private set

```csharp
public string SceneId     { get; private set; }
public string DisplayName { get; private set; }
```

> `private set` — данные читаемы снаружи (`ScenePickerPanel` читает `item.SceneId`), но записываемы только внутри класса. Изменить `SceneId` после `Init` невозможно без повторного вызова `Init`. Это соответствует концепту «элемент-паспорт»: данные зафиксированы на момент создания.

### К защите

##### Вероятные вопросы

> [!question]
> **В:** Почему элемент не подписывается на `EventBus` сам — ведь это проще, чем пробрасывать event через владельца?
> **О:** Это нарушило бы принцип «тупого представления». Элемент не знает контекста сцены, не имеет DI-инжектора — он создаётся через `Instantiate` без VContainer. Решение о публикации `SceneSelectedEvent` принадлежит `ScenePickerPanel`, которая понимает бизнес-логику (сбросить предыдущий выбор, активировать Delete). Элемент только сообщает факт нажатия.

> [!question]
> **В:** Зачем передавать `this` в `Clicked.Invoke(this)`, а не просто `SceneId`?
> **О:** Чтобы `ScenePickerPanel` могла вызвать `SetSelected(false)` на предыдущем элементе. Для этого нужна ссылка на объект, а не просто строка. Если бы `Clicked` передавал только `SceneId`, панели пришлось бы хранить словарь `id → элемент`.

> [!question]
> **В:** Что случится, если цвета `_normalColor` и `_selectedColor` одинаковы?
> **О:** `SetSelected` будет вызываться, но цвет не изменится — визуальная обратная связь пропадёт. Функционально панель продолжит работать: выбор отслеживается полем `_selectedItem`, а не цветом фона.

> [!question]
> **В:** Почему `Init` не `Awake`/`Start`? Ведь данные известны заранее?
> **О:** Элемент создаётся через `Instantiate` в `SpawnItem`; данные (`sceneId`, `displayName`) — runtime-данные от `AppStorage`, доступные лишь после `GetAllScenesAsync`. `Awake` выполняется сразу при `Instantiate` до того, как панель получила данные. `Init`-паттерн позволяет разделить создание объекта и его инициализацию данными.

### Связи

[[ScenePickerPanel]] · [[AppStorage]] · [[SceneSelectedEvent]] · [[Паттерн Publish-Subscribe]] · [[Внедрение зависимостей (VContainer)]]
