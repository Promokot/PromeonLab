using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

/// <summary>
/// App-lifetime service that handles scene-to-JSON export.
/// Subscribes to SceneExportRequestedEvent, writes a manifest JSON to
/// {MyDocuments}/{Application.productName}/{fileName}.json, then publishes SceneExportedEvent.
///
/// For the export target path, use BuildTargetPath() — the panel calls this live to display
/// the path label before the user commits to Export.
/// </summary>
public class SceneExporter : IStartable, IDisposable
{
    private readonly EventBus   _bus;
    private readonly AppStorage _storage;

    public SceneExporter(EventBus bus, AppStorage storage)
    {
        _bus     = bus;
        _storage = storage;
    }

    void IStartable.Start()
    {
        _bus.Subscribe<SceneExportRequestedEvent>(OnExportRequested);
    }

    void IDisposable.Dispose()
    {
        _bus.Unsubscribe<SceneExportRequestedEvent>(OnExportRequested);
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the full output path for a given file name (no extension required).
    /// Call this from the panel to populate the path label without triggering an export.
    /// </summary>
    public string BuildTargetPath(string fileName)
    {
        var safe = SanitizeFileName(fileName);
        if (string.IsNullOrEmpty(safe)) safe = "export";
        var dir = ExportDirectory();
        return Path.Combine(dir, safe + ".json");
    }

    /// <summary>
    /// Performs the export: creates the output directory if needed, writes the manifest JSON,
    /// and returns the written path.
    ///
    /// <!-- TODO: real geometry/animation export — replace the manifest stub below
    ///      with actual mesh data, keyframe tracks, bone poses, etc. -->
    /// </summary>
    public async Task<string> ExportAsync(string fileName, CancellationToken ct = default)
    {
        var path = BuildTargetPath(fileName);

        var dir = Path.GetDirectoryName(path);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var sceneId = _storage.ActiveSceneId ?? "none";
        var scene   = !string.IsNullOrEmpty(sceneId) ? _storage.GetCachedScene(sceneId) : null;
        var name    = scene?.DisplayName ?? "Unknown";

        // Minimal manifest — real geometry/animation data goes here in the full implementation.
        var manifest = new ExportManifest
        {
            schemaVersion = 1,
            sceneId       = sceneId,
            sceneName     = name,
            exportedAtUtc = DateTime.UtcNow.ToString("o"),
        };
        var json = JsonUtility.ToJson(manifest, prettyPrint: true);

        await File.WriteAllTextAsync(path, json, ct);
        return path;
    }

    // -------------------------------------------------------------------------
    // Internals
    // -------------------------------------------------------------------------

    private void OnExportRequested(SceneExportRequestedEvent e)
    {
        // Fire-and-forget via a tracked task; async void is acceptable on the event
        // boundary as long as exceptions are surfaced (see catch below).
        _ = RunExportAsync(e.FileName);
    }

    private async Task RunExportAsync(string fileName)
    {
        try
        {
            var path = await ExportAsync(fileName);
            _bus.Publish(new SceneExportedEvent
            {
                Path    = path,
                Success = true,
                Message = $"Saved to {path}",
            });
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SceneExporter] Export failed: {ex}");
            _bus.Publish(new SceneExportedEvent
            {
                Path    = string.Empty,
                Success = false,
                Message = $"Export failed: {ex.Message}",
            });
        }
    }

    private static string ExportDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Application.productName);

    /// <summary>
    /// Strips the .json extension (if provided by the user) and removes filesystem-illegal
    /// characters, collapsing the result to a trimmed, non-empty string.
    /// </summary>
    private static string SanitizeFileName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        // Remove extension the user might have typed
        if (raw.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            raw = raw[..^5];
        // Strip chars illegal on Windows/macOS/Linux
        var invalid = new string(Path.GetInvalidFileNameChars());
        var pattern = $"[{Regex.Escape(invalid)}]";
        return Regex.Replace(raw, pattern, "_").Trim();
    }

    // -------------------------------------------------------------------------
    // Serialization DTO (JsonUtility requires a class or [Serializable] struct)
    // -------------------------------------------------------------------------

    [Serializable]
    private class ExportManifest
    {
        public int    schemaVersion;
        public string sceneId;
        public string sceneName;
        public string exportedAtUtc;
        // TODO: real geometry/animation export — add mesh nodes, bone poses,
        //       keyframe tracks, and any other data required by the target DCC tool.
    }
}
