using System;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

public class ChatgptModel : MonoBehaviour
{
    [TextArea] public string apiKey = ""; // Put your NEW key here or inject at runtime

    [Header("Demo Input")]
    [TextArea] public string yoruba = "Báwo ni, ọ̀rẹ́? Jọ̀wọ́ túmọ̀ èyí sí Gẹ̀ẹ́sì.";

    // Unity entry point (will actually run)
    public async void Start()
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Debug.LogError("[ChatgptModel] Missing API key.");
            return;
        }

        try
        {
            string english = await TranslateYorubaToEnglish(yoruba);
            Debug.Log($"[ChatgptModel] English: {english}");

            JObject gloss = await ConvertTextToGloss(english);
            Debug.Log($"[ChatgptModel] Gloss JSON: {gloss}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ChatgptModel] Exception: {ex}");
        }
    }

    public async Task<string> TranslateYorubaToEnglish(string yorubaText)
    {
        string systemPrompt = "You are a professional translator. Translate Yoruba to English. Return only the plain English translation.";
        JObject response = await SendChatRequestAsync(systemPrompt, yorubaText);
        return response["choices"]?[0]?["message"]?["content"]?.ToString()?.Trim();
    }

    public async Task<JObject> ConvertTextToGloss(string englishText)
    {
        string systemPrompt = @"
You are an assistant that converts written or spoken English into a simplified sign language gloss list for animation in Unity.

Rules:
1) Output uppercase gloss tokens only (remove articles/helper verbs/tense).
2) Use root forms (e.g., I->ME, GOING->GO, WOULD LIKE->WANT).
3) Keep natural order when possible.

Return ONLY valid JSON:
{
  'original': 'original input sentence',
  'gloss': ['GLOSS_ITEM_1','GLOSS_ITEM_2','...']
}";
        return await SendChatRequestAsync(systemPrompt, englishText);
    }

    private async Task<JObject> SendChatRequestAsync(string systemPrompt, string userInput)
    {
        var body = new JObject
        {
            ["model"] = "openai/gpt-4.1", // or "openai/gpt-4o"
            ["messages"] = new JArray
            {
                new JObject { ["role"] = "system", ["content"] = systemPrompt.Trim() },
                new JObject { ["role"] = "user",   ["content"] = userInput }
            },
            ["temperature"] = 0,
            ["top_p"] = 1
        };

        using (var request = new UnityWebRequest(
                   "https://models.github.ai/inference/chat/completions", "POST"))
        {
            var bytes = Encoding.UTF8.GetBytes(body.ToString());
            request.uploadHandler = new UploadHandlerRaw(bytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 60;

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/vnd.github+json");              // REQUIRED
            request.SetRequestHeader("X-GitHub-Api-Version", "2022-11-28");                 // REQUIRED
            request.SetRequestHeader("Authorization", $"Bearer {apiKey}");                  // REQUIRED

            var op = request.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var json = request.downloadHandler.text;
                if (string.IsNullOrWhiteSpace(json))
                    throw new Exception("Empty response from GitHub Models.");
                return JObject.Parse(json);
            }

            Debug.LogError($"HTTP {request.responseCode} - {request.error}\n{request.downloadHandler.text}");
            throw new Exception($"Request failed ({request.responseCode}).");
        }
    }

}
