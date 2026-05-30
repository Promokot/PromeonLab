using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;

[AddComponentMenu("PromeonLab/Promeon Proxy Rig Builder")]
public class PromeonProxyRigBuilder : MonoBehaviour
{
    [SerializeField] private Material _boneMaterial;
    [SerializeField] private float    _boneWidth                = 0.06f;
    [SerializeField] private bool     _useConvexCollider        = true;
    [SerializeField] private Color    _boneOutlineColorDefault  = Color.white;
    [SerializeField] private Color    _boneOutlineColorSelected = new Color(1f, 0.5f, 0f);
    [SerializeField] private Collider      _rootCollider;
    [SerializeField] private OutlineConfig _outlineConfig;

    private Transform[] _transforms;
    private string      _rigNodeId;
    private EventBus    _eventBus;

    private readonly List<GameObject>   _proxyGOs    = new();
    private readonly List<BoneFollower> _followers   = new();
    private readonly List<Mesh>         _proxyMeshes = new();
    private Transform _proxyRoot;

    public IReadOnlyList<GameObject> ProxyGOs => _proxyGOs;

    void Awake()
    {
        // No automatic Rebuild — proxies are baked into the prefab.
        // OnEnable handles re-population of _proxyGOs from baked children.
    }

    void OnEnable()
    {
        if (_proxyGOs.Count == 0)
        {
            var proxyRoot = transform.Find("ProxyRig");
            if (proxyRoot == null) return;
            _proxyRoot = proxyRoot;
            foreach (var marker in proxyRoot.GetComponentsInChildren<BoneSceneNodeMarker>(includeInactive: true))
                _proxyGOs.Add(marker.gameObject);
        }

        // Procedurally generated meshes (assigned in editor Rebuild) do NOT survive prefab save/load —
        // Unity serializes null fileID for un-named runtime meshes. Re-create them here from the live
        // bone transforms. We don't touch GO transforms or any other component state, so animation/pose
        // is preserved.
        RegenerateMissingProxyMeshes();
    }

