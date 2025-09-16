using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

[System.Serializable]
public class TranscribeResponse {
    public string transcription; // matches your Flask JSON key
    public string error;         // in case server returns {"error": "..."}
}

public class YorubaTranscribeClient : MonoBehaviour
{
    [Header("Your Flask/ngrok endpoint (no trailing slash)")]
    public string baseUrl = "https://YOUR-NGROK.ngrok-free.app"; // <-- paste from Colab
    public float requestTimeoutSeconds = 60f;

    // === Option A: Pick a file (Editor only) and upload ===
#if UNITY_EDITOR
    [ContextMenu("Pick & Upload (Editor)")]
    public void PickAndUploadEditor()
    {
        string path = UnityEditor.EditorUtility.OpenFilePanel(
            "Select audio to transcribe", "", "wav,mp3,m4a,flac,ogg"
        );
        if (!string.IsNullOrEmpty(path))
        {
            StartCoroutine(UploadFilePath(path));
        }
        else
        {
            Debug.LogWarning("No file selected.");
        }
    }
#endif

    // === Option B: If you already have a file path at runtime (PC/Mac/Android/iOS) ===
    public IEnumerator UploadFilePath(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            Debug.LogError("Invalid audio path: " + path);
            yield break;
        }

        byte[] bytes;
        try { bytes = File.ReadAllBytes(path); }
        catch (System.Exception e) { Debug.LogError("ReadAllBytes failed: " + e); yield break; }

        string url = baseUrl + "/transcribe";

        WWWForm form = new WWWForm();
        form.AddBinaryData("file", bytes, Path.GetFileName(path), GuessMime(path));

        using (UnityWebRequest req = UnityWebRequest.Post(url, form))
        {
            req.timeout = Mathf.CeilToInt(requestTimeoutSeconds);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Upload failed: " + req.error + " | " + req.downloadHandler.text);
                yield break;
            }

            var json = req.downloadHandler.text;
            var resp = JsonUtility.FromJson<TranscribeResponse>(json);
            if (!string.IsNullOrEmpty(resp?.error))
            {
                Debug.LogError("Server error: " + resp.error);
            }
            else
            {
                Debug.Log("Transcription: " + (resp?.transcription ?? "(no text)"));
                // TODO: display in UI/TextMeshPro, etc.
            }
        }
    }

    // === Option C: Upload an in-memory AudioClip (e.g., microphone recording) ===
    public void UploadClip(AudioClip clip)
    {
        if (clip == null) { Debug.LogError("Clip is null."); return; }
        byte[] wav = AudioClipToWav16Mono(clip, 16000); // server resamples anyway, but 16 kHz keeps it small
        StartCoroutine(UploadBytes(wav, "mic.wav", "audio/wav"));
    }

    private IEnumerator UploadBytes(byte[] bytes, string filename, string contentType)
    {
        string url = baseUrl + "/transcribe";
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", bytes, filename, contentType);

        using (UnityWebRequest req = UnityWebRequest.Post(url, form))
        {
            req.timeout = Mathf.CeilToInt(requestTimeoutSeconds);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Upload failed: " + req.error + " | " + req.downloadHandler.text);
                yield break;
            }

            var json = req.downloadHandler.text;
            var resp = JsonUtility.FromJson<TranscribeResponse>(json);
            if (!string.IsNullOrEmpty(resp?.error))
                Debug.LogError("Server error: " + resp.error);
            else
                Debug.Log("Transcription: " + (resp?.transcription ?? "(no text)"));
        }
    }

    // --- Helpers ---
    private static string GuessMime(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        switch (ext)
        {
            case ".wav": return "audio/wav";
            case ".mp3": return "audio/mpeg";
            case ".m4a": return "audio/mp4";
            case ".aac": return "audio/aac";
            case ".flac": return "audio/flac";
            case ".ogg": return "audio/ogg";
            default:     return "application/octet-stream";
        }
    }

    // Minimal WAV (16-bit PCM, mono) from an AudioClip. 16 kHz resample (linear) for small uploads.
    private static byte[] AudioClipToWav16Mono(AudioClip clip, int targetHz)
    {
        // get original data
        int samples = clip.samples * clip.channels;
        float[] data = new float[samples];
        clip.GetData(data, 0);

        // to mono
        int frames = clip.samples;
        float[] mono = new float[frames];
        if (clip.channels == 1) System.Array.Copy(data, mono, frames);
        else
        {
            for (int i = 0; i < frames; i++)
                mono[i] = 0.5f * (data[i * 2] + data[i * 2 + 1]);
        }

        // naive linear resample to targetHz
        float ratio = (float)targetHz / clip.frequency;
        int newLen = Mathf.CeilToInt(mono.Length * ratio);
        float[] res = new float[newLen];
        for (int i = 0; i < newLen; i++)
        {
            float srcPos = i / ratio;
            int i0 = Mathf.Clamp((int)srcPos, 0, mono.Length - 1);
            res[i] = mono[i0];
        }

        // float -> int16
        short[] s16 = new short[newLen];
        for (int i = 0; i < newLen; i++)
            s16[i] = (short)Mathf.Clamp(res[i] * short.MaxValue, short.MinValue, short.MaxValue);

        // write WAV
        using (var mem = new MemoryStream())
        using (var bw = new BinaryWriter(mem))
        {
            int byteRate = targetHz * 2; // mono * 16-bit
            int subchunk2 = s16.Length * 2;
            int chunkSize = 36 + subchunk2;

            // RIFF
            bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(chunkSize);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

            // fmt
            bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);               // PCM
            bw.Write((short)1);         // AudioFormat
            bw.Write((short)1);         // Channels
            bw.Write(targetHz);         // SampleRate
            bw.Write(byteRate);         // ByteRate
            bw.Write((short)2);         // BlockAlign
            bw.Write((short)16);        // BitsPerSample

            // data
            bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            bw.Write(subchunk2);
            foreach (var s in s16) bw.Write(s);

            return mem.ToArray();
        }
    }
}