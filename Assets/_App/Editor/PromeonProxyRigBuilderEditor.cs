using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PromeonProxyRigBuilder))]
public class PromeonProxyRigBuilderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        if (GUILayout.Button("Rebuild"))
        {
            var rig = (PromeonProxyRigBuilder)target;
            // Defer to next editor tick — DestroyImmediate during OnInspectorGUI tears down
            // GameObjects whose Editors are currently being drawn (selected ProxyRig / its
            // proxies / UI RectTransforms in selection), and Unity throws MissingReferenceException
            // / SerializedObjectNotCreatableException when those Editors re-enable next frame.
            EditorApplication.delayCall += () =>
            {
                if (rig == null) return;
                ClearSelectionIfInsideProxies(rig);
                rig.Rebuild();
                EditorUtility.SetDirty(rig);
            };
        }
    }

    private static void ClearSelectionIfInsideProxies(PromeonProxyRigBuilder rig)
    {
        var sel = Selection.activeGameObject;
        if (sel == null) return;
        foreach (var go in rig.ProxyGOs)
        {
            if (go == null) continue;
            if (sel == go || sel.transform.IsChildOf(go.transform))
            {
                Selection.activeGameObject = rig.gameObject;
                return;
            }
        }
    }
}
