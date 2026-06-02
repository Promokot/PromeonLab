using NUnit.Framework;
using UnityEngine;

public class SceneBundleTests
{
    [Test]
    public void SceneBundle_RoundTrips_ThroughJsonUtility()
    {
        var bundle = new SceneBundle
        {
            exportedAtUtc = "2026-06-02T00:00:00Z",
            fps = 30,
        };
        bundle.scene.id = "scene1";
        bundle.scene.name = "My Scene";
        bundle.nodes.Add(new SceneBundle.Node
        {
            nodeId = "n1",
            displayName = "Hero",
            assetSource = "Imported",
            assetId = "a1",
            assetType = "Rig",
            geometryFile = "models/a1.glb",
            geometryMissing = false,
            position = new Vector3(1, 2, 3),
            rotation = Quaternion.identity,
            scale = Vector3.one,
        });

        var json = JsonUtility.ToJson(bundle);
        var back = JsonUtility.FromJson<SceneBundle>(json);

        Assert.AreEqual(1, back.schemaVersion);
        Assert.AreEqual(30, back.fps);
        Assert.AreEqual("scene1", back.scene.id);
        Assert.AreEqual(1, back.nodes.Count);
        Assert.AreEqual("models/a1.glb", back.nodes[0].geometryFile);
        Assert.AreEqual(new Vector3(1, 2, 3), back.nodes[0].position);
    }
}
