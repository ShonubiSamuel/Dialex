using System.Collections.Generic;
using UnityEngine;

namespace YourApp.Signs.UI
{
    public class SubtitleOverlay : MonoBehaviour
    {
        public enum Source { Gloss, Transcript }

        [Header("Mode")]
        public Source source = Source.Gloss;

        [Header("Layout")]
        public bool visible = true;
        public KeyCode toggleKey = KeyCode.F2;
        public int fontSize = 28;
        public float bottomMargin = 32f;
        public float maxWidthPercent = 0.8f;

        [Header("Fades")]
        public float fadeIn = 0.08f;
        public float hold = 1.2f;
        public float fadeOut = 0.12f;

        [Header("Colors")]
        public Color currentColor = Color.white;
        public Color nextColor = new Color(1f, 1f, 1f, 0.55f);
        public Color shadow = new Color(0f, 0f, 0f, 0.75f);

        // state
        private string _current;
        private string _next;
        private float _phaseT; // 0..(fadeIn+hold+fadeOut)
        private bool _animate;

        private List<string> _scheduled = new List<string>();
        private string _lastTranscript;

        private void OnEnable()
        {
            PipelineEvents.OnPlaybackStart += OnPlaybackStart;
            PipelineEvents.OnGlossList     += OnGlossList;
            PipelineEvents.OnTranscribed   += OnTranscribed;
        }

        private void OnDisable()
        {
            PipelineEvents.OnPlaybackStart -= OnPlaybackStart;
            PipelineEvents.OnGlossList     -= OnGlossList;
            PipelineEvents.OnTranscribed   -= OnTranscribed;
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey)) visible = !visible;

            if (source == Source.Gloss)
            {
                // advance animation timer
                if (_animate)
                {
                    _phaseT += Time.deltaTime;
                    float total = Mathf.Max(0.0001f, fadeIn + hold + fadeOut);
                    if (_phaseT >= total) { _phaseT = 0f; _animate = false; }
                }

                // keep next in sync
                if (!string.IsNullOrEmpty(_current) && _scheduled.Count > 0)
                {
                    int idx = _scheduled.IndexOf(_current);
                    _next = (idx >= 0 && idx + 1 < _scheduled.Count) ? _scheduled[idx + 1] : null;
                }
            }
        }

        // public hook from queue/playback (recommended)
        public void OnSignStarted(string normalizedKey)
        {
            _current = normalizedKey;
            _animate = true;
            _phaseT = 0f;
        }

        // events
        private void OnPlaybackStart(PipelineEvents.PlaybackArgs a)
        {
            _scheduled = new List<string>(a.Keys ?? System.Array.Empty<string>());
            if (_scheduled.Count > 0 && string.IsNullOrEmpty(_current))
                OnSignStarted(_scheduled[0]);
        }

        private void OnGlossList(PipelineEvents.GlossListArgs a)
        {
            _scheduled = new List<string>(a.GlossesNormalized ?? new List<string>());
        }

        private void OnTranscribed(PipelineEvents.TranscribedArgs a)
        {
            _lastTranscript = a.Transcript;
        }

        private void OnGUI()
        {
            if (!visible) return;

            var width = Mathf.RoundToInt(Screen.width * Mathf.Clamp01(maxWidthPercent));
            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.LowerCenter,
                fontSize = fontSize,
                wordWrap = true,
                richText = true
            };

            var rect = new Rect((Screen.width - width) * 0.5f, Screen.height - bottomMargin - 200, width, 200);

            if (source == Source.Transcript)
            {
                DrawShadowed(rect, _lastTranscript ?? "", currentColor, style);
            }
            else
            {
                // alpha based on fade phases
                float total = Mathf.Max(0.0001f, fadeIn + hold + fadeOut);
                float aCur;
                if (_phaseT < fadeIn) aCur = Mathf.InverseLerp(0f, fadeIn, _phaseT);
                else if (_phaseT < fadeIn + hold) aCur = 1f;
                else aCur = 1f - Mathf.InverseLerp(fadeIn + hold, total, _phaseT);

                var colCur = currentColor; colCur.a *= aCur;
                var colNext = nextColor;

                if (!string.IsNullOrEmpty(_current))
                    DrawShadowed(rect, _current, colCur, style);

                if (!string.IsNullOrEmpty(_next))
                {
                    var below = new Rect(rect.x, rect.y + fontSize * 1.4f, rect.width, rect.height);
                    DrawShadowed(below, _next, colNext, style);
                }
            }
        }

        private void DrawShadowed(Rect r, string text, Color color, GUIStyle style)
        {
            if (string.IsNullOrEmpty(text)) return;
            var old = GUI.color;

            // shadow
            GUI.color = new Color(shadow.r, shadow.g, shadow.b, shadow.a * color.a);
            var s1 = new Rect(r.x + 1, r.y + 1, r.width, r.height);
            GUI.Label(s1, text, style);

            // main
            GUI.color = color;
            GUI.Label(r, text, style);

            GUI.color = old;
        }
    }
}
