using System.Collections;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BuiltinAssetLibrary))]
public class BuiltinAssetLibraryEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var lib = (BuiltinAssetLibrary)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Recipe Bake", EditorStyles.boldLabel);

        if (GUILayout.Button("Bake All"))
            BuiltinRecipeBaker.BakeAll(lib);

        var entries = BuiltinRecipeBaker.Entries(lib);
        if (entries == null) return;

        for (int i = 0; i < entries.Count; i++)
        {
            var e = (BuiltinLabAsset)entries[i];
            var verb  = e.Type == AssetType.Reference ? "Generate" : "Bake";
            var label = $"{verb}: {(string.IsNullOrEmpty(e.DisplayName) ? e.Id : e.DisplayName)} ({e.Type})";
            if (GUILayout.Button(label))
                BuiltinRecipeBaker.BakeOne(lib, i);
        }
    }
}
