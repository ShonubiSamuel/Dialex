using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace YourApp.Signs.Pipeline.STT
{
    /// <summary>
    /// Base for HTTP STT vendors using multipart "file" upload + JSON { text } response.
    /// Subclass can override headers/fields/parse if vendor differs.
    /// </summary>
    public abstract class VendorTranscriberAdapter : MonoBehaviour,
        SignPipelineController.ITranscriber, ITranscriber
    {
        [Header("Endpoint")]
        [Tooltip("e.g., https://api.vendor.com/v1/audio/transcriptions")]
        public string endpointUrl;

        [Tooltip("Bearer token for Authorization header (or override BuildRequest to customize).")]
        [SerializeField] private string apiKey;

        [Header("Model & Params")]
        public string model = "whisper-large-v3-turbo";
        public string languageParamName = "language";      // null to omit
        public string modelParamName = "model";
        public string responseFormatParamName = "response_format";
        public string responseFormatValue = "json";        // most APIs accept "json"
        public int    requestTimeout = 180;                // seconds

        [Header("Debug")]
        public bool logRequests = false;

        public IEnumerator TranscribeAsync(
            AudioClip audio,
            string langHint,
            Action<SignPipelineController.TranscriptionResult> onDone,
            Action<Exception> onError = null)
        {
            if (audio == null)
            {
                onError?.Invoke(new ArgumentNullException(nameof(audio)));
                yield break;
            }

            // Extract PCM mono & cache
            var inter = TranscriptionCache.GetInterleaved(audio);
            var mono  = TranscriptionCache.DownmixToMono(inter, audio.channels);
            var key   = TranscriptionCache.ComputeKey(mono, audio.frequency, langHint);

            if (TranscriptionCache.TryGet(key, out var cachedText, out var cachedLang, out var cachedDur))
            {
                onDone?.Invoke(new SignPipelineController.TranscriptionResult
                {
                    text = cachedText,
                    language = string.IsNullOrEmpty(cachedLang) ? langHint : cachedLang,
                    duration = cachedDur > 0 ? cachedDur : (audio.samples / (float)audio.frequency)
                });
                yield break;
            }

            // Encode to WAV bytes (mono)
            var wav = WavEncoder.EncodeToWavBytes(mono, 1, audio.frequency);

            // Build request
            using var req = BuildRequest(wav, audio.frequency, langHint);
            req.timeout = requestTimeout;

            if (logRequests) Debug.Log($"[STT] POST {endpointUrl} ({wav.Length / 1024} KB)");

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

            string transcript;
            try { transcript = ParseTranscript(req.downloadHandler.text); }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
                yield break;
            }

            var res = new SignPipelineController.TranscriptionResult
            {
                text = transcript ?? string.Empty,
                language = langHint, // some vendors return a lang code; override ParseTranscript to capture it
                duration = audio.samples / (float)audio.frequency
            };

            // Store in cache
            TranscriptionCache.Store(key, res.text, res.language, res.duration, audio.frequency);
            onDone?.Invoke(res);
        }

        protected virtual UnityWebRequest BuildRequest(byte[] wavBytes, int sampleRate, string langHint)
        {
            var form = new WWWForm();
            form.AddBinaryData("file", wavBytes, "audio.wav", "audio/wav");

            if (!string.IsNullOrEmpty(modelParamName))
                form.AddField(modelParamName, model);

            if (!string.IsNullOrEmpty(responseFormatParamName) && !string.IsNullOrEmpty(responseFormatValue))
                form.AddField(responseFormatParamName, responseFormatValue);

            if (!string.IsNullOrEmpty(languageParamName) && !string.IsNullOrEmpty(langHint))
                form.AddField(languageParamName, langHint);

            var req = UnityWebRequest.Post(endpointUrl, form);
            if (!string.IsNullOrEmpty(apiKey))
                req.SetRequestHeader("Authorization", $"Bearer {apiKey}");

            // Some vendors want JSON; this default is multipart/form-data which is common for audio uploads.
            return req;
        }

        /// <summary>
        /// Default parser for OpenAI/Groq-style JSON: { "text": "..." }
        /// Override if vendor differs.
        /// </summary>
        protected virtual string ParseTranscript(string json)
        {
            // Minimal JSON sniff without pulling a full parser dependency
            // Look for: "text":"...."
            if (string.IsNullOrEmpty(json)) return "";
            const string key = "\"text\"";
            int k = json.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (k < 0) return json; // fallback: return whole payload
            int colon = json.IndexOf(':', k);
            if (colon < 0) return "";
            int q1 = json.IndexOf('"', colon + 1);
            if (q1 < 0) return "";
            int q2 = json.IndexOf('"', q1 + 1);
            if (q2 < 0) return "";
            return json.Substring(q1 + 1, q2 - q1 - 1)
                       .Replace("\\n", "\n")
                       .Replace("\\\"", "\"");
        }

        /// <summary>Utility WAV encoder (PCM 16-bit). Keep local to avoid extra files.</summary>
        public static class WavEncoder
        {
            public static byte[] EncodeToWavBytes(float[] mono, int channels, int sampleRate)
            {
                // 16-bit PCM
                int bytesPerSample = 2;
                int subchunk2 = mono.Length * bytesPerSample;
                int chunkSize = 36 + subchunk2;

                using var ms = new System.IO.MemoryStream(44 + subchunk2);
                using var bw = new System.IO.BinaryWriter(ms, Encoding.ASCII);

                // RIFF header
                bw.Write(Encoding.ASCII.GetBytes("RIFF"));
                bw.Write(chunkSize);
                bw.Write(Encoding.ASCII.GetBytes("WAVE"));

                // fmt  subchunk
                bw.Write(Encoding.ASCII.GetBytes("fmt "));
                bw.Write(16);                  // PCM
                bw.Write((short)1);            // audio format PCM
                bw.Write((short)channels);
                bw.Write(sampleRate);
                bw.Write(sampleRate * channels * bytesPerSample); // byte rate
                bw.Write((short)(channels * bytesPerSample));     // block align
                bw.Write((short)16);           // bits per sample

                // data subchunk
                bw.Write(Encoding.ASCII.GetBytes("data"));
                bw.Write(subchunk2);

                // samples
                for (int i = 0; i < mono.Length; i++)
                {
                    short s = (short)Mathf.Clamp(Mathf.RoundToInt(mono[i] * short.MaxValue), short.MinValue, short.MaxValue);
                    bw.Write(s);
                }

                return ms.ToArray();
            }
        }
    }
}
