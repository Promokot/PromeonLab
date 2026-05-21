using NUnit.Framework;
using UnityEngine;

public class ActionContainerTests
{
    [Test]
    public void Defaults_60Frames_24Fps()
    {
        var c = new ActionContainer();
        Assert.AreEqual(60, c.TotalFrames);
        Assert.AreEqual(24, c.Fps);
        Assert.AreEqual(0,  c.Tracks.Count);
    }

    [Test]
    public void GetOrCreateTrack_CreatesNew()
    {
        var c = new ActionContainer { OwnerNodeId = "rig" };
        var t = c.GetOrCreateTrack("rig");
        Assert.AreEqual("rig", t.NodeId);
        Assert.AreEqual(1, c.Tracks.Count);
    }

    [Test]
    public void GetOrCreateTrack_ReturnsExisting()
    {
        var c = new ActionContainer { OwnerNodeId = "rig" };
        var t1 = c.GetOrCreateTrack("rig");
        var t2 = c.GetOrCreateTrack("rig");
        Assert.AreSame(t1, t2);
    }

    [Test]
    public void FindTrack_ReturnsNullWhenMissing()
    {
        var c = new ActionContainer { OwnerNodeId = "rig" };
        Assert.IsNull(c.FindTrack("missing"));
    }

    [Test]
    public void HasAnyKeyAtFrame_TrueWhenAnyTrackHas()
    {
        var c = new ActionContainer { OwnerNodeId = "rig" };
        var t = c.GetOrCreateTrack("rig");
        t.UpsertKey(7, Vector3.zero, Quaternion.identity, Vector3.one);

        Assert.IsTrue (c.HasAnyKeyAtFrame(7));
        Assert.IsFalse(c.HasAnyKeyAtFrame(8));
    }

    [Test]
    public void TruncateToTotalFrames_DropsKeysBeyondTotal()
    {
        var c = new ActionContainer { OwnerNodeId = "rig", TotalFrames = 10 };
        var t = c.GetOrCreateTrack("rig");
        t.UpsertKey(5,  Vector3.zero, Quaternion.identity, Vector3.one);
        t.UpsertKey(15, Vector3.zero, Quaternion.identity, Vector3.one);

        c.TruncateToTotalFrames();

        Assert.AreEqual(1, t.Keys.Count);
        Assert.AreEqual(5, t.Keys[0].Frame);
    }

    [Test]
    public void TruncateToTotalFrames_RemovesEmptyTracks()
    {
        var c = new ActionContainer { OwnerNodeId = "rig", TotalFrames = 10 };
        var t = c.GetOrCreateTrack("bone:rig:hand");
        t.UpsertKey(20, Vector3.zero, Quaternion.identity, Vector3.one);

        c.TruncateToTotalFrames();

        Assert.AreEqual(0, c.Tracks.Count);
    }

    [Test]
    public void ExistingTrackNodeIds_ReturnsAllNodeIds()
    {
        var c = new ActionContainer { OwnerNodeId = "rig" };
        c.GetOrCreateTrack("rig");
        c.GetOrCreateTrack("bone:rig:hand");

        var ids = c.ExistingTrackNodeIds();
        Assert.AreEqual(2, ids.Count);
        Assert.Contains("rig",            (System.Collections.ICollection)ids);
        Assert.Contains("bone:rig:hand",  (System.Collections.ICollection)ids);
    }
}
