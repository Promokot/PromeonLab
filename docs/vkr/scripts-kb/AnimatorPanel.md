---
note_type: script
subsystem: SpatialUi / Animation
listings: "3.57, Б.29"
---

> [!info] Назначение
> `AnimatorPanel` — постоянная VR-панель, координирующая весь UI таймлайна: создание `ActionContainer`, перестройку строк дорожек, управление транспортом и клавишами постановки ключей. Относится к подсистеме `SpatialUi`; взаимодействует с `AnimationAuthoring`, `AnimationClock` и `SceneContext`. Листинги 3.57, Б.29.

### Обзор

##### Роль и место

Панель живёт на `UserPanel` (корневая область, `DontDestroyOnLoad`), поэтому инжектируется **один раз** через `[Inject] Construct(...)` и существует всё время работы приложения. Подписки на события разделены на два уровня:
- **Дurable** (весь lifetime): `BonesVisibilityChangedEvent` — подписка в `Construct`, отписка в `OnDestroy`. Причина: тумблер «Show Bones» живёт в Inspector, который может быть открыт, пока вкладка Animator закрыта.
- **OnEnable/OnDisable**: все остальные события — чтобы не обрабатывать обновления, пока панель скрыта, и не накапливать устаревшее состояние.

Сама панель — исключительно координатор; отображение делегируют служебные виды (`AnimatorToolbarView`, `AnimatorTransportView`, `AnimatorRulerView`, `AnimatorPlayheadView`, `AnimatorEmptyStateView`) и элементы списка `TimelineRow_Item`.

##### Ключевые методы

| Метод | Назначение |
|---|---|
| `Construct` | Инъекция зависимостей; дurable-подписка на `BonesVisibilityChangedEvent` |
| `Refresh` | Центральный метод обновления: определяет owner, выбирает состояние (NoSelection / NoContainer / Active) |
| `OnAddAnimationClicked` | Создаёт `ActionContainer` и дорожку владельца |
| `RebuildTimeline` / `RebuildRows` | Полная перестройка таймлайна и пула строк |
| `ApplyContainerToClock` | Передаёт длину и fps контейнера в `AnimationClock.Configure` |
| `OnToggleInterpolationClicked` | Переключает интерполяцию; тригерит повторный Seek для пересчёта |
| `OnPlayPauseClicked` | Развилка: loop-режим → управляет курсором цикла; иначе → `Clock.Play/Pause` |

### Разбор кода

##### Construct — дurable-подписка

```csharp
_bus.Subscribe<BonesVisibilityChangedEvent>(OnBonesVisibilityChanged);
```

> Подписка намеренно **вне** `OnEnable/OnDisable`. Пользователь может включить режим костей через Inspector (`Show Bones`), пока вкладка аниматора закрыта. Если подписываться только в `OnEnable`, событие будет пропущено, и при открытии вкладки панель покажет «выберите объект» вместо таймлайна рига. Комментарий прямо документирует причину. `OnDestroy` отписывается, чтобы не было утечки при уничтожении GO.

##### OnAddAnimationClicked

```csharp
private void OnAddAnimationClicked()
{
    if (_ctx.Authoring == null) return;
    var selected = _ctx.Selection?.SelectedNodeId;
    var owner    = AnimationAuthoring.OwnerOf(selected);
    if (string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(_boneModeRig))
        owner = _boneModeRig;
    if (string.IsNullOrEmpty(owner)) return;

    _ctx.Authoring.CreateContainer(owner, _config.DefaultTotalFrames, _config.DefaultFps);
    _ctx.Authoring.EnsureTrack(owner, owner);
}
```

