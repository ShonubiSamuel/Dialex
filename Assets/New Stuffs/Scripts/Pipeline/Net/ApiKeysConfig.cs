using System;
using System.Collections.Generic;
using UnityEngine;

namespace YourApp.Signs.Pipeline.Net
{
    /// <summary>
    /// Central store for API keys. Create via:
    /// Assets → Create → Sign Pipeline → API Keys Config
    /// Resolution order per key: runtimeOverride → Env Var → serialized fallback.
    /// </summary>
    [CreateAssetMenu(menuName = "Sign Pipeline/API Keys Config", fileName = "ApiKeysConfig")]
    public class ApiKeysConfig : ScriptableObject
    {
        [Serializable]
        public class KeyEntry
        {
            [Tooltip("Logical id you’ll use in code, e.g. 'openai', 'groq'.")]
            public string id = "openai";
            [Tooltip("Environment variable to read at runtime (recommended).")]
            public string envVar = "OPENAI_API_KEY";
            [Tooltip("Optional serialized fallback (dev only). Leave empty for builds.")]
            [SerializeField] private string serializedFallback;
            [NonSerialized] public string runtimeOverride;

            public string Resolve()
            {
                if (!string.IsNullOrEmpty(runtimeOverride)) return runtimeOverride;

                // 1) Environment variable
                try
                {
                    var env = Environment.GetEnvironmentVariable(envVar);
                    if (!string.IsNullOrEmpty(env)) return env;
                }
                catch { /* sandboxed platforms may throw */ }

                // 2) Serialized fallback (avoid using in production)
                return serializedFallback;
            }
        }

        [Header("Keys")]
        public List<KeyEntry> keys = new List<KeyEntry>
        {
            new KeyEntry { id="openai", envVar="OPENAI_API_KEY" },
            new KeyEntry { id="groq",   envVar="GROQ_API_KEY"   }
        };

        public string Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var k in keys)
                if (string.Equals(k.id, id, StringComparison.OrdinalIgnoreCase))
                    return k.Resolve();
            return null;
        }

        /// <summary>Helper: return standard Bearer header for a provider id.</summary>
        public (string name, string value)? GetBearerHeader(string id)
        {
            var key = Get(id);
            if (string.IsNullOrEmpty(key)) return null;
            return ("Authorization", $"Bearer {key}");
        }
    }
}
