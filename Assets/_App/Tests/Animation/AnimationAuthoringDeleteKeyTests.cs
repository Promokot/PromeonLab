using NUnit.Framework;
using UnityEngine;

public class AnimationAuthoringDeleteKeyTests
{
    private AnimationAuthoring NewAuthoring(out EventBus bus)
    {
        bus = new EventBus();
        var a = new AnimationAuthoring(null, null, null, null, bus);
        a.InitForTest();
        return a;
    }

    [Test]
    public void DeleteKey_EmptyingTrack_PublishesTracksChanged()
    {
        var a = NewAuthoring(out var bus);
        a.CreateContainer("obj", 60, 24);
        a.SetKey("obj", 5, Vector3.zero, Quaternion.identity, Vector3.one);

        bool tracksChanged = false;
        bus.Subscribe<AnimationContainerChangedEvent>(e =>
        { if (e.Change == ContainerChange.TracksChanged) tracksChanged = true; });

        a.DeleteKey("obj", 5);
        Assert.IsTrue(tracksChanged, "emptying a track announces TracksChanged so its row disappears");
        Assert.IsNull(a.GetContainer("obj").FindTrack("obj"));
    }
}
