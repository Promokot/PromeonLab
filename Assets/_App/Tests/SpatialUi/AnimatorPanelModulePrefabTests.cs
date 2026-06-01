using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class AnimatorPanelModulePrefabTests
{
    private const string PrefabPath =
        "Assets/_App/Content/Prefabs/UI/Panels/UserPanel/AnimatorPanelModule.prefab";

    [Test]
    public void LanesContent_HasVerticalLayoutGroup()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.IsNotNull(root, $"prefab not found at {PrefabPath}");

        var lanes = root.transform.Find(
            "ActiveStateRoot/Body/TimelineColumn/TimelineScroll/Viewport/TimelineContent/LanesContent");
        Assert.IsNotNull(lanes, "LanesContent path not found");

        var vlg = lanes.GetComponent<VerticalLayoutGroup>();
        Assert.IsNotNull(vlg, "LanesContent must have a VerticalLayoutGroup");
        Assert.IsTrue(vlg.childForceExpandWidth, "lanes must stretch to full timeline width");
        Assert.IsFalse(vlg.childControlHeight, "lane height comes from each lane's LayoutElement");
    }
}
