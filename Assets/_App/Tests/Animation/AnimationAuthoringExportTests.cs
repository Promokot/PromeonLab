using NUnit.Framework;

public class AnimationAuthoringExportTests
{
    [Test]
    public void CaptureForExport_ReturnsLiveData_WithCreatedContainer()
    {
        var authoring = new AnimationAuthoring(null, null, null, null, new EventBus());
        authoring.InitForTest();
        authoring.CreateContainer("node1", 50, 24);

        var data = authoring.CaptureForExport();

        Assert.IsNotNull(data);
        Assert.IsNotNull(data.FindByOwner("node1"));
        Assert.AreEqual(50, data.FindByOwner("node1").TotalFrames);
    }

    [Test]
    public void CaptureForExport_BeforeAnyData_IsNull()
    {
        var authoring = new AnimationAuthoring(null, null, null, null, new EventBus());
        // No InitForTest(), no container -> _data not yet allocated.
        Assert.IsNull(authoring.CaptureForExport());
    }
}
