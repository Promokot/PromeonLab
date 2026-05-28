using NUnit.Framework;
using UnityEngine;

public class AxisScaleStrategyTests
{
    private GameObject _targetGo;
    private Transform  _target;
    private AxisScaleStrategy _sut;

    [SetUp]
    public void SetUp()
    {
        _targetGo = new GameObject("target");
        _target   = _targetGo.transform;
        _target.position   = Vector3.zero;
        _target.rotation   = Quaternion.identity;
        _target.localScale = Vector3.one;
        _sut = new AxisScaleStrategy();
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_targetGo);

    [Test]
    public void Drag_AwayFromCenter_DoublesAxisScale()
    {
        _sut.BeginDrag(_target, AxisKind.X, new Vector3(1f, 0f, 0f), Quaternion.identity);
        _sut.UpdateDrag(new Vector3(2f, 0f, 0f), Quaternion.identity);
        Assert.AreEqual(2f, _target.localScale.x, 1e-4);
        Assert.AreEqual(1f, _target.localScale.y, 1e-4);
        Assert.AreEqual(1f, _target.localScale.z, 1e-4);
    }

    [Test]
    public void Drag_TowardCenter_HalvesAxisScale()
    {
        _sut.BeginDrag(_target, AxisKind.X, new Vector3(2f, 0f, 0f), Quaternion.identity);
        _sut.UpdateDrag(new Vector3(1f, 0f, 0f), Quaternion.identity);
        Assert.AreEqual(0.5f, _target.localScale.x, 1e-4);
    }

    [Test]
    public void Drag_AtPivot_DistAtGrabClamped()
    {
        Assert.DoesNotThrow(() =>
        {
            _sut.BeginDrag(_target, AxisKind.X, Vector3.zero, Quaternion.identity);
            _sut.UpdateDrag(new Vector3(1f, 0f, 0f), Quaternion.identity);
        });
        Assert.IsFalse(float.IsNaN(_target.localScale.x));
        Assert.IsFalse(float.IsInfinity(_target.localScale.x));
    }

    [Test]
    public void Drag_PreservesOriginalScale()
    {
        _target.localScale = new Vector3(2f, 3f, 4f);
        _sut.BeginDrag(_target, AxisKind.Y, new Vector3(0f, 1f, 0f), Quaternion.identity);
        _sut.UpdateDrag(new Vector3(0f, 2f, 0f), Quaternion.identity);
        Assert.AreEqual(2f, _target.localScale.x, 1e-4);
        Assert.AreEqual(6f, _target.localScale.y, 1e-4);
        Assert.AreEqual(4f, _target.localScale.z, 1e-4);
    }
}
