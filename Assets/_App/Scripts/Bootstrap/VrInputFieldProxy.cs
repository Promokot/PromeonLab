using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;
using VContainer.Unity;

public class VrInputFieldProxy : MonoBehaviour, IPointerDownHandler
{
    private TMP_InputField _field;
    private EventBus       _bus;

    private void Awake()
    {
        _field = GetComponent<TMP_InputField>();
        var scope = LifetimeScope.Find<RootLifetimeScope>();
        _bus = scope?.Container.Resolve<EventBus>();
    }

    public void OnPointerDown(PointerEventData _)
    {
        if (_field != null)
            _bus?.Publish(new KeyboardFocusEvent { Target = _field });
    }
}
