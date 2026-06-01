using NUnit.Framework;
using UnityEngine;

public class AnimationDataTests
{
    [Test]
    public void UpsertKey_AddsNewKeyframe()
    {
        var track = new AnimTrackData { NodeId = "n1" };
        track.UpsertKey(10, Vector3.one, Quaternion.identity, Vector3.one);
        Assert.AreEqual(1, track.Keys.Count);
        Assert.AreEqual(10, track.Keys[0].Frame);
    }

    [Test]
    public void UpsertKey_OverwritesExistingFrame()
    {
        var track = new AnimTrackData { NodeId = "n1" };
        track.UpsertKey(10, Vector3.zero,    Quaternion.identity, Vector3.one);
        track.UpsertKey(10, Vector3.forward, Quaternion.identity, Vector3.one);
        Assert.AreEqual(1,               track.Keys.Count);
        Assert.AreEqual(Vector3.forward, track.Keys[0].Position);
    }

    [Test]
    public void UpsertKey_KeysAreSortedByFrame()
    {
        var track = new AnimTrackData { NodeId = "n1" };
        track.UpsertKey(30, Vector3.zero, Quaternion.identity, Vector3.one);
        track.UpsertKey(10, Vector3.zero, Quaternion.identity, Vector3.one);
        track.UpsertKey(20, Vector3.zero, Quaternion.identity, Vector3.one);
        Assert.AreEqual(10, track.Keys[0].Frame);
        Assert.AreEqual(20, track.Keys[1].Frame);
        Assert.AreEqual(30, track.Keys[2].Frame);
    }

    [Test]
    public void RemoveKey_RemovesCorrectFrame()
    {
        var track = new AnimTrackData { NodeId = "n1" };
        track.UpsertKey(10, Vector3.zero, Quaternion.identity, Vector3.one);
        track.UpsertKey(20, Vector3.zero, Quaternion.identity, Vector3.one);
        track.RemoveKey(10);
        Assert.AreEqual(1,  track.Keys.Count);
        Assert.AreEqual(20, track.Keys[0].Frame);
    }

    [Test]
    public void HasKey_ReturnsTrueForExistingFrame()
    {
        var track = new AnimTrackData { NodeId = "n1" };
        track.UpsertKey(15, Vector3.zero, Quaternion.identity, Vector3.one);
        Assert.IsTrue(track.HasKey(15));
        Assert.IsFalse(track.HasKey(99));
    }

    [Test]
    public void TrimKeysAfter_RemovesKeysBeyondFrame()
    {
        var track = new AnimTrackData { NodeId = "n1" };
        track.UpsertKey(5,  Vector3.zero, Quaternion.identity, Vector3.one);
        track.UpsertKey(15, Vector3.zero, Quaternion.identity, Vector3.one);
        track.UpsertKey(25, Vector3.zero, Quaternion.identity, Vector3.one);

        track.TrimKeysAfter(15);

        Assert.AreEqual(2,  track.Keys.Count);
        Assert.AreEqual(5,  track.Keys[0].Frame);
        Assert.AreEqual(15, track.Keys[1].Frame);
    }

    [Test]
    public void SceneAnimationData_JsonRoundTrip_v2()
    {
        var data = new SceneAnimationData();
        var c    = data.CreateContainer("rig", 60, 24);
        var t    = c.GetOrCreateTrack("rig");
        t.UpsertKey(5, new Vector3(1, 2, 3), Quaternion.Euler(10, 20, 30), Vector3.one);

        var json   = UnityEngine.JsonUtility.ToJson(data);
        var loaded = UnityEngine.JsonUtility.FromJson<SceneAnimationData>(json);

        Assert.AreEqual(2, loaded.schemaVersion);
        Assert.AreEqual(1, loaded.Containers.Count);
        Assert.AreEqual("rig", loaded.Containers[0].OwnerNodeId);
        Assert.AreEqual(24,    loaded.Containers[0].Fps);
        Assert.AreEqual(60,    loaded.Containers[0].TotalFrames);
        Assert.AreEqual(1,     loaded.Containers[0].Tracks.Count);
        Assert.AreEqual(5,     loaded.Containers[0].Tracks[0].Keys[0].Frame);
        Assert.AreEqual(1f,    loaded.Containers[0].Tracks[0].Keys[0].Position.x, 0.001f);
    }

    [Test]
    public void FindByOwner_ReturnsNullWhenMissing()
    {
        var data = new SceneAnimationData();
        Assert.IsNull(data.FindByOwner("missing"));
    }

    [Test]
    public void CreateContainer_AddsAndReturns()
    {
        var data = new SceneAnimationData();
        var c    = data.CreateContainer("rig", 90, 30);

        Assert.AreEqual(1,    data.Containers.Count);
        Assert.AreEqual("rig", c.OwnerNodeId);
        Assert.AreEqual(90,    c.TotalFrames);
        Assert.AreEqual(30,    c.Fps);
    }

    [Test]
    public void CreateContainer_DefaultArgs_60_24()
    {
        var data = new SceneAnimationData();
        var c    = data.CreateContainer("rig");

        Assert.AreEqual(60, c.TotalFrames);
        Assert.AreEqual(24, c.Fps);
    }

    [Test]
    public void RemoveContainer_RemovesByOwner()
    {
        var data = new SceneAnimationData();
        data.CreateContainer("a");
        data.CreateContainer("b");

        data.RemoveContainer("a");

        Assert.AreEqual(1,   data.Containers.Count);
        Assert.AreEqual("b", data.Containers[0].OwnerNodeId);
    }

    [Test]
    public void SceneAnimationData_DefaultFps_Is24()
    {
        Assert.AreEqual(24, new SceneAnimationData().Fps);
    }

    [Test]
    public void SceneAnimationData_Fps_RoundTrips_AndKeepsSchemaV2()
    {
        var data = new SceneAnimationData { Fps = 48 };
        var json   = UnityEngine.JsonUtility.ToJson(data);
        var loaded = UnityEngine.JsonUtility.FromJson<SceneAnimationData>(json);
        Assert.AreEqual(48, loaded.Fps);
        Assert.AreEqual(2,  loaded.schemaVersion);
    }

    [Test]
    public void ActionContainer_Defaults_LinearAndNotLooping()
    {
        var c = new ActionContainer();
        Assert.AreEqual(InterpolationMode.Linear, c.Interpolation);
        Assert.IsFalse(c.Loop);
    }

    [Test]
    public void ActionContainer_InterpolationAndLoop_RoundTrip_SchemaV2()
    {
        var data = new SceneAnimationData();
        var c    = data.CreateContainer("obj", 60, 24);
        c.Interpolation = InterpolationMode.Stepped;
        c.Loop          = true;

        var loaded = UnityEngine.JsonUtility.FromJson<SceneAnimationData>(UnityEngine.JsonUtility.ToJson(data));
        Assert.AreEqual(InterpolationMode.Stepped, loaded.Containers[0].Interpolation);
        Assert.IsTrue(loaded.Containers[0].Loop);
        Assert.AreEqual(2, loaded.schemaVersion);
    }
}
