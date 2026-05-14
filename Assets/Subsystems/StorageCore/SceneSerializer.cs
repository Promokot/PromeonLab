using UnityEngine;

public static class SceneSerializer
{
    public static string Serialize(SceneData data) =>
        JsonUtility.ToJson(data, prettyPrint: true);

    public static SceneData Deserialize(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        return JsonUtility.FromJson<SceneData>(json);
    }
}
