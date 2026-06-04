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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject(ROOT_NAME);
        go.AddComponent<ThesisScreenshotCapturer>();
        DontDestroyOnLoad(go);
    }

    private bool _leftYWasPressed;
    private string _saveDir;

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
        ScreenCapture.CaptureScreenshot(fullPath);
        Debug.Log($"[ThesisScreenshot] captured: {fullPath}");
    }
}
