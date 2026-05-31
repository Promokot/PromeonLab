using NUnit.Framework;
using UnityEngine;

public class ModeOrchestratorTests
{
    private class FakeTransition : ISceneTransition
    {
        public string LastScene;
        public System.Action Pending;
        public bool IsTransitioning { get; set; }
        public void Load(string sceneName, System.Action onLoaded) { LastScene = sceneName; Pending = onLoaded; }
        public void CompleteLoad() { Pending?.Invoke(); Pending = null; }
    }

    private static ModeTransitionGraph AllowAllGraph()
    {
        // ModeTransitionGraph's default serialized list already allows MainMenu↔VrEditing and
        // MainMenu↔Sandbox, which is all these tests need.
        return ScriptableObject.CreateInstance<ModeTransitionGraph>();
    }

    [Test]
    public void TransitionTo_PublishesModeChanged_OnlyAfterLoadCompletes()
    {
        var bus = new EventBus();
        var fake = new FakeTransition();
        var sut = new ModeOrchestrator(bus, AllowAllGraph(), fake);

        bool fired = false;
        bus.Subscribe<ModeChangedEvent>(_ => fired = true);

        sut.TransitionTo(AppMode.VrEditing);
        Assert.AreEqual("VrEditing", fake.LastScene);
        Assert.IsFalse(fired, "ModeChangedEvent must NOT fire before the scene finishes loading");

        fake.CompleteLoad();
        Assert.IsTrue(fired, "ModeChangedEvent fires after onLoaded");
        Assert.AreEqual(AppMode.VrEditing, sut.CurrentMode);
    }

    [Test]
    public void TransitionTo_SameMode_NoOp()
    {
        var bus = new EventBus();
        var fake = new FakeTransition();
        var sut = new ModeOrchestrator(bus, AllowAllGraph(), fake);
        sut.TransitionTo(AppMode.MainMenu); // already MainMenu
        Assert.IsNull(fake.LastScene);
    }

    [Test]
    public void TransitionTo_PublishesModeExiting_BeforeLoadCompletes()
    {
        var bus = new EventBus();
        var fake = new FakeTransition();
        var sut = new ModeOrchestrator(bus, AllowAllGraph(), fake);

        ModeExitingEvent? exiting = null;
        bool changed = false;
        bus.Subscribe<ModeExitingEvent>(e => exiting = e);
        bus.Subscribe<ModeChangedEvent>(_ => changed = true);

        sut.TransitionTo(AppMode.VrEditing); // from default MainMenu; load not completed (FakeTransition holds onLoaded)

        // The exit event fires synchronously during TransitionTo, while the outgoing scene is still
        // loaded — BEFORE the scene-load callback (ModeChangedEvent) runs.
        Assert.IsTrue(exiting.HasValue, "ModeExitingEvent must fire synchronously, before the scene load completes");
        Assert.AreEqual(AppMode.MainMenu,  exiting.Value.From);
        Assert.AreEqual(AppMode.VrEditing, exiting.Value.To);
        Assert.IsFalse(changed, "ModeChangedEvent must not fire until onLoaded runs");
    }
}
