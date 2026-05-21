using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VrAnimApp.Editor
{
    public static class RemoveMissingScriptsTool
    {
        [MenuItem("Tools/PromeonLab/Remove Missing Scripts From Prefab Stage")]
        public static void RemoveMissingFromPrefabStage()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null)
            {
                Debug.LogError("[RemoveMissing] No prefab stage open.");
                return;
            }

            var root = stage.prefabContentsRoot;
            int removed = 0;

            foreach (var go in root.GetComponentsInChildren<Transform>(true))
            {
                int count = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go.gameObject);
                removed += count;
                if (count > 0)
                    Debug.Log($"[RemoveMissing] Removed {count} missing script(s) from '{go.name}'.");
            }

            Debug.Log($"[RemoveMissing] Done. Total removed: {removed}");
        }
    }
}
