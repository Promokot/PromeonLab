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
    private readonly IReadOnlyList<IAssetImportHandler> _handlers;
    private readonly AssetEntityBuilderRegistry _builders;
    private readonly AssetSourceStore           _store;

    public ImportPipeline(EventBus bus, ImportedAssetLibrary library, IReadOnlyList<IAssetImportHandler> handlers,
                          AssetEntityBuilderRegistry builders, AssetSourceStore store)
    {
        _bus      = bus;
        _library  = library;
        _handlers = handlers;
        _builders = builders;
        _store    = store;
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
                recipe.rig.TerminalAxis = e.TerminalAxis;

            record.SetRecipe(recipe);

            _library.Add(record);
            await _library.SaveAsync(CancellationToken.None);
            _bus.Publish(new AssetImportedEvent { AssetId = record.Id });
        }
        catch (Exception ex)
        {
            Debug.LogError($"ImportPipeline: import failed for '{e.FilePath}'. {ex}");
        }
    }

    private IAssetImportHandler HandlerFor(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return _handlers.FirstOrDefault(h => h.CanHandle(ext));
    }
}
