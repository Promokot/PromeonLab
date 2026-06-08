---
note_type: script
subsystem: Animation
listings: "3.61, 3.62, Б.33"
---

> [!info] Назначение
> `AnimationPlaybackSampler` — `ITickable`-класс, отвечающий за применение анимации к нодам сцены: фоновое сэмплирование зациклённых контейнеров на независимых дробных курсорах, плавное сэмплирование активного контейнера по `CurrentFrameContinuous` часов и целочисленный `ApplyFrame` при scrub/паузе. Выделен из `AnimationAuthoring` в рамках рефакторинга A1. Листинги 3.61, 3.62, Б.33.

### Обзор

##### Роль и место

Зарегистрирован в scope VrEditing, реализует `ITickable` и `IDisposable`. Данные не хранит копией — читает живой `SceneAnimationData` через делегат `_dataSource`, установленный `AnimationAuthoring.Bind`. Хранит три словаря для loop-состояния: `_loopCursors` (owner → float-курсор), `_loopClips` (owner → dict клипов), `_loopLastFrame` (owner → последний целый кадр для deduplicated событий).

Два пути сэмплирования сходятся в одном методе `Sample(c, clips, seconds)`:
- **Transport playback** (не loop): `Tick` читает `_clock.CurrentFrameContinuous / fps` — плавно.
- **Loop playback**: `Tick` продвигает свой курсор, конвертирует `cursor / fps` — плавно.
- **Scrub / пауза**: `OnFrameChanged` → `ApplyFrame(frame)` → `(float)frame / fps` — дискретно (по целому кадру).

##### Ключевые методы

| Метод | Назначение |
|---|---|
| `Bind(dataSource)` | Устанавливает делегат на живой документ |
| `Tick()` | Продвигает loop-курсоры + сэмплирует активный контейнер при воспроизведении |
| `ApplyFrame(int)` | Сэмплирует при scrub/паузе по целому кадру |
| `Sample(c, clips, seconds)` | Единое тело сэмплирования: `clip.SampleAnimation(go, seconds)` |
| `StartLoopPlayback / StopLoopPlayback` | Добавляет/удаляет owner из loop-словарей |
| `OnDataChanged(owner)` | Пересобирает клипы активного контейнера и loop-клипы owner |
| `AdvanceLoopCursor` | `static`: продвигает курсор с wrapping; `internal` для тестирования |
| `PublishLoopFrameIfChanged` | Публикует `LoopFrameChangedEvent` только при смене целого кадра |

### Разбор кода

##### Bind — делегат на живой документ

```csharp
public void Bind(Func<SceneAnimationData> dataSource) => _dataSource = dataSource ?? (() => null);
```

> Самплер не хранит ссылку на документ — только фабрику. Когда `AnimationAuthoring.LoadAsync` присваивает новый `_data`, самплер автоматически читает его при следующем `Data`. Null-guard `?? (() => null)` позволяет безопасно вызывать `Data` без проверки на null `_dataSource`.

##### Tick — loop-часть: снимок ключей словаря

```csharp
foreach (var owner in new List<string>(_loopCursors.Keys))
{
    var c = data.FindByOwner(owner);
    if (c == null || !c.Loop) { StopLoopPlayback(owner); continue; }
    float cursor = AdvanceLoopCursor(_loopCursors[owner], Time.deltaTime * fps, c.TotalFrames);
    _loopCursors[owner] = cursor;
    if (_loopClips.TryGetValue(owner, out var clips))
        Sample(c, clips, cursor / Mathf.Max(1f, fps));
    PublishLoopFrameIfChanged(owner, cursor);
}
```

> `new List<string>(_loopCursors.Keys)` — снимок коллекции ключей. `StopLoopPlayback` внутри цикла удаляет элемент из `_loopCursors` — без снимка это `InvalidOperationException` («коллекция изменена во время итерации»). Аналог `ToArray()` из эталона A.
>
> `cursor / Mathf.Max(1f, fps)` — деление на `float`, но `fps` здесь `float` (из `private int Fps => Data?.Fps ?? 24`; неявный каст). `Mathf.Max(1f, fps)` защищает от fps=0.
>
> Если у owner снят флаг `Loop` (пользователь выключил цикл через UI), `StopLoopPlayback` чистит все три словаря и освобождает клипы. Это корректнее, чем проверять флаг только в `OnToggleModeClicked`.

