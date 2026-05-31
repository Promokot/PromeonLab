using NUnit.Framework;
using UnityEngine;

public class InteractionCapabilityTests
{
    private GameObject _mgr;

    [SetUp]
    public void SetUp()
    {
        _mgr = new GameObject("mgr");
        _mgr.AddComponent<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_mgr);
    }

    [Test]
    public void Apply_AddsSceneNodeColliderSelectableInteractable()
    {
        var go = new GameObject("entity");
        try
        {
            InteractionCapability.Apply(go, InteractionLayer.SceneObjects,
                ColliderKind.Box, Vector3.zero, Vector3.one, selectable: true);

            Assert.IsNotNull(go.GetComponent<SceneNode>());
            var box = go.GetComponent<BoxCollider>();
            Assert.IsNotNull(box);
            Assert.That(box.size, Is.EqualTo(Vector3.one));
            Assert.IsNotNull(go.GetComponent<Selectable>());
            Assert.IsNotNull(go.GetComponent<XRPromeonInteractable>());
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void Apply_Idempotent_WhenInteractableAlreadyPresent()
    {
        var go = new GameObject("entity");
        go.AddComponent<XRPromeonInteractable>();
        try
        {
            InteractionCapability.Apply(go, InteractionLayer.SceneObjects,
                ColliderKind.Box, Vector3.zero, Vector3.one, selectable: true);

            Assert.AreEqual(1, go.GetComponents<XRPromeonInteractable>().Length);
            Assert.IsNull(go.GetComponent<Selectable>(), "should not add a second capability set");
            Assert.IsNull(go.GetComponent<BoxCollider>());
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void Apply_NotSelectable_AddsColliderButNoInteractable()
    {
        var go = new GameObject("entity");
        try
        {
            InteractionCapability.Apply(go, InteractionLayer.SceneObjects,
                ColliderKind.Box, Vector3.zero, Vector3.one, selectable: false);

            Assert.IsNotNull(go.GetComponent<BoxCollider>());
            Assert.IsNull(go.GetComponent<XRPromeonInteractable>());
        }
        finally { Object.DestroyImmediate(go); }
    }
}
