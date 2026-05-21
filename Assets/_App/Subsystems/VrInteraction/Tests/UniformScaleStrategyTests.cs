using NUnit.Framework;
using UnityEngine;

public class UniformScaleStrategyTests
{
    private GameObject _targetGo;
    private Transform  _target;
    private UniformScaleStrategy _sut;

    [SetUp]
    public void SetUp()
    {
        _targetGo = new GameObject("target");
        _target   = _targetGo.transform;
        _target.position   = Vector3.zero;
        _target.localScale = Vector3.one;
        _sut = new UniformScaleStrategy();
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_targetGo);

    [Test]
    public void Drag_FartherFromCenter_ScalesAllAxesUp()
    {
        _sut.BeginDrag(_target, AxisKind.X, new Vector3(1f, 0f, 0f), Quaternion.identity);
        _sut.UpdateDrag(new Vector3(2f, 0f, 0f), Quaternion.identity);
        Assert.AreEqual(2f, _target.localScale.x, 1e-4);
        Assert.AreEqual(2f, _target.localScale.y, 1e-4);
        Assert.AreEqual(2f, _target.localScale.z, 1e-4);
    }

    [Test]
    public void Drag_CloserToCenter_ScalesDownProportionally()
    {
        _sut.BeginDrag(_target, AxisKind.X, new Vector3(2f, 0f, 0f), Quaternion.identity);
        _sut.UpdateDrag(new Vector3(1f, 0f, 0f), Quaternion.identity);
        Assert.AreEqual(0.5f, _target.localScale.x, 1e-4);
        Assert.AreEqual(0.5f, _target.localScale.y, 1e-4);
        Assert.AreEqual(0.5f, _target.localScale.z, 1e-4);
    }

    [Test]
    public void Drag_AtPivot_ClampsAndDoesNotThrow()
    {
        Assert.DoesNotThrow(() =>
        {
            _sut.BeginDrag(_target, AxisKind.X, Vector3.zero, Quaternion.identity);
            _sut.UpdateDrag(new Vector3(1f, 0f, 0f), Quaternion.identity);
        });
        Assert.IsFalse(float.IsNaN(_target.localScale.x));
    }
}
