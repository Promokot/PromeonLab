using NUnit.Framework;
using UnityEngine;

public class UniformScaleStrategyTests
{
    private GameObject _go;
    private Transform  _t;
    private UniformScaleStrategy _sut;
    private static readonly float Gain = Mathf.Log(2f);

    [SetUp]
    public void SetUp()
    {
        _go = new GameObject("target");
        _t  = _go.transform;
        _t.localScale = Vector3.one;
        _sut = new UniformScaleStrategy(Gain, 0.02f);
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_go);

    [Test]
    public void PushAlongRefDir_ScalesAllAxesUp()
    {
        _sut.BeginDrag(_t, AxisKind.X, Vector3.zero, Quaternion.identity);
        _sut.UpdateDrag(new Vector3(1.02f, 0f, 0f), Quaternion.identity); // s = 1 → ×2
        Assert.AreEqual(2f, _t.localScale.x, 1e-3f);
        Assert.AreEqual(2f, _t.localScale.y, 1e-3f);
        Assert.AreEqual(2f, _t.localScale.z, 1e-3f);
    }

    [Test]
    public void PullBackPastStart_ScalesAllAxesDown()
    {
        _sut.BeginDrag(_t, AxisKind.X, Vector3.zero, Quaternion.identity);
        _sut.UpdateDrag(new Vector3(1.02f, 0f, 0f), Quaternion.identity);
        _sut.UpdateDrag(new Vector3(-0.98f, 0f, 0f), Quaternion.identity); // s = −1 → ×0.5
        Assert.AreEqual(0.5f, _t.localScale.x, 1e-3f);
        Assert.AreEqual(0.5f, _t.localScale.y, 1e-3f);
        Assert.AreEqual(0.5f, _t.localScale.z, 1e-3f);
    }

    [Test]
    public void InsideDeadzone_NoChange()
    {
        _sut.BeginDrag(_t, AxisKind.X, Vector3.zero, Quaternion.identity);
        _sut.UpdateDrag(new Vector3(0.01f, 0f, 0f), Quaternion.identity);
        Assert.AreEqual(1f, _t.localScale.x, 1e-4f);
    }
}
