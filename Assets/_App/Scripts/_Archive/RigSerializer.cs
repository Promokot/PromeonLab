using UnityEngine;

public static class RigSerializer
{
    public static string Serialize(RigDefinition def) =>
        JsonUtility.ToJson(def, prettyPrint: true);

    public static RigDefinition Deserialize(string json) =>
        string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<RigDefinition>(json);
}
