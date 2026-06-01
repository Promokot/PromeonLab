# UserPanel UI Conventions

> ⚠️ **Partially stale (2026-06-01 audit).** The general rules below (§1 Z=0, §2 World-Space Canvas,
> §5 module/DI pattern, §7 forbidden list) are still valid. But the **NavBar wiring** parts are
> obsolete: panels are now discovery-registered into the **root-lifetime region model**
> (`PanelRegionRouter` + `NavBarConfig` + `RegionMember`), not via `NavBarBinding[]` on `UserPanel`;
> and `DetachablePanel` is now `SpatialPanelDetachable`. See `CLAUDE.md` (SpatialUi row) and
> `docs/superpowers/specs/2026-05-29-spatialui-region-model-design.md` for the current model.
> `AssetBrowserModule` (the "canonical example") is now `AssetBrowserPanel`.

Правила построения панелей и модулей внутри UserPanel.

---

## 1. Плоские UI-элементы: Z всегда 0

Любой `RectTransform` внутри Canvas **обязан** иметь `localPosition.z = 0`.

Ненулевой Z ломает: сортировку слоёв, маски (`Mask` / `RectMask2D`), raycast через `GraphicRaycaster`. Unity не предупреждает об этом — ошибка проявляется только визуально или в рантайме (клики не проходят, элементы пропадают под маской).

**Проверка:** при создании любого UI-GameObject через меню или дублировании — убедиться что Z = 0 в Inspector до коммита.

---

## 2. Canvas

UserPanel и все дочерние DetachablePanel используют **World Space Canvas**:

| Поле | Значение |
|---|---|
| Render Mode | World Space |
| Pixel Perfect | off |
| Event Camera | не назначать (XR UI Input Module находит камеру сам) |
| Layer | `UI` |

Размер Canvas задаётся через `RectTransform.sizeDelta`. Физический масштаб — через `transform.localScale` на корне Canvas-объекта (не через sizeDelta).

---

## 3. Размер и позиция новых панелей

**Базовый референс — AssetBrowserModule panel prefab.** Все новые DetachablePanel-модули копируют его `RectTransform.sizeDelta` и `localPosition` относительно UserPanel root.

Отступ от UserPanel root: смотреть на AssetBrowserModule в prefab — не задавать произвольно.

---

## 4. Иерархия объектов

```
UserPanel (SpatialPanel + UserPanel)
└── [Canvas]
    ├── NavBar            ← кнопки навигации
    ├── TransportBar      ← Main Menu / Exit / Lock
    └── Panels/
        ├── AssetsPanel   (DetachablePanel + AssetBrowserModule)
        ├── OutlinerPanel (DetachablePanel + SceneOutlinerView)
        └── ...           (каждая новая панель — тот же паттерн)
```

Каждая панель: один `GameObject` с `DetachablePanel` на корне + один MonoBehaviour-модуль (`*Module` или `*View`) на том же или дочернем объекте.

---

## 5. Паттерн модуля

```csharp
public class FooModule : MonoBehaviour
{
    [SerializeField] private Button  _someButton;   // [SerializeField] private — обязательно
    [SerializeField] private TMP_Text _someLabel;

    private EventBus          _bus;
    private ISelectionManager _selection;

    [Inject]
    public void Construct(EventBus bus, ISelectionManager selection)
    {
        _bus       = bus;
        _selection = selection;
    }

    private void Awake()
    {
        _someButton?.onClick.AddListener(OnSomeButton);   // кнопки — в Awake
    }

    private void OnEnable()
    {
        _bus?.Subscribe<SelectionChangedEvent>(OnSelectionChanged);  // события — в OnEnable
    }

    private void OnDisable()
    {
        _bus?.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);
    }
}
```

**Правила:**
- Все инспектор-поля — `[SerializeField] private`, никогда `public`
- Кнопки (`onClick.AddListener`) подписываются в `Awake`, не в `Start`
- Event bus — в `OnEnable` / `OnDisable` (не `Start` / `OnDestroy`), чтобы корректно работать при повторном show/hide панели
- `[Inject] Construct(...)` — единственная точка DI; не использовать `FindObjectOfType`

---

## 6. Подключение к NavBar

Каждый новый модуль требует двух шагов:

**NavBarConfig SO** (`Assets/_App/Subsystems/SpatialUi/Data/`) — добавить `Entry`:
```
Id:              "animation"
VisibleModes:    [VrEditing]
ExclusiveGroup:  "tools"      // или пусто, если нет взаимоисключения
```

**UserPanel prefab** — добавить `NavBarBinding` в массив `_bindings`:
```
EntryId:    "animation"
NavButton:  [ссылка на кнопку в NavBar]
Panel:      [ссылка на корневой GameObject панели]
```

Видимость кнопки по режимам управляется только через `NavBarConfig` — не через ручной `SetActive`.

---

## 7. Запрещено

- `localPosition.z != 0` на RectTransform
- `public` поля вместо `[SerializeField] private`
- `FindObjectOfType` / `GameObject.Find` — только VContainer DI
- Подписка на EventBus в `Start` без отписки в `OnDisable`
- Прямой `SetActive` на панелях в обход NavBar-механизма UserPanel
