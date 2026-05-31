using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class ReferenceQuadFactory
{
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

        var go   = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name  = "ReferenceImage";
        go.transform.SetPositionAndRotation(position, rotation);

        var aspect = tex.height == 0 ? 1f : (float)tex.width / tex.height;
        go.transform.localScale = new Vector3(aspect, 1f, 1f);

        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { mainTexture = tex };
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;

        return Task.FromResult(go);
    }
}
