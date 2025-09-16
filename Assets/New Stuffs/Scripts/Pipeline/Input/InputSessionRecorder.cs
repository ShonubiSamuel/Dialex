using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace YourApp.Signs.Pipeline.Input
{
    /// <summary>
    /// Records pipeline runs for regression: inputs, transcripts, translations, glosses, keys.
    /// Writes JSON files under persistentDataPath/Sessions/.
    /// </summary>
    public class InputSessionRecorder : MonoBehaviour
    {
        [Header("Options")]
        public bool autoSubscribe = true;
        public string folderName = "Sessions";

        private readonly Dictionary<string, Session> _sessions = new();

        [Serializable]
        private class Session
        {
            public string id;
            public string language;
            public string originalText;
            public string transcript;
            public string translatedText;
            public List<string> glosses = new();
            public List<string> keys = new();
            public string startedUtc;
            public string endedUtc;
            public List<EventEntry> events = new();
        }

        [Serializable]
        private class EventEntry
        {
            public string timeUtc;
            public string stage;
            public string note;
        }

        private void OnEnable()
        {
            if (!autoSubscribe) return;

            PipelineEvents.OnInputReady     += OnInputReady;
            PipelineEvents.OnTranscribed    += OnTranscribed;
            PipelineEvents.OnTranslated     += OnTranslated;
            PipelineEvents.OnGlossList      += OnGlossList;
            PipelineEvents.OnPlaybackStart  += OnPlaybackStart;
            PipelineEvents.OnPlaybackEnd    += OnPlaybackEnd;
            PipelineEvents.OnError          += OnError;
        }

        private void OnDisable()
        {
            if (!autoSubscribe) return;

            PipelineEvents.OnInputReady     -= OnInputReady;
            PipelineEvents.OnTranscribed    -= OnTranscribed;
            PipelineEvents.OnTranslated     -= OnTranslated;
            PipelineEvents.OnGlossList      -= OnGlossList;
            PipelineEvents.OnPlaybackStart  -= OnPlaybackStart;
            PipelineEvents.OnPlaybackEnd    -= OnPlaybackEnd;
            PipelineEvents.OnError          -= OnError;
        }

        private void OnInputReady(PipelineEvents.InputReadyArgs a)
        {
            var s = NewOrGet(a.SessionId);
            s.language = a.Language;
            s.originalText = a.OriginalText;
            s.startedUtc = DateTime.UtcNow.ToString("o");
            AddEvent(s, "InputReady", $"lang={a.Language}");
        }

        private void OnTranscribed(PipelineEvents.TranscribedArgs a)
        {
            var s = NewOrGet(a.SessionId);
            s.transcript = a.Transcript;
            if (!string.IsNullOrEmpty(a.Language)) s.language = a.Language;
            AddEvent(s, "Transcribed", $"{a.AudioSeconds:0.00}s");
        }

        private void OnTranslated(PipelineEvents.TranslatedArgs a)
        {
            var s = NewOrGet(a.SessionId);
            s.translatedText = a.TranslatedText;
            AddEvent(s, "Translated", $"{a.SourceLanguage}->{a.TargetLanguage}");
        }

        private void OnGlossList(PipelineEvents.GlossListArgs a)
        {
            var s = NewOrGet(a.SessionId);
            s.glosses = new List<string>(a.GlossesNormalized ?? new List<string>());
            AddEvent(s, "GlossList", $"{s.glosses.Count} glosses");
        }

        private void OnPlaybackStart(PipelineEvents.PlaybackArgs a)
        {
            var s = NewOrGet(a.SessionId);
            s.keys = new List<string>(a.Keys ?? Array.Empty<string>());
            AddEvent(s, "PlaybackStart", $"{s.keys.Count} keys");
        }

        private void OnPlaybackEnd(PipelineEvents.PlaybackArgs a)
        {
            var s = NewOrGet(a.SessionId);
            AddEvent(s, "PlaybackEnd", $"{(s.keys?.Count ?? 0)} keys");
            s.endedUtc = DateTime.UtcNow.ToString("o");
            SaveSession(s);
        }

        private void OnError(PipelineEvents.ErrorArgs a)
        {
            var s = NewOrGet(a.SessionId);
            AddEvent(s, $"Error:{a.Stage}", a.Message ?? "");
            s.endedUtc = DateTime.UtcNow.ToString("o");
            SaveSession(s);
        }

        private Session NewOrGet(string id)
        {
            if (string.IsNullOrEmpty(id)) id = Guid.NewGuid().ToString("N");
            if (!_sessions.TryGetValue(id, out var s))
            {
                s = new Session { id = id };
                _sessions[id] = s;
            }
            return s;
        }

        private void AddEvent(Session s, string stage, string note)
        {
            s.events.Add(new EventEntry
            {
                timeUtc = DateTime.UtcNow.ToString("o"),
                stage = stage,
                note = note
            });
        }

        private void SaveSession(Session s)
        {
            try
            {
                var dir = Path.Combine(Application.persistentDataPath, folderName);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, $"{Sanitize(s.id)}.json");
                var json = JsonUtility.ToJson(s, true);
                File.WriteAllText(path, json);
                Debug.Log($"[InputSessionRecorder] Saved: {path}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[InputSessionRecorder] Save failed: {e.Message}");
            }
        }

        private string Sanitize(string id)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                id = id.Replace(c, '_');
            return id;
        }
    }
}
