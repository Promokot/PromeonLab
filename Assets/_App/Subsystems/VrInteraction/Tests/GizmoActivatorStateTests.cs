using NUnit.Framework;
using UnityEngine;

public class GizmoActivatorStateTests
{
    private GameObject _activatorGo;
    private GizmoActivator _sut;

    [SetUp]
    public void SetUp()
    {
        _activatorGo = new GameObject("activator");
        _sut         = _activatorGo.AddComponent<GizmoActivator>();
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_activatorGo);

    [Test]
    public void OnHandleGrabbed_WithoutTarget_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => _sut.OnHandleGrabbed(null, Vector3.zero, Quaternion.identity));
    }

    [Test]
    public void OnHandleReleased_WithoutActiveDrag_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => _sut.OnHandleReleased());
    }

    [Test]
    public void OnHandleDragged_WithoutActiveDrag_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => _sut.OnHandleDragged(Vector3.zero, Quaternion.identity));
    }

    [Test]
    public void OnHandleAborted_WithoutActiveDrag_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => _sut.OnHandleAborted());
    }
}
