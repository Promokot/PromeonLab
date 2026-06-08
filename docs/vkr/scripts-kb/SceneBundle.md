---
note_type: script
subsystem: ExportPipeline
listings: "3.64"
---

> [!info] Назначение
> `SceneBundle` — плоская внешняя схема данных файла `scene.json` внутри экспортного ZIP-пакета. Описывает сцену в формате, адресованном внешним инструментам (например, Blender-аддону). **Односторонняя схема**: не предназначена для обратного импорта в PromeonLab. Переиспользует `BonePose` и `AnimKeyData` из внутренних форматов. Листинг 3.64.

### Обзор

##### Роль и место

Чистый `[Serializable]` POCO — только данные, никакой логики. Сериализуется `JsonUtility.ToJson(bundle, prettyPrint: true)` в `SceneExporter.RunExportAsync`. Заполняется статическим `BuildBundle`. Вложенные классы (`SceneRef`, `Node`, `Animation`, `Track`) — nested, чтобы избежать конфликтов имён с внутренними типами (например, `Animation` — зарезервированный тип Unity-компонентов, здесь это внутренний класс).

Отличие от `scene.json` (внутренняя схема):
- Внутренняя схема (`schemaVersion 3`) — re-importable, содержит `asset-catalog`, ссылки на sources, per-rig bone poses в `NodeData`.
- `SceneBundle` — one-way snapshot: плоский список нод, геометрия — ссылка на файл внутри архива или `geometryMissing = true`.

##### Ключевые поля

| Поле | Тип | Назначение |
|---|---|---|
| `schemaVersion` | `int = 1` | Версия внешней схемы для будущих мигратoров |
| `exportedAtUtc` | `string` | ISO-8601 метка времени экспорта |
| `fps` | `int = 24` | Частота кадров всей сцены (единая для всех контейнеров) |
| `nodes` | `List<Node>` | Плоский список нод (иерархия через `parentNodeId`) |
| `Node.geometryFile` | `string` | Путь внутри архива (`"models/{id}.glb"`) или `""` при missing |
| `Node.geometryMissing` | `bool` | `true` для Builtin-ассетов без исходного файла |
| `Node.animation` | `Animation` | `null` если у ноды нет ActionContainer |
| `Animation.interpolation` | `string` | `"Linear"` или `"Stepped"` (enum → ToString) |
| `Track.targetNodeId` | `string` | Объект или `"bone:{node}:{bone}"` |

### Разбор кода

##### Nested классы и конфликт имён

```csharp
public class Animation
{
    public int    totalFrames;
    public string interpolation;
    public bool   loop;
    public List<Track> tracks = new();
}
```

> Класс назван `Animation`, но он вложен в `SceneBundle`, поэтому полное имя — `SceneBundle.Animation`. Это не конфликтует с `UnityEngine.Animation` (компонент), так как в месте использования (`SceneExporter.BuildAnimation`) он явно квалифицирован. Аналогично `SceneBundle.Track` не конфликтует с другими `Track`-классами.

##### geometryMissing — флаг вместо null

```csharp
public string geometryFile;    // "models/{id}.glb" / "textures/{id}.png", or "" when missing
public bool   geometryMissing; // true when no source file was bundled (e.g. Builtin)
```

> `JsonUtility` не поддерживает `null` для `string` (пишет пустую строку). Отдельный `bool geometryMissing` делает намерение явным: `geometryFile = ""` + `geometryMissing = false` — импортированный ассет без ошибки. `geometryFile = ""` + `geometryMissing = true` — Builtin (нет исходного файла по природе), принимающая сторона должна создать заглушку. Принимающий код может проверить флаг без парсинга строки.

##### Переиспользование BonePose и AnimKeyData

```csharp
public List<BonePose> bonePoses = new();
// ...
public List<AnimKeyData> keys = new();
```

> Структуры поз и ключей взяты напрямую из внутренних форматов без адаптации. Это уменьшает количество типов и исключает маппинг, но создаёт неявную зависимость внешней схемы от внутренних типов. Если внутренний `AnimKeyData` изменит поля — `SceneBundle` тоже изменится. Документально зафиксировано в XML-комментарии класса.

##### Инициализация полей inline

```csharp
public SceneRef  scene = new();
public List<Node> nodes = new();
```

> C# 9 target-typed `new()`. `JsonUtility` требует, чтобы `List<T>`-поля были инициализированы (не null) — иначе сериализует как пустой массив, а десериализует в null. Инициализация inline гарантирует корректный JSON даже для пустых коллекций.

### К защите

##### Вероятные вопросы

> [!question]
> **В:** Чем `SceneBundle` отличается от внутреннего `scene.json` (схема v3)?
> **О:** Внутренняя схема хранит `asset-catalog`, ссылки на sources (`AssetRef`, `SourceRef`), миграционные версии — всё для восстановления сцены. `SceneBundle` — плоский one-way snapshot: иерархия через `parentNodeId`, геометрия — путь внутри ZIP или `geometryMissing`. Обратный импорт не предусмотрен; для Blender-аддона достаточно позиций, иерархии и анимационных данных.

> [!question]
> **В:** Почему `geometryMissing` — отдельный bool, а не `null`-ссылка в `geometryFile`?
> **О:** `JsonUtility` сериализует `null string` как пустую строку `""` — отличить «файл есть, но путь пуст» от «файла нет по природе» невозможно. `bool geometryMissing` делает намерение явным и не зависит от поведения сериализатора.

> [!question]
> **В:** Почему `Animation`, `Track` — вложенные классы, а не отдельные файлы?
> **О:** Они специфичны для внешней схемы и не используются нигде, кроме `SceneBundle`. Вложенность избегает конфликта имён с `UnityEngine.Animation` и внутренними `Track`-классами. Правило «один публичный тип на файл» распространяется на top-level типы; nested — допустимы.

> [!question]
> **В:** Что содержит `SceneBundle.Animation.interpolation` — значение enum или строку?
> **О:** Строку: `container.Interpolation.ToString()` в `SceneExporter.BuildAnimation`. `JsonUtility` не сериализует `enum` как строку без [EnumSerializer], поэтому поле объявлено `string`. Принимающий код парсит строку `"Linear"` или `"Stepped"` самостоятельно.

### Связи

[[SceneExporter]] · [[ExportPanel]] · [[Версионирование схем данных]] · [[Структуры анимационных данных]]
