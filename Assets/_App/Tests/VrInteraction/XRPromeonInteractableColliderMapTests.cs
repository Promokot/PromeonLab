using NUnit.Framework;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Regression: colliders registered AFTER the interactable was registered with the
// XRInteractionManager (rig selector boxes, convex child colliders) must still resolve back to the
// interactable through the manager's collider→interactable map. The map is built once at
// registration and never re-scanned, so RegisterColliders has to re-register the interactable.
//
// The real lifecycle (OnEnable → auto find/register) only runs in play mode. We reproduce it in an
// EditMode test by wiring the manager and the initial empty-collider registration by hand – exactly
// the state the spawn pipeline is in when RegisterColliders is finally called.
public class XRPromeonInteractableColliderMapTests
{
    [Test]
    public void RegisterColliders_AfterRegistration_ResolvesThroughManagerMap()
    {
        var mgrGo = new GameObject("manager");
        var mgr   = mgrGo.AddComponent<XRInteractionManager>();

        var root  = new GameObject("rig");
        var child = new GameObject("selector");
        child.transform.SetParent(root.transform);
        var box = child.AddComponent<BoxCollider>(); // not on the interactable's GameObject

        try
        {
            var it = root.AddComponent<XRPromeonInteractable>();
            it.interactionManager = mgr;                    // setter's auto-register is play-mode-gated…
            mgr.RegisterInteractable((IXRInteractable)it);  // …so register with an EMPTY list by hand

            Assert.IsFalse(mgr.TryGetInteractableForCollider(box, out _),
                "sanity: the late collider is invisible until RegisterColliders re-registers");

            it.RegisterColliders(new Collider[] { box });

            Assert.IsTrue(mgr.TryGetInteractableForCollider(box, out var found),
                "a collider registered after the interactable must resolve through the manager map");
            Assert.AreSame(it, found);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(mgrGo);
        }
    }
}
