using NUnit.Framework;
using UnityEditor;

public class NavBarConfigExporterRegionTests
{
    [Test]
    public void NavBarConfig_Contains_ExporterRegion_VisibleInVrEditing()
    {
        var guids = AssetDatabase.FindAssets("t:NavBarConfig");
        Assert.IsNotEmpty(guids, "no NavBarConfig asset found");

        var path = AssetDatabase.GUIDToAssetPath(guids[0]);
        var config = AssetDatabase.LoadAssetAtPath<NavBarConfig>(path);
        Assert.IsNotNull(config, $"failed to load NavBarConfig at {path}");

        Assert.IsTrue(config.TryGetEntry("exporter", out _), "NavBarConfig must contain an 'exporter' region");
        Assert.IsTrue(config.IsVisibleInMode("exporter", AppMode.VrEditing),
            "'exporter' region must be visible in VrEditing");
    }
}
