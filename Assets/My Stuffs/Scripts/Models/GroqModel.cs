using UnityEngine;
using UnityEngine.Networking;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

public class GroqModel
{
    private const string ApiKey = "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";
    private const string BaseUrl = "https://api.groq.com/openai/v1/";
    
    private static GroqModel _instance;
    public static GroqModel Instance => _instance ??= new GroqModel();

    // Transcribe Yoruba speech to text
    public async Task<string> TranscribeAudio(string filePath)
    {
        Debug.Log($"[GroqModel] Transcribing file: {filePath}");

        if (!File.Exists(filePath))
        {
            Debug.LogError("[GroqModel] File not found: " + filePath);
            return null;
        }

        byte[] audioData = File.ReadAllBytes(filePath);
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", audioData, "recorded.wav", "audio/wav");
        form.AddField("model", "whisper-large-v3-turbo");
        form.AddField("response_format", "text");

        string result = await PostFormAsync("audio/transcriptions", form);

        if (!string.IsNullOrWhiteSpace(result))
        {
            Debug.Log("[GroqModel] Transcription successful.");
        }

        return result;
    }

    private async Task<string> PostFormAsync(string endpoint, WWWForm form)
    {
        string url = BaseUrl + endpoint;
        Debug.Log($"[GroqModel] Sending POST to {url}");

        using UnityWebRequest request = UnityWebRequest.Post(url, form);
        request.SetRequestHeader("Authorization", $"Bearer {ApiKey}");

        var operation = request.SendWebRequest();
        while (!operation.isDone)
            await Task.Yield();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("[GroqModel] Response: " + request.downloadHandler.text);
            return request.downloadHandler.text;
        }
        else
        {
            Debug.LogError($"[GroqModel] Error {request.responseCode}: {request.error}");
            Debug.LogError("Response body: " + request.downloadHandler.text);
            return null;
        }
    }


    // Translate Yoruba speech directly to English
    public async Task<string> TranslateWavFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError("File not found: " + filePath);
            return null;
        }

        byte[] audioData = File.ReadAllBytes(filePath);
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", audioData, "recorded.wav", "audio/wav");
        form.AddField("model", "whisper-large-v3");
        form.AddField("response_format", "text");
        form.AddField("language", "en"); // Required for translation

        return await PostFormAsync("audio/translations", form);
    }
    

    // Optional: If you want to use JSON POST for future expansion (chat, prompts, etc.)
    public async Task<string> PostJsonAsync(string endpoint, string json)
    {
        byte[] body = Encoding.UTF8.GetBytes(json);
        using UnityWebRequest request = new UnityWebRequest(BaseUrl + endpoint, "POST");
        request.uploadHandler = new UploadHandlerRaw(body);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Authorization", $"Bearer {ApiKey}");
        request.SetRequestHeader("Content-Type", "application/json");

        var operation = request.SendWebRequest();
        while (!operation.isDone)
            await Task.Yield();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("Groq JSON Result:\n" + request.downloadHandler.text);
            return request.downloadHandler.text;
        }
        else
        {
            Debug.LogError("Groq JSON Error: " + request.error);
            return null;
        }
    }
}
