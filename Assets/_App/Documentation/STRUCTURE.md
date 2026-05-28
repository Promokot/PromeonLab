# PromeonLab — File Structure (Assets/)

> Generated 2026-05-25. Unity 6000.3.7f1. GUIDs are Unity's stable asset IDs from .meta files.

## Top-level Layout

Каждая папка верхнего уровня под `Assets/` — отдельная зона ответственности:

- **`_App/`** — весь проектный код и контент PromeonLab (бутстрап, сабсистемы, сцены, _Shared контракты). Только сюда пишет команда.
- **`_Recovery/`** — авто-сохранённые сцены Unity Editor (recovery autosave); не используется в рантайме.
- **`_Shared/`** (внутри `_App/`) — кросс-сабсистемные контракты: Events / Interfaces / Models / Data; единственный канал общения между Subsystems.
- **`CompositionLayers/`** — настройки XR Composition Layers (Meta/OpenXR overlays).
- **`InputSystem_Actions.inputactions`** — глобальный inputactions-ассет Unity (заготовка из шаблона; не используется напрямую — реальные биндинги в `_App/Subsystems/InputBindings/`).
- **`Plugins/`** — отсутствует (третьесторонний код лежит в `UnityPacks/`).
- **`Readme.asset`** — TutorialInfo ScriptableObject (welcome-экран Unity template).
- **`Resources/`** — материалы, текстуры, префабы и FBX, нужные через `Resources.Load` (минимизируется per project rules; держится для гизмо, чекер-полов, рига-плейсхолдеров).
- **`Samples/`** — импортированные сэмплы XR Interaction Toolkit 3.0.7 (Starter Assets + XR Device Simulator).
- **`Screenshots/`** — отладочные скриншоты Unity (in-game capture).
- **`Settings/`** — URP renderers и pipeline assets (Mobile / PC варианты + GlobalSettings + volume profile).
- **`TextMesh Pro/`** — стандартный TMP пакет (Resources/Fonts/Examples/Shaders).
- **`TutorialInfo/`** — наследие URP-шаблона (Readme editor + иконка); планируется удаление.
- **`UnityPacks/`** — третьесторонние asset-паки (ColorSkies, Downtown Game Studio nature, HouseInteriorPack, Keyboard Package, QuickOutline, SimpleFileBrowser).
- **`XR/`** — XR Plug-in Management настройки (OpenXR + XR Simulation loaders).
- **`XRI/`** — XR Interaction Toolkit project settings (Interaction Layers + Device Simulator settings).

## Tree

