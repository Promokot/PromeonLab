# Scene Export Bundle Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Export the current VR scene as a self-contained `.zip` bundle (`scene.json` + copied model/texture sources) into `Documents/{productName}/`, reachable from a new UserPanel tab.

**Architecture:** A plain `[Serializable]` DTO `SceneBundle` defines the external JSON schema. `SceneExporter` (already root-registered) captures live state from the root `SceneContext` façade, resolves each node's asset through `IAssetRegistry` + `PathProvider`, runs a pure `static BuildBundle(...)` transform, serializes to JSON on the main thread, then writes the zip on a thread-pool thread. The UI tab is built by copying the existing `AssetBrowserModule` prefab and swapping its script for `ExportPanel`.

**Tech Stack:** Unity 6000.3.7f1, C#, VContainer, custom `EventBus`, `JsonUtility`, `System.IO.Compression.ZipArchive`, NUnit (Unity Test Runner, EditMode). Compile/verify through the Unity MCP tools.

---

## CRITICAL PROJECT RULES (read before starting)

- **NEVER run any `git` command.** The user commits manually. Every "Checkpoint" below means *stop and let the human commit* — do not stage, commit, or branch. This overrides the writing-plans default.
- **Compile/test via Unity MCP**, not a shell build:
  - After editing scripts: `mcp__unityMCP__refresh_unity` (force/compile), then `mcp__unityMCP__read_console` filtered to `error` — there must be **no `CS####`** errors before proceeding.
  - Run EditMode tests: `mcp__unityMCP__run_tests` (mode `EditMode`) then poll `mcp__unityMCP__get_test_job`.
  - **Allowed pre-existing failures** (do NOT treat as regressions): `PathProviderTests` ×4, `RingRotateStrategyTests` ×2. Any *other* failure is yours to fix.
- Prefab/ScriptableObject authoring (Tasks 6–7) is done through the Unity MCP tools / editor. Pause between scene/prefab mutations; keep MCP requests atomic.
- No namespaces on runtime gameplay code. `[SerializeField] private` on MonoBehaviour fields. One public type per file (nested types inside a DTO are fine).

---

## File Structure

| File | New/Modify | Responsibility |
|---|---|---|
| `Assets/_App/Scripts/ExportPipeline/SceneBundle.cs` | **New** | `[Serializable]` external schema (DTO) + nested `SceneRef`/`Node`/`Animation`/`Track`. Reuses `BonePose`, `AnimKeyData`. |
| `Assets/_App/Scripts/Animation/AnimationAuthoring.cs` | Modify | Add `public SceneAnimationData CaptureForExport()` read accessor. |
| `Assets/_App/Scripts/ExportPipeline/SceneExporter.cs` | **Rewrite** | Nested `AssetResolution`/`SourceFile` types; `static BuildBundle(...)`; `static WriteZipBundle(...)`; orchestration pulling from `SceneContext`/`IAssetRegistry`/`PathProvider`. |
| `Assets/_App/Scripts/SpatialUi/Panels/ExportPanel.cs` | Modify | Nothing logic-wise; path label becomes `.zip` automatically via exporter. (Confirm no `.json` literal.) |
| `Assets/_App/Content/Prefabs/UI/Panels/UserPanel/ExportModule.prefab` | **New (MCP)** | Copy of `AssetBrowserModule`, script swapped to `ExportPanel`, five fields wired, `RegionMember{export}`. |
| `Assets/_App/Content/ScriptableObjects/NavBarConfig.asset` | Modify (MCP) | Add region `"export"`. |
| `Assets/_App/Tests/ExportPipeline/SceneBundleTests.cs` | **New** | JsonUtility round-trip. |
| `Assets/_App/Tests/ExportPipeline/SceneExporterBuildTests.cs` | **New** | `BuildBundle` node/animation/dedup/missing-source. |
| `Assets/_App/Tests/ExportPipeline/SceneExporterZipTests.cs` | **New** | Zip round-trip to temp dir. |
| `Assets/_App/Tests/Animation/AnimationAuthoringExportTests.cs` | **New** | `CaptureForExport` returns live data. |
| `Assets/_App/Tests/SpatialUi/ExportModulePrefabTests.cs` | **New** | Prefab has `ExportPanel` + wired fields. |

**No new `InternalsVisibleTo` file is needed.** `_App.Runtime` already declares `[assembly: InternalsVisibleTo("_App.Tests")]` (in `Assets/_App/Scripts/Animation/InternalsVisibleTo.cs`); that attribute is assembly-wide, so `internal` members of `SceneExporter` are already visible to `_App.Tests`.

---

## Task 1: `SceneBundle` DTO

**Files:**
- Create: `Assets/_App/Scripts/ExportPipeline/SceneBundle.cs`
- Test: `Assets/_App/Tests/ExportPipeline/SceneBundleTests.cs`

