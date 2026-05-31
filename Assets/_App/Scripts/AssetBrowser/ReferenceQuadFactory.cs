using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class ReferenceQuadFactory
{
    private readonly ImportRenderProfile _renderProfile;

    public ReferenceQuadFactory(ImportRenderProfile renderProfile)
    {
        _renderProfile = renderProfile;
    }

    // Builds the empty pivot + child quad using the RECIPE's baked aspect/gap/two-sided. The pivot
    // sits at the spawn point on the floor; the image's bottom edge clears the floor by bottomGap.
    public Task<GameObject> CreateAsync(string absolutePath, Vector3 position, Quaternion rotation,
                                        float aspect, bool twoSided, CancellationToken ct)
    {
        var bytes = File.ReadAllBytes(absolutePath);
        var tex   = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
        if (!tex.LoadImage(bytes))
        {
            Debug.LogError($"ReferenceQuadFactory: not a readable image '{absolutePath}'");
            Object.Destroy(tex);
            return Task.FromResult<GameObject>(null);
        }

        // The image IS the node: a centered quad mesh whose pivot is its geometry center, so the gizmo
        // rotates around the middle. No empty parent — the vertical lift comes from the recipe's
        // spawnOffset (applied once at spawn), so it survives reload without drifting.
        var go = new GameObject("ReferenceImage");
        go.transform.SetPositionAndRotation(position, rotation);
        go.transform.localScale = new Vector3(aspect, 1f, 1f);

        go.AddComponent<MeshFilter>().sharedMesh       = BuildCenteredQuad();
        go.AddComponent<MeshRenderer>().sharedMaterial = BuildMaterial(tex, twoSided);
        return Task.FromResult(go);
    }

    private static Mesh BuildCenteredQuad()
    {
        var mesh = new Mesh { name = "ReferenceQuad" };
        mesh.vertices  = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3( 0.5f, -0.5f, 0f),
            new Vector3( 0.5f,  0.5f, 0f),
            new Vector3(-0.5f,  0.5f, 0f),
        };
        mesh.uv        = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private Material BuildMaterial(Texture2D tex, bool twoSided)
    {
        Shader shader = null;
        if (_renderProfile != null && _renderProfile.TryGet(AssetType.Reference, out var entry))
        {
            shader   = entry.Shader;
            twoSided = entry.TwoSided || twoSided;
        }
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");

        var mat = new Material(shader) { mainTexture = tex };
        if (twoSided && mat.HasProperty("_Cull"))
        {
            mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            mat.doubleSidedGI = true;
        }
        return mat;
    }
}
