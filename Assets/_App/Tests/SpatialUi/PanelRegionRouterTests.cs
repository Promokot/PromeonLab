using NUnit.Framework;
using System.Collections.Generic;

public class PanelRegionRouterTests
{
    private class FakeConfig : IRegionConfig
    {
        public readonly Dictionary<string, string> Regions = new();
        public readonly Dictionary<string, AppMode[]> Visible = new();
        public readonly Dictionary<string, string> Defaults = new();
        public bool TryGetRegion(string id, out string region) => Regions.TryGetValue(id, out region);
        public bool IsVisibleInMode(string id, AppMode mode)
        {
            if (!Visible.TryGetValue(id, out var modes) || modes == null) return false;
            foreach (var m in modes) if (m == mode) return true;
            return false;
        }
        public bool TryGetRegionDefault(string regionKey, out string moduleId) => Defaults.TryGetValue(regionKey, out moduleId);
    }

    private class FakeSurface : IRegionSurface
    {
        public int ShowCalls, HideCalls;
        public bool IsOpen { get; private set; }
        public void Show() { ShowCalls++; IsOpen = true; }
        public void Hide() { HideCalls++; IsOpen = false; }
    }

    private EventBus _bus;
    private FakeConfig _config;
    private PanelRegionRouter _sut;

    [SetUp]
    public void SetUp()
    {
        _bus = new EventBus();
        _config = new FakeConfig();
        _sut = new PanelRegionRouter(_config, _bus);
    }

    [Test]
    public void Open_ShowsModule()
    {
        _config.Regions["a"] = "body";
        var a = new FakeSurface();
        _sut.RegisterModule("a", a);
        _sut.Open("a");
        Assert.IsTrue(a.IsOpen);
        Assert.AreEqual(1, a.ShowCalls);
    }

    [Test]
    public void Open_SecondInSameRegion_HidesFirst()
    {
        _config.Regions["a"] = "body";
        _config.Regions["b"] = "body";
        var a = new FakeSurface(); var b = new FakeSurface();
        _sut.RegisterModule("a", a); _sut.RegisterModule("b", b);
        _sut.Open("a");
        _sut.Open("b");
        Assert.IsFalse(a.IsOpen);
        Assert.IsTrue(b.IsOpen);
    }

    [Test]
    public void Open_DifferentRegion_LeavesFirstOpen()
    {
        _config.Regions["a"] = "body";
        _config.Regions["dialog"] = "dialog";
        var a = new FakeSurface(); var d = new FakeSurface();
        _sut.RegisterModule("a", a); _sut.RegisterModule("dialog", d);
        _sut.Open("a");
        _sut.Open("dialog");
        Assert.IsTrue(a.IsOpen);
        Assert.IsTrue(d.IsOpen);
    }

    [Test]
    public void Toggle_OpensThenCloses()
    {
        _config.Regions["a"] = "body";
        var a = new FakeSurface();
        _sut.RegisterModule("a", a);
        _sut.Toggle("a");
        Assert.IsTrue(a.IsOpen);
        _sut.Toggle("a");
        Assert.IsFalse(a.IsOpen);
    }

    [Test]
    public void Open_AfterClose_ReopensInSameRegion()
    {
        _config.Regions["a"] = "body";
        _config.Regions["b"] = "body";
        var a = new FakeSurface(); var b = new FakeSurface();
        _sut.RegisterModule("a", a); _sut.RegisterModule("b", b);
        _sut.Open("a");
        _sut.Close("a");
        _sut.Open("b");
        Assert.IsTrue(b.IsOpen);
        Assert.IsFalse(a.IsOpen);
    }

    [Test]
    public void Open_PublishesRegionChangedEvent()
    {
        _config.Regions["a"] = "body";
        RegionChangedEvent received = default; bool fired = false;
        _bus.Subscribe<RegionChangedEvent>(e => { received = e; fired = true; });
        var a = new FakeSurface();
        _sut.RegisterModule("a", a);
        _sut.Open("a");
        Assert.IsTrue(fired);
        Assert.AreEqual("body", received.RegionKey);
        Assert.AreEqual("a", received.OpenModuleId);
    }

