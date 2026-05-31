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
                                        float aspect, float bottomGap, bool twoSided, CancellationToken ct)
    {
        var bytes = File.ReadAllBytes(absolutePath);
        var tex   = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
        if (!tex.LoadImage(bytes))
        {
            Debug.LogError($"ReferenceQuadFactory: not a readable image '{absolutePath}'");
            Object.Destroy(tex);
            return Task.FromResult<GameObject>(null);
        }

        var root = new GameObject("ReferenceImage");
        root.transform.SetPositionAndRotation(position, rotation);

        var quad  = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Image";
        quad.transform.SetParent(root.transform, worldPositionStays: false);

        const float h = 1f;
        quad.transform.localScale    = new Vector3(aspect, h, 1f);
        quad.transform.localPosition = new Vector3(0f, bottomGap + h * 0.5f, 0f);
        quad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        quad.GetComponent<MeshRenderer>().sharedMaterial = BuildMaterial(tex, twoSided);
        return Task.FromResult(root);
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
