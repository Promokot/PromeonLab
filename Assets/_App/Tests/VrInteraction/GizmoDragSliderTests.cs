using NUnit.Framework;
using UnityEngine;

public class GizmoDragSliderTests
{
    [Test]
    public void InsideDeadzone_NotLocked_ReturnsFalse()
    {
        var sl = new GizmoDragSlider();
        sl.Begin(Vector3.zero, 0.02f);
        Assert.IsFalse(sl.TryGetSignedDisplacement(new Vector3(0.01f, 0f, 0f), out var s));
        Assert.AreEqual(0f, s);
    }

    [Test]
    public void PastDeadzone_LocksDir_BaselinedAtDeadzone()
    {
        var sl = new GizmoDragSlider();
        sl.Begin(Vector3.zero, 0.02f);
        Assert.IsTrue(sl.TryGetSignedDisplacement(new Vector3(1.02f, 0f, 0f), out var s));
        Assert.AreEqual(1.0f, s, 1e-4f);   // dot 1.02 − deadzone 0.02
    }

    [Test]
    public void PullBackPastStart_GivesNegative()
    {
        var sl = new GizmoDragSlider();
        sl.Begin(Vector3.zero, 0.02f);
        sl.TryGetSignedDisplacement(new Vector3(1.02f, 0f, 0f), out _);            // lock +X
        Assert.IsTrue(sl.TryGetSignedDisplacement(new Vector3(-0.98f, 0f, 0f), out var s));
        Assert.AreEqual(-1.0f, s, 1e-4f);  // dot −0.98 − 0.02
    }
}
