# PromeonLab — Реструктуризация `_App/` (design spec)

> Дата: 2026-05-28. Unity 6000.3.7f1. Уточняет и заменяет `STRUCTURE_TARGET.md` в части asmdef-модели, namespace-политики и порядка миграции.
> Источник текущего состояния: `Assets/_App/Documentation/STRUCTURE.md`.

## 1. Цель и рамки

Перевести `_App/` с feature-based раскладки (21 asmdef, папка `_Shared/` как канал контрактов) на **layer-based раскладку с тремя сборками**: весь рантайм-код — в одной сборке, сабсистемы остаются как организационные папки. Снизить количество asmdef'ов и упростить структуру, не меняя поведения приложения.

Файлы двигаются с сохранением Unity GUID (через `AssetDatabase.MoveAsset` / Unity MCP), поэтому ссылки на скрипты в префабах/сценах/SO не ломаются.

## 2. Зафиксированные решения

| # | Решение | Обоснование |
|---|---|---|
| D1 | **3 asmdef:** `_App.Runtime` (в `Scripts/`), `_App.Editor` (в `Editor/`), `_App.Tests` (в `Tests/`). Per-subsystem asmdef удалены. | Одна папка = одна сборка. Максимально просто, без вложенных asmdef. |
| D2 | **Отдельной сборки `Core` нет.** `Core` — обычная папка `Scripts/Core/` с `EventBus.cs` + `ICommand.cs`, компилируется в `_App.Runtime`. | Стенка вокруг двух крошечных стабильных файлов не окупает 4-ю сборку и вложенный asmdef. |
| D3 | **Namespace'ы не вводим.** Рантайм-код остаётся в global namespace (как сейчас). Раздел 7 `STRUCTURE_TARGET.md` отменён. | В рантайме namespace'ов нет → слияние сборок не требует правки `using`. Не смешиваем два больших изменения. |
| D4 | **Контракты расходятся к владельцам.** Интерфейсы/модели/события/енамы — в папку сабсистемы-владельца. В `Core/` только `EventBus` + `ICommand`. | Feature-cohesion: всё про сабсистему — в одной папке. Удобно искать. |
| D5 | **Паттерн связи:** интерфейс + VContainer DI (запрос/ответ) + шина событий (уведомления). Прямые конкретные вызовы между сабсистемами — нельзя (соглашением). | Формализует уже доминирующий паттерн. Принуждения нет (одна сборка), граница — соглашение + ревью. |
| D6 | **Unity-managed конфиг не трогаем:** `XR/`, `XRI/`, `CompositionLayers/` остаются на месте. `TextMesh Pro/` — опционально, отдельным проверочным шагом в конце. | Unity пересоздаёт эти папки по дефолтным путям; перенос — высокий риск, низкая ценность. |
| D7 | **`Assets/Resources/` распускается в `_App/Content/`.** | Подтверждено: `Resources.Load(...)` в `_App` не используется нигде → ассеты держатся по GUID, перенос безопасен. |
| D8 | **Порядок: «сборки первыми».** Сначала схлопнуть сборки на месте, потом двигать файлы. | В одной сборке расположение `.cs` по папкам не влияет на компиляцию → перемещение безрисково. |
| D9 | **Перемещения выполняет Claude через Unity MCP** (`manage_asset` = `AssetDatabase.MoveAsset`). Проверка и финальные удаления — за пользователем. | GUID-safe. Unity должен быть открыт и MCP подключён. |

## 3. Целевая модель сборок

```
_App/
├── Scripts/
│   └── _App.Runtime.asmdef     name "_App.Runtime"
├── Editor/
│   └── _App.Editor.asmdef      name "_App.Editor"   includePlatforms: [Editor]
├── Tests/
│   └── _App.Tests.asmdef       name "_App.Tests"    includePlatforms: [Editor], TestAssemblies
├── Content/                    ассеты, .cs нет — без asmdef
└── Scenes/
```

В корне `_App/` asmdef'а нет.

