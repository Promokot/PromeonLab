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

    public string AssetCatalogJson(string sceneId) =>
        Path.Combine(SceneRoot(sceneId), "asset-catalog.json");

    public string AnimationJson(string sceneId) =>
        Path.Combine(SceneRoot(sceneId), "animation.json");

    public string AssetPath(string sceneId, string relativePath) =>
        Path.Combine(SceneRoot(sceneId), "assets", relativePath);

    public string ExportDir(string sceneId) =>
        Path.Combine(SceneRoot(sceneId), "export");

    public string ScenesRoot() =>
        Path.Combine(_root, "scenes");

    public string ImportedLibraryPath =>
        Path.Combine(_root, "asset-library", "imported.json");

    public string SavedLibraryPath =>
        Path.Combine(_root, "asset-library", "saved.json");
}
