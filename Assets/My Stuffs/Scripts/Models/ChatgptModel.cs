using System;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

public class ChatgptModel
{
    private string githubToken = "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";
    private string endpoint = "https://models.github.ai/inference/chat/completions";
    
    private static ChatgptModel _instance;
    public static ChatgptModel Instance => _instance ??= new ChatgptModel();


    public async Task<string> TranslateYorubaToEnglish(string yorubaText)
    {
        string prompt = "Translate the Yoruba text to English. Respond with only the plain English translation.";
        JObject response = await SendChatRequestAsync(prompt, yorubaText);
        return response["choices"]?[0]?["message"]?["content"]?.ToString()?.Trim();
    }

    public async Task<JObject> ConvertTextToGloss(string englishText)
    {
        string systemPrompt = @"
You are an assistant that converts written or spoken English into a simplified sign language gloss list for animation in Unity.

Your job is to:
1. Simplify the input sentence into a sequence of uppercase gloss tokens.
2. Each token should represent a word or phrase that could potentially map to a sign animation.
3. Remove unnecessary words like articles ('a', 'the'), helper verbs ('am', 'is', 'are'), and tense markers.
4. Use root forms and simpler terms commonly used in ASL glossing (e.g., 'I' → 'ME', 'GOING' → 'GO', 'WOULD LIKE' → 'WANT').
5. Preserve the natural word order as much as possible.
6. Do not try to classify the words — your output should only include a clean gloss array.

Return only a valid JSON object in the format:
{
  'original': 'original input sentence',
  'gloss': ['GLOSS_ITEM_1', 'GLOSS_ITEM_2', '...']
}

Constraints:
- Only return valid JSON with the fields 'original' and 'gloss'.
- All gloss items must be uppercase.
- No extra comments or explanations.
";

        JObject response = await SendChatRequestAsync(systemPrompt, englishText);

        return response;
        
    }

    private async Task<JObject> SendChatRequestAsync(string systemPrompt, string userInput)
    {
        JObject requestBody = new JObject
        {
            ["messages"] = new JArray
            {
                new JObject { ["role"] = "system", ["content"] = systemPrompt.Trim() },
                new JObject { ["role"] = "user", ["content"] = userInput }
            },
            ["temperature"] = 0,
            ["top_p"] = 1,
            ["model"] = "openai/gpt-4.1"
        };

        using (UnityWebRequest request = new UnityWebRequest(endpoint, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(requestBody.ToString());
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {githubToken}");

            var operation = request.SendWebRequest();
            while (!operation.isDone)
                await Task.Yield(); // Yield control until request is done

            if (request.result == UnityWebRequest.Result.Success)
            {
                return JObject.Parse(request.downloadHandler.text);
            }

            Debug.LogError($"HTTP Error: {request.responseCode}\n{request.downloadHandler.text}");
            throw new Exception("Request failed");
        }
    }
}
