using UnityEngine;
using UnityEngine.SceneManagement;

public class AppBootstrap : MonoBehaviour
{
    private void Start()
    {
        SceneManager.LoadScene("MainMenu", LoadSceneMode.Additive);
    }
}
