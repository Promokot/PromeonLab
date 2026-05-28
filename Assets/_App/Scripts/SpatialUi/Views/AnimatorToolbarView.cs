using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AnimatorToolbarView : MonoBehaviour
{
    [SerializeField] private TMP_InputField _currentFrameInput;
    [SerializeField] private TMP_InputField _totalFramesInput;
    [SerializeField] private TMP_InputField _fpsInput;
    [SerializeField] private Button         _setKeyButton;
    [SerializeField] private Button         _deleteKeyButton;
    [SerializeField] private Button         _copyButton;
    [SerializeField] private Button         _pasteButton;
    [SerializeField] private Button         _removeAnimationButton;

    public Action<int> OnCurrentFrameSubmitted;
    public Action<int> OnTotalFramesSubmitted;
    public Action<int> OnFpsSubmitted;
    public Action      OnSetKey;
    public Action      OnDeleteKey;
    public Action      OnCopy;
    public Action      OnPaste;
    public Action      OnRemoveAnimation;

    private void Awake()
    {
        _currentFrameInput?.onEndEdit.AddListener(OnCurrentFrameEdit);
        _totalFramesInput ?.onEndEdit.AddListener(OnTotalFramesEdit);
        _fpsInput         ?.onEndEdit.AddListener(OnFpsEdit);
        _setKeyButton         ?.onClick.AddListener(() => OnSetKey?.Invoke());
        _deleteKeyButton      ?.onClick.AddListener(() => OnDeleteKey?.Invoke());
        _copyButton           ?.onClick.AddListener(() => OnCopy?.Invoke());
        _pasteButton          ?.onClick.AddListener(() => OnPaste?.Invoke());
        _removeAnimationButton?.onClick.AddListener(() => OnRemoveAnimation?.Invoke());
    }

    public void SetCurrentFrame(int frame)
    {
        if (_currentFrameInput != null) _currentFrameInput.SetTextWithoutNotify(frame.ToString());
    }

    public void SetTotalFrames(int frames)
    {
        if (_totalFramesInput != null) _totalFramesInput.SetTextWithoutNotify(frames.ToString());
    }

    public void SetFps(int fps)
    {
        if (_fpsInput != null) _fpsInput.SetTextWithoutNotify(fps.ToString());
    }

    public void SetSetKeyInteractable   (bool v) { if (_setKeyButton    != null) _setKeyButton   .interactable = v; }
    public void SetDeleteKeyInteractable(bool v) { if (_deleteKeyButton != null) _deleteKeyButton.interactable = v; }
    public void SetPasteInteractable    (bool v) { if (_pasteButton     != null) _pasteButton    .interactable = v; }

    private void OnCurrentFrameEdit(string txt)
    {
        if (int.TryParse(txt, out var v)) OnCurrentFrameSubmitted?.Invoke(v);
    }

    private void OnTotalFramesEdit(string txt)
    {
        if (int.TryParse(txt, out var v)) OnTotalFramesSubmitted?.Invoke(v);
    }

    private void OnFpsEdit(string txt)
    {
        if (int.TryParse(txt, out var v)) OnFpsSubmitted?.Invoke(v);
    }
}
