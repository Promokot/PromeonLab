using NUnit.Framework;

public class AnimationClipboardTests
{
    [Test]
    public void DefaultIsEmpty()
    {
        var c = new AnimationClipboard();
        Assert.IsTrue(c.IsEmpty);
        Assert.IsNull(c.Current);
    }

    [Test]
    public void Set_SetsCurrent_NotEmpty()
    {
        var c = new AnimationClipboard();
        var clip = new FrameClipboard
        {
            OwnerNodeId = "x",
            SourceFrame = 5,
            Entries = new System.Collections.Generic.List<FrameClipboardEntry>
            {
                new FrameClipboardEntry { TrackNodeId = "x" }
            }
        };
        c.Set(clip);
        Assert.IsFalse(c.IsEmpty);
        Assert.AreSame(clip, c.Current);
    }

    [Test]
    public void Set_NullOrEmpty_RemainsEmpty()
    {
        var c = new AnimationClipboard();
        c.Set(null);
        Assert.IsTrue(c.IsEmpty);

        c.Set(new FrameClipboard { Entries = new System.Collections.Generic.List<FrameClipboardEntry>() });
        Assert.IsTrue(c.IsEmpty);
    }

    [Test]
    public void Clear_RemovesCurrent()
    {
        var c = new AnimationClipboard();
        c.Set(new FrameClipboard
        {
            OwnerNodeId = "x",
            Entries = new System.Collections.Generic.List<FrameClipboardEntry>
            {
                new FrameClipboardEntry { TrackNodeId = "x" }
            }
        });

        c.Clear();
        Assert.IsTrue(c.IsEmpty);
    }
}
