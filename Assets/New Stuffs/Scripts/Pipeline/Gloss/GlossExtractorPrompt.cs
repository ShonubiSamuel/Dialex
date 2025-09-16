using System;
using UnityEngine;

namespace YourApp.Signs.Pipeline.Gloss
{
    [CreateAssetMenu(menuName = "Sign Pipeline/Gloss Extractor Prompt", fileName = "GlossExtractorPrompt")]
    public class GlossExtractorPrompt : ScriptableObject
    {
        [Header("Model Hints")]
        public string model = "gpt-4o-mini";          // or a Groq/OpenAI model id
        [Range(0f,1f)] public float temperature = 0f;
        public int maxTokens = 512;

        [Header("Prompt")]
        [TextArea(4,12)]
        public string systemPrompt =
            "You convert English sentences into a sequence of sign 'gloss' keys. " +
            "Return ONLY a JSON array of strings. Each item: lower-case, words separated with underscores, " +
            "numbers preserved (e.g., \"7.2\"), and no punctuation or commentary.";

        [TextArea(3,10)]
        public string userTemplate =
            "Sentence:\n\"{text}\"\n\n" +
            "Rules:\n" +
            "- Output ONLY a JSON array of strings like [\"ask_out\",\"anyone\",\"7.2\"].\n" +
            "- Use concise dictionary-like glosses (no morphological tags).\n" +
            "- No alternatives, no explanations.\n";

        public string BuildUserContent(string englishText)
        {
            return (userTemplate ?? "{text}").Replace("{text}", englishText ?? "");
        }
    }
}