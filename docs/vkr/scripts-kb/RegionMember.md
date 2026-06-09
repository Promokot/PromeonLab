---
note_type: script
subsystem: SpatialUi
listings: [Л3.23]
---

> [!info] Назначение
> `RegionMember` — универсальная прослойка между `PanelRegionRouter` и конкретной панелью. Реализует `IRegionSurface` с двойной стратегией: по умолчанию показывает/скрывает через `SetActive`, при наличии другой реализации `IRegionSurface` на том же объекте делегирует всё ей. Относится к подсистеме `SpatialUi`. Листинг 3.23.

### Обзор

##### Роль и место

`RegionMember` — точка входа для маршрутизатора в сторону панели. Он регистрируется в `PanelRegionRouter.RegisterModule` и вызывается через интерфейс `IRegionSurface`. Две панели используют собственную реализацию `IRegionSurface` — `ImportWizardPanel` и `FileBrowserPanel` — и получают управление через делегирование. Все остальные панели управляются через простой `SetActive`.

Класс не требует DI-инъекции — все зависимости разрешаются через `GetComponents` в момент первого обращения.

##### Ключевые методы

| Метод/свойство | Суть |
|---|---|
| `Custom` (property) | Ленивый поиск другой реализации `IRegionSurface` на объекте; кешируется |
| `IsOpen` | Делегирует `Custom.IsOpen` или проверяет `gameObject.activeSelf` |
| `Show()` / `Hide()` | Делегирует или управляет `SetActive` |

### Разбор кода

##### Custom — ленивое разрешение делегата

```csharp
private IRegionSurface Custom
{
    get
    {
        if (!_resolved)
        {
            _resolved = true;
            foreach (var s in GetComponents<IRegionSurface>())
                if (!ReferenceEquals(s, this)) { _custom = s; break; }
        }
        return _custom;
    }
}
```

> `_resolved` — флаг «поиск уже выполнен». Без него `GetComponents` вызывался бы при каждом `Show`/`Hide`/`IsOpen` — это дорогой вызов (сканирует компоненты GameObject). После первого обращения результат кешируется в `_custom` (может быть `null`, если другой реализации нет — это тоже валидный результат).
>
> `ReferenceEquals(s, this)` — исключает сам `RegionMember` из результата поиска. `GetComponents<IRegionSurface>()` возвращает все компоненты на объекте, реализующие интерфейс, включая `this`. Без этого фильтра `Custom` мог бы вернуть `this` и делегирование зациклилось бы (`Show` → `Custom.Show` → `this.Show` → ...).
>
> `break` после первого найденного — берётся только первый «чужой» `IRegionSurface`. Если панель зачем-то имеет два таких компонента, второй игнорируется.

##### Show / Hide / IsOpen — двойная стратегия

```csharp
public bool IsOpen => Custom != null ? Custom.IsOpen : gameObject.activeSelf;

public void Show()
{
    if (Custom != null) Custom.Show();
    else gameObject.SetActive(true);
}

public void Hide()
{
    if (Custom != null) Custom.Hide();
    else gameObject.SetActive(false);
}
```

> Три метода — точное зеркало: все проверяют `Custom` и делегируют либо напрямую управляют объектом. `IsOpen` через `gameObject.activeSelf` возвращает состояние только данного объекта, не принимая во внимание родителей (в отличие от `activeInHierarchy`). Это корректно: маршрутизатор управляет объектом напрямую и ожидает именно `activeSelf`.

### К защите

##### Вероятные вопросы

> [!question]
> **В:** Зачем `RegionMember` вообще нужен, если можно зарегистрировать панель напрямую?
> **О:** Маршрутизатор работает с `IRegionSurface`. Большинство панелей — обычные `GameObject`-объекты, которые не реализуют этот интерфейс. `RegionMember` — это адаптер: навешивается на GameObject и даёт ему нужный интерфейс без изменения самой панели. Панели с особыми требованиями к показу (мастер импорта, файловый браузер) сами реализуют `IRegionSurface`, и `RegionMember` делегирует им.
>
> **В:** Почему `ReferenceEquals`, а не обычный `!=`?
> **О:** `!=` для Unity-объектов переопределён и может возвращать `true` для уничтоженного объекта. `ReferenceEquals` — чистое C#-сравнение ссылок, проверяет идентичность объектов на уровне CLR, без Unity-специфики. Здесь нужно именно это: исключить сам `this`, а не «мёртвые» объекты.
>
> **В:** Почему используется `activeSelf`, а не `activeInHierarchy`?
> **О:** Маршрутизатор управляет только прямым состоянием объекта, не его родителей. `activeSelf` показывает именно то, что установлено через `SetActive`. `activeInHierarchy` зависел бы ещё от родительских объектов — маршрутизатор их не контролирует и не должен зависеть от их состояния.
>
> **В:** Что происходит, если у объекта нет `IRegionSurface` кроме `RegionMember`?
> **О:** `Custom` возвращает `null` после первого поиска. `_resolved = true` кеширует этот результат. Все операции `Show`/`Hide`/`IsOpen` идут через `gameObject.SetActive` — стандартное поведение.

### Связи

[[PanelRegionRouter]] · `IRegionSurface` · [[NavBarConfig]] · [[RegionNavButton]] · [[Регионная модель UI]] · [[Внедрение зависимостей (VContainer)]]
