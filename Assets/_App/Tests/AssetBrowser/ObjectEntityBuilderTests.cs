using NUnit.Framework;

public class ObjectEntityBuilderTests
{
    [Test]
    public void HandledTypes_AreDistinct()
    {
        var obj = new ObjectEntityBuilder(null, null, null);
        var rig = new RigEntityBuilder(null, null, null);
        Assert.AreEqual(AssetType.Object, obj.HandledType);
        Assert.AreEqual(AssetType.Rig,    rig.HandledType);
    }
}
