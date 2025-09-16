// using System;
// using System.Collections.Generic;
// using UnityEngine;
//
// /// <summary>
// /// Per-language routing profile:
// ///   lang -> { transcriber, translator, glossExtractor, outputLocale, promptOverride }
// /// The behaviours should implement the interfaces from SignPipelineController:
// ///   ITranscriber, ITranslator, IGlossExtractor
// /// </summary>
// namespace YourApp.Signs.Pipeline.Language
// {
//     [CreateAssetMenu(menuName = "Sign Pipeline/Language Route Profile", fileName = "LanguageRouteProfile")]
//     public class LanguageRouteProfile : ScriptableObject
//     {
//         [Serializable]
//         public class Route
//         {
//             [Tooltip("ISO 639-1 code, e.g., en, yo, ha, ig")]
//             public string langIso = "en";
//
//             [Header("Providers (scene objects implementing the interfaces)")]
//             public MonoBehaviour transcriberBehaviour;   // ITranscriber
//             public MonoBehaviour translatorBehaviour;    // ITranslator (can be null for English)
//             public MonoBehaviour glossExtractorBehaviour;// IGlossExtractor
//
//             [Header("Output")]
//             [Tooltip("Language the extractor expects (typically 'en').")]
//             public string outputLocale = "en";
//
//             [Header("Prompts / Overrides (optional)")]
//             [Tooltip("Optional prompt asset passed to your extractor/translator (if they support it).")]
//             public TextAsset promptOverride;
//         }
//
//         [Header("Routes")]
//         public List<Route> routes = new List<Route>();
//
//         [Header("Default (fallback when no lang match)")]
//         public Route defaultRoute = new Route
//         {
//             langIso = "en",
//             outputLocale = "en"
//         };
//
//         public Route GetRouteOrDefault(string iso)
//         {
//             if (routes != null)
//             {
//                 foreach (var r in routes)
//                 {
//                     if (r != null && string.Equals(r.langIso, iso, StringComparison.OrdinalIgnoreCase))
//                         return r;
//                 }
//             }
//             return defaultRoute;
//         }
//     }
// }
