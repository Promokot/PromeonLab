---
aliases:
  - NavBarConfig
note_type: script
subsystem: SpatialUi
listings: [Л3.21, БЛ6]
---

> [!info] Назначение
> `PanelRegionRouter` — маршрутизатор регионной модели UI. Хранит реестр панелей (`IRegionSurface`) и кнопок (`RegionNavButton`), обеспечивает единственность открытой панели в каждом регионе, публикует `RegionChangedEvent`, перестраивает состав интерфейса при смене режима. Объект корневой области жизни (`RootLifetimeScope`). Листинг 3.21 (фрагмент) и Приложение Б, Листинг Б.6 (полный). Конфигурация регионов — [[NavBarConfig]].

### Обзор

##### Роль и место

Класс — pure C# (`IDisposable`), не `MonoBehaviour`. Зарегистрирован в `RootLifetimeScope` с временем жизни приложения. Зависит от `IRegionConfig` (реализация — `NavBarConfig`) и корневого `EventBus`.

Три коллекции:

| Поле | Тип | Содержимое |
|---|---|---|
| `_modules` | `Dictionary<string, IRegionSurface>` | панели по id |
| `_buttons` | `Dictionary<string, RegionNavButton>` | кнопки по moduleId |
| `_openByRegion` | `Dictionary<string, string>` | текущий открытый модуль в регионе |

Регистрация идемпотентна: `_modules[moduleId] = surface` перезаписывает старую запись без ошибки. Это позволяет панелям сцены перерегистрироваться после перезагрузки.

##### Ключевые методы

| Метод | Суть |
|---|---|
| `Open(moduleId)` | Открывает панель; скрывает прежнего владельца региона; публикует `RegionChangedEvent` |
| `Close(moduleId)` | Скрывает панель; если регион опустел — открывает панель по умолчанию |
| `Toggle(moduleId)` | `IsOpen ? Close : Open` |
| `ApplyMode(AppMode)` | Закрывает недопустимые панели, открывает дефолты, обновляет кнопки |
| `TryGetAlive` | Проверяет живость Unity-объекта; выбрасывает мёртвые записи |

### Разбор кода

##### Open — открытие с вытеснением

```csharp
public void Open(string moduleId)
{
    if (!TryGetAlive(moduleId, out var surface)) return;

    if (_config.TryGetRegion(moduleId, out var region) && !string.IsNullOrEmpty(region))
    {
        if (_openByRegion.TryGetValue(region, out var current) && current != moduleId
            && TryGetAlive(current, out var currentSurface))
        {
            currentSurface.Hide();
            ApplyButtonState(current);
        }
        _openByRegion[region] = moduleId;
        surface.Show();
        ApplyButtonState(moduleId);
        _bus.Publish(new RegionChangedEvent { RegionKey = region, OpenModuleId = moduleId });
    }
    else
    {
        surface.Show();
        ApplyButtonState(moduleId);
    }
}
```

> `current != moduleId` — защита от самовытеснения: если `Open("settings")` вызван, когда `settings` уже открыт, прежний владелец не скрывается (иначе панель мигнула бы). Только потом устанавливается `_openByRegion[region] = moduleId` и вызывается `Show()`.
>
> Панели без региона (пустой `ExclusiveGroup`) обрабатываются веткой `else`: они показываются независимо, не вытесняют других и не регистрируются в `_openByRegion`. `RegionChangedEvent` для них не публикуется.

##### Close — закрытие с восстановлением дефолта

```csharp
public void Close(string moduleId)
{
    if (!TryGetAlive(moduleId, out var surface)) return;
    surface.Hide();
    ApplyButtonState(moduleId);

    if (_config.TryGetRegion(moduleId, out var region) && !string.IsNullOrEmpty(region)
        && _openByRegion.TryGetValue(region, out var current) && current == moduleId)
    {
        _openByRegion.Remove(region);
        _bus.Publish(new RegionChangedEvent { RegionKey = region, OpenModuleId = null });

        if (_config.TryGetRegionDefault(region, out var def) && def != moduleId)
            Open(def);
    }
}
```

> `current == moduleId` — проверяет, что именно закрываемый модуль сейчас считается открытым в регионе. Если регион уже переключился на другой модуль (например, через быстрый двойной вызов), запись в `_openByRegion` не трогается.
>
> `def != moduleId` — предотвращает рекурсивный вызов `Open(def)` → `Close(def)` → `Open(def)` в случае, когда дефолтный модуль является самим собой.
>
> `Open(def)` в конце `Close` — это нерекурсивный вызов: `def != moduleId` уже проверен, и открываемый дефолт не будет сразу закрыт. Но важно, что `_openByRegion.Remove(region)` вызван до `Open(def)`, иначе `Open` нашёл бы в `_openByRegion` себя и не выполнял бы вытеснение.

