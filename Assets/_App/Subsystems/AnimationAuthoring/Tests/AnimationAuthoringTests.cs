using NUnit.Framework;

public class AnimationAuthoringTests
{
    [Test]
    public void OwnerOf_BonePrefix_StripsToRigId()
    {
        Assert.AreEqual("rig", AnimationAuthoring.OwnerOf("bone:rig:hand"));
    }

    [Test]
    public void OwnerOf_RegularNode_ReturnsAsIs()
    {
        Assert.AreEqual("plain", AnimationAuthoring.OwnerOf("plain"));
    }

    [Test]
    public void OwnerOf_BoneWithoutName_ReturnsRigId()
    {
        Assert.AreEqual("rig", AnimationAuthoring.OwnerOf("bone:rig:"));
    }

    [Test]
    public void OwnerOf_Null_ReturnsNull()
    {
        Assert.IsNull(AnimationAuthoring.OwnerOf(null));
    }

    [Test]
    public void OwnerOf_EmptyString_ReturnsEmpty()
    {
        Assert.AreEqual("", AnimationAuthoring.OwnerOf(""));
    }

    private class AuthoringFixture
    {
        public EventBus           Bus       { get; } = new();
        public AnimationAuthoring Authoring { get; }

        public AuthoringFixture()
        {
            Authoring = new AnimationAuthoring(
                clock     : null,
                sceneGraph: null,
                paths     : null,
                storage   : null,
                bus       : Bus);
            Authoring.InitForTest();
        }
    }

    [Test]
    public void HasContainer_FalseWhenMissing()
    {
        var fix = new AuthoringFixture();
        Assert.IsFalse(fix.Authoring.HasContainer("any"));
    }

    [Test]
    public void CreateContainer_AddsContainer()
    {
        var fix = new AuthoringFixture();
        fix.Authoring.CreateContainer("obj");
        Assert.IsTrue(fix.Authoring.HasContainer("obj"));
    }

    [Test]
    public void CreateContainer_PublishesAddedEvent()
    {
        var fix = new AuthoringFixture();
        AnimationContainerChangedEvent? received = null;
        fix.Bus.Subscribe<AnimationContainerChangedEvent>(e => received = e);

        fix.Authoring.CreateContainer("obj");

        Assert.IsTrue(received.HasValue);
        Assert.AreEqual("obj", received.Value.OwnerNodeId);
        Assert.AreEqual(ContainerChange.Added, received.Value.Change);
    }

    [Test]
    public void RemoveContainer_RemovesAndPublishesEvent()
    {
        var fix = new AuthoringFixture();
        fix.Authoring.CreateContainer("obj");

        AnimationContainerChangedEvent? received = null;
        fix.Bus.Subscribe<AnimationContainerChangedEvent>(e => received = e);

        fix.Authoring.RemoveContainer("obj");

        Assert.IsFalse(fix.Authoring.HasContainer("obj"));
        Assert.IsTrue(received.HasValue);
        Assert.AreEqual(ContainerChange.Removed, received.Value.Change);
    }

    [Test]
    public void GetContainer_ReturnsNullWhenMissing()
    {
        var fix = new AuthoringFixture();
        Assert.IsNull(fix.Authoring.GetContainer("missing"));
    }

    [Test]
    public void SetTotalFrames_UpdatesAndPublishesLengthChanged()
    {
        var fix = new AuthoringFixture();
        var c   = fix.Authoring.CreateContainer("obj");

        AnimationContainerChangedEvent? last = null;
        fix.Bus.Subscribe<AnimationContainerChangedEvent>(e =>
            { if (e.Change == ContainerChange.LengthChanged) last = e; });

        fix.Authoring.SetTotalFrames("obj", 30);

        Assert.AreEqual(30, c.TotalFrames);
        Assert.IsTrue(last.HasValue);
        Assert.AreEqual("obj", last.Value.OwnerNodeId);
    }

    [Test]
    public void SetTotalFrames_TruncatesKeysBeyondNewLength()
    {
        var fix = new AuthoringFixture();
        var c   = fix.Authoring.CreateContainer("obj");
        var t   = c.GetOrCreateTrack("obj");
        t.UpsertKey(50, UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);
        t.UpsertKey(80, UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);

        fix.Authoring.SetTotalFrames("obj", 60);

        Assert.AreEqual(1,  t.Keys.Count);
        Assert.AreEqual(50, t.Keys[0].Frame);
    }

    [Test]
    public void SetTotalFrames_ClampsToMinimumOne()
    {
        var fix = new AuthoringFixture();
        var c   = fix.Authoring.CreateContainer("obj");

        fix.Authoring.SetTotalFrames("obj", 0);
        Assert.AreEqual(1, c.TotalFrames);

        fix.Authoring.SetTotalFrames("obj", -5);
        Assert.AreEqual(1, c.TotalFrames);
    }

