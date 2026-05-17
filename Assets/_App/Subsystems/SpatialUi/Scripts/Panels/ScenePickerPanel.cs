using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;

public class ScenePickerPanel : MonoBehaviour
{
    [SerializeField] private Transform      _listRoot;
    [SerializeField] private GameObject     _sceneItemPrefab;
    [SerializeField] private TMP_InputField _nameInput;
    [SerializeField] private Button         _createButton;
    [SerializeField] private Button         _deleteButton;

    private AppStorage _storage;
    private EventBus   _bus;
    private SceneItem  _selectedItem;

    [Inject]
    public void Construct(AppStorage storage, EventBus bus)
    {
        _storage = storage;
        _bus     = bus;
    }

    private async void Start()
    {
        _createButton.onClick.AddListener(() => { _ = OnCreateClickedAsync(); });
        _deleteButton.onClick.AddListener(OnDeleteClicked);
        _deleteButton.interactable = false;
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        foreach (Transform child in _listRoot)
            Destroy(child.gameObject);

        _selectedItem = null;
        _deleteButton.interactable = false;
        _bus.Publish(new SceneSelectedEvent { SceneId = string.Empty, DisplayName = string.Empty });

        var scenes = await _storage.GetAllScenesAsync(CancellationToken.None);
        foreach (var (sceneId, displayName) in scenes)
            SpawnItem(sceneId, displayName);
    }

    private void SpawnItem(string sceneId, string displayName)
    {
        var go   = Instantiate(_sceneItemPrefab, _listRoot);
        var item = go.GetComponent<SceneItem>();
        item.Init(sceneId, displayName);
        item.Clicked += OnItemClicked;
    }

    private void OnItemClicked(SceneItem item)
    {
        _selectedItem?.SetSelected(false);
        _selectedItem = item;
        item.SetSelected(true);
        _deleteButton.interactable = true;
        _bus.Publish(new SceneSelectedEvent { SceneId = item.SceneId, DisplayName = item.DisplayName });
    }

    private async Task OnCreateClickedAsync()
    {
        var name = _nameInput.text;
        if (string.IsNullOrWhiteSpace(name)) name = "New Scene";
        _nameInput.text = string.Empty;
        await _storage.CreateSceneAsync(name, CancellationToken.None);
        await RefreshAsync();
    }

    private void OnDeleteClicked()
    {
        if (_selectedItem == null) return;
        _storage.DeleteScene(_selectedItem.SceneId);
        _ = RefreshAsync();
    }
}
