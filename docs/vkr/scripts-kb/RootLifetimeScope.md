---
note_type: script
subsystem: Bootstrap
source: Assets/_App/Scripts/Bootstrap/RootLifetimeScope.cs
listings: [3.12, Б.3]
---

> [!info] Назначение
> `RootLifetimeScope` — корневой контейнер зависимостей приложения: регистрирует все объекты с временем жизни приложения, живёт под `PersistentRoot` (то есть переживает любые смены сцен через `DontDestroyOnLoad`). Фрагмент — листинг 3.12, полный текст — листинг Б.3.

### Обзор

##### Роль и место

`RootLifetimeScope` наследует `VContainer.Unity.LifetimeScope` и переопределяет `Configure(IContainerBuilder)`. Это единственное место, где перечислены все объекты с временем жизни всего приложения. Внутрисценные области (`VrEditingSceneScope`, `MainMenuSceneScope`, `SandboxSceneScope`) вкладываются в корневую — объект дочерней области может потребовать зависимость из родительской, но не наоборот.

Объект `RootLifetimeScope` существует под GameObject `PersistentRoot`, который [[AppBootstrap]] помечает `DontDestroyOnLoad` при старте. В результате контейнер и все синглтоны в нём живут столько же, сколько процесс приложения.

##### Ключевые методы

Весь класс — один метод `Configure`. Все остальные поведения наследует `LifetimeScope` из VContainer.

| Способ регистрации | Смысл |
|---|---|
| `builder.Register<T>(Lifetime.Singleton)` | VContainer создаёт экземпляр при первом запросе (ленивая инициализация); не вызывает `Start`/`Tick` |
| `builder.RegisterEntryPoint<T>(Lifetime.Singleton).AsSelf()` | Как `Register`, но VContainer следит за жизненным циклом: вызывает `IStartable.Start()` после построения области, `ITickable.Tick()` каждый кадр, `IDisposable.Dispose()` при уничтожении |
| `builder.RegisterInstance(obj)` | Регистрирует уже существующий объект (ScriptableObject или MonoBehaviour найденный в сцене) — VContainer не создаёт его, только учитывает |
| `builder.RegisterComponentInHierarchy<T>()` | Находит MonoBehaviour в иерархии сцены по типу (аналог `FindObjectOfType`, но разрешённый в `Configure`) |
| `builder.RegisterBuildCallback(c => ...)` | Выполняется после построения всего контейнера; единственное место, где разрешено `FindAnyObjectByType` + `c.Inject()` |

### Разбор кода

##### Register vs RegisterEntryPoint

```csharp
builder.Register<PathProvider>(Lifetime.Singleton);
builder.Register<AppStorage>(Lifetime.Singleton);
builder.Register<EventBus>(Lifetime.Singleton);
// ...
builder.RegisterEntryPoint<ImportedAssetLibrary>(Lifetime.Singleton).AsSelf();
builder.RegisterEntryPoint<SavedAssetLibrary>(Lifetime.Singleton).AsSelf();
```

> Разница принципиальная: `Register` — пассивный объект, VContainer создаёт его, когда он понадобится другому. `RegisterEntryPoint` — активный: VContainer берёт его под управление и сам зовёт `IStartable.Start()` при построении области. `ImportedAssetLibrary` должна прочитать `imported-lib.json` с диска **сразу при старте**, не дожидаясь первого запроса со стороны UI. Без `RegisterEntryPoint` метод `Start` никогда не будет вызван — конструктор не инициализирует файловое состояние, и при первом обращении к библиотеке данных в ней нет. В комментарии исходника это прямо зафиксировано: `Plain Register<T> would NOT collect the entry point, so the library never loads from disk after a restart`.
>
> `.AsSelf()` — важная деталь: `RegisterEntryPoint` по умолчанию регистрирует объект только как `IStartable`/`ITickable`/`IDisposable`. `.AsSelf()` добавляет разрешение по конкретному типу (`ImportedAssetLibrary`), что нужно тем, кто инжектит именно его, а не интерфейс.

##### RegisterInstance для ScriptableObject

```csharp
builder.RegisterInstance(_transitionGraph);
if (_builtinLibrary != null)
    builder.RegisterInstance(_builtinLibrary);
else
    Debug.LogError("RootLifetimeScope: _builtinLibrary not assigned!");
```

> `_transitionGraph` и `_builtinLibrary` — `[SerializeField]`-поля, заполненные в инспекторе Unity. Это ScriptableObject-ассеты: они существуют в памяти ещё до вызова `Configure` (Unity сериализует их при загрузке сцены). VContainer не создаёт их — только берёт готовый экземпляр на учёт через `RegisterInstance`. Проверка на `null` с `Debug.LogError` — явный fail-fast сигнал для разработчика: если поле не назначено в инспекторе, проблема видна немедленно при запуске, а не при первом обращении к зависимости.

##### FindAnyObjectByType внутри Configure — единственный легальный шов

```csharp
var transition = Object.FindAnyObjectByType<SceneTransitionRunner>(FindObjectsInactive.Include);
if (transition != null)
    builder.RegisterInstance(transition).As<ISceneTransition>();
else
    Debug.LogError("RootLifetimeScope: SceneTransitionRunner not found – mode transitions will fail.");
```

