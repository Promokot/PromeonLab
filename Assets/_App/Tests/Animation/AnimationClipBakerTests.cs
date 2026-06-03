using NUnit.Framework;
using UnityEngine;

public class AnimationClipBakerTests
{
    [Test]
    public void ApplyInterpolation_Stepped_HoldsLeftValue()
    {
        var curve = new AnimationCurve();
        curve.AddKey(0f, 0f);
        curve.AddKey(1f, 10f);
        AnimationClipBaker.ApplyInterpolation(curve, InterpolationMode.Stepped);
        Assert.AreEqual(0f, curve.Evaluate(0.5f), 0.01f, "stepped holds the previous key");
    }

    [Test]
    public void ApplyInterpolation_Linear_BlendsBetweenKeys()
    {
        var curve = new AnimationCurve();
        curve.AddKey(0f, 0f);
        curve.AddKey(1f, 10f);
        AnimationClipBaker.ApplyInterpolation(curve, InterpolationMode.Linear);
        Assert.AreEqual(5f, curve.Evaluate(0.5f), 0.01f, "linear blends to the midpoint");
    }
}
