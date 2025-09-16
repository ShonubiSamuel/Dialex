using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Central orchestrator with explicit language selection (no auto-detect).
public class SignPipelineController : MonoBehaviour
{
    [Header("Runtime Targets")]
    public SignQueueController queueController;
    public SignPlaybackController playbackController;

    [Header("Resolvers / Loaders (optional)")]
    public SignResolver resolver; // for Contains() checks only

    [Header("Pipeline Providers")]
    [Tooltip("Transcriber used for AUDIO. Can be a router (e.g., MultiTranscriberByLanguage).")]
    public MonoBehaviour transcriberBehaviour; // ITranscriber
    [Tooltip("Translator used when selected language != extractorTargetLang (usually 'en').")]
    public MonoBehaviour translatorBehaviour;  // ITranslator
    [Tooltip("Gloss extractor that takes ENGLISH text and returns gloss list.")]
    public MonoBehaviour glossExtractorBehaviour; // IGlossExtractor

    [Header("Language")]
    [Tooltip("Current selected language ISO (e.g., 'en', 'yo', 'ha', 'ig'). Controlled by LanguageSelectorUI.")]
    public string currentLanguageIso = "en";
    [Tooltip("Target language for gloss extractor (usually 'en').")]
    public string extractorTargetLang = "en";

    [Header("Validation")]
    public bool validateKeysWithResolver = false;

    [Header("State (read-only)")]
    [SerializeField] private bool _isRunning;
    [SerializeField] private string _sessionId;
    
    [Header("Behavior")]
    [Tooltip("If ON, transcribed audio is immediately fed into the text pipeline. If OFF, it is only emitted (UI should submit manually).")]
    public bool autoProcessTranscripts = false;

    // Interfaces
    private ITranscriber _transcriber;
    private ITranslator _translator;
    private IGlossExtractor _glossExtractor;

    private Coroutine _runCo;

    // ---------- Contracts ----------
    public sealed class TranscriptionResult { public string text; public string language; public float duration; }
    public interface ITranscriber
    {
        IEnumerator TranscribeAsync(AudioClip audio, string langHint,
            Action<TranscriptionResult> onDone, Action<Exception> onError = null);
    }
    public interface ITranslator
    {
        IEnumerator TranslateAsync(string srcText, string srcLang, string dstLang,
            Action<string> onDone, Action<Exception> onError = null);
    }
    public interface IGlossExtractor
    {
        IEnumerator ExtractAsync(string englishText,
            Action<List<string>> onDone, Action<Exception> onError = null);
    }

    // ---------- Public API ----------
    public void SetLanguage(string iso) => currentLanguageIso = string.IsNullOrWhiteSpace(iso) ? "en" : iso.ToLowerInvariant();

    // UI hook (TextInputController → OnSubmit)
    public void SubmitText(string text) => SubmitTextWithHint(text, null);
    public void SubmitTextWithHint(string text, string _unused)  // kept for compatibility
    {
        if (_isRunning) CancelCurrent();
        _runCo = StartCoroutine(RunTextPipeline(text));
    }

    public void SubmitAudio(AudioClip clip) => SubmitAudioWithHint(clip, null);
    public void SubmitAudioWithHint(AudioClip clip, string _unused)
    {
        if (_isRunning) CancelCurrent();
        _runCo = StartCoroutine(RunAudioPipeline(clip));
    }

    public void CancelCurrent()
    {
        if (_runCo != null) StopCoroutine(_runCo);
        _isRunning = false;
        _runCo = null;
    }

    private void Awake()
    {
        _transcriber    = transcriberBehaviour    as ITranscriber;
        _translator     = translatorBehaviour     as ITranslator;
        _glossExtractor = glossExtractorBehaviour as IGlossExtractor;
    }

    // ---------- Pipelines ----------
    private IEnumerator RunTextPipeline(string text)
    {
        _isRunning = true; _sessionId = Guid.NewGuid().ToString("N");

        string lang = string.IsNullOrWhiteSpace(currentLanguageIso) ? "en" : currentLanguageIso.ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(text))
        {
            EmitError("Input", "Empty text");
            _isRunning = false; yield break;
        }

        PipelineEvents.RaiseInputReady(new PipelineEvents.InputReadyArgs
        {
            SessionId = _sessionId,
            OriginalText = text,
            Language = lang
        });

