using NUnit.Framework;
using UnityEngine;

public class AxisMoveStrategyTests
{
    private GameObject _targetGo;
    private Transform  _target;
    private AxisMoveStrategy _sut;

    [SetUp]
    public void SetUp()
    {
        _targetGo = new GameObject("target");
        _target   = _targetGo.transform;
        _target.position = Vector3.zero;
        _target.rotation = Quaternion.identity;
        _sut = new AxisMoveStrategy();
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_targetGo);

    [Test]
    public void Drag_AlongX_OnlyXChanges()
    {
        _sut.BeginDrag(_target, AxisKind.X, new Vector3(1f, 0f, 0f), Quaternion.identity);
        _sut.UpdateDrag(new Vector3(3f, 0f, 0f), Quaternion.identity);
        Assert.AreEqual(2f, _target.position.x, 1e-4);
        Assert.AreEqual(0f, _target.position.y, 1e-4);
        Assert.AreEqual(0f, _target.position.z, 1e-4);
    }

    [Test]
    public void Drag_HandMovesPerpendicular_ProjectionIgnoresOffset()
    {
        _sut.BeginDrag(_target, AxisKind.X, new Vector3(1f, 0f, 0f), Quaternion.identity);
        _sut.UpdateDrag(new Vector3(1f, 5f, 7f), Quaternion.identity);
        Assert.AreEqual(0f, _target.position.x, 1e-4);
        Assert.AreEqual(0f, _target.position.y, 1e-4);
        Assert.AreEqual(0f, _target.position.z, 1e-4);
    }

    [Test]
    public void Drag_AlongY_OnlyYChanges()
    {
        _sut.BeginDrag(_target, AxisKind.Y, new Vector3(0f, 1f, 0f), Quaternion.identity);
        _sut.UpdateDrag(new Vector3(0f, 4f, 0f), Quaternion.identity);
        Assert.AreEqual(3f, _target.position.y, 1e-4);
        Assert.AreEqual(0f, _target.position.x, 1e-4);
    }

    [Test]
    public void Drag_TargetRotated45_AxisIsLocalNotWorld()
    {
        _target.rotation = Quaternion.Euler(0f, 45f, 0f);
        var dir = new Vector3(Mathf.Cos(Mathf.Deg2Rad * 45f), 0f, -Mathf.Sin(Mathf.Deg2Rad * 45f));
        _sut.BeginDrag(_target, AxisKind.X, dir * 1f, Quaternion.identity);
        _sut.UpdateDrag(dir * 3f, Quaternion.identity);
        Assert.AreEqual(dir.x * 2f, _target.position.x, 1e-4);
        Assert.AreEqual(0f,         _target.position.y, 1e-4);
        Assert.AreEqual(dir.z * 2f, _target.position.z, 1e-4);
    }
}
