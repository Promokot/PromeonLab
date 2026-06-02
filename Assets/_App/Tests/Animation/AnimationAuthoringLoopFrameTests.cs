using System.Collections.Generic;
using NUnit.Framework;

public class AnimationAuthoringLoopFrameTests
{
    [Test]
    public void PublishLoopFrameIfChanged_PublishesOncePerIntegerFrame()
    {
        var bus = new EventBus();
        var captured = new List<LoopFrameChangedEvent>();
        bus.Subscribe<LoopFrameChangedEvent>(e => captured.Add(e));

        var authoring = new AnimationAuthoring(null, null, null, null, bus);
        authoring.InitForTest();

        authoring.PublishLoopFrameIfChanged("n1", 5.3f);  // → frame 5, publish
        authoring.PublishLoopFrameIfChanged("n1", 5.7f);  // still frame 5, no publish
        authoring.PublishLoopFrameIfChanged("n1", 6.1f);  // → frame 6, publish

        Assert.AreEqual(2, captured.Count);
        Assert.AreEqual("n1", captured[0].OwnerNodeId);
        Assert.AreEqual(5,    captured[0].Frame);
        Assert.AreEqual(6,    captured[1].Frame);
    }
}
