---
note_type: script
subsystem: Animation
listings: "3.60, Б.32"
---

> [!info] Назначение
> `AnimationClock` — transport-часы анимационной подсистемы: хранит целочисленный `CurrentFrame` и дробный накопитель `_accumulated`, реализует `ITickable` VContainer, публикует `FrameChangedEvent` и `PlaybackStateChangedEvent`. Воспроизведение single-shot: достигнув `TotalFrames`, часы останавливаются и сбрасываются к кадру 0. Листинги 3.60, Б.32.

### Обзор

##### Роль и место

Зарегистрирован в scope сцены VrEditing. Отсутствует в Sandbox и MainMenu. `ITickable` — VContainer вызывает `Tick()` каждый кадр рендеринга автоматически (без `MonoBehaviour.Update`). Единственный экземпляр на сцену; `AnimatorPanel` конфигурирует его через `Configure` и читает `CurrentFrame` / `CurrentFrameContinuous`.

Два выходных значения разного типа:
- `CurrentFrame` (int) — для бегунка, меток, событий (квантованная позиция).
- `CurrentFrameContinuous` (float) = `_accumulated` — для сэмплирования поз в `AnimationPlaybackSampler` (плавная позиция).

##### Ключевые методы

| Метод | Назначение |
|---|---|
| `Tick()` | Ядро транспорта: продвигает `_accumulated`, меняет `CurrentFrame` только при пересечении целой границы |
| `AdvanceFrame(int next)` | Проверяет достижение конца, сбрасывает или продвигает кадр, публикует события |
| `Configure(int totalFrames, int fps)` | Устанавливает длину и частоту; корректирует `CurrentFrame` если он вышел за новую границу |
| `Seek(int frame)` | Перемотка: `_accumulated = CurrentFrame` — сбрасывает дробную часть |
| `Play()` | Если текущий кадр = конец → Seek(0); затем `IsPlaying = true` |
| `Stop()` | `IsPlaying = false`, `_accumulated = 0`, `CurrentFrame = 0`; публикует оба события |

### Разбор кода

##### Tick — накопитель и ранний return

```csharp
public void Tick()
{
    if (!IsPlaying) return;
    _accumulated += Time.deltaTime * Fps;
    var next = Mathf.FloorToInt(_accumulated);
    if (next == CurrentFrame) return;
    AdvanceFrame(next);
}
```

> `_accumulated` — дробная позиция в кадрах. За экранный кадр прибавляется `Δt·Fps` (при 24 fps анимации и 90 fps дисплея ≈ 0,267 кадра). `CurrentFrame` (целое) меняется только при переходе через целую границу — `FloorToInt` + ранний `return`, чтобы не слать `FrameChangedEvent` 90 раз/с, не двигать бегунок и не перерисовывать маркеры на каждом рендер-кадре.
>
> Плавность движения объектов сцены даёт **не** `CurrentFrame`, а `CurrentFrameContinuous => _accumulated` (дробное), которое `AnimationPlaybackSampler.Tick` читает напрямую через `_clock.CurrentFrameContinuous / fps`. Поэтому при 90 fps рендеринга и 24 fps анимации движение остаётся плавным — поза интерполируется между ключами по дробной позиции, а не «перещёлкивается» 24 раза/с.

##### AdvanceFrame — single-shot и сброс

```csharp
internal void AdvanceFrame(int next)
{
    if (next >= TotalFrames)
    {
        IsPlaying    = false;
        CurrentFrame = 0;
        _accumulated = 0f;
        _bus.Publish(new FrameChangedEvent         { Frame = 0 });
        _bus.Publish(new PlaybackStateChangedEvent { IsPlaying = false, Frame = 0, Completed = true });
        return;
    }

    CurrentFrame = next;
    _bus.Publish(new FrameChangedEvent         { Frame     = CurrentFrame });
    _bus.Publish(new PlaybackStateChangedEvent { IsPlaying = IsPlaying, Frame = CurrentFrame });
}
```

> `next >= TotalFrames` — условие остановки. После остановки `_accumulated = 0f` — сбрасывает дробную часть, чтобы `CurrentFrameContinuous` тоже вернулся к 0. Без этого следующий `Play()` мог бы стартовать с ненулевой дробной позиции.
>
> `Completed = true` в `PlaybackStateChangedEvent` — сигнал `AnimationPlaybackSampler.OnPlaybackState` вызвать `ApplyFrame(0)`: нужно применить позу нулевого кадра сразу, не ждя следующего `Tick`.
>
> Метод `internal` — публичный API только `Tick`; но `internal` позволяет вызывать из EditMode-тестов напрямую без симуляции времени.
>
> Два `Publish` подряд — оба на одном потоке (главный). `EventBus.Publish` с `ToArray()` защищён от модификации коллекции подписчиков, но не от вложенных publish: если подписчик `FrameChangedEvent` опубликует другое событие — это ок, они последовательны.

##### Configure — коррекция текущего кадра

```csharp
public void Configure(int totalFrames, int fps)
{
    TotalFrames = Mathf.Max(1, totalFrames);
    Fps         = Mathf.Max(1, fps);

    if (CurrentFrame > TotalFrames)
    {
        CurrentFrame = TotalFrames;
        _accumulated = TotalFrames;
        _bus.Publish(new FrameChangedEvent { Frame = CurrentFrame });
    }
}
```

