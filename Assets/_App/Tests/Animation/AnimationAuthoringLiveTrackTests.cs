using NUnit.Framework;
using UnityEngine;

public class AnimationAuthoringLiveTrackTests
{
    private AnimationAuthoring NewAuthoring(out EventBus bus)
    {
        bus = new EventBus();
        var a = new AnimationAuthoring(null, null, null, null, bus);
        a.InitForTest();
        return a;
    }

    [Test]
    public void SetKey_OnNewTrack_PublishesTracksChanged()
    {
        var a = NewAuthoring(out var bus);
        a.CreateContainer("obj1", 60, 24);

        bool tracksChanged = false;
        bus.Subscribe<AnimationContainerChangedEvent>(e =>
        { if (e.Change == ContainerChange.TracksChanged) tracksChanged = true; });

        a.SetKey("obj1", 5, Vector3.zero, Quaternion.identity, Vector3.one);
        Assert.IsTrue(tracksChanged, "first key on a new track must announce TracksChanged");
    }

    [Test]
    public void SetKey_OnExistingTrack_DoesNotRepublishTracksChanged()
    {
        var a = NewAuthoring(out var bus);
        a.CreateContainer("obj1", 60, 24);
        a.SetKey("obj1", 5, Vector3.zero, Quaternion.identity, Vector3.one);

        int tracksChangedCount = 0;
        bus.Subscribe<AnimationContainerChangedEvent>(e =>
        { if (e.Change == ContainerChange.TracksChanged) tracksChangedCount++; });

        a.SetKey("obj1", 10, Vector3.one, Quaternion.identity, Vector3.one);
        Assert.AreEqual(0, tracksChangedCount, "adding a key to an existing track must not announce TracksChanged");
    }
}
