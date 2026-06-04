# Чеклист UI: все панели и интерфейсные скрипты → где описываем

Статус: создан 2026-06-04 по запросу Макса. Сверен с кодом Assets/_App/Scripts (SpatialUi полностью + GizmoToolsPanel из VrInteraction).
Отмечать [x] по мере принятия пунктов, в скобках – место описания.

## Инфраструктура интерфейса (3.2.2, перепланируется)

- [ ] SpatialPanel.cs – базовая панель, режимы крепления, биллборд, ленивое следование (3.2.2, Листинг 3.18 + Б)
- [ ] PanelRegionRouter.cs – маршрутизатор регионов (3.2.2, листинг Open/Close/Toggle + Б)
- [ ] NavBarConfig.cs (+ IRegionConfig) – конфигурация регионов и видимости по режимам (3.2.2, словами – решено)
- [ ] RegionMember.cs (+ IRegionSurface) – поверхность модуля, делегирование собственной реализации (3.2.2, Листинг 3.22 целиком – решено)
- [ ] RegionNavButton.cs – кнопка навигации; ленивая настройка из-за неактивного хоста (3.2.2, словами)
- [ ] Регистрация по регионам – в 3.2.2 кратко словами; листинги сценных областей (MainMenuSceneScope, VrEditingSceneScope целиком; Sandbox – Б.4) перенесены в 3.2.1 (решено)
- [ ] UserPanel.cs – носитель навигации и модулей; следование, фиксация, размер (3.2.2, листинг + Б)
- [ ] PanelGrabHandle.cs – ручка перемещения (3.2.2 словами + Б)
- [ ] UserPanelOpener.cs – вызов панели кнопкой контроллера (3.2.2, словами)
- [ ] VrKeyboard.cs (+ KeyboardFocusEvent) – экранная клавиатура (3.2.2, кратко)
- [x] HeadFade.cs – затемнение при переходах (описан в 3.2.1 при SceneTransitionRunner)

## Панели приложения (функциональные пункты)

- [ ] MainMenuPanel.cs (3.2.3)
- [ ] ScenePickerPanel.cs + SceneListNode_Item.cs (3.2.3)
- [ ] OutlinerPanel.cs + OutlinerNode_Item.cs, OutlinerNode_Rig_Item.cs (3.2.3)
- [ ] AssetBrowserPanel.cs + LabAsset_Item.cs (3.2.4)
- [ ] ImportWizardPanel.cs – кастомная поверхность IRegionSurface: роль в 3.2.2, содержание мастера в 3.2.4
- [ ] FileBrowserPanel.cs – кастомная поверхность, обёртка стороннего SimpleFileBrowser: роль в 3.2.2, конвейер в 3.2.4
- [ ] InspectorPanel.cs (3.2.6)
- [ ] PropertyPanel.cs (3.2.6)
- [ ] GizmoToolsPanel.cs (VrInteraction/Gizmo) – в перечне 3.2.2; в 3.2.6 коротко (решено)
- [ ] SettingsPanel.cs + BindingSection_Item.cs, BindingSectionRow_Item.cs + ControlsProfile/ControlBinding (InputBindings) – кратко в 3.2.2, абзац у VrKeyboard (решено)
- [ ] AnimatorPanel.cs + AnimatorToolbarView, AnimatorTransportView, AnimatorRulerView, AnimatorPlayheadView, AnimatorEmptyStateView + TimelineRow_Item.cs + TimelineScrubInput.cs + AnimatorPanelConfig.cs (3.2.7)
- [ ] ExportPanel.cs (3.2.8)

## Не описываем (обоснование)

- PanelId.cs / Init(PanelId, …) – рудимент, по коду не используется содержательно
- Elements/*_Item – отдельных пунктов не получают, упоминаются при своих панелях (подтверждено Максом)
