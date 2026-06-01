using NUnit.Framework;
using UnityEngine;

public class AnimationAuthoringInterpolationTests
{
    [Test]
    public void ApplyInterpolation_Stepped_HoldsLeftValue()
    {
        var curve = new AnimationCurve();
        curve.AddKey(0f, 0f);
        curve.AddKey(1f, 10f);
        AnimationAuthoring.ApplyInterpolation(curve, InterpolationMode.Stepped);
        Assert.AreEqual(0f, curve.Evaluate(0.5f), 0.01f, "stepped holds the previous key");
    }

    [Test]
    public void ApplyInterpolation_Linear_BlendsBetweenKeys()
    {
        var curve = new AnimationCurve();
        curve.AddKey(0f, 0f);
        curve.AddKey(1f, 10f);
        AnimationAuthoring.ApplyInterpolation(curve, InterpolationMode.Linear);
        Assert.AreEqual(5f, curve.Evaluate(0.5f), 0.01f, "linear blends to the midpoint");
    }

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
