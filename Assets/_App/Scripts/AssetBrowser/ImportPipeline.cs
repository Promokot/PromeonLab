using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

public class ImportPipeline : IStartable, IDisposable
{
    private readonly EventBus                  _bus;
    private readonly ImportedAssetLibrary      _library;
    private readonly IReadOnlyList<IAssetImporter> _handlers;
    private readonly AssetEntityBuilderRegistry _builders;
    private readonly ImportedSourceProvider           _store;
    private readonly GltfModelImporter            _loader;
    private readonly ThumbnailRenderer          _renderer;
    private readonly PathProvider               _paths;

    public ImportPipeline(EventBus bus, ImportedAssetLibrary library, IReadOnlyList<IAssetImporter> handlers,
                          AssetEntityBuilderRegistry builders, ImportedSourceProvider store,
                          GltfModelImporter loader, ThumbnailRenderer renderer, PathProvider paths)
    {
        _bus      = bus;
        _library  = library;
        _handlers = handlers;
        _builders = builders;
        _store    = store;
        _loader   = loader;
        _renderer = renderer;
        _paths    = paths;
    }

    public void Start()
    {
        _bus.Subscribe<FilePickedEvent>(OnFilePicked);
        _bus.Subscribe<ImportConfirmedEvent>(OnImportConfirmed);
    }

    public void Dispose()
    {
        _bus.Unsubscribe<FilePickedEvent>(OnFilePicked);
        _bus.Unsubscribe<ImportConfirmedEvent>(OnImportConfirmed);
    }

    private void OnFilePicked(FilePickedEvent e)
    {
        var handler = HandlerFor(e.Path);
        if (handler == null)
        {
            Debug.LogWarning($"ImportPipeline: no handler for '{Path.GetExtension(e.Path)}'");
            return;
        }
        _bus.Publish(new ImportRequestedEvent
        {
            FilePath      = e.Path,
            SuggestedName = Path.GetFileNameWithoutExtension(e.Path),
            SuggestedType = handler.SuggestedType,
        });
    }

    private void OnImportConfirmed(ImportConfirmedEvent e)
    {
        if (!e.Confirmed) return;
        _ = RunImportAsync(e);
    }

    private async Task RunImportAsync(ImportConfirmedEvent e)
    {
        try
        {
            var handler = HandlerFor(e.FilePath);
            if (handler == null) return;
            var record = await handler.ImportAsync(e.FilePath, e.ChosenType, e.DisplayName, CancellationToken.None);

            // Build once: bake the entity recipe now so spawn/scene-load can restore deterministically.
            var recipe = await _builders.BuildAsync(record.Type, _store.AbsolutePath(record.SourceRef), CancellationToken.None);

            // Per-rig leaf-bone orientation comes from the wizard. Only rigs have recipe.rig.
            if (recipe.rig != null)
            {
                recipe.rig.TerminalBonesAxis       = e.TerminalBonesAxis;
                recipe.rig.InvertTerminalBonesAxis = e.InvertTerminalBonesAxis;
            }

            record.SetRecipe(recipe);

            await GenerateThumbnailAsync(record, CancellationToken.None);

            _library.Add(record);
            await _library.SaveAsync(CancellationToken.None);
            _bus.Publish(new AssetImportedEvent { AssetId = record.Id });
        }
        catch (Exception ex)
        {
            Debug.LogError($"ImportPipeline: import failed for '{e.FilePath}'. {ex}");
        }
    }

    private async Task GenerateThumbnailAsync(ImportedLabAsset record, CancellationToken ct)
    {
        try
        {
            if (record.Type == AssetType.Reference)
            {
                // The image file itself is the thumbnail — no render.
                record.SetThumbnailRef(record.SourceRef);
                return;
            }

            // Object / Rig: render the .glb off-screen, parked far below the scene.
            var abs   = _store.AbsolutePath(record.SourceRef);
            var model = await _loader.LoadAsync(abs, new Vector3(0f, -10000f, 0f), Quaternion.identity, ct);
            if (model == null) return;

            try
            {
                var tex = _renderer.Render(model, 256, new Color(0.22f, 0.22f, 0.24f, 1f));
                var png = tex.EncodeToPNG();
                UnityEngine.Object.Destroy(tex);

                var path = _paths.ThumbnailPath(record.Id);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllBytesAsync(path, png, ct);

                record.SetThumbnailRef(PathProvider.ThumbnailRelativeRef(record.Id));
            }
            finally
            {
                UnityEngine.Object.Destroy(model);
            }
        }
        catch (Exception ex)
        {
            // A missing thumbnail must never abort the import.
            Debug.LogError($"ImportPipeline: thumbnail generation failed for '{record.Id}'. {ex}");
        }
    }

    private IAssetImporter HandlerFor(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return _handlers.FirstOrDefault(h => h.CanHandle(ext));
    }
}
