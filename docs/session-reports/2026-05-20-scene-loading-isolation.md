# Отчёт: Изоляция загрузки сцен — 2026-05-20

## Проблема

Bootstrap-сцена грузится первой и держит персистентную инфраструктуру (`RootLifetimeScope`, XR Rig, `UserPanel`, `AssetBrowserModule`, `VrKeyboard`, `PlayerSpawnApplier`). Mode-сцены (`MainMenu`, `VrEditing`, `ArMapping`, `ArPreview`, `Sandbox`) грузятся additively поверх неё.

Из-за этого:
- Bootstrap оставалась Unity Active Scene → её `RenderSettings` (skybox, fog, ambient) применялись во всех режимах
- Любые `Light` в bootstrap освещали все loaded scenes (Unity мержит лайтинг по всем сценам, игнорируя active-scene границу)

Каждая mode-сцена выглядела с визуалом bootstrap'а вместо своего.

## Решение

**Двухчастная правка:**

1. **Bootstrap-сцена → pure infrastructure carrier:**
   - Удалены все `Light` объекты
   - Environment: Skybox = None, Ambient = solid black, Reflection = Skybox (без skybox = nothing)
   - Fog: off

2. **`SetActiveScene` после каждой additive загрузки:**
   - `AppBootstrap.cs` — подписка на `sceneLoaded`, после загрузки `MainMenu` вызывает `SetActiveScene(scene)` + отписка
   - `ModeOrchestrator.OnSceneLoadedForSpawn` — добавлена одна строка `SceneManager.SetActiveScene(scene);` сразу после отписки

Теперь Active Scene всегда mode-сцена, её RenderSettings драйвят рендер. Bootstrap остаётся loaded, но не Active — его RenderSettings игнорируются.

## Изменённые файлы

| Файл | Изменение |
|---|---|
| `Assets/_App/Bootstrap/AppBootstrap.cs` | Replace: добавлен sceneLoaded callback с SetActiveScene |
| `Assets/_App/Subsystems/ModeOrchestrator/ModeOrchestrator.cs` | +1 строка в `OnSceneLoadedForSpawn` |
| Bootstrap-сцена (Editor) | Удалены Light'ы, neutral RenderSettings |
| Mode-сцены (Editor) | Каждая владеет своими Light/Skybox/Fog |
| `docs/superpowers/specs/2026-05-20-scene-loading-isolation.md` | Создан спек |
| `docs/superpowers/plans/2026-05-20-scene-loading-isolation.md` | Создан план |

## Альтернативы которые НЕ выбрали

- **`LoadSceneMode.Single` + `DontDestroyOnLoad`** — ломает `RootLifetimeScope` (использует `FindAnyObjectByType` для UserPanel/AssetBrowserModule/PlayerSpawnApplier/VrKeyboard, которые бы пересоздавались на каждом переходе). Большой рефакторинг DI.
- **Per-frame override skybox/fog в коде** — обходит симптом, лайты всё равно текут, brittle.

## Статус

Работает: каждая mode-сцена показывает свой визуал, переходы чистые, bootstrap не bold в Hierarchy после Frame 1.

## Известные ограничения

- Async loading отложен на потом (если будут frame stutter при transition — заменить sync `LoadScene` на `LoadSceneAsync`; `SetActiveScene` уже в `sceneLoaded`-callback'е, который работает с обоими режимами)
- Reflection probes (baked lighting) — отдельная задача, спек не покрывает
- Lighting settings asset per scene — Unity создаёт автоматически с дефолтами, не было нужды трогать
