# PromeonLab — Project Status
_Составлено: 2026-05-16_

---

## Что реализовано

### Phase 1: Foundation ✅
| Артефакт | Статус |
|---|---|
| 18 `.asmdef` файлов (все 13 подсистем + `_Shared` + `_App` + Editor + тесты) | ✅ |
| `EventBus.cs` — per-scope event bus | ✅ |
| `AppEvents.cs` — все cross-subsystem event structs | ✅ |
| `AppMode.cs`, `ErrorLevel.cs`, `PanelId.cs` | ✅ |
| `RootLifetimeScope.cs`, `MainMenuSceneScope.cs`, `VrEditingSceneScope.cs` | ✅ |
| `ArMappingSceneScope.cs`, `ArPreviewSceneScope.cs` | ✅ (сверх плана) |
| `AppBootstrap.cs`, `PlayerSpawnApplier.cs`, `PlayerSpawnAnchor.cs` | ✅ |
| Сцены: `Bootstrap.unity`, `MainMenu.unity`, `VrEditing.unity` | ✅ |
| Сцены: `ArMapping.unity`, `ArPreview.unity`, `Sandbox.unity` | ✅ (сверх плана) |

### Phase 2: SpatialUi + ModeOrchestrator ✅
| Артефакт | Статус |
|---|---|
| `ModeOrchestrator.cs` + `ModeTransitionGraph.cs` (SO) | ✅ |
| `SpatialPanel.cs` — body-lock, billboard, SetVisible | ✅ |
| `UiPanelManager.cs` — спавн панелей, подписка на ModeChangedEvent | ✅ |
| `ToolbarPanel.cs` — кнопки открытия панелей | ✅ |
| `PanelRegistry.cs` (SO), `PanelType.cs` | ✅ |
| `MainMenuPanel.cs` — кнопка перехода в VrEditing | ✅ |
| `UserPanel.cs` — контекстное меню (A-кнопка контроллера) | ✅ (сверх плана) |
| `UserPanelOpener.cs`, `PanelDragHandle.cs` | ✅ (сверх плана) |
| `UserPanel_ContextMenu_VrEditing/ArMapping/Sandbox.cs` | ✅ (сверх плана) |

### Phase 3: StorageCore + MainMenu ✅
| Артефакт | Статус |
|---|---|
| `PathProvider.cs` + `PathProviderTests.cs` (3 теста) | ✅ |
| `SceneData.cs`, `SceneSerializer.cs` + `SceneSerializerTests.cs` (2 теста) | ✅ |
| `AppStorage.cs` — async create/load/save/delete + GetAllScenesAsync + BeginSandboxSession | ✅ |
| `UnsavedChangesGuard.cs` — dirty flag через события | ✅ |
| `ScenePickerPanel.cs`, `SceneItem.cs` — список сцен, создание/открытие | ✅ |

### Phase 3b: MainMenu UI Refactor ✅
| Артефакт | Статус |
|---|---|
| `SceneSelectedEvent` в AppEvents.cs | ✅ |
| `SettingsModule.cs` | ✅ |
| Разделение на ScenePickerPanel + MainMenuPanel | ✅ |

### Phase 4: AssetBrowser + Model Loading ✅
| Артефакт | Статус |
|---|---|
| `AssetImporter.cs` — импорт через DemoAssetCatalog | ✅ |
| `DemoAssetCatalog.cs` (SO), `AssetCatalogData.cs` | ✅ |
| `AssetType.cs`, `AssetEntry.cs` | ✅ |
| `BuiltinLabAsset.cs`, `ImportedLabAsset.cs`, `SavedLabAsset.cs` | ✅ |
| `BuiltinAssetLibrary.cs`, `ImportedAssetLibrary.cs`, `SavedAssetLibrary.cs` | ✅ |
| `AssetBrowserModule.cs`, `LabAssetCard.cs`, `AssetPropertiesView.cs` | ✅ |
| `ILabAsset.cs`, `IAssetLibrary.cs` | ✅ |

### Phase 5: SceneComposition + VrInteraction ✅
| Артефакт | Статус |
|---|---|
| `SceneNode.cs`, `SceneGraph.cs`, `SelectionManager.cs` | ✅ |
| `ISceneGraph.cs`, `ISelectionManager.cs` | ✅ |
| `ICommand.cs`, `CommandStack.cs`, `TransformCommand.cs` | ✅ |
| `CommandStackTests.cs` | ✅ |
| `PropertyPanel.cs` — отображение transform | ✅ |
| `SelectionInteractor.cs`, `SelectionInteractorFactory.cs` | ✅ |
| `GizmoController.cs`, `GizmoHandle.cs` | ✅ |
| `UndoKeyHandler.cs` — Ctrl+Z | ✅ |
| `IInteractableFactory.cs` | ✅ |

