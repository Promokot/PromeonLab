---
note_type: script
subsystem: RigBuilder
listing: "3.41"
---

> [!info] Назначение
> `RigDefinitionExtractor` — чистая статическая функция: `SkinnedMeshRenderer` → `RigDefinition`. Извлекает имена костей скелета при импорте. Не создаёт объектов сцены. При отсутствии скелета возвращает `null`, позволяя обработать модель как статический объект. Листинг 3.41.

### Обзор

##### Роль и место

`static class`, без DI, без Unity lifecycle. Вызывается из `RigEntityBuilder.BuildAsync` во время выполнения `ImportPipeline.RunImportAsync`. Результат сохраняется в `AssetEntityRecipe.rig` и хранится в `imported-lib.json`.

##### Ключевые методы

| Метод | Назначение |
|---|---|
| `FromSkinnedMesh(smr)` | `smr.bones` → `RigDefinition` с `BoneRecord` на каждую кость |

---

### Разбор кода

##### FromSkinnedMesh — граничные условия

```csharp
public static RigDefinition FromSkinnedMesh(SkinnedMeshRenderer smr)
{
    if (smr == null || smr.bones == null || smr.bones.Length == 0) return null;

    var def = new RigDefinition { AssetId = smr.gameObject.name };
    foreach (var bone in smr.bones)
        if (bone != null)
            def.Bones.Add(new BoneRecord { BoneName = bone.name });

    return def.Bones.Count > 0 ? def : null;
}
```

> Три условия возврата `null` в первой строке — защита от трёх разных состояний: `smr == null` (у модели нет `SkinnedMeshRenderer`), `smr.bones == null` (Unity инициализировал компонент, но не назначил скелет), `smr.bones.Length == 0` (массив пустой). Все три дают одинаковый результат — «нет скелета».
>
> `if (bone != null)` внутри цикла — дополнительная защита. `smr.bones` — массив `Transform[]`, элементы которого могут быть `null`, если кость была удалена из иерархии после экспорта. Без проверки `BoneName = bone.name` выбросил бы `NullReferenceException`.
>
> `return def.Bones.Count > 0 ? def : null` — финальная проверка. Все `bones` были `null` → список пустой → возвращаем `null` вместо пустого `RigDefinition`. Пустой `RigDefinition` прошёл бы через `recipe.rig != null` и сбил бы логику в `ImportPipeline`.
>
> `AssetId = smr.gameObject.name` — имя корневого GO скинированного меша, обычно имя модели из glTF. Используется для идентификации при отладке, не как ключ поиска (ключ в библиотеке — `record.Id`, короткий случайный).

---

### К защите

##### Вероятные вопросы

> [!question]
> **В:** Почему `RigDefinitionExtractor` — статический класс, а не сервис с DI?
> **О:** Это чистая функция: один вход (`SkinnedMeshRenderer`), один выход (`RigDefinition`), без побочных эффектов и без зависимостей. Нет причин тащить её через DI-контейнер — `static` делает природу функции очевидной и не создаёт ненужного жизненного цикла.

> [!question]
> **В:** Что произойдёт, если модель имеет несколько `SkinnedMeshRenderer`?
> **О:** `RigEntityBuilder.BuildAsync` вызывает `GetComponentInChildren<SkinnedMeshRenderer>` — берётся **первый** в иерархии. Если их несколько (LOD, отдельный меш одежды), все кости первого SMR попадут в `RigDefinition`, остальные SMR игнорируются. Потенциальная неполнота для сложных персонажей — ограничение текущей реализации.

> [!question]
> **В:** Почему извлекатель берёт имена костей из `smr.bones`, а не из иерархии `Transform`?
> **О:** `smr.bones` — это именно те кости, к которым привязан скинированный меш (влияния вершин). Иерархия `Transform` может содержать вспомогательные объекты: IK-таргеты, empty-ноды, контрольные объекты. Взяв только `smr.bones`, мы точно получаем кости, актуальные для деформации меша.

---

### Связи

[[Структуры скелета]] · [[RigEntityFabricator]] · [[ImportPipeline]] · [[Прокси-риг]]
