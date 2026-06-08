---
note_type: script
subsystem: Bootstrap
source: Assets/_App/Scripts/Bootstrap/SceneContext.cs
listings: [3.13]
---

> [!info] Назначение
> `SceneContext` — корневой фасад, через который постоянные потребители (UI-панели и другие синглтоны) обращаются к объектам текущей сцены, не удерживая прямых ссылок на них. Заполняется и очищается исключительно [[SceneContextBinder]]. Полный текст — листинг 3.13.

### Обзор

##### Роль и место

Проблема: корневая область (`RootLifetimeScope`) живёт всё время приложения, а внутрисценные объекты (`SceneGraph`, `AnimationAuthoring`, `AnimationClock`) уничтожаются при смене режима. Если панель удерживает прямую ссылку на `SceneGraph`, после смены сцены ссылка указывает на уничтоженный объект — Unity-объект стал `null`, C#-объект завис в памяти.

`SceneContext` — безопасный посредник: он живёт в корневой области (синглтон), а его свойства обнуляются при уходе из сцены. Потребитель проверяет `HasScene` (или конкретное свойство на `null`) и не трогает уничтоженные объекты.

`SceneContext` не публикует события напрямую — об изменении своего состояния он не знает. Публикует `SceneContextChangedEvent` именно [[SceneContextBinder]] из своих `Start`/`Dispose`.

##### Ключевые методы

| Член | Что делает |
|---|---|
| `HasScene` | `Graph != null` — быстрая проверка, что сценная область активна |
| `Bind(...)` | Заполняет все четыре свойства — вызывается только `SceneContextBinder.Start()` |
| `Clear()` | Обнуляет все свойства — вызывается только `SceneContextBinder.Dispose()` |

### Разбор кода

##### Свойства с `private set`

```csharp
public SceneGraph        Graph     { get; private set; }
public ISelectionManager Selection { get; private set; }
public AnimationAuthoring Authoring { get; private set; }
public AnimationClock     Clock     { get; private set; }
```

> `private set` — запись закрыта снаружи класса. Единственные внешние точки записи — `Bind` и `Clear`, оба вызываются только `SceneContextBinder`. Ни одна другая система не может нарушить инвариант «свойства либо все заполнены, либо все null» в обход официального механизма. Это не просто инкапсуляция — это контракт: `SceneContext` хранит консистентное состояние, не допуская частичного заполнения снаружи.

##### HasScene и опасная семантика

```csharp
public bool HasScene => Graph != null;
```

> **Критически важная деталь:** `HasScene` гарантирует только, что `Graph != null`. Он **не** гарантирует, что `Authoring` или `Clock` не `null`. Причина: Sandbox регистрирует `SceneGraph`, но не регистрирует `AnimationAuthoring` и `AnimationClock`. `SceneContextBinder.Resolve<AnimationAuthoring>()` в Sandbox поймает `VContainerException` и вернёт `null` — `Authoring` будет `null` при `HasScene == true`. Правило для потребителя: перед обращением к `ctx.Authoring` проверять именно `ctx.Authoring != null`, не `ctx.HasScene`.

##### Bind — атомарное заполнение

```csharp
public void Bind(SceneGraph graph, ISelectionManager selection,
                 AnimationAuthoring authoring, AnimationClock clock)
{
    Graph = graph; Selection = selection;
    Authoring = authoring; Clock = clock;
}
```

> Все четыре присваивания — в одном методе. Это не атомарность в смысле thread-safe (нет `lock`, C# не гарантирует атомарность нескольких присваиваний), но семантически: `Bind` всегда вызывается один раз в `IStartable.Start()`, который выполняется в главном потоке Unity до того, как область становится видима потребителям. Промежуточного «частично заполненного» состояния, которое мог бы увидеть другой код, не возникает.

##### Clear — обнуление и безопасность

```csharp
public void Clear()
{
    Graph = null; Selection = null;
    Authoring = null; Clock = null;
}
```

> `Clear` вызывается из `IDisposable.Dispose()` `SceneContextBinder` — то есть в момент, когда VContainer уничтожает внутрисценную область (при `LoadSceneMode.Single`). После `Clear` ни одна из ссылок больше не удерживает уничтоженные объекты. Панель, которая в этот момент работает с `ctx.Graph`, увидит `null` — но не уничтоженный C#-объект. Это и есть цель фасада.

### К защите

> [!question] Вероятные вопросы
>
> **В:** Почему не хранить `SceneGraph` напрямую в панели через DI?
> **О:** Потому что `SceneGraph` — сценовый объект: он уничтожается при смене режима. Если панель удерживает прямую ссылку (получила через конструктор при старте VrEditing), после перехода в MainMenu ссылка указывает на уничтоженный объект. `SceneContext` живёт в корневой области и обнуляется при уходе из сцены — панель каждый раз читает актуальное состояние.
>
> **В:** `HasScene` — это достаточная проверка перед вызовом `ctx.Authoring`?
> **О:** Нет. `HasScene == true` означает только `Graph != null`. В Sandbox `Authoring` равен `null` даже при `HasScene == true`, потому что Sandbox не регистрирует `AnimationAuthoring`. Перед обращением к `ctx.Authoring` нужна проверка `ctx.Authoring != null`.
>
> **В:** Кто имеет право вызывать `Bind` и `Clear`?
> **О:** Только `SceneContextBinder` — это зафиксировано как в комментарии исходника («Populated/cleared only by SceneContextBinder»), так и в архитектуре: `SceneContextBinder` — единственный `IStartable`/`IDisposable`, который инжектит `SceneContext`. Ни одна другая система не имеет семантического права менять фасад.
>
> **В:** Что произойдёт, если `AnimatorPanel` обратится к `ctx.Clock` во время SmeneContextChangedEvent?
> **О:** Если событие пришло из `Dispose` (то есть при очистке), `Clear` уже выполнен до публикации события — `ctx.Clock` уже `null`. Если из `Start` (заполнение) — `Bind` выполнен до события, свойства уже заполнены. Порядок в `SceneContextBinder` гарантирован: сначала действие над `_ctx`, потом `_bus.Publish(...)`.

### Связи

[[SceneContextBinder]] · [[RootLifetimeScope]] · [[SceneGraph]] · [[AnimationAuthoring]] · [[AnimationClock]] · [[Области жизни сцен]] · [[SceneContextChangedEvent]] · [[Внедрение зависимостей (VContainer)]]
