---
note_type: script
subsystem: SpatialUi / AssetBrowser
listing: "3.33, Б.14"
---

> [!info] Назначение
> `AssetBrowserPanel` — VR-панель браузера ассетов. Три вкладки переключают активную библиотеку; по её записям строится сетка карточек `LabAsset_Item`. Критический метод — `ResolveIcon`: загружает PNG-миниатюру по ссылке `ThumbnailRef` и кэширует результат. Листинги 3.33, Б.14.

### Обзор

##### Роль и место

`MonoBehaviour`, внедрение зависимостей через `[Inject]` метод `Construct`. Живёт в сцене как часть регионной модели (`PanelRegionRouter`). Подписывается на три события: `ModeChangedEvent`, `AssetImportedEvent`, `RegionChangedEvent`.

##### Ключевые методы

| Метод | Назначение |
|---|---|
| `SwitchLibrary` | меняет `_activeLibrary`, вызывает `RefreshGrid` |
| `RefreshGrid` | уничтожает старые карточки, создаёт новые через `Instantiate` |
| `ResolveIcon` | возвращает спрайт: сначала `asset.Icon` (встроенные), иначе грузит PNG из `ThumbnailRef` |
| `OnSpawnClicked` | вычисляет точку 1.2 м перед камерой, публикует `AssetSpawnRequestedEvent` |
| `OnRegionChanged` | возвращает панель после файлового браузера через флаг `_reopenAfterFileBrowser` |

---

### Разбор кода

##### ResolveIcon — загрузка миниатюры с кэшем

```csharp
private Sprite ResolveIcon(ILabAsset asset)
{
    if (asset.Icon != null) return asset.Icon;

    var refPath = asset.ThumbnailRef;
    if (string.IsNullOrEmpty(refPath)) return null;

    if (_thumbCache.TryGetValue(refPath, out var cached)) return cached;

    Sprite sprite = null;
    try
    {
        var abs = _sources.AbsolutePath(refPath);
        if (System.IO.File.Exists(abs))
        {
            var bytes = System.IO.File.ReadAllBytes(abs);
            var tex   = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (tex.LoadImage(bytes))
                sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
    }
    catch (System.Exception ex)
    {
        Debug.LogWarning($"AssetBrowserPanel: failed to load thumbnail '{refPath}'. {ex.Message}");
    }

    _thumbCache[refPath] = sprite;   // cache null too – don't retry a broken ref every rebuild
    return sprite;
}
```

> `new Texture2D(2, 2, ...)` — Unity требует ненулевых размеров при создании; реальный размер выставится после `LoadImage`. `TextureFormat.RGBA32` — формат при создании, но `LoadImage` перезапишет байты и установит итоговый формат из PNG-заголовка; указанный при конструкторе формат влияет лишь на первоначальное выделение памяти.
>
> `_thumbCache[refPath] = sprite` выполняется **даже при `sprite == null`** (см. комментарий в коде). Это намеренно: сломанный путь не будет пересчитываться при каждом `RefreshGrid`. Риск: если файл появился позже (маловероятно), браузер его не подхватит без перезапуска — компромисс ради производительности.
>
> `File.ReadAllBytes` — синхронная операция на главном потоке. Для миниатюры 256×256 (≈196 КБ PNG) на Quest 3 это допустимо; при тысячах ассетов стало бы проблемой.
>
> `Sprite.Create(..., new Vector2(0.5f, 0.5f))` — pivot по центру текстуры. Важно для корректного отображения в UI Image без смещения.

##### OnSpawnClicked — расчёт позиции спавна

```csharp
var fwd = cam.forward;
fwd.y = 0f;
if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
else fwd.Normalize();

var pos = new Vector3(
    cam.position.x + fwd.x * 1.2f,
    0f,
    cam.position.z + fwd.z * 1.2f);

_bus.Publish(new AssetSpawnRequestedEvent
{
    Asset    = _selectedAsset,
    Position = pos,
    Rotation = Quaternion.LookRotation(-fwd, Vector3.up),
});
```

> `fwd.y = 0` обнуляет вертикальную составляющую взгляда, чтобы объект всегда оказывался на полу (y=0), а не в точке взгляда. `sqrMagnitude < 0.001f` — защита от взгляда строго вниз/вверх: в таком случае `fwd` обнуляется и нормализация давала бы NaN. `-fwd` разворачивает объект лицом к пользователю.

##### OnRegionChanged — возврат после файлового браузера

```csharp
if (_reopenAfterFileBrowser
    && string.IsNullOrEmpty(e.OpenModuleId) && !_router.IsOpen("fileBrowser"))
{
    _reopenAfterFileBrowser = false;
    _router.Open("assets");
}
```

> Двойная проверка: `OpenModuleId == null` AND `!IsOpen("fileBrowser")`. Без второго условия успешный выбор файла (который переключает регион на `importWizard`) мог бы ненадолго дать `OpenModuleId == null` в промежуточном состоянии и вернуть браузер поверх мастера. Первый `if` без гарда на регион — ещё одна ловушка: событие `RegionChangedEvent` летит по **всем** регионам (например, при закрытии VR-клавиатуры в регионе `overlays`), поэтому панель проверяет, что событие касается именно её региона через `TryGetModuleRegion("assets", out var myRegion)`.

---

### К защите

##### Вероятные вопросы

> [!question]
> **В:** Почему `_thumbCache` кэширует даже `null`?
> **О:** Чтобы не пытаться перечитать сломанный файл при каждом `RefreshGrid`. Если `File.Exists` вернул `false` или `LoadImage` провалился, повторная попытка даст тот же результат, но съест время и I/O. Закэшированный `null` означает «этот путь проверен, иконки нет».

> [!question]
> **В:** Почему подписки на события делаются в `Start`, а не в `Awake`?
> **О:** К моменту `Awake` VContainer ещё не вызвал `[Inject]`-метод `Construct` — `_bus` может быть `null`. `Start` гарантированно выполняется после завершения внедрения зависимостей в текущем кадре.

> [!question]
> **В:** Как панель возвращается на экран после файлового браузера?
> **О:** Кнопка импорта выставляет `_reopenAfterFileBrowser = true` и открывает `fileBrowser` через `PanelRegionRouter`. Файловый браузер делит с браузером ассетов один регион и скрывает его. Когда регион очищается (мастер импорта закрыт), `OnRegionChanged` замечает пустой `OpenModuleId` и вновь открывает `assets`.

> [!question]
> **В:** Зачем проверять принадлежность `RegionChangedEvent` своему региону?
> **О:** Событие глобальное и летит при любом изменении любого региона. Например, закрытие VR-клавиатуры (регион `overlays`) также даёт `OpenModuleId == null` — без гарда браузер бы открылся в неожиданный момент.

---

### Связи

[[AssetRegistry]] · [[AssetSpawner]] · [[ImportPipeline]] · [[ThumbnailRenderer]] · [[Структуры данных ассета]] · [[Паттерн Publish-Subscribe]] · [[AssetSpawnRequestedEvent]]
