using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Per-type runtime construction helper for Rig: loads the static mesh AND builds the proxy-bone
// hierarchy. BuildProxyRig is the single construction core, invoked from RigEntityBuilder.RestoreAsync
// (import + builtin) and RigRuntime.ApplyDefinition (manual rigging). The factory is a shared
// singleton, so proxies are built into LOCALS and handed to a per-rig ProxyRigRuntime.
public class RigEntityFactory
{
    private readonly GltfModelLoader _loader;
    private readonly ProxyRigConfig  _config;

    public RigEntityFactory(GltfModelLoader loader, ProxyRigConfig config)
    {
        _loader = loader;
        _config = config;
    }

    public Task<GameObject> CreateAsync(string absolutePath, Vector3 position, Quaternion rotation, CancellationToken ct)
        => _loader.LoadAsync(absolutePath, position, rotation, ct);

    // Builds the proxy hierarchy onto rigRoot and attaches a bound ProxyRigRuntime.
    // boneNames: from recipe.rig (import) → mapped to live bones by name; null → all SkinnedMeshRenderer.bones
    // (builtin / manual rigging). No-op if there is no skeleton.
    public void BuildProxyRig(GameObject rigRoot, IReadOnlyList<string> boneNames, TerminalBoneAxis terminalAxis, bool invertAxis)
    {
        var transforms = ResolveTransforms(rigRoot, boneNames);
        if (transforms == null || transforms.Length == 0) return;

        var proxyGOs    = new List<GameObject>();
        Transform proxyRoot = null;

        var set = new HashSet<Transform>(transforms);
        set.Remove(null);

        foreach (var bone in transforms)
        {
            if (bone == null) continue;
            if (set.Contains(bone.parent)) continue; // not a root bone of the selected set
            if (bone.parent == null)       continue;

            if (proxyRoot == null)
            {
                var armature    = bone.parent;
                var grandParent = armature.parent;
                var rig = new GameObject("ProxyRig");
                rig.transform.SetParent(grandParent, worldPositionStays: false);
                rig.transform.localPosition = armature.localPosition;
                rig.transform.localRotation = armature.localRotation;
                rig.transform.localScale    = armature.localScale;
                proxyRoot = rig.transform;
            }

            BuildProxyNode(bone, proxyRoot, set, proxyGOs, terminalAxis, invertAxis);
        }

        if (proxyRoot == null) return; // skeleton present but no buildable root bone

        var runtime = rigRoot.GetComponent<ProxyRigRuntime>() ?? rigRoot.AddComponent<ProxyRigRuntime>();
        runtime.Bind(proxyRoot, proxyGOs);
    }

    private Transform[] ResolveTransforms(GameObject rigRoot, IReadOnlyList<string> boneNames)
    {
        var smr = rigRoot.GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true);
        if (smr == null || smr.bones == null || smr.bones.Length == 0) return null;

        if (boneNames == null || boneNames.Count == 0)
            return smr.bones;

