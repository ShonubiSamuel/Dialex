using System;
using System.Collections.Generic;
using UnityEngine;

namespace YourApp.Signs.Pipeline.Diagnostics
{
    [DisallowMultipleComponent]
    public class MetricsCollector : MonoBehaviour
    {
        [Serializable] public class Stat
        {
            public int count;
            public double sum;
            public double min = double.PositiveInfinity;
            public double max = double.NegativeInfinity;

            public void Add(double v)
            {
                count++;
                sum += v;
                if (v < min) min = v;
                if (v > max) max = v;
            }
            public double Avg => count > 0 ? sum / count : 0;
        }

        [Header("Expose Singleton (optional)")]
        public static MetricsCollector Instance;

        // stage latency stats (ms)
        public Stat stt = new();
        public Stat translate = new();
        public Stat gloss = new();
        public Stat toPlayback = new();
        public Stat playback = new();

        // counts
        public int sessions;
        public int errors;
        public int sttCacheHits, sttCacheMisses;
        public int trCacheHits, trCacheMisses;

        private readonly Dictionary<string, Timeline> _t = new();

        private class Timeline
        {
            public DateTime? tInput, tSTT, tTR, tGX, tPBStart, tPBEnd;
        }

        private void Awake() => Instance = this;

        private void OnEnable()
        {
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

        private Timeline T(string id)
        {
            if (string.IsNullOrEmpty(id)) id = Guid.NewGuid().ToString("N");
            if (!_t.TryGetValue(id, out var tl)) { tl = new Timeline(); _t[id] = tl; }
            return tl;
        }

        private static double? Delta(DateTime? a, DateTime? b)
            => (a.HasValue && b.HasValue) ? (b.Value - a.Value).TotalMilliseconds : (double?)null;

        // ---- Public hooks for caches (optional from your STT/Translation caches) ----
        public void ReportSttCache(bool hit) { if (hit) sttCacheHits++; else sttCacheMisses++; }
        public void ReportTranslationCache(bool hit) { if (hit) trCacheHits++; else trCacheMisses++; }

        // ---- Events ----
        private void E_InputReady(PipelineEvents.InputReadyArgs a)
        {
            sessions++;
            var tl = T(a.SessionId);
            tl.tInput = DateTime.UtcNow;
        }
        private void E_Transcribed(PipelineEvents.TranscribedArgs a)
        {
            var tl = T(a.SessionId);
            tl.tSTT = DateTime.UtcNow;
            var ms = Delta(tl.tInput, tl.tSTT);
            if (ms.HasValue) stt.Add(ms.Value);
        }
        private void E_Translated(PipelineEvents.TranslatedArgs a)
        {
            var tl = T(a.SessionId);
            tl.tTR = DateTime.UtcNow;
            var ms = Delta(tl.tSTT ?? tl.tInput, tl.tTR);
            if (ms.HasValue) translate.Add(ms.Value);
        }
        private void E_GlossList(PipelineEvents.GlossListArgs a)
        {
            var tl = T(a.SessionId);
            tl.tGX = DateTime.UtcNow;
            var ms = Delta(tl.tTR ?? tl.tSTT ?? tl.tInput, tl.tGX);
            if (ms.HasValue) gloss.Add(ms.Value);
        }
        private void E_PlaybackStart(PipelineEvents.PlaybackArgs a)
        {
            var tl = T(a.SessionId);
            tl.tPBStart = DateTime.UtcNow;
            var ms = Delta(tl.tGX ?? tl.tTR ?? tl.tSTT ?? tl.tInput, tl.tPBStart);
            if (ms.HasValue) toPlayback.Add(ms.Value);
        }
        private void E_PlaybackEnd(PipelineEvents.PlaybackArgs a)
        {
            var tl = T(a.SessionId);
            tl.tPBEnd = DateTime.UtcNow;
            var ms = Delta(tl.tPBStart, tl.tPBEnd);
            if (ms.HasValue) playback.Add(ms.Value);
        }
        private void E_Error(PipelineEvents.ErrorArgs a)
        {
            errors++;
            var tl = T(a.SessionId);
            tl.tPBEnd = DateTime.UtcNow; // close the loop to keep time series consistent
        }

        // ---- Quick debug readout in Editor (optional) ----
#if UNITY_EDITOR
        private void OnGUI()
        {
            const int pad = 8;
            var rect = new Rect(12, Screen.height - 160, 420, 148);
            GUI.Box(rect, "Metrics");
            GUILayout.BeginArea(new Rect(rect.x + pad, rect.y + 20, rect.width - 2*pad, rect.height - 30));
            GUILayout.Label($"Sessions: {sessions}   Errors: {errors}");
            GUILayout.Label($"STT       avg {stt.Avg:0}ms   min {stt.min:0}   max {stt.max:0}   n={stt.count}");
            GUILayout.Label($"Translate avg {translate.Avg:0}ms   min {translate.min:0}   max {translate.max:0}   n={translate.count}");
            GUILayout.Label($"Gloss     avg {gloss.Avg:0}ms   min {gloss.min:0}   max {gloss.max:0}   n={gloss.count}");
            GUILayout.Label($"ToPlay    avg {toPlayback.Avg:0}ms   min {toPlayback.min:0}   max {toPlayback.max:0}   n={toPlayback.count}");
            GUILayout.Label($"Playback  avg {playback.Avg:0}ms   min {playback.min:0}   max {playback.max:0}   n={playback.count}");
            GUILayout.Label($"STT cache hits {sttCacheHits} / miss {sttCacheMisses}   |   TR hits {trCacheHits} / miss {trCacheMisses}");
            GUILayout.EndArea();
        }
#endif
    }
}
