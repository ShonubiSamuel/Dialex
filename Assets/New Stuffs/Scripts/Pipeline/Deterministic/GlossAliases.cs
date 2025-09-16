using System;
using System.Collections.Generic;
using UnityEngine;

namespace YourApp.Signs.Pipeline.Deterministic
{
    /// <summary>
    /// Authorable alias registry.
    /// - Phrases: multi-token patterns → single key (highest priority, greedy longest).
    /// - Tokens : single token → single key.
    /// - Stopwords: tokens to skip if not matched (e.g., "the","and","to").
    /// Optional per-language ISO filter ("" = all languages).
    /// </summary>
    [CreateAssetMenu(menuName = "Sign Pipeline/Gloss Aliases", fileName = "GlossAliases")]
    public class GlossAliases : ScriptableObject
    {
        [Serializable]
        public class PhraseAlias
        {
            [Tooltip("Optional language ISO (en/yo/ha/ig). Empty = applies to all.")]
            public string langIso = "";
            [Tooltip("Human-friendly phrase; we’ll tokenize/normalize it to patternTokens.")]
            public string display = "";
            [Tooltip("If set, overrides 'display' tokenization. Provide already-normalized tokens.")]
            public string[] patternTokens = Array.Empty<string>();
            [Tooltip("Destination key from your library (e.g., 'valentines_day').")]
            public string key = "";
            [Tooltip("Bigger wins when two phrases overlap at the same place.")]
            public int priority = 0;
        }

        [Serializable]
        public class TokenAlias
        {
            [Tooltip("Optional language ISO (en/yo/ha/ig). Empty = all.")]
            public string langIso = "";
            [Tooltip("Single token to match (normalized).")]
            public string token = "";
            [Tooltip("Destination key from your library.")]
            public string key = "";
            [Tooltip("Bigger wins if multiple entries set the same token.")]
            public int priority = 0;
        }

        [Header("Multi-word phrases (greedy longest, by priority)")]
        public List<PhraseAlias> phrases = new List<PhraseAlias>();

        [Header("Single tokens")]
        public List<TokenAlias> tokens = new List<TokenAlias>();

        [Header("Stopwords (normalized tokens to ignore if unmatched)")]
        public List<string> stopwords = new List<string>()
        {
            "the","a","an","and","to","of","in","on","for","with","by","at","is","are","am","be"
        };

        [Header("Examples (optional starter data)")]
        [TextArea(2,6)]
        public string notes =
            "Examples to add:\n" +
            "- Phrase: \"valentine's day\" → key: valentines_day\n" +
            "- Phrase: \"back and forth\" → key: back_and_forth_1\n" +
            "- Token : \"zero\" → key: 0 (if you also ship digit keys)\n" +
            "- Yoruba numbers (normalized): 'okan' → 1, 'meji' → 2\n" +
            "- Hausa numbers: 'daya' → 1, 'biyu' → 2\n";
    }
}
