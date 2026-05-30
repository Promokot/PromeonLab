using NUnit.Framework;
using UnityEngine;

public class PanelGrabHandleTests
{
    [Test]
    public void GrabOffset_RoundTrips_WhenAttachUnmoved()
    {
        var attach = new GameObject("attach").transform;
        attach.position = new Vector3(1f, 2f, 3f);
        attach.rotation = Quaternion.Euler(10f, 45f, 0f);
        var panelWorld = new Vector3(2f, 1f, 5f);

        var offset = PanelGrabHandle.CaptureOffset(attach, panelWorld);
        var result = PanelGrabHandle.ApplyOffset(attach, offset);

        Assert.That(Vector3.Distance(result, panelWorld), Is.LessThan(1e-4f));
        Object.DestroyImmediate(attach.gameObject);
    }

    [Test]
    public void GrabOffset_FollowsAttach_WhenAttachMoves()
    {
        var attach = new GameObject("attach").transform;
        attach.position = Vector3.zero;
        attach.rotation = Quaternion.identity;
        var panelWorld = new Vector3(0f, 0f, 1f);

        var offset = PanelGrabHandle.CaptureOffset(attach, panelWorld);
        attach.position = new Vector3(5f, 0f, 0f);     // controller moved +5 on X
        var result = PanelGrabHandle.ApplyOffset(attach, offset);

        Assert.That(Vector3.Distance(result, new Vector3(5f, 0f, 1f)), Is.LessThan(1e-4f));
        Object.DestroyImmediate(attach.gameObject);
    }
}
