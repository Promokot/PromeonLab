using NUnit.Framework;
using UnityEngine;

public class RingRotateStrategyTests
{
    private GameObject _go;
    private Transform  _t;
    private RingRotateStrategy _sut;

    // gain = 90 deg per metre → 90° at s = 1; deadzone 0.02.
    [SetUp]
    public void SetUp()
    {
        _go = new GameObject("target");
        _t  = _go.transform;
        _t.position = Vector3.zero;
        _t.rotation = Quaternion.identity;
        _sut = new RingRotateStrategy(90f, 0.02f);
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_go);

    [Test]
    public void PushAlongRefDir_RotatesAroundAxis()
    {
        _sut.BeginDrag(_t, AxisKind.Y, Vector3.zero, Quaternion.identity);
        _sut.UpdateDrag(new Vector3(1.02f, 0f, 0f), Quaternion.identity); // s = 1 → 90°
        var expected = Quaternion.AngleAxis(90f, Vector3.up);
        Assert.AreEqual(0f, Quaternion.Angle(expected, _t.rotation), 0.2f);
    }

    [Test]
    public void PullBackPastStart_RotatesNegative()
    {
        _sut.BeginDrag(_t, AxisKind.Y, Vector3.zero, Quaternion.identity);
        _sut.UpdateDrag(new Vector3(1.02f, 0f, 0f), Quaternion.identity);  // lock +X, +90°
        _sut.UpdateDrag(new Vector3(-0.98f, 0f, 0f), Quaternion.identity); // s = −1 → −90°
        var expected = Quaternion.AngleAxis(-90f, Vector3.up);
        Assert.AreEqual(0f, Quaternion.Angle(expected, _t.rotation), 0.2f);
    }

    [Test]
    public void InsideDeadzone_DoesNotRotate()
    {
        _sut.BeginDrag(_t, AxisKind.Y, Vector3.zero, Quaternion.identity);
        var before = _t.rotation;
        _sut.UpdateDrag(new Vector3(0.01f, 0f, 0f), Quaternion.identity);
        Assert.AreEqual(0f, Quaternion.Angle(before, _t.rotation), 1e-4f);
    }
}
