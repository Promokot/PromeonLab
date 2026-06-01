using System.Collections.Generic;
using UnityEngine;

public static class SceneSerializer
{
    public static string Serialize(SceneData data) =>
        JsonUtility.ToJson(data, prettyPrint: true);

    public static SceneData Deserialize(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        var data = JsonUtility.FromJson<SceneData>(json);
        if (data == null) return null;
        if (data.SchemaVersion < 2)
        {
            Debug.LogWarning($"SceneSerializer: migrating scene '{data.SceneId}' from v{data.SchemaVersion} to v2");
            data.SchemaVersion = 2;
            data.Nodes ??= new List<NodeData>();
        }
        if (data.SchemaVersion < 3)
        {
            Debug.LogWarning($"SceneSerializer: migrating scene '{data.SceneId}' from v{data.SchemaVersion} to v3");
            data.SchemaVersion = 3;
            foreach (var n in data.Nodes) n.BonePoses ??= new List<BonePose>();
        }
        return data;
    }
}
