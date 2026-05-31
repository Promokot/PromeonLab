using NUnit.Framework;
using UnityEngine;

public class BoundsBoxColliderStrategyTests
{
    [Test]
    public void Measure_SingleUnitCubeAtOrigin_GivesUnitBox()
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); // 1x1x1, renderer bounds = unit
        try
        {
            new BoundsBoxColliderStrategy().Measure(cube, out var kind, out var center, out var size);
            Assert.AreEqual(ColliderKind.Box, kind);
            Assert.That(center.magnitude, Is.LessThan(0.01f));
            Assert.That(size.x, Is.EqualTo(1f).Within(0.01f));
            Assert.That(size.y, Is.EqualTo(1f).Within(0.01f));
            Assert.That(size.z, Is.EqualTo(1f).Within(0.01f));
        }
        finally { Object.DestroyImmediate(cube); }
    }

    [Test]
    public void Measure_NoRenderers_GivesTinyFallback()
    {
        var go = new GameObject("empty");
        try
        {
            new BoundsBoxColliderStrategy().Measure(go, out var kind, out _, out var size);
            Assert.AreEqual(ColliderKind.Box, kind);
            Assert.That(size.x, Is.GreaterThan(0f));
        }
        finally { Object.DestroyImmediate(go); }
    }
}
