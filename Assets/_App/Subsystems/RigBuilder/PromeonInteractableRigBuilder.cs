using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

[AddComponentMenu("PromeonLab/Promeon Interactable Rig Builder")]
public class PromeonInteractableRigBuilder : MonoBehaviour
{
    [SerializeField] private Material _boneMaterial;
    [SerializeField] private float _boneWidth = 0.06f;
    [SerializeField] private bool _useConvexCollider = true;
    [SerializeField] private bool _buildConstraints = true;
    private Transform[] _transforms;

    private readonly List<GameObject> _boneGOs       = new();
    private readonly List<GameObject> _constraintGOs = new();
    private Transform _proxyRoot;
    private Transform _constraintRigParent;
    private Mesh _boneMesh;

    void Awake()     { if (_transforms != null && _transforms.Length > 0) Rebuild(); }
    void OnDestroy() => DestroyBoneGOs();

    public void SetTransforms(Transform[] transforms)           => _transforms            = transforms;
    public void SetMaterial(Material material)                  => _boneMaterial          = material;
    public void SetConstraintRigParent(Transform rigParent)     => _constraintRigParent   = rigParent;

    public void Rebuild()
    {
        DestroyBoneGOs();

        var transforms = ResolveTransforms();
        if (transforms == null || transforms.Length == 0) return;

        if (_boneMesh == null) _boneMesh = BuildDiamondMesh();

        if (_buildConstraints)
        {
            var go = new GameObject("_BoneProxies");
            go.transform.SetParent(transform, worldPositionStays: false);
            _proxyRoot = go.transform;
        }

        foreach (var (start, end) in ExtractPairs(transforms))
            _boneGOs.Add(CreateBoneGO(start, end));
    }

    public void SetVisualsEnabled(bool enabled)
    {
        foreach (var go in _boneGOs)
        {
            if (go == null) continue;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = enabled;
            var outline = go.GetComponent<Outline>();
            if (outline != null) outline.enabled = enabled;
        }
    }

    GameObject CreateBoneGO(Transform start, Transform end)
    {
        var go = new GameObject($"Bone_{start.name}");

        float length;

        if (_proxyRoot != null)
        {
            go.transform.SetParent(_proxyRoot, worldPositionStays: false);
            var worldVec = end.position - start.position;
            length = worldVec.magnitude;
            if (length < 0.0001f) length = 0.0001f;
            go.transform.SetPositionAndRotation(
                start.position,
                Quaternion.FromToRotation(Vector3.up, worldVec / length));
            go.transform.localScale = new Vector3(_boneWidth, length, _boneWidth);
        }
        else
        {
            go.transform.SetParent(start, worldPositionStays: false);
            var localEnd = start.InverseTransformPoint(end.position);
            length = localEnd.magnitude;
            if (length < 0.0001f) length = 0.0001f;
            var dir = localEnd.normalized;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
            go.transform.localScale    = new Vector3(_boneWidth, length, _boneWidth);
        }

        go.AddComponent<MeshFilter>().sharedMesh = _boneMesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = _boneMaterial;
        if (_boneMaterial == null)
            Debug.LogWarning("[PromeonInteractableRigBuilder] _boneMaterial not assigned.", this);

        if (_useConvexCollider)
        {
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = _boneMesh;
            mc.convex = true;
        }
        else
        {
            var col       = go.AddComponent<CapsuleCollider>();
            col.direction = 1;
            col.height    = 1f;
            col.radius    = 0.5f;
        }

        var outline          = go.AddComponent<Outline>();
        outline.OutlineMode  = Outline.Mode.SilhouetteOnly;
        outline.OutlineColor = Color.white;
        outline.OutlineWidth = 3f;

        if (_proxyRoot != null)
            AddParentConstraint(start, go.transform);

        return go;
    }

    void AddParentConstraint(Transform bone, Transform proxy)
    {
        if (_constraintRigParent == null)
        {
            Debug.Log("[PromeonInteractableRigBuilder] No rig parent set — constraints skipped, falling back to visual mode. Call SetConstraintRigParent() before Rebuild().", this);
            return;
        }

        var pcGo = new GameObject($"PC_{bone.name}");
        pcGo.transform.SetParent(_constraintRigParent, worldPositionStays: false);
        _constraintGOs.Add(pcGo);

        var pc = pcGo.AddComponent<MultiParentConstraint>();
        pc.data.constrainedObject = bone;

        var sources = pc.data.sourceObjects;
        sources.Add(new WeightedTransform(proxy, 1f));
        pc.data.sourceObjects = sources;

        pc.weight = 1f;
    }

    void DestroyBoneGOs()
    {
        foreach (var go in _constraintGOs)
            if (go != null) DestroyObj(go);
        _constraintGOs.Clear();

        if (_proxyRoot != null)
        {
            DestroyObj(_proxyRoot.gameObject);
            _proxyRoot = null;
        }
        else
        {
            foreach (var go in _boneGOs)
                if (go != null) DestroyObj(go);
        }
        _boneGOs.Clear();
    }

    static void DestroyObj(Object obj)
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
