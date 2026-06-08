// TEMPORARY: VKR (thesis) screenshot tool. Lives on feature/vkr-screenshot-* branches.
// Delete this file (and the ThesisTools folder) when no longer needed.
using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;

[DefaultExecutionOrder(-1000)]
public class ThesisScreenshotCapturer : MonoBehaviour
{
    private const string ROOT_NAME       = "[ThesisScreenshotCapturer]";
    private const string FAKE_CAM_NAME   = "[ThesisScreenshotCamera]";
    private const string OVERLAY_NAME    = "[ThesisContinuousOverlay]";
    private const int    OVERLAY_SORT    = 32000;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject(ROOT_NAME);
        go.AddComponent<ThesisScreenshotCapturer>();
        DontDestroyOnLoad(go);
    }

    private bool _leftYWasPressed;
    private string _saveDir;
    private Camera _fakeCam;

    // Continuous-mode state — only allocated while ON.
    private bool _continuousMode;
    private RenderTexture _persistentRt;
    private Vector2Int    _rtSize;
    private Canvas        _overlayCanvas;
    private RawImage      _overlayImage;

    public bool IsContinuousMode => _continuousMode;

    private void Start()
    {
        var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        _saveDir = string.IsNullOrEmpty(pictures)
            ? Path.Combine(Application.persistentDataPath, "Screenshots")
            : Path.Combine(pictures, "Screenshots");

        try { Directory.CreateDirectory(_saveDir); }
        catch (Exception e) { Debug.LogError($"[ThesisScreenshot] cannot create '{_saveDir}': {e.Message}"); return; }

        Debug.Log($"[ThesisScreenshot] ready. Press Y (keyboard) or Y on left controller. Saving to: {_saveDir}");
    }

    private void OnDestroy()
    {
        // Defensive cleanup — release RT / overlay if continuous was on.
        if (_continuousMode) SetContinuousMode(false);
    }

    private void Update()
    {
        if (string.IsNullOrEmpty(_saveDir)) return;

        // Resize persistent RT if game-view size changed.
        if (_continuousMode && _persistentRt != null &&
            (Screen.width != _rtSize.x || Screen.height != _rtSize.y))
        {
            ReallocPersistentRt();
        }

        if (Keyboard.current != null && Keyboard.current.yKey.wasPressedThisFrame)
        {
            Capture();
            return;
        }

        var leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (leftHand.isValid && leftHand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out var pressed))
        {
            if (pressed && !_leftYWasPressed) Capture();
            _leftYWasPressed = pressed;
        }
        else
        {
            _leftYWasPressed = false;
        }
    }

    // -------------------- Public API --------------------

    /// <summary>
    /// Toggle the continuous PC-monitor feed. When ON, the fake camera renders
    /// every frame into a persistent RT, blitted to the PC monitor via a
    /// screen-space overlay canvas. HMD render is untouched.
    /// </summary>
    public void SetContinuousMode(bool on)
    {
        if (on == _continuousMode) return;

        if (on)
        {
            if (!TryResolveFakeCam())
            {
                Debug.LogWarning($"[ThesisScreenshot] continuous ON requested but '{FAKE_CAM_NAME}' not found. No-op.");
                return;
            }
            EnableContinuous();
        }
        else
        {
            DisableContinuous();
        }

        _continuousMode = on;
        Debug.Log($"[ThesisScreenshot] continuous mode: {(_continuousMode ? "ON" : "OFF")}");
    }

    // -------------------- Continuous mode --------------------

    private void EnableContinuous()
    {
        ReallocPersistentRt();

        _fakeCam.gameObject.SetActive(true);
        _fakeCam.stereoTargetEye = StereoTargetEyeMask.None;
        _fakeCam.targetTexture   = _persistentRt;
        _fakeCam.enabled         = true;

        var go = new GameObject(OVERLAY_NAME);
        DontDestroyOnLoad(go);
        _overlayCanvas = go.AddComponent<Canvas>();
        _overlayCanvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        _overlayCanvas.sortingOrder = OVERLAY_SORT;

        var imgGo = new GameObject("Image");
        imgGo.transform.SetParent(go.transform, false);
        _overlayImage = imgGo.AddComponent<RawImage>();
        var rt = _overlayImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        _overlayImage.texture = _persistentRt;
        _overlayImage.raycastTarget = false;
    }

    private void DisableContinuous()
    {
        if (_fakeCam != null)
        {
            _fakeCam.enabled       = false;
            _fakeCam.targetTexture = null;
            _fakeCam.gameObject.SetActive(false);
        }
        if (_overlayCanvas != null)
        {
            Destroy(_overlayCanvas.gameObject);
            _overlayCanvas = null;
            _overlayImage  = null;
        }
        if (_persistentRt != null)
        {
            _persistentRt.Release();
            Destroy(_persistentRt);
            _persistentRt = null;
            _rtSize       = default;
        }
    }

    private void ReallocPersistentRt()
    {
        int w = Mathf.Max(1, Screen.width);
        int h = Mathf.Max(1, Screen.height);

        if (_persistentRt != null)
        {
            _persistentRt.Release();
            Destroy(_persistentRt);
        }
        _persistentRt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = "[ThesisContinuousRT]" };
        _persistentRt.Create();
        _rtSize = new Vector2Int(w, h);

        if (_fakeCam != null && _fakeCam.targetTexture != null)
            _fakeCam.targetTexture = _persistentRt;
        if (_overlayImage != null)
            _overlayImage.texture = _persistentRt;

        Debug.Log($"[ThesisScreenshot] RT (re)allocated {w}x{h}.");
    }

    // -------------------- Still capture (Y) --------------------

    private void Capture()
    {
        var fileName = $"PromeonLab_{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}.png";
        var fullPath = Path.Combine(_saveDir, fileName);

        // Fast path: continuous already running — the RT has a live frame.
        if (_continuousMode && _persistentRt != null)
        {
            try
            {
                ReadRtToFile(_persistentRt, fullPath);
                Debug.Log($"[ThesisScreenshot] captured (continuous RT): {fullPath}");
            }
            catch (Exception e) { Debug.LogError($"[ThesisScreenshot] capture failed: {e.Message}"); }
            return;
        }

        if (!TryResolveFakeCam())
        {
            ScreenCapture.CaptureScreenshot(fullPath);
            Debug.LogWarning($"[ThesisScreenshot] '{FAKE_CAM_NAME}' not found; used ScreenCapture fallback (left-eye only). Path: {fullPath}");
            return;
        }

        try
        {
            RenderFakeCamToFile(fullPath);
            Debug.Log($"[ThesisScreenshot] captured: {fullPath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[ThesisScreenshot] capture failed: {e.Message}");
        }
    }

    private bool TryResolveFakeCam()
    {
        if (_fakeCam != null) return true;
        var cams = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < cams.Length; i++)
        {
            if (cams[i] != null && cams[i].gameObject.name == FAKE_CAM_NAME)
            {
                _fakeCam = cams[i];
                return true;
            }
        }
        return false;
    }

    private void RenderFakeCamToFile(string fullPath)
    {
        int w = Mathf.Max(1, Screen.width);
        int h = Mathf.Max(1, Screen.height);

        var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
        var prevTarget = _fakeCam.targetTexture;
        var wasActive  = _fakeCam.gameObject.activeSelf;

        if (!wasActive) _fakeCam.gameObject.SetActive(true);
        _fakeCam.stereoTargetEye = StereoTargetEyeMask.None;
        _fakeCam.targetTexture   = rt;
        _fakeCam.Render();
        _fakeCam.targetTexture   = prevTarget;
        if (!wasActive) _fakeCam.gameObject.SetActive(false);

        ReadRtToFile(rt, fullPath);
        RenderTexture.ReleaseTemporary(rt);
    }

    /// <summary>Reads pixels from <paramref name="rt"/> into a PNG at <paramref name="fullPath"/>.</summary>
    private static void ReadRtToFile(RenderTexture rt, string fullPath)
    {
        var prevActive = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = prevActive;

        var bytes = tex.EncodeToPNG();
        Destroy(tex);
        File.WriteAllBytes(fullPath, bytes);
    }
}
