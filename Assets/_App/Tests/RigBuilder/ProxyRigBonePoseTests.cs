using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class ProxyRigBonePoseTests
{
    [Test]
    public void ApplyThenCapture_RoundTripsLocalTrsByBoneName()
    {
        var root      = new GameObject("rig");
        var proxyRoot = new GameObject("ProxyRig"); proxyRoot.transform.SetParent(root.transform);
        var hips  = new GameObject("proxy_hips");  hips.transform.SetParent(proxyRoot.transform);
        var spine = new GameObject("proxy_spine"); spine.transform.SetParent(hips.transform);

        var runtime = root.AddComponent<ProxyRigRuntime>();
        var map = new Dictionary<string, Transform> { { "hips", hips.transform }, { "spine", spine.transform } };
        runtime.Bind(proxyRoot.transform, new List<GameObject> { hips, spine }, null, map);

        runtime.ApplyPoses(new List<BonePose>
        {
            new BonePose { BoneName = "hips",  LocalPosition = new Vector3(1, 2, 3),   LocalRotation = Quaternion.Euler(10, 20, 30), LocalScale = Vector3.one },
            new BonePose { BoneName = "spine", LocalPosition = new Vector3(0, 0.5f, 0), LocalRotation = Quaternion.Euler(0, 45, 0),   LocalScale = new Vector3(2, 2, 2) },
        });

        Assert.AreEqual(new Vector3(1, 2, 3), hips.transform.localPosition);
        Assert.AreEqual(new Vector3(2, 2, 2), spine.transform.localScale);

        var byName = runtime.CapturePoses().ToDictionary(p => p.BoneName);
        Assert.AreEqual(2, byName.Count);
        Assert.AreEqual(new Vector3(1, 2, 3), byName["hips"].LocalPosition);
        Assert.That(Quaternion.Angle(Quaternion.Euler(0, 45, 0), byName["spine"].LocalRotation), Is.LessThan(0.01f));

        Object.DestroyImmediate(root);
    }

    [Test]
    public void ApplyPoses_NullAndUnknownBone_AreNoOps()
    {
        var root      = new GameObject("rig");
        var proxyRoot = new GameObject("ProxyRig"); proxyRoot.transform.SetParent(root.transform);
        var hips = new GameObject("proxy_hips"); hips.transform.SetParent(proxyRoot.transform);

        var runtime = root.AddComponent<ProxyRigRuntime>();
        runtime.Bind(proxyRoot.transform, new List<GameObject> { hips }, null,
            new Dictionary<string, Transform> { { "hips", hips.transform } });

        runtime.ApplyPoses(null);
        runtime.ApplyPoses(new List<BonePose> { new BonePose { BoneName = "nonexistent", LocalPosition = Vector3.one } });

        Assert.AreEqual(Vector3.zero, hips.transform.localPosition, "unknown bone must not move hips");

        Object.DestroyImmediate(root);
    }
}
