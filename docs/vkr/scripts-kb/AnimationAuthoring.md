---
note_type: script
subsystem: Animation
listings: "3.58, Б.30"
---

> [!info] Назначение
> `AnimationAuthoring` — CRUD-фасад анимационной подсистемы: владеет документом `SceneAnimationData`, публикует события при каждом изменении, запускает дебаунс-запись через `AnimationStorage` и уведомляет `AnimationPlaybackSampler` о пересборке клипов. Реализует разделение A1: бекинг вынесен в `AnimationClipBaker`, сэмплирование — в `AnimationPlaybackSampler`, персистенция — в `AnimationStorage`. Листинги 3.58, Б.30.

### Обзор

##### Роль и место

Зарегистрирован в scope сцены VrEditing. Реализует `IStartable` / `IDisposable` (VContainer). В `Start()` подписывается на `SceneOpenedEvent` и загружает `animation.json` для уже открытой сцены. `CaptureForExport()` отдаёт живой `_data` — ссылку, а не копию; вызывающий код (`SceneExporter`) читает её синхронно на главном потоке до передачи на thread-pool.

Зависимости инжектируются конструктором; `_sampler.Bind(() => _data)` в конструкторе связывает самплер с живым документом через делегат — самплер никогда не держит stale-копию.

##### Ключевые методы

| Метод | Назначение |
|---|---|
| `OwnerOf(nodeId)` | Статический: `"bone:{rig}:{bone}"` → id рига; иначе сам nodeId |
| `SetKey(nodeId, frame)` | Перегрузка: читает `localPosition/Rotation/Scale` из SceneGraph, делегирует |
| `SetKey(nodeId, frame, pos, rot, scale)` | Основной: upsert ключа, события, `RequestSave`, `OnDataChanged` |
| `DeleteKey(nodeId, frame)` | Удаляет ключ; при опустевшей дорожке — удаляет дорожку и публикует `TracksChanged` |
| `CreateContainer` | Два варианта сигнатуры; оба через `FinishCreate` |
| `CaptureForExport()` | Возвращает `_data` напрямую (read-only семантика по соглашению) |
| `RequestSave()` | Вызывает `AnimationStorage.RequestSave` → дебаунс |
| `EnsureData()` | `_data ??= new SceneAnimationData()` — ленивая инициализация |
| `SetSceneFps(int fps)` | Единственный fps всей сцены; `RebuildAllLoopClips` чтобы loop-клипы не устарели |

### Разбор кода

##### OwnerOf — парсинг bone-id

```csharp
public static string OwnerOf(string nodeId)
{
    if (nodeId == null) return null;
    if (!nodeId.StartsWith("bone:")) return nodeId;
    var parts = nodeId.Split(':');
    return parts.Length >= 2 ? parts[1] : nodeId;
}
```

> `Split(':')` на строке `"bone:rigId:boneName"` даёт `["bone", "rigId", "boneName"]`, индекс 1 — id рига. Защита `parts.Length >= 2` — на случай аномального id `"bone:"` без второй части: вернуть исходную строку, а не выбросить исключение. Метод `static` — нет зависимости от состояния экземпляра; вызывается в том числе из `AnimatorPanel`.

##### Конструктор — Bind самплера

```csharp
_sampler?.Bind(() => _data);
```

> Делегат `() => _data` захватывает **поле** `_data` по ссылке. Когда `LoadAsync` присваивает `_data = await _animStorage.LoadAsync(...)`, самплер автоматически читает новый документ при следующем вызове `Data`. Альтернатива — явно передавать документ при каждой загрузке — создала бы риск рассинхрона. `?.` защищает от null в тестах без самплера.

##### SetKey — двойная перегрузка

```csharp
public void SetKey(string nodeId, int frame)
{
    var go = _sceneGraph?.GetNode(nodeId);
    if (go == null) return;
    SetKey(nodeId, frame, go.transform.localPosition, go.transform.localRotation, go.transform.localScale);
}
```

> Первая перегрузка читает **`localPosition/Rotation/Scale`**, не мировые координаты. Анимация в Unity работает в локальном пространстве родителя: если писать мировые координаты, при наличии иерархии объект уедет. Guard `go == null` критичен: нода могла быть удалена между вызовами.

##### SetKey — логика upsert и события

```csharp
bool trackIsNew = c.FindTrack(nodeId) == null;
var track       = c.GetOrCreateTrack(nodeId);
bool existed    = track.HasKey(frame);
track.UpsertKey(frame, pos, rot, scale);

if (trackIsNew)
    _bus.Publish(new AnimationContainerChangedEvent { ... Change = ContainerChange.TracksChanged });

_bus.Publish(new AnimationKeyframeChangedEvent { ... Change = existed ? KeyframeChange.Overwritten : KeyframeChange.Added });
RequestSave();
_sampler?.OnDataChanged(owner);
```

> `trackIsNew` проверяется **до** `GetOrCreateTrack`, потому что после вызова дорожка уже существует. Порядок важен: инвертируй — всегда false.
>
> Два события: `AnimationContainerChangedEvent` (только при новой дорожке → панель перестраивает строки) и `AnimationKeyframeChangedEvent` (всегда → панель обновляет маркеры). Отправлять `TracksChanged` при каждом ключе было бы избыточно.
>
> `RequestSave()` после каждого ключа — это **не** немедленная запись на диск; это уведомление `AnimationStorage`, который дебаунсит запросы и пишет файл спустя задержку. Дорогой I/O не блокирует каждый `SetKey`.

