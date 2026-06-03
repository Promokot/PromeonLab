using NUnit.Framework;
using UnityEngine;

public class AnimationAuthoringLoopTests
{
    private AnimationAuthoring NewAuthoring(out EventBus bus)
    {
        bus = new EventBus();
        var sampler = new AnimationPlaybackSampler(null, null, bus);
        var a = new AnimationAuthoring(null, null, null, sampler, bus);
        a.InitForTest();
        return a;
    }

    [Test]
    public void SetLoop_TogglesFlag_AndPublishesLoopChanged()
    {
        var a = NewAuthoring(out var bus);
        a.CreateContainer("obj", 60, 24);

        ContainerChange? change = null;
        bus.Subscribe<AnimationContainerChangedEvent>(e => change = e.Change);

        a.SetLoop("obj", true);
        Assert.IsTrue(a.IsLooping("obj"));
        Assert.AreEqual(ContainerChange.LoopChanged, change);
    }

    [Test]
    public void StartLoopPlayback_RequiresLoopFlag_AndStopStopsIt()
    {
        var a = NewAuthoring(out _);
        a.CreateContainer("obj", 60, 24);

        a.StartLoopPlayback("obj", 0);
        Assert.IsFalse(a.IsLoopPlaying("obj"), "no playback without the Loop flag");

        a.SetLoop("obj", true);
        a.StartLoopPlayback("obj", 0);
        Assert.IsTrue(a.IsLoopPlaying("obj"));

        a.StopLoopPlayback("obj");
        Assert.IsFalse(a.IsLoopPlaying("obj"));
    }

    [Test]
    public void SetLoop_False_StopsPlayback()
    {
        var a = NewAuthoring(out _);
        a.CreateContainer("obj", 60, 24);
        a.SetLoop("obj", true);
        a.StartLoopPlayback("obj", 0);
        Assert.IsTrue(a.IsLoopPlaying("obj"));

        a.SetLoop("obj", false);
        Assert.IsFalse(a.IsLoopPlaying("obj"));
    }

    [Test]
    public void AdvanceLoopCursor_WrapsPastTotal()
    {
        Assert.AreEqual(2f, AnimationPlaybackSampler.AdvanceLoopCursor(58f, 4f, 60), 0.001f);
        Assert.AreEqual(0f, AnimationPlaybackSampler.AdvanceLoopCursor(0f, 0f, 0),  0.001f);
    }
}
