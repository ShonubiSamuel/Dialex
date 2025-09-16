using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace YourApp.Signs.Pipeline.Deterministic
{
    /// <summary>
    /// Closed, deterministic mapper:
    /// 1) Tokenize (lowercase, remove diacritics, keep numbers/decimals)
    /// 2) Greedy phrase match (longest, priority)
    /// 3) Numbers → keys ("7.2"→"7_2")
    /// 4) Single-token alias
    /// 5) Drop stopwords
    /// 6) Validate keys with SignResolver (optional)
    /// </summary>
    public sealed class DeterministicGlossMapper
    {
        // Regex: words (letters + apostrophe), or numbers with optional decimals
        private static readonly Regex TokenRegex = new Regex(@"[a-z']+|\d+(?:\.\d+)?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // ---- Data ----
        private readonly HashSet<string> _stopwords = new HashSet<string>();
        private readonly Dictionary<string, List<PhraseEntry>> _phraseIndex = new Dictionary<string, List<PhraseEntry>>();
        private readonly Dictionary<string, List<TokenEntry>> _tokenMapByToken = new Dictionary<string, List<TokenEntry>>();

        private readonly bool _validateWithResolver;
        private readonly Func<string, bool> _keyExists; // resolver.Contains

        private struct PhraseEntry
        {
            public string lang;
            public string key;
            public int priority;
            public string[] pat; // tokens
        }
        private struct TokenEntry
        {
            public string lang;
            public string key;
            public int priority;
        }

        public DeterministicGlossMapper(
            GlossAliases data,
            Func<string, bool> keyExists = null, // optional SignResolver.Contains
            bool validateKeysWithResolver = true)
        {
            _validateWithResolver = validateKeysWithResolver;
            _keyExists = keyExists ?? (_ => true);

            Build(data);
        }

        private void Build(GlossAliases data)
        {
            _stopwords.Clear();
            if (data != null && data.stopwords != null)
                foreach (var s in data.stopwords)
                    if (!string.IsNullOrWhiteSpace(s))
                        _stopwords.Add(NormToken(s));

            _phraseIndex.Clear();
            if (data != null && data.phrases != null)
            {
                foreach (var p in data.phrases)
                {
                    if (string.IsNullOrWhiteSpace(p.key)) continue;

                    string[] pat = p.patternTokens != null && p.patternTokens.Length > 0
                        ? NormalizeTokens(p.patternTokens)
                        : TokenizeToTokens(p.display);

                    if (pat.Length == 0) continue;

                    var pe = new PhraseEntry
                    {
                        lang = NormLang(p.langIso),
                        key = p.key.Trim(),
                        priority = p.priority,
                        pat = pat
                    };
                    var first = pat[0];
                    if (!_phraseIndex.TryGetValue(first, out var list))
                    {
                        list = new List<PhraseEntry>();
                        _phraseIndex[first] = list;
                    }
                    list.Add(pe);
                }

                // Sort each bucket: longer first, then priority desc
                foreach (var kv in _phraseIndex)
                {
                    kv.Value.Sort((a, b) =>
                    {
                        int len = b.pat.Length.CompareTo(a.pat.Length);
                        if (len != 0) return len;
                        return b.priority.CompareTo(a.priority);
                    });
                }
            }

            _tokenMapByToken.Clear();
            if (data != null && data.tokens != null)
            {
                foreach (var t in data.tokens)
                {
                    if (string.IsNullOrWhiteSpace(t.token) || string.IsNullOrWhiteSpace(t.key)) continue;
                    var tok = NormToken(t.token);
                    var te = new TokenEntry { lang = NormLang(t.langIso), key = t.key.Trim(), priority = t.priority };

                    if (!_tokenMapByToken.TryGetValue(tok, out var list))
                    {
                        list = new List<TokenEntry>();
                        _tokenMapByToken[tok] = list;
                    }
                    list.Add(te);
                }

                // Priority desc
                foreach (var kv in _tokenMapByToken)
                    kv.Value.Sort((a, b) => b.priority.CompareTo(a.priority));
            }
        }

        // ---- Public API ----

        public List<string> Map(string input,  out Coverage coverage, string langIso = "en", bool dedupeConsecutive = true)
        {
            coverage = new Coverage();
            var keys = new List<string>();
            if (string.IsNullOrWhiteSpace(input)) return keys;

            var tokens = TokenizeToTokens(input); // already normalized
            var lang = NormLang(langIso);
            coverage.totalTokens = tokens.Length;

            for (int i = 0; i < tokens.Length;)
            {
                var t = tokens[i];

                // 1) Phrase match (greedy)
                if (_phraseIndex.TryGetValue(t, out var plist))
                {
                    foreach (var pe in plist)
                    {
                        if (!LangOk(pe.lang, lang)) continue;
                        int L = pe.pat.Length;
                        if (i + L > tokens.Length) continue;

                        bool ok = true;
                        for (int k = 0; k < L; k++)
                            if (!tokens[i + k].Equals(pe.pat[k], StringComparison.Ordinal))
                            { ok = false; break; }

                        if (ok && KeyOk(pe.key))
                        {
                            Push(keys, pe.key, dedupeConsecutive);
                            coverage.phraseHits++;
                            i += L;
                            goto nextToken; // continue outer loop
                        }
                    }
                }

                // 2) Numbers (with decimals)
                if (IsNumberToken(t))
                {
                    string key = t.Replace('.', '_'); // 7.2 -> 7_2
                    if (KeyOk(key))
                    {
                        Push(keys, key, dedupeConsecutive);
                        coverage.numberHits++;
                    }
                    else
                    {
                        coverage.dropped++;
                    }
                    i++;
                    goto nextToken;
                }

                if (_tokenMapByToken.TryGetValue(t, out var tl))
                {
                    foreach (var te in tl)
                    {
                        if (!LangOk(te.lang, lang)) continue;
                        if (KeyOk(te.key))
                        {
                            Push(keys, te.key, dedupeConsecutive);
                            coverage.tokenHits++;
                            break;
                        }
                    }
                    i++;
                    goto nextToken;
                }

                // 3b) IDENTITY FALLBACK — if the token itself is a valid key, take it
                if (KeyOk(t))
                {
                    Push(keys, t, dedupeConsecutive);
                    coverage.tokenHits++;
                    i++;
                    goto nextToken;
                }

                // 4) Stopwords
                if (_stopwords.Contains(t))
                {
                    coverage.stopwordSkipped++;
                    i++;
                    goto nextToken;
                }

                // 5) Unknown -> drop, count as OOV
                coverage.oovTokens++;
                i++;

            nextToken:;
            }

            return keys;
        }

        public struct Coverage
        {
            public int totalTokens;
            public int phraseHits;
            public int tokenHits;
            public int numberHits;
            public int stopwordSkipped;
            public int dropped;
            public int oovTokens;
        }

        // ---- Helpers ----

        private static void Push(List<string> keys, string key, bool dedupeConsecutive)
        {
            if (!dedupeConsecutive || keys.Count == 0 || !string.Equals(keys[^1], key, StringComparison.Ordinal))
                keys.Add(key);
        }

        private bool KeyOk(string key) => !_validateWithResolver || _keyExists(key);

        private static bool IsNumberToken(string t)
        {
            // digits with optional decimal (already normalized)
            for (int i = 0; i < t.Length; i++)
            {
                char c = t[i];
                if (!(char.IsDigit(c) || c == '.')) return false;
            }
            // must contain at least one digit
            return t != ".";
        }

        private static string NormLang(string iso) =>
            string.IsNullOrWhiteSpace(iso) ? "" : iso.Trim().ToLowerInvariant();

        private static bool LangOk(string aliasLang, string selected) =>
            string.IsNullOrEmpty(aliasLang) || aliasLang == selected;

        private static string[] NormalizeTokens(string[] arr)
        {
            var list = new List<string>(arr.Length);
            foreach (var a in arr)
            {
                var t = NormToken(a);
                if (!string.IsNullOrEmpty(t)) list.Add(t);
            }
            return list.ToArray();
        }

        private static string[] TokenizeToTokens(string s)
        {
            s = PreNormalize(s);
            var list = new List<string>();
            foreach (Match m in TokenRegex.Matches(s))
            {
                var tok = m.Value;
                // strip leading/trailing apostrophes e.g., "valentine's" → "valentine's" (kept), "'cause"→"cause"
                tok = tok.Trim('\'');
                if (!string.IsNullOrEmpty(tok))
                    list.Add(tok);
            }
            return list.ToArray();
        }

        private static string PreNormalize(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;

            s = s.Trim().ToLowerInvariant();

            // Replace connectors consistently (parity with your GlossNormalizer)
            s = s.Replace("&", " and ").Replace("+", " and ");

            // Remove diacritics (yorùbá etc.)
            s = RemoveDiacritics(s);

            // Normalize hyphens/underscores to space for tokenization
            s = s.Replace('-', ' ').Replace('_', ' ');

            // Collapse whitespace
            s = Regex.Replace(s, @"\s+", " ");

            return s;
        }

        private static string NormToken(string t)
        {
            if (string.IsNullOrWhiteSpace(t)) return "";
            t = t.Trim().ToLowerInvariant();
            t = t.Replace("&", " and ").Replace("+", " and ");
            t = RemoveDiacritics(t);
            t = t.Replace('-', ' ').Replace('_', ' ');
            t = Regex.Replace(t, @"\s+", " ").Trim();
            // If token contains spaces after normalization, we keep it; caller decides how to use.
            return t;
        }

        private static string RemoveDiacritics(string s)
        {
            var norm = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(s.Length);
            foreach (var ch in norm)
            {
                var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (uc != UnicodeCategory.NonSpacingMark) sb.Append(ch);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
