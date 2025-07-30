using System.IO;
using UnityEditor;
using UnityEngine;


public class AudioUploader : MonoBehaviour
{
    
    public GlossPipelineManager glossManager;
    public void SelectFileFromSystem()
    {
#if UNITY_EDITOR
        string path = EditorUtility.OpenFilePanel("Select WAV File", "", "wav");
        if (!string.IsNullOrEmpty(path))
        {
            OnUploadAudioFileButtonClicked(path);
        }
#endif
    }

    public void OnUploadAudioFileButtonClicked(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError("Uploaded audio file does not exist at: " + filePath);
            return;
        }

        Debug.Log("Using uploaded audio file: " + filePath);
        glossManager.Audio(filePath);
    }

}