### Phase 6: RigBuilder ✅
| Артефакт | Статус |
|---|---|
| `BoneRecord.cs`, `IkChainRecord.cs`, `RigDefinition.cs` | ✅ |
| `RigSerializer.cs` | ✅ |
| `RigRuntime.cs` — применение RigDefinition к SkinnedMeshRenderer | ✅ |
| `BoneProxy.cs` — выбираемая сфера-кость | ✅ |
| `BoneInspectorPanel.cs`, `IkSetupWizard.cs` | ✅ |
| `IRigRuntime.cs` | ✅ |

---

## Что НЕ реализовано

### Phase 7: Animation Authoring + Playback ❌
Все файлы — заглушки (`// Placeholder`). Ни один класс из плана не написан.

| Нужно создать | Описание |
|---|---|
| `Keyframe.cs`, `AnimTrack.cs`, `ActionData.cs` | Структуры данных анимации |
| `AnimationClock.cs` | `ITickable`, продвигает кадр, публикует `FrameChangedEvent` |
| `TrackRecorder.cs` | Записывает transform кости в `ActionData` по кадрам |
| `AnimationEvaluator.cs` | Интерполирует `ActionData` по кадру |
| `PropertyApplicator.cs` | Применяет оценённые значения к `Transform` кости |
| `PlaybackController.cs` | Play/Pause/Stop, управление `AnimationClock` |
| `KeyframeEditorPanel.cs` | UI: timeline, кнопка "Set Key", transport |
| `AnimationEvaluatorTests.cs` | Тесты интерполяции |
| Регистрация `AnimationClock` в `RootLifetimeScope` | DI |
| Транспортные кнопки в `ToolbarPanel.cs` | Play/Pause/Stop в UI |

### Phase 8: Integration + Stubs ❌
Все файлы — заглушки. Ни один класс из плана не написан.

| Нужно создать | Описание |
|---|---|
| `ErrorRecord.cs` | Структура записи об ошибке |
| `ErrorDispatcher.cs` | Роутинг `ErrorOccurredEvent` → UI toast + Console |
| `ToastNotification.cs` | Pooled UI-элемент всплывающего уведомления |
| `ComingSoonPanel.cs` | Многоразовая заглушка-панель (AR, Export) |
| Обёртка async в `AppStorage.cs` | try-catch → ErrorDispatcher |
| Обёртка async в `AssetImporter.cs` | try-catch → ErrorDispatcher |
| Регистрация `ErrorDispatcher` в `RootLifetimeScope` | DI |

---

## Статус Unity Editor (не верифицировано кодом)

Сцены существуют на диске, но проверить их внутренний setup можно только в редакторе:

| Задача | Неизвестный статус |
|---|---|
| XR Origin + XR Ray Interactor (L/R) в `VrEditing.unity` | ? |
| XR UI Input Module на EventSystem | ? |
| XR Device Simulator prefab в сцене | ? |
| Interaction layers: SceneObjects (8), UiPanels (9), GizmoHandles (10), BoneProxies (11) | ? |
| `PanelRegistry.asset` создан и заполнен | ? |
| `DemoAssetCatalog.asset` создан и заполнен | ? |
| `DefaultModeTransitionGraph.asset` создан | ? |
| Prefabs: ToolbarPanel, UserPanel, BoneProxy | ? |
| FBX с скелетом в `Assets/_App/DemoAssets/` | ? |
| Bootstrap.unity запускает MainMenu additively | ? |

---

## Итог

| Phase | C# код | Unity Editor |
|---|---|---|
| 1 — Foundation | ✅ Готово | Частично (сцены есть) |
| 2 — SpatialUi + ModeOrchestrator | ✅ Готово | Неизвестно |
| 3 — StorageCore + MainMenu | ✅ Готово | Неизвестно |
| 3b — MainMenu UI Refactor | ✅ Готово | Неизвестно |
| 4 — AssetBrowser | ✅ Готово | Неизвестно |
| 5 — SceneComposition + VrInteraction | ✅ Готово | Неизвестно |
| 6 — RigBuilder | ✅ Готово | Неизвестно |
| **7 — Animation** | ❌ Стаб | ❌ |
| **8 — Integration / Errors** | ❌ Стаб | ❌ |
