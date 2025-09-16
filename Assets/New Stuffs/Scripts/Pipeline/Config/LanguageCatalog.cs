using System;
using System.Collections.Generic;
using UnityEngine;

namespace YourApp.Signs.Pipeline.Config
{
    /// <summary>
    /// Catalog of supported languages and fallbacks used by UI and routing.
    /// Create via: Assets → Create → Sign Pipeline → Language Catalog
    /// </summary>
    [CreateAssetMenu(menuName = "Sign Pipeline/Language Catalog", fileName = "LanguageCatalog")]
    public class LanguageCatalog : ScriptableObject
    {
        [Serializable]
        public class Lang
        {
            [Tooltip("ISO 639-1/BCP47 like 'en', 'yo', 'ha', 'ig'.")]
            public string iso = "en";

            [Tooltip("User-facing name, e.g., 'English', 'Yorùbá'.")]
            public string displayName = "English";

            [Tooltip("If STT supports this natively (no translation needed for extraction).")]
            public bool sttSupported = true;

            [Tooltip("If translation to English is required before gloss extraction.")]
            public bool needsTranslationToEnglish = false;

            [Tooltip("List of fallback ISO codes in order of preference (e.g., ['en']).")]
            public List<string> fallbacks = new List<string> { "en" };

            [Tooltip("Is this a right-to-left script? (affects UI)")]
            public bool isRTL = false;
        }

        [Header("Languages")]
        public List<Lang> languages = new List<Lang>
        {
            new Lang{ iso="en", displayName="English", sttSupported=true, needsTranslationToEnglish=false, fallbacks=new List<string>() },
            new Lang{ iso="yo", displayName="Yorùbá", sttSupported=true, needsTranslationToEnglish=true,  fallbacks=new List<string>{"en"} },
            new Lang{ iso="ha", displayName="Hausa",   sttSupported=true, needsTranslationToEnglish=true,  fallbacks=new List<string>{"en"} },
            new Lang{ iso="ig", displayName="Igbo",    sttSupported=true, needsTranslationToEnglish=true,  fallbacks=new List<string>{"en"} },
        };

        [Header("Defaults")]
        public string defaultIso = "en";

        public Lang Get(string iso)
        {
            if (string.IsNullOrEmpty(iso)) iso = defaultIso;
            foreach (var l in languages)
                if (string.Equals(l.iso, iso, StringComparison.OrdinalIgnoreCase))
                    return l;
            return null;
        }

        /// <summary>Return chain: [iso] + fallbacks (de-duplicated).</summary>
        public List<string> ResolutionChain(string iso)
        {
            var list = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void add(string s)
            {
                if (!string.IsNullOrEmpty(s) && seen.Add(s)) list.Add(s);
            }

            var lang = Get(iso) ?? Get(defaultIso);
            add(lang?.iso);
            if (lang?.fallbacks != null) foreach (var f in lang.fallbacks) add(f);
            return list;
        }

        public bool RequiresTranslationBeforeGloss(string iso)
        {
            var l = Get(iso) ?? Get(defaultIso);
            return l != null && l.needsTranslationToEnglish;
        }
    }
}
