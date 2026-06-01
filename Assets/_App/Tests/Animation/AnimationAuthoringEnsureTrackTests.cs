using NUnit.Framework;

public class AnimationAuthoringEnsureTrackTests
{
    [Test]
    public void EnsureTrack_AddsEmptyTrack_ToOwnerContainer()
    {
        var authoring = new AnimationAuthoring(null, null, null, null, new EventBus());
        authoring.InitForTest();
        authoring.CreateContainer("obj1", 100, 24);

        authoring.EnsureTrack("obj1", "obj1");

        var c = authoring.GetContainer("obj1");
        Assert.IsNotNull(c.FindTrack("obj1"), "track should exist");
        Assert.AreEqual(0, c.FindTrack("obj1").Keys.Count, "track should have no keys");
    }

    [Test]
    public void EnsureTrack_NoContainer_DoesNotThrow()
    {
        var authoring = new AnimationAuthoring(null, null, null, null, new EventBus());
        authoring.InitForTest();
        Assert.DoesNotThrow(() => authoring.EnsureTrack("missing", "missing"));
    }
}
