> [!info] Назначение
> `SceneAutoSaver` — автосохранение графа сцены при выходе из режима VR-редактирования: реагирует на `ModeExitingEvent`, синхронно снимает снимок графа, асинхронно записывает на диск. Относится к подсистеме **SceneComposition**. Ключевой метод приведён в **Листинге 3.9** раздела 3.1.4 ВКР.

### Обзор

##### Роль и место

Регистрируется в сцен-скоупе `VrEditing`, реализует `IStartable, IDisposable`. Зависимости: `EventBus` (получение событий), `SceneGraph` (снимок состояния), `AppStorage` (запись на диск и получение метаданных активной сцены). Подписывается на `ModeExitingEvent` — событие, которое `ModeOrchestrator` публикует синхронно, **пока выходящая сцена и её скоуп ещё живы**, до начала выгрузки. Это принципиально: к моменту `ModeChangedEvent` сцена уже выгружена и все сцен-скоупные сервисы уничтожены.

##### Ключевые методы

| Метод | Суть |
|---|---|
| `Start()` | `_bus.Subscribe<ModeExitingEvent>(OnModeExiting)` |
| `Dispose()` | `_bus.Unsubscribe<ModeExitingEvent>(OnModeExiting)` |
| `OnModeExiting(e)` | Фильтрует: `From == VrEditing && To != VrEditing`, запускает `_ = SaveCurrentAsync()` |
| `SaveCurrentAsync()` | Проверяет `activeId`, снимает снимок синхронно, пишет на диск async, публикует `SceneClosedEvent` |

### Разбор кода

##### OnModeExiting — фильтр перехода

```csharp
private void OnModeExiting(ModeExitingEvent e)
{
    if (e.From == AppMode.VrEditing && e.To != AppMode.VrEditing)
        _ = SaveCurrentAsync();
}
```

> Условие `From == VrEditing && To != VrEditing` исключает гипотетический переход `VrEditing → VrEditing` (reload той же сцены). Дисcard `_ =` — аналогично [[AnimationStorage]]: сохранение не должно блокировать синхронный обработчик события. Исключение из `SaveCurrentAsync` поймает `try/catch` внутри, а не пробросится в `EventBus`.

##### SaveCurrentAsync — синхронный снимок до первого await

```csharp
private async Task SaveCurrentAsync()
{
    try
    {
        var activeId = _storage.ActiveSceneId;
        if (string.IsNullOrEmpty(activeId) || activeId == "__sandbox__") return;

        // Capture before any await – scene may unload after the first yield.
        var cached = _storage.GetCachedScene(activeId);
        if (cached == null) return;
        var snap = _graph.CaptureSnapshot(activeId, cached.DisplayName, cached.CreatedAt);

        await _storage.SaveSceneAsync(snap, CancellationToken.None);
        _bus.Publish(new SceneClosedEvent());
    }
    catch (Exception ex)
    {
        Debug.LogError($"SceneAutoSaver failed: {ex}");
    }
}
```

> Комментарий `// Capture before any await – scene may unload after the first yield` — ключевой. В async-методе весь код до первого `await` выполняется синхронно в вызывающем потоке. `CaptureSnapshot` читает живые позиции нод и трансформации из объектов Unity — это возможно только пока сцена загружена. После `await _storage.SaveSceneAsync(...)` Unity может продолжить выгрузку сцены, объекты исчезнут, но `snap` — это уже отдельная структура данных в heap, не зависящая от жизни GameObject'ов.

##### Пропуск "__sandbox__"

```csharp
if (string.IsNullOrEmpty(activeId) || activeId == "__sandbox__") return;
```

> Sandbox — временная сцена без файла на диске (см. [[AppStorage]].`BeginSandboxSession`). Попытка сохранить `"__sandbox__"` означала бы создание каталога и файла `scenes/__sandbox__/scene.json` — мусорный артефакт. Строковая проверка по соглашению — простейшая защита без введения отдельного enum или флага.

##### CancellationToken.None при записи

```csharp
await _storage.SaveSceneAsync(snap, CancellationToken.None);
```

> `CancellationToken.None` — намеренно. К моменту этого `await` скоуп уже начинает уничтожаться; любой «живой» токен скоупа может оказаться отменённым. `CancellationToken.None` гарантирует, что запись дойдёт до конца независимо от состояния скоупа. Потеря данных при выходе была бы критичной ошибкой, поэтому отмена здесь неприемлема.

##### SceneClosedEvent после записи

```csharp
await _storage.SaveSceneAsync(snap, CancellationToken.None);
_bus.Publish(new SceneClosedEvent());
```

> `SceneClosedEvent` публикуется **после** успешной записи. Если `SaveSceneAsync` бросит исключение, `catch` перехватит его и событие не будет опубликовано. Подписчики `SceneClosedEvent` (например, UI очистки) не должны реагировать, если данные не сохранены. При ошибке в лог пишется `Debug.LogError`, сцена закрывается без уведомления подписчиков.

### К защите

> [!question] Вероятные вопросы
>
> **В:** Почему `SceneAutoSaver` подписывается на `ModeExitingEvent`, а не на `ModeChangedEvent`?
> О: `ModeChangedEvent` публикуется после загрузки новой сцены — к этому моменту выходящая сцена уже выгружена и её скоуп уничтожен. `SceneAutoSaver` — сцен-скоупный сервис VrEditing и к тому времени сам уничтожен. `ModeExitingEvent` публикуется синхронно **до** начала выгрузки: выходящая сцена ещё загружена, `SceneGraph` живёт, снимок можно снять.
>
> **В:** Почему снимок снимается до первого `await`?
> О: В async-методе код до первого `await` выполняется синхронно. `CaptureSnapshot` читает живые позиции объектов Unity. После первого `await` управление возвращается в event loop, Unity может продолжить выгрузку сцены, GameObject'ы исчезнут. Снимок (`SceneData`) — независимая структура данных в памяти, не привязанная к жизни объектов сцены.
>
> **В:** Что происходит, если при сохранении возникнет ошибка?
> О: Исключение перехватывается `catch (Exception ex)` и пишется в лог через `Debug.LogError`. `SceneClosedEvent` не публикуется. Данные с диска не повреждаются — запись атомарна на уровне `File.WriteAllTextAsync`. Предыдущая версия `scene.json` остаётся нетронутой.
>
> **В:** Почему при записи передаётся `CancellationToken.None`, а не токен скоупа?
> О: К моменту `await SaveSceneAsync` скоуп VrEditing начинает уничтожаться. Живой токен скоупа мог бы быть уже отменён, что прервало бы запись на середине. Потеря данных при выходе из режима — критичный сбой, поэтому отмена записи намеренно запрещена.
>
> **В:** Зачем проверка `activeId == "__sandbox__"`?
> О: Sandbox — in-memory сцена без каталога на диске (нет `scenes/__sandbox__/`). Попытка записать её создала бы мусорный файл. Проверка исключает sandbox без дополнительного флага или enum — соглашение по строке достаточно, так как `"__sandbox__"` нигде не может быть валидным GUID-фрагментом.

### Связи

[[SceneDirtyTracker]] · [[AppStorage]] · [[SceneGraph]] · [[EventBus]] · [[ModeExitingEvent]] · [[SceneClosedEvent]] · [[Паттерн Publish-Subscribe]] · [[Внедрение зависимостей (VContainer)]] · [[AnimationStorage]]
