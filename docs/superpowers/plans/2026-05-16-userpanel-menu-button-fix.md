# UserPanel Main Menu Button Fix — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Починить кнопку «Main Menu» в UserPanel — она должна возвращать в главное меню из любого режима.

**Architecture:** `_orchestrator` в UserPanel null потому что `RegisterInstance` не вызывает `[Inject]`. Регистрируем UserPanel в RootLifetimeScope (где физически живёт GO) через `RegisterBuildCallback` для гарантированной инъекции. Дополнительно добавляем переход ArMapping→MainMenu в граф.

**Tech Stack:** Unity 6, VContainer, C#

---

### Task 1: Добавить регистрацию UserPanel в RootLifetimeScope

**Files:**
- Modify: `Assets/_App/Bootstrap/RootLifetimeScope.cs`

- [ ] Открыть `Assets/_App/Bootstrap/RootLifetimeScope.cs`. Добавить в конец метода `Configure()` перед закрывающей скобкой:

```csharp
var userPanel = Object.FindAnyObjectByType<UserPanel>(FindObjectsInactive.Include);
if (userPanel != null)
{
    builder.RegisterInstance(userPanel);
    builder.RegisterBuildCallback(c => c.Inject(userPanel));
}
```

Добавить `using UnityEngine;` если отсутствует (уже есть).

Итоговый файл:

```csharp
using VContainer;
using VContainer.Unity;
using UnityEngine;

public class RootLifetimeScope : LifetimeScope
{
    [SerializeField] private DemoAssetCatalog    _demoAssetCatalog;
    [SerializeField] private ModeTransitionGraph _transitionGraph;

    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<PathProvider>(Lifetime.Singleton);
        builder.Register<AppStorage>(Lifetime.Singleton);
        builder.Register<EventBus>(Lifetime.Singleton);
        builder.RegisterInstance(_demoAssetCatalog);
        builder.RegisterInstance(_transitionGraph);
        builder.Register<ModeOrchestrator>(Lifetime.Singleton);

        var userPanel = Object.FindAnyObjectByType<UserPanel>(FindObjectsInactive.Include);
        if (userPanel != null)
        {
            builder.RegisterInstance(userPanel);
            builder.RegisterBuildCallback(c => c.Inject(userPanel));
        }
    }
}
```

---

### Task 2: Удалить регистрацию UserPanel из VrEditingSceneScope

**Files:**
- Modify: `Assets/_App/Bootstrap/VrEditingSceneScope.cs`

- [ ] Открыть `Assets/_App/Bootstrap/VrEditingSceneScope.cs`. Найти и удалить блок:

```csharp
var userPanel = Object.FindAnyObjectByType<UserPanel>(FindObjectsInactive.Include);
if (userPanel != null)
    builder.RegisterInstance(userPanel);
```

Итоговый Configure() (без изменений в остальном):

```csharp
protected override void Configure(IContainerBuilder builder)
{
    builder.RegisterInstance(_panelRegistry);
    builder.RegisterInstance(Camera.main);
    builder.Register<UiPanelManager>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
    builder.Register<UnsavedChangesGuard>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
    builder.Register<SceneGraph>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
    builder.Register<SelectionManager>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
    builder.Register<CommandStack>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
    builder.Register<GizmoController>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
    builder.Register<SelectionInteractorFactory>(Lifetime.Scoped).AsImplementedInterfaces();
    builder.Register<AssetImporter>(Lifetime.Scoped);

    var undo = Object.FindAnyObjectByType<UndoKeyHandler>(FindObjectsInactive.Include);
    if (undo != null)
        builder.RegisterInstance(undo);

    var rigRuntime = Object.FindAnyObjectByType<RigRuntime>(FindObjectsInactive.Include);
    if (rigRuntime != null) builder.RegisterInstance(rigRuntime).AsImplementedInterfaces().AsSelf();

    var ikWizard = Object.FindAnyObjectByType<IkSetupWizard>(FindObjectsInactive.Include);
    if (ikWizard != null) builder.RegisterInstance(ikWizard);

    var bonePanel = Object.FindAnyObjectByType<BoneInspectorPanel>(FindObjectsInactive.Include);
    if (bonePanel != null) builder.RegisterInstance(bonePanel);

    var propPanel = Object.FindAnyObjectByType<PropertyPanel>(FindObjectsInactive.Include);
    if (propPanel != null) builder.RegisterInstance(propPanel).AsImplementedInterfaces().AsSelf();
}
```

---

### Task 3: Добавить переход ArMapping→MainMenu в граф

**Files:**
- Modify: `Assets/_App/Subsystems/ModeOrchestrator/Data/DefaultModeTransitionGraph.asset`

- [ ] Открыть файл в текстовом редакторе (или через Unity Inspector). Найти список `_allowed` и добавить запись после последней:

```yaml
  - From: 2
    To: 0
```

Итоговый список `_allowed`:

```yaml
_allowed:
- From: 0
  To: 1
- From: 1
  To: 0
- From: 1
  To: 2
- From: 2
  To: 1
- From: 0
  To: 4
- From: 4
  To: 0
- From: 2
  To: 0
```

- [ ] Проверка: запустить Play Mode, перейти VrEditing → нажать кнопку Main Menu → должен открыться MainMenu. Затем перейти ArMapping → нажать кнопку Main Menu → должен открыться MainMenu.