> `FindAnyObjectByType` в геймплейном коде запрещён правилами проекта. Исключение — именно этот шов: `Configure` в `LifetimeScope` (и его `RegisterBuildCallback`) — единственное место, где MonoBehaviour, размещённый в сцене, может быть найден и передан DI-контейнеру. `FindObjectsInactive.Include` — ищет и в отключённых объектах (на случай если `PersistentRoot` или его дочерний объект неактивен в момент построения). После `RegisterInstance` все остальные объекты получают `ISceneTransition` через конструктор — никакого `Find*` вне `Configure`.

##### RegisterBuildCallback — инжект MonoBehaviour после сборки контейнера

```csharp
var userPanel = Object.FindAnyObjectByType<UserPanel>(FindObjectsInactive.Include);
if (userPanel != null)
{
    builder.RegisterInstance(userPanel);
    builder.RegisterBuildCallback(c => c.Inject(userPanel));
}
```

> Двухшаговый паттерн: сначала `RegisterInstance` — регистрирует `userPanel` в контейнере, чтобы другие объекты могли его получить. Затем `RegisterBuildCallback` — после того как контейнер построен (все конструкторы отработали), явно вызывается `c.Inject(userPanel)`. Зачем? `UserPanel` — `MonoBehaviour`, его создаёт Unity, не VContainer. Конструктор не работает — зависимости не инжектятся автоматически. `c.Inject` находит все `[Inject]`-аннотированные поля/методы у `userPanel` и заполняет их. Это ещё один легальный шов между Unity-иерархией и VContainer.

##### Fallback-создание ScriptableObject при null

```csharp
var renderProfile = _importRenderProfile != null
    ? _importRenderProfile
    : ScriptableObject.CreateInstance<ImportedAssetShaderProfile>();
if (_importRenderProfile == null)
    Debug.LogWarning("...");
builder.RegisterInstance(renderProfile);
```

> Если поле не назначено в инспекторе, создаётся пустой экземпляр через `ScriptableObject.CreateInstance` — контейнер всегда получает ненулевой объект и не ломается. Логирование: `LogWarning` (не `LogError`) — приложение работает, но с дефолтным профилем. Паттерн «fail-soft с предупреждением» выбран намеренно: отсутствие профиля не критично для работы, просто изменит визуальный вид импортированных моделей.

### К защите

> [!question] Вероятные вопросы
>
> **В:** Чем `RegisterEntryPoint` отличается от `Register`?
> **О:** `Register` регистрирует пассивный объект — VContainer создаёт его при первом запросе. `RegisterEntryPoint` добавляет объект в список «управляемых компонентов» области: VContainer сам вызывает `IStartable.Start()` при построении, `ITickable.Tick()` каждый кадр и `IDisposable.Dispose()` при уничтожении. Без `RegisterEntryPoint` объект, реализующий `IStartable`, никогда не получит вызов `Start` — и, например, библиотека ассетов так и не загрузится с диска.
>
> **В:** Почему `FindAnyObjectByType` разрешён только в `Configure`?
> **О:** По соглашению проекта `FindObjectOfType` и аналоги в геймплейном коде запрещены: они хрупки, не тестируемы и создают неявные зависимости. Исключение — DI-bootstrap-шим внутри `LifetimeScope.Configure` и `RegisterBuildCallback`: это единственная точка, где MonoBehaviour, размещённый в сцене, нужно найти и «отдать» контейнеру. После регистрации все остальные объекты получают зависимость через конструктор — `Find*` больше нигде не нужен.
>
> **В:** Что значит `.As<ISceneTransition>()` при `RegisterInstance`?
> **О:** Конкретный тип `SceneTransitionRunner` регистрируется под интерфейсом `ISceneTransition`. Когда `ModeOrchestrator` запрашивает `ISceneTransition` через конструктор, VContainer отдаст ему именно `SceneTransitionRunner`. Это позволяет `ModeOrchestrator` не знать о конкретном классе, зависеть только от интерфейса — подменяемость для тестов.
>
> **В:** Почему `PersistentRoot` — `GameObject`, а не отдельная сцена?
> **О:** `DontDestroyOnLoad` в Unity применяется к отдельным `GameObject`. При Single-загрузке сцены Unity выгружает все объекты, **кроме** помеченных `DontDestroyOnLoad`. `PersistentRoot` — один корневой объект, под которым живут XR-риг, `UserPanel`, `SceneTransitionRunner` и `RootLifetimeScope`. Один вызов `DontDestroyOnLoad(_persistentRoot)` сохраняет всё это дерево целиком.
>
> **В:** Может ли объект из внутрисценной области зависеть от корневого?
> **О:** Да, это основная идея иерархии: дочерняя область видит регистрации родительской. `SceneAutoSaver` (сценовый) принимает в конструктор `AppStorage` и `EventBus` (корневые) — VContainer находит их в родительском контейнере. Обратное невозможно: корневой объект не может зависеть от сценового, иначе после смены сцены ссылка указывала бы на уничтоженный объект.

### Связи

[[EventBus]] · [[AppBootstrap]] · [[SceneContext]] · [[ModeOrchestrator]] · [[SceneTransitionRunner]] · [[Области жизни сцен]] · [[ImportPipeline]] · [[SceneExporter]] · [[Внедрение зависимостей (VContainer)]]
