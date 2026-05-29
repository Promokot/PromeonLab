# SpatialUi Region Model — ручная настройка префабов и конфига (RU)

> **Зачем этот файл.** После переноса region-модели на **app-lifetime** (router живёт в Root, а не в
> scene-scope) здесь описано, что должно быть истинно, чтобы кнопка нав-панели открывала/закрывала
> свою панель, **куда прописать `NavBarConfig` и параметры модулей**, и как продиагностировать
> неработающие кнопки. Делается вручную в Инспекторе (или по YAML префабов).
>
> **История.** Раньше `PanelRegionRouter` + `NavBarConfig` регистрировались в `VrEditingSceneScope`/
> `SandboxSceneScope`. Это был баг: `UserPanel` и нав-кнопки **персистентны** (живут на XR-риге,
> переживают смену сцен), а сервис, который ими управлял, был scene-scoped → в MainMenu кнопки мертвы,
> а после VrEditing→MainMenu панель управлялась router'ом выгруженной сцены (нет изоляции). Теперь
> router — **Singleton в `RootLifetimeScope`**, под жизненный цикл персистентной панели.

---

## 0. Как это работает (модель за 30 секунд)

Открытием панелей и **видимостью самих кнопок** управляет один app-lifetime `PanelRegionRouter`:

```
[кнопка] RegionNavButton.OnClick
        → router.Toggle(moduleId)
        → router.Open/Close(moduleId)
        → находит RegionMember по moduleId в реестре
        → region берётся из NavBarConfig (ExclusiveGroup)
        → прячет того, кто открыт в этом же регионе, и Show() нужного

[смена режима] ModeChangedEvent → router.ApplyMode(mode)
        → для каждой кнопки: SetVisible(видима ли в режиме) + SetActiveHighlight(открыта ли)
        → закрывает панели, невидимые в новом режиме
```

Три участника + конфиг:

