# Scene Save / Load — Bug Analysis

**Date:** 2026-05-17

---

## Bug 1 — SceneOpenedEvent fires before SceneGraph exists (критический)

**Симптом:** Объекты не восстанавливаются при открытии сцены.

**Причина:** В `MainMenuPanel.OpenSceneAsync()`:
```csharp
_storage.SetActiveScene(data);
_bus.Publish(new SceneOpenedEvent { SceneId = data.SceneId }); // ← сначала
_orchestrator.TransitionTo(AppMode.VrEditing);                 // ← потом загружает сцену
```
`TransitionTo` вызывает `SceneManager.LoadScene(sceneName, LoadSceneMode.Additive)` — загрузка асинхронная.
`SceneGraph` создаётся внутри `VrEditingSceneScope`, который появляется только после загрузки сцены.
К моменту когда `SceneGraph.Start()` подписывается на `SceneOpenedEvent`, событие уже сгорело.

**Фикс:** В `SceneGraph.Start()` — после подписки — сразу проверять `_storage.ActiveSceneId` и запускать загрузку, если ID уже установлен:
```csharp
public void Start()
{
    _spawnedRoot = new GameObject("[Spawned]").transform;
    _bus.Subscribe<SceneOpenedEvent>(OnSceneOpened);

    var activeId = _storage.ActiveSceneId;
    if (!string.IsNullOrEmpty(activeId))
        _ = OnSceneOpenedAsync(new SceneOpenedEvent { SceneId = activeId });
}
```

---

## Bug 2 — Сцены отображаются не в порядке создания

**Симптом:** В ScenePickerPanel список сцен в случайном порядке.

**Причина:**
- `AppStorage.GetAllSceneIds()` использует `Directory.GetDirectories()` → алфавитный порядок по именам папок (случайные GUID-фрагменты).
- `CreatedAt` хранит только дату (`"yyyy-MM-dd"`), без времени → нельзя сортировать сцены созданные в один день.

**Фикс:**
1. В `AppStorage.CreateSceneAsync()` изменить формат: `DateTime.UtcNow.ToString("o")` (ISO 8601 с временем).
2. В `AppStorage.GetAllScenesAsync()` сортировать результат по `data.CreatedAt` по возрастанию.

---

## Дополнительно — SchemaVersion migration (некритично)

`JsonUtility.FromJson<SceneData>()` запускает field initializer `SchemaVersion = 2` перед применением JSON.
Если файл v1 (без поля `SchemaVersion`) — он прочитается как v2, миграция не сработает.
Актуально только для данных созданных до Spec A. Если таких нет — игнорировать.

---

## Файлы для изменения

| Файл | Изменение |
|---|---|
| `Subsystems/SceneComposition/SceneGraph.cs` | `Start()` — catch-up load |
| `Subsystems/StorageCore/AppStorage.cs` | `CreatedAt` format + sort in `GetAllScenesAsync` |
