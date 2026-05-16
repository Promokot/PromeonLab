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
        return data;
    }
}