        string englishText = text;
        if (!string.Equals(lang, extractorTargetLang, StringComparison.OrdinalIgnoreCase))
        {
            if (_translator == null)
            {
                EmitError("Translate", $"No translator assigned but language is '{lang}'.");
                _isRunning = false; yield break;
            }

            print(_translator.GetType().Name);
            string translated = null; Exception ex = null; bool done = false;
            yield return _translator.TranslateAsync(text, lang, extractorTargetLang,
                r => { translated = r; done = true; }, e => { ex = e; done = true; });

            if (ex != null) { EmitError("Translate", ex.Message, ex); _isRunning = false; yield break; }

            englishText = translated ?? "";
            PipelineEvents.RaiseTranslated(new PipelineEvents.TranslatedArgs
            {
                SessionId = _sessionId,
                SourceLanguage = lang,
                TargetLanguage = extractorTargetLang,
                SourceText = text,
                TranslatedText = englishText
            });
        }

        if (_glossExtractor == null)
        {
            EmitError("ExtractGloss", "No gloss extractor assigned.");
            _isRunning = false; yield break;
        }

        List<string> glossesRaw = null; Exception exG = null; bool doneG = false;
        yield return _glossExtractor.ExtractAsync(englishText,
            r => { glossesRaw = r; doneG = true; }, e => { exG = e; doneG = true; });

        if (exG != null) { EmitError("ExtractGloss", exG.Message, exG); _isRunning = false; yield break; }

        var normalized = NormalizeGlossList(glossesRaw);
        if (validateKeysWithResolver && resolver != null)
            normalized.RemoveAll(k => !resolver.Contains(k));

        PipelineEvents.RaiseGlossList(new PipelineEvents.GlossListArgs
        {
            SessionId = _sessionId,
            EnglishText = englishText,
            GlossesRaw = glossesRaw ?? new List<string>(),
            GlossesNormalized = normalized
        });

        EnqueueKeys(normalized);
        _isRunning = false;
    }

    private IEnumerator RunAudioPipeline(AudioClip clip)
    {
        _isRunning = true; _sessionId = Guid.NewGuid().ToString("N");

        if (clip == null)
        {
            EmitError("Input", "No audio clip.");
            _isRunning = false; yield break;
        }
        if (_transcriber == null)
        {
            EmitError("Transcribe", "No transcriber assigned.");
            _isRunning = false; yield break;
        }

        string lang = string.IsNullOrWhiteSpace(currentLanguageIso) ? "en" : currentLanguageIso.ToLowerInvariant();

        TranscriptionResult tr = null; Exception ex = null; bool done = false;
        yield return _transcriber.TranscribeAsync(clip, lang,
            r => { tr = r; done = true; }, e => { ex = e; done = true; });

        if (ex != null) { EmitError("Transcribe", ex.Message, ex); _isRunning = false; yield break; }

        string transcript = tr?.text ?? "";

        PipelineEvents.RaiseTranscribed(new PipelineEvents.TranscribedArgs
        {
            SessionId = _sessionId,
            Transcript = transcript,
            Language = lang,
            AudioSeconds = tr?.duration ?? 0f
        });

        // >>> CHANGE: only auto-continue if enabled
        if (autoProcessTranscripts)
        {
            // Continue through the text path with the selected language
            yield return RunTextPipeline(transcript);
        }
        
        _isRunning = false;
    }
    
   
    // ---------- Helpers ----------
    private List<string> NormalizeGlossList(List<string> input)
    {
        var list = new List<string>();
        if (input == null) return list;
        foreach (var g in input)
        {
            if (string.IsNullOrWhiteSpace(g)) continue;
            var k = GlossNormalizer.Normalize(g);
            if (!string.IsNullOrEmpty(k)) list.Add(k);
        }
        for (int i = list.Count - 1; i > 0; i--)
            if (list[i] == list[i - 1]) list.RemoveAt(i);
        return list;
    }

    private void EnqueueKeys(List<string> keys)
    {
        if (keys == null || keys.Count == 0) { EmitError("Schedule", "No gloss keys."); return; }

        PipelineEvents.RaisePlaybackStart(new PipelineEvents.PlaybackArgs { SessionId = _sessionId, Keys = keys });

        if (queueController != null) queueController.PlaySequenceFromKeys(keys);
        else if (playbackController != null) foreach (var k in keys) playbackController.Enqueue(k);
        else EmitError("Schedule", "No queueController or playbackController assigned.");
    }

    private void EmitError(string stage, string msg, Exception ex = null)
    {
        Debug.LogError($"[SignPipelineController] {stage}: {msg}");
        PipelineEvents.RaiseError(new PipelineEvents.ErrorArgs
        {
            SessionId = _sessionId,
            Stage = stage,
            Message = msg,
            Exception = ex
        });
    }
}
