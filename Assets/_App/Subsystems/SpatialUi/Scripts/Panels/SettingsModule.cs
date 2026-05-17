using System.Collections;
using UnityEngine;

public class SettingsModule : MonoBehaviour
{
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private float _slideDist = 0.05f;
    [SerializeField] private float _duration  = 0.25f;

    private Vector3   _shownLocalPos;
    private Vector3   _hiddenLocalPos;
    private bool      _visible;
    private Coroutine _anim;

    public bool IsVisible => _visible;

    private void Awake()
    {
        _shownLocalPos  = transform.localPosition;
        _hiddenLocalPos = _shownLocalPos - Vector3.up * _slideDist;

        transform.localPosition             = _hiddenLocalPos;
        _canvasGroup.alpha                  = 0f;
        _canvasGroup.interactable           = false;
        _canvasGroup.blocksRaycasts         = false;
    }

    public void Toggle() { if (_visible) Hide(); else Show(); }

    public void Show()
    {
        _visible = true;
        gameObject.SetActive(true);
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(AnimRoutine(true));
    }

    public void Hide()
    {
        if (!_visible) return;
        _visible = false;
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(AnimRoutine(false));
    }

    private IEnumerator AnimRoutine(bool show)
    {
        var startAlpha = _canvasGroup.alpha;
        var endAlpha   = show ? 1f : 0f;
        var startPos   = transform.localPosition;
        var endPos     = show ? _shownLocalPos : _hiddenLocalPos;

        _canvasGroup.interactable   = show;
        _canvasGroup.blocksRaycasts = show;

        float t = 0f;
        while (t < _duration)
        {
            t += Time.deltaTime;
            var p = Mathf.Clamp01(t / _duration);
            _canvasGroup.alpha         = Mathf.Lerp(startAlpha, endAlpha, p);
            transform.localPosition    = Vector3.Lerp(startPos, endPos, p);
            yield return null;
        }

        _canvasGroup.alpha      = endAlpha;
        transform.localPosition = endPos;

        if (!show)
            gameObject.SetActive(false);
    }
}
