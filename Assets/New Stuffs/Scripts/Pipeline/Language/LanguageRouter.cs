// using System;
// using UnityEngine;
//
// /// <summary>
// /// Chooses transcriber/translator/glossExtractor for a detected language
// /// using a LanguageRouteProfile, and can apply them to a SignPipelineController.
// /// </summary>
// namespace YourApp.Signs.Pipeline.Language
// {
//     public class LanguageRouter : MonoBehaviour
//     {
//         [Header("Profile")]
//         public LanguageRouteProfile profile;
//
//         [Header("Behavior")]
//         public bool allowFallbackToDefault = true;
//
//         [Serializable]
//         public struct RouteResolution
//         {
//             public string langIso;
//             public string outputLocale;
//
//             // Interfaces (what the pipeline actually needs)
//             public SignPipelineController.ITranscriber   transcriber;
//             public SignPipelineController.ITranslator    translator;   // can be null for English
//             public SignPipelineController.IGlossExtractor extractor;
//
//             // Original behaviours (handy for inspector wiring / debugging)
//             public MonoBehaviour transcriberBehaviour;
//             public MonoBehaviour translatorBehaviour;
//             public MonoBehaviour glossExtractorBehaviour;
//
//             public TextAsset promptOverride;
//
//             public bool IsValid =>
//                 transcriber != null && extractor != null; // translator may be null if not needed
//         }
//
//         /// <summary>Resolve a route for the given ISO code (e.g., "yo").</summary>
//         public RouteResolution Resolve(string langIso)
//         {
//             var res = new RouteResolution { langIso = langIso };
//
//             if (profile == null)
//             {
//                 Debug.LogError("[LanguageRouter] No profile assigned.");
//                 return res;
//             }
//
//             var route = string.IsNullOrEmpty(langIso)
//                         ? profile.defaultRoute
//                         : profile.GetRouteOrDefault(langIso);
//
//             if (route == null)
//             {
//                 if (!allowFallbackToDefault)
//                     return res;
//
//                 route = profile.defaultRoute;
//             }
//
//             res.outputLocale = string.IsNullOrEmpty(route.outputLocale) ? "en" : route.outputLocale;
//             res.transcriberBehaviour = route.transcriberBehaviour;
//             res.translatorBehaviour  = route.translatorBehaviour;
//             res.glossExtractorBehaviour = route.glossExtractorBehaviour;
//             res.promptOverride = route.promptOverride;
//
//             // Cast to interfaces the pipeline expects
//             res.transcriber = route.transcriberBehaviour as SignPipelineController.ITranscriber;
//             res.translator  = route.translatorBehaviour  as SignPipelineController.ITranslator; // may be null
//             res.extractor   = route.glossExtractorBehaviour as SignPipelineController.IGlossExtractor;
//
//             // Warn if anything crucial is missing
//             if (res.transcriber == null)
//                 Debug.LogWarning($"[LanguageRouter] Transcriber missing or wrong type for '{route.langIso}'.");
//             if (res.extractor == null)
//                 Debug.LogWarning($"[LanguageRouter] GlossExtractor missing or wrong type for '{route.langIso}'.");
//
//             return res;
//         }
//
//         /// <summary>
//         /// Convenience: apply a resolved route to a running pipeline (sets provider behaviours & target locale).
//         /// Call this after you detect language (or whenever user switches language).
//         /// </summary>
//         public bool ApplyToPipeline(SignPipelineController pipeline, string langIso)
//         {
//             if (pipeline == null) { Debug.LogError("[LanguageRouter] Pipeline is null."); return false; }
//
//             var r = Resolve(langIso);
//             if (!r.IsValid)
//             {
//                 Debug.LogError($"[LanguageRouter] Route for '{langIso}' is invalid.");
//                 return false;
//             }
//
//             // Assign provider behaviours (the pipeline casts to interfaces internally)
//             //pipeline.defaultTranscriberBehaviour = r.transcriberBehaviour;
//             pipeline.translatorBehaviour         = r.translatorBehaviour;   // can be null for English
//             pipeline.glossExtractorBehaviour     = r.glossExtractorBehaviour;
//
//             // Ensure extractor target language (usually "en")
//             pipeline.extractorTargetLang = r.outputLocale;
//
//             // (Optional) If your providers accept prompt overrides, pass r.promptOverride via their own API.
//
//             return true;
//         }
//     }
// }