    [Test]
    public void ModeChanged_ClosesModuleNotVisibleInNewMode()
    {
        _config.Regions["a"] = "body";
        _config.Visible["a"] = new[] { AppMode.VrEditing };
        var a = new FakeSurface();
        _sut.RegisterModule("a", a);
        _sut.Open("a");
        _bus.Publish(new ModeChangedEvent { CurrentMode = AppMode.MainMenu });
        Assert.IsFalse(a.IsOpen);
    }

    [Test]
    public void ModeChanged_KeepsModuleVisibleInNewMode()
    {
        _config.Regions["a"] = "body";
        _config.Visible["a"] = new[] { AppMode.VrEditing, AppMode.MainMenu };
        var a = new FakeSurface();
        _sut.RegisterModule("a", a);
        _sut.Open("a");
        _bus.Publish(new ModeChangedEvent { CurrentMode = AppMode.MainMenu });
        Assert.IsTrue(a.IsOpen);
    }

    [Test]
    public void Close_ReopensRegionDefault()
    {
        _config.Regions["def"] = "overlays";
        _config.Regions["kb"] = "overlays";
        _config.Defaults["overlays"] = "def";
        var def = new FakeSurface(); var kb = new FakeSurface();
        _sut.RegisterModule("def", def); _sut.RegisterModule("kb", kb);
        _sut.Open("def");
        _sut.Open("kb");
        Assert.IsFalse(def.IsOpen);
        Assert.IsTrue(kb.IsOpen);
        _sut.Close("kb");
        Assert.IsFalse(kb.IsOpen);
        Assert.IsTrue(def.IsOpen);
    }

    [Test]
    public void Close_Default_DoesNotRecurse()
    {
        _config.Regions["def"] = "overlays";
        _config.Defaults["overlays"] = "def";
        var def = new FakeSurface();
        _sut.RegisterModule("def", def);
        _sut.Open("def");
        _sut.Close("def");
        Assert.IsFalse(def.IsOpen);
    }

    [Test]
    public void ApplyMode_OpensRegionDefault_WhenRegionEmptyAndDefaultVisible()
    {
        _config.Regions["def"] = "overlays";
        _config.Defaults["overlays"] = "def";
        _config.Visible["def"] = new[] { AppMode.MainMenu, AppMode.VrEditing };
        var def = new FakeSurface();
        _sut.RegisterModule("def", def);
        Assert.IsFalse(def.IsOpen); // starts closed (member inactive in prefab)

        _sut.ApplyMode(AppMode.MainMenu);

        Assert.IsTrue(def.IsOpen);
    }

    [Test]
    public void ApplyMode_DoesNotOpenRegionDefault_WhenDefaultNotVisibleInMode()
    {
        _config.Regions["def"] = "overlays";
        _config.Defaults["overlays"] = "def";
        _config.Visible["def"] = new[] { AppMode.VrEditing };
        var def = new FakeSurface();
        _sut.RegisterModule("def", def);

        _sut.ApplyMode(AppMode.MainMenu);

        Assert.IsFalse(def.IsOpen);
    }

    [Test]
    public void ApplyMode_DoesNotForceDefault_WhenNonDefaultModuleAlreadyOpen()
    {
        _config.Regions["def"] = "overlays";
        _config.Regions["kb"] = "overlays";
        _config.Defaults["overlays"] = "def";
        _config.Visible["def"] = new[] { AppMode.MainMenu };
        _config.Visible["kb"]  = new[] { AppMode.MainMenu };
        var def = new FakeSurface(); var kb = new FakeSurface();
        _sut.RegisterModule("def", def); _sut.RegisterModule("kb", kb);
        _sut.Open("kb");

        _sut.ApplyMode(AppMode.MainMenu);

        Assert.IsTrue(kb.IsOpen);
        Assert.IsFalse(def.IsOpen);
    }
}
