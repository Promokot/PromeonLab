using NUnit.Framework;
using UnityEngine;

public class RingRotateStrategyTests
{
    private GameObject _targetGo;
    private Transform  _target;
    private RingRotateStrategy _sut;

    [SetUp]
    public void SetUp()
    {
        _targetGo = new GameObject("target");
        _target   = _targetGo.transform;
        _target.position = Vector3.zero;
        _target.rotation = Quaternion.identity;
        _sut = new RingRotateStrategy();
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_targetGo);

    [Test]
    public void Rotate_AroundY_90Degrees()
    {
        _sut.BeginDrag(_target, AxisKind.Y, new Vector3(1f, 0f, 0f), Quaternion.identity);
        _sut.UpdateDrag(new Vector3(0f, 0f, 1f), Quaternion.identity);
        var expected = Quaternion.AngleAxis(90f, Vector3.up);
        Assert.AreEqual(0f, Quaternion.Angle(expected, _target.rotation), 0.1f);
    }

    [Test]
    public void Rotate_ReverseDirection_NegativeAngle()
    {
        _sut.BeginDrag(_target, AxisKind.Y, new Vector3(1f, 0f, 0f), Quaternion.identity);
        _sut.UpdateDrag(new Vector3(0f, 0f, -1f), Quaternion.identity);
        var expected = Quaternion.AngleAxis(-90f, Vector3.up);
        Assert.AreEqual(0f, Quaternion.Angle(expected, _target.rotation), 0.1f);
    }

    [Test]
    public void Rotate_HandAtPivot_DoesNotWrite()
    {
        _sut.BeginDrag(_target, AxisKind.Y, new Vector3(1f, 0f, 0f), Quaternion.identity);
        var before = _target.rotation;
        Assert.DoesNotThrow(() => _sut.UpdateDrag(Vector3.zero, Quaternion.identity));
        Assert.AreEqual(0f, Quaternion.Angle(before, _target.rotation), 1e-4);
    }
}
