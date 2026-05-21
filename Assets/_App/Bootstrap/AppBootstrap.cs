using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using Scene = UnityEngine.SceneManagement.Scene;

public class AppBootstrap : MonoBehaviour
{
    private const string MAIN_MENU_SCENE = "MainMenu";

    private EventBus _bus;

    [Inject]
    public void Construct(EventBus bus) => _bus = bus;

    private void Start()
    {
        Cursor.visible   = false;
        Cursor.lockState = CursorLockMode.Locked;
        SceneManager.sceneLoaded += OnMainMenuLoaded;
        SceneManager.LoadScene(MAIN_MENU_SCENE, LoadSceneMode.Additive);
    }

    private void OnMainMenuLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != MAIN_MENU_SCENE) return;
        SceneManager.sceneLoaded -= OnMainMenuLoaded;
        SceneManager.SetActiveScene(scene);
        PublishSpawnForScene(scene);
    }

    private void PublishSpawnForScene(Scene scene)
    {
        if (_bus == null) return;
        foreach (var root in scene.GetRootGameObjects())
        {
            var anchor = root.GetComponentInChildren<PlayerSpawnAnchor>(true);
            if (anchor == null) continue;
            _bus.Publish(new PlayerSpawnRequestedEvent
            {
                Position = anchor.transform.position,
                Rotation = anchor.transform.rotation
            });
            return;
        }
    }
}
