using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Editor-only: пишет docs/controls-bindings.md из ControlsProfile.asset.
// Держит «файл, к которому возвращаемся» синхронным с единым источником правды.
public static class ControlsProfileExporter
{
    private const string ProfilePath = "Assets/_App/Content/ScriptableObjects/ControlsProfile.asset";
    private const string OutputRelative = "../docs/controls-bindings.md"; // относительно Assets/

    [MenuItem("Tools/Promeon/Export Controls Doc")]
    public static void Export()
    {
        var profile = AssetDatabase.LoadAssetAtPath<ControlsProfile>(ProfilePath);
        if (profile == null)
        {
            EditorUtility.DisplayDialog("Export Controls Doc",
                $"ControlsProfile not found at:\n{ProfilePath}", "OK");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("# Controls — Bindings Map");
        sb.AppendLine();
        sb.AppendLine($"_Generated from `{ProfilePath}` (schemaVersion {profile.SchemaVersion}). " +
                      "Do not edit by hand — edit the ControlsProfile asset and re-run " +
                      "`Tools/Promeon/Export Controls Doc`._");
        sb.AppendLine();

        ControlBindingCategory? group = null;
        foreach (var b in profile.Bindings)
        {
            if (group != b.Category)
            {
                group = b.Category;
                sb.AppendLine();
                sb.AppendLine($"## {b.Category}");
                sb.AppendLine();
                sb.AppendLine("| Action | Hand | Input | Description |");
                sb.AppendLine("|---|---|---|---|");
            }
            sb.AppendLine($"| {b.Action} | {b.Hand} | {b.InputLabel} | {b.Description} |");
        }

        var outputPath = Path.GetFullPath(Path.Combine(Application.dataPath, OutputRelative));
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        File.WriteAllText(outputPath, sb.ToString());
        Debug.Log($"[ControlsProfileExporter] Wrote {outputPath}");
        EditorUtility.RevealInFinder(outputPath);
    }
}
