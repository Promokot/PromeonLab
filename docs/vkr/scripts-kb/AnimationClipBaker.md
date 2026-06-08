---
note_type: script
subsystem: Animation
listings: "3.59, Б.31"
---

> [!info] Назначение
> `AnimationClipBaker` — статический класс чистых функций: преобразует `AnimTrackData` в `AnimationClip` Unity с десятью кривыми (позиция, кватернион, масштаб) и вручную расставляет тангенты для Linear- и Stepped-интерполяции без `AnimationUtility` (недоступен в runtime-сборке). Выделен из `AnimationAuthoring` в рамках рефакторинга A1. Листинги 3.59, Б.31.

### Обзор

##### Роль и место

Нет состояния, нет зависимостей — только два публичных статических метода. Вызывается из `AnimationPlaybackSampler` при `RebuildActiveClips`, `RebuildLoopClips`, `StartLoopPlayback`. Потокобезопасен по природе (чистые функции).

##### Ключевые методы

| Метод | Назначение |
|---|---|
| `BuildClip(track, fps, mode)` | Строит `AnimationClip` из `AnimTrackData`; вызывает `ApplyInterpolation` на каждой кривой |
| `ApplyInterpolation(curve, mode)` | Расставляет тангенты по всем ключам кривой — Linear (наклон) или Stepped (∞) |

### Разбор кода

##### BuildClip — 10 кривых и legacy-флаг

```csharp
var clip = new AnimationClip { legacy = true };
```

> `legacy = true` обязателен для `clip.SampleAnimation(go, t)`. В Unity 5+ «новый» анимационный движок требует Animator-контроллера; Legacy-клипы можно сэмплировать напрямую через `AnimationClip.SampleAnimation` без компонента Animator — именно это делает `AnimationPlaybackSampler.Sample`. Без флага `SampleAnimation` работает только если на объекте есть компонент `Animation`.

```csharp
float t = (float)k.Frame / fps;
```

> Время ключа в секундах = номер_кадра / fps. При `fps = 24` кадр 12 → `t = 0.5f`. `(float)` каст необходим: `k.Frame` и `fps` — `int`, иначе целочисленное деление обнулит дробную часть для всех кадров < fps.

```csharp
clip.SetCurve("", typeof(Transform), "m_LocalRotation.x", rx);
```

> Имена свойств поворота — `m_LocalRotation.{x,y,z,w}` (serialized property names), а не `localRotation.{x,y,z,w}`. Это внутренний формат Unity. Использование неверных имён не выбрасывает исключение, но кривая молча игнорируется — поворот не анимируется.

##### ApplyInterpolation — Stepped vs Linear

```csharp
if (mode == InterpolationMode.Stepped)
{
    keys[i].inTangent  = float.PositiveInfinity;
    keys[i].outTangent = float.PositiveInfinity;
}
else
{
    if (i < keys.Length - 1)
    {
        float dt = keys[i + 1].time - keys[i].time;
        keys[i].outTangent = dt > 0f ? (keys[i + 1].value - keys[i].value) / dt : 0f;
    }
    if (i > 0)
    {
        float dt = keys[i].time - keys[i - 1].time;
        keys[i].inTangent = dt > 0f ? (keys[i].value - keys[i - 1].value) / dt : 0f;
    }
}
```

> **Stepped**: `float.PositiveInfinity` как тангент — стандартный способ задать «удерживать значение до следующего ключа» в Unity `AnimationCurve`. Движок интерпретирует бесконечный тангент как шаговую функцию.
>
> **Linear**: тангент = `(nextValue - curValue) / (nextTime - curTime)` — это просто первая производная (наклон секущей). Для первого ключа нет `inTangent` (проверка `i > 0`); для последнего — нет `outTangent` (проверка `i < keys.Length - 1`). Без этих граничных проверок — `IndexOutOfRangeException`.
>
> `dt > 0f ? ... : 0f` — защита от двух ключей с одинаковым временем (деление на ноль). В нормальной работе невозможно (UpsertKey пишет по целому номеру кадра), но защита обязательна.
>
> **Почему не `AnimationUtility`?** `AnimationUtility` находится в пространстве `UnityEditor` и доступен только в редакторе. В Android-сборке класс отсутствует. Ручной расчёт тангентов — единственный runtime-совместимый способ.

##### curve.keys = keys — важная деталь

```csharp
curve.keys = keys;
```

> `curve.keys` — **копия** массива, а не ссылка. Изменение элементов `keys[i]` без присвоения обратно в `curve.keys` не имело бы эффекта. Это типичная ловушка Unity struct-API. Строка в конце `ApplyInterpolation` — не оптимизация, а **обязательное** завершение.

### К защите

##### Вероятные вопросы

> [!question]
> **В:** Почему тангенты рассчитываются вручную, а не через `AnimationUtility.SetEditorCurve`?
> **О:** `AnimationUtility` — часть `UnityEditor`-сборки, недоступна в Android-сборке приложения. Весь код в `_App.Runtime` должен работать без редакторских зависимостей. Ручной расчёт (наклон секущей для Linear, ∞ для Stepped) воспроизводит поведение, которое редактор делал бы автоматически.

> [!question]
> **В:** Что означает `float.PositiveInfinity` как тангент в Stepped-режиме?
> **О:** В Unity `AnimationCurve` бесконечный тангент задаёт шаговую (ступенчатую) функцию: значение удерживается постоянным до следующего ключа, скачок происходит мгновенно. Это стандарт, использованный, например, в Animator при Constant-интерполяции.

> [!question]
> **В:** Почему `AnimationClip` создаётся с `legacy = true`?
> **О:** `AnimationClip.SampleAnimation(GameObject, float)` работает только с legacy-клипами (или при наличии компонента `Animation` на объекте). В VR-приложении на объектах нет Animator/Animation компонентов — используется прямое сэмплирование. Без `legacy = true` вызов был бы проигнорирован или вызвал ошибку в зависимости от версии Unity.

> [!question]
> **В:** Что произойдёт, если два ключа стоят на одном кадре?
> **О:** `track.UpsertKey` перезапишет старый ключ — дубликатов по номеру кадра быть не должно. Но если каким-то образом `dt = 0`, защита `dt > 0f ? ... : 0f` выдаёт тангент 0 вместо деления на ноль. Анимация не сломается, но переход будет горизонтальным.

### Связи

[[AnimationPlaybackSampler]] · [[AnimationAuthoring]] · [[Структуры анимационных данных]] · [[AnimationClock]]
