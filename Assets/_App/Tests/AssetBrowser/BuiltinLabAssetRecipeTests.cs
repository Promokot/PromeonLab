using NUnit.Framework;
using UnityEngine;

public class BuiltinLabAssetRecipeTests
{
    [Test]
    public void Recipe_RoundTrips_ThroughJsonUtility()
    {
        var json = "{\"_id\":\"b1\",\"_displayName\":\"Cube\",\"_type\":0," +
                   "\"_recipe\":{\"schemaVersion\":1,\"type\":0,\"selectable\":true," +
                   "\"colliderKind\":1,\"colliderSize\":{\"x\":2.0,\"y\":3.0,\"z\":4.0}}}";

        var back = JsonUtility.FromJson<BuiltinLabAsset>(json);

        // Non-null proves _recipe is a serialized field (an unserialized private would stay null);
        // schemaVersion + colliderSize prove the nested object's contents actually deserialized.
        Assert.IsNotNull(back.Recipe, "BuiltinLabAsset must expose a deserialized recipe");
        Assert.AreEqual(1, back.Recipe.schemaVersion);
        Assert.That(back.Recipe.colliderSize.z, Is.EqualTo(4f).Within(1e-4));
    }
}
