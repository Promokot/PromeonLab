using System.Collections;
using UnityEngine;

// A head-attached fade overlay for VR scene transitions. Lives on a small unlit quad/cube parented
// to the HMD camera (so it covers both eyes uniformly — no 2D screen overlay). The transition runner
// drives FadeRoutine to black before loading and back to clear after.
public class HeadFade : MonoBehaviour
{
    [SerializeField] private Renderer _renderer;   // unlit, transparent material
    [SerializeField] private float     _defaultDuration = 0.3f;

    private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
    private MaterialPropertyBlock _mpb;
    private float _alpha;

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        Apply(); // start clear (renderer disabled while alpha≈0)
    }

    public void SetAlphaImmediate(float a)
    {
        _alpha = Mathf.Clamp01(a);
        Apply();
    }

    // Frame-locked fade: exactly one step per rendered frame (yield return null), so the ramp stays
    // visually smooth in BOTH directions regardless of SynchronizationContext quirks that made the
    // old Task.Yield() version collapse the fade-out into a single frame. The per-frame dt is clamped
    // so one heavy frame (e.g. right around a scene load) can't skip the whole ramp at once.
    public IEnumerator FadeRoutine(float targetAlpha, float? duration = null)
    {
        float dur    = duration ?? _defaultDuration;
        float start  = _alpha;
        float target = Mathf.Clamp01(targetAlpha);
        if (dur <= 0f) { SetAlphaImmediate(target); yield break; }

        float t = 0f;
        while (t < dur)
        {
            t += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            _alpha = Mathf.Lerp(start, target, Mathf.Clamp01(t / dur));
            Apply();
            yield return null;
        }
        _alpha = target;
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
