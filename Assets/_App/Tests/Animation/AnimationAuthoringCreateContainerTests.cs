using NUnit.Framework;

public class AnimationAuthoringCreateContainerTests
{
    [Test]
    public void CreateContainer_WithExplicitFramesAndFps_UsesThem()
    {
        var bus = new EventBus();
        var authoring = new AnimationAuthoring(null, null, null, null, bus);
        authoring.InitForTest();

        var c = authoring.CreateContainer("node1", 100, 24);

        Assert.AreEqual(100, c.TotalFrames);
        Assert.AreEqual(24, c.Fps);
        Assert.AreSame(c, authoring.GetContainer("node1"));
    }

    [Test]
    public void CreateContainer_Parameterless_KeepsDataLayerDefault()
    {
        var authoring = new AnimationAuthoring(null, null, null, null, new EventBus());
        authoring.InitForTest();

        var c = authoring.CreateContainer("node2");

        Assert.AreEqual(60, c.TotalFrames);
    }
}