> `AnimationAuthoring.OwnerOf(selected)` — статический метод: если `nodeId` начинается с `"bone:"`, извлекает id рига; иначе возвращает сам id. Если owner пуст (ничего не выбрано), но активен bone-режим (`_boneModeRig != null`), цель — текущий риг. Это покрывает случай «пользователь вошёл в режим костей, но не выбрал конкретную кость».
>
> `EnsureTrack(owner, owner)` — создаёт дорожку **самого** владельца (его transform). Вызов после `CreateContainer`, а не внутри него, потому что дорожку владельца нужно добавлять явно: у кости как владельца дорожки нет смысла по умолчанию.

##### RebuildRows

```csharp
private void RebuildRows(ActionContainer c)
{
    foreach (var r in _rowPool) if (r != null) r.gameObject.SetActive(false);
    if (_rowsContent == null || _rowPrefab == null) return;

    for (int i = 0; i < c.Tracks.Count; i++)
    {
        var t       = c.Tracks[i];
        var go      = _ctx.Graph?.GetNode(t.NodeId);
        var display = go != null ? go.DisplayName : t.NodeId;
        bool isBone = t.NodeId.StartsWith("bone:");

        var row = GetOrCreateRow(i);
        row.gameObject.SetActive(true);
        row.Bind(t.NodeId, display, isBone, () => _ctx.Selection.Select(t.NodeId));
        row.SetActive(t.NodeId == _ctx.Selection.SelectedNodeId);
        row.SetKeys(_ctx.Authoring.GetKeyFrames(t.NodeId), _ctx.Clock.CurrentFrame);
    }
}
```

> Паттерн **object pool**: все строки деактивируются, затем первые `Tracks.Count` переиспользуются или создаются. `GetOrCreateRow(i)` расширяет пул через `Instantiate` только при нехватке. Это избегает `Destroy`/`Instantiate` при каждой перестройке.
>
> `go != null ? go.DisplayName : t.NodeId` — fallback к raw id, если нода исчезла из графа (не должно происходить в штатной работе, но защита нужна).
>
> Лямбда `() => _ctx.Selection.Select(t.NodeId)` захватывает `t.NodeId` по значению в момент итерации — безопасно, так как `t` переприсваивается в теле цикла (не `ref`-захват).

##### OnToggleInterpolationClicked

```csharp
_ctx.Authoring.SetInterpolation(_activeOwner, next);
_toolbar.SetInterpolationLabel(next.ToString());
_ctx.Clock.Seek(_ctx.Clock.CurrentFrame); // re-fire FrameChanged → re-sample with new tangents
```

> После переключения режима тангенты в `AnimationClip` изменились, но сцена не знает об этом — объекты стоят на старых позах. `Clock.Seek` с тем же кадром публикует `FrameChangedEvent`, что вызывает `AnimationPlaybackSampler.ApplyFrame` и пересчитывает позы уже по новым кривым. Без этого вызова пересчёт произошёл бы только при следующем движении бегунка.

##### OnPlayPauseClicked — развилка loop/transport

```csharp
if (!string.IsNullOrEmpty(_activeOwner) && _ctx.Authoring != null && _ctx.Authoring.IsLooping(_activeOwner))
{
    if (_ctx.Authoring.IsLoopPlaying(_activeOwner)) _ctx.Authoring.StopLoopPlayback(_activeOwner);
    else                                            _ctx.Authoring.StartLoopPlayback(_activeOwner, _ctx.Clock.CurrentFrame);
    _transport?.SetPlaying(_ctx.Authoring.IsLoopPlaying(_activeOwner));
    return;
}
if (_ctx.Clock.IsPlaying) _ctx.Clock.Pause(); else _ctx.Clock.Play();
```

> Одна кнопка Play/Pause управляет **двумя разными механизмами** в зависимости от режима:
> - Loop ON → управляет фоновым курсором в `AnimationPlaybackSampler` (не трогает `AnimationClock`).
> - Loop OFF → управляет transport-часами (`AnimationClock`).
> Транспорт показывает визуальный статус через `_transport.SetPlaying(...)` в обоих путях, поэтому UI согласован.

##### Refresh — цепочка guard'ов

