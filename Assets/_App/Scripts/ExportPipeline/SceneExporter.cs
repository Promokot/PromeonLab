using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

/// <summary>
/// App-lifetime service that exports the current scene as a self-contained ZIP bundle
/// (`scene.json` + copied model/texture sources) under {MyDocuments}/{productName}/{name}.zip.
/// Subscribes to SceneExportRequestedEvent, publishes SceneExportedEvent with the result.
///
/// Live state is read through the root SceneContext façade (Graph + Authoring). Asset source files
/// are resolved through IAssetRegistry + PathProvider. The pure transform lives in BuildBundle.
/// </summary>
public class SceneExporter : IStartable, IDisposable
{
    private readonly EventBus       _bus;
    private readonly AppStorage     _storage;
    private readonly SceneContext   _ctx;
    private readonly IAssetRegistry _registry;
    private readonly PathProvider   _paths;

    public SceneExporter(EventBus bus, AppStorage storage, SceneContext ctx,
                         IAssetRegistry registry, PathProvider paths)
    {
        _bus      = bus;
        _storage  = storage;
        _ctx      = ctx;
        _registry = registry;
        _paths    = paths;
    }

    void IStartable.Start()    => _bus.Subscribe<SceneExportRequestedEvent>(OnExportRequested);
    void IDisposable.Dispose() => _bus.Unsubscribe<SceneExportRequestedEvent>(OnExportRequested);

    // -------------------------------------------------------------------------
    // Public API (panel uses BuildTargetPath for the live path label)
    // -------------------------------------------------------------------------

    public string BuildTargetPath(string fileName)
    {
        var safe = SanitizeFileName(fileName);
        if (string.IsNullOrEmpty(safe)) safe = "export";
        return Path.Combine(ExportDirectory(), safe + ".zip");
    }

    // -------------------------------------------------------------------------
    // Pure transform – internal so EditMode tests can call it directly.
    // -------------------------------------------------------------------------

    /// What the asset resolver returns for a node's AssetRef.
    public struct AssetResolution
    {
        public AssetSource Source;
        public AssetType   Type;
        public string      SourcePath;   // absolute path to the source file, or null
        public bool        SourceExists; // true when SourcePath points at a real file
    }

    /// A file to place inside the zip: EntryPath (e.g. "models/a1.glb") <- AbsolutePath on disk.
    public struct SourceFile
    {
        public string EntryPath;
        public string AbsolutePath;
    }

    internal static (SceneBundle bundle, List<SourceFile> sources) BuildBundle(
        SceneData scene,
        SceneAnimationData anim,
        Func<AssetRef, AssetResolution> resolve,
        string exportedAtUtc)
    {
        var bundle = new SceneBundle
        {
            schemaVersion = 1,
            exportedAtUtc = exportedAtUtc,
            fps           = anim?.Fps ?? 24,
        };
        bundle.scene.id   = scene?.SceneId ?? "none";
        bundle.scene.name = scene?.DisplayName ?? "Unknown";

        var sources = new List<SourceFile>();
        var seenEntries = new HashSet<string>();

        if (scene?.Nodes != null)
        {
            foreach (var nd in scene.Nodes)
            {
                var res = resolve(nd.AssetRef);
                var node = new SceneBundle.Node
                {
                    nodeId          = nd.NodeId,
                    displayName     = nd.DisplayName,
                    parentNodeId    = nd.ParentNodeId ?? "",
                    assetSource     = res.Source.ToString(),
                    assetId         = nd.AssetRef.AssetId,
                    assetType       = res.Type.ToString(),
                    position        = nd.Position,
                    rotation        = nd.Rotation,
                    scale           = nd.Scale,
                    bonePoses       = nd.BonePoses != null
                                        ? new List<BonePose>(nd.BonePoses)
                                        : new List<BonePose>(),
                    animation       = BuildAnimation(anim, nd.NodeId),
                };

                if (res.Source == AssetSource.Imported && res.SourceExists
                    && !string.IsNullOrEmpty(res.SourcePath))
                {
                    var folder = res.Type == AssetType.Reference ? "textures" : "models";
                    var ext    = Path.GetExtension(res.SourcePath);
                    var entry  = $"{folder}/{nd.AssetRef.AssetId}{ext}";
                    node.geometryFile    = entry;
                    node.geometryMissing = false;
                    if (seenEntries.Add(entry))
                        sources.Add(new SourceFile { EntryPath = entry, AbsolutePath = res.SourcePath });
                }
                else
                {
                    node.geometryFile    = "";
                    node.geometryMissing = true;
                }

                bundle.nodes.Add(node);
            }
        }

        return (bundle, sources);
    }

