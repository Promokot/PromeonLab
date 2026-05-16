using NUnit.Framework;
using UnityEngine;

public class SceneSerializerTests
{
    [Test]
    public void Serialize_ThenDeserialize_RoundTripsV2Fields()
    {
        var original = new SceneData
        {
            SceneId     = "scene-42",
            DisplayName = "My Scene",
            CreatedAt   = "2026-05-14",
        };
        original.Nodes.Add(new NodeData
        {
            NodeId      = "n1",
            AssetRef    = new AssetRef { Source = AssetSource.Builtin, AssetId = "chair" },
            Position    = new Vector3(1, 2, 3),
            Rotation    = Quaternion.Euler(0, 90, 0),
            Scale       = Vector3.one,
            DisplayName = "Chair 1",
        });

        var json   = SceneSerializer.Serialize(original);
        var result = SceneSerializer.Deserialize(json);

        Assert.AreEqual("scene-42", result.SceneId);
        Assert.AreEqual("My Scene", result.DisplayName);
        Assert.AreEqual(2,          result.SchemaVersion);
        Assert.AreEqual(1,          result.Nodes.Count);
        Assert.AreEqual("n1",       result.Nodes[0].NodeId);
        Assert.AreEqual(AssetSource.Builtin, result.Nodes[0].AssetRef.Source);
        Assert.AreEqual("chair",    result.Nodes[0].AssetRef.AssetId);
        Assert.AreEqual("Chair 1",  result.Nodes[0].DisplayName);
    }

    [Test]
    public void Deserialize_NullJson_ReturnsNull()
    {
        Assert.IsNull(SceneSerializer.Deserialize(null));
    }

    [Test]
    public void Deserialize_V1Json_MigratesToV2WithEmptyNodes()
    {
        var v1Json = "{ \"SchemaVersion\": 1, \"SceneId\": \"old\", \"DisplayName\": \"Old\", \"CreatedAt\": \"2024-01-01\" }";
        var result = SceneSerializer.Deserialize(v1Json);

        Assert.AreEqual(2, result.SchemaVersion);
        Assert.IsNotNull(result.Nodes);
        Assert.AreEqual(0, result.Nodes.Count);
    }
}
