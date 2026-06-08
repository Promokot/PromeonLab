> [!info] Назначение
> `PathProvider` — единственная точка построения всех файловых путей в приложении. Относится к подсистеме **StorageCore**. Полный текст приведён в **Листинге 3.1** раздела 3.1.1 ВКР.

### Обзор

##### Роль и место

`PathProvider` регистрируется в `RootLifetimeScope` и внедряется в `AppStorage`, `AnimationStorage`, `ImportedSourceProvider` и другие классы, работающие с диском. Остальной код не конкатенирует строки путей вручную: любое изменение структуры хранилища правится ровно в одном файле. Класс не является `MonoBehaviour` и не несёт состояния помимо поля `_root`.

##### Ключевые методы

| Метод / свойство | Возвращает |
|---|---|
| `SceneRoot(sceneId)` | `…/scenes/{id}` |
| `SceneJson(sceneId)` | `…/scenes/{id}/scene.json` |
| `AnimationJson(sceneId)` | `…/scenes/{id}/animation.json` |
| `ScenesRoot()` | `…/scenes` |
| `ImportedLibraryPath` | `…/asset-libraries/imported-lib.json` |
| `SourcePath(assetId, ext)` | `…/asset-libraries/sources/{id}.{ext}` |
| `ThumbnailPath(assetId)` | `…/asset-libraries/thumbnails/{id}.png` |
| `ThumbnailRelativeRef(assetId)` | `asset-libraries/thumbnails/{id}.png` (static, относительный) |
| `RootForSources` | `_root` (для построения абсолютных путей по SourceRef) |

### Разбор кода

##### Двойной конструктор (DI + тест)

```csharp
[VContainer.Inject]
public PathProvider() : this(Application.persistentDataPath) { }

public PathProvider(string root) => _root = root;
```

> Основной конструктор без параметров помечен `[VContainer.Inject]` и делегирует в перегрузку с явным корнем. Это классический тест-хук: продакшн-код получает `Application.persistentDataPath`, а тесты передают произвольный временный каталог напрямую в `new PathProvider(tmpDir)` без поднятия контейнера. `Application.persistentDataPath` нельзя читать из статического инициализатора или вне главного потока Unity — вызов именно в момент инъекции безопасен, потому что DI-bootstrap всегда выполняется в main-потоке на старте.

##### SourcePath — нормализация расширения

```csharp
public string SourcePath(string assetId, string ext)
{
    var clean = string.IsNullOrEmpty(ext) ? ""
        : (ext[0] == '.' ? ext : "." + ext);
    return System.IO.Path.Combine(SourcesDir, assetId + clean);
}
```

> Метод принимает расширение и в любом виде (`".glb"` или `"glb"`). Проверка `ext[0] == '.'` добавляет точку только если её нет, иначе получился бы `"id..glb"`. Граничный случай: пустая строка возвращает путь без расширения — покрывает гипотетические расширения без суффикса, не бросает исключение.

##### ThumbnailRelativeRef — статический метод, а не свойство экземпляра

```csharp
public static string ThumbnailRelativeRef(string assetId) =>
    System.IO.Path.Combine("asset-libraries", "thumbnails", assetId + ".png");
```

> Метод `static` намеренно: относительная ссылка не зависит от `_root` текущего устройства. Она записывается в `AssetEntityRecipe.ThumbnailRef` и при чтении соединяется с `_root` через `RootForSources`. Если бы путь хранился как абсолютный (`/data/data/…/thumbnails/…`), после переустановки приложения или смены устройства записи в библиотеке стали бы нерабочими.

### К защите

> [!question] Вероятные вопросы
>
> **В:** Зачем выделять отдельный класс для путей — разве нельзя писать строки напрямую?
> О: Чтобы описать структуру хранилища ровно в одном месте. Если каталог переименуется или добавится новый файл, правится только `PathProvider`, а не десятки мест в коде. Кроме того, конструктор с явным `root` позволяет тестировать файловую логику в изолированной временной папке без зависимости от реального устройства.
>
> **В:** Почему `ThumbnailRelativeRef` — `static`, а не обычный метод экземпляра?
> О: Потому что результат не зависит от `_root`. Относительная ссылка хранится в записи библиотеки и должна работать после переустановки или переноса приложения: абсолютный путь стал бы недействительным, а относительный всегда правильно соединяется с актуальным `_root` при чтении.
>
> **В:** Как `PathProvider` получает корневую папку на реальном устройстве?
> О: Через `Application.persistentDataPath`, который на Meta Quest указывает на внутреннее хранилище приложения — путь, доступный без специальных разрешений Android. Этот вызов происходит в момент DI-инъекции в главном потоке, что гарантирует его безопасность в отличие от статического инициализатора.
>
> **В:** Как выглядит структура хранилища и где она задана?
> О: Два раздела: `asset-libraries/` (общие данные всех сцен — записи, исходные файлы, миниатюры) и `scenes/{id}/` (данные одной сцены — `scene.json`, `animation.json`). Вся эта структура описана исключительно в `PathProvider`.

### Связи

[[AppStorage]] · [[AnimationStorage]] · [[Версионирование схем данных]] · [[Внедрение зависимостей (VContainer)]] · [[SceneSerializer]]