```csharp
if (_ctx.Authoring == null || _ctx.Clock == null) { ShowEmpty(AnimatorEmptyStateView.State.NoSelection); return; }
```

> Явная проверка именно `Authoring` и `Clock` (не просто `_ctx.HasScene`). `HasScene` означает лишь, что `Graph` привязан, но в Sandbox `Authoring`/`Clock` равны `null`. Промах привёл бы к `NullReferenceException` внутри `Refresh`.

##### OnContainerChanged — случай Added

```csharp
if (e.Change == ContainerChange.Added)
{
    var selectedOwner = AnimationAuthoring.OwnerOf(_ctx.Selection?.SelectedNodeId);
    if (e.OwnerNodeId == _activeOwner || e.OwnerNodeId == selectedOwner) Refresh();
    return;
}
```

> Когда контейнер только что создан, `_activeOwner` ещё `null` (его не было). Обычная проверка `e.OwnerNodeId != _activeOwner` заблокировала бы `Refresh`, и пользователь увидел бы «Add animation» снова. Специальная ветка сравнивает с `selectedOwner` — это аудит-находка H5, зафиксированная в комментарии кода.

### К защите

##### Вероятные вопросы

> [!question]
> **В:** Почему `BonesVisibilityChangedEvent` подписывается в `Construct`, а не в `OnEnable`?
> **О:** Тумблер Show Bones находится в инспекторе и может быть нажат, пока вкладка аниматора закрыта (`OnDisable` уже вызван). Если подписка открыта только в `OnEnable`, событие пропускается, и при следующем `OnEnable` панель не знает о bone-режиме → показывает «select something» вместо таймлайна рига. Durable-подписка решает это: состояние запоминается в `_boneModeRig`, а перерисовка откладывается до `Refresh()` в следующем `OnEnable`.

> [!question]
> **В:** Почему `Refresh` проверяет `_ctx.Authoring == null`, а не `_ctx.HasScene`?
> **О:** `HasScene` отражает только привязку `SceneGraph`. В Sandbox граф есть, но `AnimationAuthoring` и `AnimationClock` не регистрируются в scope Sandbox — они `null`. Обращение к ним без guard вызвало бы `NullReferenceException`. Правило CLAUDE.md: «`HasScene` does not imply other services are non-null».

> [!question]
> **В:** Как устроен пул строк `_rowPool`?
> **О:** `List<TimelineRow_Item>`. При перестройке все элементы деактивируются (`SetActive(false)`). `GetOrCreateRow(i)` возвращает элемент по индексу или создаёт новый через `Instantiate`. Уничтожения нет — объекты переиспользуются. Это снижает GC-нагрузку при частой перестройке (добавление/удаление дорожек).

> [!question]
> **В:** Что происходит после переключения интерполяции Linear/Stepped?
> **О:** `SetInterpolation` обновляет флаг в `ActionContainer` и вызывает `_sampler.OnDataChanged`, который пересобирает `AnimationClip` с новыми тангентами. Но текущая поза сцены не пересчитывается автоматически. `AnimatorPanel` делает `Clock.Seek(CurrentFrame)`, что публикует `FrameChangedEvent` → `ApplyFrame` → `Sample` → позы обновляются немедленно.

> [!question]
> **В:** Как панель узнаёт, что нода — кость, а не объект?
> **О:** `t.NodeId.StartsWith("bone:")` — соглашение об именовании: кости получают id вида `"bone:{rigId}:{boneName}"`. `OwnerOf` парсит его через `Split(':')`. Это пронизывает всю анимационную подсистему без отдельного типового поля.

### Связи

[[AnimationAuthoring]] · [[AnimationClock]] · [[AnimationPlaybackSampler]] · [[SceneContext]] · [[Паттерн Publish-Subscribe]] · [[Внедрение зависимостей (VContainer)]] · [[BonesVisibilityChangedEvent]] · [[AnimationContainerChangedEvent]] · [[AnimationKeyframeChangedEvent]] · [[FrameChangedEvent]]
