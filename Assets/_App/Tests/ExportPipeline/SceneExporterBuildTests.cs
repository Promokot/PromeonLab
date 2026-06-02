using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class SceneExporterBuildTests
{
    private static SceneData OneNodeScene(string nodeId, AssetSource source, string assetId,
                                          IList<BonePose> poses = null)
    {
        var nd = new NodeData
        {
            NodeId = nodeId,
            AssetRef = new AssetRef { Source = source, AssetId = assetId },
            Position = new Vector3(1, 0, 0),
            Rotation = Quaternion.identity,
            Scale = Vector3.one,
            DisplayName = nodeId,
            BonePoses = poses != null ? new List<BonePose>(poses) : new List<BonePose>(),
        };
        return new SceneData { SceneId = "s1", DisplayName = "Scene 1", Nodes = { nd } };
    }

    private static SceneExporter.AssetResolution Imported(AssetType type, string path) =>
        new SceneExporter.AssetResolution
        { Source = AssetSource.Imported, Type = type, SourcePath = path, SourceExists = true };

    [Test]
    public void BuildBundle_ImportedObject_SetsGeometryFile_AndSourceEntry()
    {
        var scene = OneNodeScene("n1", AssetSource.Imported, "a1");
        var (bundle, sources) = SceneExporter.BuildBundle(
            scene, null, _ => Imported(AssetType.Object, "C:/fake/a1.glb"), "utc");

        Assert.AreEqual(1, bundle.nodes.Count);
        var node = bundle.nodes[0];
        Assert.AreEqual("Imported", node.assetSource);
        Assert.AreEqual("Object", node.assetType);
        Assert.AreEqual("models/a1.glb", node.geometryFile);
        Assert.IsFalse(node.geometryMissing);
        Assert.AreEqual(1, sources.Count);
        Assert.AreEqual("models/a1.glb", sources[0].EntryPath);
        Assert.AreEqual("C:/fake/a1.glb", sources[0].AbsolutePath);
    }

    [Test]
    public void BuildBundle_ImportedReference_GoesToTexturesFolder()
    {
        var scene = OneNodeScene("n1", AssetSource.Imported, "img1");
        var (bundle, sources) = SceneExporter.BuildBundle(
            scene, null, _ => Imported(AssetType.Reference, "C:/fake/img1.png"), "utc");

        Assert.AreEqual("textures/img1.png", bundle.nodes[0].geometryFile);
        Assert.AreEqual("textures/img1.png", sources[0].EntryPath);
    }

    [Test]
    public void BuildBundle_Builtin_FlagsGeometryMissing_NoSource()
    {
        var scene = OneNodeScene("n1", AssetSource.Builtin, "cube");
        var (bundle, sources) = SceneExporter.BuildBundle(
            scene, null,
            _ => new SceneExporter.AssetResolution
            { Source = AssetSource.Builtin, Type = AssetType.Object, SourcePath = null, SourceExists = false },
            "utc");

        Assert.AreEqual("Builtin", bundle.nodes[0].assetSource);
        Assert.IsTrue(bundle.nodes[0].geometryMissing);
        Assert.AreEqual("", bundle.nodes[0].geometryFile);
        Assert.AreEqual(0, sources.Count);
    }

    [Test]
    public void BuildBundle_BonePoses_AreCarried()
    {
        var poses = new[] { new BonePose { BoneName = "pelvis", LocalPosition = new Vector3(0, 1, 0),
                                           LocalRotation = Quaternion.identity, LocalScale = Vector3.one } };
        var scene = OneNodeScene("n1", AssetSource.Imported, "a1", poses);
        var (bundle, _) = SceneExporter.BuildBundle(
            scene, null, _ => Imported(AssetType.Rig, "C:/fake/a1.glb"), "utc");

        Assert.AreEqual(1, bundle.nodes[0].bonePoses.Count);
        Assert.AreEqual("pelvis", bundle.nodes[0].bonePoses[0].BoneName);
    }

    [Test]
    public void BuildBundle_Animation_MappedForMatchingOwner_NullOtherwise()
    {
        var scene = OneNodeScene("n1", AssetSource.Imported, "a1");
        scene.Nodes.Add(new NodeData
        {
            NodeId = "n2", AssetRef = new AssetRef { Source = AssetSource.Imported, AssetId = "a2" },
            Position = Vector3.zero, Rotation = Quaternion.identity, Scale = Vector3.one,
            DisplayName = "n2", BonePoses = new List<BonePose>(),
        });

        var anim = new SceneAnimationData { Fps = 30 };
        var c = anim.CreateContainer("n1", 40, 30);
        c.Interpolation = InterpolationMode.Stepped;
        c.Loop = true;
        var track = c.GetOrCreateTrack("n1");
        track.UpsertKey(0, Vector3.zero, Quaternion.identity, Vector3.one);
        track.UpsertKey(10, new Vector3(1, 0, 0), Quaternion.identity, Vector3.one);

        var (bundle, _) = SceneExporter.BuildBundle(
            scene, anim, _ => Imported(AssetType.Object, "C:/fake/x.glb"), "utc");

        Assert.AreEqual(30, bundle.fps);
        var n1 = bundle.nodes.Find(n => n.nodeId == "n1");
        Assert.IsNotNull(n1.animation);
        Assert.AreEqual(40, n1.animation.totalFrames);
        Assert.AreEqual("Stepped", n1.animation.interpolation);
        Assert.IsTrue(n1.animation.loop);
        Assert.AreEqual(1, n1.animation.tracks.Count);
        Assert.AreEqual(2, n1.animation.tracks[0].keys.Count);

        var n2 = bundle.nodes.Find(n => n.nodeId == "n2");
        Assert.IsNull(n2.animation);
    }

    [Test]
    public void BuildBundle_ImportedButSourceMissing_FlagsGeometryMissing_NoSource()
    {
        var scene = OneNodeScene("n1", AssetSource.Imported, "gone");
        var (bundle, sources) = SceneExporter.BuildBundle(
            scene, null,
            _ => new SceneExporter.AssetResolution
            { Source = AssetSource.Imported, Type = AssetType.Object, SourcePath = "C:/missing/gone.glb", SourceExists = false },
            "utc");

        Assert.IsTrue(bundle.nodes[0].geometryMissing, "absent imported source must flag geometryMissing");
        Assert.AreEqual("", bundle.nodes[0].geometryFile);
        Assert.AreEqual(0, sources.Count, "missing source must not be added to the bundle list");
    }

    [Test]
    public void BuildBundle_SameAssetTwice_DedupsSourceList()
    {
        var scene = OneNodeScene("n1", AssetSource.Imported, "a1");
        scene.Nodes.Add(new NodeData
        {
            NodeId = "n2", AssetRef = new AssetRef { Source = AssetSource.Imported, AssetId = "a1" },
            Position = Vector3.zero, Rotation = Quaternion.identity, Scale = Vector3.one,
            DisplayName = "n2", BonePoses = new List<BonePose>(),
        });

        var (_, sources) = SceneExporter.BuildBundle(
            scene, null, _ => Imported(AssetType.Object, "C:/fake/a1.glb"), "utc");

        Assert.AreEqual(1, sources.Count, "same assetId must be bundled once");
    }
}
