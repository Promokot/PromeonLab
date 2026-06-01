using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class BonePosePersistenceTests
{
    [Test]
    public void SceneData_WithBonePoses_RoundTripsThroughSerializer()
    {
        var data = new SceneData { SceneId = "scene-01", DisplayName = "Test", CreatedAt = "2026-06-01" };
        data.Nodes.Add(new NodeData
        {
            NodeId    = "n1",
            BonePoses =
            {
                new BonePose
                {
                    BoneName      = "hips",
                    LocalPosition = new Vector3(1, 2, 3),
                    LocalRotation = Quaternion.Euler(0, 90, 0),
                    LocalScale    = new Vector3(2, 2, 2),
                },
            },
        });

        var json = SceneSerializer.Serialize(data);
        var back = SceneSerializer.Deserialize(json);

        Assert.AreEqual(3, back.SchemaVersion);
        Assert.AreEqual(1, back.Nodes[0].BonePoses.Count);
        var p = back.Nodes[0].BonePoses[0];
        Assert.AreEqual("hips", p.BoneName);
        Assert.AreEqual(new Vector3(1, 2, 3), p.LocalPosition);
        Assert.AreEqual(new Vector3(2, 2, 2), p.LocalScale);
        Assert.That(Quaternion.Angle(Quaternion.Euler(0, 90, 0), p.LocalRotation), Is.LessThan(0.01f));
    }

    [Test]
    public void LegacyV2Json_WithoutBonePoses_MigratesToV3WithEmptyList()
    {
        var json = "{\"SchemaVersion\":2,\"SceneId\":\"old\",\"DisplayName\":\"Old\",\"CreatedAt\":\"x\"," +
                   "\"Nodes\":[{\"NodeId\":\"n1\",\"Position\":{\"x\":0,\"y\":0,\"z\":0}," +
                   "\"Rotation\":{\"x\":0,\"y\":0,\"z\":0,\"w\":1},\"Scale\":{\"x\":1,\"y\":1,\"z\":1}}]}";
        var back = SceneSerializer.Deserialize(json);

        Assert.AreEqual(3, back.SchemaVersion);
        Assert.IsNotNull(back.Nodes[0].BonePoses);
        Assert.AreEqual(0, back.Nodes[0].BonePoses.Count);
    }
}