```
Assets/
├── _App/                                                       [folder]
│   ├── Bootstrap/                                              [folder]
│   │   ├── AppBootstrap.cs                                     guid: e9a1ac45b7cefef4d9da76c74419d205
│   │   ├── FallGuard.cs                                        guid: 8bec68205600dbe4b87eb97dc907129a
│   │   ├── MainMenuSceneScope.cs                               guid: ef5c02ab16e9f99458c9632dbd591066
│   │   ├── PlayerSpawnApplier.cs                               guid: d6478f46acf2ef749b5b906d903daf7f
│   │   ├── RootLifetimeScope.cs                                guid: 38061a6cdb528fe4286ee8f5d58cecc7
│   │   ├── SandboxSceneScope.cs                                guid: 7eb2eff948c91c04cb26e7c48c441e6a
│   │   ├── UndoKeyHandler.cs                                   guid: 6c31e675df13a70489e29318e90dbe29
│   │   ├── VrEditingSceneScope.cs                              guid: dbfa4f08b550b7342a0490d2f6182db0
│   │   └── VrInputFieldProxy.cs                                guid: 390a98ae4b96d104d9c0643d6b775315
│   ├── DemoAssets/                                             [folder]
│   │   └── DefaultDemoAssetCatalog.asset                       guid: 7dea85a16edadbd43a3aad3be23c3725
│   ├── Documentation/                                          [folder]
│   │   ├── architecture_context.md                             guid: e1b98b5a6840c8942b185f0f4ecfd81f
│   │   ├── conventions.md                                      guid: 72b772820451572499d7b697841e07e4
│   │   └── coursework_context.md                               guid: 93023a883889b8748874af7a0a1a3f11
│   ├── Editor/                                                 [folder]
│   │   ├── EditorPlaceholder.cs                                guid: bb062182328cee4409451f5d0858db5e
│   │   ├── PromeonLab.Editor.asmdef                            guid: 557d77c1b288ca34bb9a4cc22692c9c4
│   │   ├── PromeonProxyRigBuilderEditor.cs                     guid: 756351fbf45775b438e805073003b73a
│   │   └── RemoveMissingScriptsTool.cs                         guid: bc0bef39e9ffe5e46b33c77cf2c29b9c
│   ├── Scenes/                                                 [folder]
│   │   ├── _Sandbox/                                           [folder]
│   │   │   └── AnimatorPanelSandbox.unity                      guid: e44d6957fb907a047b37322a2d88e195
│   │   ├── Tests/                                              [folder]
│   │   │   ├── Asset_Review.unity                              guid: 7359c49d0f8de674cb8a2f2d198fcf07
│   │   │   ├── MCP_testScene.unity                             guid: bbad6b7b110d9cb4d9aaf4322a9aa5d0
│   │   │   └── Prototyping_UI.unity                            guid: c8865d6aea92ed84ba62578f251019f9
│   │   ├── Bootstrap.unity                                     guid: 239d7576841d1f54b879077a83ba9b6e
│   │   ├── MainMenu.unity                                      guid: bb75d359692959a4db03b42c75644708
│   │   ├── Sandbox.unity                                       guid: c09ef6447eda0fb4483e5b1c90b65202
│   │   └── VrEditing.unity                                     guid: f8765520defca2a499ca9f62cfadb50d
│   ├── _Shared/                                                [folder]
│   │   ├── Data/                                               [folder]
│   │   │   ├── AssetRef.cs                                     guid: f33cbcd1856cb4d40ad3c96cbcc9ff4d
│   │   │   ├── AssetSource.cs                                  guid: d89295080f2c018458f05d37c7850035
│   │   │   └── SelectionVisual.cs                              guid: cc6ec8cda14b64640bc1f51b9966be1c
│   │   ├── Events/                                             [folder]
│   │   │   ├── AnimationContainerChangedEvent.cs               guid: 9edf847c38a79d84da3fb2047a7ea09f
│   │   │   ├── AppEvents.cs                                    guid: b29ddd215ad1cb04696843fa7d4156a3
│   │   │   ├── BonesVisibilityChangedEvent.cs                  guid: 2f078da9caec897438040d8ba69a8282
│   │   │   ├── ContainerChange.cs                              guid: 3ec5621cbe1d7924dba785d424d2b2f8
│   │   │   ├── EventBus.cs                                     guid: 815ba0c98adeede4690f9f13bee8c52c
│   │   │   └── KeyframeChange.cs                               guid: 55afb09fc2668ea47ad4f631d761c9ac
│   │   ├── Interfaces/                                         [folder]
│   │   │   ├── IAssetLibrary.cs                                guid: 1ebedb64f5e5fb8479add69939c9ef8c
│   │   │   ├── IAssetRegistry.cs                               guid: 9a0c1d7dd5c72e641bd7da2cb049adf7
│   │   │   ├── ICommand.cs                                     guid: ae93832da173ef742b57be298d5ef64f
│   │   │   ├── ILabAsset.cs                                    guid: 53f09be466e13944cac28096c37ef474
│   │   │   ├── IRigRuntime.cs                                  guid: 0088f1f3d8449d24a9fff9ae152fda82
│   │   │   ├── ISceneGraph.cs                                  guid: 9ba8d1d3343291a49a9ff1875b23bb59
│   │   │   └── ISelectionManager.cs                            guid: 748a70ee845e2a844a6e601f66c56db9
│   │   ├── Models/                                             [folder]
│   │   │   ├── AppMode.cs                                      guid: 5b9811e1f7bf84045b2a76ffff00853a
│   │   │   ├── AssetEntry.cs                                   guid: 62d7fcfbb2bbad0448c77aa2a3cbba44
│   │   │   ├── AssetType.cs                                    guid: 969e0ac7548bb5546824957783887b08
│   │   │   ├── BoneRecord.cs                                   guid: 042b7657540b91649a0a2de2e8e19c20
│   │   │   ├── BoneSceneNodeMarker.cs                          guid: 3047d4de96c0f734f8dd2a0a2feb3c23
│   │   │   ├── ErrorLevel.cs                                   guid: 42dc82f9cede5184daf2ade2f887f1df
│   │   │   ├── GizmoMode.cs                                    guid: e54e5b55de85afd4696e1b9184d4d2ea
│   │   │   ├── IkChainRecord.cs                                guid: 93322a3c9a46122488f010aba29f6c24
│   │   │   ├── PanelId.cs                                      guid: 392345a3ced13a047a2c3068d19bfe16
│   │   │   └── RigDefinition.cs                                guid: 6bd3a387afbe3b84fbcea74bae173657
│   │   └── _Shared.asmdef                                      guid: 396f4d483abd17a48aa0d639ede24696
│   ├── Subsystems/                                             [folder]
│   │   ├── AnimationAuthoring/                                 [folder]
│   │   │   ├── Data/                                           [folder]
│   │   │   │   ├── ActionContainer.cs                          guid: aa273c3ba9f0ca44e86be622da82957b
│   │   │   │   ├── AnimKeyData.cs                              guid: 86d6af3676c3f1a499843e8622b1fb4c
│   │   │   │   ├── AnimTrackData.cs                            guid: 4e46c43a950c13e48bfe2993621d0198
│   │   │   │   ├── DefaultAnimatorPanelConfig.asset            guid: 3284c4df4c17a264e98f6c9d10d38dd0
│   │   │   │   ├── FrameClipboard.cs                           guid: 68f9ea4e52dd1564287286b1899c8c9f
│   │   │   │   ├── FrameClipboardEntry.cs                      guid: b058612a3ef618e489fdb5ae5854d118
│   │   │   │   └── SceneAnimationData.cs                       guid: af792e358b1039847861521709ad1298
│   │   │   ├── Tests/                                          [folder]
│   │   │   │   ├── ActionContainerTests.cs                     guid: bf89ede4b8b336f43a34ac043cae1190
│   │   │   │   ├── AnimationAuthoringTests.cs                  guid: a068f5082601d864d97b6683833cf71f
│   │   │   │   ├── AnimationClipboardTests.cs                  guid: 081771708ca68924c9702dc856329483
│   │   │   │   ├── AnimationClockTests.cs                      guid: b1952a60c508bdd44bc2b4a3a0c4634e
│   │   │   │   ├── AnimationDataTests.cs                       guid: b8c81af791000084aa7811784eb71c53
│   │   │   │   └── Subsystems.AnimationAuthoring.Tests.asmdef  guid: 2ff03c6434904874db9ce85096eb0026
│   │   │   ├── UI/                                             [folder]
│   │   │   │   └── KeyframeMarker.prefab                       guid: 64966e4161d4dd0448b5751d62a95af2
│   │   │   ├── AnimationAuthoring.cs                           guid: 4960d41eff17cff46bdb71da1669e915
│   │   │   ├── AnimationClipboard.cs                           guid: 7cfc6c5d0e731f44394dbf7d93581530
│   │   │   ├── AnimationClock.cs                               guid: 9b0ffc72675b4d449a8b87913903db58
│   │   │   ├── InternalsVisibleTo.cs                           guid: c92901e589d6be24d81e3a6869412e92
│   │   │   └── Subsystems.AnimationAuthoring.asmdef            guid: 93ac966e37b4cae40bdca2f0fa5c626b
│   │   ├── AnimationPlayback/                                  [folder]
│   │   │   ├── AnimationPlayback.cs                            guid: 3ae415bf85444e145902f1f7d9af063e
│   │   │   └── Subsystems.AnimationPlayback.asmdef             guid: 669c94aeb3f16df4abd9508f0162dc8d
│   │   ├── AssetBrowser/                                       [folder]
│   │   │   ├── Data/                                           [folder]
│   │   │   │   ├── BuiltinLabAsset.cs                          guid: 3f3391c8d0928244697a3ddd52b0228f
│   │   │   │   ├── DefaultBuiltinAssetLibrary.asset            guid: 30918a1e8f7dd9b42b4b376d5cf19bf1
│   │   │   │   ├── DemoAssetCatalog.cs                         guid: 40ebaead2a0bf44448c3030d4a399743
│   │   │   │   ├── ImportedLabAsset.cs                         guid: bead50a4d737dd14691fd9b1c0f2c076
│   │   │   │   ├── NoRigsBuiltinAssetLibrary.asset             guid: 9066455bd219f9e48ac283cc0840d3fa
│   │   │   │   └── SavedLabAsset.cs                            guid: e4d1df69dbbdf054893ee3e8c24e9127
│   │   │   ├── AssetImporter.cs                                guid: 82cf979117aa4fe49bfd99354c88685b
│   │   │   ├── AssetRegistry.cs                                guid: df0de772078a1904e9cb262f9a2ac4a0
│   │   │   ├── AssetSpawner.cs                                 guid: 33427adce3afb4e4fbf5872e88339c42
│   │   │   ├── BuiltinAssetLibrary.cs                          guid: ce326e58cfb9b6241b614b278041b07f
│   │   │   ├── ImportedAssetLibrary.cs                         guid: d53b8aaed105be84db09c15913549ea2
│   │   │   ├── SavedAssetLibrary.cs                            guid: 8774731ae7171884c86b2e400edb1ee8
│   │   │   └── Subsystems.AssetBrowser.asmdef                  guid: dfe945ed1f17f4c448e6866277e621df
│   │   ├── ErrorHandling/                                      [folder]
│   │   │   ├── ErrorHandling.cs                                guid: 74d3789f19dc8fb4e9829f3c1e23d17b
│   │   │   └── Subsystems.ErrorHandling.asmdef                 guid: 0cf76888c4ceb9445a7b161e19fb3638
│   │   ├── ExportPipeline/                                     [folder]
│   │   │   ├── ExportPipeline.cs                               guid: cc6c3051eb3eee945b6bfc2fad2e4c58
│   │   │   └── Subsystems.ExportPipeline.asmdef                guid: 4f164fe8ddbb5f540bfa9f3bf4250bc6
│   │   ├── InputBindings/                                      [folder]
│   │   │   ├── InputBindings.cs                                guid: dbf0b738a4f5bb74ca8c1878b1075740
│   │   │   └── Subsystems.InputBindings.asmdef                 guid: 096f6d9a48e136c44b74b932f7c27447
│   │   ├── ModeOrchestrator/                                   [folder]
│   │   │   ├── Data/                                           [folder]
│   │   │   │   ├── DefaultModeTransitionGraph.asset            guid: 09614598fc34b51479f27ca93cf85d8d
│   │   │   │   └── ModeTransitionGraph.cs                      guid: 7d5fdcc647f89494680a54642ae2ba89
│   │   │   ├── ModeOrchestrator.cs                             guid: a8797fa8a98d7954e8ef017440cc763b
│   │   │   └── Subsystems.ModeOrchestrator.asmdef              guid: f0d13fa2fc6019842b9db83a8e44e8a1
│   │   ├── RigBuilder/                                         [folder]
│   │   │   ├── Data/                                           [folder]
│   │   │   │   ├── BoneRecord.cs                               guid: 31646a22754270b4e829d768a33559c2
│   │   │   │   ├── IkChainRecord.cs                            guid: eb54231e8dcd7db44bcc5849676c2d53
│   │   │   │   └── RigDefinition.cs                            guid: 5b444e81a22da674191bcf1b31110150
│   │   │   ├── Tests/                                          [folder]
│   │   │   │   ├── PromeonProxyRigBuilderTests.cs              guid: 10ba42c25d0ccb248aecc512345f2687
│   │   │   │   └── Subsystems.RigBuilder.Tests.asmdef          guid: 1b317763b5cd30f4e826e4c2a13938a4
│   │   │   ├── BoneFollower.cs                                 guid: cadad6133fd450e4b92f63ce8048d310
│   │   │   ├── BoneProxy.cs                                    guid: 211c053959609584d9ec07caddefdb63
│   │   │   ├── PromeonProxyRigBuilder.cs                       guid: dbc3551a634a9fd4a99307ed5569274c
│   │   │   ├── RigRuntime.cs                                   guid: 2d8086d90a1f24d4fa13d9f414aa25b4
│   │   │   ├── RigSerializer.cs                                guid: 7a80781bf9af79a4c96b7c199d650c20
│   │   │   └── Subsystems.RigBuilder.asmdef                    guid: b99cea2635ec4014da4626f617100e58
│   │   ├── SceneComposition/                                   [folder]
│   │   │   ├── Constraints/                                    [folder]
│   │   │   │   └── ConstraintFreezePosition.cs                 guid: 5341122c0e007a348916ec6bc67f4351
│   │   │   ├── Data/                                           [folder]
│   │   │   │   ├── CommandStack.cs                             guid: 872c889444b57c0438c758cee2a2f279
│   │   │   │   ├── DefaultGizmoConfig.asset                    guid: ba9f00b526ca5664abcc429525a0ce13
│   │   │   │   └── TransformCommand.cs                         guid: b4354c5736018fc47a350db6c6d4a3dd
│   │   │   ├── Tests/                                          [folder]
│   │   │   │   ├── AssetRegistryTests.cs                       guid: 1b320e5de67fad14db4d1540fc2fc223
│   │   │   │   ├── CommandStackTests.cs                        guid: df1f06cdf83c2f74e93cafa98574c483
│   │   │   │   ├── SceneGraphTests.cs                          guid: 9eafb7a12a1b7394a9fc35554e5135fd
│   │   │   │   ├── SceneNodeTests.cs                           guid: bce929ae15b26b444b2d4dd755906f8f
│   │   │   │   ├── SelectionManagerTests.cs                    guid: e43ae707ba5fbec45b03eca2e705ef6c
│   │   │   │   └── Subsystems.SceneComposition.Tests.asmdef    guid: 9fd8eacd03b0ce148b189ca585455204
│   │   │   ├── SceneAutoSaver.cs                               guid: 35bc9bb11eb32b94ab94ea718d2cbf27
│   │   │   ├── SceneGraph.cs                                   guid: 4f21ddf853ff3c643a020a05284a2e5f
│   │   │   ├── SceneNode.cs                                    guid: 1ff5e2a3995ee144fb7aacf9d15a2291
│   │   │   ├── SelectionManager.cs                             guid: d1cfbfd29d264b84ba63f49f0741a2dc
│   │   │   └── Subsystems.SceneComposition.asmdef              guid: 636a52eae58768d4e8c738fa8f3ec47f
│   │   ├── SpatialUi/                                          [folder]
│   │   │   ├── Data/                                           [folder]
│   │   │   │   ├── AnimatorPanelConfig.asset                   guid: 4b710848b9de3b74b97536367c823ac8
│   │   │   │   ├── AnimatorPanelConfig.cs                      guid: 66d582c477a43584ea2a0320d6e1d73a
│   │   │   │   ├── DefaultNavBarConfig.asset                   guid: 44b184438768cae4e81d3a22c376d5b0
│   │   │   │   ├── DefaultPanelRegistry.asset                  guid: a887d01d6cbad31468655994aa5adb01
│   │   │   │   ├── NavBarConfig.cs                             guid: 26220492e3df09a4fbc0391e46393287
│   │   │   │   ├── PanelRegistry.cs                            guid: cf920f1c0606a6c4ca8cb6082d5abf0f
│   │   │   │   └── PanelType.cs                                guid: 1240cf74fb6bc404f934d61505f0e918
│   │   │   ├── Editor/                                         [folder]
│   │   │   │   ├── AnimatorPanelModuleBuilder.cs               guid: 4876f6e73dba9f9468284b396dbec8e3
│   │   │   │   └── Subsystems.SpatialUi.Editor.asmdef          guid: 12f9a498bf7f28d4f81d64699f3eb631
│   │   │   ├── Prefabs/                                        [folder]
│   │   │   │   ├── Items/                                      [folder]
│   │   │   │   │   ├── LabAssetCard_ItemUI.prefab              guid: 80c03fb1f6aeabf488069fed67014e25
│   │   │   │   │   ├── OutlinerObject-Object_ItemUI.prefab     guid: c6e3f45b8da76744bb23ba4ac6e10be3
│   │   │   │   │   ├── OutlinerObject-Rig_ItemUI.prefab        guid: c0f7f57a816073f43866eb0f13d67c20
│   │   │   │   │   ├── ScenePrefab_ItemUI.prefab               guid: a5c0f55c3a3bcf148bb9d57439674fc0
│   │   │   │   │   ├── TimelineKeyDiamond.prefab               guid: df3a050a1a9a74b4c891cdbc999e9eb3
│   │   │   │   │   ├── TimelineLane.prefab                     guid: 3e952b66d4228ef46aad35972f3a9fa6
│   │   │   │   │   ├── TimelineTick.prefab                     guid: 69faedcd0469c8c46be87769762bfd0b
│   │   │   │   │   ├── TimelineTickLabel.prefab                guid: 51b8a14a41745434f802f9d92021cf68
│   │   │   │   │   ├── TrackRow.prefab                         guid: c8b855ca1e7ed93439a4218d344977fc
│   │   │   │   │   └── UserPanelButton-PrefDefault.prefab      guid: 969498e96da1df2478c0caf0b6f71621
│   │   │   │   └── Panels/                                     [folder]
│   │   │   │       ├── Static/                                 [folder]
│   │   │   │       │   ├── MainMenu_CombinedPanel.prefab       guid: 3084e0367e36b3f498e58f80a6cb9009
│   │   │   │       │   ├── MainMenuPanel.prefab                guid: 1748de82e8e4dad44a497dc115ab0810
│   │   │   │       │   └── ScenePickerPanel.prefab             guid: 3479f4fc3df8e334fb1aa5a2af415a4a
│   │   │   │       └── UserPanel/                              [folder]
│   │   │   │           ├── AnimatorPanelModule.prefab          guid: 2cd966e584e7bdb4e9bbb92e38d27723
│   │   │   │           ├── AssetBrowserModule.prefab           guid: 5b2b86e231a088147b42ee139a4f0754
│   │   │   │           ├── ContextMenu_VrEditing.prefab        guid: f68fa9ed508520a49ace72d5529f1387
│   │   │   │           ├── GizmoToolsModule.prefab             guid: ae551cf484ed61044bc3169254ed5c02
│   │   │   │           ├── RiggingToolsModule.prefab           guid: e0f2d36a057dbce4ea653c7f0bb58e0b
│   │   │   │           ├── SceneInspectorModule.prefab         guid: 9d0d3e50e78e1a7449378a37fd2a3f77
│   │   │   │           ├── SceneOutlinerModule.prefab          guid: e644d997e0e127045b948d117e98cdda
│   │   │   │           ├── SettingsModule.prefab               guid: 51255f67c47e0e541a54131bd5e05b77
│   │   │   │           └── UserPanel.prefab                    guid: 7a4de75d919ab50449b093180517b28c
│   │   │   ├── Scripts/                                        [folder]
│   │   │   │   ├── Elements/                                   [folder]
│   │   │   │   │   ├── DetachablePanelDragHandle.cs            guid: a1e780ab1973ed44c9bc41061d0bffcb
│   │   │   │   │   ├── FileBrowserVrAnchor.cs                  guid: 836b07c64d2f3e344b292833c497410e
│   │   │   │   │   ├── LabAssetCard.cs                         guid: 4b9c40f083e1a6a49b85653c61a88d11
│   │   │   │   │   ├── OutlinerItem.cs                         guid: 62e362646d084824ea009a6d28ef024f
│   │   │   │   │   ├── PanelDragHandle.cs                      guid: a5609cfa59c196c468325078a9d4fec7
│   │   │   │   │   ├── RigOutlinerItem.cs                      guid: 363bd4b4b93fa654aae4c1afeab6bb10
│   │   │   │   │   ├── SceneItem.cs                            guid: ac9032e3adb9d444cb3e3f12e351725d
│   │   │   │   │   ├── SceneOutlinerRow.cs                     guid: ec17370db9115c845b0d386e1b3690ef
│   │   │   │   │   ├── TimelineInputHandler.cs                 guid: 6821c0c9b58a55a48b17f30bb023565a
│   │   │   │   │   ├── TimelineLaneView.cs                     guid: d74e928a2650d8d4282232c750c3cac9
│   │   │   │   │   ├── TimelineLanesView.cs                    guid: 6ab02c585c46a1445bb3eccf8791f30a
│   │   │   │   │   ├── TimelinePlayheadView.cs                 guid: 36f8446d4a980ce48a1a60aa18bf09bf
│   │   │   │   │   ├── TimelineRulerView.cs                    guid: 191a846be38bba04898e4d67b64e0ac0
│   │   │   │   │   ├── TimelineScrollSync.cs                   guid: 13498021c20736449b630c93cf27d6e2
│   │   │   │   │   ├── TrackRowView.cs                         guid: c58d059aa19d4de4cab74eba17a2609b
│   │   │   │   │   ├── UserPanelKeyboardToggle.cs              guid: 98e49eb3c68a32745b9d9a776ff43a25
│   │   │   │   │   ├── UserPanelOpener.cs                      guid: 8ba698ec7bba2244d8db895175e1e943
│   │   │   │   │   └── VrKeyboard.cs                           guid: 871402bd97631a347bd500b9379b163d
│   │   │   │   ├── Panels/                                     [folder]
│   │   │   │   │   ├── AssetBrowserModule.cs                   guid: 0abd5dc0c61e78848963e4e9ef7491a4
│   │   │   │   │   ├── BoneInspectorPanel.cs                   guid: 10f9c3b833735084fa42f947b0cd842c
│   │   │   │   │   ├── DetachablePanel.cs                      guid: 6193c7e43078b874b8405770030f7ab4
│   │   │   │   │   ├── IkSetupWizard.cs                        guid: a5b5ff4e63bf30248884449bbb45c36e
│   │   │   │   │   ├── MainMenuPanel.cs                        guid: f5687a2f7bdc1324c8a7c1d4d44da85a
│   │   │   │   │   ├── PropertyPanel.cs                        guid: ed43dc1ecaf4c404781afaf7f2165661
│   │   │   │   │   ├── ScenePickerPanel.cs                     guid: 68c7f368e420abb49bf255738d2389d9
│   │   │   │   │   ├── SettingsModule.cs                       guid: ba4bf5d630b591d4d95f99d75b5e8715
│   │   │   │   │   ├── SpatialPanel.cs                         guid: 8a288fd3925a9b34f9554b3a8a16e4f6
│   │   │   │   │   ├── UiPanelManager.cs                       guid: 64acb12f6e3705047b877770378f6700
│   │   │   │   │   └── UserPanel.cs                            guid: a855310e980355d40bcdda591c684265
│   │   │   │   └── Views/                                      [folder]
│   │   │   │       ├── AnimatorEmptyStateView.cs               guid: 7c7b69f82f4426e4a876d38b97e6d31a
│   │   │   │       ├── AnimatorPanelView.cs                    guid: 57c5daa7c511dc24db7cd9e65a568565
│   │   │   │       ├── AnimatorToolbarView.cs                  guid: 69dc5e20bc84d9c4ba34197875ab5542
│   │   │   │       ├── AnimatorTransportView.cs                guid: bcc42c56dcff8514b96c94e78f902177
│   │   │   │       ├── AssetPropertiesView.cs                  guid: f0ac83e780bb5aa41aa5a33c2cdc246e
│   │   │   │       ├── SceneInspectorView.cs                   guid: c80757e098dae5048a35a538cf3b7e97
│   │   │   │       └── SceneOutlinerView.cs                    guid: 6f91ee46bdd1c2f41bac3aeaacb9336f
│   │   │   └── Subsystems.SpatialUi.asmdef                     guid: 35405651ebe33dc4d8e38025f6c0a20a
│   │   ├── StorageCore/                                        [folder]
│   │   │   ├── Data/                                           [folder]
│   │   │   │   ├── AssetCatalogData.cs                         guid: 09f86f428dd8a4e4a99e55b84aa7b969
│   │   │   │   ├── NodeData.cs                                 guid: d09a5143da2b03543bc88bb6f651250b
│   │   │   │   └── SceneData.cs                                guid: a1c958f586eb650449c3570aafacd561
│   │   │   ├── Tests/                                          [folder]
│   │   │   │   ├── PathProviderTests.cs                        guid: a388cbb4e0ec932409695e01997f2233
│   │   │   │   ├── SceneSerializerTests.cs                     guid: 2cf05bd39b696b744af9de9374e1bb5c
│   │   │   │   └── Subsystems.StorageCore.Tests.asmdef         guid: ddbd28fdd2b22da4fb5984dcf74117c1
│   │   │   ├── AppStorage.cs                                   guid: 423cc1e12f884ae47a135ea3889ecff3
│   │   │   ├── PathProvider.cs                                 guid: b511fedbed5cba848b0d637327ebd3fd
│   │   │   ├── SceneSerializer.cs                              guid: 50be47b7dec7b174abb7df96b0a72c6e
│   │   │   ├── Subsystems.StorageCore.asmdef                   guid: 3258b41e5baca2a4eb21ae76225c513d
│   │   │   └── UnsavedChangesGuard.cs                          guid: 2984408ea8f2c024c86de9e4db321735
│   │   └── VrInteraction/                                      [folder]
│   │       ├── Gizmo/                                          [folder]
│   │       │   ├── Strategies/                                 [folder]
│   │       │   │   ├── AxisMoveStrategy.cs                     guid: 042fcd9bed0b5574d9c852741ab2c6f5
│   │       │   │   ├── AxisScaleStrategy.cs                    guid: 8b3f661e26776b641b3c0e58f66f24fb
│   │       │   │   ├── IGizmoDragStrategy.cs                   guid: e8aebadb630f39e4b85deed3355b7a00
│   │       │   │   ├── RingRotateStrategy.cs                   guid: 22bca4a09bd92c044817ad937b7199d9
│   │       │   │   └── UniformScaleStrategy.cs                 guid: ebd9902c75c34f2468dc708399f1fbe2
│   │       │   ├── UI/                                         [folder]
│   │       │   │   └── GizmoToolsPanel.cs                      guid: 8efef404c8d86bb4798ed9b30659f90d
│   │       │   ├── AxisKind.cs                                 guid: c1230461ca96c5041b7b8354da57de94
│   │       │   ├── BoundsFitter.cs                             guid: 474b8b64483ffe34baa368936f7e3a48
│   │       │   ├── GizmoActivator.cs                           guid: e83edfad4fa2c9243a50fe5463e0008b
│   │       │   ├── GizmoConfig.cs                              guid: 2e62216b1a650b64baf739856b7be370
│   │       │   ├── GizmoHandle.cs                              guid: 9a1069a0fb2380b43bdd6030746b9567
│   │       │   ├── GizmoHierarchy.cs                           guid: 98ac50b55478df64b9a5c254def9e8fe
│   │       │   └── HandleKind.cs                               guid: 8e3070e96f5240c479fd38b7ddfbb0b0
│   │       ├── Tests/                                          [folder]
│   │       │   ├── AxisMoveStrategyTests.cs                    guid: b95f5548048fe1c48b5cac862d79643c
│   │       │   ├── AxisScaleStrategyTests.cs                   guid: 5de9063695d451946aed610921376004
│   │       │   ├── BoundsFitterTests.cs                        guid: 84ada184e2d7ab04795553332aa212e7
│   │       │   ├── GizmoActivatorStateTests.cs                 guid: 8d87cd03b894f574d8f1f039750d430f
│   │       │   ├── RingRotateStrategyTests.cs                  guid: b120d9bbe93e0a3469930df96a96a2c0
│   │       │   ├── Subsystems.VrInteraction.Tests.asmdef       guid: 45ed237e3af75c44e99d6ad4743180f6
│   │       │   └── UniformScaleStrategyTests.cs                guid: 6a5701d68217c7e4f896b9a2416cdf43
│   │       ├── GizmoController.cs                              guid: fbe20144813e5c648a087534bace655b
│   │       ├── IDragStrategy.cs                                guid: 0e52b0afeef3466499a4b0b23165d935
│   │       ├── Selectable.cs                                   guid: c60405ee75790034ca99a3a05d7693fb
│   │       ├── SelectionVisualSync.cs                          guid: 9fc302f6356ab6d43ad32499533d27c9
│   │       ├── Subsystems.VrInteraction.asmdef                 guid: 525e62a8fb3e63b418039cc755976878
│   │       ├── WorldClickCatcher.cs                            guid: 97a3566fdb728da4bae1cdeed58741a5
│   │       └── XRPromeonInteractable.cs                        guid: 5233041f40801be40ad483e06fd8997a
│   └── _App.asmdef                                             guid: 6911e07198aee944ca28467c08335ec5
│
├── _Recovery/                                                  [folder]
│   └── 0.unity                                                 guid: d91771ba1b64af949a818ea63163506d
│
├── CompositionLayers/                                          [folder]
│   └── UserSettings/                                           [folder]
│       ├── Resources/                                          [folder]
│       │   └── CompositionLayersRuntimeSettings.asset          guid: 87edeb8d4d3c4c548ab9e4dff75a0046
│       └── CompositionLayersPreferences.asset                  guid: 471eeb3b2b46a6540b314c0a231a050f
│
├── Resources/                                                  [folder]
│   ├── Materials/                                              [folder]
│   │   ├── Shaders/                                            [folder]
│   │   │   └── triplanarSpecific/                              [folder]
│   │   │       ├── CheckerBase.png                             guid: b5137947a0853794d9ebca68f99c71d1
│   │   │       ├── TriplanarBase_000.mat                       guid: 9b253910522e3f441b4381384c9b1ecb
│   │   │       └── URP_TriplanarSimplified_Promokot.shadergraph  guid: 4a889b19e2c6cd44e8d01df0079b6536
│   │   ├── Simple/                                             [folder]
│   │   │   ├── MainMenuPanel-Bg.mat                            guid: 6acf5e1709c8796419dfa14ce55b364a
│   │   │   ├── WhiteUnlit_Blue.mat                             guid: a11ba53949c07ca4dab589c2c1730285
│   │   │   ├── WhiteUnlit_Green.mat                            guid: eb02c039287fa1441b84885d523c0a2d
│   │   │   ├── WhiteUnlit_Red.mat                              guid: dbf4408651841924e9cdbfe32920b0de
│   │   │   └── WhiteUnlit_Yellow.mat                           guid: 489835ac77ddff14da2e68e6e0afb77e
│   │   ├── CheckerFloor_Blue.mat                               guid: 10c5df94ab3d4ac40b05af2a0eeb2c71
│   │   ├── CheckerFloor_Neutral.mat                            guid: 3825dc5f66b1b0c4c9a18999f76b9ee2
│   │   ├── CheckerFloor_Neutralediting.mat                     guid: d420ed6c5cb72b7449daf94dca5dcefa
│   │   ├── CheckerFloor_Tests.mat                              guid: bc6c32bcfb6abdf4c90f7f13178b0f2d
│   │   ├── crush_dummy_UE4.mat                                 guid: b2591250e8334c349a9b203a4f5597d3
│   │   ├── crush_dummy_UE4_red.mat                             guid: f8bdbbaaacb540d428e050cc0aadf208
│   │   ├── NoSignal_Material.mat                               guid: f51ef1add376d624a89069fa6c3eadcf
│   │   └── PromeonBoneRenderer_Material.mat                    guid: 7fd11f4cd3a7c4a4390f8a4bf270a1a1
│   ├── Models/                                                 [folder]
│   │   ├── Characters/                                         [folder]
│   │   │   └── crush_dummy_UE4_skinned.fbx                     guid: 2ce9efd0ef59bff42980bbdbd1333667
│   │   └── Gizmos/                                             [folder]
│   │       ├── Gizmo_Blue.mat                                  guid: 05c0a0dcc73aee44e83a758cc647723e
│   │       ├── Gizmo_Default.mat                               guid: 847da427eac4fa1418280e21f62ba163
│   │       ├── Gizmo_Green.mat                                 guid: 623824339101fbf4aa8353db2f49b4ee
│   │       ├── Gizmo_Move.fbx                                  guid: 80ea28f1ca96a854ba77a23f39195ddf
│   │       ├── Gizmo_Red.mat                                   guid: ba690d80b2f5b114f818517f73354940
│   │       ├── Gizmo_Rotate.fbx                                guid: 5a933a6e121e6c44ebf4764ebff5e40c
│   │       └── Gizmo_Scale.fbx                                 guid: e7c4cc772bfe419499c6d9f249120124
│   ├── Prefabs/                                                [folder]
│   │   ├── _User/                                              [folder]
│   │   │   ├── EventSystem.prefab                              guid: 305e92b11ce3639458691bc2e82da4a8
│   │   │   └── User XR Origin (XR Rig).prefab                  guid: 8f514251caced8e4c81aa66f85ab0430
│   │   ├── AssetLibraryPrefabs/                                [folder]
│   │   │   └── BuiltinLab_ObjectPrefabs/                       [folder]
│   │   │       ├── icons/                                      [folder]
│   │   │       │   ├── icnon_crashDummy.png                    guid: a5e778c81bf5fc14697bca0b5685d5f9
│   │   │       │   ├── icnon_crashDummy2.png                   guid: be3845e7a648c114aa3fd99d70ccae19
│   │   │       │   ├── icon_(Prb)CoffeTable.png                guid: 7b44e63be4ca308418f6de1e37336057
│   │   │       │   ├── icon_(Prb)Drawer1.png                   guid: 4679f7b7343bdbf46822ce9537199054
│   │   │       │   ├── icon_(Prb)Storage2.png                  guid: 6456abc86c4b64c4ca478b0f17051174
│   │   │       │   ├── icon_(Prb)Toilet.png                    guid: 1ba136227da9cbf428376981c1a70d00
│   │   │       │   ├── Plants1.png                             guid: 78a9d1d6deb88ae43a8d283b04b8438f
│   │   │       │   ├── Plants2.png                             guid: 776d7d4ac9521174286c8a97d3e7d913
│   │   │       │   ├── Plants3.png                             guid: 803a266a4c9fe194cb5d3663b105c5a6
│   │   │       │   ├── PlantsTree1.png                         guid: 2cdf35881aa401144993c3630ce05980
│   │   │       │   ├── PlantsTree2.png                         guid: 4698d9d355c3cf7498a28d9f17d3d788
│   │   │       │   └── PlantsTree3.png                         guid: f6b8448c363479c48bc5de1b66f8b023
│   │   │       ├── (Prb)CoffeTable.prefab                      guid: b235c805f008f6f4da469f7364846b10
│   │   │       ├── (Prb)Drawer1.prefab                         guid: 676edecf7cf6d7645a15b3550dce3e70
│   │   │       ├── (Prb)Storage2.prefab                        guid: 6f78a21f077ae8f4f85a5d7128a8056d
│   │   │       ├── (Prb)Toilet.prefab                          guid: 5e1b8b1cf115f2940b01346027776031
│   │   │       ├── Crush Dummy.prefab                          guid: 54e8cd525b578914d912cca117aa6a2a
│   │   │       ├── Potted Plant 1.prefab                       guid: e9ff77abae7e263459c87f573b09929d
│   │   │       ├── Potted Plant 2.prefab                       guid: 33a55beaafdc05442aced67b7919bf5f
│   │   │       ├── Potted Plant 3.prefab                       guid: 2edfb40d4bae84a4888593afa512daac
│   │   │       ├── Street Tree 1.prefab                        guid: fc8b90f36ca2c874580c01f6ce44fae5
│   │   │       ├── Street Tree 2.prefab                        guid: 6a7b58c713998dc4daeccdabd58c23a3
│   │   │       └── Street Tree 3.prefab                        guid: 7c378151d3dbfbc42ad905926a37e692
│   │   ├── Environment/                                        [folder]
│   │   │   └── FloorDefault.prefab                             guid: 5d18e28859ba844479d16f3c5e3e035c
│   │   └── Gizmos/                                             [folder]
│   │       ├── SceneOriginGizmo.prefab                         guid: 5e4c1d65babee4a44ba7e467b23d6495
│   │       └── Vr3D_Gizmos.prefab                              guid: c61374e972eb9724d96c9fb850a0ffa1
│   └── Textures/                                               [folder]
│       ├── Checkers/                                           [folder]
│       │   ├── !Texel Checker 4k 10.24.png                     guid: 1f7973c2854409741a29e508c55fed53
│       │   ├── !Texel Checker 4k 5.12.png                      guid: f8491e00ff1639f4385e7041f3b215da
│       │   ├── DiffuseColor_Texture.png                        guid: 9a63d281aeb9cde49a7a1a6fca588be7
│       │   ├── DiffuseColor_Texture_B.png                      guid: 1d7dafda85ef56f48a94f43b1a53af10
│       │   ├── DiffuseColor_Texture_G.png                      guid: a2a5c566201d1ef4e8f6922e7390c204
│       │   ├── DiffuseColor_Texture_R.png                      guid: c7b4d62f521fcdc418dc90f54332a2dd
│       │   ├── DiffuseColor_TextureEditing.png                 guid: f477d5ea803df1c4ba251241b35bbed3
│       │   └── uv checker.jpg                                  guid: 4c06acd584f61b44f9b542986e5cf7e6
│       ├── Pbr/                                                [folder]
│       │   ├── crush_dummy_default_BaseColor.tga.png           guid: 0d530e53f4f03774aa8cd69522d832e3
│       │   ├── crush_dummy_default_Metallic.tga.png            guid: d178de78e455f1146a2cefea9653915a
│       │   ├── crush_dummy_default_Normal.tga.png              guid: 48ede01aca1b12e44a13ba232e54b4de
│       │   ├── crush_dummy_default_Occlusion.tga.png           guid: 2495474961460bd41b2dee0112390c04
│       │   └── crush_dummy_default_Roughness.tga.png           guid: 87c7049d6e60afe43b92d222f11f32d5
│       ├── Sprites/                                            [folder]
│       │   ├── 3d-Coordinate-Axis--Streamline-Core-Remix.png   guid: 0d60c5984c335eb4caf97e5aa88883f2
│       │   ├── 3d-Module-Dimension--Streamline-Core-Remix.png  guid: 17f5ee7cf29acbb44a00d28992efea15
│       │   ├── 3d-Rotate-1--Streamline-Core-Remix.png          guid: 7e912e9f297683d47aafe9c4889f502d
│       │   ├── black-keyboard-with-white-keys_icon-icons.com_72857.png  guid: f96755b84f9b2f94faabe36f1a557e47
│       │   ├── exit_icon-icons.com_70975.png                   guid: bb84b577be0e3774b9e6581fb165e423
│       │   ├── icons8-settings-240.png                         guid: 8fd1125cc7107384899484edcf5967b3
│       │   ├── ObjectIcon_blalsalsadlasd.png                   guid: deb8e2cea8008274dacb30a790ed8e65
│       │   ├── RigIcon-Bring-To-Front.png                      guid: 6e7a465742e1a774da0e681be73711e5
│       │   ├── secure-icon-png.png                             guid: 87472126b39a2e14a842819e204a8b95
│       │   └── TetoCar_AppIconPlaceHolder.png                  guid: 4c0ebe001d89b734d9fc17bcf5bda47b
│       └── Тo-signal-Иackground-Сolorful.jpg                   guid: 14780c46e50039a48b445e3ec228dfdd
│
├── Samples/                                                    [folder]
│   └── XR Interaction Toolkit/3.0.7/                           [folder]
│       ├── Starter Assets/                                     ... (151 files, see folder)
│       └── XR Device Simulator/                                ... (38 files, see folder)
│   Notable assets:
│   ├── Starter Assets/XRI Default Input Actions.inputactions   guid: c348712bda248c246b8c49b3db54643f
│   ├── Starter Assets/StarterAssets.asmdef                     guid: 8f07e33567e0ee542b40769c456c6b53
│   ├── Starter Assets/Prefabs/XR Origin (XR Rig).prefab        guid: f6336ac4ac8b4d34bc5072418cdc62a0
│   ├── Starter Assets/DemoScene.unity                          guid: 319dafa5c80f29f428dc1e0d03f04177
│   ├── XR Device Simulator/XR Device Simulator.prefab          guid: 18ddb545287c546e19cc77dc9fbb2189
│   └── XR Device Simulator/Unity.XR.Interaction.Toolkit.Samples.DeviceSimulator.asmdef  guid: a0c6cb4ff4b70b44e933543a342fb2b1
│
├── Screenshots/                                                [folder]
│   ├── screenshot-20260515-203351.png                          guid: d54a305989281c7489463ecfdca17fbc
│   ├── screenshot-20260515-203412.png                          guid: 899463f4f4c8c6d439088b6c267817d7
│   ├── screenshot-20260515-204154.png                          guid: c64c619476139fe498e6487d92ac37cb
│   ├── screenshot-20260515-220553.png                          guid: b2365dd7cfcbcde4bbb5b509e9b601fa
│   ├── screenshot-20260515-220614.png                          guid: c372c0b93eec26b4686b2def4a40a367
│   ├── screenshot-20260515-220652.png                          guid: fc082da00a6ea0d42be596c538f109ef
│   └── screenshot-20260515-221750.png                          guid: ee85c605f3c3d4b4a84222f58d6016d7
│
├── Settings/                                                   [folder]
│   ├── DefaultVolumeProfile.asset                              guid: ab09877e2e707104187f6f83e2f62510
│   ├── Mobile_Renderer.asset                                   guid: 65bc7dbf4170f435aa868c779acfb082
│   ├── Mobile_RPAsset.asset                                    guid: 5e6cbd92db86f4b18aec3ed561671858
│   ├── PC_Renderer.asset                                       guid: f288ae1f4751b564a96ac7587541f7a2
│   ├── PC_RPAsset.asset                                        guid: 4b83569d67af61e458304325a23e5dfd
│   ├── SampleSceneProfile.asset                                guid: 10fc4df2da32a41aaa32d77bc913491c
│   └── UniversalRenderPipelineGlobalSettings.asset             guid: 18dc0cd2c080841dea60987a38ce93fa
│
├── TextMesh Pro/                                               [folder]
│   ├── Documentation/                                          ... (1 file, see folder)
│   ├── Examples & Extras/                                      ... (~130 files, see folder)
│   ├── Fonts/                                                  ... (2 files, see folder)
│   ├── Resources/                                              ... (10 files, see folder)
│   ├── Shaders/                                                ... (20 files, see folder)
│   └── Sprites/                                                ... (3 files, see folder)
│   Notable assets:
│   ├── Resources/TMP Settings.asset                            guid: 3f5b5dff67a942289a9defa416b206f3
│   ├── Resources/Fonts & Materials/LiberationSans SDF.asset    guid: 8f586378b4e144a9851e7b34d9b748ee
│   └── Fonts/LiberationSans.ttf                                guid: e3265ab4bf004d28a9537516768c1c75
│
├── TutorialInfo/                                               [folder]
│   ├── Icons/                                                  [folder]
│   │   └── URP.png                                             guid: 727a75301c3d24613a3ebcec4a24c2c8
│   ├── Scripts/                                                [folder]
│   │   ├── Editor/                                             [folder]
│   │   │   └── ReadmeEditor.cs                                 guid: 476cc7d7cd9874016adc216baab94a0a
│   │   └── Readme.cs                                           guid: fcf7219bab7fe46a1ad266029b2fee19
│   └── Layout.wlt                                              guid: eabc9546105bf4accac1fd62a63e88e6
│
├── UnityPacks/                                                 [folder]
│   ├── ColorSkies/                                             [folder]
│   │   ├── Demo/                                               ... (4 files: SkyboxChanger.cs, LookCamera.cs, DemoScene.unity, DemoSceneSettings.lighting)
│   │   ├── Skies/                                              ... (8 .mat files: Sky_1..Sky_8)
│   │   └── Textures/                                           ... (8 skyboxes × 6 face textures = ~48 PNGs in Skybox_1..Skybox_8)
│   │   Notable assets:
│   │   ├── Demo/Scripts/SkyboxChanger.cs                       guid: dca6002b33ac5bc48b39588e222296fb
│   │   ├── Demo/Scripts/LookCamera.cs                          guid: 9a3e5f51e31f79c4fb340e92ffc286e8
│   │   └── Demo/DemoScene.unity                                guid: 47cd513b8c4397e4ca272a1ff269d933
│   │
│   ├── Downtown Game Studio/                                   [folder]
│   │   └── Nature Pack - Low Polly Trees & Bushes/             ... (~70 files: 24 .fbx trees/bushes, 24 .prefab, materials, textures, 1 demo scene)
│   │       Notable: Scenes/Demo Scene.unity                    guid: 0f5d237d1c2d03e41b64eaa6850efb2e
│   │
│   ├── HouseInteriorPack/                                      [folder]
│   │   ├── Materials/                                          ... (16 .mat — gradient color palette + glass/skybox/metallic/floor)
│   │   ├── Models/                                             ... (49 .fbx — furniture, kitchen, bathroom, lighting)
│   │   ├── Prefabs/                                            ... (49 .prefab — matching Models)
│   │   ├── Textures/                                           ... (12 gradient .png)
│   │   └── HouseInteriorPack.unity                             guid: 9fc0d4010bbf28b4594072e72b8655ab
│   │
│   ├── Keyboard Package/                                       [folder]
│   │   ├── Prefabs/                                            [folder]
│   │   │   ├── Base Structure/                                 [folder]
│   │   │   │   ├── Keyboard Background.prefab                  guid: 0ba0dceb2f0324ce8afc1db7c57f1a7e
│   │   │   │   ├── KeyboardLetter - Empty.prefab               guid: 27de4f094cd29433aa58ccea79ea4885
│   │   │   │   └── KeyboardRow - Empty.prefab                  guid: 91945d3a49f504d0897c49fc17581ce9
│   │   │   ├── Keyboard Layouts/                               [folder]
│   │   │   │   ├── Keyboard - Alpha Numeric.prefab             guid: c5c3f07364c99474e89c2be03cf3a387
│   │   │   │   └── Keyboard - Full.prefab                      guid: c05ff9432700e4f379c19941ed39cd21
│   │   │   └── Keyboard Rows/                                  [folder]
│   │   │       ├── KeyboardRow - Actions.prefab                guid: 913a6ef154ade4eba8889947dd6c49dd
│   │   │       ├── KeyboardRow - Capital Alpha 1.prefab        guid: 18b6872e93e594632b7bc00f8c251575
│   │   │       ├── KeyboardRow - Capital Alpha 2.prefab        guid: 31d6b7b7275124bdca1c47799769a2ec
│   │   │       ├── KeyboardRow - Capital Alpha 3.prefab        guid: 8fb3e79f1046a476aaf55ec195500895
│   │   │       ├── KeyboardRow - Numbers.prefab                guid: ca7ba7d8dac7149a5bb3ab1ccae3ccb6
│   │   │       ├── KeyboardRow - Small Alpha 1.prefab          guid: cf954843e0c4f445a9e467f81669e2f7
│   │   │       ├── KeyboardRow - Small Alpha 2.prefab          guid: 0afa03cdcd0b349fb9f5ea078350795e
│   │   │       ├── KeyboardRow - Small Alpha 3.prefab          guid: b75b2ab31b9d246dd80471bab1edb625
│   │   │       ├── KeyboardRow - Small AlphaNum 1.prefab       guid: 31c904a077086496ca4a5caeb4c0c610
│   │   │       ├── KeyboardRow - Small AlphaNum 2.prefab       guid: e6fa906a4579b43afa18f5e2b91bcd32
│   │   │       ├── KeyboardRow - Small AlphaNum 3.prefab       guid: 808ac0b004a6a4296996bb89660c98cc
│   │   │       ├── KeyboardRow - Spl Chars 1.prefab            guid: 0f53370e7da2144c78aa9f8848063d59
│   │   │       ├── KeyboardRow - Spl Chars 2.prefab            guid: 62deeda2d9d32460fa83b750d09b0d01
│   │   │       ├── KeyboardRow - Spl Chars Num 1.prefab        guid: e44d23f9bfa79400aa842cccd28ee711
│   │   │       └── KeyboardRow - Spl Chars Num 2.prefab        guid: 47e6a52b2dcfc49fdb566fc2d25cd1c9
│   │   ├── Scenes/                                             [folder]
│   │   │   ├── Demo 1 - Full Keyboard.unity                    guid: abd376551467646dd9a32bcae2b6d215
│   │   │   └── Demo 2 - AlphaNumeric Keyboard.unity            guid: a21d5280ec63344bcbf2a298cfb83c5d
│   │   ├── Scripts/                                            [folder]
│   │   │   ├── ColorDataStore.cs                               guid: 88e466f4a7082254f9ce9a8033284458
│   │   │   ├── GameManager.cs                                  guid: 1e80f595980db49f4a581e35a181ae65
│   │   │   ├── KeyboardButtonController.cs                     guid: 7915f1def9d7c43acb5c8d4a89b3d705
│   │   │   ├── KeyboardController.cs                           guid: 58e5b4119978242a1b262c686aaf1fda
│   │   │   └── KeyboardPackage.asmdef                          guid: 88cda448e0aa25f46af12334dcc564aa
│   │   └── Sprites/                                            [folder]
│   │       ├── Backspace.png                                   guid: 476b89269f2b94243af0294b0af9d397
│   │       ├── BorderThin.png                                  guid: cbdb4142c678e4c8ba188b78ad3c2254
│   │       └── Fill.png                                        guid: b66b2b5159ddf4101b46386f48e006f5
│   │
│   ├── QuickOutline/                                           [folder]
│   │   ├── Resources/                                          [folder]
│   │   │   ├── Materials/                                      [folder]
│   │   │   │   ├── OutlineFill.mat                             guid: 311313efa011949e98b6761d652ad13c
│   │   │   │   └── OutlineMask.mat                             guid: 106f3ff43a17d4967a2b64c7a92e49ec
│   │   │   └── Shaders/                                        [folder]
│   │   │       ├── OutlineFill.shader                          guid: 4e76d4023d7e0411297c670f878973e2
│   │   │       └── OutlineMask.shader                          guid: 341b058cd7dee4f5cba5cc59a513619e
│   │   ├── Samples/                                            [folder]
│   │   │   ├── Materials/                                      [folder]
│   │   │   │   └── Plane.mat                                   guid: f58cf65ea995c4b45be95713bdea8134
│   │   │   └── Scenes/                                         [folder]
│   │   │       ├── QuickOutline/                               [folder]
│   │   │       │   ├── LightingData.asset                      guid: ccdddc9cf6ae25a4e821983c98e5fb0f
│   │   │       │   └── ReflectionProbe-0.exr                   guid: b88d312b71c1ea04cbd58541f750def4
│   │   │       ├── QuickOutline.unity                          guid: f23712c79adc910408e872b127e825cf
│   │   │       └── QuickOutlineSettings.lighting               guid: 41a4959e076ff394c9b2b74195b0d72f
│   │   ├── Scripts/                                            [folder]
│   │   │   ├── Outline.cs                                      guid: 5fea29bb7c508c244a1f805a5fd3fc4d
│   │   │   └── QuickOutline.asmdef                             guid: c3d5e405c66e4da4f845dd08233d1ae8
│   │   └── Readme.txt                                          guid: 5933bfd39d7a5b843a0ed821f85bca19
│   │
│   └── SimpleFileBrowser/                                      [folder]
│       ├── Android/                                            [folder]
│       │   ├── FBCallbackHelper.cs                             guid: 997bfc59716c24c41ad03bcbd7f8ef0a
│       │   ├── FBDirectoryReceiveCallbackAndroid.cs            guid: 8dec4dc5be16ca84e9c147627361671d
│       │   ├── FBPermissionCallbackAndroid.cs                  guid: 2cd91db0ba676ef47af67e3597037d1a
│       │   └── SimpleFileBrowser.aar                           guid: cae0a78f915b13748ba09fd56bafb4c8
│       ├── Prefabs/                                            [folder]
│       │   ├── SimpleFileBrowserItem.prefab                    guid: c2db22c1e3cd2584fa0e9168745a4536
│       │   └── SimpleFileBrowserQuickLink.prefab               guid: c4e8ee7cea600bf4fb4498b4a47ae8f5
│       ├── Resources/                                          [folder]
│       │   └── SimpleFileBrowserCanvas.prefab                  guid: 9ea2606f8fddead46aabb7adb3d8d434
│       ├── Scripts/                                            [folder]
│       │   ├── SimpleRecycledListView/                         [folder]
│       │   │   ├── IListViewAdapter.cs                         guid: 08e51b912648ace4784ebe20fc6cc961
│       │   │   ├── ListItem.cs                                 guid: 9c3e7249b2cb96446a7ccfbed51aab81
│       │   │   └── RecycledListView.cs                         guid: 87ad67b4806678e40a492e337338760b
│       │   ├── EventSystemHandler.cs                           guid: 0868341868a4a4641b4d272d2fc5f538
│       │   ├── FileBrowser.cs                                  guid: f51dc09bf9e35804ba0f5e76c527025e
│       │   ├── FileBrowserAccessRestrictedPanel.cs             guid: 85ea21be7cacb484cb6db0d183d3b2a8
│       │   ├── FileBrowserContextMenu.cs                       guid: 0d5261bc2717e6143961d30ccb76fb66
│       │   ├── FileBrowserCursorHandler.cs                     guid: 759524cf7ef37f244bb00cd9724f0349
│       │   ├── FileBrowserFileOperationConfirmationPanel.cs    guid: 524a683efed82084b9a9c4a3eff23b73
│       │   ├── FileBrowserHelpers.cs                           guid: 2370e7a82ec4087499ebf7efa149e9eb
│       │   ├── FileBrowserItem.cs                              guid: b5f1b2825c50f7b4d9be146ab2137bff
│       │   ├── FileBrowserMovement.cs                          guid: 46d41d79fe7c3d44ca846b4f3d81a476
│       │   ├── FileBrowserQuickLink.cs                         guid: 1f277f5418eabf94cad94208055878af
│       │   ├── FileBrowserRenamedItem.cs                       guid: c7397ff7ae1ba4c47b6dfd3c84936584
│       │   ├── NonDrawingGraphic.cs                            guid: b4fd8cdb8c068dd4bb48c415877496ba
│       │   └── UISkin.cs                                       guid: 66bc3ce4885990c40a88f80fe0ad0101
│       ├── Skins/                                              [folder]
│       │   ├── DarkSkin.asset                                  guid: 07c1616acb3e05d4789781b38d6ab800
│       │   └── LightSkin.asset                                 guid: 758becaa4751c514ab3abd821b4078bb
│       ├── Sprites/                                            ... (~16 .psd/.png/.spriteatlas — file icons + cursors)
│       ├── README.txt                                          guid: 02a0a0f34932297429c157aca8b9a977
│       └── SimpleFileBrowser.Runtime.asmdef                    guid: c685d05731421f64287ad6d13be5af0e
│
├── XR/                                                         [folder]
│   ├── Loaders/                                                [folder]
│   │   ├── OpenXRLoader.asset                                  guid: bb2068d93afd206449373aa9e22588c9
│   │   └── SimulationLoader.asset                              guid: b650bb322d349e547ae76f38b7e562ec
│   ├── Resources/                                              [folder]
│   │   └── XRSimulationRuntimeSettings.asset                   guid: 2faab7ece7cfc12448db492319d0ba8e
│   ├── Settings/                                               [folder]
│   │   ├── OpenXR Editor Settings.asset                        guid: bc632e278dcdc21419ed73854f12b4b9
│   │   └── OpenXR Package Settings.asset                       guid: 088cc6010f8aacc4f977ba182eb1ac21
│   ├── UserSimulationSettings/                                 [folder]
│   │   ├── Resources/                                          [folder]
│   │   │   └── XRSimulationPreferences.asset                   guid: 253b30e25e1a2b54e94a469f79f84163
│   │   └── SimulationEnvironmentAssetsManager.asset            guid: 492a5cec3de9b274ba873a8eb16bfdcb
│   └── XRGeneralSettingsPerBuildTarget.asset                   guid: 137009674b184ff42bac7a9a1456d112
│
├── XRI/                                                        [folder]
│   └── Settings/                                               [folder]
│       ├── Resources/                                          [folder]
│       │   ├── InteractionLayerSettings.asset                  guid: e3806f5e9e1039445be71d431d19d374
│       │   └── XRDeviceSimulatorSettings.asset                 guid: 7f3a3e57e13dc614983393dbda66f198
│       └── XRInteractionEditorSettings.asset                   guid: 1d226e35a8560f747b53efb414001f85
│
├── InputSystem_Actions.inputactions                            guid: 052faaac586de48259a63d0c4782560b
└── Readme.asset                                                guid: 8105016687592461f977c054a80ce2f2
```

