using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Editor-only: bakes the AssetEntityRecipe (and, for References, generates the prefab) into each
// BuiltinLabAsset entry of a BuiltinAssetLibrary. Built-in source is a prefab (already a GameObject),
// so the bake loads the prefab in an isolated preview scene and reuses the same synchronous
// measurement core as runtime import. Writes the struct's private serialized fields via reflection so
// the runtime types stay editor-clean; FieldInfos are cached and fail loudly if a field is renamed.
public static class BuiltinRecipeBaker
{
    private const BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Instance;

    private static readonly FieldInfo EntriesField =
        typeof(BuiltinAssetLibrary).GetField("_entries", Priv)
        ?? throw new MissingFieldException(nameof(BuiltinAssetLibrary), "_entries");
    private static readonly FieldInfo RecipeField =
        typeof(BuiltinLabAsset).GetField("_recipe", Priv)
        ?? throw new MissingFieldException(nameof(BuiltinLabAsset), "_recipe");
    private static readonly FieldInfo PrefabField =
        typeof(BuiltinLabAsset).GetField("_prefab", Priv)
        ?? throw new MissingFieldException(nameof(BuiltinLabAsset), "_prefab");

    public static void BakeAll(BuiltinAssetLibrary lib)
    {
        var list = Entries(lib);
        if (list == null) return;
        for (int i = 0; i < list.Count; i++) BakeIndex(list, i);
        Persist(lib);
    }

    public static void BakeOne(BuiltinAssetLibrary lib, int index)
    {
        var list = Entries(lib);
        if (list == null || index < 0 || index >= list.Count) return;
        BakeIndex(list, index);
        Persist(lib);
    }

    public static IList Entries(BuiltinAssetLibrary lib) => EntriesField.GetValue(lib) as IList;

    private static void BakeIndex(IList list, int i)
    {
        var entry = (BuiltinLabAsset)list[i];

        AssetEntityRecipe recipe;
        GameObject generatedPrefab = null;

        switch (entry.Type)
        {
            case AssetType.Object:
                if (!TryGetPrefabPath(entry, "Object", out var objPath)) return;
                recipe = MeasurePrefab(objPath, go => ObjectEntityBuilder.RecipeFromInstance(go, AssetType.Object));
                break;

            case AssetType.Rig:
                if (!TryGetPrefabPath(entry, "Rig", out var rigPath)) return;
                recipe = MeasurePrefab(rigPath, go => RigEntityBuilder.RecipeFromInstance(
                    go, entry.TerminalBonesAxis, entry.InvertTerminalBonesAxis));
                break;

            case AssetType.Reference:
                if (entry.Image == null) { Debug.LogWarning($"Bake: '{entry.Id}' Reference has no image – skipped."); return; }
                generatedPrefab = ReferenceImagePrefabGenerator.Generate(entry.Id, entry.Image, out recipe);
                break;

            default:
                Debug.LogWarning($"Bake: '{entry.Id}' unsupported AssetType {entry.Type} – skipped.");
                return;
        }

        object boxed = entry; // box the struct so reflected SetValue sticks
        RecipeField.SetValue(boxed, recipe);
        if (generatedPrefab != null)
            PrefabField.SetValue(boxed, generatedPrefab);
        list[i] = (BuiltinLabAsset)boxed;
    }

    // Loads the prefab into an isolated preview scene (NOT the user's open scene), measures it, and
    // unloads – so baking never dirties the active scene or fires Awake/OnEnable there.
    private static AssetEntityRecipe MeasurePrefab(string assetPath, Func<GameObject, AssetEntityRecipe> measure)
    {
        var root = PrefabUtility.LoadPrefabContents(assetPath);
        try { return measure(root); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static bool TryGetPrefabPath(BuiltinLabAsset entry, string kind, out string path)
    {
        path = entry.Prefab != null ? AssetDatabase.GetAssetPath(entry.Prefab) : null;
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogWarning($"Bake: '{entry.Id}' {kind} has no prefab asset – skipped.");
            return false;
        }
        return true;
    }

    private static void Persist(BuiltinAssetLibrary lib)
    {
        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
    }
}
