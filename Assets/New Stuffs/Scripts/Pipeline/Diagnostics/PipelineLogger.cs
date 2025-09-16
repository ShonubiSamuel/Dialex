using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace YourApp.Signs.Pipeline.Diagnostics
{
    [DisallowMultipleComponent]
    public class PipelineLogger : MonoBehaviour
    {
        [Header("Output")]
        public bool logToConsole = true;
        public bool logToFile = true;
        public string folderName = "Logs";
        public string filePrefix = "pipeline";
        public bool appendDateToFile = true;

        private string _filePath;
        private readonly Dictionary<string, Timeline> _t = new();

        [Serializable]
        private class Line
        {
            public string timeUtc;
            public string stage;          // InputReady, Transcribed, Translated, GlossList, PlaybackStart, PlaybackEnd, Error
            public string sessionId;
            public string note;

            // snapshot durations (ms) when this event arrived
            public double? stt;
            public double? translate;
            public double? gloss;
            public double? toPlayback;
            public double? playback;
        }

        private class Timeline
        {
            public DateTime? tInput, tSTT, tTR, tGX, tPBStart, tPBEnd;
            public double? DStt => Delta(tInput, tSTT);
            public double? DTr  => Delta(tSTT ?? tInput, tTR);
            public double? DGx  => Delta(tTR   ?? tSTT ?? tInput, tGX);
            public double? DToPb=> Delta(tGX   ?? tTR ?? tSTT ?? tInput, tPBStart);
            public double? DPb  => Delta(tPBStart, tPBEnd);
            private static double? Delta(DateTime? a, DateTime? b)
                => (a.HasValue && b.HasValue) ? (b.Value - a.Value).TotalMilliseconds : (double?)null;
        }

        private void OnEnable()
        {
            if (logToFile) InitFile();

            PipelineEvents.OnInputReady    += E_InputReady;
            PipelineEvents.OnTranscribed   += E_Transcribed;
            PipelineEvents.OnTranslated    += E_Translated;
            PipelineEvents.OnGlossList     += E_GlossList;
            PipelineEvents.OnPlaybackStart += E_PlaybackStart;
            PipelineEvents.OnPlaybackEnd   += E_PlaybackEnd;
            PipelineEvents.OnError         += E_Error;
        }

        private void OnDisable()
        {
            PipelineEvents.OnInputReady    -= E_InputReady;
            PipelineEvents.OnTranscribed   -= E_Transcribed;
            PipelineEvents.OnTranslated    -= E_Translated;
            PipelineEvents.OnGlossList     -= E_GlossList;
            PipelineEvents.OnPlaybackStart -= E_PlaybackStart;
            PipelineEvents.OnPlaybackEnd   -= E_PlaybackEnd;
            PipelineEvents.OnError         -= E_Error;
        }

        private void InitFile()
        {
            try
            {
                var dir = Path.Combine(Application.persistentDataPath, folderName);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var name = appendDateToFile
                    ? $"{filePrefix}_{DateTime.UtcNow:yyyyMMdd}.log"
                    : $"{filePrefix}.log";
                _filePath = Path.Combine(dir, name);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PipelineLogger] File init failed: {e.Message}");
                logToFile = false;
            }
        }

        private Timeline T(string id)
        {
            if (string.IsNullOrEmpty(id)) id = "unknown";
            if (!_t.TryGetValue(id, out var tl)) { tl = new Timeline(); _t[id] = tl; }
            return tl;
        }

        private void Write(string stage, string sessionId, string note = null)
        {
            var tl = T(sessionId);
            var line = new Line
            {
                timeUtc = DateTime.UtcNow.ToString("o"),
                stage = stage,
                sessionId = sessionId,
                note = note,
                stt = tl.DStt,
                translate = tl.DTr,
                gloss = tl.DGx,
                toPlayback = tl.DToPb,
                playback = tl.DPb
            };
            var json = JsonUtility.ToJson(line);

            if (logToConsole) Debug.Log(json);
            if (logToFile && !string.IsNullOrEmpty(_filePath))
            {
                try { File.AppendAllText(_filePath, json + Environment.NewLine); }
                catch (Exception e) { Debug.LogWarning($"[PipelineLogger] Write failed: {e.Message}"); }
            }
        }

        // ---- Event handlers ----
        private void E_InputReady(PipelineEvents.InputReadyArgs a)        { var tl = T(a.SessionId); tl.tInput = DateTime.UtcNow;      Write("InputReady",    a.SessionId, $"lang={a.Language}"); }
        private void E_Transcribed(PipelineEvents.TranscribedArgs a)      { var tl = T(a.SessionId); tl.tSTT = DateTime.UtcNow;        Write("Transcribed",  a.SessionId, $"{a.AudioSeconds:0.00}s"); }
        private void E_Translated(PipelineEvents.TranslatedArgs a)        { var tl = T(a.SessionId); tl.tTR = DateTime.UtcNow;         Write("Translated",   a.SessionId, $"{a.SourceLanguage}->{a.TargetLanguage}"); }
        private void E_GlossList(PipelineEvents.GlossListArgs a)          { var tl = T(a.SessionId); tl.tGX = DateTime.UtcNow;         Write("GlossList",    a.SessionId, $"{(a.GlossesNormalized?.Count ?? 0)} items"); }
        private void E_PlaybackStart(PipelineEvents.PlaybackArgs a)       { var tl = T(a.SessionId); tl.tPBStart = DateTime.UtcNow;    Write("PlaybackStart",a.SessionId, $"{(a.Keys?.Count ?? 0)} keys"); }
        private void E_PlaybackEnd(PipelineEvents.PlaybackArgs a)         { var tl = T(a.SessionId); tl.tPBEnd = DateTime.UtcNow;      Write("PlaybackEnd",  a.SessionId); }
        private void E_Error(PipelineEvents.ErrorArgs a)
        {
            var tl = T(a.SessionId); tl.tPBEnd = DateTime.UtcNow;
            Write("Error", a.SessionId, $"{a.Stage}: {a.Message}");
        }
    }
}
