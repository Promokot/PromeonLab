using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class ExportModulePrefabTests
{
    private const string PrefabPath =
        "Assets/_App/Content/Prefabs/UI/Panels/UserPanel/ExportModule.prefab";

    [Test]
    public void ExportModule_HasExportPanel_WithAllFieldsWired()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.IsNotNull(root, $"prefab not found at {PrefabPath}");

        var panel = root.GetComponentInChildren<ExportPanel>(true);
        Assert.IsNotNull(panel, "ExportPanel component missing");

        var so = new SerializedObject(panel);
        foreach (var field in new[]
                 { "_fileNameInput", "_pathLabel", "_sceneNameLabel", "_exportButton", "_statusLabel" })
        {
            var prop = so.FindProperty(field);
            Assert.IsNotNull(prop, $"serialized field {field} not found");
            Assert.IsNotNull(prop.objectReferenceValue, $"field {field} is not wired in the prefab");
        }
    }

    [Test]
    public void ExportModule_HasRegionMember_Exporter()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.IsNotNull(root, $"prefab not found at {PrefabPath}");

        var member = root.GetComponentInChildren<RegionMember>(true);
        Assert.IsNotNull(member, "RegionMember missing");
        Assert.AreEqual("exporter", member.ModuleId);
    }
}