**`_App.Runtime` references** (union из текущих asmdef): `VContainer`, `Unity.TextMeshPro`, `Unity.XR.Interaction.Toolkit`, `Unity.InputSystem`, `SimpleFileBrowser.Runtime`, `QuickOutline`, `Unity.Animation.Rigging`. `autoReferenced: false`.

**`_App.Editor` references:** `_App.Runtime`, `Unity.TextMeshPro`, `Unity.Animation.Rigging`. `includePlatforms: ["Editor"]`, `autoReferenced: false`.

**`_App.Tests` references:** `_App.Runtime` + те же внешние пакеты, что использует тест-код (`VContainer`, `Unity.XR.Interaction.Toolkit`, `Unity.InputSystem`, `QuickOutline`) + TestAssemblies. `includePlatforms: ["Editor"]`. Требует `[assembly: InternalsVisibleTo("_App.Tests")]` в Runtime (заменяет нынешний в `AnimationAuthoring/InternalsVisibleTo.cs`, целивший в `Subsystems.AnimationAuthoring.Tests`).

## 4. Целевая раскладка `_App/Scripts/`

Сабсистемы — как организационные папки (имена прежние). `Data/`-подпапки уплощаются в корень сабсистемы. Исключение — `VrInteraction/Gizmo/Strategies/` сохраняется (5 файлов одной роли).

```
Scripts/
├── _App.Runtime.asmdef
├── Core/                 EventBus.cs, ICommand.cs
├── Bootstrap/            AppBootstrap, RootLifetimeScope, *SceneScope, FallGuard, UndoKeyHandler, VrInputFieldProxy, PlayerSpawnApplier
├── ModeOrchestrator/     ModeOrchestrator, ModeTransitionGraph, AppMode, ModeChangedEvent
├── SceneComposition/     SceneGraph, SceneNode, SelectionManager, SceneAutoSaver, CommandStack, TransformCommand,
│                         ISceneGraph, ISelectionManager, Constraints/, + Scene*/Selection*/NodeRenamed события
├── AssetBrowser/         Asset{Importer,Registry,Spawner}, *AssetLibrary, *LabAsset, DemoAssetCatalog,
│                         IAssetLibrary, IAssetRegistry, ILabAsset, AssetEntry, AssetType, AssetRef, AssetSource, Asset*Event
├── AnimationAuthoring/   AnimationAuthoring, AnimationClock, AnimationClipboard, Action/Anim*/FrameClipboard*/SceneAnimationData,
│                         ContainerChange, KeyframeChange, InternalsVisibleTo, Frame/Playback/AnimationKeyframe/Container события
├── AnimationPlayback/    AnimationPlayback (placeholder)
├── RigBuilder/           PromeonProxyRigBuilder, RigRuntime, RigSerializer, BoneFollower, BoneProxy,
│                         IRigRuntime, RigDefinition, BoneRecord, IkChainRecord, BoneSceneNodeMarker, BonesVisibilityChangedEvent
├── VrInteraction/        XRPromeonInteractable, GizmoController, WorldClickCatcher, Selectable, SelectionVisualSync, IDragStrategy,
│                         GizmoMode, SelectionVisual, Gizmo/ (+Strategies/), Gizmo*Event
├── SpatialUi/            UiPanelManager, *Panel, *Module, Views/, Elements/, PanelType, PanelRegistry, NavBarConfig,
│                         AnimatorPanelConfig, PanelId, Panel*/KeyboardFocus события
├── StorageCore/          AppStorage, PathProvider, SceneSerializer, UnsavedChangesGuard, SceneData, NodeData, AssetCatalogData
├── InputBindings/        InputBindings (placeholder)
├── ErrorHandling/        ErrorHandling, ErrorLevel, ErrorOccurredEvent
└── ExportPipeline/       ExportPipeline (placeholder)
```

`Content/`, `Tests/`, `Editor/`, `Scenes/`, `Documentation/` — как в `STRUCTURE_TARGET.md` (разделы 3, 4.5–4.9), с поправкой: тесты в `_App/Tests/<Subsystem>/` под одним `_App.Tests.asmdef`; editor плоско в `_App/Editor/` под `_App.Editor.asmdef`.

