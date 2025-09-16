using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace YourApp.Signs.Pipeline.STT
{
    /// <summary>
    /// Simple hybrid in-memory + disk cache for STT transcripts.
    /// Key = SHA256(PCM mono data + sampleRate + langHint).
    /// Files saved under persistentDataPath/Transcripts/<key>.json
    /// </summary>
    public static class TranscriptionCache
    {
        [Serializable]
        private class Entry
        {
            public string key;
            public string text;
            public string language;
            public float  duration;
            public int    sampleRate;
            public string createdUtc;
        }

        private static readonly Dictionary<string, Entry> _mem = new();
        private static string Dir
        {
            get
            {
                var d = Path.Combine(Application.persistentDataPath, "Transcripts");
                if (!Directory.Exists(d)) Directory.CreateDirectory(d);
                return d;
            }
        }

        public static string ComputeKey(float[] monoPcm, int sampleRate, string langHint)
        {
            // Hash bytes of PCM + SR + lang
            using var sha = SHA256.Create();
            void Feed(byte[] b) => sha.TransformBlock(b, 0, b.Length, null, 0);

            // PCM floats → bytes
            var pcmBytes = new byte[monoPcm.Length * 4];
            Buffer.BlockCopy(monoPcm, 0, pcmBytes, 0, pcmBytes.Length);
            Feed(pcmBytes);

            var meta = Encoding.UTF8.GetBytes($"|sr:{sampleRate}|lang:{langHint ?? ""}|");
            sha.TransformFinalBlock(meta, 0, meta.Length);

            return BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant();
        }

        public static bool TryGet(string key, out string text, out string language, out float duration)
        {
            text = null; language = null; duration = 0f;

            if (string.IsNullOrEmpty(key)) return false;

            if (_mem.TryGetValue(key, out var e))
            {
                text = e.text; language = e.language; duration = e.duration;
                return true;
            }

            var path = Path.Combine(Dir, key + ".json");
            if (!File.Exists(path)) return false;

            try
            {
                var json = File.ReadAllText(path, Encoding.UTF8);
                var e2 = JsonUtility.FromJson<Entry>(json);
                if (e2 != null)
                {
                    _mem[key] = e2;
                    text = e2.text; language = e2.language; duration = e2.duration;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TranscriptionCache] Read failed: {ex.Message}");
            }

            return false;
        }

        public static void Store(string key, string text, string language, float duration, int sampleRate)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(text)) return;

            var e = new Entry
            {
                key = key,
                text = text,
                language = language,
                duration = duration,
                sampleRate = sampleRate,
                createdUtc = DateTime.UtcNow.ToString("o")
            };

            _mem[key] = e;

            try
            {
                var path = Path.Combine(Dir, key + ".json");
                var json = JsonUtility.ToJson(e, true);
                File.WriteAllText(path, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TranscriptionCache] Write failed: {ex.Message}");
            }
        }

        /// <summary>Downmix interleaved multi-channel float PCM to mono.</summary>
        public static float[] DownmixToMono(float[] interleaved, int channels)
        {
            if (channels <= 1) return interleaved;
            int frames = interleaved.Length / channels;
            var mono = new float[frames];
            int idx = 0;
            for (int f = 0; f < frames; f++)
            {
                double sum = 0; for (int c = 0; c < channels; c++) sum += interleaved[idx++];
                mono[f] = (float)(sum / channels);
            }
            return mono;
        }

        /// <summary>Extracts interleaved samples from an AudioClip (all frames).</summary>
        public static float[] GetInterleaved(AudioClip clip)
        {
            var data = new float[clip.samples * clip.channels];
            clip.GetData(data, 0);
            return data;
        }
    }
}