    void OnDestroy()
    {
        if (_eventBus != null) _eventBus.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);
        _eventBus = null;
        DestroyBoneGOs();
    }

    [Inject]
    public void Construct(EventBus bus)
    {
        if (_eventBus == bus) return;
        if (_eventBus != null) _eventBus.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);
        _eventBus = bus;
        if (_eventBus != null) _eventBus.Subscribe<SelectionChangedEvent>(OnSelectionChanged);
    }

    public void SetTransforms(Transform[] transforms) => _transforms   = transforms;
    public void SetMaterial(Material material)        => _boneMaterial = material;
    public void SetRigNodeId(string rigNodeId)        => _rigNodeId    = rigNodeId; // kept for backwards compatibility; ignored at bake time
    public void SetRootCollider(Collider rootCollider) => _rootCollider = rootCollider;

    public void Rebuild()
    {
        DestroyBoneGOs();
        var transforms = ResolveTransforms();
        if (transforms == null || transforms.Length == 0) return;
        BuildProxyHierarchy(transforms);
        SetBonesInteractive(false);
    }

    public void SetVisualsEnabled(bool enabled)
    {
        foreach (var go in _proxyGOs)
        {
            if (go == null) continue;
            var mr      = go.GetComponent<MeshRenderer>();
            if (mr      != null) mr.enabled      = enabled;
            var outline = go.GetComponent<Outline>();
            if (outline != null) outline.enabled = enabled;
        }
    }

    public void SetBonesInteractive(bool enabled)
    {
        // Force-activate the proxy hierarchy. If the baked prefab serialized any proxy GO (or the
        // ProxyRig parent) as inactive, just toggling Behaviour.enabled on its components does
        // nothing — Unity skips Update/render for inactive GameObjects.
        if (enabled && _proxyRoot != null && !_proxyRoot.gameObject.activeSelf)
            _proxyRoot.gameObject.SetActive(true);

        foreach (var go in _proxyGOs)
        {
            if (go == null) continue;
            if (enabled && !go.activeSelf) go.SetActive(true);
            var mr      = go.GetComponent<MeshRenderer>();
            if (mr      != null) mr.enabled      = enabled;

            // Strip any baked-in / leftover outline materials BEFORE re-enabling Outline.
            // QuickOutline.OnEnable appends outlineMask + outlineFill without checking for
            // existing entries — duplicates accumulate across edit sessions or repeated toggles.
            // Stacked outline materials caused stencil-write conflicts that hid the outline until
            // a subsequent OnDisable/OnEnable cycle (triggered by clicking the bone) thinned them
            // back down.
            if (enabled && mr != null)
            {
                var current = mr.sharedMaterials;
                var cleaned = current.Where(m => m == null ||
                    (!m.name.StartsWith("OutlineMask") && !m.name.StartsWith("OutlineFill"))).ToArray();
                if (cleaned.Length != current.Length)
                    mr.materials = cleaned;
            }

            var outline = go.GetComponent<Outline>();
            if (outline != null)
            {
                // Supply the forked-shader materials before enabling so OnEnable's lazy build has sources.
                if (enabled && _outlineConfig != null)
                    outline.SetOutlineMaterials(_outlineConfig.MaskMaterial, _outlineConfig.FillMaterial);
                outline.enabled = enabled;
                if (enabled)
                {
                    // Re-enabling an Outline does NOT re-run Awake and OnEnable never sets needsUpdate,
                    // so the SilhouetteOnly ZTest stayed stale until a click poked a setter. Re-asserting
                    // the mode sets needsUpdate -> UpdateMaterialProperties runs this frame -> the
                    // see-through rim appears immediately (fixes "bone outline needs a click").
                    outline.OutlineMode    = Outline.Mode.SilhouetteOnly;
                    outline.RenderPriority = 1; // above the selected-mesh outline (priority 0)
                }
            }
            var col     = go.GetComponent<Collider>();
            if (col     != null) col.enabled     = enabled;

            // Brute-force renderer invalidation: writing sharedMaterial back to itself kicks the
            // render pipeline into re-evaluating this renderer. Without this, URP+XR was deferring
            // newly-enabled mesh renderers until an unrelated event (e.g. click) nudged the pipeline.
            if (enabled && mr != null && mr.sharedMaterial != null)
                mr.sharedMaterial = mr.sharedMaterial;
        }
        if (_rootCollider != null) _rootCollider.enabled = !enabled;

        if (enabled) ApplyBoneOutlineColors(null);

        // Forced disable→enable cycle after one frame: the first OnEnable on a freshly toggled
        // Outline did not light up the outline visually until an external event (click) nudged
        // the XR render pipeline. A second toggle cycle, one frame later, reliably triggers it.
        if (enabled && isActiveAndEnabled) StartCoroutine(BumpOutlineNextFrame());
    }

    private IEnumerator BumpOutlineNextFrame()
    {
        yield return null; // wait one full frame so the first enable cycle has rendered
        foreach (var go in _proxyGOs)
        {
            if (go == null) continue;
            var outline = go.GetComponent<Outline>();
            if (outline == null) continue;
            outline.enabled = false;
            outline.enabled = true;
        }
        ApplyBoneOutlineColors(null);
    }

    /// Walks each proxy GO and re-creates its diamond mesh if the MeshFilter has lost its sharedMesh
    /// reference (i.e. the procedural mesh did not survive prefab serialization). Uses the live
    /// SkinnedMeshRenderer.bones to derive each proxy's bone + children. Transforms and components
    /// are not touched.
    private void RegenerateMissingProxyMeshes()
    {
        var transforms = ResolveTransforms();
        if (transforms == null || transforms.Length == 0) return;

        var boneSet     = new HashSet<Transform>(transforms);
        boneSet.Remove(null);
        var bonesByName = new Dictionary<string, Transform>();
        foreach (var t in transforms)
            if (t != null && !bonesByName.ContainsKey(t.name)) bonesByName[t.name] = t;

        foreach (var proxyGo in _proxyGOs)
        {
            if (proxyGo == null) continue;
            var mf = proxyGo.GetComponent<MeshFilter>();
            if (mf == null) continue;
            if (mf.sharedMesh != null) continue; // already has mesh

            // Proxy GO is named "proxy_<boneName>" by BuildProxyNode.
            var name = proxyGo.name;
            var boneName = name.StartsWith("proxy_") ? name.Substring("proxy_".Length) : name;
            if (!bonesByName.TryGetValue(boneName, out var bone) || bone == null) continue;

            var children = new List<Transform>();
            for (int i = 0; i < bone.childCount; i++)
            {
                var c = bone.GetChild(i);
                if (boneSet.Contains(c)) children.Add(c);
            }

            Mesh mesh;
            if (children.Count > 0)
            {
                mesh = BuildCombinedDiamondMesh(bone, children);
            }
            else
            {
                if (bone.parent == null) continue;
                var worldDir    = bone.position - bone.parent.position;
                float parentLen = Mathf.Max(worldDir.magnitude, 0.0001f);
                float length    = parentLen * 0.5f;
                Vector3 localChildDir = bone.InverseTransformDirection(worldDir).normalized;
                if (localChildDir.sqrMagnitude < 0.0001f) localChildDir = Vector3.up;
                float width = EffectiveWidth(_boneWidth, length);
                mesh = BuildOrientedDiamondMesh(localChildDir, length, width);
            }

            _proxyMeshes.Add(mesh);
            mf.sharedMesh = mesh;
            var mc = proxyGo.GetComponent<MeshCollider>();
            if (mc != null) mc.sharedMesh = mesh;
        }
    }

    private void OnSelectionChanged(SelectionChangedEvent evt)
    {
        ApplyBoneOutlineColors(evt.SelectedNodeId);
    }

    private void ApplyBoneOutlineColors(string selectedId)
    {
        foreach (var go in _proxyGOs)
        {
            if (go == null) continue;
            var sn      = go.GetComponent<SceneNode>();
            var outline = go.GetComponent<Outline>();
            if (sn == null || outline == null) continue;
            outline.OutlineColor = sn.NodeId == selectedId
                ? _boneOutlineColorSelected
                : _boneOutlineColorDefault;
        }
    }

    void BuildProxyHierarchy(Transform[] transforms)
    {
        var set = new HashSet<Transform>(transforms);
        set.Remove(null);

        foreach (var bone in transforms)
        {
            if (bone == null) continue;
            if (set.Contains(bone.parent)) continue;   // not a root bone
            if (bone.parent == null)       continue;   // root bone at scene root — skip

            if (_proxyRoot == null)
            {
                var armature    = bone.parent;
                var grandParent = armature.parent;

                var rig = new GameObject("ProxyRig");
                rig.transform.SetParent(grandParent, worldPositionStays: false);
                rig.transform.localPosition = armature.localPosition;
                rig.transform.localRotation = armature.localRotation;
                rig.transform.localScale    = armature.localScale;
                _proxyRoot = rig.transform;
            }

            BuildProxyNode(bone, _proxyRoot, set);
        }
    }

    void BuildProxyNode(Transform bone, Transform proxyParent, HashSet<Transform> set)
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
            mesh = BuildCombinedDiamondMesh(bone, children);
        }
        else
        {
            var worldDir    = bone.position - bone.parent.position;
            float parentLen = Mathf.Max(worldDir.magnitude, 0.0001f);
            float length    = parentLen * 0.5f;
            Vector3 localChildDir = bone.InverseTransformDirection(worldDir).normalized;
            if (localChildDir.sqrMagnitude < 0.0001f) localChildDir = Vector3.up;
            float width = EffectiveWidth(_boneWidth, length);
            mesh = BuildOrientedDiamondMesh(localChildDir, length, width);
        }
        _proxyMeshes.Add(mesh);

        var proxyGo = new GameObject($"proxy_{bone.name}");
        proxyGo.transform.SetParent(proxyParent, worldPositionStays: false);
        proxyGo.transform.SetPositionAndRotation(bone.position, bone.rotation);
        proxyGo.transform.localScale = Vector3.one;

        AddMeshAndOutline(proxyGo, mesh);
        AddCollider(proxyGo, mesh);

        // SceneNode + bone marker — runtime SceneGraph rewrites NodeId into "bone:{rigId}:{boneName}".
        var sceneNode = proxyGo.AddComponent<SceneNode>();
        sceneNode.Init(bone.name, default, bone.name);
        proxyGo.AddComponent<BoneSceneNodeMarker>();

        // Interaction components — DI wired by IObjectResolver.InjectGameObject at spawn time.
        // Colliders auto-discover in XRPromeonInteractable.Awake (own GO only by default).
        proxyGo.AddComponent<Selectable>();
        proxyGo.AddComponent<XRPromeonInteractable>();

        _proxyGOs.Add(proxyGo);

        // Clean up stale followers that may have survived a domain reload.
        foreach (var stale in bone.GetComponents<BoneFollower>())
            DestroyObj(stale);

        var follower = bone.gameObject.AddComponent<BoneFollower>();
        follower.SetProxy(proxyGo.transform);
        _followers.Add(follower);

        foreach (var child in children)
            BuildProxyNode(child, proxyGo.transform, set);
    }

    Mesh BuildCombinedDiamondMesh(Transform bone, List<Transform> children)
    {
        var allVerts = new List<Vector3>();
        var allTris  = new List<int>();

        foreach (var child in children)
        {
            var worldDir = child.position - bone.position;
            float   length        = Mathf.Max(worldDir.magnitude, 0.0001f);
            Vector3 localChildDir = bone.InverseTransformDirection(worldDir).normalized;
            if (localChildDir.sqrMagnitude < 0.0001f) localChildDir = Vector3.up;
            float width = EffectiveWidth(_boneWidth, length);

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

            int[] tris = { 0, 1, 3,  0, 3, 2,  0, 2, 4,  0, 4, 1,
                           1, 5, 3,  3, 5, 2,  2, 5, 4,  4, 5, 1 };
            foreach (var t in tris) allTris.Add(t + baseIdx);
        }

        var mesh = new Mesh { name = "PromeonBoneDiamond" };
        mesh.vertices  = allVerts.ToArray();
        mesh.triangles = allTris.ToArray();
        mesh.RecalculateNormals();
        return mesh;
    }

    void AddCollider(GameObject go, Mesh mesh)
    {
        if (_useConvexCollider)
        {
            var mc        = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
            mc.convex     = true;
        }
        else
        {
            var col       = go.AddComponent<CapsuleCollider>();
            col.direction = 1;
            col.height    = 1f;
            col.radius    = 0.5f;
        }
    }

    void AddMeshAndOutline(GameObject go, Mesh mesh)
    {
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        if (_boneMaterial == null)
            Debug.LogWarning("[PromeonProxyRigBuilder] _boneMaterial not assigned.", this);
        mr.sharedMaterial    = _boneMaterial;

        var outline          = go.AddComponent<Outline>();
        outline.OutlineMode  = Outline.Mode.SilhouetteOnly;
        outline.OutlineColor = _boneOutlineColorDefault;
        outline.OutlineWidth = 3f;
    }

    void DestroyBoneGOs()
    {
        if (_proxyRoot != null)
        {
            DestroyObj(_proxyRoot.gameObject);
            _proxyRoot = null;
        }
        _proxyGOs.Clear();

        foreach (var f in _followers)
            if (f != null) DestroyObj(f);
        _followers.Clear();

        foreach (var m in _proxyMeshes)
            if (m != null) DestroyObj(m);
        _proxyMeshes.Clear();
    }

    private static void DestroyObj(Object obj)
    {
        if (Application.isPlaying) Destroy(obj);
        else                       DestroyImmediate(obj);
    }

    Transform[] ResolveTransforms()
    {
        if (_transforms != null && _transforms.Length > 0)
            return _transforms;

        var smr = GetComponentInChildren<SkinnedMeshRenderer>()
               ?? GetComponentInParent<SkinnedMeshRenderer>();
        if (smr != null && smr.bones.Length > 0)
            return smr.bones;

        Debug.LogWarning("[PromeonProxyRigBuilder] No transforms set and no SkinnedMeshRenderer found.", this);
        return null;
    }

    public static float EffectiveWidth(float boneWidth, float length) =>
        Mathf.Min(boneWidth, length * 0.2f);

    public static Mesh BuildDiamondMesh() => BuildOrientedDiamondMesh(Vector3.up, 1f, 1f);

    public static Mesh BuildOrientedDiamondMesh(Vector3 localLongAxis, float length, float width)
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
            var v    = baseVerts[i];
            v        = new Vector3(v.x * width, v.y * length, v.z * width);
            verts[i] = rot * v;
        }

        var mesh = new Mesh { name = "PromeonBoneDiamond" };
        mesh.vertices  = verts;
        mesh.triangles = new[]
        {
            0, 1, 3,  0, 3, 2,  0, 2, 4,  0, 4, 1,
            1, 5, 3,  3, 5, 2,  2, 5, 4,  4, 5, 1,
        };
        mesh.RecalculateNormals();
        return mesh;
    }

    public static (Transform start, Transform end)[] ExtractPairs(Transform[] transforms)
    {
        var set    = new HashSet<Transform>(transforms);
        set.Remove(null);
        var result = new List<(Transform, Transform)>();

        foreach (var t in transforms)
        {
            if (t == null) continue;
            for (int i = 0; i < t.childCount; i++)
            {
                var child = t.GetChild(i);
                if (set.Contains(child))
                    result.Add((t, child));
            }
        }
        return result.ToArray();
    }
}