## Asset Counts

| Folder | .cs | .prefab | .unity | .asset | total non-meta |
|---|---:|---:|---:|---:|---:|
| `_App/` | 165 | 23 | 8 | 9 | 229 |
| `Resources/` | 0 | 16 | 0 | 0 | 76 |
| `Settings/` | 0 | 0 | 0 | 7 | 7 |
| `Samples/` (XR Interaction Toolkit) | 17 | 52 | 1 | 18 | 189 |
| `TextMesh Pro/` | 34 | 3 | 31 | 18 | 174 |
| `UnityPacks/` (all 3rd-party) | 26 | 118 | 6 | 5 | 386 |
| `XR/` | 0 | 0 | 0 | 8 | 8 |
| `XRI/` | 0 | 0 | 0 | 3 | 3 |
| `CompositionLayers/` | 0 | 0 | 0 | 2 | 2 |
| `TutorialInfo/` | 2 | 0 | 0 | 0 | 4 |
| `Screenshots/` | 0 | 0 | 0 | 0 | 7 |
| `_Recovery/` | 0 | 0 | 1 | 0 | 1 |
| top-level (root files) | 0 | 0 | 0 | 1 | 2 |
| **Total** | **244** | **212** | **47** | **71** | **~1088** |

Project code (`_App/`) breakdown: ~165 .cs files distributed across 12 subsystems plus Bootstrap/_Shared/Editor.

## How to look up by GUID

Unity сериализует ссылки на ассеты в YAML-полях вида `m_Script: {fileID: 11500000, guid: <hash>, type: 3}` или `m_Texture: {fileID: 2800000, guid: <hash>, type: 3}` — GUID и есть стабильный идентификатор файла. Чтобы найти всех «потребителей» конкретного ассета, грепни его guid по всему проекту:

```bash
grep -r "guid: 8f514251caced8e4c81aa66f85ab0430" Assets/ ProjectSettings/
```

(пример выше ищет ссылки на `User XR Origin (XR Rig).prefab`). Так же ловятся обратные зависимости префабов, шейдеров, scriptable object'ов и материалов. GUID `.meta`-файла **никогда** не меняется при переименовании или перемещении ассета внутри проекта — поэтому он надёжнее путей.