##### DeleteKey — удаление пустой дорожки

```csharp
track.RemoveKey(frame);
bool trackRemoved = false;
if (track.Keys.Count == 0) { c.Tracks.Remove(track); trackRemoved = true; }

_bus.Publish(new AnimationKeyframeChangedEvent { ... Change = KeyframeChange.Removed });
if (trackRemoved)
    _bus.Publish(new AnimationContainerChangedEvent { ... Change = ContainerChange.TracksChanged });
```

> Сначала публикуется `KeyframeChangedEvent` (ключ удалён), потом — `ContainerChangedEvent` (дорожка удалена). Порядок важен: подписчики на `KeyframeChanged` могут читать track.Keys; если дорожка уже удалена из c.Tracks, данные ещё доступны через локальную ссылку `track`. Если бы события шли в обратном порядке, подписчик мог бы пытаться найти дорожку через `FindTrack` и получить null.

##### CaptureForExport

```csharp
public SceneAnimationData CaptureForExport() => _data;
```

> Возвращает живую ссылку, не snapshot. Контракт по соглашению: вызывающий код (`SceneExporter.RunExportAsync`) читает данные на **главном потоке** до `await Task.Run(...)`. После передачи на поток — `_data` больше не трогает. Если бы порядок нарушился (мутация `_data` во время записи zip), возможна гонка данных. Комментарий в коде экспортёра явно разделяет «main thread: capture» и «thread pool: write».

##### EnsureData — ленивая инициализация

```csharp
private void EnsureData() => _data ??= new SceneAnimationData();
```

> `??=` — атомарное присвоение только если null (C# 8). Вызывается перед любой записью (CreateContainer, SetKey, SetSceneFps), но не перед чтением — читающие методы возвращают null/default если `_data == null`, что корректно для сцены без анимации.

##### LoadAsync — async void antipattern избежан

```csharp
public void Start()
{
    _bus.Subscribe<SceneOpenedEvent>(OnSceneOpened);
    var activeId = _storage.ActiveSceneId;
    if (!string.IsNullOrEmpty(activeId))
        _ = LoadAsync(activeId, CancellationToken.None);
}
```

> `_ = LoadAsync(...)` — `Task` дисповат намеренно: `Start()` — синхронный VContainer-callback, не может быть async. Исключение внутри `LoadAsync` не всплывёт без явной обработки, но метод не выбрасывает — только присваивает `_data`. Альтернатива `async void` запрещена правилами CLAUDE.md («No `async void` except Unity lifecycle entry points»), поэтому паттерн `_ = Task` здесь корректен.

### К защите

##### Вероятные вопросы

> [!question]
> **В:** Что такое «разделение A1» и зачем оно сделано?
> **О:** До рефакторинга A1 `AnimationAuthoring` содержал бекинг клипов, сэмплирование и персистенцию в одном файле. A1 разделил обязанности: `AnimationClipBaker` — чистые функции track→clip; `AnimationPlaybackSampler` — ITickable-сэмплирование; `AnimationStorage` — дебаунс-запись animation.json. `AnimationAuthoring` остался CRUD-фасадом и точкой публикации событий. Это упростило тестирование каждой части изолированно.

> [!question]
> **В:** Почему `CaptureForExport` возвращает живую ссылку, а не копию данных?
> **О:** Копирование глубокого графа (все контейнеры → дорожки → ключи) затратно и избыточно, если вызов происходит синхронно на главном потоке. `SceneExporter` гарантирует, что читает данные до `await Task.Run`, то есть до любой потенциальной мутации. Это соглашение задокументировано комментарием в экспортёре и разделением кода на «capture on main thread» / «write on thread pool».

> [!question]
> **В:** Как дебаунс-запись устроена технически?
> **О:** `AnimationAuthoring.RequestSave()` вызывает `AnimationStorage.RequestSave(_data, _sceneId)`. `AnimationStorage` запускает (или перезапускает) отложенный таймер; по его истечении сериализует `_data` в JSON и пишет `animation.json`. Повторные вызовы `SetKey` в рамках одного редактирования продлевают таймер. Это [[Дебаунс записи]]: частые мутации не порождают частый I/O.

> [!question]
> **В:** Почему `SetKey` читает `localPosition`, а не `position`?
> **О:** `AnimationClip.SampleAnimation` применяет значения как локальные трансформации относительно родителя. Если записать мировые координаты, при наличии родительской ноды (рига, который сам двигается) кость окажется в неверной позиции. Вся система анимации в Unity работает в локальном пространстве.

> [!question]
> **В:** Что произойдёт, если `SetKey` вызвать для ноды без контейнера?
> **О:** `_data.FindByOwner(owner)` вернёт `null`, проверка `if (c == null) return` остановит выполнение. Контейнер нужно создать явно через `CreateContainer`. Это намеренное ограничение: ключ нельзя поставить без контейнера.

### Связи

[[AnimatorPanel]] · [[AnimationClock]] · [[AnimationPlaybackSampler]] · [[AnimationClipBaker]] · [[SceneExporter]] · [[Структуры анимационных данных]] · [[Дебаунс записи]] · [[Паттерн Publish-Subscribe]] · [[Внедрение зависимостей (VContainer)]] · [[AnimationContainerChangedEvent]] · [[AnimationKeyframeChangedEvent]] · [[SceneOpenedEvent]]
