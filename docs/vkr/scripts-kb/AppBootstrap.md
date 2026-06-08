---
note_type: script
subsystem: Bootstrap
source: Assets/_App/Scripts/Bootstrap/AppBootstrap.cs
listings: [3.19]
---

> [!info] Назначение
> `AppBootstrap` — точка входа всего приложения: помечает `PersistentRoot` как `DontDestroyOnLoad` и запускает первую загрузку сцены главного меню через [[SceneTransitionRunner]]. После первой загрузки стартовая сцена выгружается; выживает только `PersistentRoot`. Листинг 3.19.

### Обзор

##### Роль и место

Unity запускает все MonoBehaviour стартовой сцены в методе `Start`/`Awake`. `AppBootstrap` — MonoBehaviour на объекте стартовой сцены. Его единственная задача: организовать «холодный старт» — создать постоянную инфраструктуру (пометить `PersistentRoot`) и передать управление первой игровой сцене. После того как [[SceneTransitionRunner]] выполнит `LoadSceneMode.Single("MainMenu", ...)`, стартовая сцена с `AppBootstrap` выгрузится; остаётся только `PersistentRoot` и его дочерние объекты.

В отличие от большинства классов проекта, `AppBootstrap` — MonoBehaviour (не POCO): ему нужен вызов `Start` движком Unity и доступ к `DontDestroyOnLoad`. DI-контейнер в этот момент ещё строится, поэтому зависимости получены через `[SerializeField]`, а не через конструктор.

##### Ключевые методы

| Метод | Когда | Что делает |
|---|---|---|
| `Start()` | Unity при запуске стартовой сцены | Прячет курсор, помечает `PersistentRoot`, запускает `LoadInitial` |

### Разбор кода

##### Start — последовательность действий

```csharp
private void Start()
{
    Cursor.visible   = false;
    Cursor.lockState = CursorLockMode.Locked;

    if (_persistentRoot != null) DontDestroyOnLoad(_persistentRoot);

    if (_transitionRunner != null)
        _transitionRunner.LoadInitial(MAIN_MENU_SCENE, null);
    else
        Debug.LogError("AppBootstrap: _transitionRunner not assigned - "
            + "first scene will not load.");
}
```

> **Курсор:** `Cursor.visible = false` и `CursorLockMode.Locked` — стандартная инициализация для VR-приложений: системный курсор мыши скрыт и заблокирован внутри окна. В гарнитуре курсор мыши не нужен; без блокировки он мог бы появляться поверх VR-кадра в некоторых конфигурациях.
>
> **`DontDestroyOnLoad(_persistentRoot)`:** Unity по умолчанию уничтожает все объекты текущей сцены при `LoadSceneMode.Single`. `DontDestroyOnLoad` переводит `_persistentRoot` в специальную «не-сценовую» группу — он не будет уничтожен ни при каком `LoadScene`. Всё дерево под `_persistentRoot` (XR-риг, `UserPanel`, [[SceneTransitionRunner]], [[RootLifetimeScope]]) выживает вместе с ним одним вызовом.
>
> **Порядок:** `DontDestroyOnLoad` вызывается **до** `LoadInitial`. Если бы порядок был обратным, `LoadSceneMode.Single` выгрузил бы стартовую сцену вместе с `_persistentRoot` до того, как он стал «постоянным».
>
> **Null-проверка `_transitionRunner`:** если поле не назначено в инспекторе — `Debug.LogError` с объяснением, первая сцена не загружается. `LogError` (не `LogWarning`) — это критическая неисправность: приложение зависнет на пустой стартовой сцене.

##### MAIN_MENU_SCENE — константа

```csharp
private const string MAIN_MENU_SCENE = "MainMenu";
```

> Имя сцены — строковая константа, не жёсткая строка в коде. Если сцена будет переименована, достаточно поменять одно место. `private const` — не `static readonly`: значение известно на этапе компиляции, подстановка происходит в IL-код напрямую.

##### Что происходит после Start

> После `_transitionRunner.LoadInitial("MainMenu", null)`:
> 1. [[SceneTransitionRunner]] устанавливает альфу в 1 (чёрный экран немедленно) и запускает `RunRoutine`.
> 2. Корутина выполняет `SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single)`.
> 3. Unity выгружает стартовую сцену — `AppBootstrap` уничтожается вместе с ней.
> 4. `PersistentRoot` (уже под `DontDestroyOnLoad`) выживает.
> 5. Загружается MainMenu, строится `MainMenuSceneScope`.
> 6. `onLoaded == null` — callback отсутствует (нет события `ModeChangedEvent` при первой загрузке, это не смена режима).
> 7. Затемнение снимается, пользователь видит главное меню.

### К защите

> [!question] Вероятные вопросы
>
> **В:** Зачем стартовая сцена, если MainMenu всё равно загружается вместо неё?
> **О:** Стартовая сцена — место для размещения `PersistentRoot` и объектов, которые должны пережить все смены сцен: XR-риг, `UserPanel`, `RootLifetimeScope`. Unity не позволяет пометить объекты `DontDestroyOnLoad` до начала `Play` — их нужно разместить в какой-то сцене. Стартовая сцена служит этой «подложкой»: загружается первой, выгружается после `Single`-загрузки MainMenu, но её DontDestroyOnLoad-объекты остаются.
>
> **В:** Почему `DontDestroyOnLoad` вызывается в `Start`, а не в `Awake`?
> **О:** В данном классе нет принципиальной разницы: `DontDestroyOnLoad` можно вызывать и в `Awake`. Выбор `Start` оставляет время для инициализации VContainer-контейнера в `Awake` родительского `LifetimeScope` — к моменту `Start` область уже построена. Порядок `Awake → OnEnable → Start` в Unity гарантирован: `LifetimeScope.Awake` строит контейнер, `AppBootstrap.Start` его использует.
>
> **В:** Что произойдёт, если `_persistentRoot` не назначен?
> **О:** `DontDestroyOnLoad` не будет вызван. После `LoadSceneMode.Single("MainMenu")` Unity выгрузит всю стартовую сцену, включая `RootLifetimeScope`, XR-риг и `SceneTransitionRunner`. Приложение потеряет весь корневой контейнер зависимостей — крах. Разработчик увидит этот баг немедленно при первом запуске.
>
> **В:** Почему `onLoaded == null` при `LoadInitial`?
> **О:** При первом старте нет «предыдущего режима» — событие `ModeChangedEvent` семантически неуместно (мы не переходим из одного режима в другой, мы стартуем). UI навигации и роутер настраиваются при построении области через `RegisterBuildCallback` в [[RootLifetimeScope]] — без дополнительного события. В последующих переходах callback всегда есть: [[ModeOrchestrator]] передаёт лямбду с `Publish(ModeChangedEvent)`.
>
> **В:** Почему `AppBootstrap` — MonoBehaviour, а не чистый C#-класс в VContainer?
> **О:** Три причины: (1) нужен вызов `Start` движком до того, как контейнер построен — POCO-класс в VContainer получит вызов `IStartable.Start()` только после построения; (2) `DontDestroyOnLoad` — метод `Object` (Unity), недоступный без MonoBehaviour/Object; (3) `_persistentRoot` — GameObject в иерархии сцены, его проще всего назначить через `[SerializeField]` на MonoBehaviour в том же инспекторе.

### Связи

[[SceneTransitionRunner]] · [[RootLifetimeScope]] · [[ModeOrchestrator]] · [[Области жизни сцен]] · [[SceneContext]] · [[Внедрение зависимостей (VContainer)]]
