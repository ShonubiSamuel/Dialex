using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace YourApp.Signs.Pipeline.Translation
{
    /// <summary>
    /// Translation via GitHub Models Chat Completions (JSON over HTTP).
    /// Endpoint: https://models.github.ai/inference/chat/completions
    /// Requires: Accept + X-GitHub-Api-Version headers and Bearer token (PAT).
    ///
    /// Defaults: model = "openai/gpt-4.1" (works on GitHub Models), temperature = 0.
    /// Implements both pipeline translator interfaces.
    /// </summary>
    public class OpenAITranslator : MonoBehaviour,
        ITranslator, SignPipelineController.ITranslator
    {
        [Header("Endpoint (GitHub Models)")]
        [Tooltip("GitHub Models Chat Completions endpoint.")]
        public string endpointUrl = "https://models.github.ai/inference/chat/completions";

        [Tooltip("Bearer API key (GitHub token with models access). Inject securely at runtime if possible.")]
        [SerializeField] private string apiKey;

        [Header("Model")]
        [Tooltip("Example: openai/gpt-4.1, openai/gpt-4.1-mini, openai/gpt-4o, etc.")]
        public string model = "openai/gpt-4.1";
        [Range(0f, 1f)] public float temperature = 0f;
        public int maxTokens = 1024;
        public int requestTimeout = 120;

        [Header("Prompt")]
        [TextArea(2, 6)]
        public string systemPrompt =
            "You are a professional translator. Translate the user's text into the requested target language.\n" +
            "Return ONLY the translated text with no extra commentary, quotes, or brackets.";

        [Header("Debug")]
        public bool logRequests = false;
        public bool logResponses = false;

        public IEnumerator TranslateAsync(
            string srcText, string srcLang, string dstLang,
            Action<string> onDone, Action<Exception> onError = null)
        {
            print(srcText + srcLang+ dstLang);
            // No-op if languages match or text is empty
            if (string.IsNullOrWhiteSpace(srcText) ||
                string.Equals(srcLang, dstLang, StringComparison.OrdinalIgnoreCase))
            {
                onDone?.Invoke(srcText ?? string.Empty);
                yield break;
            }

            // Cache
            string key = TranslationCache.ComputeKey(srcText, srcLang ?? "", dstLang ?? "en");
            if (TranslationCache.TryGet(key, out var cached))
            {
                onDone?.Invoke(cached);
                yield break;
            }

            print(srcText + srcLang+ dstLang);
            // Payload (OpenAI-compatible schema)
            var payload = new ChatPayload
            {
                model = model,
                temperature = temperature,
                max_tokens = maxTokens,
                messages = new[]
                {
                    new ChatMessage{ role="system", content = systemPrompt },
                    new ChatMessage{ role="user",   content = BuildUserPrompt(srcText, srcLang, dstLang) }
                }
            };

            var json = JsonUtility.ToJson(payload);
            if (logRequests) Debug.Log($"[OpenAITranslator] POST {endpointUrl}\n{json}");

            using var req = new UnityWebRequest(endpointUrl, "POST");
            byte[] body = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = requestTimeout;

            // --- GitHub Models headers (required) ---
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Accept", "application/vnd.github+json");
            req.SetRequestHeader("X-GitHub-Api-Version", "2022-11-28");
            if (!string.IsNullOrEmpty(apiKey))
                req.SetRequestHeader("Authorization", $"Bearer {apiKey}");

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

            var text = ParseText(req.downloadHandler.text);
            if (logResponses) Debug.Log($"[OpenAITranslator] → {text}");

            TranslationCache.Store(key, srcText, srcLang ?? "", dstLang ?? "en", text ?? "");
            onDone?.Invoke(text ?? "");
        }

        private string BuildUserPrompt(string srcText, string srcLang, string dstLang)
        {
            var src = string.IsNullOrEmpty(srcLang) ? "auto" : srcLang;
            var dst = string.IsNullOrEmpty(dstLang) ? "en" : dstLang;
            
            print($"Translate from {src} to {dst}:\n\n{srcText}");
            return $"Translate from {src} to {dst}:\n\n{srcText}";
        }

        // --- Minimal JSON types for Chat Completions ---
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

        private string ParseText(string json)
        {
            try
            {
                var resp = JsonUtility.FromJson<ChatResponse>(json);
                if (resp?.choices != null && resp.choices.Length > 0)
                    return resp.choices[0]?.message?.content ?? "";
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[OpenAITranslator] Parse failed: {e.Message}");
            }
            // fallback: raw body
            return json;
        }
    }
}