##### Tick — transport-часть: проверка 4 условий

```csharp
if (_clock != null && _clock.IsPlaying
    && !string.IsNullOrEmpty(_activeContainerOwner)
    && !_loopCursors.ContainsKey(_activeContainerOwner)
    && fps > 0f)
{
    var c = data.FindByOwner(_activeContainerOwner);
    if (c != null) Sample(c, _clips, _clock.CurrentFrameContinuous / fps);
}
```

> Четыре guard'а обязательны:
> 1. `_clock.IsPlaying` — не тратить время на Sample при паузе (scrub идёт через `ApplyFrame`).
> 2. `_activeContainerOwner` не пуст — есть активный контейнер.
> 3. `!_loopCursors.ContainsKey(_activeContainerOwner)` — **критично**: если активный контейнер зациклен и уже сэмплируется loop-веткой, transport-ветка не должна конкурировать. Без этого оба пути пишут разные позы в одни и те же ноды.
> 4. `fps > 0f` — деление `CurrentFrameContinuous / fps`.

##### ApplyFrame — три ранних return

```csharp
private void ApplyFrame(int frame)
{
    if (string.IsNullOrEmpty(_activeContainerOwner)) return;
    if (_loopCursors.ContainsKey(_activeContainerOwner)) return;
    if (_clock != null && _clock.IsPlaying) return;
    var c = Data?.FindByOwner(_activeContainerOwner);
    if (c == null) return;
    int fps = Fps;
    if (fps <= 0) return;
    Sample(c, _clips, (float)frame / fps);
}
```

> - `_loopCursors.ContainsKey(_activeContainerOwner)` — если активный контейнер зациклен, loop-ветка `Tick` уже управляет позами. `ApplyFrame` не должен перебивать плавную интерполяцию дискретным целым кадром.
> - `_clock.IsPlaying` — при воспроизведении `Tick` сэмплирует по `CurrentFrameContinuous`. `FrameChangedEvent` всё равно приходит (раз в кадр анимации), но `ApplyFrame` отклоняет его: плавный путь важнее дискретного.
> - `(float)frame / fps` — `frame` целый, каст обязателен, иначе целочисленное деление обнуляет дробную часть. (Аналог опасного места в `BuildClip`.)

##### Sample — единое тело

```csharp
private void Sample(ActionContainer c, Dictionary<string, AnimationClip> clips, float seconds)
{
    foreach (var track in c.Tracks)
    {
        if (!clips.TryGetValue(track.NodeId, out var clip)) continue;
        var go = _graph?.GetNode(track.NodeId);
        if (go == null) continue;
        clip.SampleAnimation(go, seconds);
    }
}
```

> `TryGetValue` вместо прямого доступа: клипы могут не быть перестроены для всех дорожек (например, только что добавленная дорожка ещё не прошла через `OnDataChanged`). `continue` вместо `return` — остальные дорожки сэмплируются без этой.
>
> `clip.SampleAnimation(go, seconds)` — главный вызов Unity: применяет значения всех кривых клипа к компонентам `Transform` объекта `go`. Работает только с `legacy = true` клипами (см. `AnimationClipBaker`).

##### AdvanceLoopCursor — wrapping с while

```csharp
internal static float AdvanceLoopCursor(float cursor, float deltaFrames, int total)
{
    if (total <= 0) return 0f;
    float c = cursor + deltaFrames;
    while (c >= total) c -= total;
    if (c < 0f) c = 0f;
    return c;
}
```

