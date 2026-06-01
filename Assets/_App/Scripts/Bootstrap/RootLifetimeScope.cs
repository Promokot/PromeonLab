using VContainer;
using VContainer.Unity;
using UnityEngine;

public class RootLifetimeScope : LifetimeScope
{
    [SerializeField] private DemoAssetCatalog    _demoAssetCatalog;
    [SerializeField] private ModeTransitionGraph _transitionGraph;
    [SerializeField] private BuiltinAssetLibrary _builtinLibrary;
    [SerializeField] private NavBarConfig        _navBarConfig;
    [SerializeField] private OutlineConfig       _outlineConfig;
    [SerializeField] private ImportRenderProfile _importRenderProfile;

    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<PathProvider>(Lifetime.Singleton);
        builder.Register<AppStorage>(Lifetime.Singleton);
        builder.Register<EventBus>(Lifetime.Singleton);
        builder.Register<SceneContext>(Lifetime.Singleton);
        builder.Register<AnimationClipboard>(Lifetime.Singleton);
        builder.RegisterInstance(_demoAssetCatalog);
        builder.RegisterInstance(_transitionGraph);
        if (_builtinLibrary != null)
            builder.RegisterInstance(_builtinLibrary);
        else
            Debug.LogError("RootLifetimeScope: _builtinLibrary not assigned!");
        if (_outlineConfig != null)
            builder.RegisterInstance(_outlineConfig);
        else
            Debug.LogError("RootLifetimeScope: _outlineConfig not assigned — selection/bone outlines will not render!");
        // RegisterEntryPoint exposes IStartable so VContainer calls Start() → LoadAsync on app start;
        // .AsSelf() keeps the concrete type resolvable (AssetRegistry/ImportPipeline/AssetBrowserPanel
        // inject ImportedAssetLibrary directly). Plain Register<T> would NOT collect the entry point,
        // so the library never loads from disk after a restart (only mid-session Add() populates it).
        builder.RegisterEntryPoint<ImportedAssetLibrary>(Lifetime.Singleton).AsSelf();
        builder.RegisterEntryPoint<SavedAssetLibrary>(Lifetime.Singleton).AsSelf();
        builder.Register<AssetRegistry>(Lifetime.Singleton).As<IAssetRegistry>();

        // Render presets for runtime-imported assets (shader + two-sided per AssetType).
        // Always register a non-null instance so ReferenceEntityFactory resolves; an empty
        // runtime profile just means every type falls back to built-in defaults.
        var renderProfile = _importRenderProfile != null
            ? _importRenderProfile
            : ScriptableObject.CreateInstance<ImportRenderProfile>();
        if (_importRenderProfile == null)
            Debug.LogWarning("RootLifetimeScope: _importRenderProfile not assigned — imported images fall back to built-in URP/Unlit (two-sided).");
        builder.RegisterInstance(renderProfile);

        // Runtime loaders + per-type spawners.
        builder.Register<AssetSourceStore>(Lifetime.Singleton);
        builder.Register<GltfModelLoader>(Lifetime.Singleton);
        builder.Register<ReferenceEntityFactory>(Lifetime.Singleton);
        builder.Register<BoundsBoxColliderStrategy>(Lifetime.Singleton).As<IColliderStrategy>();
        builder.Register<ObjectEntityBuilder>(Lifetime.Singleton).As<IAssetEntityBuilder>();
        builder.Register<RigEntityBuilder>(Lifetime.Singleton).As<IAssetEntityBuilder>();
        builder.Register<ReferenceEntityBuilder>(Lifetime.Singleton).As<IAssetEntityBuilder>();
        builder.Register<AssetEntityBuilderRegistry>(Lifetime.Singleton);

        // Import handlers + pipeline.
        builder.Register<GltfImportHandler>(Lifetime.Singleton).As<IAssetImportHandler>();
        builder.Register<ImageImportHandler>(Lifetime.Singleton).As<IAssetImportHandler>();
        builder.RegisterEntryPoint<ImportPipeline>(Lifetime.Singleton).AsSelf();

