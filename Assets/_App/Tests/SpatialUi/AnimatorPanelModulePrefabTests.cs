using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class AnimatorPanelModulePrefabTests
{
    private const string PrefabPath =
        "Assets/_App/Content/Prefabs/UI/Panels/UserPanel/AnimatorPanelModule.prefab";

    [Test]
    public void TimelineRowPrefab_HasNameAndKeyStrip()
    {
        var row = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_App/Content/Prefabs/UI/Elements/TimelineRow.prefab");
        Assert.IsNotNull(row, "TimelineRow.prefab missing");
        Assert.IsNotNull(row.GetComponent<TimelineRow>(), "TimelineRow component missing");
        Assert.IsNotNull(row.transform.Find("NameSegment"), "NameSegment missing");
        Assert.IsNotNull(row.transform.Find("KeyStrip"), "KeyStrip missing");
    }

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

    [Test]
    public void AnimatorModule_NoScrollSync_NoTracksColumn()
    {
        var root = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_App/Content/Prefabs/UI/Panels/UserPanel/AnimatorPanelModule.prefab");
        bool hasSync = root.GetComponentsInChildren<MonoBehaviour>(true)
            .Any(m => m != null && m.GetType().Name == "TimelineScrollSync");
        Assert.IsFalse(hasSync, "TimelineScrollSync must be removed from the prefab");
        Assert.IsNull(root.transform.Find("ActiveStateRoot/Body/TracksColumn"), "TracksColumn must be deleted");
    }
}
