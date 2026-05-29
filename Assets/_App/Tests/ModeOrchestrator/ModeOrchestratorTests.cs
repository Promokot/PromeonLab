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
}