    private static SceneBundle.Animation BuildAnimation(SceneAnimationData anim, string ownerNodeId)
    {
        var container = anim?.FindByOwner(ownerNodeId);
        if (container == null) return null;

        var block = new SceneBundle.Animation
        {
            totalFrames   = container.TotalFrames,
            interpolation = container.Interpolation.ToString(),
            loop          = container.Loop,
        };
        foreach (var t in container.Tracks)
        {
            block.tracks.Add(new SceneBundle.Track
            {
                targetNodeId = t.NodeId,
                keys         = t.Keys != null ? new List<AnimKeyData>(t.Keys) : new List<AnimKeyData>(),
            });
        }
        return block;
    }

    // -------------------------------------------------------------------------
    // Zip writing – pure file IO (no Unity API), safe on a thread-pool thread.
    // -------------------------------------------------------------------------

    internal static void WriteZipBundle(string zipPath, string sceneJson, IReadOnlyList<SourceFile> sources)
    {
        var dir = Path.GetDirectoryName(zipPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        if (File.Exists(zipPath)) File.Delete(zipPath);

        using var fs  = new FileStream(zipPath, FileMode.CreateNew);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

        var jsonEntry = zip.CreateEntry("scene.json");
        using (var w = new StreamWriter(jsonEntry.Open()))
            w.Write(sceneJson);

        var seen = new HashSet<string>();
        foreach (var s in sources)
        {
            if (!seen.Add(s.EntryPath)) continue;
            if (!File.Exists(s.AbsolutePath)) continue;
            var entry = zip.CreateEntry(s.EntryPath);
            using var es  = entry.Open();
            using var src = File.OpenRead(s.AbsolutePath);
            src.CopyTo(es);
        }
    }

    // -------------------------------------------------------------------------
    // Orchestration
    // -------------------------------------------------------------------------

    private void OnExportRequested(SceneExportRequestedEvent e) => _ = RunExportAsync(e.FileName);

    private async Task RunExportAsync(string fileName)
    {
        try
        {
            if (!_ctx.HasScene)
            {
                _bus.Publish(new SceneExportedEvent
                { Path = "", Success = false, Message = "Export failed: no active scene." });
                return;
            }

            var path = BuildTargetPath(fileName);

            // --- main thread: capture live state + resolve sources ---
            var sceneId   = _storage.ActiveSceneId ?? "none";
            var cached    = !string.IsNullOrEmpty(sceneId) ? _storage.GetCachedScene(sceneId) : null;
            var display   = cached?.DisplayName ?? "Unknown";
            var createdAt = cached?.CreatedAt ?? DateTime.UtcNow.ToString("o");

            var sceneData = _ctx.Graph.CaptureSnapshot(sceneId, display, createdAt);
            var anim      = _ctx.Authoring != null ? _ctx.Authoring.CaptureForExport() : null;

            var (bundle, sources) = BuildBundle(sceneData, anim, Resolve, DateTime.UtcNow.ToString("o"));
            var json = JsonUtility.ToJson(bundle, prettyPrint: true);

            // --- thread pool: write the zip (pure file IO) ---
            await Task.Run(() => WriteZipBundle(path, json, sources));

            int missing = bundle.nodes.FindAll(n => n.geometryMissing).Count;
            var msg = $"Saved to {path}";
            if (missing > 0) msg += $" ({missing} node(s) without geometry)";

            _bus.Publish(new SceneExportedEvent { Path = path, Success = true, Message = msg });
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SceneExporter] Export failed: {ex}");
            _bus.Publish(new SceneExportedEvent
            { Path = "", Success = false, Message = $"Export failed: {ex.Message}" });
        }
    }

    private AssetResolution Resolve(AssetRef r)
    {
        var asset = _registry.Find(r);
        if (asset == null)
            return new AssetResolution
            { Source = r.Source, Type = AssetType.Object, SourcePath = null, SourceExists = false };

        string abs = string.IsNullOrEmpty(asset.SourceRef)
            ? null
            : Path.Combine(_paths.RootForSources, asset.SourceRef);
        bool exists = !string.IsNullOrEmpty(abs) && File.Exists(abs);

        return new AssetResolution
        { Source = asset.Source, Type = asset.Type, SourcePath = abs, SourceExists = exists };
    }

    // -------------------------------------------------------------------------
    // Internals
    // -------------------------------------------------------------------------

    private static string ExportDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Application.productName);

    private static string SanitizeFileName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        if (raw.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) raw = raw[..^4];
        var invalid = new string(Path.GetInvalidFileNameChars());
        var pattern = $"[{Regex.Escape(invalid)}]";
        return Regex.Replace(raw, pattern, "_").Trim();
    }
}