## 5. Распределение контрактов (что куда из `_Shared/`)

`Core/`: `EventBus`, `ICommand` — и больше ничего.

Всё остальное из `_Shared/` уезжает к владельцу:
- Интерфейсы → папка реализующей сабсистемы (`ISceneGraph`/`ISelectionManager`→SceneComposition; `IRigRuntime`→RigBuilder; `IAssetLibrary`/`IAssetRegistry`/`ILabAsset`→AssetBrowser).
- Модели → владельцу (`AppMode`→ModeOrchestrator; `AssetEntry`/`AssetType`/`AssetRef`/`AssetSource`→AssetBrowser; `RigDefinition`/`BoneRecord`/`IkChainRecord`/`BoneSceneNodeMarker`→RigBuilder; `GizmoMode`/`SelectionVisual`→VrInteraction; `PanelId`→SpatialUi; `ErrorLevel`→ErrorHandling).
- Енамы изменений: `ContainerChange`, `KeyframeChange` → AnimationAuthoring (по факту анимационные, в `Core` не идут — уточнение к таргету).
- `AppEvents.cs` (23 структуры) разрезается по одному файлу-событию рядом с издателем (карта — раздел 4.3 `STRUCTURE_TARGET.md`).

## 6. Паттерн связи (D5) и опциональная уборка

Текущая реальность: события — ~90 `Publish/Subscribe` в 30 файлах; DI через интерфейсы там, где интерфейс есть; но есть протечки в конкретику — `SceneOutlinerView.cs:14`, `AnimatorPanelView.cs:25`, `SceneInspectorView.cs:39` инжектят конкретный `SceneGraph` вместо `ISceneGraph`.

Опциональный отдельный шаг (не блокирует миграцию): заменить инжект конкретного `SceneGraph` на `ISceneGraph` в этих 3-4 view для единообразия. `AnimationClipboard` (конкретный, инжектится в `AnimatorPanelView`) — интерфейса нет, оставляем как есть.

## 7. Порядок миграции

### Phase 0 — Пред-полёт (пользователь)
- Сделать чекпоинт-коммит (git — за пользователем).
- Открыть Unity, убедиться что MCP подключён.

### Phase 1 — Атомарное слияние сборок (Claude через MCP; одно компилируемое состояние)
Тесты и editor-код нельзя оставить под удаляемыми asmdef — иначе они «провалятся» в рантайм-сборку (NUnit/UnityEditor не скомпилируются там). Поэтому переносим их в этом же шаге.

1. Создать `_App/Tests/`; перенести файлы 5 Tests-папок → `_App/Tests/<Subsystem>/`; создать `_App/Tests/_App.Tests.asmdef`; удалить 5 per-subsystem `*.Tests.asmdef`.
2. Перенести `AnimatorPanelModuleBuilder.cs` → `_App/Editor/`; переименовать `_App/Editor/PromeonLab.Editor.asmdef` → `_App.Editor.asmdef` (обновить `name` + references); удалить `Subsystems/SpatialUi/Editor/Subsystems.SpatialUi.Editor.asmdef`.
3. Переименовать корневой `_App/_App.asmdef` → `_App.Runtime.asmdef` (`name:"_App.Runtime"`, references = только внешние пакеты из §3, убрать ссылки на сабсистемы и `_Shared`). Удалить `_Shared/_Shared.asmdef` + 11 subsystem asmdef + 3 пустых tombstone (`RigBuilder/Data/{BoneRecord,IkChainRecord,RigDefinition}.cs`). Переписать `InternalsVisibleTo.cs` → `[assembly: InternalsVisibleTo("_App.Tests")]`.
   - Корневой `_App.Runtime.asmdef` становится зонтиком над всем `_App/` (кроме `Editor/` и `Tests/`) и впитывает `Bootstrap/`, `Subsystems/`, `_Shared/` **на месте**.
4. **CHECKPOINT:** Reimport → компиляция → прогон тестов → открыть Bootstrap. Пользователь проверяет консоль и сцены (нет missing-script).