> **Schema representation note:** Vectors/quaternions serialize in Unity's `JsonUtility` object form
> (`{"x":..,"y":..,"z":..}`), not as arrays — because we reuse `BonePose`/`AnimKeyData` (which hold
> `Vector3`/`Quaternion`) and keep the node transform consistent with them. JsonUtility cannot emit
> `null` for a nested `[Serializable]` class, so a node with no animation serializes as an empty
> `animation` object with `totalFrames:0` and empty `tracks`; the in-memory field is still `null`
> (which is what the unit tests assert). The external tool treats empty `tracks` as "no animation".

- [ ] **Step 1: Write the failing test**

Create `Assets/_App/Tests/ExportPipeline/SceneBundleTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

public class SceneBundleTests
{
    [Test]
    public void SceneBundle_RoundTrips_ThroughJsonUtility()
    {
        var bundle = new SceneBundle
        {
            exportedAtUtc = "2026-06-02T00:00:00Z",
            fps = 30,
        };
        bundle.scene.id = "scene1";
        bundle.scene.name = "My Scene";
        bundle.nodes.Add(new SceneBundle.Node
        {
            nodeId = "n1",
            displayName = "Hero",
            assetSource = "Imported",
            assetId = "a1",
            assetType = "Rig",
            geometryFile = "models/a1.glb",
            geometryMissing = false,
            position = new Vector3(1, 2, 3),
            rotation = Quaternion.identity,
            scale = Vector3.one,
        });

        var json = JsonUtility.ToJson(bundle);
        var back = JsonUtility.FromJson<SceneBundle>(json);

        Assert.AreEqual(1, back.schemaVersion);
        Assert.AreEqual(30, back.fps);
        Assert.AreEqual("scene1", back.scene.id);
        Assert.AreEqual(1, back.nodes.Count);
        Assert.AreEqual("models/a1.glb", back.nodes[0].geometryFile);
        Assert.AreEqual(new Vector3(1, 2, 3), back.nodes[0].position);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Refresh Unity (`mcp__unityMCP__refresh_unity`), then `read_console` (errors): expect compile error `CS0246: The type or namespace name 'SceneBundle' could not be found`.

- [ ] **Step 3: Create the DTO**

Create `Assets/_App/Scripts/ExportPipeline/SceneBundle.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// External-facing scene description written as `scene.json` inside the export bundle.
/// One-way snapshot for an outside tool — NOT a re-importable PromeonLab format.
/// Reuses BonePose and AnimKeyData so the bundle does not duplicate those shapes.
/// </summary>
[Serializable]
public class SceneBundle
{
    public int       schemaVersion = 1;
    public string    exportedAtUtc;
    public SceneRef  scene = new();
    public int       fps = 24;
    public List<Node> nodes = new();

    [Serializable]
    public class SceneRef
    {
        public string id;
        public string name;
    }

    [Serializable]
    public class Node
    {
        public string     nodeId;
        public string     displayName;
        public string     parentNodeId;
        public string     assetSource;     // "Imported" | "Builtin"
        public string     assetId;
        public string     assetType;       // "Object" | "Rig" | "Reference"
        public string     geometryFile;    // "models/{id}.glb" / "textures/{id}.png", or "" when missing
        public bool       geometryMissing; // true when no source file was bundled (e.g. Builtin)
        public Vector3    position;
        public Quaternion rotation;
        public Vector3    scale;
        public List<BonePose> bonePoses = new();
        public Animation  animation;       // null when the node has no ActionContainer
    }

    [Serializable]
    public class Animation
    {
        public int    totalFrames;
        public string interpolation;       // "Linear" | "Stepped"
        public bool   loop;
        public List<Track> tracks = new();
    }

