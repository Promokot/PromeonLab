> [!info] Назначение
> `AppStorage` — центральный класс доступа к сценам: создание, загрузка, сохранение, удаление, кеш в памяти. Относится к подсистеме **StorageCore**. Фрагмент создания и загрузки — **Листинг 3.2**; полный текст — **Приложение Б, Листинг Б.1**.

### Обзор

##### Роль и место

Регистрируется в `RootLifetimeScope` как синглтон. Содержит `Dictionary<string, SceneData> _cache` — кеш загруженных сцен; повторные обращения к уже открытой сцене не порождают дисковый I/O. Делегирует построение путей в [[PathProvider]] и сериализацию — в [[SceneSerializer]]. Управляет полем `_activeSceneId`, по которому другие классы (например, [[SceneAutoSaver]]) знают, какая сцена сейчас открыта.

##### Ключевые методы

| Метод | Суть |
|---|---|
| `CreateSceneAsync` | Генерирует `sceneId`, создаёт каталог, сохраняет начальный `scene.json`, кладёт в кеш |
| `LoadSceneAsync` | Cache-first: возвращает кешированное или читает с диска → десериализует → кеширует |
| `SaveSceneAsync` | Сериализует → записывает поверх → обновляет кеш |
| `DeleteScene` | Рекурсивно удаляет каталог сцены и вычищает кеш |
| `GetAllScenesAsync` | Обходит все подкаталоги `scenes/`, загружает каждый, сортирует по `CreatedAt` |
| `BeginSandboxSession` | Создаёт in-memory запись с `SceneId = "__sandbox__"`, не пишет на диск |
| `GetCachedScene` | Возвращает `SceneData` из кеша или `null`, без дискового чтения |

### Разбор кода

##### CreateSceneAsync — формирование идентификатора

```csharp
var sceneId = Guid.NewGuid().ToString("N")[..8];
```

> `"N"` — формат GUID без дефисов (32 шестнадцатеричных символа). `[..8]` — range-индексатор C# 8, первые 8 символов. Итог: `"3f9c1a72"`. Восемь символов дают 16⁸ ≈ 4 млрд вариантов — достаточно для пользовательской коллекции сцен, при этом имя каталога остаётся коротким и читаемым. Коллизия теоретически возможна, но у одного пользователя нереальна.

##### CreateSceneAsync — порядок операций

```csharp
Directory.CreateDirectory(_paths.SceneRoot(sceneId));
await SaveSceneAsync(data, ct);
_cache[sceneId] = data;
return data;
```

> Три шага строго упорядочены: сначала каталог (иначе `SaveSceneAsync` выбросит `DirectoryNotFoundException`), потом файл, потом кеш. `SaveSceneAsync` внутри тоже пишет в кеш (`_cache[data.SceneId] = data`), так что строка `_cache[sceneId] = data` здесь избыточна, но безвредна. Если `await SaveSceneAsync` бросит исключение, каталог останется на диске — это приемлемо, поскольку при следующем запуске `GetAllSceneIds` просто обнаружит пустой каталог без `scene.json` и `LoadSceneAsync` вернёт `null`.

##### LoadSceneAsync — cache-first

```csharp
if (_cache.TryGetValue(sceneId, out var cached)) return cached;

var path = _paths.SceneJson(sceneId);
if (!File.Exists(path)) return null;

var json = await File.ReadAllTextAsync(path, ct);
var data = SceneSerializer.Deserialize(json);
_cache[sceneId] = data;
return data;
```

> Ранний возврат из кеша — не оптимизация, а архитектурный контракт: после первой загрузки все части приложения получают один и тот же объект `SceneData`. Вызов `File.Exists` перед чтением предотвращает исключение для несуществующего файла и возвращает `null` — сигнал, что сцены нет. `CancellationToken ct` пробрасывается в `File.ReadAllTextAsync`: если пользователь переключит сцену в момент загрузки, операция прерывается чисто.

##### BeginSandboxSession — зарезервированный ID

```csharp
public SceneData BeginSandboxSession()
{
    var data = new SceneData
    {
        SceneId     = "__sandbox__",
        DisplayName = "Sandbox",
        CreatedAt   = DateTime.UtcNow.ToString("yyyy-MM-dd")
    };
    _activeSceneId = data.SceneId;
    return data;
}
```

> `"__sandbox__"` — строковая константа-соглашение, а не `enum`. Именно по ней [[SceneAutoSaver]] определяет, что сохранять не нужно: `if (activeId == "__sandbox__") return;`. Метод намеренно не `async` и не пишет на диск — песочница существует только в памяти и не должна оставлять следов в `scenes/`.

##### GetAllScenesAsync — сортировка по дате

```csharp
result.Sort((a, b) => string.Compare(a.CreatedAt, b.CreatedAt, StringComparison.Ordinal));
return result.ConvertAll(x => (x.SceneId, x.DisplayName));
```

> `CreatedAt` хранится в формате ISO 8601 (`"o"` — round-trip формат `DateTime`). Лексикографическая (`Ordinal`) сортировка даёт хронологический порядок именно потому, что ISO 8601 спроектирован так: более ранняя дата строково меньше. Преобразование через `ConvertAll` скрывает поле `CreatedAt` от вызывающего кода — публичный API возвращает только `(SceneId, DisplayName)`.

### К защите

> [!question] Вероятные вопросы
>
> **В:** Зачем кеш `_cache` и почему он не сбрасывается при сохранении?
> О: Кеш гарантирует, что все части приложения работают с одним экземпляром `SceneData`. `SaveSceneAsync` обновляет кеш той же ссылкой, что записывает на диск, поэтому расхождения нет. Без кеша каждое обращение к сцене порождало бы дисковый I/O, что в главном потоке VR-гарнитуры ведёт к провалам кадровой частоты.
>
> **В:** Почему все методы `async` и как это связано с VR?
> О: `File.ReadAllTextAsync` / `File.WriteAllTextAsync` не блокируют поток рендеринга. В VR целевая частота 72–90 fps; задержка кадра в 16–20 мс ощущается как фриз. Асинхронное I/O освобождает главный поток на время дисковой операции.
>
> **В:** Что происходит, если `CancellationToken` отменяется в середине `LoadSceneAsync`?
> О: `File.ReadAllTextAsync` выбросит `OperationCanceledException`, который пробросится вверх по стеку. Кеш в этом случае не обновляется, данные не теряются. Вызывающий код (`AppStorage`-клиенты) обязан обработать отмену сам.
>
> **В:** Почему `__sandbox__` — строка, а не отдельный флаг или `enum`?
> О: Это соглашение, которое позволяет передавать идентификатор через те же каналы, что и обычный `sceneId`. Альтернатива — отдельный `bool IsSandbox` — потребовала бы изменений в сигнатуре всех методов, принимающих `sceneId`. Строковый зарезервированный ID проще и не требует отдельной ветки в `PathProvider`.
>
> **В:** Что произойдёт, если при `CreateSceneAsync` упадёт `SaveSceneAsync`?
> О: Каталог сцены уже создан, но `scene.json` не записан. При следующем запуске `GetAllScenesAsync` найдёт каталог, `LoadSceneAsync` не найдёт файл и вернёт `null` — сцена будет пропущена в списке. Данные пользователя не повреждены, лишний каталог остаётся безвредным мусором.

### Связи

[[PathProvider]] · [[SceneSerializer]] · [[SceneAutoSaver]] · [[SceneDirtyTracker]] · [[Внедрение зависимостей (VContainer)]] · [[Версионирование схем данных]]
