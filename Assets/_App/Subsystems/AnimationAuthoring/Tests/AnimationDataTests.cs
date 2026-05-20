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
    public void GetOrCreateTrack_CreatesNewTrack()
    {
        var data  = new SceneAnimationData();
        var track = data.GetOrCreateTrack("abc");
        Assert.AreEqual("abc", track.NodeId);
        Assert.AreEqual(1, data.Tracks.Count);
    }

    [Test]
    public void GetOrCreateTrack_ReturnsExistingTrack()
    {
        var data   = new SceneAnimationData();
        var first  = data.GetOrCreateTrack("abc");
        var second = data.GetOrCreateTrack("abc");
        Assert.AreSame(first, second);
        Assert.AreEqual(1, data.Tracks.Count);
    }

    [Test]
    public void SceneAnimationData_JsonRoundTrip()
    {
        var data  = new SceneAnimationData { Fps = 24, TotalFrames = 60 };
        var track = data.GetOrCreateTrack("node1");
        track.UpsertKey(5, new Vector3(1, 2, 3), Quaternion.Euler(10, 20, 30), Vector3.one);

        var json   = UnityEngine.JsonUtility.ToJson(data);
        var loaded = UnityEngine.JsonUtility.FromJson<SceneAnimationData>(json);

        Assert.AreEqual(24,      loaded.Fps);
        Assert.AreEqual(60,      loaded.TotalFrames);
        Assert.AreEqual(1,       loaded.Tracks.Count);
        Assert.AreEqual("node1", loaded.Tracks[0].NodeId);
        Assert.AreEqual(5,       loaded.Tracks[0].Keys[0].Frame);
        Assert.AreEqual(1f,      loaded.Tracks[0].Keys[0].Position.x, 0.001f);
    }
}
