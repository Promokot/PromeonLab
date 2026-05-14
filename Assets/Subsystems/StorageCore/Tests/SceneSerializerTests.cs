using NUnit.Framework;

public class SceneSerializerTests
{
    [Test]
    public void Serialize_ThenDeserialize_RoundTrips()
    {
        var original = new SceneData
        {
            SceneId     = "scene-42",
            DisplayName = "My Scene",
            CreatedAt   = "2026-05-14"
        };

        var json   = SceneSerializer.Serialize(original);
        var result = SceneSerializer.Deserialize(json);

        Assert.AreEqual("scene-42",  result.SceneId);
        Assert.AreEqual("My Scene",  result.DisplayName);
        Assert.AreEqual(1,           result.SchemaVersion);
    }

    [Test]
    public void Deserialize_NullJson_ReturnsNull()
    {
        Assert.IsNull(SceneSerializer.Deserialize(null));
    }
}
