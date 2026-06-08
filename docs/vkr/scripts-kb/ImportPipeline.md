---
note_type: script
subsystem: AssetBrowser
listing: "3.35, 3.36, Б.16"
---

> [!info] Назначение
> `ImportPipeline` — корневой сервис конвейера импорта. Подписывается на `FilePickedEvent` (выбор файла) и `ImportConfirmedEvent` (подтверждение мастера), выбирает `IAssetImporter` по расширению, асинхронно копирует источник, запекает рецепт, генерирует миниатюру и добавляет запись в `ImportedAssetLibrary`. Листинги 3.35, 3.36, Б.16.

### Обзор

##### Роль и место

Реализует `IStartable, IDisposable` (VContainer.Unity) — автоматический запуск/останов при входе/выходе из области жизни. Живёт в `RootLifetimeScope`. Два зарегистрированных `IAssetImporter`: `GltfAssetImporter` (.glb/.gltf) и `ImageAssetImporter` (.png/.jpg/.jpeg).

##### Ключевые методы

| Метод | Назначение |
|---|---|
| `OnFilePicked` | ищет импортёр по расширению, публикует `ImportRequestedEvent` |
| `OnImportConfirmed` | проверяет `e.Confirmed`, запускает `RunImportAsync` fire-and-forget |
| `RunImportAsync` | основной конвейер: import → build → rig axis → thumbnail → save → publish |
| `GenerateThumbnailAsync` | Reference: ссылка = источник; Object/Rig: off-screen рендер через `ThumbnailRenderer` |
| `HandlerFor` | линейный поиск по `CanHandle(ext)` |

---

### Разбор кода

##### OnFilePicked — выбор импортёра

```csharp
private void OnFilePicked(FilePickedEvent e)
{
    var handler = HandlerFor(e.Path);
    if (handler == null)
    {
        Debug.LogWarning($"ImportPipeline: no handler for '{Path.GetExtension(e.Path)}'");
        return;
    }
    _bus.Publish(new ImportRequestedEvent
    {
        FilePath      = e.Path,
        SuggestedName = Path.GetFileNameWithoutExtension(e.Path),
        SuggestedType = handler.SuggestedType,
    });
}
```

> `HandlerFor` — `_handlers.FirstOrDefault(h => h.CanHandle(ext))` с `ToLowerInvariant()` на расширении. Регистр-нечувствительность важна: на Android файловая система может вернуть `.GLB` в верхнем регистре. Метод **синхронный** — не грузит файл, только проверяет расширение и публикует событие. Тяжёлая работа начнётся только после подтверждения пользователем в мастере.

##### OnImportConfirmed — fire-and-forget

```csharp
private void OnImportConfirmed(ImportConfirmedEvent e)
{
    if (!e.Confirmed) return;
    _ = RunImportAsync(e);
}
```

> `_ = RunImportAsync(e)` — намеренный fire-and-forget. `async void` запрещён по конвенции проекта, поэтому метод возвращает `Task`, а результат отбрасывается через `_`. Исключения пойманы внутри `RunImportAsync` через `try/catch` — без этого выброшенное из `Task` исключение молчаливо потерялось бы. Паттерн «не ждать, но логировать» типичен для Unity event-driven кода.

##### RunImportAsync — основной конвейер

```csharp
var record = await handler.ImportAsync(e.FilePath, e.ChosenType, e.DisplayName, CancellationToken.None);

var recipe = await _builders.BuildAsync(record.Type, _store.AbsolutePath(record.SourceRef), CancellationToken.None);

if (recipe.rig != null)
{
    recipe.rig.TerminalBonesAxis       = e.TerminalBonesAxis;
    recipe.rig.InvertTerminalBonesAxis = e.InvertTerminalBonesAxis;
}

record.SetRecipe(recipe);

await GenerateThumbnailAsync(record, CancellationToken.None);

_library.Add(record);
await _library.SaveAsync(CancellationToken.None);
_bus.Publish(new AssetImportedEvent { AssetId = record.Id });
```

> Порядок строго последовательный: рецепт нужен для миниатюры (тип ассета), миниатюра должна быть готова до сохранения библиотеки, а `AssetImportedEvent` публикуется только после успешного сохранения на диск — иначе браузер мог бы отобразить запись, которую при следующем запуске не восстановить.
>
> `recipe.rig != null` — защита: только ассеты типа `Rig` имеют ненулевой `RigDefinition` в рецепте; для `Object`/`Reference` это поле `null` и установка осей просто пропускается.
>
> `CancellationToken.None` — токен намеренно без отмены: прерванный на полпути импорт оставил бы скопированный файл-источник без записи в библиотеке, что создало бы мусор в хранилище. Полная поддержка отмены потребовала бы транзакционной логики.

##### GenerateThumbnailAsync — Reference vs Rig/Object

```csharp
if (record.Type == AssetType.Reference)
{
    record.SetThumbnailRef(record.SourceRef);
    return;
}

var model = await _loader.LoadAsync(abs, new Vector3(0f, -10000f, 0f), Quaternion.identity, ct);
```

> Для `Reference` (изображение) сам файл-источник и является миниатюрой — `ThumbnailRef = SourceRef`. Для моделей модель паркуется в `(0, -10000, 0)` — заведомо за пределами видимой сцены, чтобы рендер не засветил её в окуляре HMD во время съёмки. `Destroy(model)` в `finally` гарантирует уборку даже при ошибке рендера.

---

### К защите

##### Вероятные вопросы

> [!question]
> **В:** Почему `CancellationToken.None` а не токен, отменяемый при выходе из области жизни?
> **О:** Частично выполнённый импорт (файл скопирован, но запись не сохранена) оставил бы мусор в `sources/`. До реализации транзакционного отката использование `None` — осознанный компромисс безопасности. Отмена добавляется после реализации cleanup-логики.

> [!question]
> **В:** Почему модель при генерации миниатюры паркуется на y = -10000?
> **О:** Камера Quest смотрит вперёд; модель в -10000 по Y гарантированно вне frustum HMD. Off-screen рендер через `cam.targetTexture` не зависит от позиции модели относительно главной камеры, но если модель окажется в зоне видимости — она «мелькнёт» в виде геометрии на один кадр.

> [!question]
> **В:** Как конвейер узнаёт, какой импортёр вызвать?
> **О:** `_handlers` — `IReadOnlyList<IAssetImporter>`, зарегистрированный в VContainer. `HandlerFor` ищет первый, у которого `CanHandle(ext)` возвращает `true`. Порядок в списке не принципиален, так как расширения не пересекаются: `.glb`/`.gltf` — только у `GltfAssetImporter`, `.png`/`.jpg`/`.jpeg` — только у `ImageAssetImporter`.

> [!question]
> **В:** Что случится, если `_library.SaveAsync` выбросит исключение?
> **О:** `catch (Exception ex)` в `RunImportAsync` поймает его, напишет `LogError` и завершит метод. `AssetImportedEvent` не будет опубликован — браузер не обновится. Запись в памяти уже добавлена (`_library.Add`), но на диск не записана: следующий запуск не увидит этот ассет. Это известное ограничение без транзакций.

---

### Связи

[[ImportWizardPanel]] · [[AssetEntityBuilderRegistry]] · [[ThumbnailRenderer]] · [[AssetRegistry]] · [[Структуры данных ассета]] · [[Паттерн Publish-Subscribe]] · [[FilePickedEvent]] · [[ImportConfirmedEvent]] · [[AssetImportedEvent]]