    [Test]
    public void SetFps_UpdatesAndPublishesFpsChanged()
    {
        var fix = new AuthoringFixture();
        var c   = fix.Authoring.CreateContainer("obj");

        AnimationContainerChangedEvent? last = null;
        fix.Bus.Subscribe<AnimationContainerChangedEvent>(e =>
            { if (e.Change == ContainerChange.FpsChanged) last = e; });

        fix.Authoring.SetFps("obj", 60);

        Assert.AreEqual(60, c.Fps);
        Assert.IsTrue(last.HasValue);
    }

    [Test]
    public void SetFps_ClampsToMinimumOne()
    {
        var fix = new AuthoringFixture();
        var c   = fix.Authoring.CreateContainer("obj");
        fix.Authoring.SetFps("obj", 0);
        Assert.AreEqual(1, c.Fps);
    }

    [Test]
    public void SetTotalFrames_NoContainer_NoOp()
    {
        var fix = new AuthoringFixture();
        bool published = false;
        fix.Bus.Subscribe<AnimationContainerChangedEvent>(_ => published = true);

        fix.Authoring.SetTotalFrames("missing", 30);

        Assert.IsFalse(published);
    }

    [Test]
    public void SetKey_WithExplicitValues_AddsKeyToTrack()
    {
        var fix = new AuthoringFixture();
        fix.Authoring.CreateContainer("obj");

        fix.Authoring.SetKey("obj", 10,
            UnityEngine.Vector3.up, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);

        var c = fix.Authoring.GetContainer("obj");
        var t = c.FindTrack("obj");
        Assert.IsNotNull(t);
        Assert.IsTrue(t.HasKey(10));
    }

    [Test]
    public void SetKey_PublishesKeyframeChangedAdded()
    {
        var fix = new AuthoringFixture();
        fix.Authoring.CreateContainer("obj");

        AnimationKeyframeChangedEvent? evt = null;
        fix.Bus.Subscribe<AnimationKeyframeChangedEvent>(e => evt = e);

        fix.Authoring.SetKey("obj", 5,
            UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);

        Assert.IsTrue(evt.HasValue);
        Assert.AreEqual("obj", evt.Value.NodeId);
        Assert.AreEqual("obj", evt.Value.OwnerNodeId);
        Assert.AreEqual(5,     evt.Value.Frame);
        Assert.AreEqual(KeyframeChange.Added, evt.Value.Change);
    }

    [Test]
    public void SetKey_OverwriteExisting_PublishesOverwritten()
    {
        var fix = new AuthoringFixture();
        fix.Authoring.CreateContainer("obj");
        fix.Authoring.SetKey("obj", 5,
            UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);

        AnimationKeyframeChangedEvent? evt = null;
        fix.Bus.Subscribe<AnimationKeyframeChangedEvent>(e => evt = e);
        fix.Authoring.SetKey("obj", 5,
            UnityEngine.Vector3.up, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);

        Assert.AreEqual(KeyframeChange.Overwritten, evt.Value.Change);
    }

    [Test]
    public void DeleteKey_RemovesAndPublishesEvent()
    {
        var fix = new AuthoringFixture();
        fix.Authoring.CreateContainer("obj");
        fix.Authoring.SetKey("obj", 5,
            UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);

        AnimationKeyframeChangedEvent? evt = null;
        fix.Bus.Subscribe<AnimationKeyframeChangedEvent>(e => evt = e);

        fix.Authoring.DeleteKey("obj", 5);

        Assert.IsFalse(fix.Authoring.HasKey("obj", 5));
        Assert.AreEqual(KeyframeChange.Removed, evt.Value.Change);
    }

    [Test]
    public void DeleteKey_LastInTrack_RemovesTrack()
    {
        var fix = new AuthoringFixture();
        fix.Authoring.CreateContainer("obj");
        fix.Authoring.SetKey("obj", 5,
            UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);

        fix.Authoring.DeleteKey("obj", 5);

        var c = fix.Authoring.GetContainer("obj");
        Assert.AreEqual(0, c.Tracks.Count);
    }

    [Test]
    public void SetKey_BoneNode_RoutesToParentRigContainer()
    {
        var fix = new AuthoringFixture();
        fix.Authoring.CreateContainer("rig");

        fix.Authoring.SetKey("bone:rig:hand", 7,
            UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);

        var c = fix.Authoring.GetContainer("rig");
        Assert.IsNotNull(c.FindTrack("bone:rig:hand"));
    }

    [Test]
    public void HasKey_FalseWhenNoContainer()
    {
        var fix = new AuthoringFixture();
        Assert.IsFalse(fix.Authoring.HasKey("missing", 5));
    }