##### ApplyMode — перестройка при смене режима

```csharp
public void ApplyMode(AppMode mode)
{
    _mode = mode;

    List<string> toClose = null;
    foreach (var kv in _modules)
        if (Alive(kv.Value) && kv.Value.IsOpen && !_config.IsVisibleInMode(kv.Key, mode))
            (toClose ??= new List<string>()).Add(kv.Key);
    if (toClose != null)
        foreach (var id in toClose) Close(id);

    EnsureRegionDefaults(mode);

    foreach (var kv in _buttons)
        ApplyButtonState(kv.Key);
}
```

> `toClose ??= new List<string>()` — ленивое создание списка: при отсутствии закрываемых панелей аллокация не происходит. Критично: итерация по `_modules` производится отдельно от вызовов `Close`, потому что `Close` может изменить `_modules` через `TryGetAlive` (удаление мёртвых записей). Если бы `Close` вызывался прямо в `foreach` по `_modules` — `InvalidOperationException`.

##### TryGetAlive — сборщик мусора регистра

```csharp
private bool TryGetAlive(string moduleId, out IRegionSurface surface)
{
    if (_modules.TryGetValue(moduleId, out surface))
    {
        if (Alive(surface)) return true;
        _modules.Remove(moduleId);
    }
    surface = null;
    return false;
}

private static bool Alive(IRegionSurface s) =>
    s != null && !(s is UnityEngine.Object uo && uo == null);
```

> `s is UnityEngine.Object uo && uo == null` — это Unity-специфичная проверка. Unity переопределяет оператор `==` для своих объектов: уничтоженный GameObject возвращает `true` при сравнении с `null`, но C#-ссылка при этом остаётся ненулевой. Обычный `s != null` пропустил бы уничтоженный объект. Паттерн `is UnityEngine.Object uo` + `uo == null` использует Unity-оператор и корректно обнаруживает «мёртвый» объект.
>
> При обнаружении мёртвого объекта запись удаляется из `_modules` прямо при обращении — ленивая очистка без отдельного прохода.

### К защите

##### Вероятные вопросы

> [!question]
> **В:** Что такое регион и как обеспечивается единственность открытой панели?
> **О:** Регион — это строковый ключ `ExclusiveGroup` в `NavBarConfig`. Словарь `_openByRegion` хранит только один `moduleId` на регион. При `Open` прежний владелец скрывается до показа нового. Таким образом, в каждом регионе может быть открыта не более одной панели одновременно.
>
> **В:** Почему при закрытии автоматически открывается панель по умолчанию?
> **О:** Чтобы регион не остался пустым. Если пользователь закрывает единственную открытую панель, маршрутизатор ищет запись с `IsRegionDefault = true` для этого региона и открывает её. Если дефолт — сам закрываемый модуль, рекурсия предотвращается проверкой `def != moduleId`.
>
> **В:** Зачем список `toClose` в `ApplyMode` вместо вызова `Close` прямо в `foreach`?
> **О:** `Close` вызывает `TryGetAlive`, который может удалить запись из `_modules`. Изменение коллекции во время итерации по ней даёт `InvalidOperationException`. Сбор идентификаторов в отдельный список разрывает итерацию и мутацию.
>
> **В:** Как маршрутизатор обнаруживает уничтоженные Unity-объекты в реестре?
> **О:** Через `Alive(s)`: Unity переопределяет `==` для `UnityEngine.Object`, и уничтоженный объект равен `null` через этот оператор, хотя C#-ссылка ненулевая. Проверка `s is UnityEngine.Object uo && uo == null` использует именно Unity-оператор и удаляет запись при первом же обращении.
>
> **В:** Что происходит при перезагрузке сцены с панелями сцены?
> **О:** Сцена-уровень уничтожает свои объекты. Записи в `_modules` остаются, но помечаются как мёртвые. При следующей сцене новые `RegionMember`-компоненты повторно вызывают `RegisterModule`, перезаписывая записи идемпотентно. Мёртвые записи параллельно удаляются в `TryGetAlive`.

### Связи

[[NavBarConfig]] · [[RegionMember]] · [[RegionNavButton]] · [[IRegionSurface]] · [[RegionChangedEvent]] · [[ModeChangedEvent]] · [[UserPanel]] · [[SpatialPanel]] · [[Регионная модель UI]] · [[Паттерн Publish-Subscribe]] · [[Внедрение зависимостей (VContainer)]]
