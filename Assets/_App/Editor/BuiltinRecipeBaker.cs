using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Editor-only: bakes the AssetEntityRecipe (and, for References, generates the prefab) into each
// BuiltinLabAsset entry of a BuiltinAssetLibrary. Built-in source is a prefab (already a GameObject),
// so the bake instantiates it and reuses the same synchronous measurement core as runtime import.
// Writes the struct's private serialized fields via reflection so the runtime types stay editor-clean.
public static class BuiltinRecipeBaker
{
    private const BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Instance;

    public static void BakeAll(BuiltinAssetLibrary lib)
    {
        var list = Entries(lib);
        if (list == null) return;
        for (int i = 0; i < list.Count; i++) BakeIndex(lib, list, i);
        Persist(lib);
    }

    public static void BakeOne(BuiltinAssetLibrary lib, int index)
    {
        var list = Entries(lib);
        if (list == null || index < 0 || index >= list.Count) return;
        BakeIndex(lib, list, index);
        Persist(lib);
    }

    public static IList Entries(BuiltinAssetLibrary lib)
        => typeof(BuiltinAssetLibrary).GetField("_entries", Priv)?.GetValue(lib) as IList;

    private static void BakeIndex(BuiltinAssetLibrary lib, IList list, int i)
    {
        var entry = (BuiltinLabAsset)list[i];
        var collider = new BoundsBoxColliderStrategy();

        AssetEntityRecipe recipe;
        GameObject generatedPrefab = null;

        switch (entry.Type)
        {
            case AssetType.Object:
                if (entry.Prefab == null) { Debug.LogWarning($"Bake: '{entry.Id}' Object has no prefab — skipped."); return; }
                {
                    var temp = Object.Instantiate(entry.Prefab);
                    try { recipe = ObjectEntityBuilder.RecipeFromInstance(temp, collider, AssetType.Object); }
                    finally { Object.DestroyImmediate(temp); }
                }
                break;

            case AssetType.Rig:
                if (entry.Prefab == null) { Debug.LogWarning($"Bake: '{entry.Id}' Rig has no prefab — skipped."); return; }
                {
                    var temp = Object.Instantiate(entry.Prefab);
                    try { recipe = RigEntityBuilder.RecipeFromInstance(temp, collider, entry.TerminalBonesAxis, entry.InvertTerminalBonesAxis); }
                    finally { Object.DestroyImmediate(temp); }
                }
                break;

            case AssetType.Reference:
                if (entry.Image == null) { Debug.LogWarning($"Bake: '{entry.Id}' Reference has no image — skipped."); return; }
                generatedPrefab = ReferenceImagePrefabGenerator.Generate(entry.Id, entry.Image, out recipe);
                break;

            default:
                return;
        }

        object boxed = entry; // box the struct so reflected SetValue sticks
        typeof(BuiltinLabAsset).GetField("_recipe", Priv).SetValue(boxed, recipe);
        if (generatedPrefab != null)
            typeof(BuiltinLabAsset).GetField("_prefab", Priv).SetValue(boxed, generatedPrefab);
        list[i] = (BuiltinLabAsset)boxed;
    }

    private static void Persist(BuiltinAssetLibrary lib)
    {
        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
    }
}