        var wanted = new HashSet<string>(boneNames);
        return smr.bones.Where(b => b != null && wanted.Contains(b.name)).ToArray();
    }

    private void BuildProxyNode(Transform bone, Transform proxyParent, HashSet<Transform> set, List<GameObject> proxyGOs, TerminalBoneAxis terminalAxis, bool invertAxis)
    {
        var children = new List<Transform>();
        for (int i = 0; i < bone.childCount; i++)
        {
            var c = bone.GetChild(i);
            if (set.Contains(c)) children.Add(c);
        }

        Mesh mesh;
        if (children.Count > 0)
        {
            mesh = BuildCombinedDiamondMesh(bone, children, _config.BoneWidth);
        }
        else
        {
            // Length always follows the bone's offset from its parent; only the DIRECTION is configurable.
            var worldDir    = bone.position - bone.parent.position;
            float parentLen = Mathf.Max(worldDir.magnitude, 0.0001f);
            float length    = parentLen * 0.5f;

            Vector3 localLongAxis;
            if (terminalAxis == TerminalBoneAxis.Auto)
            {
                localLongAxis = bone.InverseTransformDirection(worldDir).normalized;
                if (localLongAxis.sqrMagnitude < 0.0001f) localLongAxis = Vector3.up;
            }
            else
            {
                localLongAxis = terminalAxis switch
                {
                    TerminalBoneAxis.X => Vector3.right,
                    TerminalBoneAxis.Y => Vector3.up,
                    TerminalBoneAxis.Z => Vector3.forward,
                    _                  => Vector3.up,
                };
                if (invertAxis) localLongAxis = -localLongAxis;
            }

            float width = EffectiveWidth(_config.BoneWidth, length);
            mesh = BuildOrientedDiamondMesh(localLongAxis, length, width);
        }

        var proxyGo = new GameObject($"proxy_{bone.name}");
        proxyGo.transform.SetParent(proxyParent, worldPositionStays: false);
        proxyGo.transform.SetPositionAndRotation(bone.position, bone.rotation);
        proxyGo.transform.localScale = Vector3.one;

        proxyGo.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = proxyGo.AddComponent<MeshRenderer>();
        if (_config.BoneMaterial == null)
            Debug.LogWarning("RigEntityFactory: ProxyRigConfig.BoneMaterial not assigned — proxy renders outline-only.");
        mr.sharedMaterial = _config.BoneMaterial;

        var outline          = proxyGo.AddComponent<Outline>();
        outline.OutlineMode  = Outline.Mode.SilhouetteOnly;
        outline.OutlineWidth = 3f;

        if (_config.UseConvexCollider)
        {
            var mc = proxyGo.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
            mc.convex     = true;
        }
        else
        {
            var col = proxyGo.AddComponent<CapsuleCollider>();
            col.direction = 1; col.height = 1f; col.radius = 0.5f;
        }

        var sceneNode = proxyGo.AddComponent<SceneNode>();
        sceneNode.Init(bone.name, default, bone.name);
        proxyGo.AddComponent<BoneSceneNodeMarker>();
        proxyGo.AddComponent<Selectable>();
        proxyGo.AddComponent<XRPromeonInteractable>().SetInteractionLayer(InteractionLayer.BoneProxies);

        proxyGOs.Add(proxyGo);

        foreach (var stale in bone.GetComponents<BoneFollower>())
            UnityEngine.Object.Destroy(stale);
        bone.gameObject.AddComponent<BoneFollower>().SetProxy(proxyGo.transform);

        foreach (var child in children)
            BuildProxyNode(child, proxyGo.transform, set, proxyGOs, terminalAxis, invertAxis);
    }

    // ---- Static mesh builders (moved verbatim from the original proxy-rig builder) ----

    private static float EffectiveWidth(float boneWidth, float length) =>
        Mathf.Min(boneWidth, length * 0.2f);

    private static Mesh BuildOrientedDiamondMesh(Vector3 localLongAxis, float length, float width)
    {
        var rot = Quaternion.FromToRotation(Vector3.up, localLongAxis.normalized);
        var baseVerts = new[]
        {
            new Vector3( 0f,    0f,    0f),
            new Vector3( 0.5f,  0.15f, 0f),
            new Vector3(-0.5f,  0.15f, 0f),
            new Vector3( 0f,    0.15f, 0.5f),
            new Vector3( 0f,    0.15f,-0.5f),
            new Vector3( 0f,    1f,    0f),
        };
        var verts = new Vector3[baseVerts.Length];
        for (int i = 0; i < baseVerts.Length; i++)
        {
            var v = baseVerts[i];
            v = new Vector3(v.x * width, v.y * length, v.z * width);
            verts[i] = rot * v;
        }
        var mesh = new Mesh { name = "PromeonBoneDiamond" };
        mesh.vertices  = verts;
        mesh.triangles = new[] { 0,1,3, 0,3,2, 0,2,4, 0,4,1, 1,5,3, 3,5,2, 2,5,4, 4,5,1 };
        mesh.RecalculateNormals();
        return mesh;
    }

    private static Mesh BuildCombinedDiamondMesh(Transform bone, List<Transform> children, float boneWidth)
    {
        var allVerts = new List<Vector3>();
        var allTris  = new List<int>();
        foreach (var child in children)
        {
            var worldDir = child.position - bone.position;
            float length        = Mathf.Max(worldDir.magnitude, 0.0001f);
            Vector3 localChildDir = bone.InverseTransformDirection(worldDir).normalized;
            if (localChildDir.sqrMagnitude < 0.0001f) localChildDir = Vector3.up;
            float width = EffectiveWidth(boneWidth, length);
            var rot = Quaternion.FromToRotation(Vector3.up, localChildDir);

            int baseIdx = allVerts.Count;
            var baseVerts = new[]
            {
                new Vector3( 0f,    0f,    0f),
                new Vector3( 0.5f,  0.15f, 0f),
                new Vector3(-0.5f,  0.15f, 0f),
                new Vector3( 0f,    0.15f, 0.5f),
                new Vector3( 0f,    0.15f,-0.5f),
                new Vector3( 0f,    1f,    0f),
            };
            foreach (var v in baseVerts)
            {
                var scaled = new Vector3(v.x * width, v.y * length, v.z * width);
                allVerts.Add(rot * scaled);
            }
            int[] tris = { 0,1,3, 0,3,2, 0,2,4, 0,4,1, 1,5,3, 3,5,2, 2,5,4, 4,5,1 };
            foreach (var t in tris) allTris.Add(t + baseIdx);
        }
        var mesh = new Mesh { name = "PromeonBoneDiamond" };
        mesh.vertices  = allVerts.ToArray();
        mesh.triangles = allTris.ToArray();
        mesh.RecalculateNormals();
        return mesh;
    }
}
