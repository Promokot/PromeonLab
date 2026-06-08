---
note_type: script
subsystem: ExportPipeline
listings: "3.65, 3.66, Б.35"
---

> [!info] Назначение
> `SceneExporter` — app-lifetime сервис (scope Root), подписывается на `SceneExportRequestedEvent`, строит ZIP-пакет (`scene.json` + скопированные источники моделей/текстур) и записывает его в `Documents/{productName}/{name}.zip`. Захват состояния — синхронно на главном потоке; запись файлов — `Task.Run` на thread-pool. Чистая трансформация данных изолирована в `static BuildBundle`. Листинги 3.65, 3.66, Б.35.

### Обзор

##### Роль и место

Реализует `IStartable` / `IDisposable`. В `Start()` подписывается на `SceneExportRequestedEvent`; в `Dispose()` отписывается. App-lifetime означает: живёт всё время работы приложения, пережив смены сцен. Читает сцену через `SceneContext` (фасад), что обеспечивает доступ к `Graph` и `Authoring` независимо от текущей сцены.

Ключевое архитектурное разделение:
- **Главный поток**: `CaptureSnapshot` графа + `CaptureForExport` анимационных данных + `Resolve` источников → всё sync.
- **Thread-pool**: `WriteZipBundle` — только File IO, никаких Unity API.

##### Ключевые методы

| Метод | Назначение |
|---|---|
| `BuildTargetPath(fileName)` | Чистая функция: санитизация имени + `Path.Combine(ExportDirectory(), name + ".zip")` |
| `BuildBundle(scene, anim, resolve, utc)` | `internal static`: pure transform данных → `SceneBundle` + список `SourceFile` |
| `WriteZipBundle(zipPath, json, sources)` | `internal static`: запись ZIP через `ZipArchive`; никакого Unity API |
| `RunExportAsync(fileName)` | Оркестрация: capture → BuildBundle → JSON → `Task.Run(WriteZipBundle)` → publish |
| `Resolve(AssetRef)` | Instance-метод: ищет запись в `IAssetRegistry`, строит абсолютный путь через `PathProvider` |
| `SanitizeFileName(raw)` | Regex-замена недопустимых символов на `_`; strip `.zip` суффикса |

### Разбор кода

##### BuildBundle — чистая функция, делегат resolve

```csharp
internal static (SceneBundle bundle, List<SourceFile> sources) BuildBundle(
    SceneData scene,
    SceneAnimationData anim,
    Func<AssetRef, AssetResolution> resolve,
    string exportedAtUtc)
```

> `static` гарантирует: внутри нет обращений к полям экземпляра, нет Unity API. Это позволяет вызывать метод из EditMode-тестов без создания полного экземпляра `SceneExporter`.
>
> `Func<AssetRef, AssetResolution> resolve` — делегат разрешения ссылок. В продакшне передаётся `Resolve` (instance-метод, использующий `IAssetRegistry` и `PathProvider`). В тестах — лямбда-заглушка. Инверсия зависимостей без интерфейса: pure function с инжектированной зависимостью.
>
> `anim?.Fps ?? 24` — в Sandbox `_ctx.Authoring == null` → `anim = null` → fps по умолчанию 24.

##### BuildBundle — дедупликация источников

```csharp
var seenEntries = new HashSet<string>();
// ...
if (seenEntries.Add(entry))
    sources.Add(new SourceFile { EntryPath = entry, AbsolutePath = res.SourcePath });
```

> `HashSet<string>.Add` возвращает `false` если элемент уже есть — атомарный check-and-add. Несколько нод могут ссылаться на один и тот же ассет (например, клонированные объекты). Без дедупликации файл попал бы в архив дважды с одинаковым `EntryPath` — `ZipArchive` выбросил бы исключение (дублирующийся путь не допускается).

##### BuildBundle — geometryMissing для Builtin

```csharp
if (res.Source == AssetSource.Imported && res.SourceExists
    && !string.IsNullOrEmpty(res.SourcePath))
{
    // ... geometryFile = entry, geometryMissing = false
}
else
{
    node.geometryFile    = "";
    node.geometryMissing = true;
}
```

