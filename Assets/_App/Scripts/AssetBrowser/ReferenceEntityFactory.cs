using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Per-type runtime construction helper for Reference: builds a centered textured quad from the
// recipe's baked aspect/two-sided. The quad IS the node (pivot at geometry center). BuildCenteredQuad
// and BuildMaterial are public statics so the editor builtin-image generator builds asset-backed
// equivalents from the SAME geometry/material logic.
public class ReferenceEntityFactory
{
    private readonly ImportRenderProfile _renderProfile;

    public ReferenceEntityFactory(ImportRenderProfile renderProfile)
    {
        _renderProfile = renderProfile;
    }

    public Task<GameObject> CreateAsync(string absolutePath, Vector3 position, Quaternion rotation,
                                        float aspect, bool twoSided, CancellationToken ct)
    {
        var bytes = File.ReadAllBytes(absolutePath);
        var tex   = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
        if (!tex.LoadImage(bytes))
        {
            Debug.LogError($"ReferenceEntityFactory: not a readable image '{absolutePath}'");
            Object.Destroy(tex);
            return Task.FromResult<GameObject>(null);
        }

        var go = new GameObject("ReferenceImage");
        go.transform.SetPositionAndRotation(position, rotation);
        go.transform.localScale = new Vector3(aspect, 1f, 1f);

        go.AddComponent<MeshFilter>().sharedMesh       = BuildCenteredQuad();
        go.AddComponent<MeshRenderer>().sharedMaterial = BuildMaterial(tex, twoSided, _renderProfile);
        return Task.FromResult(go);
    }

    public static Mesh BuildCenteredQuad()
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

    public static Material BuildMaterial(Texture2D tex, bool twoSided, ImportRenderProfile profile)
    {
        Shader shader = null;
        if (profile != null && profile.TryGet(AssetType.Reference, out var entry))
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
