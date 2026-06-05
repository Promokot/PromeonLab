using UnityEngine;

/// <summary>
/// Renders a loaded model GameObject to a square thumbnail Texture2D via an off-screen camera.
/// Knows nothing about glTF – the caller hands it an already-instantiated, already-parked model.
/// The render is off-screen (Camera.Render to a targetTexture), so the live scene/display are untouched.
/// </summary>
public class ThumbnailRenderer
{
    private const float FovDeg = 30f;
    private static readonly Vector3 ViewDir = new Vector3(1f, 0.7f, -1f).normalized;

    /// <summary>Camera distance that fits the bounding sphere of <paramref name="bounds"/> at the given vertical FOV.</summary>
    internal static float FrameDistance(Bounds bounds, float verticalFovDeg)
    {
        float radius  = Mathf.Max(0.0001f, bounds.extents.magnitude);
        float halfFov = verticalFovDeg * 0.5f * Mathf.Deg2Rad;
        return radius / Mathf.Sin(halfFov);
    }

    /// <summary>Renders <paramref name="model"/> to a size×size RGB24 Texture2D on a solid background.</summary>
    public Texture2D Render(GameObject model, int size, Color background)
    {
        var bounds = ComputeBounds(model);

        var camGo = new GameObject("ThumbnailCamera");
        var cam   = camGo.AddComponent<Camera>();
        cam.enabled         = false;                 // never draws to the screen; we call Render() manually
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = background;
        cam.fieldOfView     = FovDeg * 2f;           // FovDeg is the half-angle used by FrameDistance
        cam.cullingMask     = ~0;

        var dist = FrameDistance(bounds, cam.fieldOfView);
        cam.transform.position = bounds.center + ViewDir * dist;
        cam.transform.LookAt(bounds.center);
        cam.nearClipPlane = Mathf.Max(0.01f, dist - bounds.extents.magnitude * 2f);
        cam.farClipPlane  = dist + bounds.extents.magnitude * 4f;

        var lightGo = new GameObject("ThumbnailLight");
        var light   = lightGo.AddComponent<Light>();
        light.type  = LightType.Directional;
        light.transform.rotation = Quaternion.LookRotation(bounds.center - cam.transform.position);
        light.intensity = 1.1f;

        var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
        var prevActive = RenderTexture.active;
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture.active = rt;
        var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        tex.Apply();

        RenderTexture.active = prevActive;
        cam.targetTexture    = null;
        rt.Release();
        Object.Destroy(rt);
        Object.Destroy(camGo);
        Object.Destroy(lightGo);
        return tex;
    }

    private static Bounds ComputeBounds(GameObject model)
    {
        var renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(model.transform.position, Vector3.one);

        var b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);
        return b;
    }
}
