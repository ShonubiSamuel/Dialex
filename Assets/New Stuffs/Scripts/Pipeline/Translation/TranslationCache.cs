using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace YourApp.Signs.Pipeline.Translation
{
    /// <summary>
    /// Simple in-memory + disk cache for translations.
    /// Key = SHA256(UTF8(srcText) + srcLang + "->" + dstLang).
    /// Files: persistentDataPath/Translations/<key>.json
    /// </summary>
    public static class TranslationCache
    {
        [Serializable]
        private class Entry
        {
            public string key;
            public string srcLang;
            public string dstLang;
            public string srcText;
            public string translated;
            public string createdUtc;
        }

        private static readonly Dictionary<string, Entry> _mem = new();

        private static string Dir
        {
            get
            {
                var d = Path.Combine(Application.persistentDataPath, "Translations");
                if (!Directory.Exists(d)) Directory.CreateDirectory(d);
                return d;
            }
        }

        public static string ComputeKey(string srcText, string srcLang, string dstLang)
        {
            using var sha = SHA256.Create();
            var payload = Encoding.UTF8.GetBytes($"{srcLang}->{dstLang}||{srcText}");
            var hash = sha.ComputeHash(payload);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        public static bool TryGet(string key, out string translated)
        {
            translated = null;
            if (string.IsNullOrEmpty(key)) return false;

            if (_mem.TryGetValue(key, out var e))
            {
                translated = e.translated;
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
                    translated = e2.translated;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TranslationCache] Read failed: {ex.Message}");
            }
            return false;
        }

        public static void Store(string key, string srcText, string srcLang, string dstLang, string translated)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(translated)) return;

            var e = new Entry
            {
                key = key,
                srcText = srcText,
                srcLang = srcLang,
                dstLang = dstLang,
                translated = translated,
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
                Debug.LogWarning($"[TranslationCache] Write failed: {ex.Message}");
            }
        }
    }
}
