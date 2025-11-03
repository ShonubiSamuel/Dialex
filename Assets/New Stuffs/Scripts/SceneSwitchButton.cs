using Mediapipe;
using Mediapipe.Unity;
using Mediapipe.Unity.Sample;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneSwitchButton : MonoBehaviour
{
    [SerializeField] private string sceneName;

    [SerializeField] private BaseRunner _baseRunner;

    public void SwitchScene()
    {
        if (!string.IsNullOrEmpty(sceneName))
        {

            
            if (_baseRunner != null)
            {
                _baseRunner.Stop();
                Quit();
            }
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
    }
    
     private void Quit()
    {
      GpuManager.Shutdown();

      
        Glog.Shutdown();
      

      Protobuf.ResetLogHandler();
    }
}