| Что | Где живёт | За что отвечает |
|---|---|---|
| `PanelRegionRouter` | **Root-scope (Singleton, C#, не на сцене)** | реестр `moduleId → поверхность` **и** `moduleId → кнопка`; «в одном регионе открыт максимум один»; единственный подписчик `ModeChangedEvent`; гонит видимость+подсветку кнопок |
| `RegionMember` | на каждой открываемой панели/оверлее | несёт `_moduleId`; по умолчанию Show/Hide = `SetActive`; делегирует соседнему `IRegionSurface`, если он есть |
| `RegionNavButton` | на каждой кнопке | **тонкая**: по клику зовёт `router.Toggle(_moduleId)`. Видимость и подсветку ей задаёт router через `SetVisible`/`SetActiveHighlight` — сама на события **не подписывается** |
| `NavBarConfig` (`.asset`) | ScriptableObject, ссылка в **`RootLifetimeScope`** | для каждого `moduleId`: регион (`ExclusiveGroup`), видимость по режимам (`VisibleModes`), дефолт региона (`IsRegionDefault`) |

**Ключевая связка по строкам:** `RegionNavButton._moduleId` == `RegionMember._moduleId` == `NavBarConfig.Entry.Id`.
Это обычные строки, регистр важен. Если хоть одна не совпала — кнопка молча ничего не делает.

**Разделение по lifetime:**
- *Навигационная оболочка* (router, нав-кнопки, `RegionMember` для show/hide) — **Root**, регистрируется один раз при старте приложения.
- *Контент модулей* (`OutlinerPanel` нужен `SceneGraph`, `AssetBrowserPanel` нужен `AssetImporter` и т.д.) — остаётся **scene-scoped**, инжектится в `VrEditingSceneScope`/`SandboxSceneScope`.
- *Scene-bound поверхности* (file browser зависит от scene-scoped `AssetBrowserPanel`) — регистрируются в root-router'е из своего scene-scope.

---

## 1. Что должно быть истинно, чтобы кнопка работала (чек-лист причинной цепочки)

Кнопка «живая», только если выполнены ВСЕ пункты. Это же — порядок диагностики «ничего не делает»:

1. **`RootLifetimeScope` в `Bootstrap.unity`, и в нём заполнен `_navBarConfig`.** Поле `Nav Bar Config` на компоненте `RootLifetimeScope` (объект `[RootScope]`) → `DefaultNavBarConfig.asset`. Если пусто — в Console при старте `RootLifetimeScope: _navBarConfig not assigned — nav buttons will be inert!`, router/config не регистрируются, и все нав-кнопки мертвы во всех сценах.
2. **`RegionNavButton` проинъектирован.** Root build-callback делает `c.Inject(nav)` по всем `RegionNavButton` (включая неактивные) и `router.RegisterButton(nav)`. После этого `_router` не null. Если инъекции не было — `OnClick` зовёт `_router?.Toggle(...)` = пусто.
3. **`_button` заполнен.** В `EnsureSetup()` (вызывается из `Awake`/`OnEnable`) строка `if (_button == null) return` — если `_button` пуст, `onClick.AddListener(OnClick)` не выполнится, клик ни к чему не приведёт. `_button` должен ссылаться на компонент `Button` на **том же** GameObject.
4. **GameObject кнопки может быть активирован.** Кнопка авторится **active** в префабе. Router сам спрячет её по режиму (`SetActive(false)`) уже после регистрации. `EnsureSetup` идемпотентен и срабатывает на `OnEnable`, поэтому листенер навешивается, как только панель открывается и кнопка становится видимой. Мёртвая `RiggingBtn` остаётся inactive.
5. **`RegionMember` нужного модуля зарегистрирован.** Персистентные модули регистрируются root-callback'ом; scene-bound (file browser) — scene-scope'ом. `Toggle → Open` ищет модуль в реестре; если его нет (нет `RegionMember` или другой `_moduleId`) — `return`, ничего не происходит.
6. **`moduleId` совпадает по всем трём местам** (кнопка / member / конфиг), регистр в т.ч.
7. **Модуль видим в текущем режиме.** `NavBarConfig.IsVisibleInMode(moduleId, mode)`: если текущего режима нет в `VisibleModes`, router прячет кнопку и закрывает модуль. См. §3 — там сейчас **ошибка в значениях режимов** (отдельный хвост, не трогаем здесь).

---

## 2. ScriptableObject `DefaultNavBarConfig` — структура полей

Файл: `Assets/_App/Content/ScriptableObjects/DefaultNavBarConfig.asset`
Класс: `NavBarConfig` (`Assets/_App/Scripts/SpatialUi/NavBarConfig.cs`).

В Инспекторе это массив **`_entries`** (`Entries`). Каждый элемент:

| Поле | Тип | Что значит |
|---|---|---|
| `Id` | string | идентификатор модуля; ровно равен `RegionNavButton._moduleId` и `RegionMember._moduleId` |
| `VisibleModes` | `AppMode[]` | в каких режимах модуль/кнопка показываются. Пусто = не показывается нигде |
| `ExclusiveGroup` | string | **регион**. В одном регионе открыт максимум один модуль. Пусто = модуль вне взаимного исключения (просто Show/Hide) |
| `IsRegionDefault` | bool | этот модуль авто-восстанавливается, когда его регион опустел (закрыли активный модуль). Ровно один `true` на регион |

`AppMode` (важно для `VisibleModes`): `MainMenu=0, VrEditing=1, Sandbox=2, Debug=3`. **Других значений нет.**

### Регионы (значения `ExclusiveGroup`)

- `center`, `left`, `right` — **независимы**. Одновременно могут быть открыты один `center` + `left` + `right`. Внутри `center` — только один из {settings, assets, gizmo, animator}.
- `overlays` — держит {`userPanelDefault`, `keyboard`}; `userPanelDefault` — дефолт (восстанавливается при закрытии клавиатуры).
- `dialog` — только файл-браузер.

---

## 3. Целевые значения `_entries` (ОШИБКА данных — отдельный хвост)

> ⚠️ Этот раздел — **отдельная задача по данным** (правка в инспекторе ассета), не часть lifetime-фикса.
> Решено разбирать отдельно. Здесь оставлено как справка.

Ниже — что сейчас в `.asset` (расшифровано) и что должно быть. **`4` — несуществующий режим**, его быть не должно.

| `Id` | `ExclusiveGroup` | `IsRegionDefault` | `VisibleModes` сейчас | Должно быть |
|---|---|---|---|---|
| `settings` | `center` | — | MainMenu, VrEditing, **4**, Sandbox, Debug | MainMenu, VrEditing, Sandbox, Debug |
| `assets` | `center` | — | MainMenu, VrEditing, **4** | MainMenu, VrEditing, Sandbox |
| `outliner` | `left` | — | VrEditing, **4** | VrEditing, Sandbox |
| `inspector` | `right` | — | VrEditing, **4** | VrEditing, Sandbox |
| `rigging` | (пусто) | — | (пусто) | (пусто) — мёртвая запись, оставить |
| `gizmo` | `center` | — | VrEditing, **4** | VrEditing, Sandbox |
| `animator` | `center` | — | VrEditing, Sandbox | VrEditing, Sandbox (уже верно) |
| `keyboard` | `overlays` | false | VrEditing, **4** | VrEditing, Sandbox |
| `userPanelDefault` | `overlays` | **true** | VrEditing, **4** | VrEditing, Sandbox |
| `fileBrowser` | `dialog` | — | VrEditing, **4** | VrEditing, Sandbox |

> **Что сделать в Инспекторе:** открыть `DefaultNavBarConfig.asset`, в каждой записи заменить элемент
> `VisibleModes` со значением `4` (в дропдауне он пустой/`(не задано)`) на **`Sandbox`**. Критично для
> Sandbox: сейчас там скрыт даже `userPanelDefault`. В MainMenu/VrEditing уже видно.

---

## 4. Префабы — где какие компоненты и `moduleId`

### 4.1 `UserPanel.prefab` (`Content/Prefabs/UI/Panels/UserPanel/`)

**Корневой компонент `UserPanel`** — только: `_mainMenuButton`, `_exitButton`, `_lockButton`, `_lockButtonImage`, поля smart-follow. Полей `_bindings`/`_navBarConfig`/яркостей быть не должно. **Нигде в иерархии не должно быть «Missing Script»**.

**`RegionMember` на модулях** (`UserPanel/ModulesSlot/…`) — `_moduleId` + стартовое состояние:

| GameObject | `_moduleId` | Active в префабе |
|---|---|---|
| `SettingsModule` | `settings` | inactive |
| `AssetBrowserModule` | `assets` | inactive |
| `SceneOutlinerModule` | `outliner` | inactive |
| `SceneInspectorModule` | `inspector` | inactive |
| `GizmoToolsModule` | `gizmo` | inactive |
| `AnimatorPanelModule` | `animator` | inactive |

**`RegionMember` на оверлеях** (`UserPanel/OverlaysSlot/…`):

| GameObject | `_moduleId` | Active в префабе |
|---|---|---|
| `Default` | `userPanelDefault` | **active** (дефолт региона `overlays`) |
| `Keyboard` | `keyboard` | inactive |

**`RegionNavButton` на кнопках** — `_moduleId` + `_button` (= `Button` на этом же GO) + **Active**:

| GameObject (путь) | `_moduleId` | Active |
|---|---|---|
| `OverlaysSlot/Default/.../SettingsButton` | `settings` | active |
| `OverlaysSlot/Default/.../AssetsBtn` | `assets` | active |
| `OverlaysSlot/Default/.../OutlinerBtn` | `outliner` | active |
| `OverlaysSlot/Default/.../InspectorBtn` | `inspector` | active |
| `OverlaysSlot/Default/.../AnimatorBtn` | `animator` | active |
| `OverlaysSlot/Default/.../GizmoBtn` | `gizmo` | active |
| `OverlaysSlot/Default/ButtonsBar_2/RiggingBtn` | `rigging` | inactive (мёртвая) |
| `FuncButtons/RightPart/KeyboardButton` | `keyboard` | active |

> `_button` у каждой кнопки **обязателен и должен указывать на `Button` своего же GameObject**. Поле инъекции у кнопки теперь одно — `PanelRegionRouter` (через `[Inject] Construct`), сериализованного конфига на кнопке нет. Яркости можно оставить по умолчанию (1.2 / 0.6 / 0.8).

### 4.2 `SimpleFileBrowserCanvas.prefab` (там же)

| Компонент на корне канваса | Ожидается |
|---|---|
| `RegionMember` | `_moduleId = fileBrowser` |
| `FileBrowserSurface` | присутствует (`EventBus`+`PanelRegionRouter` приходят через `[Inject]`) |
| `FileBrowserVrAnchor` | присутствует; инъектит scene-scoped `AssetBrowserPanel` |

### 4.3 Сцены и `RootLifetimeScope`

- **`Bootstrap.unity` → `[RootScope]` (`RootLifetimeScope`):** поле `Nav Bar Config` → `DefaultNavBarConfig.asset`. **Это новый обязательный слот** — здесь теперь живёт region-модель.
- **`VrEditing.unity` / `Sandbox.unity` (scope-объекты):** поля `_navBarConfig` больше **нет** (удалено из `VrEditingSceneScope`/`SandboxSceneScope`). Старая ссылка осиротеет — Unity отбросит override, это нормально. Сами scope'ы инжектят только scene-bound контент (Outliner/Inspector/AssetBrowser/Animator/Gizmo) и привязывают file browser к root-router'у.
- Инстанс `UserPanel` на сцене (точнее — на персистентном XR-риге) должен нести все компоненты из 4.1.
- Файл-браузер берётся из **сценового** `SimpleFileBrowserCanvas`, а не из Resources-копии `_legacy`.

---

## 5. Почему кнопки ничего не делают — диагностика (по убыванию вероятности)

Проверять в Play, глядя в Console (логи `[RegionDBG]`). Поскольку клик «молчит», виноват один из разрывов цепочки §1:

**A. `_navBarConfig` не назначен на `RootLifetimeScope`.** Признак: при старте `RootLifetimeScope: _navBarConfig not assigned — nav buttons will be inert!`. Router/config не зарегистрированы → у всех кнопок `_router == null`. → Назначь `DefaultNavBarConfig.asset` в слот `Nav Bar Config` на `[RootScope]`. *(Самая частая причина после рефактора — слот новый.)*

**B. `_button` не назначен на `RegionNavButton`.** Тогда `EnsureSetup` не навешивает `onClick` → клик пуст. → Перетащи `Button` кнопки в её поле `_button`.

**C. `RegionNavButton` не проинъектирован** (`_router` null при заполненном конфиге). Признак: в логе `OnClick id=… routerNull=True`. → Убедись, что `RootLifetimeScope` отрабатывает свой build-callback (есть логи `[RegionDBG] RegisterButton id=…` при старте) и что нав-кнопки физически в иерархии на момент старта приложения (персистентный XR-риг в `Bootstrap.unity`).

**D. `RegionMember` модуля не зарегистрирован / `_moduleId` не совпал.** Тогда `Open` не находит поверхность и тихо выходит (`router.Toggle` → `Open` → `return`). → Сверь три строки (кнопка/member/конфиг) посимвольно; проверь логи `[RegionDBG] RegisterModule id=…`.

**E. Кнопка спряталась по режиму.** Если в `VisibleModes` модуля нет текущего режима — `ApplyMode` делает `SetActive(false)`, кнопки не видно. → Объясняет пропажу кнопок в **Sandbox** (см. §3, баг с `4`), но не «видна, но мёртва».

> Быстрый способ локализовать: логи `[RegionDBG]` уже стоят в `RegisterButton`/`RegisterModule`/`OnClick`. По тому, какой лог не появился (или `routerNull=True`), причина определяется однозначно.

---

## 6. Проверка в Play (VR, человек)

После §4.3 (+ опц. §3 для Sandbox) проверить:
1. **MainMenu сразу после старта (без захода в VrEditing):** нав-кнопки, видимые в MainMenu (`settings`/`assets` по §3), реагируют на клик. Это и есть проверка, что router app-lifetime.
2. **Загрузка VrEditing:** нет исключений; `UserPanel` появляется; нав-оверлей `Default` виден.
3. **Навигация:** каждая кнопка открывает/закрывает свой модуль; второй `center` закрывает первый; `outliner`(left)/`inspector`(right) — независимо; яркость активной кнопки переключается (её гонит router).
4. **Клавиатура:** показывает её и прячет нав-оверлей; при закрытии нав-оверлей восстанавливается (дефолт региона `overlays`).
5. **Файл-браузер:** «+» в asset browser открывает диалог; выбор импортирует; отмена закрывает.
6. **Изоляция при смене режима** `VrEditing`→`MainMenu`: модули, невидимые в MainMenu, закрываются, их кнопки прячутся, и в MainMenu панелью управляет тот же (живой) root-router — никаких «мёртвых» сценовых router'ов.

---

## 7. Подводные камни

- **`RegionNavButton` теперь тонкая.** Она не подписывается на `ModeChangedEvent`/`RegionChangedEvent` — видимость и подсветку задаёт `PanelRegionRouter`. Не возвращай в неё `IRegionConfig`/`ModeOrchestrator`/`EventBus` — единственная инъекция `PanelRegionRouter`.
- **Setup кнопки идемпотентен и lifecycle-safe.** `UserPanel` стартует неактивной → `Awake`/`OnEnable` кнопок не вызываются до первого открытия панели. `Construct` (инъекция) проходит и на неактивном GO; `EnsureSetup` навешивает листенер/цвета при первом `OnEnable`. Кнопка авторится **active**; router спрячет её по режиму.
- **Начальный active-стейт модуля = стартовый открытый в регионе.** Root-callback регистрирует активные `RegionMember` как открытые. Поэтому `Default` — ON (регион `overlays`), все `ModulesSlot`-модули — OFF, `Keyboard` — OFF.
- **Scene-bound member умирает со сценой.** `fileBrowser` регистрируется из scene-scope; после выгрузки сцены его объект уничтожается. Root-router держит `Alive()`-guard и сам выкидывает мёртвые ссылки — звать `Show/Hide` на уничтоженном объекте он не будет.
- **Вложенные префабы.** Модули в `ModulesSlot` — вложенные префаб-инстансы. Их `RegionMember` мог быть добавлен как override; при пересборке `AnimatorPanelModuleBuilder`-ом override может потеряться. → По возможности добавлять `RegionMember` в собственный префаб модуля.
- **Источник файл-браузера** — **сценовый** `SimpleFileBrowserCanvas`, не Resources-копия `..._legacy.prefab`.
- **`rigging` — мёртвая запись** (null-панель, пустые регион/режимы). Оставить как есть либо удалить `RiggingBtn` + запись.
