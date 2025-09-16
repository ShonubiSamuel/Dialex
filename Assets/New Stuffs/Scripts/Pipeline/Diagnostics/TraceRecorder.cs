using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace YourApp.Signs.Pipeline.Diagnostics
{
    [DisallowMultipleComponent]
    public class TraceRecorder : MonoBehaviour
    {
        [Header("Output")]
        public bool enabledRecording = true;
        public string folderName = "Traces";

        // active traces by session
        private readonly Dictionary<string, Trace> _traces = new();

        [Serializable]
        private class Trace
        {
            public string sessionId;
            public string startedUtc;
            public string endedUtc;

            // input
            public string detectedLanguage;
            public string originalText;
            public float  audioSeconds;

            // stt
            public string transcript;
            public string sttLanguage;

            // translation
            public string translatedText;
            public string srcLang;
            public string dstLang;

            // glossing
            public List<string> glosses = new();
            public List<string> keys    = new();

            // timing (absolute)
            public string tInput;
            public string tSTT;
            public string tTR;
            public string tGX;
            public string tPBStart;
            public string tPBEnd;

            // errors
            public string errorStage;
            public string errorMessage;

            // environment
            public string appVersion;
            public string unityVersion;
            public string platform;
            public string deviceModel;
            public string systemLanguage;
        }

        private void OnEnable()
        {
            PipelineEvents.OnInputReady    += OnInputReady;
            PipelineEvents.OnTranscribed   += OnTranscribed;
            PipelineEvents.OnTranslated    += OnTranslated;
            PipelineEvents.OnGlossList     += OnGlossList;
            PipelineEvents.OnPlaybackStart += OnPlaybackStart;
            PipelineEvents.OnPlaybackEnd   += OnPlaybackEnd;
            PipelineEvents.OnError         += OnError;
        }

        private void OnDisable()
        {
            PipelineEvents.OnInputReady    -= OnInputReady;
            PipelineEvents.OnTranscribed   -= OnTranscribed;
            PipelineEvents.OnTranslated    -= OnTranslated;
            PipelineEvents.OnGlossList     -= OnGlossList;
            PipelineEvents.OnPlaybackStart -= OnPlaybackStart;
            PipelineEvents.OnPlaybackEnd   -= OnPlaybackEnd;
            PipelineEvents.OnError         -= OnError;
        }

        private Trace T(string id)
        {
            if (!_traces.TryGetValue(id, out var tr))
            {
                tr = new Trace
                {
                    sessionId = id,
                    startedUtc = DateTime.UtcNow.ToString("o"),
                    appVersion = Application.version,
                    unityVersion = Application.unityVersion,
                    platform = Application.platform.ToString(),
                    deviceModel = SystemInfo.deviceModel,
                    systemLanguage = Application.systemLanguage.ToString()
                };
                _traces[id] = tr;
            }
            return tr;
        }

        private void EndAndSave(string id)
        {
            if (!enabledRecording) return;
            if (!_traces.TryGetValue(id, out var tr)) return;

            tr.endedUtc = DateTime.UtcNow.ToString("o");
            var json = JsonUtility.ToJson(tr, prettyPrint: true);

            try
            {
                var dir = Path.Combine(Application.persistentDataPath, folderName);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                // mask invalid chars
                var safe = id;
                foreach (var c in Path.GetInvalidFileNameChars()) safe = safe.Replace(c, '_');
                var path = Path.Combine(dir, $"{safe}.json");
                File.WriteAllText(path, json);
                Debug.Log($"[TraceRecorder] Saved trace → {path}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TraceRecorder] Save failed: {e.Message}");
            }
            finally
            {
                _traces.Remove(id);
            }
        }

        // ---- events ----
        private void OnInputReady(PipelineEvents.InputReadyArgs a)
        {
            if (!enabledRecording) return;
            var tr = T(a.SessionId);
            tr.detectedLanguage = a.Language;
            tr.originalText = a.OriginalText;
            // tr.audioSeconds will be filled when Transcribed arrives
            tr.tInput = DateTime.UtcNow.ToString("o");
        }

        private void OnTranscribed(PipelineEvents.TranscribedArgs a)
        {
            if (!enabledRecording) return;
            var tr = T(a.SessionId);
            tr.transcript = a.Transcript;
            tr.sttLanguage = a.Language;
            tr.tSTT = DateTime.UtcNow.ToString("o");
        }
        private void OnTranslated(PipelineEvents.TranslatedArgs a)
        {
            if (!enabledRecording) return;
            var tr = T(a.SessionId);
            tr.translatedText = a.TranslatedText;
            tr.srcLang = a.SourceLanguage;
            tr.dstLang = a.TargetLanguage;
            tr.tTR = DateTime.UtcNow.ToString("o");
        }
        private void OnGlossList(PipelineEvents.GlossListArgs a)
        {
            if (!enabledRecording) return;
            var tr = T(a.SessionId);
            tr.glosses = new List<string>(a.GlossesNormalized ?? new List<string>());
            tr.tGX = DateTime.UtcNow.ToString("o");
        }
        private void OnPlaybackStart(PipelineEvents.PlaybackArgs a)
        {
            if (!enabledRecording) return;
            var tr = T(a.SessionId);
            tr.keys = new List<string>(a.Keys ?? Array.Empty<string>());
            tr.tPBStart = DateTime.UtcNow.ToString("o");
        }
        private void OnPlaybackEnd(PipelineEvents.PlaybackArgs a)
        {
            if (!enabledRecording) return;
            var tr = T(a.SessionId);
            tr.tPBEnd = DateTime.UtcNow.ToString("o");
            EndAndSave(a.SessionId);
        }
        private void OnError(PipelineEvents.ErrorArgs a)
        {
            if (!enabledRecording) return;
            var tr = T(a.SessionId);
            tr.errorStage = a.Stage;
            tr.errorMessage = a.Message;
            tr.tPBEnd = DateTime.UtcNow.ToString("o");
            EndAndSave(a.SessionId);
        }
    }
}
