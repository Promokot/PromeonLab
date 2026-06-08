---
note_type: script
subsystem: ModeOrchestrator
source: Assets/_App/Scripts/ModeOrchestrator/SceneTransitionRunner.cs
listings: [3.18]
---

> [!info] Назначение
> `SceneTransitionRunner` — механизм смены сцены: затемняет VR-обзор, загружает сцену через `LoadSceneMode.Single`, вызывает callback (публикацию `ModeChangedEvent`), затем снимает затемнение. Живёт на `PersistentRoot`, переживает любые смены сцен. Листинг 3.18.

### Обзор

##### Роль и место

`SceneTransitionRunner` реализует интерфейс `ISceneTransition` и является MonoBehaviour — ему нужны корутины (`StartCoroutine`) для анимации затемнения и асинхронной загрузки. Регистрируется в [[RootLifetimeScope]] через `FindAnyObjectByType + RegisterInstance` как `ISceneTransition` — [[ModeOrchestrator]] получает его через конструктор и вызывает только интерфейсные методы.

Объект находится на `PersistentRoot` под `DontDestroyOnLoad` — живёт столько же, сколько процесс. Это принципиально: корутина, запущенная на `SceneTransitionRunner`, не прерывается при `LoadSceneMode.Single` (корутины, запущенные на объектах выгруженной сцены, прерываются).

##### Ключевые методы

| Метод | Назначение |
|---|---|
| `Load(string sceneName, Action onLoaded)` | Основной вход: проверяет реентерабельность, запускает `RunRoutine` |
| `LoadInitial(string sceneName, Action onLoaded)` | Для холодного старта: выставляет альфу в 1 (чёрный экран), затем `Load` |
| `RunRoutine(...)` (private корутина) | Fade→Load→callback→Fade обратно |

### Разбор кода

##### Load — реентерабельность и пустое имя

```csharp
public void Load(string sceneName, Action onLoaded)
{
    if (IsTransitioning || string.IsNullOrEmpty(sceneName)) return;
    IsTransitioning = true;
    StartCoroutine(RunRoutine(sceneName, onLoaded));
}
```

> Два барьера: `IsTransitioning` защищает от повторного входа во время идущего перехода (флаг устанавливается в `true` здесь, сбрасывается в конце `RunRoutine`). `string.IsNullOrEmpty(sceneName)` — защита от `null`, который возвращает `SceneNameFor` для неизвестного `AppMode` в [[ModeOrchestrator]]. Без второй проверки `SceneManager.LoadSceneAsync(null, ...)` выбросит исключение. Важно: `IsTransitioning = true` присваивается до `StartCoroutine` — не внутри корутины. Это гарантирует, что флаг установлен ещё до первого `yield`, то есть второй вызов `Load` в том же кадре уже видит `true`.

##### LoadInitial — холодный старт

```csharp
public void LoadInitial(string sceneName, Action onLoaded)
{
    if (_fade != null) _fade.SetAlphaImmediate(1f);
    Load(sceneName, onLoaded);
}
```

> При первом запуске экран должен быть чёрным **немедленно** (не после анимации — иначе пользователь увидит начальную сцену bootstrap на долю секунды). `SetAlphaImmediate(1f)` устанавливает альфу без анимации. Затем вызывается обычный `Load` — корутина начнёт с шага Fade к 1 (уже чёрный), что тоже не заметно, зато логика корутины унифицирована для холодного и горячего старта.

##### RunRoutine — четыре шага корутины

```csharp
private IEnumerator RunRoutine(string sceneName, Action onLoaded)
{
    if (_fade != null) yield return StartCoroutine(_fade.FadeRoutine(1f));

    var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
    if (op != null) yield return op;

    onLoaded?.Invoke();

    if (_fade != null) yield return StartCoroutine(_fade.FadeRoutine(0f));

    IsTransitioning = false;
}
```

