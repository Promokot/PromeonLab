using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PromeonInteractableRigBuilder))]
public class PromeonInteractableRigBuilderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        if (GUILayout.Button("Rebuild"))
        {
            var rig = (PromeonInteractableRigBuilder)target;
            rig.Rebuild();
            EditorUtility.SetDirty(rig);
        }
    }
}
