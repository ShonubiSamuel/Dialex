using UnityEngine;
using UnityEngine.Networking;
using System;
using System.IO;
using System.Threading.Tasks;

public class YorubaModel 
{
    private static YorubaModel _instance;
    public static YorubaModel Instance => _instance ??= new YorubaModel();
    
    [Serializable]
    public class TranscriptionResponse
    {
        public string transcription;
    }

    // Call this method from other scripts using `await`
    public async Task<string> TranscribeAudio(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError("Audio file not found: " + filePath);
            return null;
        }

        byte[] audioData = File.ReadAllBytes(filePath);

        // Prepare form with audio data
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", audioData, Path.GetFileName(filePath), "audio/wav");

        // Replace this with your deployed or local URL
        string url = "https://0b58-34-34-124-19.ngrok-free.app/transcribe";

        using UnityWebRequest request = UnityWebRequest.Post(url, form);
        request.SetRequestHeader("Accept", "application/json");

        var operation = request.SendWebRequest();
        while (!operation.isDone)
        {
            await Task.Yield();
        }

        if (request.result == UnityWebRequest.Result.Success)
        {
            try
            {
                string responseText = request.downloadHandler.text;

                TranscriptionResponse response = JsonUtility.FromJson<TranscriptionResponse>(responseText);
                return response.transcription;
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to parse transcription: " + ex.Message);
                return null;
            }
        }
        else
        {
            Debug.LogError("Transcription request failed: " + request.error);
            return null;
        }
    }
}