> **Шаг 1 — Fade к 1 (чёрный):** `FadeRoutine(1f)` — анимированное затемнение. `yield return StartCoroutine(...)` — ждём завершения вложенной корутины. Если `_fade` не назначен — пропускаем, загружаем без затемнения.
>
> **Шаг 2 — LoadSceneAsync Single:** `LoadSceneMode.Single` выгружает все объекты всех сцен, **кроме** объектов под `DontDestroyOnLoad`. Это уничтожит старую сценовую область (`VrEditingSceneScope`), вызовет `IDisposable.Dispose()` на все её объекты, включая `SceneContextBinder` (который очистит [[SceneContext]]). `yield return op` — ждём завершения загрузки. Важно: `if (op != null)` — защита на случай, если `LoadSceneAsync` вернул `null` (сцена не найдена в build settings); без неё `yield return null` означал бы «подождать один кадр», что логически неверно.
>
> **Шаг 3 — `onLoaded?.Invoke()`:** Callback вызывается после того, как новая сцена загружена и активирована, её `LifetimeScope` построен. Здесь [[ModeOrchestrator]] публикует `ModeChangedEvent`. Это происходит **до** снятия затемнения — пользователь видит чёрный экран, пока UI перестраивается по событию.
>
> **Шаг 4 — Fade к 0 (прозрачный):** Затемнение снимается, пользователь видит уже готовую новую сцену. Плавное появление скрывает возможные визуальные артефакты первого кадра (поп-ин геометрии, перестройку UI).
>
> **`IsTransitioning = false`** — только в самом конце, после всех шагов. Если корутина прервётся досрочно (например, MonoBehaviour уничтожен), флаг не сбросится — переходы будут заблокированы навсегда. Это приемлемо: `SceneTransitionRunner` на `PersistentRoot` не уничтожается в штатном режиме.

##### Зачем затемнение — две причины

> По тексту ВКР: (1) асинхронная загрузка не гарантирует стабильной частоты кадров и может сопровождаться подвисаниями — за чёрным экраном артефакты не видны; (2) пользователь получает чёткий визуальный маркер смены режима: окружение не подменяется на глазах, а проявляется уже новым.

### К защите

> [!question] Вероятные вопросы
>
> **В:** Почему корутина запущена на `SceneTransitionRunner`, а не на объекте старой или новой сцены?
> **О:** Корутины, запущенные на MonoBehaviour, прерываются, когда объект уничтожается. `LoadSceneMode.Single` уничтожает все объекты старой сцены. Если бы корутина была на объекте старой сцены — она прервалась бы прямо во время `yield return op`. `SceneTransitionRunner` живёт на `PersistentRoot` под `DontDestroyOnLoad` и не уничтожается при смене сцены.
>
> **В:** Почему `onLoaded` вызывается до снятия затемнения?
> **О:** Чтобы UI перестроился за «занавесом»: пользователь видит чёрный экран, пока `ModeChangedEvent` перестраивает навигацию и панели. Когда затемнение снимается — всё уже готово. Если вызвать `onLoaded` после снятия — пользователь увидит кадр со старыми панелями и старой навигацией.
>
> **В:** Что произойдёт, если `_fade` не назначен в инспекторе?
> **О:** Все три `if (_fade != null)` пропускаются. Сцена загружается без анимации: мгновенное переключение. Функционально это корректно, но визуально грубо. `_fade` — `[SerializeField]`, необязательный: это осознанное решение — отсутствие компонента не ломает переход.
>
> **В:** Что значит `LoadSceneMode.Single` в отличие от `Additive`?
> **О:** `Single` выгружает все текущие сцены (кроме `DontDestroyOnLoad`) и загружает одну новую. `Additive` добавил бы новую сцену поверх существующих. Проект использует `Single`: в любой момент активна только одна режимная сцена, это упрощает управление областями жизни.
>
> **В:** `IsTransitioning = true` устанавливается до `StartCoroutine` — имеет ли это значение?
> **О:** Да. Если в одном кадре дважды вызвать `Load`, второй вызов проверяет `IsTransitioning` до следующего кадра (корутина ещё не дошла до первого `yield`). Если бы флаг устанавливался внутри корутины — второй вызов в том же кадре прошёл бы в обход защиты.

### Связи

[[ModeOrchestrator]] · [[RootLifetimeScope]] · [[AppBootstrap]] · [[SceneContext]] · [[SceneContextBinder]] · [[ModeChangedEvent]] · [[ModeExitingEvent]]