> `Mathf.Max(1, ...)` — защита от нулевых/отрицательных значений: fps=0 сделал бы `Tick` бесконечным циклом (деление на ноль в самплере), а totalFrames=0 — немедленную остановку при любом `AdvanceFrame`.
>
> Если пользователь сократил длину контейнера и `CurrentFrame > TotalFrames`, часы переставляют позицию к новому концу и публикуют `FrameChangedEvent`. Без публикации бегунок остался бы на невалидной позиции.
>
> `Configure` **не** останавливает воспроизведение. Если часы играли, они продолжат; при следующем `AdvanceFrame` они корректно остановятся на новом `TotalFrames`.

##### Seek — сброс дробной части

```csharp
public void Seek(int frame)
{
    CurrentFrame = Mathf.Clamp(frame, 0, TotalFrames);
    _accumulated = CurrentFrame;
    _bus.Publish(new FrameChangedEvent { Frame = CurrentFrame });
}
```

> `_accumulated = CurrentFrame` (int → float) — критично. Без этого после Seek дробная часть `_accumulated` могла бы быть произвольной (например, `23.8f` от предыдущего Tick), и первый же `Tick` вычислил бы `FloorToInt(23.8 + delta)` → возможно сразу двинул бы кадр вперёд, минуя только что установленную позицию.
>
> `Seek` не публикует `PlaybackStateChangedEvent`. Перемотка — не смена состояния play/pause.

##### Play — защита от воспроизведения с конца

```csharp
public void Play()
{
    if (CurrentFrame >= TotalFrames) Seek(0);
    IsPlaying = true;
    _bus.Publish(new PlaybackStateChangedEvent { IsPlaying = true, Frame = CurrentFrame });
}
```

> Если пользователь нажал Play, стоя на последнем кадре, — воспроизведение сразу бы остановилось на первом же `AdvanceFrame`. `Seek(0)` перематывает в начало превентивно.

### К защите

##### Вероятные вопросы

> [!question]
> **В:** Зачем хранить `_accumulated` отдельно от `CurrentFrame`? Разве недостаточно одного целого числа?
> **О:** `CurrentFrame` (int) управляет бегунком и событиями — он квантован по частоте анимации. `_accumulated` (float) — плавная дробная позиция, которую `AnimationPlaybackSampler` использует для сэмплирования `AnimationClip.SampleAnimation`. Если бы сэмплер читал только целый `CurrentFrame`, при 90 fps рендеринга и 24 fps анимации поза менялась бы 24 раза в секунду — эффект «квантования» (дёрганое движение). `CurrentFrameContinuous` даёт плавность без увеличения количества событий.

> [!question]
> **В:** Почему `FrameChangedEvent` не публикуется 90 раз в секунду при воспроизведении?
> **О:** `Tick` вычисляет `next = FloorToInt(_accumulated)` и сравнивает с `CurrentFrame`. Если целая часть не изменилась — ранний `return`. При 90 fps рендеринга и 24 fps анимации за большинство кадров `_accumulated` вырастает на ~0.27, не пересекая целую границу. Событие публикуется только при смене целого кадра — примерно 24 раза в секунду.

> [!question]
> **В:** Что значит «single-shot» воспроизведение?
> **О:** Достигнув `TotalFrames`, транспорт останавливается (`IsPlaying = false`), сбрасывается к кадру 0 и публикует `Completed = true`. Повтора нет. Цикл (Loop) — это отдельный механизм `AnimationPlaybackSampler._loopCursors`, не зависящий от `AnimationClock`.

> [!question]
> **В:** Зачем `Seek` присваивает `_accumulated = CurrentFrame`?
> **О:** Без этого после перемотки дробный накопитель остался бы на значении времени предыдущего воспроизведения. Например, `_accumulated = 23.8f`, Seek(5): `CurrentFrame = 5`, но `_accumulated = 23.8`. Первый же `Tick` вычислит `FloorToInt(23.8 + 0.27) = 24`, сразу прыгнув к кадру 24 вместо движения от 5.

> [!question]
> **В:** Почему `Configure` публикует `FrameChangedEvent` только если `CurrentFrame > TotalFrames`?
> **О:** Если кадр в пределах новой длины, ничего не меняется — событие было бы холостым и могло вызвать лишние перерисовки. Публикация только при необходимой коррекции — минимально необходимый UI-апдейт.

> [!question]
> **В:** `AdvanceFrame` помечен `internal`, хотя почти вся логика там. Почему?
> **О:** Публичный API — `Tick`, `Play`, `Pause`, `Seek`, `Configure`. `AdvanceFrame` — деталь реализации, вынесенная отдельно для тестируемости: EditMode-тесты могут вызвать `AdvanceFrame(n)` напрямую без симуляции `Time.deltaTime`, проверяя логику остановки и публикации событий изолированно.

### Связи

[[AnimationPlaybackSampler]] · [[AnimationAuthoring]] · [[AnimatorPanel]] · [[FrameChangedEvent]] · [[PlaybackStateChangedEvent]] · [[Паттерн Publish-Subscribe]] · [[Внедрение зависимостей (VContainer)]]