        var transition = Object.FindAnyObjectByType<SceneTransitionRunner>(FindObjectsInactive.Include);
        if (transition != null)
            builder.RegisterInstance(transition).As<ISceneTransition>();
        else
            Debug.LogError("RootLifetimeScope: SceneTransitionRunner not found — mode transitions will fail.");

        builder.Register<ModeOrchestrator>(Lifetime.Singleton);
        // AssetImporter registered in VrEditingSceneScope (needs SceneGraph)

        var userPanel = Object.FindAnyObjectByType<UserPanel>(FindObjectsInactive.Include);
        if (userPanel != null)
        {
            builder.RegisterInstance(userPanel);
            builder.RegisterBuildCallback(c => c.Inject(userPanel));
        }

        var spawnApplier = Object.FindAnyObjectByType<PlayerSpawnApplier>(FindObjectsInactive.Include);
        if (spawnApplier != null)
            builder.RegisterInstance(spawnApplier);

        var keyboard = Object.FindAnyObjectByType<VrKeyboard>(FindObjectsInactive.Include);
        if (keyboard != null)
            builder.RegisterBuildCallback(c => c.Inject(keyboard));

        // Persistent interaction-mask switch on the XR rig; inject the root EventBus so it can react
        // to selection/gizmo/bone-mode events and re-mask the interactor casters per context.
        var maskBinder = Object.FindAnyObjectByType<InteractionMaskBinder>(FindObjectsInactive.Include);
        if (maskBinder != null)
            builder.RegisterBuildCallback(c => c.Inject(maskBinder));

        // --- Region model (app-lifetime) ---
        // The UserPanel + its nav buttons live on the persistent XR rig, so the router that
        // drives them must share that lifetime. Scene-bound module CONTENT controllers
        // (OutlinerPanel, AssetBrowserPanel, …) stay scene-scoped; scene-bound surfaces like
        // the file browser register themselves against this root router from their scene scope.
        if (_navBarConfig != null)
        {
            builder.RegisterInstance(_navBarConfig).As<IRegionConfig>().AsSelf();
            builder.Register<PanelRegionRouter>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();

            builder.RegisterBuildCallback(c =>
            {
                var router = c.Resolve<PanelRegionRouter>();

                foreach (var nav in Object.FindObjectsByType<RegionNavButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    c.Inject(nav);
                    router.RegisterButton(nav);
                }

                // Persistent panels that live on the XR rig with UserPanel and whose Construct deps
                // are all root-scoped — inject here so they work in EVERY mode, including MainMenu
                // where no scene scope runs. (AssetBrowserPanel's "+" → router.Open("fileBrowser");
                // FileBrowserSurface needs EventBus + router for publish/close.)
                foreach (var ab in Object.FindObjectsByType<AssetBrowserPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    c.Inject(ab);
                foreach (var fbs in Object.FindObjectsByType<FileBrowserSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    c.Inject(fbs);
                foreach (var iw in Object.FindObjectsByType<ImportWizardSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    c.Inject(iw);

                // AnimatorPanel is also persistent (nested in UserPanel on the XR rig) and its
                // Construct deps (EventBus, AnimationClipboard, SceneContext) are all root-scoped,
                // so inject here too — otherwise its button listeners never wire in modes that
                // don't run VrEditingSceneScope (or when it enables before scene-scope injection),
                // leaving every animator button inert. Scene services it touches (Authoring/Clock)
                // are reached through the root SceneContext façade, which is null-guarded at use.
                foreach (var ap in Object.FindObjectsByType<AnimatorPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    c.Inject(ap);

                foreach (var rm in Object.FindObjectsByType<RegionMember>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    c.Inject(rm);
                    router.RegisterModule(rm.ModuleId, rm);
                }

                router.ApplyMode(c.Resolve<ModeOrchestrator>().CurrentMode);
            });
        }
        else
        {
            Debug.LogError("RootLifetimeScope: _navBarConfig not assigned — nav buttons will be inert!");
        }
    }
}