> `while` вместо `%` (modulo) для float. Float-modulo (`c % total`) даёт корректный результат, но при очень большом `deltaFrames` (например, после паузы) `while` нагляднее и safe. При нормальной работе (`deltaFrames < total`) цикл выполняется 0–1 раз.
>
> `if (c < 0f)` — защита от отрицательного результата при `deltaFrames < 0` (теоретически невозможно, но страховка).
>
> `internal static` — тестируется изолированно в EditMode без создания экземпляра.

##### PublishLoopFrameIfChanged — дедупликация событий

```csharp
internal void PublishLoopFrameIfChanged(string owner, float cursor)
{
    int frame = Mathf.FloorToInt(cursor);
    if (_loopLastFrame.TryGetValue(owner, out var last) && last == frame) return;
    _loopLastFrame[owner] = frame;
    _bus.Publish(new LoopFrameChangedEvent { OwnerNodeId = owner, Frame = frame });
}
```

> Аналог механизма `FloorToInt + ранний return` из `AnimationClock.Tick`, но для loop-курсора. `LoopFrameChangedEvent` публикуется максимум раз в анимационный кадр (≤ fps раз/с), а не 90 раз/с. Панель аниматора двигает бегунок по `LoopFrameChangedEvent` только если `e.OwnerNodeId == _activeOwner`.

### К защите

##### Вероятные вопросы

> [!question]
> **В:** Чем отличается сэмплирование при воспроизведении от сэмплирования при scrub?
> **О:** При воспроизведении `Tick` вызывает `Sample(c, _clips, _clock.CurrentFrameContinuous / fps)` — дробная позиция обновляется каждый рендер-кадр, движение плавное. При scrub/паузе `OnFrameChanged → ApplyFrame(frame)` вызывает `Sample(c, _clips, (float)frame / fps)` — целый кадр, дискретный шаг. `ApplyFrame` ранним `return` отклоняет вызов при `IsPlaying`, чтобы не конкурировать с плавным путём.

> [!question]
> **В:** Почему у нескольких зациклённых объектов независимые курсоры?
> **О:** `_loopCursors` — словарь `owner → float`. Каждый owner продвигает свой курсор в `Tick` независимо от других и от `AnimationClock`. Поэтому несколько объектов с Loop=true воспроизводятся одновременно с разными фазами, и пользователь может редактировать сцену, пока они играют.

> [!question]
> **В:** Зачем `new List<string>(_loopCursors.Keys)` перед foreach?
> **О:** `StopLoopPlayback` внутри цикла удаляет элемент из `_loopCursors`. Итерирование по коллекции, изменяемой в теле цикла, даёт `InvalidOperationException`. Снимок `new List<string>(...)` фиксирует набор ключей до начала итерации — то же что `list.ToArray()` в `EventBus.Publish` (эталон A).

> [!question]
> **В:** Почему transport-ветка `Tick` проверяет `!_loopCursors.ContainsKey(_activeContainerOwner)`?
> **О:** Если активный контейнер зациклен, loop-ветка уже сэмплирует его ноды по своему курсору. Если transport-ветка тоже сэмплирует (по `CurrentFrameContinuous`), два вызова `SampleAnimation` за один `Tick` пишут разные позы — побеждает последний, движение становится дёрганым. Guard гарантирует взаимоисключение путей.

> [!question]
> **В:** Как `AnimationPlaybackSampler` узнаёт об изменении данных (новый ключ, смена интерполяции)?
> **О:** `AnimationAuthoring` после каждой мутации вызывает `_sampler?.OnDataChanged(owner)`. `OnDataChanged` пересобирает `_clips` (активный контейнер) и `_loopClips[owner]` (loop-клипы), вызывая `AnimationClipBaker.BuildClip`. Без этого самплер использовал бы устаревшие клипы.

### Связи

[[AnimationClock]] · [[AnimationAuthoring]] · [[AnimationClipBaker]] · [[AnimatorPanel]] · [[FrameChangedEvent]] · [[PlaybackStateChangedEvent]] · [[LoopFrameChangedEvent]] · [[Паттерн Publish-Subscribe]] · [[Внедрение зависимостей (VContainer)]]
