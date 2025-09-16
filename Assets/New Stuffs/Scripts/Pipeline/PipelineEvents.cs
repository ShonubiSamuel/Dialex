using System;
using System.Collections.Generic;

/// <summary>
/// Lightweight static event hub for the end-to-end sign pipeline.
/// Subscribe anywhere in your app; raise only from SignPipelineController.
/// </summary>
public static class PipelineEvents
{
    // ---- Payload DTOs ----
    public sealed class InputReadyArgs
    {
        public string SessionId;       // unique per run
        public string OriginalText;    // raw user text (or post-transcription text pre-translation)
        public string Language;        // ISO code (e.g., "en", "yo")
    }

    public sealed class TranscribedArgs
    {
        public string SessionId;
        public string Transcript;      // raw transcript
        public string Language;        // language detected by STT (if provided)
        public float  AudioSeconds;    // optional
    }

    public sealed class TranslatedArgs
    {
        public string SessionId;
        public string SourceLanguage;  // e.g., "yo"
        public string TargetLanguage;  // e.g., "en"
        public string SourceText;
        public string TranslatedText;  // English
    }

    public sealed class GlossListArgs
    {
        public string SessionId;
        public string EnglishText;            // text that glosses were extracted from (English)
        public List<string> GlossesRaw;       // as returned by extractor (order matters)
        public List<string> GlossesNormalized;// normalized via GlossNormalizer
    }

    public sealed class PlaybackArgs
    {
        public string SessionId;
        public IReadOnlyList<string> Keys;    // normalized keys enqueued
    }

    public sealed class ErrorArgs
    {
        public string SessionId;
        public string Stage;        // e.g., "Transcribe", "Translate", "ExtractGloss", "Schedule"
        public string Message;
        public Exception Exception; // optional
    }

    // ---- Events ----
    public static event Action<InputReadyArgs>    OnInputReady;
    public static event Action<TranscribedArgs>   OnTranscribed;
    public static event Action<TranslatedArgs>    OnTranslated;
    public static event Action<GlossListArgs>     OnGlossList;
    public static event Action<PlaybackArgs>      OnPlaybackStart;
    public static event Action<PlaybackArgs>      OnPlaybackEnd;
    public static event Action<ErrorArgs>         OnError;

    // ---- Raise helpers (internal use) ----
    internal static void RaiseInputReady(InputReadyArgs a)     => OnInputReady?.Invoke(a);
    internal static void RaiseTranscribed(TranscribedArgs a)   => OnTranscribed?.Invoke(a);
    internal static void RaiseTranslated(TranslatedArgs a)     => OnTranslated?.Invoke(a);
    internal static void RaiseGlossList(GlossListArgs a)       => OnGlossList?.Invoke(a);
    internal static void RaisePlaybackStart(PlaybackArgs a)    => OnPlaybackStart?.Invoke(a);
    internal static void RaisePlaybackEnd(PlaybackArgs a)      => OnPlaybackEnd?.Invoke(a);
    internal static void RaiseError(ErrorArgs a)               => OnError?.Invoke(a);
}