    [Test]
    public void GetKeyFrames_ReturnsFramesInOrder()
    {
        var fix = new AuthoringFixture();
        fix.Authoring.CreateContainer("obj");
        fix.Authoring.SetKey("obj", 5,  UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);
        fix.Authoring.SetKey("obj", 1,  UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);
        fix.Authoring.SetKey("obj", 10, UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);

        var frames = fix.Authoring.GetKeyFrames("obj");
        Assert.AreEqual(3, frames.Count);
        Assert.AreEqual(1,  frames[0]);
        Assert.AreEqual(5,  frames[1]);
        Assert.AreEqual(10, frames[2]);
    }

    [Test]
    public void SetKeyForFrame_ActiveOnly_WritesActiveTrack()
    {
        var fix = new AuthoringFixture();
        fix.Authoring.CreateContainer("rig");

        var snapshots = new System.Collections.Generic.Dictionary<string, (UnityEngine.Vector3, UnityEngine.Quaternion, UnityEngine.Vector3)>
        {
            ["rig"] = (UnityEngine.Vector3.up, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one),
        };
        fix.Authoring.SetKeyForFrame_Test("rig", "rig", 10, snapshots);

        Assert.IsTrue(fix.Authoring.HasKey("rig", 10));
    }

    [Test]
    public void SetKeyForFrame_ActiveNodeLazyCreatesTrack()
    {
        var fix = new AuthoringFixture();
        fix.Authoring.CreateContainer("rig");

        var snapshots = new System.Collections.Generic.Dictionary<string, (UnityEngine.Vector3, UnityEngine.Quaternion, UnityEngine.Vector3)>
        {
            ["bone:rig:hand"] = (UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one),
        };
        fix.Authoring.SetKeyForFrame_Test("rig", "bone:rig:hand", 5, snapshots);

        var c = fix.Authoring.GetContainer("rig");
        Assert.IsNotNull(c.FindTrack("bone:rig:hand"));
        Assert.IsTrue(fix.Authoring.HasKey("bone:rig:hand", 5));
    }

    [Test]
    public void SetKeyForFrame_WritesActiveAndAllExistingTracks()
    {
        var fix = new AuthoringFixture();
        fix.Authoring.CreateContainer("rig");
        fix.Authoring.SetKey("bone:rig:hand", 0,
            UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);

        var snapshots = new System.Collections.Generic.Dictionary<string, (UnityEngine.Vector3, UnityEngine.Quaternion, UnityEngine.Vector3)>
        {
            ["rig"]           = (UnityEngine.Vector3.up,   UnityEngine.Quaternion.identity, UnityEngine.Vector3.one),
            ["bone:rig:hand"] = (UnityEngine.Vector3.down, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one),
        };
        fix.Authoring.SetKeyForFrame_Test("rig", "rig", 20, snapshots);

        Assert.IsTrue(fix.Authoring.HasKey("rig",           20));
        Assert.IsTrue(fix.Authoring.HasKey("bone:rig:hand", 20));
    }

    [Test]
    public void SetKeyForFrame_NoContainer_NoOp()
    {
        var fix = new AuthoringFixture();
        fix.Authoring.SetKeyForFrame_Test("missing", "missing", 5,
            new System.Collections.Generic.Dictionary<string, (UnityEngine.Vector3, UnityEngine.Quaternion, UnityEngine.Vector3)>());

        Assert.IsFalse(fix.Authoring.HasContainer("missing"));
    }

    [Test]
    public void DeleteAllKeysAtFrame_RemovesFromAllTracksAtFrame()
    {
        var fix = new AuthoringFixture();
        fix.Authoring.CreateContainer("rig");
        fix.Authoring.SetKey("rig",           10, UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);
        fix.Authoring.SetKey("bone:rig:hand", 10, UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);
        fix.Authoring.SetKey("rig",           20, UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);

        fix.Authoring.DeleteAllKeysAtFrame("rig", 10);

        Assert.IsFalse(fix.Authoring.HasKey("rig",           10));
        Assert.IsFalse(fix.Authoring.HasKey("bone:rig:hand", 10));
        Assert.IsTrue (fix.Authoring.HasKey("rig",           20));
    }

    [Test]
    public void DeleteAllKeysAtFrame_NoOpForMissingFrame()
    {
        var fix = new AuthoringFixture();
        fix.Authoring.CreateContainer("rig");
        fix.Authoring.SetKey("rig", 10, UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);

        fix.Authoring.DeleteAllKeysAtFrame("rig", 99);

        Assert.IsTrue(fix.Authoring.HasKey("rig", 10));
    }

