---
note_type: script
subsystem: SpatialUi / AssetBrowser
listing: "Б.17"
---

> [!info] Назначение
> `ImportWizardPanel` — VR-мастер импорта ассета. Получает `ImportRequestedEvent`, отображает имя файла, поля имени и типа, переключатели оси концевых костей; при подтверждении публикует `ImportConfirmedEvent`. Листинг Б.17.

### Обзор

##### Роль и место

`MonoBehaviour`, реализует `IRegionSurface` (регионная модель `PanelRegionRouter`). Самостоятельно управляет `gameObject.SetActive` через `Show`/`Hide` — `RegionMember` не трогает `SetActive` для пользовательских `IRegionSurface`. Подписка на `ImportRequestedEvent` выполняется в `[Inject]`-методе, не в `OnEnable`.

##### Ключевые методы

| Метод | Назначение |
|---|---|
| `OnImportRequested` | заполняет UI, открывает панель через `_router.Open("importWizard")` |
| `Show` / `Hide` | управляет `gameObject.SetActive`, устанавливает `_open` |
| `OnImport` | собирает `ImportConfirmedEvent{Confirmed=true}`, публикует, закрывает регион |
| `OnCancel` | публикует `{Confirmed=false}`, закрывает регион |
| `ClearAxisSelection` | сбрасывает все axis-тоглы через `SetIsOnWithoutNotify` |
| `SelectedTerminalBonesAxis` | читает состояние X/Y/Z тоглов → `TerminalBoneAxis` enum |

---

### Разбор кода

##### Подписка в Construct, а не в OnEnable

```csharp
[Inject]
public void Construct(EventBus bus, PanelRegionRouter router)
{
    _bus    = bus;
    _router = router;
    // Subscribe at DI time, NOT in OnEnable: this panel stays hidden (GameObject
    // inactive) until an import is requested, so OnEnable never runs to wire the
    // subscription. EventBus invokes the delegate regardless of active state, so the
    // request still reaches us and we can self-activate via Open → Show.
    _bus?.Subscribe<ImportRequestedEvent>(OnImportRequested);
}
```

> Панель **неактивна** (`SetActive(false)`) до первого запроса импорта. Unity не вызывает `OnEnable` у неактивных `MonoBehaviour`. Если бы подписка была в `OnEnable`, первый `ImportRequestedEvent` пришёл бы в никуда — панель никогда бы не открылась. `EventBus.Subscribe` не проверяет активность объекта: делегат хранится в `List<object>` и вызывается независимо. Отписка — только `OnDestroy`, что правильно: панель живёт всё время работы сцены.

##### ClearAxisSelection — SetIsOnWithoutNotify

```csharp
private void ClearAxisSelection()
{
    _axisXToggle?.SetIsOnWithoutNotify(false);
    _axisYToggle?.SetIsOnWithoutNotify(false);
    _axisZToggle?.SetIsOnWithoutNotify(false);
    _axisInvertToggle?.SetIsOnWithoutNotify(false);
}
```

> `SetIsOnWithoutNotify` меняет состояние тогла без вызова `onValueChanged` — иначе при сбросе сработал бы обработчик, который мог бы ошибочно опубликовать событие или переключить другие тоглы группы. Unity `Toggle Group` с `AllowSwitchOff = true` обязателен, чтобы Auto-состояние (ни один тогл не активен) было возможным по умолчанию.

##### OnImport — сборка ImportConfirmedEvent

```csharp
private void OnImport()
{
    _bus?.Publish(new ImportConfirmedEvent
    {
        Confirmed   = true,
        FilePath    = _filePath,
        DisplayName = string.IsNullOrWhiteSpace(_nameInput?.text)
            ? System.IO.Path.GetFileNameWithoutExtension(_filePath)
            : _nameInput.text,
        ChosenType  = SelectedType(),
        TerminalBonesAxis       = SelectedTerminalBonesAxis(),
        InvertTerminalBonesAxis = _axisInvertToggle != null && _axisInvertToggle.isOn,
    });
    _router?.Close("importWizard");
}
```

> `string.IsNullOrWhiteSpace` — защита от пустого поля ввода: пользователь мог стереть имя. Fallback — имя файла без расширения, то же, что предложил `ImportPipeline` изначально. `SelectedTerminalBonesAxis()` возвращает `TerminalBoneAxis.Auto`, если ни один axis-тогл не выбран — это состояние по умолчанию после `ClearAxisSelection`.

##### SelectedType — приоритет Object по умолчанию

```csharp
private AssetType SelectedType()
{
    if (_rigToggle       != null && _rigToggle.isOn)       return AssetType.Rig;
    if (_referenceToggle != null && _referenceToggle.isOn) return AssetType.Reference;
    return AssetType.Object;
}
```

> `Object` — последний вариант без явной проверки тогла. Если тогл `_objectToggle` окажется `null` или сломан, метод всё равно вернёт `Object`. `Rig` и `Reference` проверяются первыми, так как они специализированные — пользователь явно выбирает их.

---

### К защите

##### Вероятные вопросы

> [!question]
> **В:** Почему подписка на `ImportRequestedEvent` в `Construct`, а не в `Start` или `OnEnable`?
> **О:** Панель стартует неактивной — `OnEnable` и `Start` не вызываются. `Construct` вызывается VContainer при внедрении зависимостей независимо от `activeSelf`. Это единственный момент, когда можно безопасно подписаться до первого показа панели.

> [!question]
> **В:** Зачем нужен `AllowSwitchOff` на группе axis-тоглов?
> **О:** По умолчанию `Toggle Group` принудительно выбирает первый тогл при инициализации (`EnsureValidState`). Если `AllowSwitchOff = false`, Auto-состояние (ни один тогл не выбран) невозможно — первый тогл всегда будет включён. Пользователь не смог бы оставить автоматическое определение оси.

> [!question]
> **В:** Почему ось концевых костей задаётся в мастере вручную, а не определяется автоматически?
> **О:** Автоматическое определение требовало бы полной загрузки модели ещё до подтверждения импорта — дорогая операция, которую пользователь может и не захотеть завершать. Мастер показывает предложенный тип за миллисекунды; загрузка glTF начинается только после `Confirmed = true`.

---

### Связи

[[ImportPipeline]] · [[AssetBrowserPanel]] · [[Структуры данных ассета]] · [[Паттерн Publish-Subscribe]] · [[ImportRequestedEvent]] · [[ImportConfirmedEvent]]
