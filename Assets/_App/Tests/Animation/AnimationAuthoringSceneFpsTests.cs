using NUnit.Framework;

public class AnimationAuthoringSceneFpsTests
{
    private AnimationAuthoring NewAuthoring(out EventBus bus)
    {
        bus = new EventBus();
        var a = new AnimationAuthoring(null, null, null, null, bus);
        a.InitForTest();
        return a;
    }

    [Test]
    public void GetSceneFps_DefaultsTo24()
    {
        var a = NewAuthoring(out _);
        Assert.AreEqual(24, a.GetSceneFps());
    }

    [Test]
    public void SetSceneFps_UpdatesValue_AndClampsMinimum()
    {
        var a = NewAuthoring(out _);
        a.SetSceneFps(48);
        Assert.AreEqual(48, a.GetSceneFps());
        a.SetSceneFps(0);
        Assert.AreEqual(1, a.GetSceneFps());
    }

    [Test]
    public void SetSceneFps_PublishesFpsChanged_ForActiveOwner()
    {
        var a = NewAuthoring(out var bus);
        a.CreateContainer("obj1", 60, 24);
        a.SetActiveContainerOwner("obj1");

        ContainerChange? change = null;
        bus.Subscribe<AnimationContainerChangedEvent>(e => change = e.Change);

        a.SetSceneFps(30);
        Assert.AreEqual(ContainerChange.FpsChanged, change);
    }
}
