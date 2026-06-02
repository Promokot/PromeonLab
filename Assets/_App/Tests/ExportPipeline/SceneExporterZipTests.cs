using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using NUnit.Framework;
using UnityEngine;

public class SceneExporterZipTests
{
    [Test]
    public void WriteZipBundle_ContainsSceneJson_AndDedupedModels()
    {
        var tempDir  = Path.Combine(Path.GetTempPath(), "promeon_zip_" + Path.GetRandomFileName());
        var srcGlb   = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".glb");
        File.WriteAllBytes(srcGlb, new byte[] { 9, 9, 9, 9 });
        var zipPath  = Path.Combine(tempDir, "out.zip");

        var bundle = new SceneBundle { exportedAtUtc = "utc" };
        bundle.scene.id = "s1";
        bundle.nodes.Add(new SceneBundle.Node { nodeId = "n1", assetId = "a1", position = Vector3.zero });
        bundle.nodes.Add(new SceneBundle.Node { nodeId = "n2", assetId = "a1", position = Vector3.zero });
        var json = JsonUtility.ToJson(bundle, true);

        // Two sources with the SAME entry path -> writer must dedup to one zip entry.
        var sources = new List<SceneExporter.SourceFile>
        {
            new SceneExporter.SourceFile { EntryPath = "models/a1.glb", AbsolutePath = srcGlb },
            new SceneExporter.SourceFile { EntryPath = "models/a1.glb", AbsolutePath = srcGlb },
        };

        SceneExporter.WriteZipBundle(zipPath, json, sources);

        Assert.IsTrue(File.Exists(zipPath), "zip must exist");
        // Open via ZipArchive over a FileStream (System.IO.Compression core) — avoids depending on
        // ZipFile, which lives in the separate System.IO.Compression.FileSystem assembly.
        using (var fs  = File.OpenRead(zipPath))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Read))
        {
            Assert.IsNotNull(zip.GetEntry("scene.json"), "scene.json entry missing");
            Assert.AreEqual(1, CountEntries(zip, "models/a1.glb"), "model must be bundled once");

            var sceneEntry = zip.GetEntry("scene.json");
            using var reader = new StreamReader(sceneEntry.Open());
            var back = JsonUtility.FromJson<SceneBundle>(reader.ReadToEnd());
            Assert.AreEqual("s1", back.scene.id);
            Assert.AreEqual(2, back.nodes.Count);
        }

        Directory.Delete(tempDir, true);
        File.Delete(srcGlb);
    }

    private static int CountEntries(ZipArchive zip, string entryName)
    {
        int n = 0;
        foreach (var e in zip.Entries) if (e.FullName == entryName) n++;
        return n;
    }
}
