// using System;
// using System.Collections;
// using System.Linq;
// using System.Text;
// using UnityEngine;
//
// /// <summary>
// /// Lightweight language detector.
// /// - TEXT: heuristic based on distinctive characters + stopwords → "yo","ha","ig","en".
// /// - AUDIO: returns null (let your STT handle autodetect), or add your own model call.
// /// Implements SignPipelineController.ILanguageDetector.
// /// </summary>
// namespace YourApp.Signs.Pipeline.Language
// {
//     public class LanguageDetector : MonoBehaviour, SignPipelineController.ILanguageDetector
//     {
//         [Header("Defaults")]
//         [Tooltip("If detection is ambiguous, fall back to this language.")]
//         public string fallbackIso = "en";
//
//         [Tooltip("Force English when the text is plain ASCII and looks like English sentences.")]
//         public bool preferEnglishOnAscii = true;
//
//         // Distinctive characters (quick wins)
//         private static readonly char[] YO_CHARS = "ẹẸọỌṣṢàÀèÈìÌòÒùÙáÁéÉíÍóÓúÚ".ToCharArray(); // dotted e/o + s-dot + tones
//         private static readonly char[] IG_CHARS = "ịỊụỤọỌńŃḿḾ".ToCharArray(); // i-dot-below, u-dot-below, etc.
//         private static readonly char[] HA_CHARS = "ƙƘɓƁɗƊƴƳ".ToCharArray(); // hooked letters
//
//         // Very small stopword sets (non-exhaustive, just to break ties)
//         private static readonly string[] YO_WORDS = { "ati", "ni", "sí", "ki", "mo", "ẹ", "ọ" };
//         private static readonly string[] IG_WORDS = { "na", "bụ", "anyi", "anyị", "ga", "m", "ụ" };
//         private static readonly string[] HA_WORDS = { "da", "ne", "shi", "ta", "kai", "ku", "su", "ƙ" };
//
//         public IEnumerator DetectAsync(string text, Action<string> onDone, Action<Exception> onError = null)
//         {
//             try
//             {
//                 string iso = DetectFromText(text);
//                 onDone?.Invoke(iso);
//             }
//             catch (Exception ex)
//             {
//                 onError?.Invoke(ex);
//             }
//             yield break;
//         }
//
//         public IEnumerator DetectAsync(AudioClip audio, Action<string> onDone, Action<Exception> onError = null)
//         {
//             // Keep it simple: let your STT do language ID. Return null → router/transcriber decides.
//             onDone?.Invoke(null);
//             yield break;
//         }
//
//         private string DetectFromText(string text)
//         {
//             if (string.IsNullOrWhiteSpace(text))
//                 return fallbackIso;
//
//             string s = text.Trim();
//
//             // If mostly ASCII and looks English-ish, prefer en
//             bool asciiOnly = s.All(c => c <= 0x7F);
//             if (asciiOnly && preferEnglishOnAscii)
//             {
//                 // quick English cues: spaces, apostrophes, common words
//                 var lower = s.ToLowerInvariant();
//                 int englishCues = 0;
//                 if (lower.Contains(" the ")) englishCues++;
//                 if (lower.Contains(" and ")) englishCues++;
//                 if (lower.Contains(" you ")) englishCues++;
//                 if (englishCues >= 1) return "en";
//             }
//
//             // Score by distinctive chars
//             int yoScore = CountAny(s, YO_CHARS);
//             int igScore = CountAny(s, IG_CHARS);
//             int haScore = CountAny(s, HA_CHARS);
//
//             // Minor tie-break via stopwords (case-insensitive)
//             string l = s.ToLowerInvariant();
//             yoScore += CountWords(l, YO_WORDS);
//             igScore += CountWords(l, IG_WORDS);
//             haScore += CountWords(l, HA_WORDS);
//
//             // Choose the max; if all zero → fall back
//             if (yoScore == 0 && igScore == 0 && haScore == 0)
//                 return asciiOnly ? "en" : fallbackIso;
//
//             if (yoScore >= igScore && yoScore >= haScore) return "yo";
//             if (igScore >= yoScore && igScore >= haScore) return "ig";
//             if (haScore >= yoScore && haScore >= igScore) return "ha";
//
//             return fallbackIso;
//         }
//
//         private static int CountAny(string s, char[] set)
//         {
//             int c = 0;
//             for (int i = 0; i < s.Length; i++)
//                 if (Array.IndexOf(set, s[i]) >= 0) c++;
//             return c;
//         }
//
//         private static int CountWords(string lower, string[] words)
//         {
//             int c = 0;
//             foreach (var w in words)
//             {
//                 if (string.IsNullOrEmpty(w)) continue;
//                 // cheap contains check bounded by spaces when possible
//                 if (lower.Contains(" " + w + " ") || lower.StartsWith(w + " ") || lower.EndsWith(" " + w) || lower == w)
//                     c++;
//             }
//             return c;
//         }
//     }
// }