    [Test]
    public void CopyFrame_NoKeysAtFrame_ReturnsEmptyClipboard()
    {
        var fix = new AuthoringFixture();
        fix.Authoring.CreateContainer("rig");

        var clip = fix.Authoring.CopyFrame("rig", 10);

        Assert.IsTrue(clip.IsEmpty);
    }

    [Test]
    public void CopyFrame_ReturnsAllKeysAtFrame()
    {
        var fix = new AuthoringFixture();
        fix.Authoring.CreateContainer("rig");
        fix.Authoring.SetKey("rig",           10, UnityEngine.Vector3.up,   UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);
        fix.Authoring.SetKey("bone:rig:hand", 10, UnityEngine.Vector3.down, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);

        var clip = fix.Authoring.CopyFrame("rig", 10);

        Assert.AreEqual(2,     clip.Entries.Count);
        Assert.AreEqual("rig", clip.OwnerNodeId);
        Assert.AreEqual(10,    clip.SourceFrame);
    }

    [Test]
    public void PasteFrame_RestoresKeysAtTargetFrame()
    {
        var fix = new AuthoringFixture();
        fix.Authoring.CreateContainer("rig");
        fix.Authoring.SetKey("rig",           10, UnityEngine.Vector3.up,   UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);
        fix.Authoring.SetKey("bone:rig:hand", 10, UnityEngine.Vector3.down, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);
        var clip = fix.Authoring.CopyFrame("rig", 10);

        fix.Authoring.PasteFrame("rig", 30, clip);

        Assert.IsTrue(fix.Authoring.HasKey("rig",           30));
        Assert.IsTrue(fix.Authoring.HasKey("bone:rig:hand", 30));
    }

    [Test]
    public void PasteFrame_LazyCreatesMissingTrack()
    {
        var fix = new AuthoringFixture();
        fix.Authoring.CreateContainer("src");
        fix.Authoring.SetKey("src", 5, UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);
        var clip = fix.Authoring.CopyFrame("src", 5);

        fix.Authoring.CreateContainer("dst");
        fix.Authoring.PasteFrame("dst", 5, clip);

        var c = fix.Authoring.GetContainer("dst");
        Assert.IsNotNull(c.FindTrack("src"));
    }

    [Test]
    public void PasteFrame_NullClipboard_NoOp()
    {
        var fix = new AuthoringFixture();
        fix.Authoring.CreateContainer("rig");

        fix.Authoring.PasteFrame("rig", 10, null);
        Assert.AreEqual(0, fix.Authoring.GetContainer("rig").Tracks.Count);
    }

    [Test]
    public void NearestKeyBefore_ReturnsPreviousKey()
    {
        var fix = new AuthoringFixture();
        fix.Authoring.CreateContainer("rig");
        fix.Authoring.SetKey("rig",           5,  UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);
        fix.Authoring.SetKey("bone:rig:hand", 12, UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);
        fix.Authoring.SetKey("rig",           20, UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);

        Assert.AreEqual(12, fix.Authoring.NearestKeyBefore("rig", 15));
        Assert.AreEqual(5,  fix.Authoring.NearestKeyBefore("rig", 10));
    }

    [Test]
    public void NearestKeyBefore_ReturnsNullIfNone()
    {
        var fix = new AuthoringFixture();
        fix.Authoring.CreateContainer("rig");
        fix.Authoring.SetKey("rig", 5, UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);
        Assert.IsNull(fix.Authoring.NearestKeyBefore("rig", 5));
        Assert.IsNull(fix.Authoring.NearestKeyBefore("rig", 0));
    }

    [Test]
    public void NearestKeyAfter_ReturnsNextKey()
    {
        var fix = new AuthoringFixture();
        fix.Authoring.CreateContainer("rig");
        fix.Authoring.SetKey("rig", 5,  UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);
        fix.Authoring.SetKey("rig", 20, UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);

        Assert.AreEqual(20, fix.Authoring.NearestKeyAfter("rig", 5));
        Assert.AreEqual(5,  fix.Authoring.NearestKeyAfter("rig", 0));
    }

    [Test]
    public void NearestKeyAfter_ReturnsNullIfNone()
    {
        var fix = new AuthoringFixture();
        fix.Authoring.CreateContainer("rig");
        fix.Authoring.SetKey("rig", 5, UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity, UnityEngine.Vector3.one);
        Assert.IsNull(fix.Authoring.NearestKeyAfter("rig", 5));
        Assert.IsNull(fix.Authoring.NearestKeyAfter("rig", 100));
    }

    [Test]
    public void NearestKey_EmptyContainer_ReturnsNull()
    {
        var fix = new AuthoringFixture();
        fix.Authoring.CreateContainer("rig");
        Assert.IsNull(fix.Authoring.NearestKeyBefore("rig", 10));
        Assert.IsNull(fix.Authoring.NearestKeyAfter ("rig", 10));
    }
}
