using UnityEngine;
using UnityEngine.SceneManagement;

public class AppBootstrap : MonoBehaviour
{
    private void Start()
    {
        Cursor.visible   = false;
        Cursor.lockState = CursorLockMode.Locked;
        SceneManager.LoadScene("MainMenu", LoadSceneMode.Additive);
    }
}
