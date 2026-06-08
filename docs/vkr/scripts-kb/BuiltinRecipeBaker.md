---
note_type: script
subsystem: Editor / AssetBrowser
listing: "3.34, Б.15"
---

> [!info] Назначение
> `BuiltinRecipeBaker` — editor-only инструмент (папка `Assets/_App/Editor/`, сборка `_App.Editor`). Выпекает `AssetEntityRecipe` в каждую запись `BuiltinLabAsset` через рефлексию, не добавляя редакторских методов в рантайм-типы. Листинги 3.34, Б.15.

### Обзор

##### Роль и место

`static class` без Unity-атрибутов — вызывается из custom Editor (кнопки «Bake All» / «Bake Selected» в Inspector `BuiltinAssetLibrary`). Не компилируется в билд: находится в папке `Editor/`, автоматически исключаемой из Android-сборки.

##### Ключевые методы

| Метод | Назначение |
|---|---|
| `BakeAll(lib)` | перебирает все записи, вызывает `BakeIndex` для каждой, сохраняет SO |
| `BakeOne(lib, index)` | то же для одной записи по индексу |
| `BakeIndex(list, i)` | switch по `AssetType` → вызывает `MeasurePrefab` или генератор Reference-префаба |
| `MeasurePrefab(path, measure)` | загружает префаб в preview-сцену, применяет `measure`, выгружает |

---

### Разбор кода

##### Кэшированные FieldInfo — защита от переименования

```csharp
private static readonly FieldInfo EntriesField =
    typeof(BuiltinAssetLibrary).GetField("_entries", Priv)
    ?? throw new MissingFieldException(nameof(BuiltinAssetLibrary), "_entries");
private static readonly FieldInfo RecipeField =
    typeof(BuiltinLabAsset).GetField("_recipe", Priv)
    ?? throw new MissingFieldException(nameof(BuiltinLabAsset), "_recipe");
private static readonly FieldInfo PrefabField =
    typeof(BuiltinLabAsset).GetField("_prefab", Priv)
    ?? throw new MissingFieldException(nameof(BuiltinLabAsset), "_prefab");
```

> `static readonly` инициализируются один раз при первом обращении к классу. `?? throw` — fail-fast: если поле `_entries` переименовать в рантайм-типе, ошибка возникнет немедленно при первом использовании Baker'а в редакторе, а не молчаливо при запуске билда. `BindingFlags.NonPublic | BindingFlags.Instance` необходимы, так как поля сериализованные приватные.

##### BakeIndex — boxing struct при записи через рефлексию

```csharp
object boxed = entry; // box the struct so reflected SetValue sticks
RecipeField.SetValue(boxed, recipe);
if (generatedPrefab != null)
    PrefabField.SetValue(boxed, generatedPrefab);
list[i] = (BuiltinLabAsset)boxed;
```

> `BuiltinLabAsset` — **struct** (значимый тип). `SetValue` на struct через рефлексию требует упаковки в `object`; без явного `boxed` изменения уйдут в копию и потеряются. После `SetValue` распакованный `(BuiltinLabAsset)boxed` записывается обратно в `list[i]`, иначе обновлённый struct тоже будет потерян. Это классическая ловушка C# struct + reflection.

##### MeasurePrefab — preview-сцена

```csharp
private static AssetEntityRecipe MeasurePrefab(string assetPath, Func<GameObject, AssetEntityRecipe> measure)
{
    var root = PrefabUtility.LoadPrefabContents(assetPath);
    try { return measure(root); }
    finally { PrefabUtility.UnloadPrefabContents(root); }
}
```

> `PrefabUtility.LoadPrefabContents` загружает префаб в изолированную preview-сцену Unity, которая **не является** открытой сценой разработчика. `Awake`/`OnEnable` не вызываются. `finally` гарантирует выгрузку даже при исключении внутри `measure` — без этого preview-сцена осталась бы открытой и следующий вызов выбросил бы ошибку.

##### Persist — сохранение ScriptableObject

```csharp
private static void Persist(BuiltinAssetLibrary lib)
{
    EditorUtility.SetDirty(lib);
    AssetDatabase.SaveAssets();
}
```

> `SetDirty` помечает SO изменённым (Unity не отслеживает изменения через рефлексию автоматически). `SaveAssets` записывает на диск немедленно — без этого изменения сохранились бы только при закрытии редактора с диалогом «Save».

---

### К защите

##### Вероятные вопросы

> [!question]
> **В:** Почему Baker использует рефлексию, а не публичные методы на `BuiltinLabAsset`?
> **О:** Рантайм-типы не должны знать про Editor-операции. Добавление публичного метода `SetRecipe` в `BuiltinLabAsset` означало бы, что рантайм-код может случайно вызвать его в игре. Рефлексия ограничена `Editor/`-сборкой — она физически не скомпилируется в Android-билд.

> [!question]
> **В:** Что такое «build-once / restore-many» применительно к встроенным ассетам?
> **О:** Baker запускается один раз во время разработки и сохраняет результат в ScriptableObject. При каждом запуске приложения билдер просто читает уже готовый `AssetEntityRecipe` — дорогого измерения не происходит. Та же логика работает для импортированных ассетов: рецепт вычисляется при импорте и хранится в `imported-lib.json`.

> [!question]
> **В:** Чем опасен boxing struct при `SetValue` и как это решено?
> **О:** `SetValue` на struct работает через `object`: без явного boxing `entry` получает копию и изменения теряются. Решение — явно упаковать `object boxed = entry`, изменить поля, а затем записать распакованный struct обратно в список `list[i] = (BuiltinLabAsset)boxed`.

---

### Связи

[[AssetEntityBuilderRegistry]] · [[Структуры данных ассета]] · [[RigEntityFabricator]] · [[Внедрение зависимостей (VContainer)]]