### Phase 2 — Раскладка кода и ассетов (Claude через MCP; компиляции уже не угрожает)
5. Создать `_App/Scripts/` + подпапки. Перенести весь рантайм-`.cs` в `Scripts/<Subsystem>/`, `Bootstrap`→`Scripts/Bootstrap/`, `EventBus`+`ICommand`→`Scripts/Core/`.
6. Переместить файл рантайм-asmdef из корня `_App/` → `_App/Scripts/` (зонтик теперь над `Scripts/`; в корне `_App/` asmdef не остаётся).
7. Разрезать `AppEvents.cs` → 23 файла к издателям; распустить `_Shared` интерфейсы/модели/события к владельцам (§5); уплостить `Data/`-подпапки (кроме `VrInteraction/Gizmo/Strategies/`).
8. Перенести ассеты: `Assets/Resources/*` и `Subsystems/*/{Prefabs,Data,UI}/*` → `_App/Content/...` (карта — разделы 4.5–4.7 `STRUCTURE_TARGET.md`).
9. (опц.) Уборка `SceneGraph`→`ISceneGraph` (§6).
10. **CHECKPOINT:** Reimport → компиляция → тесты → Bootstrap. Пользователь проверяет.
11. Обновить `CLAUDE.md` + `Assets/_App/Documentation/*.md` под новую раскладку (3 asmdef, `Core`-папка, отсутствие `_Shared`, фактический `EventBus` вместо MessagePipe).

### Phase 3 — Зачистка (пользователь, либо Claude через MCP по запросу)
12. Удалить опустевшие `_App/_Shared/`, `_App/Subsystems/`, `_App/DemoAssets/`, остатки контента `Assets/Resources/`, `AppEvents.cs`.

### Опционально (в самом конце, отдельно)
- Перенос `TextMesh Pro/` → `UnityPacks/` с проверкой загрузки шрифтов (TMP грузит свои ресурсы через `Resources.Load` из вложенной `Resources/`-папки — путь должен пережить move, но проверить).

## 8. Разбивка работ

**Claude (через Unity MCP + правка файлов):** все 3 asmdef; все перемещения скриптов и ассетов; разрезка `AppEvents`; роспуск `_Shared`; уплощение `Data/`; правка `InternalsVisibleTo`; опц. уборка интерфейсов; обновление доков. Перемещения — атомарными пачками с паузами.

**Пользователь:** пред-полёт (git-коммит, открытый Unity + MCP); проверка на чекпоинтах (консоль, плей, отсутствие missing-script на префабах/сценах); финальные удаления опустевших папок (Phase 3) — либо делегировать Claude через MCP.

## 9. Проверенные факты (снижают риск)

- В рантайм-коде namespace'ов нет (только `VrAnimApp.Editor`); ни один asmdef не задаёт `rootNamespace` → слияние не требует правки `using`.
- Единственные дубли имён типов на все 165 `.cs` — 3 пустых tombstone (`BoneRecord`/`IkChainRecord`/`RigDefinition` в `RigBuilder/Data/`). После их удаления коллизий в одной сборке нет.
- `Resources.Load(...)` в `_App` не используется → роспуск `Resources/` безопасен.
- `MessagePipe` в коде не используется; реальная шина — кастомный `EventBus.cs` (доки это переврают, поправить в §7 шаг 11).

## 10. Риски и страховки

- **Unity должен быть открыт и MCP подключён** на время перемещений (MoveAsset выполняется в редакторе). Если Unity закрыт — не двигать.
- **Чекпоинт после Phase 1 — критический гейт.** До проверки пользователем массовую раскладку (Phase 2) не начинать.
- **Скрытые циклы между сабсистемами** после слияния не ломают компиляцию (одна сборка), но логически нежелательны — фиксируем как долг, не блокер.
- **Перенос TMP** — единственный шаг с риском путей; вынесен в конец и опционален.

## 11. Вне рамок

- Namespace'ы (`PromeonLab.*`) — не вводим (D3).
- Перенос `XR/`, `XRI/`, `CompositionLayers/` — не делаем (D6).
- Рефакторинг логики сабсистем, изменение поведения — не входит; миграция чисто структурная.
