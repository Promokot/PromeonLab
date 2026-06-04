// TEMPORARY: VKR (thesis) screenshot tool. Lives on feature/vkr-screenshot branch only.
// Delete this file (and the ThesisTools folder) when no longer needed.
using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

[DefaultExecutionOrder(-1000)]
public class ThesisScreenshotCapturer : MonoBehaviour
{
    private const string ROOT_NAME = "[ThesisScreenshotCapturer]";
    private const string FAKE_CAM_NAME = "[ThesisScreenshotCamera]";

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

    private void Update()
    {
        if (string.IsNullOrEmpty(_saveDir)) return;

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

    private void Capture()
    {
        var fileName = $"PromeonLab_{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}.png";
        var fullPath = Path.Combine(_saveDir, fileName);

        if (!TryResolveFakeCam())
        {
            // Fallback: stereo left-eye only, but at least we don't lose the shot.
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
        var prevActive = RenderTexture.active;
        var prevTarget = _fakeCam.targetTexture;
        var wasActive = _fakeCam.gameObject.activeSelf;

        if (!wasActive) _fakeCam.gameObject.SetActive(true);
        _fakeCam.stereoTargetEye = StereoTargetEyeMask.None;
        _fakeCam.targetTexture = rt;
        _fakeCam.Render();
        _fakeCam.targetTexture = prevTarget;
        if (!wasActive) _fakeCam.gameObject.SetActive(false);

        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = prevActive;
        RenderTexture.ReleaseTemporary(rt);

        var bytes = tex.EncodeToPNG();
        Destroy(tex);

        File.WriteAllBytes(fullPath, bytes);
    }
}
