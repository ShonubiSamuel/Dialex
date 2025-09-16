// using System.Threading.Tasks;
// using Newtonsoft.Json.Linq;
// using UnityEngine;
//
// public class LanguageService
// {
//     private readonly ITranscriber _transcriber;
//     private readonly ITranslator _translator;
//     private readonly IGlossifier _glossifier;
//
//     public LanguageService(ITranscriber transcriber, ITranslator translator, IGlossifier glossifier)
//     {
//         _transcriber = transcriber;
//         _translator = translator;
//         _glossifier = glossifier;
//     }
//
//     public async Task<(string nativeText, JObject gloss)> AudioToGlossWithTranscript(string audioPath)
//     {
//         string nativeText = await _transcriber.Transcribe(audioPath);
//         JObject gloss = await TextToGloss(nativeText);
//         return (nativeText, gloss);
//     }
//
//
//     public async Task<JObject> TextToGloss(string nativeText)
//     {
//         string englishText = _translator != null
//             ? await _translator.Translate(nativeText)
//             : nativeText;
//
//         JObject result = await _glossifier.TextToGloss(englishText);
//         string content = result["choices"]?[0]?["message"]?["content"]?.ToString();
//
//         if (!string.IsNullOrEmpty(content))
//         {
//             JObject parsed = JObject.Parse(content);
//             Debug.Log("Gloss: " + string.Join(", ", parsed["gloss"] ?? new JArray()));
//             Debug.Log("Types: " + string.Join(", ", parsed["types"] ?? new JArray()));
//         }
//
//         return result;
//     }
// }