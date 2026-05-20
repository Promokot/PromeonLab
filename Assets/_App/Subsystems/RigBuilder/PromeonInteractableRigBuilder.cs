using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("PromeonLab/Promeon Interactable Rig Builder")]
public class PromeonInteractableRigBuilder : MonoBehaviour
{
    [SerializeField] private Material _boneMaterial;
    [SerializeField] private float    _boneWidth          = 0.06f;
    [SerializeField] private bool     _useConvexCollider  = true;
    private Transform[] _transforms;

    private readonly List<GameObject>   _proxyGOs  = new();
    private readonly List<BoneFollower> _followers = new();
    private Transform _proxyRoot;
    private Mesh      _boneMesh;

    void Awake()     { if (_transforms != null && _transforms.Length > 0) Rebuild(); }
    void OnDestroy() => DestroyBoneGOs();

    public void SetTransforms(Transform[] transforms) => _transforms   = transforms;
    public void SetMaterial(Material material)        => _boneMaterial = material;

    public void Rebuild()
    {
        DestroyBoneGOs();
        var transforms = ResolveTransforms();
        if (transforms == null || transforms.Length == 0) return;
        if (_boneMesh == null) _boneMesh = BuildDiamondMesh();
        BuildProxyHierarchy(transforms);
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

    void BuildProxyHierarchy(Transform[] transforms)
    {
        var set = new HashSet<Transform>(transforms);
        set.Remove(null);

        foreach (var bone in transforms)
        {
            if (bone == null) continue;
            if (set.Contains(bone.parent)) continue;    // not a root bone

            if (_proxyRoot == null)
            {
                var container = new GameObject("_ProxyBones");
                container.transform.SetParent(bone.parent, worldPositionStays: false);
                _proxyRoot = container.transform;
            }

            BuildProxyNode(bone, _proxyRoot, set);
        }
    }

    void BuildProxyNode(Transform bone, Transform proxyParent, HashSet<Transform> set)
    {
        Transform firstChild = null;
        for (int i = 0; i < bone.childCount; i++)
        {
            var c = bone.GetChild(i);
            if (set.Contains(c)) { firstChild = c; break; }
        }

        float length = firstChild != null
            ? Mathf.Max((firstChild.position - bone.position).magnitude, 0.0001f)
            : _boneWidth * 5f;
        float width = EffectiveWidth(_boneWidth, length);

        var proxyGo = new GameObject($"proxy_{bone.name}");
        proxyGo.transform.SetParent(proxyParent, worldPositionStays: false);
        proxyGo.transform.SetPositionAndRotation(bone.position, bone.rotation);
        proxyGo.transform.localScale = new Vector3(width, length, width);

        AddMeshAndOutline(proxyGo);
        AddCollider(proxyGo);
        _proxyGOs.Add(proxyGo);

        var follower = bone.gameObject.AddComponent<BoneFollower>();
        follower.SetProxy(proxyGo.transform);
        _followers.Add(follower);

        for (int i = 0; i < bone.childCount; i++)
        {
            var child = bone.GetChild(i);
            if (set.Contains(child))
                BuildProxyNode(child, proxyGo.transform, set);
        }
    }

    void AddCollider(GameObject go)
    {
        if (_useConvexCollider)
        {
            var mc           = go.AddComponent<MeshCollider>();
            mc.sharedMesh    = _boneMesh;
            mc.convex        = true;
        }
        else
        {
            var col       = go.AddComponent<CapsuleCollider>();
            col.direction = 1;
            col.height    = 1f;
            col.radius    = 0.5f;
        }
    }

    void AddMeshAndOutline(GameObject go)
    {
        go.AddComponent<MeshFilter>().sharedMesh = _boneMesh;
        var mr = go.AddComponent<MeshRenderer>();
        if (_boneMaterial == null)
            Debug.LogWarning("[PromeonInteractableRigBuilder] _boneMaterial not assigned.", this);
        mr.sharedMaterial    = _boneMaterial;
        var outline          = go.AddComponent<Outline>();
        outline.OutlineMode  = Outline.Mode.SilhouetteOnly;
        outline.OutlineColor = Color.white;
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

        if (_boneMesh != null) { DestroyObj(_boneMesh); _boneMesh = null; }
    }

    private static void DestroyObj(Object obj)
    {
        if (Application.isPlaying) Destroy(obj);
        else                        DestroyImmediate(obj);
    }

    Transform[] ResolveTransforms()
    {
        if (_transforms != null && _transforms.Length > 0)
            return _transforms;

        var smr = GetComponentInChildren<SkinnedMeshRenderer>()
               ?? GetComponentInParent<SkinnedMeshRenderer>();
        if (smr != null && smr.bones.Length > 0)
            return smr.bones;

        Debug.LogWarning("[PromeonInteractableRigBuilder] No transforms set and no SkinnedMeshRenderer found.", this);
        return null;
    }

    public static float EffectiveWidth(float boneWidth, float length) =>
        Mathf.Min(boneWidth, length * 0.2f);

    public static Mesh BuildDiamondMesh()
    {
        var mesh = new Mesh { name = "PromeonBoneDiamond" };

        mesh.vertices = new[]
        {
            new Vector3( 0f,    0f,    0f),
            new Vector3( 0.5f,  0.15f, 0f),
            new Vector3(-0.5f,  0.15f, 0f),
            new Vector3( 0f,    0.15f, 0.5f),
            new Vector3( 0f,    0.15f,-0.5f),
            new Vector3( 0f,    1f,    0f),
        };

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
