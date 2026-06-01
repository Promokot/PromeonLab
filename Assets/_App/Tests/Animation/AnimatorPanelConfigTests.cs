using NUnit.Framework;
using UnityEngine;

public class AnimatorPanelConfigTests
{
    [Test]
    public void Config_ExposesMetricDefaults()
    {
        var c = ScriptableObject.CreateInstance<AnimatorPanelConfig>();
        Assert.AreEqual(22f, c.KeySize,         0.001f);
        Assert.AreEqual(26f, c.KeySizeSelected, 0.001f);
        Assert.AreEqual(24f, c.MajorTickHeight, 0.001f);
        Assert.AreEqual(16f, c.MinorTickHeight, 0.001f);
        Assert.AreEqual(52f, c.RowHeight,       0.001f);
        Object.DestroyImmediate(c);
    }
}
