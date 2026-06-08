---
note_type: script
subsystem: AssetBrowser
listing: 3.32
---

> [!info] Назначение
> `AssetRegistry` — единственная точка поиска ассетов по ссылке `AssetRef`. Агрегирует три библиотеки (`Builtin`/`Imported`/`Saved`) и выбирает нужную по полю `Source`, не зная деталей ни одной из них. Листинг 3.32.

### Обзор

##### Роль и место

`AssetRegistry` реализует `IAssetRegistry` и живёт в корневой области жизни (`RootLifetimeScope`). Граф сцены обращается к нему при загрузке: каждый узел хранит `AssetRef{Source, AssetId}` вместо самой геометрии, а реестр разрешает ссылку в запись `ILabAsset`.

##### Ключевые методы

| Метод | Назначение |
|---|---|
| `Find(AssetRef r)` | switch по `r.Source` → перебор `lib.Assets` → `ILabAsset` или `null` |

---

### Разбор кода

##### Find — switch-выражение на Source

```csharp
IAssetLibrary lib = r.Source switch
{
    AssetSource.Builtin  => _builtin,
    AssetSource.Imported => _imported,
    AssetSource.Saved    => _saved,
    _                    => null,
};
if (lib == null) return null;
foreach (var a in lib.Assets)
    if (a.Id == r.AssetId) return a;
return null;
```

> switch-выражение C# 8 — exhaust pattern, `_` ловит неизвестные значения без исключения. Решение намеренно мягкое: неизвестный `Source` возвращает `null`, не бросает — вызывающий граф сам логирует ошибку. Перебор `lib.Assets` — линейный поиск; при типичном размере библиотеки (десятки записей) это приемлемо. Метод не кэширует результат: библиотека может пополниться импортом между двумя вызовами, поэтому устаревший кэш был бы опасен. `foreach` по `IEnumerable<ILabAsset>` — `Builtin` отдаёт массив, `Imported`/`Saved` — `List<>`, что безопасно без снимка.

---

### К защите

##### Вероятные вопросы

> [!question]
> **В:** Почему `AssetRegistry` сам ничего не кэширует, если поиск линейный?
> **О:** Библиотека может измениться в любой момент (пользователь импортировал ассет, удалил запись). Устаревший кэш отдал бы уже удалённую запись. Граф сцены вызывает `Find` единожды при восстановлении узла, а не в каждом кадре — нагрузка ничтожна.

> [!question]
> **В:** Что произойдёт, если передать `AssetRef` с неизвестным `Source` (например, новый enum-вариант без обновления реестра)?
> **О:** Ветка `_` вернёт `lib = null`, следующий `if` немедленно вернёт `null`. Вызывающий код (обычно `SceneGraph`) логирует это как ошибку восстановления ноды и продолжает загрузку остальных узлов.

> [!question]
> **В:** Почему три библиотеки инжектируются как конкретные типы, а не как `IAssetLibrary`?
> **О:** Регистрации в `RootLifetimeScope` различаются по типу: `BuiltinAssetLibrary` — ScriptableObject, `ImportedAssetLibrary`/`SavedAssetLibrary` — обычные классы. VContainer разрешает их по точному типу. Приведение к общему интерфейсу происходит внутри `Find` — за пределами реестра конкретика не видна.

> [!question]
> **В:** Чем `AssetRegistry.Find` отличается от `AssetSpawner` — не дублируют ли они одно и то же?
> **О:** Нет. `AssetRegistry.Find` отвечает только за поиск записи по ссылке. `AssetSpawner` — за создание GameObject: он берёт запись (которую уже нашёл граф или передал браузер) и вызывает `AssetEntityBuilderRegistry.RestoreAsync`.

---

### Связи

[[AssetSpawner]] · [[ImportPipeline]] · [[Структуры данных ассета]] · [[Внедрение зависимостей (VContainer)]] · [[AssetEntityBuilderRegistry]]
