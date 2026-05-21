using UnityEngine;
using UnityEngine.SceneManagement;
using Scene = UnityEngine.SceneManagement.Scene;

public class AppBootstrap : MonoBehaviour
{
    private const string MAIN_MENU_SCENE = "MainMenu";

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
    }
}
