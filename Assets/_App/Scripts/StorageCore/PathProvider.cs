using System.IO;
using UnityEngine;

public class PathProvider
{
    private readonly string _root;

    [VContainer.Inject]
    public PathProvider() : this(Application.persistentDataPath) { }

    public PathProvider(string root) => _root = root;

    public string SceneRoot(string sceneId) =>
        Path.Combine(_root, "scenes", sceneId);

    public string SceneJson(string sceneId) =>
        Path.Combine(SceneRoot(sceneId), "scene.json");

    public string AnimationJson(string sceneId) =>
        Path.Combine(SceneRoot(sceneId), "animation.json");

    public string ScenesRoot() =>
        Path.Combine(_root, "scenes");

    public string ImportedLibraryPath =>
        Path.Combine(_root, "asset-libraries", "imported-lib.json");

    public string SavedLibraryPath =>
        Path.Combine(_root, "asset-libraries", "saved-lib.json");

    public string SourcesDir =>
        System.IO.Path.Combine(_root, "asset-libraries", "sources");

    public string SourcePath(string assetId, string ext)
    {
        var clean = string.IsNullOrEmpty(ext) ? "" : (ext[0] == '.' ? ext : "." + ext);
        return System.IO.Path.Combine(SourcesDir, assetId + clean);
    }

    public string ThumbnailsDir =>
        System.IO.Path.Combine(_root, "asset-libraries", "thumbnails");

    public string ThumbnailPath(string assetId) =>
        System.IO.Path.Combine(ThumbnailsDir, assetId + ".png");

    /// Root-independent relative ref stored on the record (mirrors how SourceRef is stored).
    public static string ThumbnailRelativeRef(string assetId) =>
        System.IO.Path.Combine("asset-libraries", "thumbnails", assetId + ".png");

    public string RootForSources => _root;
}
