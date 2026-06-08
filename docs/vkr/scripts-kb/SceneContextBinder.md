---
note_type: script
subsystem: Bootstrap
source: Assets/_App/Scripts/Bootstrap/SceneContextBinder.cs
listings: [3.14]
---

> [!info] Назначение
> `SceneContextBinder` — внутрисценный `IStartable`/`IDisposable`, единственный объект, который заполняет и очищает [[SceneContext]]. Регистрируется во всех трёх сценовых областях через `RegisterEntryPoint`. Полный текст — листинг 3.14.

### Обзор

##### Роль и место

Проблема: `SceneContext` — корневой синглтон, а данные для него (конкретный `SceneGraph`, `AnimationAuthoring` и т. д.) существуют только в рамках сценовой области. Нельзя влить их напрямую при создании `SceneContext` (они ещё не существуют), нельзя хранить в корне постоянно (они уничтожаются при смене сцены).

`SceneContextBinder` — мост: он создаётся вместе со сценовой областью, заполняет `SceneContext` из своего `IObjectResolver`, публикует `SceneContextChangedEvent`. При уничтожении области он очищает `SceneContext` и публикует событие снова. Остальные системы не знают ни про `SceneContextBinder`, ни про детали заполнения.

##### Ключевые методы

| Метод | Когда вызывается | Что делает |
|---|---|---|
| `Start()` | VContainer при построении области | Заполняет `SceneContext`, публикует `SceneContextChangedEvent { HasScene = true }` |
| `Dispose()` | VContainer при уничтожении области | Очищает `SceneContext`, публикует `SceneContextChangedEvent { HasScene = false }` |
| `Resolve<T>()` (private) | Внутри `Start()` | Защищённое извлечение сервиса: возвращает `null` при `VContainerException` |

### Разбор кода

##### Конструктор — три зависимости

```csharp
public SceneContextBinder(IObjectResolver resolver, SceneContext ctx, EventBus bus)
{
    _resolver = resolver;
    _ctx      = ctx;
    _bus      = bus;
}
```

> `IObjectResolver` — интерфейс самого VContainer-контейнера текущей области. Через него `Resolve<T>()` обращается к реестру **сценовой** области, не корневой. Это важно: `SceneContextBinder` зарегистрирован в сценовой области — `_resolver` её контейнер, и он видит как сценовые, так и корневые регистрации (иерархия областей). `SceneContext` и `EventBus` — корневые синглтоны, оба инжектятся через родительскую область.

##### Start — Resolve с перехватом

```csharp
public void Start()
{
    _ctx.Bind(
        Resolve<SceneGraph>(),
        Resolve<ISelectionManager>(),
        Resolve<AnimationAuthoring>(),
        Resolve<AnimationClock>());

    _bus.Publish(new SceneContextChangedEvent { HasScene = _ctx.HasScene });
}
```

> Порядок принципиален: сначала `Bind` (заполняем фасад), потом `Publish`. Подписчики `SceneContextChangedEvent` вызываются синхронно внутри `Publish` — к этому моменту `_ctx.Graph` уже не `null`, поэтому любой обработчик может немедленно обращаться к фасаду без гонки.

##### Dispose — зеркало Start

```csharp
public void Dispose()
{
    _ctx.Clear();
    _bus.Publish(new SceneContextChangedEvent { HasScene = _ctx.HasScene });
}
```

> Тот же порядок: сначала `Clear`, потом `Publish`. Когда подписчик события получает `{ HasScene = false }`, `_ctx.Graph` уже `null`. `HasScene` в событии вычисляется **после** `Clear`, поэтому поле `HasScene` в структуре равно `false` — `Graph == null`.

##### Защищённый Resolve

```csharp
private T Resolve<T>() where T : class
{
    try { return _resolver.Resolve<T>(); }
    catch (VContainerException) { return null; } // service not registered in this scope
}
```

> `VContainerException` выбрасывается, если тип `T` не зарегистрирован в текущей (или родительской) области. Это **ожидаемый** сценарий для `AnimationAuthoring` и `AnimationClock` в `SandboxSceneScope` — они там не регистрируются намеренно. Перехват исключения позволяет `SceneContext.Authoring` и `SceneContext.Clock` оставаться `null` в Sandbox, не прерывая построение области. Ограничение `where T : class` нужно, чтобы `null` был допустимым возвращаемым значением.
>
> Это один из немногих случаев в проекте, где исключение используется как управляющая логика (не как ошибка). Альтернатива — явная проверка через `container.TryResolve` или регистрация заглушек; здесь выбран самый компактный вариант.

### К защите

> [!question] Вероятные вопросы
>
> **В:** Зачем отдельный класс `SceneContextBinder`? Почему не заполнить `SceneContext` прямо в `VrEditingSceneScope`?
> **О:** `SceneContextBinder` — единственная точка, которая знает, как заполнять и очищать `SceneContext`. Если бы заполнение было в `Configure` скоупа, логика дублировалась бы в каждом из трёх скоупов (VrEditing, Sandbox — оба нужны). Вынос в отдельный `IStartable`/`IDisposable` даёт единый контракт: `Start` = заполнить, `Dispose` = очистить, и это работает одинаково во всех областях.
>
> **В:** Почему `Resolve<AnimationAuthoring>()` не падает с исключением в Sandbox?
> **О:** Потому что `Resolve<T>()` — приватный вспомогательный метод, который ловит `VContainerException`. Если тип не зарегистрирован — возвращает `null` вместо исключения. `AnimationAuthoring` не зарегистрирована в `SandboxSceneScope`, поэтому `ctx.Authoring == null` в Sandbox — это штатное поведение.
>
> **В:** Что произойдёт, если `SceneContextBinder` зарегистрировать через `Register`, а не `RegisterEntryPoint`?
> **О:** `IStartable.Start()` и `IDisposable.Dispose()` никогда не будут вызваны VContainer. `SceneContext` останется незаполненным, все панели увидят `HasScene == false` даже в VrEditing. `RegisterEntryPoint` обязателен для участия в жизненном цикле области.
>
> **В:** Что будет, если обработчик `SceneContextChangedEvent` обратится к `ctx.Authoring` при `HasScene = true` (Sandbox)?
> **О:** `ctx.Authoring == null`, потому что Sandbox не регистрирует `AnimationAuthoring`. Обращение к методу на `null` вызовет `NullReferenceException`. Потребитель обязан проверять именно `ctx.Authoring != null`, не ограничиваться `ctx.HasScene`.

### Связи

[[SceneContext]] · [[RootLifetimeScope]] · [[Области жизни сцен]] · [[EventBus]] · [[SceneContextChangedEvent]] · [[Внедрение зависимостей (VContainer)]] · [[SceneGraph]] · [[AnimationAuthoring]] · [[AnimationClock]]
