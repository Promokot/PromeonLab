---
note_type: script
subsystem: AssetBrowser
listing: "3.39, Б.19"
---

> [!info] Назначение
> `AssetEntityBuilderRegistry` — диспетчер по типу ассета для двух операций: `BuildAsync` (выпечка рецепта при импорте) и `RestoreAsync` (восстановление `GameObject` при спавне/загрузке). После восстановления в одной точке применяет `InteractionCapability` и регистрирует селекторные коллайдеры костей. Листинги 3.39, Б.19.

### Обзор

##### Роль и место

Обычный C# класс в `RootLifetimeScope`. Хранит `Dictionary<AssetType, IAssetEntityBuilder>`. Три реализации `IAssetEntityBuilder`: `ObjectEntityBuilder`, `RigEntityBuilder`, `ReferenceEntityBuilder`.

##### Ключевые методы

| Метод | Назначение |
|---|---|
| `BuildAsync(type, path, ct)` | делегирует `IAssetEntityBuilder.BuildAsync` — вычисляет рецепт при импорте |
| `RestoreAsync(asset, pos, rot, ct)` | восстанавливает GameObject + применяет `InteractionCapability.Apply` + BoneBoxes |
| `Resolve(type)` | `_byType.TryGetValue` с `NotSupportedException` при отсутствии |

---

### Разбор кода

##### RestoreAsync — единственная точка финализации

```csharp
public async Task<GameObject> RestoreAsync(ILabAsset asset, Vector3 position, Quaternion rotation, CancellationToken ct)
{
    var recipe = asset.Recipe;

    if (recipe == null && asset.Source == AssetSource.Builtin)
        throw new NotSupportedException(
            $"Builtin asset '{asset.Id}' has no baked recipe – bake it in the BuiltinAssetLibrary inspector.");

    var go = await Resolve(asset.Type).RestoreAsync(asset, recipe, position, rotation, ct);

    if (go != null && recipe != null)
        InteractionCapability.Apply(go, recipe.interactionLayer, recipe.colliderKind,
            recipe.colliderCenter, recipe.colliderSize, recipe.selectable);

    if (go != null && recipe != null && recipe.colliderKind == ColliderKind.BoneBoxes)
        go.GetComponent<ProxyRigRuntime>()?.RegisterSelectorColliders();

    return go;
}
```

> Первый `if` — fail-fast для незапечённого встроенного ассета. `Saved` с `recipe == null` не бросает — будущая реализация `SavedAssetLibrary` может предоставить другой путь восстановления без рецепта.
>
> `InteractionCapability.Apply` — единственная точка назначения слоя взаимодействия, коллайдера и `Selectable`. Билдеры создают только геометрию и намеренно не знают о взаимодействии. Это разделение позволяет менять параметры взаимодействия в рецепте без правок билдеров.
>
> `ColliderKind.BoneBoxes` — специальный случай: коллайдер выбора рига уже построен `RigEntityFabricator.BuildSelectorColliders`, но не зарегистрирован с `XRPromeonInteractable` (его ещё не существовало в момент постройки). `RegisterSelectorColliders` делает это после `Apply`, когда interactable создан. Порядок критичен: `Apply` → `RegisterSelectorColliders`.

##### Resolve — строгий словарь

```csharp
private IAssetEntityBuilder Resolve(AssetType type)
{
    if (!_byType.TryGetValue(type, out var b))
        throw new NotSupportedException($"No entity builder registered for asset type {type}");
    return b;
}
```

> `NotSupportedException` при отсутствии билдера — намеренно строгий. Silent fallback привёл бы к невидимому объекту без коллайдера. Ошибка поймается в `try/catch` `AssetSpawner.SpawnCoreAsync` и попадёт в лог с конкретным сообщением.

##### Конструктор — регистрация по HandledType

```csharp
public AssetEntityBuilderRegistry(IReadOnlyList<IAssetEntityBuilder> builders)
{
    foreach (var b in builders) _byType[b.HandledType] = b;
}
```

> Если два билдера регистрируют один `HandledType` — второй тихо перезаписывает первый. Это поведение Unity VContainer при регистрации `IReadOnlyList<>`: порядок определяется порядком `builder.Register<>()` в `LifetimeScope`. Потенциальная ловушка при добавлении нового билдера-дублёра.

---

### К защите

##### Вероятные вопросы

> [!question]
> **В:** Почему `InteractionCapability.Apply` вызывается в реестре, а не внутри каждого билдера?
> **О:** Единственная точка — гарантия консистентности. Если каждый билдер применял бы capability по-своему, при добавлении нового билдера легко пропустить коллайдер или слой. Реестр видит всё: и тип ассета, и рецепт, и готовый GO.

> [!question]
> **В:** Почему встроенный ассет без рецепта бросает исключение, а не возвращает null?
> **О:** `null` GO молчаливо не появится в сцене — пользователь не поймёт, что произошло. `NotSupportedException` с конкретным сообщением («bake it in the inspector») сразу указывает разработчику на действие. Ошибка попадает в лог через `catch` в `AssetSpawner`.

> [!question]
> **В:** Что такое принцип «build-once / restore-many» и как реестр его реализует?
> **О:** При импорте `BuildAsync` вычисляет рецепт (загружает модель, измеряет, сохраняет параметры). При каждом спавне и при загрузке сцены `RestoreAsync` читает уже готовый рецепт из записи — дорогого измерения нет. Реестр реализует этот принцип, разделяя `BuildAsync` (вызывается один раз в `ImportPipeline`) и `RestoreAsync` (вызывается многократно).

---

### Связи

[[AssetSpawner]] · [[ImportPipeline]] · [[RigEntityFabricator]] · [[ProxyRigRuntime]] · [[Структуры данных ассета]] · [[Внедрение зависимостей (VContainer)]] · [[Прокси-риг]]