    [Serializable]
    public class Track
    {
        public string targetNodeId;        // object track = node id; bone track = "bone:{node}:{bone}"
        public List<AnimKeyData> keys = new();
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Refresh Unity, `read_console` (no `CS####`), then `run_tests` EditMode filtered to `SceneBundleTests`; poll `get_test_job`. Expect PASS (plus only the allowed pre-existing failures if running the full suite).

- [ ] **Step 5: Checkpoint** — stop; the user commits. (Do not run git.)

---

## Task 2: `AnimationAuthoring.CaptureForExport()`

**Files:**
- Modify: `Assets/_App/Scripts/Animation/AnimationAuthoring.cs`
- Test: `Assets/_App/Tests/Animation/AnimationAuthoringExportTests.cs`

- [ ] **Step 1: Write the failing test**

Create `Assets/_App/Tests/Animation/AnimationAuthoringExportTests.cs`:

```csharp
using NUnit.Framework;

public class AnimationAuthoringExportTests
{
    [Test]
    public void CaptureForExport_ReturnsLiveData_WithCreatedContainer()
    {
        var authoring = new AnimationAuthoring(null, null, null, null, new EventBus());
        authoring.InitForTest();
        authoring.CreateContainer("node1", 50, 24);

        var data = authoring.CaptureForExport();

        Assert.IsNotNull(data);
        Assert.IsNotNull(data.FindByOwner("node1"));
        Assert.AreEqual(50, data.FindByOwner("node1").TotalFrames);
    }

    [Test]
    public void CaptureForExport_BeforeAnyData_IsNull()
    {
        var authoring = new AnimationAuthoring(null, null, null, null, new EventBus());
        // No InitForTest(), no container → _data not yet allocated.
        Assert.IsNull(authoring.CaptureForExport());
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Refresh Unity, `read_console`: expect `CS1061` / `CS0117` — `AnimationAuthoring` has no `CaptureForExport`.

- [ ] **Step 3: Add the accessor**

In `Assets/_App/Scripts/Animation/AnimationAuthoring.cs`, add this method next to the other public read accessors (immediately after `GetContainer`, around line 49):

```csharp
    /// <summary>
    /// Read-only access to the live scene animation data for export. Returns null when no
    /// animation data has been created/loaded yet (e.g. Sandbox, or an untouched scene).
    /// </summary>
    public SceneAnimationData CaptureForExport() => _data;
```

- [ ] **Step 4: Run the test to verify it passes**

Refresh Unity, `read_console` (no `CS####`), `run_tests` EditMode filtered to `AnimationAuthoringExportTests`. Expect PASS.

- [ ] **Step 5: Checkpoint** — stop; the user commits.

---

## Task 3: `SceneExporter.BuildBundle` (pure transform)

This is the core. We add the nested types and the `static` transform first, with the orchestration rewritten in Task 5. To keep Task 3 compiling, we rewrite `SceneExporter.cs` fully here (orchestration included but calling the new build) — Task 5 only touches the zip-writing wiring and panel label. To avoid a half-built file, **Task 3 delivers the complete rewritten `SceneExporter.cs`**, and Tasks 4–5 verify/extend its pieces with their own tests.

**Files:**
- Rewrite: `Assets/_App/Scripts/ExportPipeline/SceneExporter.cs`
- Test: `Assets/_App/Tests/ExportPipeline/SceneExporterBuildTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `Assets/_App/Tests/ExportPipeline/SceneExporterBuildTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class SceneExporterBuildTests
{
    private static SceneData OneNodeScene(string nodeId, AssetSource source, string assetId,
                                          IList<BonePose> poses = null)
    {
        var nd = new NodeData
        {
            NodeId = nodeId,
            AssetRef = new AssetRef { Source = source, AssetId = assetId },
            Position = new Vector3(1, 0, 0),
            Rotation = Quaternion.identity,
            Scale = Vector3.one,
            DisplayName = nodeId,
            BonePoses = poses != null ? new List<BonePose>(poses) : new List<BonePose>(),
        };
        return new SceneData { SceneId = "s1", DisplayName = "Scene 1", Nodes = { nd } };
    }

    // Resolver helper: Imported with a fake .glb that "exists".
    private static SceneExporter.AssetResolution Imported(AssetType type, string path) =>
        new SceneExporter.AssetResolution
        { Source = AssetSource.Imported, Type = type, SourcePath = path, SourceExists = true };

    [Test]
    public void BuildBundle_ImportedObject_SetsGeometryFile_AndSourceEntry()
    {
        var scene = OneNodeScene("n1", AssetSource.Imported, "a1");
        var (bundle, sources) = SceneExporter.BuildBundle(
            scene, null,
            _ => Imported(AssetType.Object, "C:/fake/a1.glb"),
            "utc");

        Assert.AreEqual(1, bundle.nodes.Count);
        var node = bundle.nodes[0];
        Assert.AreEqual("Imported", node.assetSource);
        Assert.AreEqual("Object", node.assetType);
        Assert.AreEqual("models/a1.glb", node.geometryFile);
        Assert.IsFalse(node.geometryMissing);
        Assert.AreEqual(1, sources.Count);
        Assert.AreEqual("models/a1.glb", sources[0].EntryPath);
        Assert.AreEqual("C:/fake/a1.glb", sources[0].AbsolutePath);
    }

    [Test]
    public void BuildBundle_ImportedReference_GoesToTexturesFolder()
    {
        var scene = OneNodeScene("n1", AssetSource.Imported, "img1");
        var (bundle, sources) = SceneExporter.BuildBundle(
            scene, null,
            _ => Imported(AssetType.Reference, "C:/fake/img1.png"),
            "utc");

        Assert.AreEqual("textures/img1.png", bundle.nodes[0].geometryFile);
        Assert.AreEqual("textures/img1.png", sources[0].EntryPath);
    }

    [Test]
    public void BuildBundle_Builtin_FlagsGeometryMissing_NoSource()
    {
        var scene = OneNodeScene("n1", AssetSource.Builtin, "cube");
        var (bundle, sources) = SceneExporter.BuildBundle(
            scene, null,
            _ => new SceneExporter.AssetResolution
            { Source = AssetSource.Builtin, Type = AssetType.Object, SourcePath = null, SourceExists = false },
            "utc");

        Assert.AreEqual("Builtin", bundle.nodes[0].assetSource);
        Assert.IsTrue(bundle.nodes[0].geometryMissing);
        Assert.AreEqual("", bundle.nodes[0].geometryFile);
        Assert.AreEqual(0, sources.Count);
    }

    [Test]
    public void BuildBundle_BonePoses_AreCarried()
    {
        var poses = new[] { new BonePose { BoneName = "pelvis", LocalPosition = new Vector3(0, 1, 0),
                                           LocalRotation = Quaternion.identity, LocalScale = Vector3.one } };
        var scene = OneNodeScene("n1", AssetSource.Imported, "a1", poses);
        var (bundle, _) = SceneExporter.BuildBundle(
            scene, null, _ => Imported(AssetType.Rig, "C:/fake/a1.glb"), "utc");

        Assert.AreEqual(1, bundle.nodes[0].bonePoses.Count);
        Assert.AreEqual("pelvis", bundle.nodes[0].bonePoses[0].BoneName);
    }

    [Test]
    public void BuildBundle_Animation_MappedForMatchingOwner_NullOtherwise()
    {
        var scene = OneNodeScene("n1", AssetSource.Imported, "a1");
        scene.Nodes.Add(new NodeData
        {
            NodeId = "n2", AssetRef = new AssetRef { Source = AssetSource.Imported, AssetId = "a2" },
            Position = Vector3.zero, Rotation = Quaternion.identity, Scale = Vector3.one,
            DisplayName = "n2", BonePoses = new List<BonePose>(),
        });

        var anim = new SceneAnimationData { Fps = 30 };
        var c = anim.CreateContainer("n1", 40, 30);
        c.Interpolation = InterpolationMode.Stepped;
        c.Loop = true;
        var track = c.GetOrCreateTrack("n1");
        track.UpsertKey(0, Vector3.zero, Quaternion.identity, Vector3.one);
        track.UpsertKey(10, new Vector3(1, 0, 0), Quaternion.identity, Vector3.one);

        var (bundle, _) = SceneExporter.BuildBundle(
            scene, anim, _ => Imported(AssetType.Object, "C:/fake/x.glb"), "utc");

        Assert.AreEqual(30, bundle.fps);
        var n1 = bundle.nodes.Find(n => n.nodeId == "n1");
        Assert.IsNotNull(n1.animation);
        Assert.AreEqual(40, n1.animation.totalFrames);
        Assert.AreEqual("Stepped", n1.animation.interpolation);
        Assert.IsTrue(n1.animation.loop);
        Assert.AreEqual(1, n1.animation.tracks.Count);
        Assert.AreEqual(2, n1.animation.tracks[0].keys.Count);

        var n2 = bundle.nodes.Find(n => n.nodeId == "n2");
        Assert.IsNull(n2.animation);
    }

    [Test]
    public void BuildBundle_ImportedButSourceMissing_FlagsGeometryMissing_NoSource()
    {
        var scene = OneNodeScene("n1", AssetSource.Imported, "gone");
        var (bundle, sources) = SceneExporter.BuildBundle(
            scene, null,
            _ => new SceneExporter.AssetResolution
            { Source = AssetSource.Imported, Type = AssetType.Object, SourcePath = "C:/missing/gone.glb", SourceExists = false },
            "utc");

        Assert.IsTrue(bundle.nodes[0].geometryMissing, "absent imported source must flag geometryMissing");
        Assert.AreEqual("", bundle.nodes[0].geometryFile);
        Assert.AreEqual(0, sources.Count, "missing source must not be added to the bundle list");
    }

    [Test]
    public void BuildBundle_SameAssetTwice_DedupsSourceList()
    {
        var scene = OneNodeScene("n1", AssetSource.Imported, "a1");
        scene.Nodes.Add(new NodeData
        {
            NodeId = "n2", AssetRef = new AssetRef { Source = AssetSource.Imported, AssetId = "a1" },
            Position = Vector3.zero, Rotation = Quaternion.identity, Scale = Vector3.one,
            DisplayName = "n2", BonePoses = new List<BonePose>(),
        });

        var (_, sources) = SceneExporter.BuildBundle(
            scene, null, _ => Imported(AssetType.Object, "C:/fake/a1.glb"), "utc");

        Assert.AreEqual(1, sources.Count, "same assetId must be bundled once");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Refresh Unity, `read_console`: expect `CS0117`/`CS1061` — `SceneExporter.BuildBundle` / `SceneExporter.AssetResolution` / `SceneExporter.SourceFile` do not exist yet.

- [ ] **Step 3: Rewrite `SceneExporter.cs`**

Replace the entire contents of `Assets/_App/Scripts/ExportPipeline/SceneExporter.cs` with:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

/// <summary>
/// App-lifetime service that exports the current scene as a self-contained ZIP bundle
/// (`scene.json` + copied model/texture sources) under {MyDocuments}/{productName}/{name}.zip.
/// Subscribes to SceneExportRequestedEvent, publishes SceneExportedEvent with the result.
///
/// Live state is read through the root SceneContext façade (Graph + Authoring). Asset source files
/// are resolved through IAssetRegistry + PathProvider. The pure transform lives in BuildBundle.
/// </summary>
public class SceneExporter : IStartable, IDisposable
{
    private readonly EventBus       _bus;
    private readonly AppStorage     _storage;
    private readonly SceneContext   _ctx;
    private readonly IAssetRegistry _registry;
    private readonly PathProvider   _paths;

    public SceneExporter(EventBus bus, AppStorage storage, SceneContext ctx,
                         IAssetRegistry registry, PathProvider paths)
    {
        _bus      = bus;
        _storage  = storage;
        _ctx      = ctx;
        _registry = registry;
        _paths    = paths;
    }

    void IStartable.Start()    => _bus.Subscribe<SceneExportRequestedEvent>(OnExportRequested);
    void IDisposable.Dispose() => _bus.Unsubscribe<SceneExportRequestedEvent>(OnExportRequested);

    // -------------------------------------------------------------------------
    // Public API (panel uses BuildTargetPath for the live path label)
    // -------------------------------------------------------------------------

    public string BuildTargetPath(string fileName)
    {
        var safe = SanitizeFileName(fileName);
        if (string.IsNullOrEmpty(safe)) safe = "export";
        return Path.Combine(ExportDirectory(), safe + ".zip");
    }

    // -------------------------------------------------------------------------
    // Pure transform — internal so EditMode tests can call it directly.
    // -------------------------------------------------------------------------

    /// What the asset resolver returns for a node's AssetRef.
    public struct AssetResolution
    {
        public AssetSource Source;
        public AssetType   Type;
        public string      SourcePath;   // absolute path to the source file, or null
        public bool        SourceExists; // true when SourcePath points at a real file
    }

    /// A file to place inside the zip: EntryPath (e.g. "models/a1.glb") ← AbsolutePath on disk.
    public struct SourceFile
    {
        public string EntryPath;
        public string AbsolutePath;
    }

    internal static (SceneBundle bundle, List<SourceFile> sources) BuildBundle(
        SceneData scene,
        SceneAnimationData anim,
        Func<AssetRef, AssetResolution> resolve,
        string exportedAtUtc)
    {
        var bundle = new SceneBundle
        {
            schemaVersion = 1,
            exportedAtUtc = exportedAtUtc,
            fps           = anim?.Fps ?? 24,
        };
        bundle.scene.id   = scene?.SceneId ?? "none";
        bundle.scene.name = scene?.DisplayName ?? "Unknown";

        var sources = new List<SourceFile>();
        var seenEntries = new HashSet<string>();

        if (scene?.Nodes != null)
        {
            foreach (var nd in scene.Nodes)
            {
                var res = resolve(nd.AssetRef);
                var node = new SceneBundle.Node
                {
                    nodeId          = nd.NodeId,
                    displayName     = nd.DisplayName,
                    parentNodeId    = nd.ParentNodeId ?? "",
                    assetSource     = res.Source.ToString(),
                    assetId         = nd.AssetRef.AssetId,
                    assetType       = res.Type.ToString(),
                    position        = nd.Position,
                    rotation        = nd.Rotation,
                    scale           = nd.Scale,
                    bonePoses       = nd.BonePoses != null
                                        ? new List<BonePose>(nd.BonePoses)
                                        : new List<BonePose>(),
                    animation       = BuildAnimation(anim, nd.NodeId),
                };

                if (res.Source == AssetSource.Imported && res.SourceExists
                    && !string.IsNullOrEmpty(res.SourcePath))
                {
                    var folder = res.Type == AssetType.Reference ? "textures" : "models";
                    var ext    = Path.GetExtension(res.SourcePath);
                    var entry  = $"{folder}/{nd.AssetRef.AssetId}{ext}";
                    node.geometryFile    = entry;
                    node.geometryMissing = false;
                    if (seenEntries.Add(entry))
                        sources.Add(new SourceFile { EntryPath = entry, AbsolutePath = res.SourcePath });
                }
                else
                {
                    node.geometryFile    = "";
                    node.geometryMissing = true;
                }

                bundle.nodes.Add(node);
            }
        }

        return (bundle, sources);
    }

    private static SceneBundle.Animation BuildAnimation(SceneAnimationData anim, string ownerNodeId)
    {
        var container = anim?.FindByOwner(ownerNodeId);
        if (container == null) return null;

        var block = new SceneBundle.Animation
        {
            totalFrames   = container.TotalFrames,
            interpolation = container.Interpolation.ToString(),
            loop          = container.Loop,
        };
        foreach (var t in container.Tracks)
        {
            block.tracks.Add(new SceneBundle.Track
            {
                targetNodeId = t.NodeId,
                keys         = t.Keys != null ? new List<AnimKeyData>(t.Keys) : new List<AnimKeyData>(),
            });
        }
        return block;
    }

    // -------------------------------------------------------------------------
    // Zip writing — pure file IO (no Unity API), safe on a thread-pool thread.
    // -------------------------------------------------------------------------

    internal static void WriteZipBundle(string zipPath, string sceneJson, IReadOnlyList<SourceFile> sources)
    {
        var dir = Path.GetDirectoryName(zipPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        if (File.Exists(zipPath)) File.Delete(zipPath);

        using var fs  = new FileStream(zipPath, FileMode.CreateNew);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

        var jsonEntry = zip.CreateEntry("scene.json");
        using (var w = new StreamWriter(jsonEntry.Open()))
            w.Write(sceneJson);

        var seen = new HashSet<string>();
        foreach (var s in sources)
        {
            if (!seen.Add(s.EntryPath)) continue;
            if (!File.Exists(s.AbsolutePath)) continue;
            var entry = zip.CreateEntry(s.EntryPath);
            using var es  = entry.Open();
            using var src = File.OpenRead(s.AbsolutePath);
            src.CopyTo(es);
        }
    }

    // -------------------------------------------------------------------------
    // Orchestration
    // -------------------------------------------------------------------------

    private void OnExportRequested(SceneExportRequestedEvent e) => _ = RunExportAsync(e.FileName);

    private async Task RunExportAsync(string fileName)
    {
        try
        {
            if (!_ctx.HasScene)
            {
                _bus.Publish(new SceneExportedEvent
                { Path = "", Success = false, Message = "Export failed: no active scene." });
                return;
            }

            var path = BuildTargetPath(fileName);

            // --- main thread: capture live state + resolve sources ---
            var sceneId   = _storage.ActiveSceneId ?? "none";
            var cached    = !string.IsNullOrEmpty(sceneId) ? _storage.GetCachedScene(sceneId) : null;
            var display   = cached?.DisplayName ?? "Unknown";
            var createdAt = cached?.CreatedAt ?? DateTime.UtcNow.ToString("o");

            var sceneData = _ctx.Graph.CaptureSnapshot(sceneId, display, createdAt);
            var anim      = _ctx.Authoring != null ? _ctx.Authoring.CaptureForExport() : null;

            var (bundle, sources) = BuildBundle(sceneData, anim, Resolve, DateTime.UtcNow.ToString("o"));
            var json = JsonUtility.ToJson(bundle, prettyPrint: true);

            // --- thread pool: write the zip (pure file IO) ---
            await Task.Run(() => WriteZipBundle(path, json, sources));

            int missing = bundle.nodes.FindAll(n => n.geometryMissing).Count;
            var msg = $"Saved to {path}";
            if (missing > 0) msg += $" ({missing} node(s) without geometry)";

            _bus.Publish(new SceneExportedEvent { Path = path, Success = true, Message = msg });
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SceneExporter] Export failed: {ex}");
            _bus.Publish(new SceneExportedEvent
            { Path = "", Success = false, Message = $"Export failed: {ex.Message}" });
        }
    }

    private AssetResolution Resolve(AssetRef r)
    {
        var asset = _registry.Find(r);
        if (asset == null)
            return new AssetResolution
            { Source = r.Source, Type = AssetType.Object, SourcePath = null, SourceExists = false };

        string abs = string.IsNullOrEmpty(asset.SourceRef)
            ? null
            : Path.Combine(_paths.RootForSources, asset.SourceRef);
        bool exists = !string.IsNullOrEmpty(abs) && File.Exists(abs);

        return new AssetResolution
        { Source = asset.Source, Type = asset.Type, SourcePath = abs, SourceExists = exists };
    }

    // -------------------------------------------------------------------------
    // Internals
    // -------------------------------------------------------------------------

    private static string ExportDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Application.productName);

    private static string SanitizeFileName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        if (raw.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) raw = raw[..^4];
        var invalid = new string(Path.GetInvalidFileNameChars());
        var pattern = $"[{Regex.Escape(invalid)}]";
        return Regex.Replace(raw, pattern, "_").Trim();
    }
}
```

> **Note on `public struct AssetResolution`/`SourceFile`:** they are nested public types on `SceneExporter`
> (one public top-level type per file is preserved — these are nested). Tests reference them as
> `SceneExporter.AssetResolution` / `SceneExporter.SourceFile`. `BuildBundle`/`WriteZipBundle` are
> `internal static` and reachable from `_App.Tests` via the existing assembly `InternalsVisibleTo`.
>
> **Compression assembly:** the code uses only `System.IO.Compression.ZipArchive` (core
> `System.IO.Compression.dll`), which Unity's .NET Standard 2.1 profile provides — no asmdef change
> expected. If the console reports `ZipArchive` unresolved, add an `Assets/csc.rsp` line
> `-r:System.IO.Compression.dll` and re-refresh; do **not** switch to `ZipFile`/`FileSystem`.

- [ ] **Step 4: Run the tests to verify they pass**

Refresh Unity, `read_console` (no `CS####`), `run_tests` EditMode filtered to `SceneExporterBuildTests`. Expect all 7 PASS.

- [ ] **Step 5: Checkpoint** — stop; the user commits.

---

## Task 4: Zip round-trip test

The zip writer was implemented in Task 3. This task locks its behavior with an IO test (write → reopen → assert entries + deserialize `scene.json`).

**Files:**
- Test: `Assets/_App/Tests/ExportPipeline/SceneExporterZipTests.cs`

- [ ] **Step 1: Write the test**

Create `Assets/_App/Tests/ExportPipeline/SceneExporterZipTests.cs`:

```csharp
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

        // Two sources with the SAME entry path → writer must dedup to one zip entry.
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
```

- [ ] **Step 2: Run the test to verify it fails (or passes immediately)**

`run_tests` EditMode filtered to `SceneExporterZipTests`. Because `WriteZipBundle` already exists from Task 3, this should PASS on first run; if it fails, fix `WriteZipBundle`, not the test. (Writing the test after the impl here is acceptable — Task 3's writer is the "implementation"; this task is a characterization test for the IO path that can't be unit-tested purely.)

- [ ] **Step 3: Checkpoint** — stop; the user commits.

---

## Task 5: Panel `.zip` label confirmation

The path label already calls `BuildTargetPath`, which now returns `.zip`. Confirm `ExportPanel.cs` carries no `.json` literal and the "Exporting…" / result flow is intact.

**Files:**
- Inspect (modify only if a `.json` literal exists): `Assets/_App/Scripts/SpatialUi/Panels/ExportPanel.cs`

- [ ] **Step 1: Inspect the panel**

`mcp__unityMCP__find_in_file` (or Read) `Assets/_App/Scripts/SpatialUi/Panels/ExportPanel.cs` for the substring `.json`. Expected: **no matches** (the panel reads the path from `_exporter.BuildTargetPath`). If a literal `.json` exists anywhere in a user-facing string, change it to `.zip`.

- [ ] **Step 2: Verify compile**

Refresh Unity, `read_console`: no `CS####`. Run the **full** EditMode suite (`run_tests` EditMode, poll `get_test_job`). Expect green except the allowed pre-existing failures (`PathProviderTests` ×4, `RingRotateStrategyTests` ×2).

- [ ] **Step 3: Checkpoint** — stop; the user commits. **End of the code phase — the exporter is fully functional headlessly; remaining tasks make it reachable from the UI.**

---

## Task 6: `ExportModule` prefab (Unity MCP / editor)

Build the panel by copying the working asset-browser panel and swapping its script. Visual layout is the user's job — this task only needs the **five fields wired** and the `ExportPanel` script present.

**Files:**
- Create (MCP): `Assets/_App/Content/Prefabs/UI/Panels/UserPanel/ExportModule.prefab`
- Test: `Assets/_App/Tests/SpatialUi/ExportModulePrefabTests.cs`

- [ ] **Step 1: Write the failing prefab-validation test**

Create `Assets/_App/Tests/SpatialUi/ExportModulePrefabTests.cs`:

```csharp
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
    public void ExportModule_HasRegionMember_Export()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.IsNotNull(root, $"prefab not found at {PrefabPath}");

        var member = root.GetComponentInChildren<RegionMember>(true);
        Assert.IsNotNull(member, "RegionMember missing");
        // ModuleId is a serialized field on RegionMember — read it via SerializedObject.
        var so = new SerializedObject(member);
        var moduleId = so.FindProperty("_moduleId") ?? so.FindProperty("ModuleId");
        Assert.IsNotNull(moduleId, "RegionMember has no module-id serialized field");
        Assert.AreEqual("export", moduleId.stringValue);
    }
}
```

> Before authoring, open `Assets/_App/Scripts/SpatialUi/.../RegionMember.cs` to confirm the exact
> serialized field name for the module id (the test tries `_moduleId` then `ModuleId`). If it differs,
> adjust the `FindProperty` name in the test to match.

- [ ] **Step 2: Run the test to verify it fails**

`run_tests` EditMode filtered to `ExportModulePrefabTests`. Expect FAIL (`prefab not found`).

- [ ] **Step 3: Author the prefab via MCP**

In the Unity editor (via MCP `manage_prefabs` / `manage_gameobject` / `manage_components`):

1. Open `Assets/_App/Content/Prefabs/UI/Panels/UserPanel/UserPanel.prefab` and locate the existing
   `AssetBrowserModule` instance under the panels container.
2. Duplicate that GameObject, **unpack the prefab instance completely** (Prefab → Unpack Completely).
3. **Remove the `AssetBrowserPanel` component** and any asset-browser-only sub-components/children
   (grid, file-browser hooks) that are clearly browser-specific. Keep the Canvas root +
   `TrackedDeviceGraphicRaycaster` + a simple body container.
4. **Add the `ExportPanel` component** to the panel root.
5. Ensure these child UI objects exist and assign them to the `ExportPanel` serialized fields:
   - `_fileNameInput` → a `TMP_InputField` (add a `VrInputFieldProxy` to it so the VR keyboard sees it),
   - `_pathLabel`, `_sceneNameLabel`, `_statusLabel` → `TMP_Text`,
   - `_exportButton` → a `Button`.
6. Add a `RegionMember` component to the root; set its module-id field to `"export"`.
7. Save the GameObject as a new prefab at
   `Assets/_App/Content/Prefabs/UI/Panels/UserPanel/ExportModule.prefab`, and keep an instance of it in
   the UserPanel hierarchy (same level as `AnimatorPanelModule`).

Pause and verify in the editor that the five fields show assigned references in the Inspector.

- [ ] **Step 4: Run the test to verify it passes**

Refresh Unity, `read_console` (no errors), `run_tests` EditMode filtered to `ExportModulePrefabTests`. Expect both PASS.

- [ ] **Step 5: Checkpoint** — stop; the user commits.

---

## Task 7: NavBar tab + nav button (Unity MCP / editor)

Register the `"export"` region and give it a nav button so the tab appears in VrEditing.

**Files:**
- Modify (MCP): `Assets/_App/Content/ScriptableObjects/NavBarConfig.asset`
- Modify (MCP): the UserPanel nav-bar (duplicate the Gizmo Tools `RegionNavButton`)
- Test: `Assets/_App/Tests/SpatialUi/NavBarConfigExportRegionTests.cs`

> First, open `NavBarConfig.cs` and `RegionNavButton.cs` (under `Scripts/SpatialUi/`) to confirm the
> exact field names used below (region list field, `RegionId`/`ModuleId`, `VisibleInModes`). Adjust the
> test's `FindProperty` calls if the serialized names differ.

- [ ] **Step 1: Write the failing test**

Create `Assets/_App/Tests/SpatialUi/NavBarConfigExportRegionTests.cs`:

```csharp
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class NavBarConfigExportRegionTests
{
    [Test]
    public void NavBarConfig_Contains_ExportRegion()
    {
        // Locate the single NavBarConfig asset in the project.
        var guids = AssetDatabase.FindAssets("t:NavBarConfig");
        Assert.IsNotEmpty(guids, "no NavBarConfig asset found");

        var path = AssetDatabase.GUIDToAssetPath(guids[0]);
        var config = AssetDatabase.LoadAssetAtPath<NavBarConfig>(path);
        Assert.IsNotNull(config, $"failed to load NavBarConfig at {path}");

        // The config exposes its regions; assert one of them is the "export" module.
        var so = new SerializedObject(config);
        bool found = HasExportRegion(so);
        Assert.IsTrue(found, "NavBarConfig must contain a region with module id 'export'");
    }

    // Walks the serialized region list looking for a string property valued "export".
    private static bool HasExportRegion(SerializedObject so)
    {
        var it = so.GetIterator();
        while (it.Next(true))
        {
            if (it.propertyType == SerializedPropertyType.String && it.stringValue == "export")
                return true;
        }
        return false;
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

`run_tests` EditMode filtered to `NavBarConfigExportRegionTests`. Expect FAIL (no `export` region yet). If it errors on the `NavBarConfig` type name, correct the type in the test to the real ScriptableObject class name found in `Scripts/SpatialUi/`.

- [ ] **Step 3: Configure via MCP**

1. In `NavBarConfig.asset`, add a new region entry: module id `"export"`, a Label (e.g. "Export") and an
   Icon sprite (reuse any existing nav icon for now — the user re-skins later), and set its visible-modes
   to include **VrEditing** (omit MainMenu/Sandbox).
2. In the UserPanel nav bar, **duplicate the Gizmo Tools `RegionNavButton`** GameObject in the same
   button group; set its region id field to `"export"`. Point its Label/Icon at the same assets chosen
   above. No scope code changes are required — `RootLifetimeScope.RegisterBuildCallback` already loops
   over every `RegionNavButton` and `RegionMember` and registers them (`RegisterButton` / `RegisterModule`).

Pause; verify in the editor that the new button is present in the nav group and the config lists the
`export` region.

- [ ] **Step 4: Run the test to verify it passes**

Refresh Unity, `read_console` (no errors), `run_tests` EditMode filtered to `NavBarConfigExportRegionTests`. Expect PASS. Then run the **full** EditMode suite; expect green except the allowed pre-existing failures.

- [ ] **Step 5: Checkpoint** — stop; the user commits.

---

## Task 8: In-headset verification (user-performed)

Not an automated task — hand back to the user with this checklist:

- [ ] The **Export** tab appears in the UserPanel nav bar in VR Editing.
- [ ] Opening it shows the scene name, a file-name input, a live path label ending in `.zip`, and an
      Export button.
- [ ] Typing a name with the VR keyboard updates the path label.
- [ ] Tapping **Export** writes `Documents/{productName}/{name}.zip`; unzipping it on a PC shows
      `scene.json` plus `models/*.glb` (and `textures/*` if the scene uses imported images).
- [ ] `scene.json` lists every node with transform, bone poses, and animation; Builtin nodes carry
      `geometryMissing: true`.

Visual polish of the panel layout is the user's to finish.

---

## Notes carried from the spec

- **Out of scope:** re-import, baking builtin geometry, FBX, `ErrorDispatcher` (errors still go to
  `Debug.LogError` + `SceneExportedEvent.Message`).
- **Sandbox:** `SceneContext.Authoring` is null there; `BuildBundle` is called with `anim == null` and
  simply omits animation blocks. Export tab is VrEditing-only anyway.
