using System;
using System.Collections.Generic;
using UnityEngine;
using YourApp.Signs.Pipeline.Scheduling;

namespace YourApp.Signs.UI
{
    /// <summary>
    /// Lightweight debug HUD for your sign pipeline.
    /// Subscribes to PipelineEvents and exposes public callbacks you can
    /// wire from SignQueueController/SignPlaybackController (OnSignStarted/Completed).
    /// Toggle with F1 by default.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlaybackHUD : MonoBehaviour
    {
        [Header("References (optional)")]
        public SignLibraryLoader loader;             // for showing mode
        public PreloadHints preloadHints;            // to show lookahead if present

        [Header("Display")]
        public bool visible = true;
        public KeyCode toggleKey = KeyCode.F1;
        public Vector2 anchor = new Vector2(16, 16);
        public float width = 520f;
        public int lineHeight = 18;
        public int fontSize = 12;

        // ---- runtime state ----
        private string _currentKey;
        private string _nextKey;
        private List<string> _scheduled = new List<string>();

        private string _lastTranscript;
        private string _lastTranslated;
        private List<string> _lastGlosses = new List<string>();

        // timing
        private string _sessionId;
        private DateTime? tInput, tTranscribed, tTranslated, tGloss, tPlaybackStart, tPlaybackEnd;

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

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey)) visible = !visible;

            // keep nextKey in sync with scheduled list
            if (!string.IsNullOrEmpty(_currentKey) && _scheduled.Count > 0)
            {
                int idx = _scheduled.IndexOf(_currentKey);
                _nextKey = (idx >= 0 && idx + 1 < _scheduled.Count) ? _scheduled[idx + 1] : null;
            }
            else if (_scheduled.Count > 0)
            {
                _nextKey = _scheduled[0];
            }
        }

        // --- public hooks (wire these from your queue/playback if available) ---
        public void OnSignStarted(string normalizedKey)
        {
            _currentKey = normalizedKey;
        }

        public void OnSignCompleted(string normalizedKey)
        {
            if (_currentKey == normalizedKey) _currentKey = null;
        }

        // --- pipeline event listeners ---
        private void OnInputReady(PipelineEvents.InputReadyArgs a)
        {
            _sessionId = a.SessionId;
            tInput = DateTime.UtcNow;
            _lastTranscript = a.OriginalText;
            _lastTranslated = null;
            _lastGlosses.Clear();
            _scheduled.Clear();
            _currentKey = null;
            _nextKey = null;
        }

        private void OnTranscribed(PipelineEvents.TranscribedArgs a)
        {
            if (_sessionId != a.SessionId) _sessionId = a.SessionId;
            tTranscribed = DateTime.UtcNow;
            _lastTranscript = a.Transcript;
            print("Transcript  " +a.Transcript);
        }

        private void OnTranslated(PipelineEvents.TranslatedArgs a)
        {
            if (_sessionId != a.SessionId) _sessionId = a.SessionId;
            tTranslated = DateTime.UtcNow;
            _lastTranslated = a.TranslatedText;
            print("TranslatedText " +a.TranslatedText);
        }

        private void OnGlossList(PipelineEvents.GlossListArgs a)
        {
            if (_sessionId != a.SessionId) _sessionId = a.SessionId;
            tGloss = DateTime.UtcNow;
            _lastGlosses = a.GlossesNormalized ?? new List<string>();
            print("Glosses Normal " +a.GlossesNormalized);
        }

        private void OnPlaybackStart(PipelineEvents.PlaybackArgs a)
        {
            if (_sessionId != a.SessionId) _sessionId = a.SessionId;
            tPlaybackStart = DateTime.UtcNow;
            _scheduled = new List<string>(a.Keys ?? Array.Empty<string>());
            // seed current if your queue doesn't call OnSignStarted:
            if (_scheduled.Count > 0 && string.IsNullOrEmpty(_currentKey))
                _currentKey = _scheduled[0];
        }

        private void OnPlaybackEnd(PipelineEvents.PlaybackArgs a)
        {
            tPlaybackEnd = DateTime.UtcNow;
            _currentKey = null;
            _nextKey = null;
            _scheduled.Clear();
        }

        private void OnError(PipelineEvents.ErrorArgs a)
        {
            // mark end so latency can be inspected even on failure
            tPlaybackEnd = DateTime.UtcNow;
        }

        // --- GUI ---
        private void OnGUI()
        {
            if (!visible) return;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                wordWrap = true
            };

            var area = new Rect(anchor.x, anchor.y, width, Screen.height);
            GUILayout.BeginArea(area, GUI.skin.box);

            Label(style, $"<b>Sign Pipeline HUD</b>  {(string.IsNullOrEmpty(_sessionId) ? "" : $"#{_sessionId[..6]}")}");

            // Current / Next
            Label(style, $"Current: <b>{_currentKey ?? "—"}</b>");
            Label(style, $"Next:    {_nextKey ?? "—"}");
            Label(style, $"Queue:   {(_scheduled.Count > 0 ? string.Join(", ", _scheduled) : "—")}");

            // Loader status
            if (loader != null)
                Label(style, $"Loader:  {loader.mode}  (cache: on)");
            if (preloadHints != null)
                Label(style, $"Preload lookahead: {preloadHints.lookahead}");

            GUILayout.Space(6);

            // Text / Gloss snapshot
            if (!string.IsNullOrEmpty(_lastTranscript))
                Label(style, $"Transcript: {_lastTranscript}");
            if (!string.IsNullOrEmpty(_lastTranslated))
                Label(style, $"Translated: {_lastTranslated}");
            if (_lastGlosses != null && _lastGlosses.Count > 0)
                Label(style, $"Glosses:   {string.Join(" · ", _lastGlosses)}");

            GUILayout.Space(6);
            // Latencies (ms)
            if (tInput.HasValue)
            {
                var lSTT  = DeltaMs(tInput, tTranscribed);
                var lTR   = DeltaMs(tTranscribed ?? tInput, tTranslated);
                var lGX   = DeltaMs(tTranslated ?? tTranscribed ?? tInput, tGloss);
                var lPlay = DeltaMs(tGloss ?? tTranslated ?? tTranscribed ?? tInput, tPlaybackStart);
                var lEnd  = DeltaMs(tPlaybackStart, tPlaybackEnd);

                Label(style, "<b>Latency (ms)</b>");
                Label(style, $"STT: {Fmt(lSTT)}  |  Translate: {Fmt(lTR)}  |  Gloss: {Fmt(lGX)}");
                Label(style, $"To Playback: {Fmt(lPlay)}  |  Playback Duration: {Fmt(lEnd)}");
            }

            GUILayout.EndArea();
        }

        private static void Label(GUIStyle s, string text)
        {
            GUILayout.Label(text, s, GUILayout.Height(s.fontSize + 6));
        }

        private static double? DeltaMs(DateTime? a, DateTime? b)
        {
            if (!a.HasValue || !b.HasValue) return null;
            return (b.Value - a.Value).TotalMilliseconds;
        }

        private static string Fmt(double? ms) => ms.HasValue ? ms.Value.ToString("0") : "—";
    }
}
