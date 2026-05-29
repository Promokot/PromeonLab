using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// A head-attached fade overlay for VR scene transitions. Lives on a small unlit quad parented to
// the HMD camera (so it covers both eyes uniformly — no 2D screen overlay). The transition runner
// awaits FadeAsync to black before loading and back to clear after.
public class HeadFade : MonoBehaviour
{
    [SerializeField] private Renderer _renderer;   // unlit, transparent material on the quad
    [SerializeField] private float     _defaultDuration = 0.25f;

    private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
    private MaterialPropertyBlock _mpb;
    private float _alpha;

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        Apply(); // start clear
    }

    public void SetAlphaImmediate(float a)
    {
        _alpha = Mathf.Clamp01(a);
        Apply();
    }

    public async Task FadeAsync(float targetAlpha, CancellationToken token, float? duration = null)
    {
        float dur   = duration ?? _defaultDuration;
        float start = _alpha;
        float t     = 0f;
        if (dur <= 0f) { SetAlphaImmediate(targetAlpha); return; }
        while (t < dur)
        {
            token.ThrowIfCancellationRequested();
            t += Time.unscaledDeltaTime;
            _alpha = Mathf.Lerp(start, Mathf.Clamp01(targetAlpha), Mathf.Clamp01(t / dur));
            Apply();
            await Task.Yield();
        }
        _alpha = Mathf.Clamp01(targetAlpha);
        Apply();
    }

    private void Apply()
    {
        if (_renderer == null) return;
        _renderer.GetPropertyBlock(_mpb);
        var c = Color.black; c.a = _alpha;
        _mpb.SetColor(ColorId, c);
        _renderer.SetPropertyBlock(_mpb);
        _renderer.enabled = _alpha > 0.001f;
    }
}
