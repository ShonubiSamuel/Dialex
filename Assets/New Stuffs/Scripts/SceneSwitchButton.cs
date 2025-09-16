using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneSwitchButton : MonoBehaviour
{
    [SerializeField] private string sceneName;

    public void SwitchScene()
    {
        if (!string.IsNullOrEmpty(sceneName))
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }
}