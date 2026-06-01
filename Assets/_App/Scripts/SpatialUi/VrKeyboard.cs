using TMPro;
using VContainer;

public class VrKeyboard : UnityEngine.MonoBehaviour
{
    private TMP_InputField _target;
    private EventBus       _bus;

    [Inject]
    public void Construct(EventBus bus) => _bus = bus;

    private void Start()     => _bus?.Subscribe<KeyboardFocusEvent>(OnFocus);
    private void OnDestroy() => _bus?.Unsubscribe<KeyboardFocusEvent>(OnFocus);

    private void OnFocus(KeyboardFocusEvent e)
    {
        if (_target != null && _target != e.Target)
            _target.onEndEdit?.Invoke(_target.text); // commit the field we are leaving
        _target = e.Target;
    }

    public void AddLetter(string letter)
    {
        if (_target == null) return;
        _target.text += letter;
    }

    public void DeleteLetter()
    {
        if (_target == null || _target.text.Length == 0) return;
        _target.text = _target.text[..^1];
    }

    public void SubmitWord()
    {
        if (_target == null) return;
        _target.onEndEdit?.Invoke(_target.text);
        _target = null;
    }
}
