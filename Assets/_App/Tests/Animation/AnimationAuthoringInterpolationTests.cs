using NUnit.Framework;
using UnityEngine;

public class AnimationAuthoringInterpolationTests
{
    private AnimationAuthoring NewAuthoring(out EventBus bus)
    {
        bus = new EventBus();
        var a = new AnimationAuthoring(null, null, null, null, bus);
        a.InitForTest();
        return a;
    }

    [Test]
    public void SetInterpolation_UpdatesValue_AndPublishesInterpolationChanged()
    {
        var a = NewAuthoring(out var bus);
        a.CreateContainer("obj", 60, 24);
        a.SetActiveContainerOwner("obj");

        ContainerChange? change = null;
        bus.Subscribe<AnimationContainerChangedEvent>(e => change = e.Change);

        a.SetInterpolation("obj", InterpolationMode.Stepped);
        Assert.AreEqual(InterpolationMode.Stepped, a.GetInterpolation("obj"));
        Assert.AreEqual(ContainerChange.InterpolationChanged, change);
    }
}