> Три условия для включения геометрии: (1) ассет Imported (не Builtin), (2) исходный файл существует на диске, (3) путь не пуст. Любое нарушение → `geometryMissing = true`. Builtin-ассеты — встроенные префабы без исходного файла. Экспортировать их геометрию невозможно, поэтому нода попадает в пакет без геометрии, с трансформацией и анимацией.

##### RunExportAsync — разделение потоков

```csharp
var sceneData = _ctx.Graph.CaptureSnapshot(sceneId, display, createdAt);
var anim      = _ctx.Authoring != null ? _ctx.Authoring.CaptureForExport() : null;

var (bundle, sources) = BuildBundle(sceneData, anim, Resolve, DateTime.UtcNow.ToString("o"));
var json = JsonUtility.ToJson(bundle, prettyPrint: true);

// --- thread pool: write the zip (pure file IO) ---
await Task.Run(() => WriteZipBundle(path, json, sources));
```

> `CaptureSnapshot` и `CaptureForExport` вызываются **до** `await`. Unity API (включая `JsonUtility`) недоступно вне главного потока — `ToJson` тоже здесь. После `await Task.Run` поток вернётся в `SynchronizationContext` Unity (в Unity async/await с правильным контекстом продолжение выполняется на главном потоке).
>
> `WriteZipBundle` получает `string json` (уже готовый) и `IReadOnlyList<SourceFile>` (только пути). Никаких Unity-объектов не передаётся на thread-pool — это ключевое условие потокобезопасности.

##### WriteZipBundle — порядок записей и повторная защита

```csharp
if (File.Exists(zipPath)) File.Delete(zipPath);
using var fs  = new FileStream(zipPath, FileMode.CreateNew);
using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

var jsonEntry = zip.CreateEntry("scene.json");
using (var w = new StreamWriter(jsonEntry.Open()))
    w.Write(sceneJson);

var seen = new HashSet<string>();
foreach (var s in sources)
{
    if (!seen.Add(s.EntryPath)) continue;
    if (!File.Exists(s.AbsolutePath)) continue;
    var entry = zip.CreateEntry(s.EntryPath);
    using var es  = entry.Open();
    using var src = File.OpenRead(s.AbsolutePath);
    src.CopyTo(es);
}
```

> `File.Delete` перед `CreateNew` — если файл уже существует, `FileMode.CreateNew` бросит `IOException`. Удаление явное, не `FileMode.Create` (который бы перезаписал) — намеренно: старый файл уничтожается до начала записи, нет частично перезаписанного архива при ошибке в середине.
>
> `var seen = new HashSet<string>()` — вторая линия защиты от дублей (первая — в `BuildBundle.seenEntries`). Если каким-то образом `sources` содержит дубль — `seen.Add` вернёт false, итерация пропустится. `ZipArchive.CreateEntry` с одинаковым путём не выбросит исключение в режиме Create (создаст второй entry с тем же именем), но ридер может запутаться.
>
> `if (!File.Exists(s.AbsolutePath)) continue` — файл мог быть удалён между `BuildBundle` и `WriteZipBundle` (задержка `await`). Пропуск вместо исключения: пакет будет записан с `geometryMissing` у таких нод (уже установлен в `BuildBundle`).
>
> `src.CopyTo(es)` — потоковое копирование без буферизации в память. Большие GLB-файлы не загружаются целиком — защита от OOM на Quest.

##### Resolve — относительный путь → абсолютный

```csharp
string abs = string.IsNullOrEmpty(asset.SourceRef)
    ? null
    : Path.Combine(_paths.RootForSources, asset.SourceRef);
bool exists = !string.IsNullOrEmpty(abs) && File.Exists(abs);
```

> `asset.SourceRef` — относительный путь от `persistentDataPath/asset-libraries/sources/`. `PathProvider.RootForSources` — абсолютный базовый путь. `Path.Combine` строит абсолютный путь. `File.Exists` — проверка реального наличия файла на диске (файл мог быть удалён вручную).

##### SanitizeFileName — защита от недопустимых символов

