using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class ProxyRigRuntimeTests
{
    private static GameObject MakeProxy(Transform parent)
    {
        var go = new GameObject("proxy");
        go.transform.SetParent(parent);
        go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>();
        go.AddComponent<BoxCollider>();
        return go;
    }

    [Test]
    public void SetBonesInteractive_True_EnablesProxyColliders_AndDisablesSelectorColliders()
    {
        var root      = new GameObject("rig");
        var proxyRoot = new GameObject("ProxyRig"); proxyRoot.transform.SetParent(root.transform);
        var p1 = MakeProxy(proxyRoot.transform);
        var p2 = MakeProxy(proxyRoot.transform);

        // A selector collider (whole-rig box) – must be OFF in bone mode, ON outside it.
        var selectorGo  = new GameObject("selector");
        var selectorCol = selectorGo.AddComponent<BoxCollider>();

        var runtime = root.AddComponent<ProxyRigRuntime>();
        runtime.Bind(proxyRoot.transform, new List<GameObject> { p1, p2 },
            new List<Collider> { selectorCol }, new Dictionary<string, Transform>());

        runtime.SetBonesInteractive(true);

        Assert.IsTrue(p1.GetComponent<MeshRenderer>().enabled);
        Assert.IsTrue(p1.GetComponent<Collider>().enabled);
        Assert.IsFalse(selectorCol.enabled, "selector collider must be OFF in bone mode");

        runtime.SetBonesInteractive(false);
        Assert.IsFalse(p1.GetComponent<MeshRenderer>().enabled);
        Assert.IsTrue(selectorCol.enabled, "selector collider must be ON outside bone mode");

        Object.DestroyImmediate(root);
        Object.DestroyImmediate(selectorGo);
    }

    [Test]
    public void SetVisualsEnabled_TogglesRenderers()
    {
        var root      = new GameObject("rig");
        var proxyRoot = new GameObject("ProxyRig"); proxyRoot.transform.SetParent(root.transform);
        var p1 = MakeProxy(proxyRoot.transform);

        var runtime = root.AddComponent<ProxyRigRuntime>();
        runtime.Bind(proxyRoot.transform, new List<GameObject> { p1 }, null, null);

        runtime.SetVisualsEnabled(false);
        Assert.IsFalse(p1.GetComponent<MeshRenderer>().enabled);
        runtime.SetVisualsEnabled(true);
        Assert.IsTrue(p1.GetComponent<MeshRenderer>().enabled);

        Object.DestroyImmediate(root);
    }
}
