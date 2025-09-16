using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Server-backed transcriber for Yorùbá audio.
/// Implements SignPipelineController.ITranscriber so the pipeline can call it with an AudioClip.
/// Sends 16 kHz mono WAV to POST {baseUrl}/transcribe and expects:
///   { "transcription": "...", "error": "..." }  (or { "text": "..." } as fallback)
/// </summary>
public class YorubaServerTranscriber : MonoBehaviour, SignPipelineController.ITranscriber
{
    [Header("Endpoint (no trailing slash)")]
    [Tooltip("Your Flask/ngrok base URL, e.g., https://xxxx.ngrok-free.app")]
    public string baseUrl = "https://YOUR-NGROK.ngrok-free.app";

    [Tooltip("Path appended to baseUrl. Change only if your server uses a different route.")]
    public string transcribePath = "/transcribe";

    [Header("Networking")]
    public int requestTimeoutSeconds = 60;
    public bool verboseLogs = true;

    [Serializable]
    private class TranscribeResponse
    {
        public string transcription;
        public string error;
        public string text; // tolerate servers that return "text" instead
    }

    /// <summary>
    /// Upload the provided clip to your Flask endpoint and return the transcription.
    /// </summary>
    public IEnumerator TranscribeAsync(
        AudioClip audio, string langHint,
        Action<SignPipelineController.TranscriptionResult> onDone,
        Action<Exception> onError = null)
    {
        if (audio == null)
        {
            onError?.Invoke(new Exception("AudioClip is null."));
            yield break;
        }

        // Encode to compact 16 kHz mono WAV (matches your working client)
        byte[] wav;
        try
        {
            wav = AudioClipToWav16Mono(audio, 16000);
        }
        catch (Exception e)
        {
            onError?.Invoke(new Exception("WAV encode failed: " + e.Message, e));
            yield break;
        }

        string url = baseUrl.TrimEnd('/') + transcribePath;

        // === Same upload approach as YorubaTranscribeClient ===
        var form = new WWWForm();
        form.AddBinaryData("file", wav, "mic.wav", "audio/wav");

        using (UnityWebRequest req = UnityWebRequest.Post(url, form))
        {
            req.timeout = Mathf.CeilToInt(requestTimeoutSeconds);

            if (verboseLogs)
                Debug.Log($"[YorubaServerTranscriber] POST {url} ({wav.Length / 1024f:0.0} KB) langHint={langHint}");

            yield return req.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
            bool ok = req.result == UnityWebRequest.Result.Success;
#else
            bool ok = !(req.isHttpError || req.isNetworkError);
#endif
            if (!ok)
            {
                string body = req.downloadHandler != null ? req.downloadHandler.text : "";
                string msg = $"HTTP {(long)req.responseCode}: {req.error} | {body}";
                if (verboseLogs) Debug.LogError("[YorubaServerTranscriber] " + msg);
                onError?.Invoke(new Exception(msg));
                yield break;
            }

            string json = req.downloadHandler.text ?? "";
            if (verboseLogs) Debug.Log($"[YorubaServerTranscriber] ← {json}");

            TranscribeResponse resp = null;
            try
            {
                resp = JsonUtility.FromJson<TranscribeResponse>(json);
            }
            catch (Exception e)
            {
                onError?.Invoke(new Exception("JSON parse failed: " + e.Message + "\n" + json));
                yield break;
            }

            if (!string.IsNullOrEmpty(resp?.error))
            {
                onError?.Invoke(new Exception("Server error: " + resp.error));
                yield break;
            }

            // Prefer "transcription", fallback to "text"
            string yo = !string.IsNullOrWhiteSpace(resp?.transcription) ? resp.transcription : resp?.text;

            if (string.IsNullOrWhiteSpace(yo))
            {
                onError?.Invoke(new Exception("Empty transcription from server."));
                yield break;
            }

            onDone?.Invoke(new SignPipelineController.TranscriptionResult
            {
                text = yo,
                language = "yo",
                duration = audio.length
            });
        }
    }

    // === Helpers: AudioClip -> minimal PCM16 mono WAV at targetHz (matches your client) ===
    private static byte[] AudioClipToWav16Mono(AudioClip clip, int targetHz)
    {
        // Pull original samples
        int total = clip.samples * clip.channels;
        float[] data = new float[total];
        clip.GetData(data, 0);

        // Downmix to mono
        int frames = clip.samples;
        float[] mono = new float[frames];
        if (clip.channels == 1)
        {
            Array.Copy(data, mono, frames);
        }
        else
        {
            // simple average of 2 channels (extend if >2 channels)
            for (int i = 0; i < frames; i++)
                mono[i] = 0.5f * (data[i * 2] + data[i * 2 + 1]);
        }

        // Naive resample to targetHz (nearest-neighbor to match your client code)
        if (clip.frequency != targetHz)
        {
            float ratio = (float)targetHz / clip.frequency;
            int newLen = Mathf.CeilToInt(mono.Length * ratio);
            float[] res = new float[newLen];
            for (int i = 0; i < newLen; i++)
            {
                float srcPos = i / ratio;
                int i0 = Mathf.Clamp((int)srcPos, 0, mono.Length - 1);
                res[i] = mono[i0];
            }
            mono = res;
        }

        // Float [-1..1] -> PCM16
        short[] s16 = new short[mono.Length];
        for (int i = 0; i < mono.Length; i++)
            s16[i] = (short)Mathf.Clamp(mono[i] * short.MaxValue, short.MinValue, short.MaxValue);

        // Minimal WAV header + data
        using (var mem = new MemoryStream())
        using (var bw = new BinaryWriter(mem, Encoding.UTF8, leaveOpen: true))
        {
            int channels = 1;
            int byteRate = targetHz * channels * 2;
            int subchunk2 = s16.Length * 2;
            int chunkSize = 36 + subchunk2;

            // RIFF
            bw.Write(Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(chunkSize);
            bw.Write(Encoding.ASCII.GetBytes("WAVE"));

            // fmt
            bw.Write(Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);                 // PCM
            bw.Write((short)1);           // AudioFormat
            bw.Write((short)channels);    // Channels
            bw.Write(targetHz);           // SampleRate
            bw.Write(byteRate);           // ByteRate
            bw.Write((short)(channels * 2)); // BlockAlign
            bw.Write((short)16);          // BitsPerSample

            // data
            bw.Write(Encoding.ASCII.GetBytes("data"));
            bw.Write(subchunk2);
            for (int i = 0; i < s16.Length; i++) bw.Write(s16[i]);

            return mem.ToArray();
        }
    }
}
