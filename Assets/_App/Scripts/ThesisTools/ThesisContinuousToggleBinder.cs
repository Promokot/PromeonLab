// TEMPORARY: VKR (thesis) screenshot tool. Lives on feature/vkr-screenshot-* branches.
// Delete this file (and the ThesisTools folder) when no longer needed.
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Wires a UI Toggle in SettingsPanel's General tab to
/// <see cref="ThesisScreenshotCapturer.SetContinuousMode"/>. Self-contained so
/// the whole ВКР tool removes by deleting ThesisTools/ + the toggle row in the
/// User XR Rig prefab.
/// </summary>
public class ThesisContinuousToggleBinder : MonoBehaviour
{
    [SerializeField] private Toggle _toggle;

    private ThesisScreenshotCapturer _capturer;

    private void Start()
    {
        if (_toggle == null)
        {
            Debug.LogWarning("[ThesisContinuousToggleBinder] _toggle not assigned.", this);
            return;
        }

        var found = FindObjectsByType<ThesisScreenshotCapturer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (found == null || found.Length == 0)
        {
            Debug.LogWarning("[ThesisContinuousToggleBinder] ThesisScreenshotCapturer not found in scene.", this);
            return;
        }
        _capturer = found[0];

        // Default to OFF every session — intentional, no PlayerPrefs.
        _toggle.SetIsOnWithoutNotify(false);
        _capturer.SetContinuousMode(false);

        _toggle.onValueChanged.AddListener(OnToggleChanged);
    }

    private void OnDestroy()
    {
        if (_toggle != null)
            _toggle.onValueChanged.RemoveListener(OnToggleChanged);
    }

    private void OnToggleChanged(bool isOn)
    {
        if (_capturer != null)
            _capturer.SetContinuousMode(isOn);
    }
}
