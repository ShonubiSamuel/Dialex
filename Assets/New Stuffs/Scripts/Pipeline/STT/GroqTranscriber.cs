using UnityEngine;

namespace YourApp.Signs.Pipeline.STT
{
    /// <summary>
    /// Groq STT using OpenAI-compatible endpoint: 
    /// POST https://api.groq.com/openai/v1/audio/transcriptions
    /// Fields: file (wav), model=whisper-large-v3, response_format=json, language=(optional)
    /// </summary>
    public class GroqTranscriber : VendorTranscriberAdapter
    {
        private void Reset()
        {
            // Sensible defaults
            endpointUrl = "https://api.groq.com/openai/v1/audio/transcriptions";
            model = "whisper-large-v3";
            languageParamName = "language";
            responseFormatParamName = "response_format";
            responseFormatValue = "json";
            logRequests = false;
        }

        // If Groq ever returns {"text": "..."} like OpenAI, base ParseTranscript() works.
        // Override ParseTranscript if you need to map a different JSON shape.
    }
}