```csharp
if (raw.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) raw = raw[..^4];
var invalid = new string(Path.GetInvalidFileNameChars());
var pattern = $"[{Regex.Escape(invalid)}]";
return Regex.Replace(raw, pattern, "_").Trim();
```

> `raw[..^4]` — C# 8 range operator: срез последних 4 символов. Если пользователь ввёл `"export.zip"`, суффикс убирается, чтобы не получить `"export.zip.zip"`.
>
> `Path.GetInvalidFileNameChars()` возвращает символы, недопустимые в именах файлов на текущей платформе (Quest — Android, Linux-подобный; Windows имеет другой набор). `Regex.Escape` экранирует спецсимволы в pattern.

##### ExportDirectory — путь за пределами persistentDataPath

```csharp
private static string ExportDirectory() =>
    Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        Application.productName);
```

> `Environment.SpecialFolder.MyDocuments` на Quest → путь внешнего хранилища, доступный файловым менеджерам. `persistentDataPath` — внутреннее хранилище приложения, недоступное без root. Это намеренное разделение: экспорт адресован пользователю, внутреннее хранилище — только для работы приложения.

### К защите

##### Вероятные вопросы

> [!question]
> **В:** Почему захват состояния и запись файлов разделены на два потока?
> **О:** Unity API (`CaptureSnapshot`, `JsonUtility.ToJson`, обращение к `_ctx`) допустимо только на главном потоке. File IO (запись ZIP, чтение GLB-файлов) может блокировать главный поток на сотни миллисекунд. Разделение: main thread — только захват данных, thread-pool — только IO. Это предотвращает фриз VR-рендеринга во время экспорта.

> [!question]
> **В:** Почему `BuildBundle` объявлен `internal static`?
> **О:** `static` — гарантия отсутствия побочных эффектов через поля экземпляра: pure transform данных. `internal` — доступен из `_App.Tests` (один assembly) для EditMode-тестов без поднятия полного scope. Тесты передают mock-данные и лямбду-resolve, не нужен ни `EventBus`, ни `PathProvider`.

> [!question]
> **В:** Что происходит с Builtin-ассетами при экспорте?
> **О:** У Builtin-ассетов нет исходного файла — это Unity-префабы, встроенные в APK. `Resolve` вернёт `SourceExists = false`. `BuildBundle` установит `geometryMissing = true` и `geometryFile = ""`. Нода попадает в `scene.json` с трансформацией, позами костей и анимацией, но без геометрии. Счётчик таких нод включается в итоговое сообщение.

> [!question]
> **В:** Как дедупликация ассетов работает при нескольких нодах с одним ассетом?
> **О:** `BuildBundle` хранит `HashSet<string> seenEntries` с ключами вида `"models/{assetId}.glb"`. Первая нода добавляет запись и добавляет `SourceFile`; вторая нода с тем же assetId: `seenEntries.Add` возвращает `false` — `SourceFile` не добавляется. В ZIP файл попадает один раз, но обе ноды ссылаются на один `geometryFile`.

> [!question]
> **В:** Зачем `WriteZipBundle` удаляет существующий файл перед записью?
> **О:** `FileMode.CreateNew` выбрасывает `IOException` если файл уже существует. Явное удаление — намеренно: гарантирует, что читатель не получит частично перезаписанный архив. Альтернатива `FileMode.Create` могла бы оставить «хвост» старого файла если новый меньше — невалидный ZIP.

> [!question]
> **В:** Куда записывается ZIP на Quest и почему именно туда?
> **О:** `Environment.SpecialFolder.MyDocuments` на Quest — внешнее хранилище (`/sdcard/Documents`), доступное файловому менеджеру и при подключении по USB. `persistentDataPath` — внутреннее хранилище приложения (недоступно без root/adb). Экспорт адресован пользователю, не внутренней логике — поэтому Documents.

### Связи

[[SceneBundle]] · [[ExportPanel]] · [[SceneContext]] · [[AnimationAuthoring]] · [[Паттерн Publish-Subscribe]] · [[Внедрение зависимостей (VContainer)]] · [[Версионирование схем данных]] · [[SceneExportRequestedEvent]] · [[SceneExportedEvent]]
