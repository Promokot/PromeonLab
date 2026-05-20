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

    // Awake, Rebuild, ExtractPairs added in later tasks.

    public static Mesh BuildDiamondMesh()
    {
        var mesh = new Mesh { name = "PromeonBoneDiamond" };

        mesh.vertices = new[]
        {
            new Vector3( 0f,    0f,    0f),    // 0 head
            new Vector3( 0.5f,  0.15f, 0f),    // 1 shoulder +X
            new Vector3(-0.5f,  0.15f, 0f),    // 2 shoulder -X
            new Vector3( 0f,    0.15f, 0.5f),  // 3 shoulder +Z
            new Vector3( 0f,    0.15f,-0.5f),  // 4 shoulder -Z
            new Vector3( 0f,    1f,    0f),    // 5 tail
        };

        // Winding order: clockwise when viewed from outside (Unity left-hand coords).
        mesh.triangles = new[]
        {
            // Head faces (4 tris from v0 to shoulder ring)
            0, 1, 3,
            0, 3, 2,
            0, 2, 4,
            0, 4, 1,
            // Tail faces (4 tris from shoulder ring to v5)
            1, 5, 3,
            3, 5, 2,
            2, 5, 4,
            4, 5, 1,
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
