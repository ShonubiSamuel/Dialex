#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Window that listens to PipelineEvents and shows per-session timings.
/// Tools → Sign Pipeline → Latency Profiler
/// </summary>
public class LatencyProfilerWindow : EditorWindow
{
    private class TL
    {
        public string session;
        public DateTime? tIn, tSTT, tTR, tGX, tPBs, tPBe;
        public string lastNote;
    }

    private readonly Dictionary<string, TL> _map = new();
    private Vector2 _scroll;
    private double _lastRepaint;

    [MenuItem("Tools/Sign Pipeline/Latency Profiler")]
    public static void Open() => GetWindow<LatencyProfilerWindow>("Latency Profiler");

    private void OnEnable()
    {
        PipelineEvents.OnInputReady    += E_In;
        PipelineEvents.OnTranscribed   += E_STT;
        PipelineEvents.OnTranslated    += E_TR;
        PipelineEvents.OnGlossList     += E_GX;
        PipelineEvents.OnPlaybackStart += E_PBs;
        PipelineEvents.OnPlaybackEnd   += E_PBe;
        PipelineEvents.OnError         += E_Err;

        EditorApplication.update += AutoRepaint;
    }

    private void OnDisable()
    {
        PipelineEvents.OnInputReady    -= E_In;
        PipelineEvents.OnTranscribed   -= E_STT;
        PipelineEvents.OnTranslated    -= E_TR;
        PipelineEvents.OnGlossList     -= E_GX;
        PipelineEvents.OnPlaybackStart -= E_PBs;
        PipelineEvents.OnPlaybackEnd   -= E_PBe;
        PipelineEvents.OnError         -= E_Err;

        EditorApplication.update -= AutoRepaint;
    }

    private void AutoRepaint()
    {
        // repaint ~10 fps while open
        if (EditorApplication.timeSinceStartup - _lastRepaint > 0.1)
        {
            Repaint();
            _lastRepaint = EditorApplication.timeSinceStartup;
        }
    }

    private TL T(string id)
    {
        if (string.IsNullOrEmpty(id)) id = "unknown";
        if (!_map.TryGetValue(id, out var tl)) { tl = new TL { session = id }; _map[id] = tl; }
        return tl;
    }

    private static double? D(DateTime? a, DateTime? b)
        => (a.HasValue && b.HasValue) ? (b.Value - a.Value).TotalMilliseconds : (double?)null;

    private void OnGUI()
    {
        EditorGUILayout.Space();
        GUILayout.Label("Live Latency (ms) per session", EditorStyles.boldLabel);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        foreach (var kv in _map)
        {
            var tl = kv.Value;
            var stt = D(tl.tIn, tl.tSTT);
            var tr  = D(tl.tSTT ?? tl.tIn, tl.tTR);
            var gx  = D(tl.tTR  ?? tl.tSTT ?? tl.tIn, tl.tGX);
            var top = D(tl.tGX  ?? tl.tTR ?? tl.tSTT ?? tl.tIn, tl.tPBs);
            var pb  = D(tl.tPBs, tl.tPBe);

            using (new GUILayout.VerticalScope("box"))
            {
                GUILayout.Label($"Session: {tl.session}");
                GUILayout.Label($"STT: {Fmt(stt)}   |   Translate: {Fmt(tr)}   |   Gloss: {Fmt(gx)}   |   →Playback: {Fmt(top)}   |   Playback: {Fmt(pb)}");
                if (!string.IsNullOrEmpty(tl.lastNote))
                    EditorGUILayout.HelpBox(tl.lastNote, MessageType.None);
            }
        }
        EditorGUILayout.EndScrollView();

        if (_map.Count == 0)
            EditorGUILayout.HelpBox("No events yet. Enter Play Mode and run the pipeline.", MessageType.Info);
    }

    private static string Fmt(double? ms) => ms.HasValue ? ms.Value.ToString("0") : "—";

    // ---- Event taps ----
    private void E_In(PipelineEvents.InputReadyArgs a) { var t = T(a.SessionId); t.tIn  = DateTime.UtcNow; t.lastNote = $"lang={a.Language}  textLen={a.OriginalText?.Length ?? 0}"; }
    private void E_STT(PipelineEvents.TranscribedArgs a) { var t = T(a.SessionId); t.tSTT = DateTime.UtcNow; t.lastNote = $"STT {a.AudioSeconds:0.00}s"; }
    private void E_TR(PipelineEvents.TranslatedArgs a) { var t = T(a.SessionId); t.tTR  = DateTime.UtcNow; t.lastNote = $"{a.SourceLanguage}->{a.TargetLanguage}"; }
    private void E_GX(PipelineEvents.GlossListArgs a) { var t = T(a.SessionId); t.tGX  = DateTime.UtcNow; t.lastNote = $"glosses={a.GlossesNormalized?.Count ?? 0}"; }
    private void E_PBs(PipelineEvents.PlaybackArgs a) { var t = T(a.SessionId); t.tPBs = DateTime.UtcNow; t.lastNote = $"keys={a.Keys?.Count ?? 0}"; }
    private void E_PBe(PipelineEvents.PlaybackArgs a) { var t = T(a.SessionId); t.tPBe = DateTime.UtcNow; t.lastNote = "done"; }
    private void E_Err(PipelineEvents.ErrorArgs a)    { var t = T(a.SessionId); t.tPBe = DateTime.UtcNow; t.lastNote = $"ERROR {a.Stage}: {a.Message}"; }
}
#endif
