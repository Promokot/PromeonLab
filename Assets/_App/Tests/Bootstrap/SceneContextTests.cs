using NUnit.Framework;

public class SceneContextTests
{
    // SceneGraph's constructor only stores its dependencies, so nulls are safe for a
    // reference-only test double. (We never call any SceneGraph method here.)
    private static SceneGraph MakeGraph() => new SceneGraph(new EventBus(), null, null, null, null);

    [Test]
    public void New_HasNoScene()
    {
        var ctx = new SceneContext();

        Assert.IsFalse(ctx.HasScene);
        Assert.IsNull(ctx.Graph);
        Assert.IsNull(ctx.Selection);
    }

    [Test]
    public void Bind_WithGraph_SetsHasSceneAndExposesServices()
    {
        var ctx   = new SceneContext();
        var graph = MakeGraph();

        ctx.Bind(graph, null, null, null);

        Assert.IsTrue(ctx.HasScene);
        Assert.AreSame(graph, ctx.Graph);
        Assert.IsNull(ctx.Selection);
    }

    [Test]
    public void Clear_NullsEverythingAndHasNoScene()
    {
        var ctx = new SceneContext();
        ctx.Bind(MakeGraph(), null, null, null);

        ctx.Clear();

        Assert.IsFalse(ctx.HasScene);
        Assert.IsNull(ctx.Graph);
    }
}
