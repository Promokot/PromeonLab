using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

[AddComponentMenu("PromeonLab/Bone Renderer (Promeon)")]
public class PromeonBoneRenderer : BoneRenderer
{
    [SerializeField] private Material _boneMaterial;
    [SerializeField] private float _boneWidth = 0.12f;

    private readonly List<GameObject> _boneGOs = new();
    private static Mesh s_BoneMesh;

    void Awake() => Rebuild();

    void OnValidate() => Rebuild();

    void OnDestroy() => DestroyBoneGOs();

    public void Rebuild()
    {
        DestroyBoneGOs();

        if (!drawBones || transforms == null || transforms.Length == 0) return;

        if (s_BoneMesh == null) s_BoneMesh = BuildDiamondMesh();

        foreach (var (start, end) in ExtractPairs(transforms))
            _boneGOs.Add(CreateBoneGO(start, end));
    }

    GameObject CreateBoneGO(Transform start, Transform end)
    {
        var go = new GameObject($"Bone_{start.name}");
        go.transform.SetParent(start, worldPositionStays: false);

        go.AddComponent<MeshFilter>().sharedMesh = s_BoneMesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = _boneMaterial;
        if (_boneMaterial == null)
            Debug.LogWarning("[PromeonBoneRenderer] _boneMaterial not assigned.", this);

        var col    = go.AddComponent<CapsuleCollider>();
        col.direction = 1;      // Y-axis, matches bone local Y
        col.height    = 1f;     // local space; world height = 1 * localScale.y = bone length
        col.radius    = 0.5f * _boneWidth;

        var localEnd = start.InverseTransformPoint(end.position);
        float length = localEnd.magnitude;
        if (length < 0.0001f) length = 0.0001f;

        var dir = localEnd.normalized;
        if (dir == Vector3.zero) dir = Vector3.up;

        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
        go.transform.localScale    = new Vector3(_boneWidth * length, length, _boneWidth * length);

        return go;
    }

    void DestroyBoneGOs()
    {
        foreach (var go in _boneGOs)
            if (go != null) DestroyObj(go);
        _boneGOs.Clear();
    }

    static void DestroyObj(Object obj)
    {
        if (Application.isPlaying) Destroy(obj);
        else                        DestroyImmediate(obj);
    }

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
