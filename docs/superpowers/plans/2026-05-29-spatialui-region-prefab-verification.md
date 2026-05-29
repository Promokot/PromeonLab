# SpatialUi Region Model — ручная настройка префабов и конфига (RU)

> **Зачем этот файл.** После рефактора region-модели (`PanelRegionRouter` + `RegionMember` +
> `RegionNavButton` + `NavBarConfig`) кнопки нав-панели сейчас **ничего не делают**. Здесь описано, что
> должно быть истинно, чтобы кнопка открывала/закрывала свою панель, **куда и какие параметры
> прописать в ScriptableObject `DefaultNavBarConfig`**, и как продиагностировать неработающие кнопки.
> Делается вручную в Инспекторе (или по YAML префабов).

---

## 0. Как это работает (модель за 30 секунд)

Открытием панелей больше не управляет сам `UserPanel`. Цепочка такая:

```
[кнопка] RegionNavButton.OnClick
        → router.Toggle(moduleId)
        → router.Open/Close(moduleId)
        → находит RegionMember по moduleId в реестре
        → region берётся из NavBarConfig (ExclusiveGroup)
        → прячет того, кто открыт в этом же регионе, и Show() нужного
```

Три участника + конфиг:

| Что | Где живёт | За что отвечает |
|---|---|---|
| `PanelRegionRouter` | scene-scope (C#, не на сцене) | реестр `moduleId → поверхность`; «в одном регионе открыт максимум один» |
| `RegionMember` | на каждой открываемой панели/оверлее | несёт `_moduleId`; по умолчанию Show/Hide = `SetActive`; делегирует соседнему `IRegionSurface`, если он есть |
| `RegionNavButton` | на каждой кнопке | по клику зовёт `router.Toggle(_moduleId)`; видимость кнопки по режиму; яркость активной кнопки |
| `NavBarConfig` (`.asset`) | ScriptableObject, ссылка в scene-scope | для каждого `moduleId`: регион (`ExclusiveGroup`), видимость по режимам (`VisibleModes`), дефолт региона (`IsRegionDefault`) |

**Ключевая связка по строкам:** `RegionNavButton._moduleId` == `RegionMember._moduleId` == `NavBarConfig.Entry.Id`.
Это обычные строки, регистр важен. Если хоть одна не совпала — кнопка молча ничего не делает.

---

## 1. Что должно быть истинно, чтобы кнопка работала (чек-лист причинной цепочки)

Кнопка «живая», только если выполнены ВСЕ пункты. Это же — порядок диагностики «ничего не делает»:

1. **Scene-scope в сцене, и в нём заполнен `_navBarConfig`.** На `VrEditingSceneScope` (сцена `VrEditing`) и `SandBox_VrEditingSceneScope` (сцена `Sandbox`) поле `_navBarConfig` → `DefaultNavBarConfig.asset`. Если пусто — `IRegionConfig` не зарегистрируется, `PanelRegionRouter` не построится, и весь region-блок упадёт с исключением (см. §5, причина A).
2. **`RegionNavButton` проинъектирован.** Scene-scope в build-callback делает `c.Inject(nav)` по всем `RegionNavButton` (включая неактивные). После этого `_router` не null. Если инъекции не было — `OnClick` вызывает `_router?.Toggle(...)` = пусто.
3. **GameObject кнопки активен, чтобы выполнился `Start`.** `RegionNavButton.Start` навешивает `onClick`, подписывается на события и применяет режим. Unity **не вызывает `Start` на неактивном объекте**. Старая централизованная логика активировала кнопки снаружи — новая так не умеет. Значит кнопки (кроме мёртвой `RiggingBtn`) должны быть **active** в префабе.
4. **`_button` заполнен.** В `Start` строка `if (_button != null)` — если `_button` пуст, `onClick.AddListener(OnClick)` **не выполнится**, и клик ни к чему не приведёт. `_button` должен ссылаться на компонент `Button` на **том же** GameObject.
5. **`RegionMember` нужного модуля зарегистрирован.** Build-callback находит все `RegionMember` (включая неактивные) и делает `router.Register(moduleId, rm)`. `Toggle → Open` ищет `_modules[moduleId]`; если такого нет (модуль без `RegionMember` или другой `_moduleId`) — `return`, ничего не происходит.
6. **`moduleId` совпадает по всем трём местам** (кнопка / member / конфиг), регистр в т.ч.
7. **Модуль видим в текущем режиме.** `NavBarConfig.IsVisibleInMode(moduleId, mode)`: если текущего режима нет в `VisibleModes`, кнопка прячется (`SetActive(false)`), а открытый модуль закрывается. См. §3 — там сейчас **ошибка в значениях режимов**.

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

## 3. Целевые значения `_entries` (и ОШИБКА, которую надо поправить)

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

> **Что сделать в Инспекторе:** открыть `DefaultNavBarConfig.asset`, в каждой записи в списке `VisibleModes`
> заменить элемент со значением `4` (в дропдауне он отрисуется пустым/`(не задано)`) на **`Sandbox`**.
> Это чисто косметически для `VrEditing` (там и так видно), но **критично для `Sandbox`**: сейчас в Sandbox
> скрыт даже `userPanelDefault` — то есть вся нав-панель не показывается. `animator` уже корректен (VrEditing+Sandbox) — его можно взять за образец.
>
> ⚠️ Это **не** причина «ничего не делает в VrEditing» (там mode=1 присутствует во всех нужных записях).
> Причину неработающих кнопок в VrEditing ищи в §5.

---

## 4. Префабы — где какие компоненты и `moduleId`

### 4.1 `UserPanel.prefab` (`Content/Prefabs/UI/Panels/UserPanel/`)

**Корневой компонент `UserPanel`** — только: `_mainMenuButton`, `_exitButton`, `_lockButton`, `_lockButtonImage`, поля smart-follow. Полей `_bindings`/`_navBarConfig`/яркостей быть не должно. **Нигде в иерархии не должно быть «Missing Script»** (старый `UserPanelKeyboardToggle` удалён).

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
| `OverlaysSlot/Default/ButtonsBar_2/RiggingBtn` | `rigging` | inactive (мёртвая — пусть остаётся скрытой) |
| `FuncButtons/RightPart/KeyboardButton` | `keyboard` | active |

> `_button` у каждой кнопки **обязателен и должен указывать на `Button` своего же GameObject**. Яркости можно оставить по умолчанию (1.2 / 0.6 / 0.8).

### 4.2 `SimpleFileBrowserCanvas.prefab` (там же)

| Компонент на корне канваса | Ожидается |
|---|---|
| `RegionMember` | `_moduleId = fileBrowser` |
| `FileBrowserSurface` | присутствует (сериализованных полей нет; `EventBus`+router приходят через `[Inject]`) |
| `FileBrowserVrAnchor` | присутствует; **без сериализованной цели** (инъектит `AssetBrowserPanel`) |

### 4.3 Сцены `VrEditing.unity` и `Sandbox.unity`

- На scope-объекте поле `_navBarConfig` → `DefaultNavBarConfig.asset` (на `VrEditingSceneScope` в VrEditing; на `SandBox_VrEditingSceneScope` в Sandbox).
- Инстанс `UserPanel` на сцене должен нести все компоненты из 4.1; в Overrides не должно быть «осиротевших» правок на удалённые поля (`_bindings`/`_navBarConfig`).
- Файл-браузер берётся из **сценового** `SimpleFileBrowserCanvas`, а не из Resources-копии `_legacy`.

---

## 5. Почему кнопки ничего не делают — диагностика (по убыванию вероятности)

Проверять в Play (VR/редактор), глядя в Console. Поскольку клик «молчит», виноват один из разрывов цепочки §1:

**A. `_button` не назначен на `RegionNavButton`.** Тогда `Start` не навешивает `onClick` → клик пуст. → В префабе/инстансе у каждой кнопки перетащи её `Button` в поле `_button`. *(Самая частая причина «вообще ничего».)*

**B. `RegionNavButton` не проинъектирован** (`_router` null). Признак: в Console нет ошибок, но `Toggle` не зовётся. → Проверь, что scope в сцене и `_navBarConfig` заполнен. Можно временно добавить в `Start` строку `Debug.Log($"NavBtn {_moduleId} router={( _router!=null)}");` — если `false`, инъекция не дошла (часто из-за того, что весь region-блок упал, см. C).

**C. Region-блок упал с исключением при построении scope.** Если `_navBarConfig` пуст → `IRegionConfig` не зарегистрирован → `c.Resolve<PanelRegionRouter>()` бросает исключение в build-callback → дальше кнопки/мемберы НЕ инъектятся/НЕ регистрируются. → Открой Console **сразу при входе в Play**, ищи исключение про `PanelRegionRouter`/`IRegionConfig`/`EventBus`. Лечится заполнением `_navBarConfig`.

**D. `Start` не выполнился — GameObject кнопки неактивен.** Если кнопка (или родительский `ButtonsBar`/`Default`) остаётся inactive после открытия `UserPanel` → `Start` не вызвался → нет `onClick`. → Убедись, что кнопки active в префабе (§4.1) и что родители активируются вместе с `UserPanel`.

**E. `RegionMember` модуля не зарегистрирован / `_moduleId` не совпал.** Тогда `Open` не находит поверхность и тихо выходит. → Сверь три строки (кнопка/member/конфиг) посимвольно; убедись, что на модуле есть `RegionMember` с правильным `_moduleId`.

**F. Кнопка спряталась по режиму.** Если в `VisibleModes` модуля нет текущего режима — `ApplyMode` сделает `SetActive(false)`, кнопки не будет видно вовсе. → Это объясняет пропажу кнопок в **Sandbox** (см. §3, баг с `4`), но не «видна, но мёртва» в VrEditing.

> Быстрый способ локализовать: добавить временные `Debug.Log` в `RegionNavButton.OnClick` (`зовётся ли клик`), в `Start` (`_button` и `_router` не null?), и в `PanelRegionRouter.Open` (`нашёлся ли moduleId`). По тому, какой лог не появился, причина определяется однозначно.

---

## 6. Проверка в Play (VR, человек)

После §3–§5 проверить в `VrEditing` (и `Sandbox`):
1. **Загрузка:** нет исключений/NullReference; `UserPanel` появляется; нав-оверлей `Default` виден.
2. **Навигация:** каждая кнопка открывает/закрывает свой модуль; второй модуль `center` закрывает первый; `outliner`(left)/`inspector`(right) открываются независимо от `center`; яркость активной кнопки переключается.
3. **Клавиатура:** кнопка клавиатуры показывает её и **прячет нав-оверлей**; повторно (или при закрытии) **нав-оверлей восстанавливается** (дефолт региона `overlays`); ввод в поля работает.
4. **Файл-браузер:** «+» в asset browser открывает диалог перед панелью; выбор файла импортирует; отмена закрывает; asset browser остаётся виден позади.
5. **Смена режима** `MainMenu`↔`VrEditing`(↔`Sandbox`): невидимые в новом режиме модули закрываются, их кнопки прячутся.

---

## 7. Подводные камни

- **`RegionNavButton.Start` нужен активный GameObject.** Кнопка, авторенная inactive, мертва: нет `onClick`, нет подписок. Все живые кнопки — active; `ApplyMode` спрячет их по режиму уже после `Start`. Только мёртвая `RiggingBtn` остаётся inactive. *(Это и была регрессия, починенная коммитом `f1af836` для Outliner/Inspector/Animator/Gizmo.)*
- **Начальный active-стейт = стартовый открытый модуль региона.** Build-callback регистрирует активные `RegionMember` как открытые в своём регионе. Поэтому: `Default` — ON (регион `overlays` стартует с него), все `ModulesSlot`-модули — OFF, `Keyboard` — OFF. Если модуль случайно ON — он станет стартовым в своём регионе.
- **Вложенные префабы.** Модули в `ModulesSlot` — вложенные префаб-инстансы внутри `UserPanel`. Их `RegionMember` мог быть добавлен как override на инстансе, а не в собственный префаб-ассет модуля. Работает, но при пересборке модуля `AnimatorPanelModuleBuilder`-ом override может потеряться. → По возможности добавлять `RegionMember` в **собственный** префаб модуля; как минимум — перепроверять этот чек-лист после `AnimatorPanelModuleBuilder`.
- **Сценовый инстанс vs префаб.** `UserPanel` — объект сцены; сценовый инстанс должен нести новые компоненты префаба. Если у инстанса старые overrides — Revert/Apply, чтобы совпадал с префабом.
- **Источник файл-браузера.** Должен использоваться **сценовый** `SimpleFileBrowserCanvas` (с нашими `FileBrowserVrAnchor`/`FileBrowserSurface`), а не Resources-копия `..._legacy.prefab` — иначе диалог не спозиционируется в VR.
- **`rigging` — мёртвая запись** (null-панель, пустые регион/режимы). Её `RegionNavButton` скрыт во всех режимах. Оставить как есть либо удалить `RiggingBtn` + запись `rigging`.
