using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace YourApp.Signs.Pipeline.Gloss
{
    /// <summary>
    /// Gloss extraction via OpenAI Chat Completions.
    /// Endpoint: https://api.openai.com/v1/chat/completions
    /// </summary>
    public class OpenAIGlossExtractor : MonoBehaviour,
        IGlossExtractor, SignPipelineController.IGlossExtractor
    {
        [Header("Endpoint")]
        public string endpointUrl = "https://api.openai.com/v1/chat/completions";
        [SerializeField] private string apiKey;

        [Header("Prompt Settings")]
        public GlossExtractorPrompt prompt;

        [Header("Debug")]
        public bool logRequest = false;
        public bool logResponse = false;

        public IEnumerator ExtractAsync(
            string englishText,
            Action<List<string>> onDone,
            Action<Exception> onError = null)
        {
            if (string.IsNullOrWhiteSpace(englishText))
            {
                onDone?.Invoke(new List<string>());
                yield break;
            }
            if (prompt == null)
            {
                onError?.Invoke(new InvalidOperationException("GlossExtractorPrompt is not assigned."));
                yield break;
            }

            var payload = new ChatPayload
            {
                model = string.IsNullOrEmpty(prompt.model) ? "gpt-4o-mini" : prompt.model,
                temperature = prompt.temperature,
                max_tokens = prompt.maxTokens,
                messages = new[]
                {
                    new ChatMessage{ role="system", content = prompt.systemPrompt },
                    new ChatMessage{ role="user",   content = prompt.BuildUserContent(englishText) }
                }
            };
            var json = JsonUtility.ToJson(payload);

            using var req = new UnityWebRequest(endpointUrl, "POST");
            var body = Encoding.UTF8.GetBytes(json);
            req.uploadHandler   = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(apiKey))
                req.SetRequestHeader("Authorization", $"Bearer {apiKey}");
            req.timeout = 120;

            if (logRequest) Debug.Log($"[OpenAIGlossExtractor] POST {endpointUrl}\n{json}");
            yield return req.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
            bool ok = req.result == UnityWebRequest.Result.Success;
#else
            bool ok = !(req.isNetworkError || req.isHttpError);
#endif
            if (!ok)
            {
                var err = $"HTTP {req.responseCode}: {req.error}\n{req.downloadHandler?.text}";
                onError?.Invoke(new Exception(err));
                yield break;
            }

            var raw = req.downloadHandler.text;
            if (logResponse) Debug.Log($"[OpenAIGlossExtractor] Raw:\n{raw}");

            var glosses = ParseChoicesToList(raw);
            var cleaned = GlossPostProcessor.Process(glosses);

            onDone?.Invoke(cleaned);
        }

        // --- schema ---
        [Serializable] private class ChatPayload
        {
            public string model;
            public float  temperature;
            public int    max_tokens;
            public ChatMessage[] messages;
        }
        [Serializable] private class ChatMessage { public string role; public string content; }
        [Serializable] private class ChatResponse { public Choice[] choices; }
        [Serializable] private class Choice { public ChatMessage message; }

        private List<string> ParseChoicesToList(string json)
        {
            try
            {
                var resp = JsonUtility.FromJson<ChatResponse>(json);
                if (resp?.choices != null && resp.choices.Length > 0)
                {
                    var content = resp.choices[0]?.message?.content ?? "";
                    return GlossPostProcessor.ParseGlossBlock(content);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[OpenAIGlossExtractor] ParseChoices failed: {e.Message}");
            }
            return GlossPostProcessor.ParseGlossBlock(json);
        }
    }
}
