using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MultiTranscriberByLanguage : MonoBehaviour, SignPipelineController.ITranscriber
{
    [Serializable]
    public class Entry
    {
        [Tooltip("ISO code (e.g., 'en','yo','ha','ig')")]
        public string iso = "en";
        [Tooltip("Component that implements ITranscriber for this language.")]
        public MonoBehaviour transcriber; // ITranscriber
    }

    [Header("Map language → transcriber")]
    public List<Entry> entries = new();

    [Header("Fallback")]
    [Tooltip("If set, used when no exact ISO match is found.")]
    public MonoBehaviour fallbackTranscriber; // ITranscriber

    private readonly Dictionary<string, SignPipelineController.ITranscriber> _map =
        new Dictionary<string, SignPipelineController.ITranscriber>(StringComparer.OrdinalIgnoreCase);

    private SignPipelineController.ITranscriber _fallback;

    private void Awake()
    {
        _map.Clear();
        foreach (var e in entries)
        {
            var t = e.transcriber as SignPipelineController.ITranscriber;
            if (t == null && e.transcriber != null)
                Debug.LogError($"[MultiTranscriberByLanguage] {e.transcriber.name} does not implement ITranscriber.");
            if (t != null && !string.IsNullOrWhiteSpace(e.iso))
                _map[e.iso.ToLowerInvariant()] = t;
        }
        _fallback = fallbackTranscriber as SignPipelineController.ITranscriber;
    }

    public IEnumerator TranscribeAsync(AudioClip audio, string langHint,
        Action<SignPipelineController.TranscriptionResult> onDone, Action<Exception> onError = null)
    {
        var iso = string.IsNullOrWhiteSpace(langHint) ? "en" : langHint.ToLowerInvariant();
        if (!_map.TryGetValue(iso, out var impl)) impl = _fallback;

        if (impl == null)
        {
            onError?.Invoke(new Exception($"No transcriber for language '{iso}'."));
            yield break;
        }

        SignPipelineController.TranscriptionResult res = null; Exception ex = null; bool done = false;
        yield return impl.TranscribeAsync(audio, iso, r => { res = r; done = true; }, e => { ex = e; done = true; });

        if (ex != null) onError?.Invoke(ex);
        else onDone?.Invoke(res);
    }
}
