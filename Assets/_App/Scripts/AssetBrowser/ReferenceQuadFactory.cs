using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class ReferenceQuadFactory
{
    // Gap from the floor (spawn Y = 0) to the bottom edge of the image — like a picture
    // standing on a low easel rather than sunk into the ground.
    private const float BOTTOM_GAP_METERS = 0.5f;

    private readonly ImportRenderProfile _renderProfile;

    public ReferenceQuadFactory(ImportRenderProfile renderProfile)
    {
        _renderProfile = renderProfile;
    }

    public Task<GameObject> CreateAsync(string absolutePath, Vector3 position, Quaternion rotation, CancellationToken ct)
    {
        var bytes = File.ReadAllBytes(absolutePath);
        var tex   = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
        if (!tex.LoadImage(bytes))
        {
            Debug.LogError($"ReferenceQuadFactory: not a readable image '{absolutePath}'");
            Object.Destroy(tex);
            return Task.FromResult<GameObject>(null);
        }

        // Empty pivot sits at the spawn point on the floor; the image hangs above it so its
        // bottom edge clears the floor by BOTTOM_GAP_METERS. The pivot carries the spawn rotation
        // (aimed at the player), so the whole reference faces the user.
        var root = new GameObject("ReferenceImage");
        root.transform.SetPositionAndRotation(position, rotation);

        var quad  = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Image";
        quad.transform.SetParent(root.transform, worldPositionStays: false);

        var aspect    = tex.height == 0 ? 1f : (float)tex.width / tex.height;
        const float h = 1f;   // the Quad mesh is 1 unit tall; we keep localScale.y = 1m
        quad.transform.localScale    = new Vector3(aspect, h, 1f);
        quad.transform.localPosition = new Vector3(0f, BOTTOM_GAP_METERS + h * 0.5f, 0f);
        // Unity's Quad presents its textured face toward -Z; flip 180° so the textured side faces
        // the pivot's +Z (which the spawn rotation points at the player). TwoSided keeps it visible
        // from behind regardless, so the user never sees a blank back.
        quad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        quad.GetComponent<MeshRenderer>().sharedMaterial = BuildMaterial(tex);

        return Task.FromResult(root);
    }

    private Material BuildMaterial(Texture2D tex)
    {
        var    twoSided = true;
        Shader shader   = null;
        if (_renderProfile != null && _renderProfile.TryGet(AssetType.Reference, out var entry))
        {
            shader   = entry.Shader;
            twoSided = entry.TwoSided;
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
