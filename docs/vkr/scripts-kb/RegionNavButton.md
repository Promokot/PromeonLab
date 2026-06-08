---
note_type: script
subsystem: SpatialUi
listings: [Л3.22, БЛ7]
---

> [!info] Назначение
> `RegionNavButton` — тонкая кнопка навигационной панели. Хранит только строковый `_moduleId` и по нажатию вызывает `PanelRegionRouter.Toggle`. Видимость и подсветка управляются снаружи маршрутизатором. Относится к подсистеме `SpatialUi`. Листинг 3.22 (фрагмент) и Приложение Б, Листинг Б.7 (полный).

### Обзор

##### Роль и место

Кнопка не хранит ссылку на панель и не подписывается на события режима или региона самостоятельно. Вся логика видимости и активности делегирована `PanelRegionRouter`, который вызывает `SetVisible` и `SetActiveHighlight` извне. Связь «кнопка – конфиг – панель» держится исключительно на совпадении строкового `_moduleId` во всех трёх местах.

`[Inject] Construct` вызывается VContainer до `Awake`/`OnEnable`, потому что `UserPanel` стартует **неактивным** — Unity не вызывает `Awake` на неактивных объектах. Поэтому настройка цветов и подключение обработчика собраны в ленивый идемпотентный метод `EnsureSetup`, который выполняется при первом из жизненных вызовов.

##### Ключевые методы

| Метод | Суть |
|---|---|
| `Construct(router)` | DI-инъекция роутера; вызывается до Awake |
| `EnsureSetup()` | Ленивая настройка цветов и подключение `OnClick`; защита от повтора |
| `SetVisible(bool)` | Включает/выключает GameObject; охраняет от лишнего `SetActive` |
| `SetActiveHighlight(bool)` | Переключает цвет кнопки — активный/неактивный |
| `OnClick()` | Единственная точка взаимодействия: `_router?.Toggle(_moduleId)` |

### Разбор кода

##### Inject и ленивая настройка

```csharp
[Inject]
public void Construct(PanelRegionRouter router) => _router = router;

private void Awake()    => EnsureSetup();
private void OnEnable()  => EnsureSetup();
```

> `[Inject]` метод вызывается VContainer при построении контейнера — объект в этот момент может быть неактивным, `Awake` ещё не выполнен. Поэтому настройку нельзя делать в `Construct` (кнопка `_button` может быть ещё не инициализирована) и нельзя делать только в `Awake` (если объект будет активирован после инъекции — `Awake` уже не повторится). `EnsureSetup` вызывается из обоих: первый вызов выполняет настройку, последующие — нет (флаги `_colorsReady`, `_listenerAttached`).

##### EnsureSetup — идемпотентная инициализация

```csharp
private void EnsureSetup()
{
    if (_button == null) return;

    if (!_colorsReady)
    {
        var baseColor = _button.colors.normalColor;
        var block     = _button.colors;

        var activeBase = Brighten(baseColor, _activeBrightness);

        _inactiveColors                  = block;
        _inactiveColors.normalColor      = baseColor;
        _inactiveColors.highlightedColor = Hover(baseColor);
        _inactiveColors.selectedColor    = baseColor;

        _activeColors                  = block;
        _activeColors.normalColor      = activeBase;
        _activeColors.highlightedColor = Hover(activeBase);
        _activeColors.selectedColor    = activeBase;

        _colorsReady = true;
    }

    if (!_listenerAttached)
    {
        _button.onClick.AddListener(OnClick);
        _listenerAttached = true;
    }

    ApplyColors();
}
```

> `var block = _button.colors` — `ColorBlock` — это `struct`, поэтому `block` является копией. Последующие присваивания (`_inactiveColors = block`, `_activeColors = block`) также создают копии. Изменение `_inactiveColors.normalColor` не затрагивает оригинал кнопки и не затрагивает `_activeColors`. Без понимания этой семантики студент может предположить, что все три ссылаются на один объект.
>
> `_listenerAttached` — защита от двойного `AddListener`. Без флага при каждом `OnEnable` добавлялся бы ещё один экземпляр `OnClick`, и одно нажатие вызывало бы `Toggle` многократно. `OnDestroy` симметрично удаляет слушатель.

##### Hover и Brighten — цветовая логика

```csharp
private static Color Hover(Color c)
{
    Color.RGBToHSV(c, out float h, out float s, out float v);
    float vNew   = v > 0.5f ? v - 0.22f : v + 0.22f;
    var   result = Color.HSVToRGB(h, s, Mathf.Clamp01(vNew));
    result.a = c.a;
    return result;
}
```

> Работа в пространстве HSV позволяет изменять только яркость (`V`), не трогая оттенок и насыщенность. Логика `v > 0.5f ? v - 0.22f : v + 0.22f` делает тёмные кнопки светлее при наведении, а светлые — темнее. Это решает проблему «невидимого ховера» для близких к белому кнопок, где простое увеличение яркости ничего не меняло бы. `result.a = c.a` — возврат оригинальной прозрачности, которую `HSVToRGB` не сохраняет.

##### SetVisible — охрана SetActive

```csharp
public void SetVisible(bool visible)
{
    if (gameObject.activeSelf != visible)
        gameObject.SetActive(visible);
}
```

> Проверка `activeSelf != visible` перед `SetActive` — не косметика. `SetActive` запускает `OnEnable`/`OnDisable` даже при отсутствии реального изменения состояния. `EnsureSetup` в `OnEnable` безвреден повторно, но вызов `SetActive(true)` на уже активном объекте всё равно спровоцировал бы ненужный цикл событий Unity.

### К защите

##### Вероятные вопросы

> [!question]
> **В:** Почему кнопка не хранит ссылку на панель напрямую?
> **О:** Связь держится на строковом `_moduleId`. Это позволяет кнопкам и панелям существовать в разных областях жизни (корневой и сценовой), не зная друг о друге напрямую. Маршрутизатор как посредник держит оба реестра по одному ключу.
>
> **В:** Почему настройка цветов не в `Construct`, а в `EnsureSetup`?
> **О:** В момент инъекции объект может быть неактивен, и Unity не выполняет `Awake` для его дочерних компонентов. Ссылка `_button` назначается Unity-сериализацией в `Awake` (через инспектор) — она может быть `null` во время `Construct`. `EnsureSetup` вызывается в `Awake` (когда `_button` уже назначен) и защищён от повторного выполнения флагом.
>
> **В:** Зачем нужен флаг `_listenerAttached`?
> **О:** `OnEnable` вызывается при каждой активации объекта. `AddListener` не проверяет дубликаты — одна и та же функция может быть добавлена несколько раз. Без флага каждая активация добавляла бы ещё один `OnClick`, и одно нажатие вызывало бы `Toggle` столько раз, сколько раз объект был активирован.
>
> **В:** Почему `ColorBlock` — struct — важна для понимания кода `EnsureSetup`?
> **О:** `var block = _button.colors` создаёт копию структуры, не ссылку. `_inactiveColors = block` и `_activeColors = block` — тоже копии. Поля `_inactiveColors` и `_activeColors` независимы. Изменение одного не влияет на другой и не влияет на кнопку.

### Связи

[[PanelRegionRouter]] · [[NavBarConfig]] · [[RegionMember]] · [[UserPanel]] · [[ModeChangedEvent]] · [[RegionChangedEvent]] · [[Регионная модель UI]] · [[Внедрение зависимостей (VContainer)]]
