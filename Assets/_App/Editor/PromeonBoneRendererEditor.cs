using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PromeonBoneRenderer))]
public class PromeonBoneRendererEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        if (GUILayout.Button("Rebuild"))
        {
            var renderer = (PromeonBoneRenderer)target;
            renderer.Rebuild();
            EditorUtility.SetDirty(renderer);
        }
    }
}
