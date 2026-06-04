using UnityEngine;

// Boot entry: marks the persistent infrastructure root as DontDestroyOnLoad, then loads the first
// mode scene single-mode through the transition runner (which fades in from black). After the first
// single load, the bootstrap scene itself unloads – only PersistentRoot survives.
public class AppBootstrap : MonoBehaviour
{
    private const string MAIN_MENU_SCENE = "MainMenu";

    [SerializeField] private GameObject            _persistentRoot;     // the infra root to keep alive
    [SerializeField] private SceneTransitionRunner _transitionRunner;   // lives under _persistentRoot

    private void Start()
    {
        Cursor.visible   = false;
        Cursor.lockState = CursorLockMode.Locked;

        if (_persistentRoot != null) DontDestroyOnLoad(_persistentRoot);

        if (_transitionRunner != null)
            _transitionRunner.LoadInitial(MAIN_MENU_SCENE, null);
        else
            Debug.LogError("AppBootstrap: _transitionRunner not assigned - first scene will not load.");
    }
}
